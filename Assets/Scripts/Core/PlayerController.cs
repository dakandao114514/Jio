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
    Collider2D col;
    float lastJumpTime = -999f;
    float lastDischargeTime = -999f;
    bool wasAirborne;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 1.5f;
    }

    void Start()
    {
        col = GetComponent<Collider2D>();
    }

    void FixedUpdate()
    {
        bool grounded = CheckGrounded();

        // 落地瞬间放电：从空中→地面的那一刻
        if (grounded && wasAirborne)
        {
            if (Time.time - lastDischargeTime >= dischargeCooldown)
            {
                Vector2 point = GetGroundContactPoint();
                PerformDischarge(point);
                lastDischargeTime = Time.time;
            }
            wasAirborne = false;
        }

        // 离地标记
        if (!grounded)
        {
            wasAirborne = true;
        }

        // 自动小跳
        if (grounded && Time.time - lastJumpTime > hopCooldown)
        {
            Vector2 vel = rb.velocity;
            vel.y = jumpVelocity;
            rb.velocity = vel;
            lastJumpTime = Time.time;
        }

        // 左右控制：检测前方是否有墙，有墙时不施加水平速度
        float h = Input.GetAxis("Horizontal");
        Vector2 velocity = rb.velocity;

        if (Mathf.Abs(h) > 0.01f && !IsWallInDirection(h))
        {
            velocity.x = h * maxHorizontalSpeed;
        }
        else
        {
            velocity.x = 0f;
        }
        rb.velocity = velocity;
    }

    bool IsWallInDirection(float dir)
    {
        if (col == null) return false;

        // 从碰撞体侧边边缘发出，不是从中心
        float edgeX = dir > 0 ? col.bounds.max.x : col.bounds.min.x;
        Vector2 origin = new Vector2(edgeX + (dir > 0 ? 0.01f : -0.01f), transform.position.y);
        Vector2 direction = new Vector2(Mathf.Sign(dir), 0f);
        float checkDist = 0.1f;

        int originalLayer = gameObject.layer;
        gameObject.layer = 2; // IgnoreRaycast
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, checkDist);
        gameObject.layer = originalLayer;

        return hit.collider != null && Mathf.Abs(hit.normal.x) > 0.5f;
    }

    bool CheckGrounded()
    {
        if (col == null) return false;

        float bottomY = col.bounds.min.y;
        float rayLength = 0.15f;

        // 左下角、中心、右下角三个点
        Vector2[] origins = new Vector2[]
        {
            new Vector2(col.bounds.min.x, bottomY - 0.01f),
            new Vector2(transform.position.x, bottomY - 0.01f),
            new Vector2(col.bounds.max.x, bottomY - 0.01f)
        };

        int originalLayer = gameObject.layer;
        gameObject.layer = 2; // IgnoreRaycast
        bool hit = false;
        foreach (var origin in origins)
        {
            if (Physics2D.Raycast(origin, Vector2.down, rayLength))
            {
                hit = true;
                break;
            }
        }
        gameObject.layer = originalLayer;
        return hit;
    }

    Vector2 GetGroundContactPoint()
    {
        if (col == null) return transform.position;

        float bottomY = col.bounds.min.y;

        // 左下角、中心、右下角三个点
        Vector2[] origins = new Vector2[]
        {
            new Vector2(col.bounds.min.x, bottomY - 0.01f),
            new Vector2(transform.position.x, bottomY - 0.01f),
            new Vector2(col.bounds.max.x, bottomY - 0.01f)
        };

        int originalLayer = gameObject.layer;
        gameObject.layer = 2;

        // 优先返回第一个打中的点
        Vector2 result = new Vector2(transform.position.x, bottomY);
        foreach (var origin in origins)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.2f);
            if (hit.collider != null)
            {
                result = hit.point;
                break;
            }
        }

        gameObject.layer = originalLayer;
        return result;
    }

    void PerformDischarge(Vector2 point)
    {
        float intensity = 1f;
        Vector2 playerCenter = transform.position;

        // 玩家放电圆环大小始终不变
        StartCoroutine(SpawnDischargeRing(playerCenter, dischargeRadius));

        // 玩家放电范围内的导电体直接引爆
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerCenter, dischargeRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
            {
                target.OnDischarge(playerCenter, intensity, 0);
            }
        }

        // 检测玩家是否站在水滩上，水潭整个变成导电源
        Collider2D[] puddleHits = Physics2D.OverlapCircleAll(playerCenter, dischargeRadius, Physics2D.AllLayers);
        foreach (var h in puddleHits)
        {
            WaterPuddle puddle = h.GetComponentInParent<WaterPuddle>();
            if (puddle != null)
            {
                // 通过水潭导电：以水潭边界引爆范围内的导电体
                puddle.OnPlayerEnter(playerCenter, intensity);
            }
        }

        // 给玩家一点反冲上推
        rb.AddForce(Vector2.up * selfBounceForce, ForceMode2D.Impulse);
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

        if (col != null)
        {
            Gizmos.color = Color.green;
            float bottomY = col.bounds.min.y;
            Gizmos.DrawLine(
                new Vector2(transform.position.x, bottomY - 0.01f),
                new Vector2(transform.position.x, bottomY - 0.16f)
            );
        }
    }
}
