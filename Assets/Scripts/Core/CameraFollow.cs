using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -10f);
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        Vector3 focusPos;
        var players = Object.FindObjectsOfType<PlayerController>();

        if (players.Length > 0)
        {
            // 多人模式：跟随所有玩家中点
            Vector3 sum = Vector3.zero;
            foreach (var p in players) sum += p.transform.position;
            focusPos = sum / players.Length;
        }
        else if (target != null)
        {
            focusPos = target.position;
        }
        else
        {
            return;
        }

        Vector3 desired = focusPos + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.identity;
    }
}
