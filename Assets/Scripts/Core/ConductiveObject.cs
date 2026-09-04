using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ConductiveObject : MonoBehaviour, IConductive
{
    [Header("爆炸参数")]
    [Tooltip("基础爆炸推力")]
    public float baseExplosionForce = 12f;
    [Tooltip("爆炸影响半径")]
    public float explosionRadius = 2.5f;
    [Tooltip("向其他导电体传播电弧的半径")]
    public float propagationRadius = 5f;

    [Header("连锁与过载")]
    [Tooltip("最大连锁层数，防止无限递归")]
    public int maxChainDepth = 8;
    [Tooltip("每层连锁额外增加的推力倍率")]
    public float overloadMultiplierPerChain = 0.5f;

    [Header("表现")]
    public Color dischargeColor = Color.yellow;

    Rigidbody rb;
    bool exploding;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void OnDischarge(Vector3 origin, float intensity, int chainDepth)
    {
        if (chainDepth > maxChainDepth) return;
        if (exploding) return;
        exploding = true;

        float overload = 1f + chainDepth * overloadMultiplierPerChain;
        float force = baseExplosionForce * intensity * overload;

        // 自身被炸开
        rb.AddExplosionForce(force, origin, explosionRadius, 0.5f, ForceMode.Impulse);

        // 推开半径内其他刚体
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            Rigidbody otherRb = hit.attachedRigidbody;
            if (otherRb != null && otherRb != rb)
            {
                otherRb.AddExplosionForce(force, transform.position, explosionRadius, 0.5f, ForceMode.Impulse);
            }
        }

        SpawnEffect(transform.position, overload);

        // 向附近导电体继续传播
        Collider[] propagate = Physics.OverlapSphere(transform.position, propagationRadius, Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (var p in propagate)
        {
            IConductive target = p.GetComponentInParent<IConductive>();
            if (target != null && !ReferenceEquals(target, this))
            {
                target.OnDischarge(transform.position, intensity, chainDepth + 1);
            }
        }

        // 短时间内不再被同一波连锁重复触发
        Invoke(nameof(ResetExploding), 0.1f);
    }

    void ResetExploding() => exploding = false;

    void SpawnEffect(Vector3 position, float scaleMult)
    {
        GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.name = "DischargeFX";
        fx.transform.position = position;
        fx.transform.localScale = Vector3.one * 0.25f * scaleMult;
        fx.transform.rotation = Random.rotation;

        Destroy(fx.GetComponent<Collider>());

        Renderer r = fx.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = dischargeColor;
            r.material.SetFloat("_EmissionColor", 2f);
            r.material.EnableKeyword("_EMISSION");
        }

        Destroy(fx, 0.15f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, propagationRadius);
    }
}
