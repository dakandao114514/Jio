using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class WaterPuddle : MonoBehaviour
{
    [Tooltip("水潭导电时显示的圆环颜色")]
    public Color conductColor = Color.cyan;

    /// <summary>
    /// 玩家踩到水潭时，整个水潭变成导电源，引爆水潭范围内的所有导电体
    /// </summary>
    public void OnPlayerEnter(Vector2 playerPos, float intensity)
    {
        Vector2 puddleCenter = transform.position;
        // 用水潭碰撞体的边界作为导电范围
        Collider2D puddleCol = GetComponent<Collider2D>();
        float conductRadius = Mathf.Max(puddleCol.bounds.size.x, puddleCol.bounds.size.y) * 0.5f;

        // 水潭导电圆环效果
        StartCoroutine(SpawnConductRing(puddleCenter, conductRadius));

        // 引爆水潭范围内的所有导电体
        Collider2D[] hits = Physics2D.OverlapCircleAll(puddleCenter, conductRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
            {
                target.OnDischarge(puddleCenter, intensity, 0);
            }
        }
    }

    IEnumerator SpawnConductRing(Vector2 center, float maxRadius)
    {
        GameObject ring = new GameObject("WaterConductRing");
        ring.transform.position = center;

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = conductColor;
        sr.sortingOrder = 9;

        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float radius = Mathf.Lerp(0.1f, maxRadius, t);
            ring.transform.localScale = Vector3.one * (radius * 2f);

            Color c = conductColor;
            c.a = Mathf.Lerp(0.8f, 0f, t);
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
        Gizmos.color = Color.cyan;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            float r = Mathf.Max(col.bounds.size.x, col.bounds.size.y) * 0.5f;
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
