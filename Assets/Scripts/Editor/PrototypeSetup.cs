using UnityEngine;
using UnityEditor;

public class PrototypeSetup
{
    [MenuItem("电极弹射/搭建核心原型场景")]
    static void BuildScene()
    {
        EnsureLayer("Ground");

        // 地面
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(60f, 1f, 12f);
        ground.transform.position = new Vector3(20f, -0.5f, 0f);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial(Color.gray, "GroundMat");

        // 玩家
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        Rigidbody prb = player.AddComponent<Rigidbody>();
        prb.mass = 1f;
        player.AddComponent<PlayerController>();
        player.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.2f, 0.6f, 1f), "PlayerMat");

        // 导电易拉罐
        for (int i = 0; i < 5; i++)
        {
            GameObject can = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            can.name = "Can_" + i;
            can.transform.position = new Vector3(6f + i * 3f, 0.5f, 0f);
            can.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            Rigidbody crb = can.AddComponent<Rigidbody>();
            crb.mass = 0.5f;
            can.AddComponent<ConductiveObject>();
            can.GetComponent<Renderer>().sharedMaterial = CreateMaterial(Color.red, "CanMat");
        }

        // 积水滩（触发器）
        GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        puddle.name = "WaterPuddle";
        puddle.transform.position = new Vector3(18f, 0.05f, 0f);
        puddle.transform.localScale = new Vector3(5f, 0.1f, 4f);
        GameObject.DestroyImmediate(puddle.GetComponent<Collider>());
        BoxCollider pcol = puddle.AddComponent<BoxCollider>();
        pcol.isTrigger = true;
        puddle.AddComponent<WaterPuddle>();
        puddle.GetComponent<Renderer>().sharedMaterial = CreateTransparentMaterial(new Color(0f, 0.6f, 1f, 0.5f), "PuddleMat");

        // 绝缘块墙
        GameObject insulator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        insulator.name = "Insulator";
        insulator.transform.position = new Vector3(26f, 1.5f, 0f);
        insulator.transform.localScale = new Vector3(1f, 3f, 5f);
        insulator.layer = LayerMask.NameToLayer("Ground");
        insulator.GetComponent<Renderer>().sharedMaterial = CreateMaterial(Color.green, "InsulatorMat");

        // 终点框（可视化）
        GameObject finish = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finish.name = "FinishArea";
        finish.transform.position = new Vector3(45f, 0.5f, 0f);
        finish.transform.localScale = new Vector3(3f, 2f, 3f);
        DestroyImmediate(finish.GetComponent<Collider>());
        finish.GetComponent<Renderer>().sharedMaterial = CreateTransparentMaterial(new Color(1f, 0.8f, 0f, 0.4f), "FinishMat");

        // 摄像机
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        CameraFollow cf = mainCam.gameObject.GetComponent<CameraFollow>();
        if (cf == null) cf = mainCam.gameObject.AddComponent<CameraFollow>();
        cf.target = player.transform;
        cf.offset = new Vector3(0f, 6f, -12f);

        // 灯光
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject light = new GameObject("Directional Light");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        Selection.activeGameObject = player;
        EditorUtility.DisplayDialog("电极弹射", "核心原型场景搭建完成！\n\n按 Ctrl+P 运行，用 A/D 或 ←/→ 控制左右落点。", "确定");
    }

    static Material CreateMaterial(Color color, string name)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.name = name;
        return mat;
    }

    static Material CreateTransparentMaterial(Color color, string name)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = name;
        mat.color = color;
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
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
