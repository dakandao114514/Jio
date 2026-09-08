using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public enum GamePhase { Placement, Play }

/// <summary>
/// 联机阶段管理器（Host-Authoritative）：
/// - NetworkVariable 同步阶段/过关状态
/// - ServerRpc 接收客户端请求（开始游戏/返回布置/放置道具/删除/清空）
/// - ClientRpc 广播视觉事件（道具出现/消失/碎片/圆环）
/// - 客户端维护 ID→视觉物体 字典
/// </summary>
public class GamePhaseManager : NetworkBehaviour
{
    public static GamePhaseManager Instance { get; private set; }

    public NetworkVariable<int> NetPhase = new NetworkVariable<int>(0); // 0=Placement,1=Play
    public NetworkVariable<bool> NetWon = new NetworkVariable<bool>(false);

    public GamePhase Phase => (GamePhase)NetPhase.Value;
    public bool Won => NetWon.Value;

    PlayerController[] players;
    CameraFollow cameraFollow;
    PlacementController placementController;
    Vector3 playerSpawn = new Vector3(0f, 1f, 0f);

    void Awake()
    {
        Instance = this;
        placementController = GetComponent<PlacementController>();
        cameraFollow = Object.FindObjectOfType<CameraFollow>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        NetPhase.OnValueChanged += (_, v) => OnPhaseChanged((GamePhase)v);
        NetWon.OnValueChanged += (_, v) => { if (v) OnWon(); };
        if (IsServer)
        {
            NetPhase.Value = 0;
            NetWon.Value = false;
        }
        else
        {
            OnPhaseChanged((GamePhase)NetPhase.Value);
        }
    }

    void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Placement)
        {
            // 冻结所有玩家
            foreach (var p in Object.FindObjectsOfType<PlayerController>())
            {
                p.enabled = false;
                var rb = p.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Static;
                }
            }
            if (cameraFollow != null) cameraFollow.enabled = false;
            if (placementController != null) placementController.SetPlacementActive(true);
        }
        else
        {
            // 解冻所有玩家
            foreach (var p in Object.FindObjectsOfType<PlayerController>())
            {
                var rb = p.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.velocity = Vector2.zero;
                }
                p.ResetAirState();
                p.enabled = true;
            }
            if (cameraFollow != null) cameraFollow.enabled = true;
            if (placementController != null) placementController.SetPlacementActive(false);
        }
    }

    void OnWon()
    {
        // 客户端侧：冻结所有玩家
        foreach (var p in Object.FindObjectsOfType<PlayerController>())
            p.enabled = false;
    }

    // ========== 公开方法 ==========

    public Vector3 GetPlayerSpawn() => playerSpawn;

    public void SetPlayerSpawn(Vector3 pos) => playerSpawn = pos;

    /// <summary>服务端：开始游戏</summary>
    [ServerRpc(RequireOwnership = false)]
    public void StartPlayServerRpc()
    {
        if (NetPhase.Value != 0) return;
        NetWon.Value = false;
        NetPhase.Value = 1;
        // OnValueChanged → OnPhaseChanged 会冻结/解冻两端玩家
    }

    /// <summary>服务端：返回布置阶段</summary>
    [ServerRpc(RequireOwnership = false)]
    public void BackToPlacementServerRpc()
    {
        if (placementController != null) placementController.RestoreDestroyed();
        NetWon.Value = false;
        NetPhase.Value = 0;
    }

    /// <summary>服务端：玩家过关</summary>
    public void Win()
    {
        if (NetPhase.Value != 1 || NetWon.Value) return;
        NetWon.Value = true;
        players = Object.FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.enabled = false;
            var rb = p.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Static;
            }
        }
    }

    // ========== 道具布置 ServerRpc ==========

    [ServerRpc(RequireOwnership = false)]
    public void PlaceItemServerRpc(int typeInt, Vector3 pos)
    {
        placementController.ServerPlaceItem((PlacementController.ItemType)typeInt, pos);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DeleteItemServerRpc(Vector3 pos)
    {
        placementController.ServerDeleteAt(pos);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearAllServerRpc()
    {
        placementController.ServerClearAll();
    }

    // ========== 视觉同步 ClientRpc（仅碎片和圆环）==========

    [ClientRpc]
    public void SpawnRingClientRpc(Vector2 center, float maxRadius, Color color, float duration)
    {
        // 圆环是纯视觉，服务端也要执行
        StartCoroutine(SpawnRingCoroutine(center, maxRadius, color, duration));
    }

    [ClientRpc]
    public void SpawnShardsVisualClientRpc(Vector3[] positions, Vector3[] velocities, float size, Color color)
    {
        if (IsServer) return; // 服务端有真实碎片
        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject("VShard_" + i);
            go.transform.position = positions[i];
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ConductiveObject.GetShardSprite();
            sr.color = color;
            sr.sortingOrder = 5;
            // 视觉碎片不需要物理，给个简单衰减
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = velocities[i];
            Destroy(go, 2f);
        }
    }

    System.Collections.IEnumerator SpawnRingCoroutine(Vector2 center, float maxRadius, Color color, float duration)
    {
        GameObject ring = new GameObject("Ring");
        ring.transform.position = center;
        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = color;
        sr.sortingOrder = 10;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float radius = Mathf.Lerp(0.1f, maxRadius, t);
            ring.transform.localScale = Vector3.one * (radius * 2f);
            Color c = color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(ring);
    }

    static Sprite _ringSprite;
    public static Sprite CreateRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[size * size];
        var center = new Vector2(size * 0.5f, size * 0.5f);
        float outer = size * 0.5f - 1f;
        float inner = outer - 6f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(center, new Vector2(x, y));
                pixels[y * size + x] = (d <= outer && d >= inner) ? Color.white : Color.clear;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
        return _ringSprite;
    }
}
