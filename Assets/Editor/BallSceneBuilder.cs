using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the "Ball Drop" scene from primitives: a stepped platform, a domed building,
/// a tilted canopy on pillars (a prefab), a bouncy ball, a camera that follows it,
/// a directional light, and Play / Reset buttons. Run it from Tools > Build Ball Drop Scene.
/// </summary>
public static class BallSceneBuilder
{
    const string ScenePath = "Assets/Scenes/BallDropScene.unity";
    const string MaterialsDir = "Assets/Materials";
    const string TexturesDir = "Assets/Textures";
    const string PrefabsDir = "Assets/Prefabs";
    const string PhysicsDir = "Assets/Physics";

    const float StepHeight = 0.3f;
    const int StepCount = 4;

    [MenuItem("Tools/Build Ball Drop Scene")]
    public static void Build()
    {
        EnsureFolder(MaterialsDir);
        EnsureFolder(TexturesDir);
        EnsureFolder(PrefabsDir);
        EnsureFolder(PhysicsDir);

        // ---------- Assets ----------
        Texture2D checker = CreateCheckerTexture();
        Material groundMat = CreateMaterial("Ground", Color.white, 0.1f, checker, new Vector2(100f, 100f));
        Material stepMat = CreateMaterial("Stone", new Color(0.93f, 0.93f, 0.91f), 0.15f);
        Material wallMat = CreateMaterial("BuildingWall", new Color(0.96f, 0.92f, 0.84f), 0.1f);
        Material domeMat = CreateMaterial("Dome", new Color(0.62f, 0.78f, 0.95f), 0.5f);
        Material roofMat = CreateMaterial("Roof", new Color(0.86f, 0.88f, 0.92f), 0.3f);
        Material pillarMat = CreateMaterial("Pillar", new Color(0.99f, 0.99f, 0.99f), 0.2f);
        Material ballMat = CreateMaterial("Ball", new Color(0.86f, 0.12f, 0.1f), 0.65f);

        PhysicsMaterial bouncy = CreateBouncyMaterial();
        GameObject pillarPrefab = CreatePillarPrefab(pillarMat);

        // ---------- New empty scene ----------
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.58f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.28f);

        // ---------- Environment ----------
        GameObject environment = new GameObject("Environment");

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // has a MeshCollider
        ground.name = "Ground";
        ground.transform.SetParent(environment.transform);
        ground.transform.localScale = new Vector3(20f, 1f, 20f); // 200 x 200 units
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        GameObject lightGo = new GameObject("Directional Light");
        lightGo.transform.SetParent(environment.transform);
        lightGo.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(45f, 30f, 0f));
        Light sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        RenderSettings.sun = sun;

        // ---------- Structure ----------
        GameObject structure = new GameObject("Structure");

        // Stepped platform: each step is smaller and shifted toward the back-left,
        // so the stairs show on the front and right sides.
        GameObject steps = Child("Steps", structure.transform);
        for (int i = 0; i < StepCount; i++)
        {
            float size = 16f - 1.2f * i;
            float shift = 0.6f * i;
            Cube($"Step_{i + 1}", steps.transform,
                new Vector3(-shift, StepHeight * (i + 0.5f), shift),
                new Vector3(size, StepHeight, size), Quaternion.identity, stepMat);
        }
        float topY = StepHeight * StepCount; // 1.2

        // Building: plinth + block + dome.
        GameObject building = Child("Building", structure.transform);
        Vector3 bPos = new Vector3(-4.5f, 0f, -0.8f);
        Cube("Plinth", building.transform, new Vector3(bPos.x, topY + 0.3f, bPos.z),
            new Vector3(4.2f, 0.6f, 4.2f), Quaternion.identity, stepMat);
        float blockBase = topY + 0.6f;
        Cube("Block", building.transform, new Vector3(bPos.x, blockBase + 1.25f, bPos.z),
            new Vector3(3.2f, 2.5f, 3.2f), Quaternion.identity, wallMat);
        GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "Dome";
        dome.transform.SetParent(building.transform);
        dome.transform.position = new Vector3(bPos.x, blockBase + 2.5f, bPos.z);
        dome.transform.localScale = Vector3.one * 2.3f;
        dome.GetComponent<Renderer>().sharedMaterial = domeMat;

        // Canopy: a tilted roof on pillars. The ball lands on this angled surface.
        GameObject canopy = Child("Canopy", structure.transform);
        Vector3 roofCenter = new Vector3(1.0f, 4.0f, 4.5f);
        Vector3 roofSize = new Vector3(9f, 0.25f, 4f);
        Quaternion roofRot = Quaternion.Euler(0f, 0f, -12f); // slopes down toward +X
        GameObject roof = Cube("Roof", canopy.transform, roofCenter, roofSize, roofRot, roofMat);
        roof.GetComponent<Collider>().sharedMaterial = bouncy;

        GameObject pillars = Child("Pillars", canopy.transform);
        float[] pillarX = { -2f, 1f, 4f };
        for (int i = 0; i < pillarX.Length; i++)
        {
            float px = pillarX[i], pz = roofCenter.z;
            float height = RoofUndersideY(roofCenter, roofSize, roofRot, px, pz) - topY;
            GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, scene);
            p.name = $"Pillar_{i + 1}";
            p.transform.SetParent(pillars.transform);
            p.transform.position = new Vector3(px, topY, pz);
            p.transform.localScale = new Vector3(1f, height, 1f); // prefab pillar is 1 unit tall
        }

        // ---------- Gameplay ----------
        GameObject gameplay = new GameObject("Gameplay");

        GameObject spawn = Child("BallSpawnPoint", gameplay.transform);
        spawn.transform.position = new Vector3(-1.5f, 9f, 4.5f);

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); // has a SphereCollider
        ball.name = "Ball";
        ball.transform.SetParent(gameplay.transform);
        ball.transform.position = spawn.transform.position;
        ball.transform.localScale = Vector3.one * 0.8f;
        ball.GetComponent<Renderer>().sharedMaterial = ballMat;
        ball.GetComponent<SphereCollider>().sharedMaterial = bouncy;
        Rigidbody rb = ball.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 1.2f; // acts as rolling resistance so the ball comes to rest
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ball.SetActive(false); // hidden until Play is pressed

        // ---------- Camera ----------
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 55f;
        cam.farClipPlane = 300f;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(9f, 12f, -15f);
        camGo.transform.LookAt(new Vector3(-1f, 1.5f, 1.5f));
        CameraFollow follow = camGo.AddComponent<CameraFollow>();
        follow.target = ball.transform;
        follow.offset = new Vector3(5f, 3.5f, -9f);
        follow.smoothTime = 0.3f;

        GameObject manager = Child("GameManager", gameplay.transform);
        BallDropController controller = manager.AddComponent<BallDropController>();
        controller.ball = rb;
        controller.spawnPoint = spawn.transform;
        controller.cameraFollow = follow;

        // ---------- UI ----------
        GameObject ui = new GameObject("UI");
        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(ui.transform);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        Button play = CreateButton("PlayButton", "Play", canvasGo.transform, new Vector2(40f, -40f),
            new Color(0.25f, 0.68f, 0.35f));
        UnityEventTools.AddPersistentListener(play.onClick, new UnityAction(controller.Play));
        Button reset = CreateButton("ResetButton", "Reset", canvasGo.transform, new Vector2(40f, -120f),
            new Color(0.88f, 0.45f, 0.2f));
        UnityEventTools.AddPersistentListener(reset.onClick, new UnityAction(controller.ResetScene));

        GameObject es = new GameObject("EventSystem");
        es.transform.SetParent(ui.transform);
        es.AddComponent<EventSystem>();
        InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();

        // ---------- Save ----------
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BallSceneBuilder] Scene built and saved to {ScenePath}");
    }

    // ===== Helpers =====

    static GameObject Child(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); // has a BoxCollider
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Height of the roof's bottom face directly above (x, z).
    static float RoofUndersideY(Vector3 center, Vector3 size, Quaternion rot, float x, float z)
    {
        Vector3 up = rot * Vector3.up;
        Vector3 p0 = center - up * (size.y * 0.5f);
        return p0.y - ((x - p0.x) * up.x + (z - p0.z) * up.z) / up.y;
    }

    static Button CreateButton(string name, string label, Transform parent, Vector2 anchoredPos, Color color)
    {
        var res = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
        };
        GameObject go = DefaultControls.CreateButton(res);
        go.name = name;
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(220f, 64f);

        go.GetComponent<Image>().color = color;

        Text text = go.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
        }
        return go.GetComponent<Button>();
    }

    static Texture2D CreateCheckerTexture()
    {
        string path = $"{TexturesDir}/GroundChecker.png";
        const int size = 256, cells = 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color a = new Color(0.78f, 0.78f, 0.76f), b = new Color(0.45f, 0.47f, 0.5f);
        Color line = new Color(0.3f, 0.3f, 0.32f);
        int cell = size / cells;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool odd = ((x / cell) + (y / cell)) % 2 == 1;
            bool border = x % cell < 2 || y % cell < 2;
            tex.SetPixel(x, y, border ? line : (odd ? a : b));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material CreateMaterial(string name, Color color, float smoothness,
        Texture2D tex = null, Vector2? tiling = null)
    {
        string path = $"{MaterialsDir}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static PhysicsMaterial CreateBouncyMaterial()
    {
        string path = $"{PhysicsDir}/Bouncy.physicMaterial";
        PhysicsMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm == null)
        {
            pm = new PhysicsMaterial("Bouncy");
            AssetDatabase.CreateAsset(pm, path);
        }
        pm.bounciness = 0.6f;
        pm.bounceCombine = PhysicsMaterialCombine.Maximum;
        pm.dynamicFriction = 0.4f;
        pm.staticFriction = 0.4f;
        EditorUtility.SetDirty(pm);
        return pm;
    }

    // Pillar prefab: root pivot at the base, 1 unit tall, so instances are sized by Y scale.
    static GameObject CreatePillarPrefab(Material mat)
    {
        string path = $"{PrefabsDir}/Pillar.prefab";
        GameObject root = new GameObject("Pillar");
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // has a CapsuleCollider
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        shaft.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f); // cylinder mesh is 2 units tall
        shaft.GetComponent<Renderer>().sharedMaterial = mat;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path))
            return;
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
