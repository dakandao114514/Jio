using UnityEngine;

/// <summary>
/// 爆炸碎片：飞出后碰到其他导电体才会触发连锁爆炸
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveShard : MonoBehaviour
{
    [HideInInspector] public int chainDepth;
    [HideInInspector] public float intensity;
    [HideInInspector] public float lifetime;

    float spawnTime;
    // 生成后短暂不触发，避免刚出生就碰到东西
    const float immunityDuration = 0.15f;

    void Start()
    {
        spawnTime = Time.time;
    }

    void Update()
    {
        if (Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        // 刚生成后短暂免疫
        if (Time.time - spawnTime < immunityDuration) return;

        IConductive target = col.collider.GetComponentInParent<IConductive>();
        if (target != null)
        {
            target.OnDischarge(transform.position, intensity, chainDepth);
            Destroy(gameObject);
        }
    }
}
