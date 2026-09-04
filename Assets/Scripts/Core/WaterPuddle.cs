using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterPuddle : MonoBehaviour, IConductive
{
    [Tooltip("积水滩被漏电击中后，向多大范围传播电弧")]
    public float amplifyRadius = 7f;
    [Tooltip("传播时电弧强度倍率")]
    public float intensityMultiplier = 1.5f;

    public Color dischargeColor = Color.cyan;

    public void OnDischarge(Vector3 origin, float intensity, int chainDepth)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, amplifyRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null && !ReferenceEquals(target, this))
            {
                target.OnDischarge(transform.position, intensity * intensityMultiplier, chainDepth + 1);
            }
        }

        SpawnEffect();
    }

    void SpawnEffect()
    {
        GameObject fx = new GameObject("PuddleDischargeFX");
        fx.transform.position = transform.position;
        fx.transform.localScale = Vector3.one * amplifyRadius * 0.5f;

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = dischargeColor;
        sr.sortingOrder = 9;

        Destroy(fx, 0.2f);
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, amplifyRadius);
    }
}
