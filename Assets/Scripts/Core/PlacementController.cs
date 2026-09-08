using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlacementController : MonoBehaviour
{
    public enum ItemType { Can, WaterPuddle, Insulator }

    ItemType current = ItemType.Can;
    bool placementActive = true;
    GamePhaseManager phaseManager;

    // 玩家颜色（与 PlayerController 一致）
    static readonly Color[] PlayerColors = { new Color(0.2f, 0.6f, 1f), new Color(1f, 0.5f, 0.2f) };

    class PlacedRecord
    {
        public GameObject go;
        public ItemType type;
        public Vector3 pos;
        public ulong ownerId; // 谁放的
    }
    readonly List<PlacedRecord> placed = new List<PlacedRecord>();

    static Sprite _whiteSprite;
    Rect[] buttonRects;

    public float cameraSpeed = 30f;

    // 道具 prefab（运行时从 Resources 加载）
    static GameObject canPrefab;
    static GameObject puddlePrefab;
    static GameObject insulatorPrefab;

    static void LoadPrefabs()
    {
        if (canPrefab == null) canPrefab = Resources.Load<GameObject>("Can");
        if (puddlePrefab == null) puddlePrefab = Resources.Load<GameObject>("WaterPuddle");
        if (insulatorPrefab == null) insulatorPrefab = Resources.Load<GameObject>("Insulator");
    }

    void Awake()
    {
        phaseManager = GetComponent<GamePhaseManager>();
        BuildButtonRects();
    }

    void BuildButtonRects()
    {
        float w = 120f, h = 36f, gap = 6f;
        buttonRects = new Rect[5];
        for (int i = 0; i < 5; i++)
            buttonRects[i] = new Rect(10f, 10f + i * (h + gap), w, h);
    }

    public void SetPlacementActive(bool v) { placementActive = v; }

    void Update()
    {
        if (!placementActive) return;
        if (phaseManager != null && phaseManager.Phase != GamePhase.Placement) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) current = ItemType.Can;
        if (Input.GetKeyDown(KeyCode.Alpha2)) current = ItemType.WaterPuddle;
        if (Input.GetKeyDown(KeyCode.Alpha3)) current = ItemType.Insulator;

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            phaseManager.PlaceItemServerRpc((int)current, MouseWorld());

        if (Input.GetMouseButtonDown(1))
            phaseManager.DeleteItemServerRpc(MouseWorld());

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 p = cam.transform.position;
            if (Input.GetKey(KeyCode.W)) p.y += cameraSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.S)) p.y -= cameraSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.A)) p.x -= cameraSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.D)) p.x += cameraSpeed * Time.deltaTime;
            cam.transform.position = p;
        }
    }

    Vector3 MouseWorld()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = -Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(mp);
    }

    bool IsPointerOverUI()
    {
        Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        foreach (var r in buttonRects)
            if (r.Contains(m)) return true;
        return false;
    }

    // ========== 服务端方法（由 GamePhaseManager ServerRpc 调用）==========

    public void ServerPlaceItem(ItemType type, Vector3 pos, ulong ownerId)
    {
        GameObject go = CreateItem(type, pos, ownerId);
        if (go == null) return;
        var no = go.GetComponent<NetworkObject>();
        if (no != null) no.Spawn();
        placed.Add(new PlacedRecord { go = go, type = type, pos = pos, ownerId = ownerId });
    }

    public void ServerDeleteAt(Vector3 pos, ulong requesterId)
    {
        for (int i = placed.Count - 1; i >= 0; i--)
        {
            if (placed[i].go == null) continue;
            // 只能删除自己放的道具
            if (placed[i].ownerId != requesterId) continue;
            var sr = placed[i].go.GetComponent<SpriteRenderer>();
            if (sr != null && sr.bounds.Contains(pos))
            {
                var no = placed[i].go.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
                else Destroy(placed[i].go);
                placed.RemoveAt(i);
                break;
            }
        }
    }

    public void ServerClearAll(ulong requesterId)
    {
        for (int i = placed.Count - 1; i >= 0; i--)
        {
            if (placed[i].go == null || placed[i].ownerId != requesterId) continue;
            var no = placed[i].go.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn(true);
            else Destroy(placed[i].go);
            placed.RemoveAt(i);
        }
    }

    public void RestoreDestroyed()
    {
        for (int i = 0; i < placed.Count; i++)
        {
            if (placed[i].go == null)
            {
                placed[i].go = CreateItem(placed[i].type, placed[i].pos, placed[i].ownerId);
                if (placed[i].go == null) continue;
                var no = placed[i].go.GetComponent<NetworkObject>();
                if (no != null) no.Spawn();
            }
        }
    }

    GameObject CreateItem(ItemType type, Vector3 pos, ulong ownerId)
    {
        LoadPrefabs();
        GameObject prefab = null;
        switch (type)
        {
            case ItemType.Can: prefab = canPrefab; break;
            case ItemType.WaterPuddle: prefab = puddlePrefab; break;
            case ItemType.Insulator: prefab = insulatorPrefab; break;
        }
        if (prefab == null)
        {
            Debug.LogError($"[PlacementController] Prefab未加载！type={type}");
            return null;
        }

        GameObject go = Instantiate(prefab);
        go.transform.position = pos;
        go.name = type.ToString() + "_" + placed.Count;

        // 按放置者颜色设置 NetworkVariable（Spawn 后自动同步到客户端）
        if (type == ItemType.Can)
        {
            int colorIdx = (int)(ownerId % (ulong)PlayerColors.Length);
            var co = go.GetComponent<ConductiveObject>();
            if (co != null) co.OwnerColorIndex.Value = colorIdx;
        }

        return go;
    }

    public static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
        return _whiteSprite;
    }

    void OnGUI()
    {
        if (phaseManager != null && phaseManager.Won)
        {
            GUI.Label(new Rect(Screen.width / 2 - 50f, Screen.height / 2 - 80f, 100f, 40f), "过关！");
            if (GUI.Button(new Rect(Screen.width / 2 - 80f, Screen.height / 2 - 20f, 160f, 40f), "返回布置阶段"))
                phaseManager.BackToPlacementServerRpc();
            return;
        }

        if (!placementActive)
        {
            GUI.Label(new Rect(10, 10, 200, 24), "游戏中");
            if (GUI.Button(new Rect(10, 40, 120f, 30f), "返回布置"))
                phaseManager.BackToPlacementServerRpc();
            return;
        }

        string[] labels = { "易拉罐 (1)", "积水滩 (2)", "绝缘块 (3)", "清空全部", "开始游戏" };
        for (int i = 0; i < labels.Length; i++)
        {
            bool isCurrent = (i < 3 && (ItemType)i == current);
            Color old = GUI.color;
            if (isCurrent) GUI.color = Color.yellow;
            if (GUI.Button(buttonRects[i], labels[i]))
            {
                switch (i)
                {
                    case 0: current = ItemType.Can; break;
                    case 1: current = ItemType.WaterPuddle; break;
                    case 2: current = ItemType.Insulator; break;
                    case 3: phaseManager.ClearAllServerRpc(); break;
                    case 4: phaseManager.StartPlayServerRpc(); break;
                }
            }
            GUI.color = old;
        }
        GUI.Label(new Rect(10, 220, 300, 24), "左键放置 / 右键删除(仅自己放的) / 1·2·3 切换");
    }
}
