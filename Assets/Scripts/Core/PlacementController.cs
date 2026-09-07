using UnityEngine;
using System.Collections.Generic;

public class PlacementController : MonoBehaviour
{
    public enum ItemType { Can, WaterPuddle, Insulator }

    ItemType current = ItemType.Can;
    bool placementActive = true;
    GamePhaseManager phaseManager;

    /// <summary>道具记录：游玩中被炸掉的罐子（go==null）返回布置阶段时按原位恢复</summary>
    class PlacedRecord
    {
        public GameObject go;
        public ItemType type;
        public Vector3 pos;
    }
    readonly List<PlacedRecord> placed = new List<PlacedRecord>();

    static Sprite _whiteSprite;

    Rect[] buttonRects;

    void Awake()
    {
        phaseManager = GetComponent<GamePhaseManager>();
        BuildButtonRects();
    }

    void BuildButtonRects()
    {
        float w = 120f;
        float h = 36f;
        float gap = 6f;
        buttonRects = new Rect[5];
        for (int i = 0; i < 5; i++)
            buttonRects[i] = new Rect(10f, 10f + i * (h + gap), w, h);
    }

    public float cameraSpeed = 30f;

    public void SetPlacementActive(bool v) { placementActive = v; }

    void Update()
    {
        if (!placementActive) return;
        if (phaseManager != null && phaseManager.Phase != GamePhase.Placement) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) current = ItemType.Can;
        if (Input.GetKeyDown(KeyCode.Alpha2)) current = ItemType.WaterPuddle;
        if (Input.GetKeyDown(KeyCode.Alpha3)) current = ItemType.Insulator;

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            PlaceItem(current, MouseWorld());

        if (Input.GetMouseButtonDown(1))
            DeleteAt(MouseWorld());

        // 布置阶段用WASD移动摄像机
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 camPos = cam.transform.position;
            if (Input.GetKey(KeyCode.W)) camPos.y += cameraSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.S)) camPos.y -= cameraSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.A)) camPos.x -= cameraSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.D)) camPos.x += cameraSpeed * Time.deltaTime;
            cam.transform.position = camPos;
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
        Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        foreach (var r in buttonRects)
            if (r.Contains(guiMouse)) return true;
        return false;
    }

    GameObject CreateItem(ItemType type, Vector3 pos)
    {
        GameObject go = new GameObject(type.ToString() + "_" + placed.Count);
        go.transform.position = pos;
        Sprite sprite = GetWhiteSprite();

        switch (type)
        {
            case ItemType.Can:
                go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                AddSprite(go, sprite, Color.red);
                Rigidbody2D crb = go.AddComponent<Rigidbody2D>();
                crb.mass = 0.5f;
                go.AddComponent<CircleCollider2D>();
                go.AddComponent<ConductiveObject>();
                break;

            case ItemType.WaterPuddle:
                go.transform.localScale = new Vector3(5f, 1f, 1f);
                AddSprite(go, sprite, new Color(0f, 0.6f, 1f, 0.5f));
                go.AddComponent<WaterPuddle>();
                break;

            case ItemType.Insulator:
                go.transform.localScale = new Vector3(1f, 3f, 1f);
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer >= 0) go.layer = groundLayer;
                AddSprite(go, sprite, Color.green);
                Rigidbody2D irb = go.AddComponent<Rigidbody2D>();
                irb.bodyType = RigidbodyType2D.Static;
                go.AddComponent<BoxCollider2D>();
                break;
        }

        return go;
    }

    void PlaceItem(ItemType type, Vector3 pos)
    {
        GameObject go = CreateItem(type, pos);
        placed.Add(new PlacedRecord { go = go, type = type, pos = pos });
    }

    /// <summary>恢复游玩期间被炸掉的道具（返回布置阶段时调用）</summary>
    public void RestoreDestroyed()
    {
        for (int i = 0; i < placed.Count; i++)
        {
            if (placed[i].go == null)
                placed[i].go = CreateItem(placed[i].type, placed[i].pos);
        }
    }

    void DeleteAt(Vector3 pos)
    {
        for (int i = placed.Count - 1; i >= 0; i--)
        {
            if (placed[i].go == null)
                continue;
            SpriteRenderer sr = placed[i].go.GetComponent<SpriteRenderer>();
            if (sr != null && sr.bounds.Contains(pos))
            {
                Destroy(placed[i].go);
                placed.RemoveAt(i);
                break;
            }
        }
    }

    void AddSprite(GameObject go, Sprite sprite, Color color)
    {
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 0;
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;

        int size = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
        return _whiteSprite;
    }

    void OnGUI()
    {
        // 过关界面
        if (phaseManager != null && phaseManager.Won)
        {
            GUI.Label(new Rect(Screen.width / 2 - 50f, Screen.height / 2 - 80f, 100f, 40f), "过关！");
            if (GUI.Button(new Rect(Screen.width / 2 - 80f, Screen.height / 2 - 20f, 160f, 40f), "返回布置阶段"))
                phaseManager.BackToPlacement();
            return;
        }

        if (!placementActive)
        {
            GUI.Label(new Rect(10, 10, 200, 24), "游戏中");
            if (GUI.Button(new Rect(10, 40, 120f, 30f), "返回布置"))
                phaseManager.BackToPlacement();
            return;
        }

        string[] labels = { "易拉罐 (1)", "积水滩 (2)", "绝缘块 (3)", "清空全部", "开始游戏" };
        for (int i = 0; i < labels.Length; i++)
        {
            bool isCurrent = (i < 3 && (ItemType)i == current);
            Color oldColor = GUI.color;
            if (isCurrent) GUI.color = Color.yellow;
            if (GUI.Button(buttonRects[i], labels[i]))
            {
                switch (i)
                {
                    case 0: current = ItemType.Can; break;
                    case 1: current = ItemType.WaterPuddle; break;
                    case 2: current = ItemType.Insulator; break;
                    case 3:
                        foreach (var rec in placed) if (rec.go) Destroy(rec.go);
                        placed.Clear();
                        break;
                    case 4:
                        if (phaseManager != null) phaseManager.StartPlay();
                        break;
                }
            }
            GUI.color = oldColor;
        }

        GUI.Label(new Rect(10, 220, 300, 24), "左键放置 / 右键删除 / 1·2·3 切换");
    }
}
