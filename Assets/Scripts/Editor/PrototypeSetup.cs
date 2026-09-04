using UnityEngine;
using UnityEditor;
using System.IO;

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

        // 积水滩（触发器）
        GameObject puddle = new GameObject("WaterPuddle");
        puddle.transform.position = new Vector3(18f, 0.05f, 0f);
        puddle.transform.localScale = new Vector3(5f, 1f, 1f);
        AddSprite(puddle, whiteSprite, new Color(0f, 0.6f, 1f, 0.5f));
        BoxCollider2D pcol = puddle.AddComponent<BoxCollider2D>();
        pcol.isTrigger = true;
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
