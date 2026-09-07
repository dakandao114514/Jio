using UnityEngine;

public enum GamePhase { Placement, Play }

public class GamePhaseManager : MonoBehaviour
{
    public GamePhase Phase { get; private set; } = GamePhase.Placement;

    PlayerController player;
    Rigidbody2D playerRb;
    PlacementController placementController;
    CameraFollow cameraFollow;

    void Awake()
    {
        player = Object.FindObjectOfType<PlayerController>();
        placementController = GetComponent<PlacementController>();
        cameraFollow = Object.FindObjectOfType<CameraFollow>();
        if (player != null)
            playerRb = player.GetComponent<Rigidbody2D>();

        EnterPlacementPhase();
    }

    public void EnterPlacementPhase()
    {
        Phase = GamePhase.Placement;
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
        Phase = GamePhase.Play;
        if (playerRb != null) playerRb.bodyType = RigidbodyType2D.Dynamic;
        if (player != null) player.enabled = true;
        if (cameraFollow != null) cameraFollow.enabled = true;
        if (placementController != null) placementController.SetPlacementActive(false);
    }
}
