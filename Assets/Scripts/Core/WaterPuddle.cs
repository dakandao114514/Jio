using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class WaterPuddle : MonoBehaviour
{
    [Tooltip("玩家踩入水滩后，放电范围放大倍率")]
    public float dischargeAmplify = 2.5f;
    [Tooltip("放大后的放电颜色")]
    public Color amplifiedColor = Color.cyan;

    /// <summary>
    /// 玩家踩入水滩时调用，返回放大后的放电半径
    /// </summary>
    public float GetAmplifiedRadius(float baseRadius)
    {
        return baseRadius * dischargeAmplify;
    }

    /// <summary>
    /// 玩家踩入水滩时触发放大效果，直接引爆范围内的所有导电体
    /// </summary>
    public void OnPlayerEnter(Vector2 playerPos, float baseRadius, float intensity)
    {
        float amplified = baseRadius * dischargeAmplify;

        // 扩散圆环效果
        StartCoroutine(SpawnAmplifyRing(playerPos, amplified));

        // 范围内所有导电体直接爆炸
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerPos, amplified, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
            {
                target.OnDischarge(playerPos, intensity * dischargeAmplify, 0);
            }
        }
    }

    IEnumerator SpawnAmplifyRing(Vector2 center, float maxRadius)
    {
        GameObject ring = new GameObject("WaterAmplifyRing");
        ring.transform.position = center;

        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = amplifiedColor;
        sr.sortingOrder = 9;

        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float radius = Mathf.Lerp(0.1f, maxRadius, t);
            ring.transform.localScale = Vector3.one * (radius * 2f);

            Color c = amplifiedColor;
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 3f * dischargeAmplify);
    }
}
