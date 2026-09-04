using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterPuddle : MonoBehaviour, IConductive
{
    [Tooltip("积水滩被漏电击中后，向多大范围传播电弧")]
    public float amplifyRadius = 8f;
    [Tooltip("传播时电弧强度倍率")]
    public float intensityMultiplier = 1.5f;

    public Color dischargeColor = Color.cyan;

    public void OnDischarge(Vector3 origin, float intensity, int chainDepth)
    {
        // 向更广范围触发所有导电体
        Collider[] hits = Physics.OverlapSphere(transform.position, amplifyRadius, Physics.AllLayers, QueryTriggerInteraction.Collide);
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
        GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.name = "PuddleDischargeFX";
        fx.transform.position = transform.position;
        fx.transform.localScale = Vector3.one * amplifyRadius * 0.25f;
        Destroy(fx.GetComponent<Collider>());

        Renderer r = fx.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = dischargeColor;
        }

        Destroy(fx, 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, amplifyRadius);
    }
}
