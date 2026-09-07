using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("自动小跳")]
    [Tooltip("每次起跳时给予的向上速度")]
    public float jumpVelocity = 7f;
    [Tooltip("两次起跳之间的最小间隔（秒）")]
    public float hopCooldown = 0.35f;

    [Header("左右控制")]
    [Tooltip("空中最大左右速度")]
    public float maxHorizontalSpeed = 8f;

    [Header("落地漏电")]
    [Tooltip("漏电影响半径")]
    public float dischargeRadius = 3f;
    [Tooltip("漏电时给自身的额外上推力")]
    public float selfBounceForce = 2f;
    [Tooltip("两次放电之间的最小间隔（秒）")]
    public float dischargeCooldown = 0.15f;
    [Tooltip("扩散圆环持续时间（秒）")]
    public float ringDuration = 0.35f;
    public Color dischargeColor = Color.yellow;

    Rigidbody2D rb;
    float lastJumpTime = -999f;
    float lastDischargeTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 1.5f;
    }

    void FixedUpdate()
    {
        bool grounded = CheckGrounded();

        // 自动小跳
        if (grounded && Time.time - lastJumpTime > hopCooldown)
        {
            Vector2 vel = rb.velocity;
            vel.y = jumpVelocity;
            rb.velocity = vel;
            lastJumpTime = Time.time;
        }

        // 空中左右控制
        float h = Input.GetAxis("Horizontal");
        Vector2 velocity = rb.velocity;
        velocity.x = h * maxHorizontalSpeed;
        rb.velocity = velocity;
    }

    /// <summary>
    /// 底部碰到任何物体时触发漏电
    /// </summary>
    void OnCollisionEnter2D(Collision2D col)
    {
        if (Time.time - lastDischargeTime < dischargeCooldown) return;

        foreach (ContactPoint2D contact in col.contacts)
        {
            // normal.y > 0.5 表示碰撞来自下方（玩家落在物体上方）
            if (contact.normal.y > 0.5f)
            {
                PerformDischarge(contact.point);
                lastDischargeTime = Time.time;
                break;
            }
        }
    }

    bool CheckGrounded()
    {
        // 检测任何固体表面，不限于 Ground 层
        float rayLength = 0.6f;
        return Physics2D.Raycast(transform.position, Vector2.down, rayLength, Physics2D.AllLayers);
    }

    void PerformDischarge(Vector2 point)
    {
        // 扩散圆环效果
        StartCoroutine(SpawnDischargeRing(point, dischargeRadius));

        // 给玩家一点反冲上推
        rb.AddForce(Vector2.up * selfBounceForce, ForceMode2D.Impulse);

        // 与可放电物体交互
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, dischargeRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
            {
                target.OnDischarge(point, 1f, 0);
            }
        }
    }

    IEnumerator SpawnDischargeRing(Vector2 center, float maxRadius)
    {
        GameObject ring = new GameObject("DischargeRing");
        ring.transform.position = center;

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = dischargeColor;
        sr.sortingOrder = 10;

        float elapsed = 0f;
        while (elapsed < ringDuration)
        {
            float t = elapsed / ringDuration;
            // 从 0 扩展到 maxRadius（sprite 外缘 = scale * 0.5，所以 scale = radius * 2）
            float radius = Mathf.Lerp(0.1f, maxRadius, t);
            ring.transform.localScale = Vector3.one * (radius * 2f);

            Color c = dischargeColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(ring);
    }

    static Sprite CreateRingSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.5f - 1f;
        float innerRadius = outerRadius - 6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = (dist <= outerRadius && dist >= innerRadius) ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dischargeRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.6f);
    }
}
