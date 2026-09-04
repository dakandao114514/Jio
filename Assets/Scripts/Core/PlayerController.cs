using UnityEngine;

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
    public float dischargeRadius = 1.2f;
    [Tooltip("漏电时给自身的额外上推力")]
    public float selfBounceForce = 2f;
    [Tooltip("地面层遮罩，留空则自动使用 Ground")]
    public LayerMask groundLayers;
    public Color dischargeColor = Color.yellow;

    Rigidbody2D rb;
    float lastJumpTime = -999f;
    bool wasAirborne;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 1.5f;

        if (groundLayers == 0)
            groundLayers = LayerMask.GetMask("Ground");
    }

    void FixedUpdate()
    {
        bool grounded = CheckGrounded();

        // 落地瞬间漏电
        if (grounded && wasAirborne)
        {
            Vector2 point = transform.position;
            if (Physics2D.Raycast(transform.position, Vector2.down, 1f, groundLayers))
            {
                point = Physics2D.Raycast(transform.position, Vector2.down, 1f, groundLayers).point;
            }

            PerformDischarge(point);
            wasAirborne = false;
        }

        // 自动小跳
        if (grounded && Time.time - lastJumpTime > hopCooldown)
        {
            Vector2 vel = rb.velocity;
            vel.y = jumpVelocity;
            rb.velocity = vel;

            lastJumpTime = Time.time;
            wasAirborne = true;
        }

        // 空中左右控制
        float h = Input.GetAxis("Horizontal");
        Vector2 velocity = rb.velocity;
        velocity.x = h * maxHorizontalSpeed;
        rb.velocity = velocity;
    }

    bool CheckGrounded()
    {
        float rayLength = 0.55f;
        return Physics2D.Raycast(transform.position, Vector2.down, rayLength, groundLayers);
    }

    void PerformDischarge(Vector2 point)
    {
        // 视觉反馈：落点放电光圈
        GameObject fx = new GameObject("LandingDischargeFX");
        fx.transform.position = point;
        fx.transform.localScale = Vector3.one * dischargeRadius * 0.5f;

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = dischargeColor;
        sr.sortingOrder = 10;

        Destroy(fx, 0.12f);

        // 给玩家一点反冲上推
        rb.AddForce(Vector2.up * selfBounceForce, ForceMode2D.Impulse);

        // 与可放电物体交互（包括触发器）
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

    static Sprite CreateCircleSprite()
    {
        Texture2D tex = new Texture2D(64, 64);
        Color clear = new Color(1f, 1f, 1f, 1f);
        Color[] pixels = new Color[64 * 64];
        Vector2 center = new Vector2(32, 32);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * 64 + x] = dist <= 30f ? clear : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dischargeRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.55f);
    }
}
