using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

public class PrototypeSetup
{
    [MenuItem("电极弹射/搭建核心原型场景")]
    static void BuildScene()
    {
        EnsureLayer("Ground");

        Sprite whiteSprite = GetOrCreateWhiteSprite();

        // 地面
        GameObject ground = new GameObject("Ground");
        ground.transform.localScale = new Vector3(60f, 1f, 1f);
        ground.transform.position = new Vector3(20f, -0.5f, 0f);
        ground.layer = LayerMask.NameToLayer("Ground");
        AddSprite(ground, whiteSprite, Color.gray);
        Rigidbody2D groundRb = ground.AddComponent<Rigidbody2D>();
        groundRb.bodyType = RigidbodyType2D.Static;
        ground.AddComponent<BoxCollider2D>();

        // 玩家
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, 0f);
        AddSprite(player, whiteSprite, new Color(0.2f, 0.6f, 1f));
        Rigidbody2D prb = player.AddComponent<Rigidbody2D>();
        prb.mass = 1f;
        player.AddComponent<BoxCollider2D>();
        player.AddComponent<PlayerController>();

        // 导电易拉罐
        for (int i = 0; i < 5; i++)
        {
            GameObject can = new GameObject("Can_" + i);
            can.transform.position = new Vector3(6f + i * 3f, 0.5f, 0f);
            can.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            AddSprite(can, whiteSprite, Color.red);
            Rigidbody2D crb = can.AddComponent<Rigidbody2D>();
            crb.mass = 0.5f;
            can.AddComponent<CircleCollider2D>();
            can.AddComponent<ConductiveObject>();
        }

        // 积水滩（无碰撞体，玩家和碎片可直接穿过）
        GameObject puddle = new GameObject("WaterPuddle");
        puddle.transform.position = new Vector3(18f, 0.05f, 0f);
        puddle.transform.localScale = new Vector3(5f, 1f, 1f);
        AddSprite(puddle, whiteSprite, new Color(0f, 0.6f, 1f, 0.5f));
        puddle.AddComponent<WaterPuddle>();

        // 绝缘块墙
        GameObject insulator = new GameObject("Insulator");
        insulator.transform.position = new Vector3(26f, 1.5f, 0f);
        insulator.transform.localScale = new Vector3(1f, 3f, 1f);
        insulator.layer = LayerMask.NameToLayer("Ground");
        AddSprite(insulator, whiteSprite, Color.green);
        Rigidbody2D insRb = insulator.AddComponent<Rigidbody2D>();
        insRb.bodyType = RigidbodyType2D.Static;
        insulator.AddComponent<BoxCollider2D>();

        // 终点框（可视化）
        GameObject finish = new GameObject("FinishArea");
        finish.transform.position = new Vector3(45f, 1f, 0f);
        finish.transform.localScale = new Vector3(3f, 3f, 1f);
        AddSprite(finish, whiteSprite, new Color(1f, 0.8f, 0f, 0.4f));

        // 摄像机
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        mainCam.orthographic = true;
        mainCam.orthographicSize = 8f;
        mainCam.transform.position = new Vector3(0f, 3f, -10f);

        CameraFollow cf = mainCam.gameObject.GetComponent<CameraFollow>();
        if (cf == null) cf = mainCam.gameObject.AddComponent<CameraFollow>();
        cf.target = player.transform;
        cf.offset = new Vector3(0f, 3f, -10f);

        // 灯光（2D 也保留一个平行光用于颜色）
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject light = new GameObject("Directional Light");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        Selection.activeGameObject = player;
        EditorUtility.DisplayDialog("电极弹射", "2D 核心原型场景搭建完成！\n\n按 Ctrl+P 运行，用 A/D 或 ←/→ 控制左右落点。", "确定");
    }

    [MenuItem("电极弹射/搭建布置阶段场景")]
    static void BuildPlacementScene()
    {
        EnsureLayer("Ground");

        Sprite whiteSprite = GetOrCreateWhiteSprite();

        // 地面
        GameObject ground = new GameObject("Ground");
        ground.transform.localScale = new Vector3(60f, 1f, 1f);
        ground.transform.position = new Vector3(20f, -0.5f, 0f);
        ground.layer = LayerMask.NameToLayer("Ground");
        AddSprite(ground, whiteSprite, Color.gray);
        Rigidbody2D groundRb = ground.AddComponent<Rigidbody2D>();
        groundRb.bodyType = RigidbodyType2D.Static;
        ground.AddComponent<BoxCollider2D>();

        // 玩家
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, 0f);
        AddSprite(player, whiteSprite, new Color(0.2f, 0.6f, 1f));
        Rigidbody2D prb = player.AddComponent<Rigidbody2D>();
        prb.mass = 1f;
        player.AddComponent<BoxCollider2D>();
        player.AddComponent<PlayerController>();

        // 终点框（玩家进入判定胜利）
        GameObject finish = new GameObject("FinishArea");
        finish.transform.position = new Vector3(45f, 1f, 0f);
        finish.transform.localScale = new Vector3(3f, 3f, 1f);
        AddSprite(finish, whiteSprite, new Color(1f, 0.8f, 0f, 0.4f));
        finish.AddComponent<FinishArea>();

        // 摄像机
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        mainCam.orthographic = true;
        mainCam.orthographicSize = 8f;
        mainCam.transform.position = new Vector3(0f, 3f, -10f);

        CameraFollow cf = mainCam.gameObject.GetComponent<CameraFollow>();
        if (cf == null) cf = mainCam.gameObject.AddComponent<CameraFollow>();
        cf.target = player.transform;
        cf.offset = new Vector3(0f, 3f, -10f);

        // 灯光
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject light = new GameObject("Directional Light");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // 阶段系统
        GameObject phaseSystem = new GameObject("PhaseSystem");
        phaseSystem.AddComponent<GamePhaseManager>();
        phaseSystem.AddComponent<PlacementController>();

        Selection.activeGameObject = phaseSystem;
        EditorUtility.DisplayDialog("电极弹射", "布置阶段场景搭建完成！\n\n按 Ctrl+P 运行：\n左键放置道具 / 右键删除\n1=易拉罐 2=积水滩 3=绝缘块\n点击「开始游戏」进入游玩", "确定");
    }

    [MenuItem("电极弹射/搭建联机场景")]
    static void BuildNetScene()
    {
        EnsureLayer("Ground");
        Sprite whiteSprite = GetOrCreateWhiteSprite();

        // ===== Player Prefab =====
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        string playerPath = "Assets/Resources/Player.prefab";
        GameObject pf = new GameObject("Player");
        AddSprite(pf, whiteSprite, new Color(0.2f, 0.6f, 1f));
        var pfRb = pf.AddComponent<Rigidbody2D>();
        pfRb.mass = 1f;
        pfRb.gravityScale = 1.5f;
        pfRb.freezeRotation = true;
        pf.AddComponent<BoxCollider2D>();
        pf.AddComponent<NetworkObject>();
        pf.AddComponent<NetworkTransform>();
        pf.AddComponent<PlayerController>();
        PrefabUtility.SaveAsPrefabAsset(pf, playerPath);
        Object.DestroyImmediate(pf);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);

        // ===== Can Prefab =====
        string canPath = "Assets/Resources/Can.prefab";
        GameObject canObj = new GameObject("Can");
        canObj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        AddSprite(canObj, whiteSprite, Color.red);
        var canRb = canObj.AddComponent<Rigidbody2D>();
        canRb.mass = 0.5f;
        canObj.AddComponent<CircleCollider2D>();
        canObj.AddComponent<NetworkObject>();
        canObj.AddComponent<NetworkTransform>();
        var cnt = canObj.GetComponent<NetworkTransform>();
        cnt.SyncScaleX = cnt.SyncScaleY = cnt.SyncScaleZ = false;
        canObj.AddComponent<ConductiveObject>();
        PrefabUtility.SaveAsPrefabAsset(canObj, canPath);
        Object.DestroyImmediate(canObj);
        GameObject canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(canPath);

        // ===== WaterPuddle Prefab =====
        string puddlePath = "Assets/Resources/WaterPuddle.prefab";
        GameObject wf = new GameObject("WaterPuddle");
        wf.transform.localScale = new Vector3(5f, 1f, 1f);
        AddSprite(wf, whiteSprite, new Color(0f, 0.6f, 1f, 0.5f));
        wf.AddComponent<NetworkObject>();
        wf.AddComponent<WaterPuddle>();
        PrefabUtility.SaveAsPrefabAsset(wf, puddlePath);
        Object.DestroyImmediate(wf);
        GameObject puddlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(puddlePath);

        // ===== Insulator Prefab =====
        string insulatorPath = "Assets/Resources/Insulator.prefab";
        GameObject inf = new GameObject("Insulator");
        inf.transform.localScale = new Vector3(1f, 3f, 1f);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) inf.layer = groundLayer;
        AddSprite(inf, whiteSprite, Color.green);
        var infRb = inf.AddComponent<Rigidbody2D>();
        infRb.bodyType = RigidbodyType2D.Static;
        inf.AddComponent<BoxCollider2D>();
        inf.AddComponent<NetworkObject>();
        inf.AddComponent<NetworkTransform>();
        var insNt = inf.GetComponent<NetworkTransform>();
        insNt.SyncScaleX = insNt.SyncScaleY = insNt.SyncScaleZ = false;
        PrefabUtility.SaveAsPrefabAsset(inf, insulatorPath);
        Object.DestroyImmediate(inf);
        GameObject insulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(insulatorPath);

        // ===== 地面 =====
        GameObject ground = new GameObject("Ground");
        ground.transform.localScale = new Vector3(60f, 1f, 1f);
        ground.transform.position = new Vector3(20f, -0.5f, 0f);
        ground.layer = LayerMask.NameToLayer("Ground");
        AddSprite(ground, whiteSprite, Color.gray);
        var groundRb = ground.AddComponent<Rigidbody2D>();
        groundRb.bodyType = RigidbodyType2D.Static;
        ground.AddComponent<BoxCollider2D>();

        // ===== 终点框 =====
        GameObject finish = new GameObject("FinishArea");
        finish.transform.position = new Vector3(45f, 1f, 0f);
        finish.transform.localScale = new Vector3(3f, 3f, 1f);
        AddSprite(finish, whiteSprite, new Color(1f, 0.8f, 0f, 0.4f));
        finish.AddComponent<FinishArea>();

        // ===== 摄像机 =====
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        mainCam.orthographic = true;
        mainCam.orthographicSize = 8f;
        mainCam.transform.position = new Vector3(0f, 3f, -10f);
        var camFollow = mainCam.gameObject.GetComponent<CameraFollow>();
        if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<CameraFollow>();
        camFollow.offset = new Vector3(0f, 3f, -10f);

        // ===== 灯光 =====
        if (Object.FindObjectOfType<Light>() == null)
        {
            var light = new GameObject("Directional Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ===== NetworkManager =====
        GameObject netMgr = new GameObject("NetworkManager");
        var nm = netMgr.AddComponent<NetworkManager>();
        var transport = netMgr.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777);
        // NGO 1.8.x 需要显式赋值 NetworkTransport
        nm.NetworkConfig.NetworkTransport = transport;
        nm.NetworkConfig.PlayerPrefab = playerPrefab;
        // 注册道具 prefab（布置时 Spawn 用）
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = canPrefab });
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = puddlePrefab });
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = insulatorPrefab });
        netMgr.AddComponent<NetUI>();

        // ===== PhaseSystem（NetworkObject + NetworkBehaviour）=====
        GameObject phaseSystem = new GameObject("PhaseSystem");
        phaseSystem.AddComponent<NetworkObject>();
        phaseSystem.AddComponent<GamePhaseManager>();
        phaseSystem.AddComponent<PlacementController>();

        Selection.activeGameObject = netMgr;
        EditorUtility.DisplayDialog("电极弹射",
            "联机场景搭建完成！\n\nPlayer prefab 已生成在 Assets/Resources/Player.prefab\n\n" +
            "测试方式：\n1. Ctrl+P 运行，点「启动主机」\n2. File → Build And Run 打包第二实例\n3. 第二实例点「连接」加入\n" +
            "4. 两端均可布置道具，开始游戏后双人同屏", "确定");
    }

    static void AddSprite(GameObject go, Sprite sprite, Color color)
    {
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 0;
    }

    /// <summary>
    /// 生成或复用项目里的白色 Sprite 资源，这样场景保存后也不会丢失图片。
    /// </summary>
    static Sprite GetOrCreateWhiteSprite()
    {
        string folder = "Assets/PrototypeAssets";
        string texPath = folder + "/WhiteTexture.png";
        string spritePath = folder + "/WhiteSprite.asset";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "PrototypeAssets");

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (existing != null)
            return existing;

        Texture2D tex;
        if (File.Exists(texPath))
        {
            AssetDatabase.ImportAsset(texPath);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }
        else
        {
            tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 2f;
            importer.SaveAndReimport();
        }

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 2f);
        AssetDatabase.CreateAsset(sprite, spritePath);
        AssetDatabase.SaveAssets();
        return sprite;
    }

    static void EnsureLayer(string name)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (layer.stringValue == name)
                return;
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return;
            }
        }
    }
}
