using UnityEngine;

/// <summary>
/// 终点区域：玩家进入后通知 GamePhaseManager 判定胜利
/// </summary>
public class FinishArea : MonoBehaviour
{
    GamePhaseManager phaseManager;

    void Awake()
    {
        phaseManager = Object.FindObjectOfType<GamePhaseManager>();

        // 终点框需要触发器碰撞体（场景搭建时只有视觉Sprite）
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (phaseManager == null) return;
        if (other.GetComponent<PlayerController>() != null)
            phaseManager.Win();
    }
}
