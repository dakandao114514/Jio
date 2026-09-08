using UnityEngine;
using Unity.Netcode;

public class WaterPuddle : MonoBehaviour
{
    public Color conductColor = Color.cyan;

    static bool IsServerLogic => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    public void OnPlayerEnter(Vector2 playerPos, float intensity)
    {
        if (!IsServerLogic) return;

        Vector2 puddleCenter = transform.position;
        float conductRadius = Mathf.Max(transform.localScale.x, transform.localScale.y) * 0.5f;

        // 圆环视觉 → 所有客户端（含服务端）
        if (GamePhaseManager.Instance != null)
            GamePhaseManager.Instance.SpawnRingClientRpc(puddleCenter, conductRadius, conductColor, 0.4f);

        // 引爆水潭范围内的导电体（仅服务端）
        Collider2D[] hits = Physics2D.OverlapCircleAll(puddleCenter, conductRadius, Physics2D.AllLayers);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
                target.OnDischarge(puddleCenter, intensity, 0);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        float r = Mathf.Max(transform.localScale.x, transform.localScale.y) * 0.5f;
        Gizmos.DrawWireSphere(transform.position, r);
    }
}
