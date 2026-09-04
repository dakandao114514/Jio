using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveObject : MonoBehaviour, IConductive
{
    [Header("爆炸参数")]
    [Tooltip("基础爆炸推力")]
    public float baseExplosionForce = 12f;
    [Tooltip("爆炸影响半径")]
    public float explosionRadius = 2f;
    [Tooltip("向其他导电体传播电弧的半径")]
    public float propagationRadius = 4f;

    [Header("连锁与过载")]
    [Tooltip("最大连锁层数，防止无限递归")]
    public int maxChainDepth = 8;
    [Tooltip("每层连锁额外增加的推力倍率")]
    public float overloadMultiplierPerChain = 0.5f;

    [Header("表现")]
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

        SpawnEffect(transform.position, overload);

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

    void SpawnEffect(Vector3 position, float scaleMult)
    {
        GameObject fx = new GameObject("DischargeFX");
        fx.transform.position = position;
        fx.transform.localScale = Vector3.one * 0.4f * scaleMult;

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = dischargeColor;
        sr.sortingOrder = 10;

        Destroy(fx, 0.15f);
    }

    static Sprite CreateCircleSprite()
    {
        Texture2D tex = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];
        Vector2 center = new Vector2(32, 32);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * 64 + x] = dist <= 30f ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, propagationRadius);
    }
}
