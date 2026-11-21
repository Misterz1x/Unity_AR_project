using System.IO;
using ARGolf.Putting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Assets/Scenes/FamousGreens.unity: the built-in greens (FamousGreens) with a ball, hole and flag,
/// keyboard aim + power (AimPowerLauncher), follow camera and GreenPracticeController.
/// Run from Tools > AR Golf > Build Famous Greens Scene. Rebuilding overwrites the scene.
/// Reuses the aim arrow from the PuttingGreen scene if it exists.
/// </summary>
public static class FamousGreensBuilder
{
    const string ScenePath = "Assets/Scenes/FamousGreens.unity";
    const string GenDir = "Assets/Generated/FamousGreens";
    const string ArrowMeshPath = "Assets/Generated/PuttingGreen/AimArrowMesh.asset";
    const string ArrowMaterialPath = "Assets/Generated/PuttingGreen/AimArrow.mat";

    const float FlagstickDiameter = 0.015f, FlagstickHeight = 1.4f;
    static readonly Vector3 FlagClothSize = new Vector3(0.45f, 0.3f, 0.005f);

    [MenuItem("Tools/AR Golf/Build Famous Greens Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Directory.CreateDirectory(GenDir);
        AssetDatabase.Refresh();

        var rules = new RollSettings();

        Material greenMat = LitMaterial("GreenBase", Color.white, 0.12f);
        Material fringeMat = LitMaterial("Fringe", new Color(0.25f, 0.47f, 0.16f), 0.08f);
        Material roughMat = LitMaterial("Rough", new Color(0.2f, 0.38f, 0.13f), 0.04f);
        Material waterMat = LitMaterial("Water", new Color(0.12f, 0.35f, 0.55f), 0.92f);
        Material stickMat = LitMaterial("Flagstick", new Color(0.95f, 0.95f, 0.95f), 0.5f);
        Material clothMat = LitMaterial("FlagCloth", new Color(0.9f, 0.15f, 0.12f), 0.1f);
        Material ballMat = LitMaterial("GolfBall", Color.white, 0.7f);
        Material lineMat = SpriteMaterial("PathLine");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.62f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.2f);

        var lightGo = new GameObject("Directional Light");
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        GameObject ground = Primitive(PrimitiveType.Plane, "Rough", null, roughMat);
        ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300 x 300 m
        GameObject water = Primitive(PrimitiveType.Plane, "Water", null, waterMat);
        water.transform.localScale = new Vector3(10f, 1f, 10f);
        water.SetActive(false);

        // Green
        var greenGo = new GameObject("Green");
        var view = greenGo.AddComponent<GreenView>();
        view.greenMaterial = greenMat;
        view.fringeMaterial = fringeMat;
        view.lineMaterial = lineMat;
        view.greenId = FamousGreens.All[0].id;
        view.Rebuild();
        GreenDefinition def = view.Definition;
        GreenPutt firstPutt = def.putts[0];

        // Hole with flag
        var hole = new GameObject("Hole");
        hole.transform.position = view.PinWorldPosition(firstPutt.pin);
        BuildFlag(hole.transform, stickMat, clothMat);

        // Ball
        GameObject ball = Primitive(PrimitiveType.Sphere, "Ball", null, ballMat);
        ball.transform.localScale = Vector3.one * rules.ballDiameter;
        ball.transform.position = view.SurfacePoint(firstPutt.ball) + Vector3.up * rules.BallRadius;
        var virtualBall = ball.AddComponent<VirtualBall>();
        virtualBall.hole = hole.transform;
        virtualBall.green = view;

        // Aim arrow (partner's mesh if available)
        GameObject arrow = BuildArrow();
        arrow.SetActive(false);

        // Camera
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 400f;
        camGo.AddComponent<AudioListener>();

        // Game
        var game = new GameObject("Game");
        var launcher = game.AddComponent<AimPowerLauncher>();
        launcher.ball = ball.transform;
        launcher.hole = hole.transform;
        launcher.arrow = arrow.transform;
        launcher.AimAtHole();

        var puttCam = camGo.AddComponent<PuttCamera>();
        puttCam.ball = ball.transform;
        puttCam.launcher = launcher;
        puttCam.Snap();

        var controller = game.AddComponent<GreenPracticeController>();
        controller.green = view;
        controller.ball = virtualBall;
        controller.launcher = launcher;
        controller.puttCamera = puttCam;
        controller.hole = hole.transform;
        controller.ground = ground.transform;
        controller.water = water.transform;

        float low = view.LowestVisualHeight - 0.01f;
        ground.transform.position = new Vector3(0f, low, 0f);
        water.transform.position = new Vector3(0f, low + 0.005f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FamousGreensBuilder] Saved {ScenePath} with {FamousGreens.All.Count} greens. Press Play; keys are shown on screen.");
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // putts are simulated, no colliders needed
        if (parent != null) go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void BuildFlag(Transform hole, Material stickMat, Material clothMat)
    {
        var flag = new GameObject("Flag");
        flag.transform.SetParent(hole, false);

        GameObject stick = Primitive(PrimitiveType.Cylinder, "Flagstick", flag.transform, stickMat); // 2 units tall
        stick.transform.localScale = new Vector3(FlagstickDiameter, FlagstickHeight * 0.5f, FlagstickDiameter);
        stick.transform.localPosition = Vector3.up * (FlagstickHeight * 0.5f);

        GameObject cloth = Primitive(PrimitiveType.Cube, "FlagCloth", flag.transform, clothMat);
        cloth.transform.localScale = FlagClothSize;
        cloth.transform.localPosition = new Vector3(-(FlagClothSize.x + FlagstickDiameter) * 0.5f,
            FlagstickHeight - FlagClothSize.y * 0.5f, 0f);
    }

    static GameObject BuildArrow()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArrowMeshPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ArrowMaterialPath);
        var arrow = new GameObject("AimArrow");
        if (mesh != null)
        {
            arrow.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = arrow.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat != null ? mat : UnlitMaterial("AimArrow", Color.white);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return arrow;
        }
        // Fallback: a thin bar pointing along +Z from the ball.
        GameObject bar = Primitive(PrimitiveType.Cube, "Bar", arrow.transform, UnlitMaterial("AimArrow", Color.white));
        bar.transform.localScale = new Vector3(0.03f, 0.002f, 0.9f);
        bar.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        bar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return arrow;
    }

    static Material LitMaterial(string name, Color color, float smoothness)
    {
        Material mat = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material UnlitMaterial(string name, Color color)
    {
        Material mat = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Uses vertex colours, so LineRenderer colours show.
    static Material SpriteMaterial(string name) => LoadOrCreate(name, Shader.Find("Sprites/Default"));

    static Material LoadOrCreate(string name, Shader shader)
    {
        string path = $"{GenDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        return mat;
    }
}
