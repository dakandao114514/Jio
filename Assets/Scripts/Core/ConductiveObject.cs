using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveObject : MonoBehaviour, IConductive
{
    [Header("爆炸参数")]
    public float baseExplosionForce = 15f;
    public float explosionRadius = 1.5f;

    [Header("碎片")]
    public int shardCount = 5;
    public float shardSpeed = 3f;
    public float shardLifetime = 2f;
    public float shardSize = 0.5f;

    [Header("连锁与过载")]
    public int maxChainDepth = 8;
    public float overloadMultiplierPerChain = 0.5f;

    [Header("表现")]
    public Color dischargeColor = Color.yellow;

    Rigidbody2D rb;
    bool exploding;
    bool destroyed;

    static bool IsServerLogic => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnDischarge(Vector3 origin, float intensity, int chainDepth)
    {
        if (!IsServerLogic) return;
        if (chainDepth > maxChainDepth) return;
        if (exploding) return;
        if (destroyed) return;
        exploding = true;
        destroyed = true;

        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, explosionRadius, Physics2D.AllLayers);
        int syncCount = 1;
        foreach (var hit in nearby)
        {
            var other = hit.GetComponentInParent<ConductiveObject>();
            if (other != null && other != this && !other.exploding && !other.destroyed)
            {
                syncCount++;
                other.exploding = true;
                other.destroyed = true;
            }
        }

        float overload = 1f + chainDepth * overloadMultiplierPerChain;
        float force = baseExplosionForce * intensity * overload * syncCount;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            var otherRb = hit.attachedRigidbody;
            if (otherRb != null && otherRb != rb)
                Apply2DExplosionForce(otherRb, transform.position, explosionRadius, force);
        }

        foreach (var hit in nearby)
        {
            var other = hit.GetComponentInParent<ConductiveObject>();
            if (other != null && other != this)
                other.SyncExplode(chainDepth, intensity);
        }

        SpawnShards(chainDepth, intensity);

        // NetworkObject.Despawn(true) 会销毁对象并在所有客户端自动删除
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn(true);
        else Destroy(gameObject, 0.05f);
    }

    public void SyncExplode(int chainDepth, float intensity)
    {
        SpawnShards(chainDepth, intensity);
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn(true);
        else Destroy(gameObject, 0.05f);
    }

    void SpawnShards(int chainDepth, float intensity)
    {
        Collider2D canCol = GetComponent<Collider2D>();
        float spawnRadius = 0.5f;
        if (canCol != null)
            spawnRadius = Mathf.Max(canCol.bounds.extents.x, canCol.bounds.extents.y) + 0.15f;

        Sprite shardSprite = GetShardSprite();

        // 先计算所有碎片的位置和速度
        Vector3[] positions = new Vector3[shardCount];
        Vector3[] velocities = new Vector3[shardCount];
        CircleCollider2D[] shardColliders = new CircleCollider2D[shardCount];

        for (int i = 0; i < shardCount; i++)
        {
            float angle = (360f / shardCount) * i + Random.Range(-40f, 40f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            positions[i] = transform.position + (Vector3)(dir * spawnRadius);
            float speedMul = Random.Range(0.7f, 1.3f);
            velocities[i] = dir * shardSpeed * speedMul * (1f + chainDepth * overloadMultiplierPerChain);
        }

        // 服务端：创建真实碎片
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject("Shard_" + i);
            shard.transform.position = positions[i];
            shard.transform.localScale = Vector3.one * shardSize;

            var sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = shardSprite;
            sr.color = dischargeColor;
            sr.sortingOrder = 5;

            var shardRb = shard.AddComponent<Rigidbody2D>();
            shardRb.mass = 0.1f;
            shardRb.gravityScale = 1f;
            shardRb.freezeRotation = false;
            shardRb.velocity = velocities[i];

            var shardCol = shard.AddComponent<CircleCollider2D>();
            shardCol.radius = 0.5f;
            shardColliders[i] = shardCol;

            if (canCol != null)
                Physics2D.IgnoreCollision(shardCol, canCol);

            // 忽略与所有玩家的碰撞
            foreach (var player in Object.FindObjectsOfType<PlayerController>())
            {
                var pc = player.GetComponent<Collider2D>();
                if (pc != null)
                    Physics2D.IgnoreCollision(shardCol, pc);
            }

            var cs = shard.AddComponent<ConductiveShard>();
            cs.chainDepth = chainDepth + 1;
            cs.intensity = intensity;
            cs.lifetime = shardLifetime;
        }

        // 碎片互相忽略碰撞
        for (int i = 0; i < shardCount; i++)
            for (int j = i + 1; j < shardCount; j++)
                Physics2D.IgnoreCollision(shardColliders[i], shardColliders[j]);

        // 客户端：创建视觉碎片
        if (GamePhaseManager.Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            GamePhaseManager.Instance.SpawnShardsVisualClientRpc(positions, velocities, shardSize, dischargeColor);
    }

    void Apply2DExplosionForce(Rigidbody2D targetRb, Vector3 origin, float radius, float maxForce)
    {
        Vector2 dir = targetRb.position - (Vector2)origin;
        float dist = dir.magnitude;
        if (dist > radius || dist < 0.01f) return;

        float falloff = 1f - (dist / radius);
        float massBoost = 1f;
        if (targetRb.mass >= 1f)
            massBoost = targetRb.mass * 2.5f;
        Vector2 force = dir.normalized * maxForce * falloff * massBoost;
        targetRb.AddForce(force, ForceMode2D.Impulse);

        var player = targetRb.GetComponent<PlayerController>();
        if (player != null)
            player.SetExternalForceLock(0.6f);
    }

    static Sprite _shardSprite;
    public static Sprite GetShardSprite()
    {
        if (_shardSprite != null) return _shardSprite;
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        var center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.45f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = d <= radius ? Color.white : Color.clear;
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
