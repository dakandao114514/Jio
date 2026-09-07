using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveObject : MonoBehaviour, IConductive
{
    [Header("爆炸参数")]
    [Tooltip("基础爆炸推力")]
    public float baseExplosionForce = 8f;
    [Tooltip("爆炸影响半径（仅用于推开附近刚体）")]
    public float explosionRadius = 1.5f;

    [Header("碎片")]
    [Tooltip("爆炸后裂成的碎片数量")]
    public int shardCount = 5;
    [Tooltip("碎片飞出的初速度")]
    public float shardSpeed = 3f;
    [Tooltip("碎片存活时间（秒）")]
    public float shardLifetime = 2f;
    [Tooltip("碎片大小")]
    public float shardSize = 0.35f;

    [Header("连锁与过载")]
    [Tooltip("最大连锁层数，防止无限递归")]
    public int maxChainDepth = 8;
    [Tooltip("每层连锁额外增加的推力倍率")]
    public float overloadMultiplierPerChain = 0.5f;

    [Header("表现")]
    [Tooltip("扩散圆环持续时间（秒）")]
    public float ringDuration = 0.35f;
    [Tooltip("扩散圆环最大半径")]
    public float ringRadius = 1.5f;
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

        float overload = 1f + chainDepth * overloadMultiplierPerChain;
        float force = baseExplosionForce * intensity * overload;

        // 爆炸圆环效果
        StartCoroutine(SpawnExplosionRing(transform.position, ringRadius * overload, overload));

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

        // 裂成碎片飞出
        SpawnShards(chainDepth, intensity);

        // 销毁自身
        Destroy(gameObject, 0.05f);
    }

    void SpawnShards(int chainDepth, float intensity)
    {
        CircleCollider2D[] shardColliders = new CircleCollider2D[shardCount];

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject("Shard_" + i);
            float angle = (360f / shardCount) * i + Random.Range(-20f, 20f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            shard.transform.position = transform.position + (Vector3)(dir * 0.3f);
            shard.transform.localScale = Vector3.one * shardSize;

            SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = CreateShardSprite();
            sr.color = dischargeColor;
            sr.sortingOrder = 5;

            Rigidbody2D shardRb = shard.AddComponent<Rigidbody2D>();
            shardRb.mass = 0.1f;
            shardRb.gravityScale = 1f;
            shardRb.freezeRotation = false;
            shardRb.velocity = dir * shardSpeed * (1f + chainDepth * overloadMultiplierPerChain);

            CircleCollider2D shardCol = shard.AddComponent<CircleCollider2D>();
            shardCol.radius = 0.3f;
            shardColliders[i] = shardCol;

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
        Vector2 force = dir.normalized * maxForce * falloff;
        targetRb.AddForce(force, ForceMode2D.Impulse);
    }

    IEnumerator SpawnExplosionRing(Vector3 center, float maxRadius, float overload)
    {
        GameObject ring = new GameObject("ExplosionRing");
        ring.transform.position = center;

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = Color.Lerp(dischargeColor, Color.red, Mathf.Clamp01((overload - 1f) * 0.3f));
        sr.sortingOrder = 10;

        float elapsed = 0f;
        while (elapsed < ringDuration)
        {
            float t = elapsed / ringDuration;
            float radius = Mathf.Lerp(0.1f, maxRadius, t);
            ring.transform.localScale = Vector3.one * (radius * 2f);

            Color c = sr.color;
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

    static Sprite CreateShardSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = dist <= size * 0.4f ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
