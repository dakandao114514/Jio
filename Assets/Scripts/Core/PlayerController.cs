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
    public float dischargeRadius = 1f;
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
    float externalForceLockUntil = -999f; // 被爆炸推力期间锁定控制

    /// <summary>
    /// 外部调用：锁定玩家控制一段时间（如被爆炸炸飞）
    /// </summary>
    public void SetExternalForceLock(float duration)
    {
        externalForceLockUntil = Time.time + duration;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 1.5f;

        // 无摩擦物理材质，防止撞墙时卡住上浮
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null && collider.sharedMaterial == null)
        {
            PhysicsMaterial2D mat = new PhysicsMaterial2D("PlayerFrictionless");
            mat.friction = 0f;
            mat.bounciness = 0f;
            collider.sharedMaterial = mat;
        }
    }

    void Start()
    {
        col = GetComponent<Collider2D>();
    }

    void FixedUpdate()
    {
        bool grounded = CheckGrounded();

        // 爆炸推力期间，不干预速度
        if (Time.time < externalForceLockUntil)
        {
            if (!grounded) wasAirborne = true;
            else wasAirborne = false;
            return;
        }

        // 落地瞬间放电：从空中→地面的那一刻
        if (grounded && wasAirborne)
        {
            if (Time.time - lastDischargeTime >= dischargeCooldown)
            {
                Vector2 point = GetGroundContactPoint();
                PerformDischarge(point);
                lastDischargeTime = Time.time;
                // 放电可能触发了爆炸，如果被锁定则本帧不再干预速度
                if (Time.time < externalForceLockUntil) return;
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

        // 左右控制：按方向键移动，松手停止
        float h = Input.GetAxis("Horizontal");
        Vector2 velocity = rb.velocity;
        if (Mathf.Abs(h) > 0.01f && !IsWallInDirection(h))
        {
            velocity.x = h * maxHorizontalSpeed;
        }
        else if (Time.time < externalForceLockUntil + 0.4f)
        {
            // 爆炸锁定结束后的余速窗口：缓慢衰减水平速度，让玩家感受到被炸飞的滑行
            velocity.x *= 0.92f;
        }
        else
        {
            velocity.x = 0f;
        }
        rb.velocity = velocity;

        // 撞墙时如果实际上站在地面上，强制标记为落地状态
        if (grounded)
        {
            wasAirborne = false;
        }
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
            // 用 RaycastAll 穿透水潭等非地面物体，找到真正的地面
            RaycastHit2D[] results = Physics2D.RaycastAll(origin, Vector2.down, rayLength);
            foreach (var r in results)
            {
                if (r.collider.GetComponentInParent<WaterPuddle>() != null) continue;
                if (r.collider.GetComponentInParent<ConductiveShard>() != null) continue;
                // 只认法线朝上的才是地面，避免侧壁误判
                if (r.normal.y < 0.5f) continue;
                hit = true;
                break;
            }
            if (hit) break;
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

        // 优先返回第一个打中的点（穿透水潭）
        Vector2 result = new Vector2(transform.position.x, bottomY);
        foreach (var origin in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 0.2f);
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<WaterPuddle>() != null) continue;
                if (h.collider.GetComponentInParent<ConductiveShard>() != null) continue;
                if (h.normal.y < 0.5f) continue;
                result = h.point;
                goto Found;
            }
        }
        Found:

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

        // 检测玩家是否在水潭附近：水潭无碰撞体，用距离检测
        WaterPuddle[] puddles = Object.FindObjectsOfType<WaterPuddle>();
        foreach (var puddle in puddles)
        {
            Vector2 puddleCenter = puddle.transform.position;
            float puddleRadius = Mathf.Max(puddle.transform.localScale.x, puddle.transform.localScale.y) * 0.5f;
            float distToPuddle = Vector2.Distance(playerCenter, puddleCenter);
            // 玩家在水潭范围内或放电半径内即触发水潭导电
            if (distToPuddle <= puddleRadius + dischargeRadius)
            {
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
