using System.Collections.Generic;
using System.IO;
using ARGolf.Putting;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds a realistic-looking putting green at real-world scale (metres):
/// an oval green with natural bulges, a cup (108 mm) with a flag, a fringe ring, rough,
/// a sand bunker, Kenney Nature Pack trees and bushes, a ball, a camera and a light.
/// Meshes and grass textures are generated in code into Assets/Generated/PuttingGreen.
/// Run from Tools > AR Golf > Build Putting Green Scene. Rebuilding overwrites the scene.
/// </summary>
public static class PuttingGreenBuilder
{
    const string ScenePath = "Assets/Scenes/PuttingGreen.unity";
    const string GenDir = "Assets/Generated/PuttingGreen";
    const string NatureDir = "Assets/ThirdParty/Kenney Nature Pack/Models";

    // ---------- Green shape (metres) ----------
    const float GreenHalfWidth = 6f;   // along X  -> green about 12 m wide
    const float GreenHalfLength = 8f;  // along Z  -> green about 16 m long
    // Gentle bulges on the oval: (harmonic, amplitude, phase).
    static readonly Vector3[] GreenWobble = { new Vector3(2, 0.07f, 0.6f), new Vector3(3, 0.045f, 2.1f), new Vector3(5, 0.025f, 4f) };
    const float FringeWidth = 0.9f;

    // ---------- Hole and putt ----------
    static readonly Vector3 HolePosition = new Vector3(1.5f, 0f, 4.5f);
    const float PuttLength = 4f;          // ball starts this far from the hole
    const float PuttHeading = -12f;       // degrees from +Z, direction ball -> hole
    const float CupDepth = 0.1016f;       // 4 in
    const float LinerTopDepth = 0.025f;   // white cup liner starts this far below the surface

    // ---------- Flag (round stick, cloth at the top) ----------
    const float FlagstickDiameter = 0.015f;
    const float FlagstickHeight = 1.4f;
    static readonly Vector2 FlagClothSize = new Vector2(0.45f, 0.3f);
    const float FlagClothThickness = 0.005f;

    // ---------- Bunker ----------
    static readonly Vector3 BunkerCenter = new Vector3(-9.8f, 0f, 1f);
    const float BunkerHalfA = 2.2f, BunkerHalfB = 1.4f, BunkerRotation = 25f;

    // ---------- Surface heights, a few mm apart to avoid z-fighting ----------
    const float RoughY = 0f, BunkerY = 0.003f, FringeY = 0.004f, GreenY = 0.006f;

    // ---------- Texture repeat size in the world (metres per texture tile) ----------
    const float GreenTexWorld = 3f;   // contains two 1.5 m mowing stripes
    const float FringeTexWorld = 2f;
    const float RoughTexWorld = 4f;
    const float SandTexWorld = 1.5f;
    const float RoughSize = 200f;
    const int TextureSize = 512;

    // ---------- Mesh resolution ----------
    const int GreenSegments = 160, GreenRings = 30, FringeSegments = 200, BunkerSegments = 96, BunkerRings = 6;

    // ---------- Trees and bushes ----------
    const int TreeCount = 10, BushCount = 10, RandomSeed = 12345;
    static readonly string[] TreeModels = { "naturePack_051.fbx", "naturePack_164.fbx", "naturePack_059.fbx" };
    static readonly string[] BushModels = { "naturePack_018.fbx", "naturePack_021.fbx", "naturePack_flat_013.fbx" };

    // ---------- Aim arrow (flat on the ground, metres) ----------
    const float ArrowStart = 0.05f, ArrowLength = 0.9f, ArrowShaftWidth = 0.035f;
    const float ArrowHeadWidth = 0.14f, ArrowHeadLength = 0.16f;

    [MenuItem("Tools/AR Golf/Build Putting Green Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(GenDir);
        var rules = new RollSettings();
        float holeR = rules.HoleRadius;

        // ---------- Textures and materials ----------
        var greenTex = MakeTexture("GreenGrass", 11, (u, v, s) => Grass(u, v, s, new Color(0.31f, 0.56f, 0.20f), 0.10f, 8, 0.07f));
        var fringeTex = MakeTexture("FringeGrass", 23, (u, v, s) => Grass(u, v, s, new Color(0.27f, 0.50f, 0.17f), 0.16f, 6, 0f));
        var roughTex = MakeTexture("RoughGrass", 37, (u, v, s) => Grass(u, v, s, new Color(0.23f, 0.43f, 0.14f), 0.24f, 4, 0f));
        var sandTex = MakeTexture("Sand", 51, (u, v, s) => Sand(u, v, s));

        Material greenMat = MakeMaterial("Green", Color.white, 0.12f, greenTex);
        Material fringeMat = MakeMaterial("Fringe", Color.white, 0.08f, fringeTex);
        Material roughMat = MakeMaterial("Rough", Color.white, 0.05f, roughTex, Vector2.one * (RoughSize / RoughTexWorld));
        Material sandMat = MakeMaterial("Sand", Color.white, 0.05f, sandTex);
        Material soilMat = MakeMaterial("CupSoil", new Color(0.25f, 0.18f, 0.12f), 0.05f);
        Material linerMat = MakeMaterial("CupLiner", new Color(0.92f, 0.92f, 0.9f), 0.3f);
        Material stickMat = MakeMaterial("Flagstick", new Color(0.95f, 0.95f, 0.95f), 0.5f);
        Material clothMat = MakeMaterial("FlagCloth", new Color(0.95f, 0.78f, 0.1f), 0.1f);
        Material ballMat = MakeMaterial("GolfBall", Color.white, 0.7f);

        // ---------- Meshes ----------
        Mesh greenMesh = SaveMesh(BuildGreenMesh(holeR), "GreenMesh");
        Mesh fringeMesh = SaveMesh(BuildFringeMesh(), "FringeMesh");
        Mesh bunkerMesh = SaveMesh(BuildBunkerMesh(), "BunkerMesh");
        Mesh arrowMesh = SaveMesh(BuildArrowMesh(), "AimArrowMesh");
        Material arrowMat = MakeUnlitMaterial("AimArrow", new Color(1f, 1f, 1f));

        // ---------- Scene ----------
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.62f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.9f);

        // Environment
        GameObject environment = new GameObject("Environment");

        GameObject lightGo = new GameObject("Directional Light");
        lightGo.transform.SetParent(environment.transform);
        lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        Light sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        GameObject rough = GameObject.CreatePrimitive(PrimitiveType.Plane);
        rough.name = "Rough";
        rough.transform.SetParent(environment.transform);
        rough.transform.position = new Vector3(0f, RoughY, 0f);
        rough.transform.localScale = new Vector3(RoughSize / 10f, 1f, RoughSize / 10f); // Plane mesh is 10 x 10
        rough.GetComponent<Renderer>().sharedMaterial = roughMat;

        PlaceVegetation(environment.transform, scene);

        // Course
        GameObject course = new GameObject("Course");
        GameObject greenGo = MeshObject("Green", course.transform, greenMesh, greenMat, soilMat, linerMat);
        MeshCollider greenCollider = greenGo.AddComponent<MeshCollider>(); // used to find random spots on the green
        greenCollider.sharedMesh = greenMesh;
        MeshObject("Fringe", course.transform, fringeMesh, fringeMat);
        MeshObject("Bunker", course.transform, bunkerMesh, sandMat);

        // Hole marker: centre of the cup at green level. VirtualBall.hole uses its X/Z.
        Vector3 holeCenter = new Vector3(HolePosition.x, GreenY, HolePosition.z);
        GameObject hole = new GameObject("Hole");
        hole.transform.SetParent(course.transform);
        hole.transform.position = holeCenter;
        BuildFlag(hole.transform, holeCenter.y - CupDepth, stickMat, clothMat);

        // Ball, real size, PuttLength in front of the hole.
        Vector3 puttDir = Quaternion.Euler(0f, PuttHeading, 0f) * Vector3.forward;
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        Object.DestroyImmediate(ball.GetComponent<Collider>()); // putt is simulated, no colliders needed
        ball.transform.SetParent(course.transform);
        ball.transform.localScale = Vector3.one * rules.ballDiameter;
        ball.transform.position = holeCenter - puttDir * PuttLength + Vector3.up * rules.BallRadius;
        ball.GetComponent<Renderer>().sharedMaterial = ballMat;
        VirtualBall virtualBall = ball.AddComponent<VirtualBall>();
        virtualBall.hole = hole.transform;

        GameObject arrow = MeshObject("AimArrow", course.transform, arrowMesh, arrowMat);
        arrow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        arrow.SetActive(false);

        // Camera: behind the ball, looking down the putt line.
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 300f;
        camGo.AddComponent<AudioListener>();
        Vector3 ballPos = ball.transform.position;
        camGo.transform.position = ballPos - puttDir * 1.8f + Vector3.up * 1.3f;
        camGo.transform.LookAt(ballPos + puttDir * 2.2f);

        // ---------- Game: keyboard aim + power gauge, buttons ----------
        GameObject game = new GameObject("Game");
        AimPowerLauncher launcher = game.AddComponent<AimPowerLauncher>();
        launcher.ball = ball.transform;
        launcher.hole = hole.transform;
        launcher.arrow = arrow.transform;

        PuttCamera puttCam = camGo.AddComponent<PuttCamera>();
        puttCam.ball = ball.transform;
        puttCam.launcher = launcher;

        PuttingGameController controller = game.AddComponent<PuttingGameController>();
        controller.ball = virtualBall;
        controller.launcher = launcher;
        controller.puttCamera = puttCam;
        controller.hole = hole.transform;
        controller.green = greenCollider;

        BuildUi(controller, launcher);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PuttingGreenBuilder] Saved {ScenePath}. Green about {GreenHalfWidth * 2:F0} x {GreenHalfLength * 2:F0} m, " +
                  $"putt {PuttLength:F1} m.");
    }

    // =====================================================================
    // Shapes
    // =====================================================================

    static float GreenRadius(float phi)
    {
        float c = Mathf.Cos(phi) / GreenHalfWidth, s = Mathf.Sin(phi) / GreenHalfLength;
        float r = 1f / Mathf.Sqrt(c * c + s * s);
        float wobble = 1f;
        foreach (var w in GreenWobble) wobble += w.y * Mathf.Sin(w.x * phi + w.z);
        return r * wobble;
    }

    static Vector3 GreenEdge(float phi, float extra)
    {
        float r = GreenRadius(phi) + extra;
        return new Vector3(r * Mathf.Cos(phi), 0f, r * Mathf.Sin(phi));
    }

    static bool InsideGreen(Vector3 p) =>
        new Vector2(p.x, p.z).magnitude < GreenRadius(Mathf.Atan2(p.z, p.x));

    // Distance from 'from' along 'dir' to where it first leaves the green.
    static float DistanceToGreenEdge(Vector3 from, Vector3 dir)
    {
        const float step = 0.1f;
        float t = 0f;
        while (InsideGreen(from + dir * (t + step)) && t < 100f) t += step;
        float lo = t, hi = t + step;
        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (InsideGreen(from + dir * mid)) lo = mid; else hi = mid;
        }
        return lo;
    }

    static float BunkerRadius(float phi)
    {
        float c = Mathf.Cos(phi) / BunkerHalfA, s = Mathf.Sin(phi) / BunkerHalfB;
        return 1f / Mathf.Sqrt(c * c + s * s) * (1f + 0.08f * Mathf.Sin(2f * phi + 1f) + 0.05f * Mathf.Sin(3f * phi + 0.3f));
    }

    // =====================================================================
    // Meshes
    // =====================================================================

    // Green surface as rings around the hole, so the cup is an exact circle.
    // Submeshes: 0 grass, 1 soil (top of the cup wall), 2 white liner (rest of the cup).
    static Mesh BuildGreenMesh(float holeR)
    {
        var md = new MeshData(3);
        Vector3 h = new Vector3(HolePosition.x, GreenY, HolePosition.z);
        int n = GreenSegments, m = GreenRings;

        var grid = new int[n, m + 1];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float edge = DistanceToGreenEdge(h, dir);
            for (int j = 0; j <= m; j++)
            {
                float r = Mathf.Lerp(holeR, edge, (float)j / m);
                Vector3 p = h + dir * r;
                grid[i, j] = md.Add(p, Vector3.up, new Vector2(p.x, p.z) / GreenTexWorld);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int i2 = (i + 1) % n;
            for (int j = 0; j < m; j++)
                md.Quad(0, grid[i, j], grid[i, j + 1], grid[i2, j + 1], grid[i2, j]);
        }

        // Cup wall (normals point into the hole) and bottom.
        float yTop = GreenY, yMid = GreenY - LinerTopDepth, yBot = GreenY - CupDepth;
        var top = new int[n]; var mid = new int[n]; var midL = new int[n]; var bot = new int[n]; var floorRing = new int[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 p = new Vector3(h.x, 0f, h.z) + dir * holeR;
            float u = (float)i / n * 4f;
            top[i] = md.Add(p + Vector3.up * yTop, -dir, new Vector2(u, 0f));
            mid[i] = md.Add(p + Vector3.up * yMid, -dir, new Vector2(u, 0.25f));
            midL[i] = md.Add(p + Vector3.up * yMid, -dir, new Vector2(u, 0.25f));
            bot[i] = md.Add(p + Vector3.up * yBot, -dir, new Vector2(u, 1f));
            floorRing[i] = md.Add(p + Vector3.up * yBot, Vector3.up, new Vector2(dir.x, dir.z));
        }
        int center = md.Add(new Vector3(h.x, yBot, h.z), Vector3.up, Vector2.zero);
        for (int i = 0; i < n; i++)
        {
            int i2 = (i + 1) % n;
            md.Quad(1, top[i], top[i2], mid[i2], mid[i]);
            md.Quad(2, midL[i], midL[i2], bot[i2], bot[i]);
            md.Tri(2, center, floorRing[i], floorRing[i2]);
        }
        return md.ToMesh("GreenMesh");
    }

    // Fringe: a band around the green edge (starts slightly under the green to hide gaps).
    static Mesh BuildFringeMesh()
    {
        var md = new MeshData(1);
        int n = FringeSegments;
        var inner = new int[n]; var outer = new int[n];
        for (int i = 0; i < n; i++)
        {
            float phi = i * Mathf.PI * 2f / n;
            Vector3 a = GreenEdge(phi, -0.08f) + Vector3.up * FringeY;
            Vector3 b = GreenEdge(phi, FringeWidth) + Vector3.up * FringeY;
            inner[i] = md.Add(a, Vector3.up, new Vector2(a.x, a.z) / FringeTexWorld);
            outer[i] = md.Add(b, Vector3.up, new Vector2(b.x, b.z) / FringeTexWorld);
        }
        for (int i = 0; i < n; i++)
        {
            int i2 = (i + 1) % n;
            md.Quad(0, inner[i], outer[i], outer[i2], inner[i2]);
        }
        return md.ToMesh("FringeMesh");
    }

    static Mesh BuildBunkerMesh()
    {
        var md = new MeshData(1);
        Quaternion rot = Quaternion.Euler(0f, BunkerRotation, 0f);
        Vector3 c = BunkerCenter + Vector3.up * BunkerY;
        int n = BunkerSegments, m = BunkerRings;
        int center = md.Add(c, Vector3.up, new Vector2(c.x, c.z) / SandTexWorld);
        var grid = new int[n, m + 1];
        for (int i = 0; i < n; i++)
        {
            float phi = i * Mathf.PI * 2f / n;
            Vector3 dir = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
            float edge = BunkerRadius(phi);
            for (int j = 1; j <= m; j++)
            {
                Vector3 p = c + rot * (dir * edge * j / m);
                grid[i, j] = md.Add(p, Vector3.up, new Vector2(p.x, p.z) / SandTexWorld);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int i2 = (i + 1) % n;
            md.Tri(0, center, grid[i, 1], grid[i2, 1]);
            for (int j = 1; j < m; j++)
                md.Quad(0, grid[i, j], grid[i, j + 1], grid[i2, j + 1], grid[i2, j]);
        }
        return md.ToMesh("BunkerMesh");
    }

    // Flat arrow pointing along +Z, pivot at the ball centre.
    static Mesh BuildArrowMesh()
    {
        var md = new MeshData(1);
        float shaftEnd = ArrowLength - ArrowHeadLength, sw = ArrowShaftWidth * 0.5f, hw = ArrowHeadWidth * 0.5f;
        int a = md.Add(new Vector3(-sw, 0f, ArrowStart), Vector3.up, Vector2.zero);
        int b = md.Add(new Vector3(-sw, 0f, shaftEnd), Vector3.up, Vector2.zero);
        int c = md.Add(new Vector3(sw, 0f, shaftEnd), Vector3.up, Vector2.zero);
        int d = md.Add(new Vector3(sw, 0f, ArrowStart), Vector3.up, Vector2.zero);
        md.Quad(0, a, b, c, d);
        int h1 = md.Add(new Vector3(-hw, 0f, shaftEnd), Vector3.up, Vector2.zero);
        int h2 = md.Add(new Vector3(0f, 0f, ArrowLength), Vector3.up, Vector2.zero);
        int h3 = md.Add(new Vector3(hw, 0f, shaftEnd), Vector3.up, Vector2.zero);
        md.Tri(0, h1, h2, h3);
        return md.ToMesh("AimArrowMesh");
    }

    class MeshData
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] subs;

        public MeshData(int submeshes)
        {
            subs = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) subs[i] = new List<int>();
        }

        public int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            verts.Add(p); normals.Add(n); uvs.Add(uv);
            return verts.Count - 1;
        }

        // Adds a triangle, flipping its winding if needed so it faces along its normals.
        public void Tri(int sub, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0f) (b, c) = (c, b);
            subs[sub].Add(a); subs[sub].Add(b); subs[sub].Add(c);
        }

        public void Quad(int sub, int a, int b, int c, int d)
        {
            Tri(sub, a, b, c);
            Tri(sub, a, c, d);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    // =====================================================================
    // Scene objects
    // =====================================================================

    static GameObject MeshObject(string name, Transform parent, Mesh mesh, params Material[] mats)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        return go;
    }

    static void BuildFlag(Transform hole, float baseY, Material stickMat, Material clothMat)
    {
        GameObject flag = new GameObject("Flag");
        flag.transform.SetParent(hole);
        flag.transform.position = new Vector3(hole.position.x, baseY, hole.position.z);

        GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // mesh is 2 units tall
        stick.name = "Flagstick";
        Object.DestroyImmediate(stick.GetComponent<Collider>());
        stick.transform.SetParent(flag.transform, false);
        stick.transform.localScale = new Vector3(FlagstickDiameter, FlagstickHeight * 0.5f, FlagstickDiameter);
        stick.transform.localPosition = Vector3.up * (FlagstickHeight * 0.5f);
        stick.GetComponent<Renderer>().sharedMaterial = stickMat;

        GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cloth.name = "FlagCloth";
        Object.DestroyImmediate(cloth.GetComponent<Collider>());
        cloth.transform.SetParent(flag.transform, false);
        cloth.transform.localScale = new Vector3(FlagClothSize.x, FlagClothSize.y, FlagClothThickness);
        cloth.transform.localPosition = new Vector3(
            -(FlagClothSize.x + FlagstickDiameter) * 0.5f, FlagstickHeight - FlagClothSize.y * 0.5f, 0f);
        cloth.GetComponent<Renderer>().sharedMaterial = clothMat;
    }

    // =====================================================================
    // UI: two buttons (top left), status text (bottom), power gauge (right)
    // =====================================================================

    static void BuildUi(PuttingGameController controller, AimPowerLauncher launcher)
    {
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
        Transform root = canvasGo.transform;

        Button newBall = UiButton("NewBallButton", "New ball location", root, new Vector2(40f, -40f), new Color(0.2f, 0.45f, 0.8f));
        UnityEventTools.AddPersistentListener(newBall.onClick, new UnityAction(controller.NewBallLocation));
        Button play = UiButton("PlayButton", "Play", root, new Vector2(40f, -130f), new Color(0.25f, 0.65f, 0.3f));
        UnityEventTools.AddPersistentListener(play.onClick, new UnityAction(controller.Play));
        controller.newBallButton = newBall;
        controller.playButton = play;

        Text status = UiText("StatusText", root, "", 32, TextAnchor.MiddleCenter);
        RectTransform srt = status.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 40f);
        srt.sizeDelta = new Vector2(1700f, 60f);
        controller.statusText = status;

        launcher.gauge = BuildGauge(root);

        GameObject es = new GameObject("EventSystem");
        es.transform.SetParent(ui.transform);
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static PowerGauge BuildGauge(Transform root)
    {
        const float barWidth = 60f, barHeight = 520f, frame = 6f;

        GameObject gaugeGo = new GameObject("PowerGauge", typeof(RectTransform));
        gaugeGo.transform.SetParent(root, false);
        var grt = (RectTransform)gaugeGo.transform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(1f, 0.5f);
        grt.anchoredPosition = new Vector2(-90f, 0f);
        grt.sizeDelta = new Vector2(barWidth + frame * 2f, barHeight + frame * 2f);
        PowerGauge gauge = gaugeGo.AddComponent<PowerGauge>();

        GameObject visuals = new GameObject("Visuals", typeof(RectTransform));
        visuals.transform.SetParent(gaugeGo.transform, false);
        Stretch((RectTransform)visuals.transform, 0f);

        Image background = UiImage("Frame", visuals.transform, new Color(0f, 0f, 0f, 0.6f));
        Stretch(background.rectTransform, 0f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(visuals.transform, false);
        RawImage fill = fillGo.AddComponent<RawImage>();
        Stretch(fill.rectTransform, frame);

        // Ticks and labels at 0, 50, 100.
        foreach (int v in new[] { 0, 50, 100 })
        {
            float y = v / 100f;
            Image tick = UiImage($"Tick_{v}", fillGo.transform, Color.white);
            RectTransform trt = tick.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, y);
            trt.pivot = new Vector2(1f, 0.5f);
            trt.anchoredPosition = new Vector2(-frame - 4f, 0f);
            trt.sizeDelta = new Vector2(18f, 4f);

            Text label = UiText($"Label_{v}", fillGo.transform, v.ToString(), 28, TextAnchor.MiddleRight);
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, y);
            lrt.pivot = new Vector2(1f, 0.5f);
            lrt.anchoredPosition = new Vector2(-frame - 28f, 0f);
            lrt.sizeDelta = new Vector2(80f, 40f);
        }

        // Marker line: spans the bar plus a little overhang, moved by PowerGauge.SetValue.
        Image marker = UiImage("Marker", fillGo.transform, Color.white);
        RectTransform mrt = marker.rectTransform;
        mrt.anchorMin = new Vector2(0f, 0f);
        mrt.anchorMax = new Vector2(1f, 0f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.offsetMin = new Vector2(-14f, -4f);
        mrt.offsetMax = new Vector2(14f, 4f);
        Outline outline = marker.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, 2f);

        Text title = UiText("Title", visuals.transform, "Power", 30, TextAnchor.LowerCenter);
        RectTransform tirt = title.rectTransform;
        tirt.anchorMin = tirt.anchorMax = new Vector2(0.5f, 1f);
        tirt.pivot = new Vector2(0.5f, 0f);
        tirt.anchoredPosition = new Vector2(0f, 50f);
        tirt.sizeDelta = new Vector2(200f, 40f);

        Text value = UiText("Value", visuals.transform, "0", 36, TextAnchor.LowerCenter);
        RectTransform vrt = value.rectTransform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 1f);
        vrt.pivot = new Vector2(0.5f, 0f);
        vrt.anchoredPosition = new Vector2(0f, 8f);
        vrt.sizeDelta = new Vector2(200f, 44f);

        gauge.visuals = visuals;
        gauge.fill = fill;
        gauge.marker = mrt;
        gauge.valueText = value;
        visuals.SetActive(false);
        return gauge;
    }

    static Button UiButton(string name, string label, Transform parent, Vector2 pos, Color color)
    {
        var res = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        };
        GameObject go = DefaultControls.CreateButton(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(320f, 70f);
        go.GetComponent<Image>().color = color;

        Button button = go.GetComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None }; // Space must not press buttons

        Text text = go.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
        }
        return button;
    }

    static Text UiText(string name, Transform parent, string content, int size, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        Outline o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
        return t;
    }

    static Image UiImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // Trees in a loose ring well outside the green, bushes a bit closer. Fixed seed, same result every build.
    static void PlaceVegetation(Transform parent, Scene scene)
    {
        var rng = new System.Random(RandomSeed);
        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        GameObject trees = new GameObject("Trees");
        trees.transform.SetParent(parent);
        GameObject bushes = new GameObject("Bushes");
        bushes.transform.SetParent(parent);

        void Scatter(int count, string[] models, Transform group, string prefix,
                     float minGap, float maxGap, float minScale, float maxScale)
        {
            for (int i = 0; i < count; i++)
            {
                float phi = (i + Range(0.15f, 0.85f)) / count * Mathf.PI * 2f;
                Vector3 pos = GreenEdge(phi, FringeWidth + Range(minGap, maxGap));
                if (Vector3.Distance(pos, BunkerCenter) < BunkerHalfA + 2f)
                    pos += (pos - BunkerCenter).normalized * 3f;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{NatureDir}/{models[i % models.Length]}");
                if (model == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                go.name = $"{prefix}_{i + 1}";
                go.transform.SetParent(group);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(minScale, maxScale);
            }
        }

        Scatter(TreeCount, TreeModels, trees.transform, "Tree", 6f, 12f, 1.3f, 1.8f);
        Scatter(BushCount, BushModels, bushes.transform, "Bush", 2.5f, 5f, 1.5f, 2.5f);
    }

    // =====================================================================
    // Textures (tileable, generated from periodic value noise)
    // =====================================================================

    static Color Grass(float u, float v, int seed, Color baseColor, float variation, int period, float stripeContrast)
    {
        float broad = Fbm(u, v, period, 5, seed);
        float grain = ValueNoise(u, v, TextureSize / 2, seed + 99);
        float k = 1f + (broad - 0.5f) * 2f * variation + (grain - 0.5f) * 0.12f;
        if (stripeContrast > 0f)
            k *= 1f + Mathf.Clamp(Mathf.Sin(v * Mathf.PI * 2f) * 4f, -1f, 1f) * stripeContrast; // two mowing stripes
        Color c = baseColor * k;
        c.a = 1f;
        return c;
    }

    static Color Sand(float u, float v, int seed)
    {
        float broad = Fbm(u, v, 6, 4, seed);
        float grain = ValueNoise(u, v, TextureSize / 2, seed + 7);
        Color c = new Color(0.86f, 0.78f, 0.6f) * (1f + (broad - 0.5f) * 0.12f + (grain - 0.5f) * 0.16f);
        c.a = 1f;
        return c;
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    // Value noise that repeats every 'period' cells across u, v in [0, 1).
    static float ValueNoise(float u, float v, int period, int seed)
    {
        float x = u * period, y = v * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int Wrap(int i) => ((i % period) + period) % period;
        int xa = Wrap(x0), xb = Wrap(x0 + 1), ya = Wrap(y0), yb = Wrap(y0 + 1);
        float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
        float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * ValueNoise(u, v, period << o, seed + o * 31);
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static Texture2D MakeTexture(string name, int seed, System.Func<float, float, int, Color> pixel)
    {
        string path = $"{GenDir}/{name}.png";
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
            pixels[y * TextureSize + x] = pixel((float)x / TextureSize, (float)y / TextureSize, seed);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // =====================================================================
    // Asset helpers
    // =====================================================================

    static Material MakeMaterial(string name, Color color, float smoothness, Texture2D tex = null, Vector2? tiling = null)
    {
        string path = $"{GenDir}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeUnlitMaterial(string name, Color color)
    {
        string path = $"{GenDir}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{GenDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
