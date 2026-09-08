using UnityEngine;
using Unity.Netcode;

public class FinishArea : MonoBehaviour
{
    GamePhaseManager phaseManager;

    static bool IsServerLogic => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    void Awake()
    {
        phaseManager = Object.FindObjectOfType<GamePhaseManager>();
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServerLogic) return;
        if (phaseManager == null) return;
        if (other.GetComponent<PlayerController>() != null)
            phaseManager.Win();
    }
}
