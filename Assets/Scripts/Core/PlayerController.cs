using UnityEngine;
using System.Collections;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    [Header("自动小跳")]
    public float jumpVelocity = 7f;
    public float hopCooldown = 0.35f;

    [Header("左右控制")]
    public float maxHorizontalSpeed = 8f;

    [Header("落地漏电")]
    public float dischargeRadius = 1f;
    public float selfBounceForce = 2f;
    public float dischargeCooldown = 0.15f;
    public float ringDuration = 0.35f;
    public Color dischargeColor = Color.yellow;

    Rigidbody2D rb;
    Collider2D col;
    float lastJumpTime = -999f;
    float lastDischargeTime = -999f;
    bool wasAirborne;
    float externalForceLockUntil = -999f;

    // 联机输入：客户端→ServerRpc→服务端存储
    float networkedInput = 0f;

    static readonly Color[] PlayerColors = { new Color(0.2f, 0.6f, 1f), new Color(1f, 0.5f, 0.2f) };

    public void SetExternalForceLock(float duration)
    {
        externalForceLockUntil = Time.time + duration;
    }

    public void ResetAirState()
    {
        wasAirborne = false;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 1.5f;

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null && collider.sharedMaterial == null)
        {
            PhysicsMaterial2D mat = new PhysicsMaterial2D("PlayerFrictionless");
            mat.friction = 0f;
            mat.bounciness = 0f;
            collider.sharedMaterial = mat;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 按客户端ID偏移出生点
        float xOff = (OwnerClientId == 0) ? -1f : 1f;
        if (IsServer)
        {
            transform.position = new Vector3(xOff, 1f, 0f);
            if (GamePhaseManager.Instance != null)
                GamePhaseManager.Instance.SetPlayerSpawn(new Vector3(xOff, 1f, 0f));
        }

        // 颜色
        int colorIdx = (int)(OwnerClientId % (ulong)PlayerColors.Length);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = PlayerColors[colorIdx];

        // 客户端：Kinematic，位置由 NetworkTransform 同步
        if (!IsServer)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        col = GetComponent<Collider2D>();

        // 布置阶段自我冻结，防止连接后立即跳跃
        if (GamePhaseManager.Instance != null && GamePhaseManager.Instance.Phase == GamePhase.Placement)
        {
            enabled = false;
            if (IsServer)
            {
                rb.bodyType = RigidbodyType2D.Static;
                rb.velocity = Vector2.zero;
            }
        }
    }

    void Update()
    {
        // 只有本地玩家读输入
        if (!IsOwner) return;

        float h = Input.GetAxis("Horizontal");
        if (Mathf.Abs(h - networkedInput) > 0.01f)
        {
            networkedInput = h;
            if (!IsServer)
                SubmitInputServerRpc(h);
        }
    }

    [ServerRpc]
    void SubmitInputServerRpc(float h)
    {
        networkedInput = h;
    }

    void FixedUpdate()
    {
        // 物理只在服务端跑
        if (!IsServer) return;

        bool grounded = CheckGrounded();

        if (Time.time < externalForceLockUntil)
        {
            if (!grounded) wasAirborne = true;
            else wasAirborne = false;
            return;
        }

        if (grounded && wasAirborne)
        {
            if (Time.time - lastDischargeTime >= dischargeCooldown)
            {
                Vector2 point = GetGroundContactPoint();
                PerformDischarge(point);
                lastDischargeTime = Time.time;
                if (Time.time < externalForceLockUntil) return;
            }
            wasAirborne = false;
        }

        if (!grounded) wasAirborne = true;

        if (grounded && Time.time - lastJumpTime > hopCooldown)
        {
            Vector2 vel = rb.velocity;
            vel.y = jumpVelocity;
            rb.velocity = vel;
            lastJumpTime = Time.time;
        }

        float moveInput = networkedInput;
        Vector2 velocity = rb.velocity;
        if (Mathf.Abs(moveInput) > 0.01f && !IsWallInDirection(moveInput))
        {
            velocity.x = moveInput * maxHorizontalSpeed;
        }
        else if (Time.time < externalForceLockUntil + 0.4f)
        {
            velocity.x *= 0.92f;
        }
        else
        {
            velocity.x = 0f;
        }
        rb.velocity = velocity;

        if (grounded) wasAirborne = false;
    }

    bool IsWallInDirection(float dir)
    {
        if (col == null) return false;
        float edgeX = dir > 0 ? col.bounds.max.x : col.bounds.min.x;
        Vector2 origin = new Vector2(edgeX + (dir > 0 ? 0.01f : -0.01f), transform.position.y);
        Vector2 direction = new Vector2(Mathf.Sign(dir), 0f);
        int orig = gameObject.layer;
        gameObject.layer = 2;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, 0.1f);
        gameObject.layer = orig;
        return hit.collider != null && Mathf.Abs(hit.normal.x) > 0.5f;
    }

    bool CheckGrounded()
    {
        if (col == null) return false;
        float bottomY = col.bounds.min.y;
        float rayLen = 0.15f;
        Vector2[] origins = {
            new Vector2(col.bounds.min.x, bottomY - 0.01f),
            new Vector2(transform.position.x, bottomY - 0.01f),
            new Vector2(col.bounds.max.x, bottomY - 0.01f)
        };
        int orig = gameObject.layer;
        gameObject.layer = 2;
        bool hit = false;
        foreach (var o in origins)
        {
            RaycastHit2D[] results = Physics2D.RaycastAll(o, Vector2.down, rayLen);
            foreach (var r in results)
            {
                if (r.collider.GetComponentInParent<WaterPuddle>() != null) continue;
                if (r.collider.GetComponentInParent<ConductiveShard>() != null) continue;
                if (r.normal.y < 0.5f) continue;
                hit = true;
                break;
            }
            if (hit) break;
        }
        gameObject.layer = orig;
        return hit;
    }

    Vector2 GetGroundContactPoint()
    {
        if (col == null) return transform.position;
        float bottomY = col.bounds.min.y;
        Vector2[] origins = {
            new Vector2(col.bounds.min.x, bottomY - 0.01f),
            new Vector2(transform.position.x, bottomY - 0.01f),
            new Vector2(col.bounds.max.x, bottomY - 0.01f)
        };
        int orig = gameObject.layer;
        gameObject.layer = 2;
        Vector2 result = new Vector2(transform.position.x, bottomY);
        foreach (var o in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(o, Vector2.down, 0.2f);
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
        gameObject.layer = orig;
        return result;
    }

    void PerformDischarge(Vector2 point)
    {
        float intensity = 1f;
        Vector2 playerCenter = transform.position;

        // 放电圆环视觉 → 所有客户端（含服务端）
        if (GamePhaseManager.Instance != null)
            GamePhaseManager.Instance.SpawnRingClientRpc(playerCenter, dischargeRadius, dischargeColor, ringDuration);
        else
            StartCoroutine(SpawnRingLocal(playerCenter, dischargeRadius));

        // 导电体引爆（仅服务端）
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerCenter, dischargeRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
                target.OnDischarge(playerCenter, intensity, 0);
        }

        // 水潭检测
        WaterPuddle[] puddles = Object.FindObjectsOfType<WaterPuddle>();
        foreach (var puddle in puddles)
        {
            Vector2 pc = puddle.transform.position;
            float pr = Mathf.Max(puddle.transform.localScale.x, puddle.transform.localScale.y) * 0.5f;
            if (Vector2.Distance(playerCenter, pc) <= pr + dischargeRadius)
                puddle.OnPlayerEnter(playerCenter, intensity);
        }

        rb.AddForce(Vector2.up * selfBounceForce, ForceMode2D.Impulse);
    }

    IEnumerator SpawnRingLocal(Vector2 center, float maxRadius)
    {
        // 单机回退（无 NetworkManager 时）
        GameObject ring = new GameObject("DischargeRing");
        ring.transform.position = center;
        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = GamePhaseManager.CreateRingSprite();
        sr.color = dischargeColor;
        sr.sortingOrder = 10;
        float elapsed = 0f;
        while (elapsed < ringDuration)
        {
            float t = elapsed / ringDuration;
            ring.transform.localScale = Vector3.one * (Mathf.Lerp(0.1f, maxRadius, t) * 2f);
            Color c = dischargeColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(ring);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dischargeRadius);
    }
}
