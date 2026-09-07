using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveObject : MonoBehaviour, IConductive
{
    [Header("爆炸参数")]
    [Tooltip("基础爆炸推力")]
    public float baseExplosionForce = 12f;
    [Tooltip("爆炸影响半径")]
    public float explosionRadius = 4f;
    [Tooltip("向其他导电体传播电弧的半径")]
    public float propagationRadius = 6f;

    [Header("连锁与过载")]
    [Tooltip("最大连锁层数，防止无限递归")]
    public int maxChainDepth = 8;
    [Tooltip("每层连锁额外增加的推力倍率")]
    public float overloadMultiplierPerChain = 0.5f;

    [Header("表现")]
    [Tooltip("扩散圆环持续时间（秒）")]
    public float ringDuration = 0.35f;
    public Color dischargeColor = Color.yellow;

    Rigidbody2D rb;
    bool exploding;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void OnDischarge(Vector3 origin, float intensity, int chainDepth)
    {
        if (chainDepth > maxChainDepth) return;
        if (exploding) return;
        exploding = true;

        float overload = 1f + chainDepth * overloadMultiplierPerChain;
        float force = baseExplosionForce * intensity * overload;

        // 自身被炸开
        Apply2DExplosionForce(rb, origin, explosionRadius, force);

        // 推开半径内其他刚体
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            Rigidbody2D otherRb = hit.attachedRigidbody;
            if (otherRb != null && otherRb != rb)
            {
                Apply2DExplosionForce(otherRb, transform.position, explosionRadius, force);
            }
        }

        // 扩散圆环效果，以物体中心为起点
        StartCoroutine(SpawnExplosionRing(transform.position, explosionRadius * overload, overload));

        // 向附近导电体继续传播
        Collider2D[] propagate = Physics2D.OverlapCircleAll(transform.position, propagationRadius, Physics2D.AllLayers);
        foreach (var p in propagate)
        {
            IConductive target = p.GetComponentInParent<IConductive>();
            if (target != null && !ReferenceEquals(target, this))
            {
                target.OnDischarge(transform.position, intensity, chainDepth + 1);
            }
        }

        Invoke(nameof(ResetExploding), 0.1f);
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
        // 连锁越深颜色越偏红
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, propagationRadius);
    }
}
