using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveObject : MonoBehaviour, IConductive
{
    [Header("爆炸参数")]
    [Tooltip("基础爆炸推力")]
    public float baseExplosionForce = 15f;
    [Tooltip("爆炸影响半径（用于推开附近刚体和检测同步引爆的罐子）")]
    public float explosionRadius = 1.5f;

    [Header("碎片")]
    [Tooltip("爆炸后裂成的碎片数量")]
    public int shardCount = 5;
    [Tooltip("碎片飞出的初速度")]
    public float shardSpeed = 3f;
    [Tooltip("碎片存活时间（秒）")]
    public float shardLifetime = 2f;
    [Tooltip("碎片大小")]
    public float shardSize = 0.5f;

    [Header("连锁与过载")]
    [Tooltip("最大连锁层数，防止无限递归")]
    public int maxChainDepth = 8;
    [Tooltip("每层连锁额外增加的推力倍率")]
    public float overloadMultiplierPerChain = 0.5f;

    [Header("表现")]
    public Color dischargeColor = Color.yellow;

    Rigidbody2D rb;
    bool exploding;
    bool destroyed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnDischarge(Vector3 origin, float intensity, int chainDepth)
    {
        if (chainDepth > maxChainDepth) return;
        if (exploding) return;
        if (destroyed) return;
        exploding = true;
        destroyed = true;

        // 检测范围内是否有其他未引爆的罐子，统计同步爆炸数量
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, explosionRadius, Physics2D.AllLayers);
        int syncCount = 1; // 自身算1个
        foreach (var hit in nearby)
        {
            ConductiveObject other = hit.GetComponentInParent<ConductiveObject>();
            if (other != null && other != this && !other.exploding && !other.destroyed)
            {
                syncCount++;
                // 标记为同步引爆，防止重复计数
                other.exploding = true;
                other.destroyed = true;
            }
        }

        // 威力叠加：每个同步引爆的罐子增加一倍威力
        float overload = 1f + chainDepth * overloadMultiplierPerChain;
        float force = baseExplosionForce * intensity * overload * syncCount;

        Debug.Log($"[ConductiveObject] 爆炸！位置={transform.position}，同步引爆 {syncCount} 个罐子，force={force}");

        // 推开附近刚体
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            Rigidbody2D otherRb = hit.attachedRigidbody;
            if (otherRb != null && otherRb != rb)
            {
                Apply2DExplosionForce(otherRb, transform.position, explosionRadius, force);
            }
        }

        // 同步引爆的罐子也立即爆炸
        foreach (var hit in nearby)
        {
            ConductiveObject other = hit.GetComponentInParent<ConductiveObject>();
            if (other != null && other != this)
            {
                // 已标记的同步罐子直接触发爆炸效果（碎片+推力），但不再重复检测
                other.SyncExplode(chainDepth, intensity);
            }
        }

        // 裂成碎片飞出，碎片碰到其他罐子才连锁
        SpawnShards(chainDepth, intensity);

        // 销毁自身
        Destroy(gameObject, 0.05f);
    }

    /// <summary>
    /// 被同步引爆时调用：只生成碎片和销毁，不再检测附近罐子
    /// </summary>
    public void SyncExplode(int chainDepth, float intensity)
    {
        SpawnShards(chainDepth, intensity);
        Destroy(gameObject, 0.05f);
    }

    void SpawnShards(int chainDepth, float intensity)
    {
        // 获取罐子碰撞体半径，确保碎片从碰撞体外部生成
        Collider2D canCol = GetComponent<Collider2D>();
        float spawnRadius = 0.5f; // 默认值
        if (canCol != null)
        {
            spawnRadius = Mathf.Max(canCol.bounds.extents.x, canCol.bounds.extents.y) + 0.15f;
        }

        Sprite shardSprite = GetShardSprite();
        CircleCollider2D[] shardColliders = new CircleCollider2D[shardCount];

        Debug.Log($"[ConductiveObject] 生成 {shardCount} 个碎片，spawnRadius={spawnRadius:F2}，位置={transform.position}");

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject("Shard_" + i);
            float angle = (360f / shardCount) * i + Random.Range(-40f, 40f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            // 从碰撞体外部生成，避免卡在罐子内部被弹飞
            shard.transform.position = transform.position + (Vector3)(dir * spawnRadius);
            shard.transform.localScale = Vector3.one * shardSize;

            SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = shardSprite;
            sr.color = dischargeColor;
            sr.sortingOrder = 5;

            Rigidbody2D shardRb = shard.AddComponent<Rigidbody2D>();
            shardRb.mass = 0.1f;
            shardRb.gravityScale = 1f;
            shardRb.freezeRotation = false;
            float speedMul = Random.Range(0.7f, 1.3f);
            shardRb.velocity = dir * shardSpeed * speedMul * (1f + chainDepth * overloadMultiplierPerChain);

            CircleCollider2D shardCol = shard.AddComponent<CircleCollider2D>();
            shardCol.radius = 0.5f;
            shardColliders[i] = shardCol;

            // 忽略与罐子自身的碰撞
            if (canCol != null)
            {
                Physics2D.IgnoreCollision(shardCol, canCol);
            }

            // 忽略与玩家的碰撞
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            if (player != null)
            {
                Collider2D playerCol = player.GetComponent<Collider2D>();
                if (playerCol != null)
                {
                    Physics2D.IgnoreCollision(shardCol, playerCol);
                }
            }

            ConductiveShard cs = shard.AddComponent<ConductiveShard>();
            cs.chainDepth = chainDepth + 1;
            cs.intensity = intensity;
            cs.lifetime = shardLifetime;
        }

        // 碎片互相忽略碰撞
        for (int i = 0; i < shardCount; i++)
        {
            for (int j = i + 1; j < shardCount; j++)
            {
                Physics2D.IgnoreCollision(shardColliders[i], shardColliders[j]);
            }
        }
    }

    void ResetExploding() => exploding = false;

    void Apply2DExplosionForce(Rigidbody2D targetRb, Vector3 origin, float radius, float maxForce)
    {
        Vector2 dir = targetRb.position - (Vector2)origin;
        float dist = dir.magnitude;
        if (dist > radius || dist < 0.01f) return;

        float falloff = 1f - (dist / radius);
        // 玩家质量大，额外加力使其能被明显炸飞
        float massBoost = 1f;
        if (targetRb.mass >= 1f)
            massBoost = targetRb.mass * 2.5f;
        Vector2 force = dir.normalized * maxForce * falloff * massBoost;
        targetRb.AddForce(force, ForceMode2D.Impulse);
        Debug.Log($"  -> 推力施加！目标={targetRb.name}，力={force}，massBoost={massBoost}，falloff={falloff}");

        // 对玩家额外锁定控制一段时间，防止跳跃逻辑覆盖爆炸推力
        PlayerController player = targetRb.GetComponent<PlayerController>();
        if (player != null)
        {
            player.SetExternalForceLock(0.6f);
            Debug.Log($"  -> 已锁定玩家控制 0.6s");
        }
    }

    static Sprite _shardSprite;

    static Sprite GetShardSprite()
    {
        if (_shardSprite != null) return _shardSprite;

        int size = 32;
        // 使用 RGBA32 格式，关闭 mipmap，Point 过滤——确保运行时纹理能正确渲染
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _shardSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
        return _shardSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
