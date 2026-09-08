using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
public class ConductiveShard : MonoBehaviour
{
    [HideInInspector] public int chainDepth;
    [HideInInspector] public float intensity;
    [HideInInspector] public float lifetime;

    float spawnTime;
    const float immunityDuration = 0.15f;

    static bool IsServerLogic => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    void Start()
    {
        spawnTime = Time.time;
    }

    void Update()
    {
        if (!IsServerLogic) return;
        if (Time.time - spawnTime > lifetime)
            Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!IsServerLogic) return;
        if (Time.time - spawnTime < immunityDuration) return;

        IConductive target = col.collider.GetComponentInParent<IConductive>();
        if (target != null)
        {
            target.OnDischarge(transform.position, intensity, chainDepth);
            Destroy(gameObject);
        }
    }
}
