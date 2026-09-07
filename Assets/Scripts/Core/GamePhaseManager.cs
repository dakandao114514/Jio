using UnityEngine;

public enum GamePhase { Placement, Play }

public class GamePhaseManager : MonoBehaviour
{
    public GamePhase Phase { get; private set; } = GamePhase.Placement;
    /// <summary>本局是否已过关（显示过关UI用）</summary>
    public bool Won { get; private set; }

    PlayerController player;
    Rigidbody2D playerRb;
    PlacementController placementController;
    CameraFollow cameraFollow;
    Vector3 playerSpawn;

    void Awake()
    {
        player = Object.FindObjectOfType<PlayerController>();
        placementController = GetComponent<PlacementController>();
        cameraFollow = Object.FindObjectOfType<CameraFollow>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerSpawn = player.transform.position;
        }

        EnterPlacementPhase();
    }

    public void EnterPlacementPhase()
    {
        Phase = GamePhase.Placement;
        Won = false;
        if (player != null) player.enabled = false;
        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.bodyType = RigidbodyType2D.Static;
        }
        if (cameraFollow != null) cameraFollow.enabled = false;
        if (placementController != null) placementController.SetPlacementActive(true);
    }

    public void StartPlay()
    {
        Won = false;
        Phase = GamePhase.Play;
        if (playerRb != null)
        {
            playerRb.bodyType = RigidbodyType2D.Dynamic;
            playerRb.position = playerSpawn;
            playerRb.velocity = Vector2.zero;
        }
        if (player != null)
        {
            player.ResetAirState();
            player.enabled = true;
        }
        if (cameraFollow != null) cameraFollow.enabled = true;
        if (placementController != null) placementController.SetPlacementActive(false);
    }

    /// <summary>玩家到达终点，本关胜利（冻结玩家，显示过关UI）</summary>
    public void Win()
    {
        if (Phase != GamePhase.Play || Won) return;
        Won = true;
        if (player != null) player.enabled = false;
        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
            playerRb.bodyType = RigidbodyType2D.Static;
        }
    }

    /// <summary>
    /// 返回布置阶段：恢复被炸掉的道具，玩家和摄像机回到出生点，可继续调整布局后再次开始
    /// </summary>
    public void BackToPlacement()
    {
        if (placementController != null) placementController.RestoreDestroyed();
        EnterPlacementPhase();
        if (player != null) player.transform.position = playerSpawn;
        if (cameraFollow != null)
            cameraFollow.transform.position = playerSpawn + cameraFollow.offset;
    }
}
