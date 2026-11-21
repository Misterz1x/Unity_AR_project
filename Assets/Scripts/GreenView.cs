using System;
using System.Collections.Generic;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Shows a green from FamousGreens (or any GreenDefinition) and gives the physics its surface.
/// Builds the mesh and texture in code, at runtime and in the Editor (so it also works when the
/// green is placed in AR). The object's position is the green centre, its Y rotation turns the green.
/// Texture: grass with mowing stripes; optional slope colours (green = flat ... red = too steep to stop on)
/// and contour lines every few cm of height. The active hole is a small disc mesh on the surface.
/// </summary>
[ExecuteAlways]
public class GreenView : MonoBehaviour
{
    [Tooltip("Id from FamousGreens: practice, island, funnel, turtleback, lightning, double.")]
    public string greenId = "practice";

    [Tooltip("Active hole location (index into the green's pins).")]
    public int pin;

    [Header("Look")]
    [Tooltip("Drawn height = real height × this. 0 = flat (e.g. in AR on a real floor), 1 = real, 3 = exaggerated.")]
    [Range(0f, 5f)] public float visualHeightScale = 1f;
    [Tooltip("Height lines, like a map: thin every Contour Interval, stronger every 5th.")]
    public bool showContours;
    public bool showSlopeColours;
    [Tooltip("Height between contour lines (m).")]
    public float contourInterval = 0.02f;
    [Tooltip("Width of a contour line on the ground (m).")]
    public float contourWidth = 0.012f;
    [Tooltip("URP Lit material for the green; its base map is replaced by the generated texture.")]
    public Material greenMaterial;
    public Material fringeMaterial;
    [Tooltip("Unlit material for the ideal line and the ball path.")]
    public Material lineMaterial;

    [Header("Mesh")]
    [Range(64, 512)] public int segments = 200;
    [Range(8, 80)] public int rings = 40;
    [Range(256, 2048)] public int textureSize = 1024;

    public GreenModel Model { get; private set; }
    public GreenDefinition Definition => Model?.Definition;

    /// <summary>The green in world coordinates, for RollSimulator.</summary>
    public PlacedGreen Surface { get; private set; }

    /// <summary>Lowest drawn point of green and fringe, relative to this object (m).</summary>
    public float LowestVisualHeight { get; private set; }

    public event Action Rebuilt;

    static readonly Color GrassLight = new Color(0.33f, 0.60f, 0.22f);
    static readonly Color GrassDark = new Color(0.29f, 0.54f, 0.19f);
    static readonly Color FringeGrass = new Color(0.25f, 0.47f, 0.16f);
    const float StripeWidth = 1.5f;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Texture2D texture;
    Material greenInstance, fringeInstance;
    GameObject cup;
    Mesh cupMesh;
    Material cupInstance, rimInstance;
    LineRenderer idealLine, pathLine;
    bool dirty = true;
    Vector3 builtPosition;
    float builtYaw;

    // ------------------------------------------------------------------ public API

    public void SetGreen(string id, int pinIndex = 0)
    {
        greenId = id;
        pin = pinIndex;
        Rebuild();
    }

    public void SetPin(int pinIndex)
    {
        pin = pinIndex;
        if (Model == null) return;
        pin = Mathf.Clamp(pin, 0, Mathf.Max(0, Model.Definition.pins.Count - 1));
        UpdateCup();
    }

    /// <summary>Roll settings with this green's speed, keeping the ball and hole sizes from template.</summary>
    public RollSettings RollSettingsFor(RollSettings template) => Model != null ? Model.MakeRollSettings(template) : template;

    public Vector2 LocalToWorld(Vector2 local) => Surface != null ? Surface.ToWorld(local) : local;

    /// <summary>World position on the drawn surface for a green-local point.</summary>
    public Vector3 SurfacePoint(Vector2 local)
    {
        Vector2 w = LocalToWorld(local);
        return new Vector3(w.x, VisualHeightAt(w), w.y);
    }

    /// <summary>Drawn surface height (world Y) at a world floor position.</summary>
    public float VisualHeightAt(Vector2 world)
    {
        if (Surface == null) return transform.position.y;
        return transform.position.y + visualHeightScale * Surface.Local.HeightAt(Surface.ToLocal(world));
    }

    public Vector3 PinWorldPosition(int index) =>
        Model != null ? SurfacePoint(Model.PinPosition(index)) : transform.position;

    public bool IsOnGreen(Vector2 world) => Model != null && Model.IsOnGreen(Surface.ToLocal(world));

    public GrassKind GrassAt(Vector2 world) => Model != null ? Model.GrassAt(Surface.ToLocal(world)) : GrassKind.Green;

    /// <summary>Draws a ball path (world floor positions) on the green. ideal = yellow, else white.</summary>
    public void ShowPath(IReadOnlyList<PathPoint> path, bool ideal)
    {
        LineRenderer line = ideal ? EnsureLine(ref idealLine, "IdealLine", new Color(1f, 0.85f, 0.1f))
                                  : EnsureLine(ref pathLine, "BallPath", new Color(1f, 1f, 1f, 0.8f));
        if (path == null || path.Count < 2)
        {
            line.positionCount = 0;
            return;
        }
        var points = new List<Vector3>();
        float spacing = 0.05f;
        Vector2 last = path[0].Position;
        points.Add(LinePoint(last));
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 p = path[i].Position;
            if ((p - last).sqrMagnitude < spacing * spacing && i < path.Count - 1) continue;
            points.Add(LinePoint(p));
            last = p;
        }
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    public void ClearPaths()
    {
        if (idealLine != null) idealLine.positionCount = 0;
        if (pathLine != null) pathLine.positionCount = 0;
    }

    public bool IdealLineVisible => idealLine != null && idealLine.positionCount > 0;

    // ------------------------------------------------------------------ Unity

    void OnEnable()
    {
        dirty = true;
        Rebuild();
    }

    void OnValidate()
    {
        dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && dirty) Rebuild(); };
#endif
    }

    void Update()
    {
        if (dirty) Rebuild();
        // Moving or turning the object moves the physics green with it.
        if (Model != null && (transform.position != builtPosition || !Mathf.Approximately(transform.eulerAngles.y, builtYaw)))
            UpdatePlacement();
    }

    void OnDisable()
    {
        Release(mesh);
        Release(texture);
        Release(greenInstance);
        Release(fringeInstance);
        Release(cupMesh);
        Release(cupInstance);
        Release(rimInstance);
        mesh = null;
        texture = null;
        cupMesh = null;
        greenInstance = fringeInstance = cupInstance = rimInstance = null;
    }

    // ------------------------------------------------------------------ building

    public void Rebuild()
    {
        dirty = false;
        GreenDefinition d = FamousGreens.Find(greenId) ?? FamousGreens.All[0];
        greenId = d.id;
        pin = Mathf.Clamp(pin, 0, Mathf.Max(0, d.pins.Count - 1));
        Model = GreenModel.Build(d);
        UpdatePlacement();

        meshFilter = GetOrAdd<MeshFilter>();
        meshRenderer = GetOrAdd<MeshRenderer>();

        Release(mesh);
        mesh = BuildMesh(Model, out float lowest);
        LowestVisualHeight = lowest;
        meshFilter.sharedMesh = mesh;

        Release(greenInstance);
        Release(fringeInstance);
        greenInstance = MakeMaterial(greenMaterial, Color.white);
        fringeInstance = MakeMaterial(fringeMaterial, FringeGrass);
        meshRenderer.sharedMaterials = new[] { greenInstance, fringeInstance };

        RebuildTexture();
        UpdateCup();
        ClearPaths();
        Rebuilt?.Invoke();
    }

    void UpdatePlacement()
    {
        builtPosition = transform.position;
        builtYaw = transform.eulerAngles.y;
        Surface = new PlacedGreen(Model, new Vector2(builtPosition.x, builtPosition.z), builtYaw, builtPosition.y);
    }

    // Polar mesh: rings from the centre to the edge (submesh 0, green), then a fringe ring
    // and a skirt down to below the lowest point (submesh 1).
    Mesh BuildMesh(GreenModel model, out float lowest)
    {
        GreenOutline o = model.Definition.outline;
        float fringe = model.Definition.fringeWidth;
        int n = segments;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        TextureFrame(model, out Vector2 texMin, out float texSize);

        Vector3 Vertex(Vector2 local, float drop = 0f)
        {
            uvs.Add((local - texMin) / texSize);
            return new Vector3(local.x, visualHeightScale * model.HeightAt(local) - drop, local.y);
        }

        // Centre + rings.
        vertices.Add(Vertex(o.center));
        for (int r = 1; r <= rings; r++)
        {
            float t = (float)r / rings;
            for (int k = 0; k < n; k++)
            {
                float phi = 2f * Mathf.PI * k / n;
                vertices.Add(Vertex(Vector2.Lerp(o.center, o.EdgePoint(phi), t)));
            }
        }
        int fringeStart = vertices.Count;
        for (int k = 0; k < n; k++) vertices.Add(Vertex(o.EdgePoint(2f * Mathf.PI * k / n, fringe)));

        lowest = 0f;
        for (int i = 0; i < vertices.Count; i++) lowest = Mathf.Min(lowest, vertices[i].y);
        float skirtY = lowest - 0.05f;
        lowest = skirtY;
        // Skirt: own vertices (top and bottom), so its normals point sideways.
        int skirtTop = vertices.Count;
        for (int k = 0; k < n; k++)
        {
            vertices.Add(vertices[fringeStart + k]);
            uvs.Add(uvs[fringeStart + k]);
        }
        int skirtStart = vertices.Count;
        for (int k = 0; k < n; k++)
        {
            Vector3 top = vertices[fringeStart + k];
            vertices.Add(new Vector3(top.x, skirtY, top.z));
            uvs.Add(uvs[fringeStart + k]);
        }

        var green = new List<int>();
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            green.Add(0); green.Add(1 + k2); green.Add(1 + k);
        }
        for (int r = 1; r < rings; r++)
        {
            int inner = 1 + (r - 1) * n, outer = 1 + r * n;
            for (int k = 0; k < n; k++)
                Quad(green, inner + k, inner + (k + 1) % n, outer + k, outer + (k + 1) % n);
        }

        var rest = new List<int>();
        int edge = 1 + (rings - 1) * n;
        for (int k = 0; k < n; k++)
        {
            Quad(rest, edge + k, edge + (k + 1) % n, fringeStart + k, fringeStart + (k + 1) % n);
            Quad(rest, skirtTop + k, skirtTop + (k + 1) % n, skirtStart + k, skirtStart + (k + 1) % n);
        }

        var m = new Mesh { name = "Green " + model.Definition.id, hideFlags = HideFlags.DontSave };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(vertices);
        m.SetUVs(0, uvs);
        m.subMeshCount = 2;
        m.SetTriangles(green, 0);
        m.SetTriangles(rest, 1);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Two triangles, facing up, for ring quad (a, b inner; c, d outer), counter-clockwise order in phi.
    static void Quad(List<int> t, int a, int b, int c, int d)
    {
        t.Add(a); t.Add(b); t.Add(c);
        t.Add(b); t.Add(d); t.Add(c);
    }

    static void TextureFrame(GreenModel model, out Vector2 min, out float size)
    {
        float reach = model.Definition.outline.MaxRadius() + model.Definition.fringeWidth;
        min = model.Definition.outline.center - Vector2.one * reach;
        size = 2f * reach;
    }

    void RebuildTexture()
    {
        if (Model == null) return;
        TextureFrame(Model, out Vector2 min, out float size);
        int res = textureSize;
        if (texture == null || texture.width != res)
        {
            Release(texture);
            texture = new Texture2D(res, res, TextureFormat.RGBA32, true)
            {
                name = "Green texture",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
            };
        }

        float holding = Model.MakeRollSettings().MaxHoldingSlope;
        float texel = size / res;
        var pixels = new Color32[res * res];
        bool needHeights = showContours || showSlopeColours;

        // Smooth heights from the feature formulas (not the 10 cm grid), shifted like the model.
        float[] heights = null;
        if (needHeights)
        {
            GreenDefinition d = Model.Definition;
            float offset = d.FeatureHeightAt(Model.Heights.Origin) - Model.Heights[0, 0];
            heights = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    heights[x + y * res] = d.FeatureHeightAt(min + new Vector2((x + 0.5f) * texel, (y + 0.5f) * texel)) - offset;
        }

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float stripeY = min.y + (y + 0.5f) * texel;
                // Soft edge between mowing stripes.
                float stripe = Mathf.Repeat(stripeY, 2f * StripeWidth) / StripeWidth; // 0..2
                float light = Mathf.Clamp01(Mathf.Min(Mathf.Abs(stripe - 0f), Mathf.Abs(stripe - 1f), Mathf.Abs(stripe - 2f)) * StripeWidth / (2f * texel));
                Color c = stripe < 1f ? Color.Lerp(GrassDark, GrassLight, 0.5f + 0.5f * light) : Color.Lerp(GrassLight, GrassDark, 0.5f + 0.5f * light);

                if (needHeights)
                {
                    int k = x + y * res;
                    int xl = Mathf.Max(0, x - 1), xr = Mathf.Min(res - 1, x + 1);
                    int yd = Mathf.Max(0, y - 1), yu = Mathf.Min(res - 1, y + 1);
                    float gx = (heights[xr + y * res] - heights[xl + y * res]) / ((xr - xl) * texel);
                    float gy = (heights[x + yu * res] - heights[x + yd * res]) / ((yu - yd) * texel);
                    float slope = Mathf.Sqrt(gx * gx + gy * gy);

                    if (showSlopeColours)
                        c = Color.Lerp(c, SlopeColour(slope / holding), 0.55f);

                    if (showContours && contourInterval > 0f)
                    {
                        float level = heights[k] / contourInterval;
                        float nearest = Mathf.Round(level);
                        // Distance to the nearest line on the ground (m), from height distance / slope.
                        float dist = Mathf.Abs(level - nearest) * contourInterval / Mathf.Max(slope, 1e-4f);
                        bool major = Mathf.Abs(Mathf.Repeat(nearest, 5f)) < 0.5f;
                        float half = (major ? 1.8f : 1f) * Mathf.Max(contourWidth, texel) * 0.5f;
                        float coverage = Mathf.Clamp01(1f - (dist - half) / texel); // 1 texel soft edge
                        c = Color.Lerp(c, new Color(0.08f, 0.2f, 0.05f), coverage * (major ? 0.55f : 0.35f));
                    }
                }

                pixels[x + y * res] = c;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(true);

        if (greenInstance != null)
        {
            if (greenInstance.HasProperty("_BaseMap")) greenInstance.SetTexture("_BaseMap", texture);
            if (greenInstance.HasProperty("_MainTex")) greenInstance.SetTexture("_MainTex", texture);
        }
    }

    // The active hole: a dark disc with a light rim, as a small mesh that follows the green's surface,
    // so it stays round and sharp at any distance.
    void UpdateCup()
    {
        if (Model == null) return;
        if (cup == null)
        {
            Transform existing = transform.Find("Cup");
            cup = existing != null ? existing.gameObject : new GameObject("Cup");
            cup.hideFlags = HideFlags.DontSave;
            cup.transform.SetParent(transform, false);
            cup.transform.localPosition = Vector3.zero;
            cup.transform.localRotation = Quaternion.identity;
            if (cup.GetComponent<MeshFilter>() == null) cup.AddComponent<MeshFilter>();
            var r = cup.GetComponent<MeshRenderer>();
            if (r == null) r = cup.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (cupInstance == null)
        {
            cupInstance = MakeMaterial(null, new Color(0.04f, 0.04f, 0.04f));
            rimInstance = MakeMaterial(null, new Color(0.85f, 0.85f, 0.82f));
        }

        Vector2 hole = Model.PinPosition(pin);
        float r0 = Model.MakeRollSettings().HoleRadius, r1 = r0 + 0.004f;
        const int n = 48;
        const float lift = 0.0015f;
        var vertices = new List<Vector3>();
        Vector3 Point(Vector2 local) => new Vector3(local.x, visualHeightScale * Model.HeightAt(local) + lift, local.y);

        vertices.Add(Point(hole));
        for (int k = 0; k < n; k++)
        {
            float a = 2f * Mathf.PI * k / n;
            vertices.Add(Point(hole + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r0));
        }
        for (int k = 0; k < n; k++)
        {
            float a = 2f * Mathf.PI * k / n;
            vertices.Add(Point(hole + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r1));
        }

        var disc = new List<int>();
        var rim = new List<int>();
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            disc.Add(0); disc.Add(1 + k2); disc.Add(1 + k);
            Quad(rim, 1 + k, 1 + k2, 1 + n + k, 1 + n + k2);
        }

        if (cupMesh == null) cupMesh = new Mesh { name = "Cup", hideFlags = HideFlags.DontSave };
        cupMesh.Clear();
        cupMesh.SetVertices(vertices);
        cupMesh.subMeshCount = 2;
        cupMesh.SetTriangles(disc, 0);
        cupMesh.SetTriangles(rim, 1);
        cupMesh.RecalculateNormals();
        cupMesh.RecalculateBounds();
        cup.GetComponent<MeshFilter>().sharedMesh = cupMesh;
        cup.GetComponent<MeshRenderer>().sharedMaterials = new[] { cupInstance, rimInstance };
    }

    // 0 = flat ... 1 = steepest slope a ball can stop on (and beyond).
    static Color SlopeColour(float t)
    {
        if (t < 0.25f) return Color.Lerp(new Color(0.2f, 0.5f, 1f), new Color(0.2f, 0.85f, 0.3f), t / 0.25f);
        if (t < 0.6f) return Color.Lerp(new Color(0.2f, 0.85f, 0.3f), new Color(1f, 0.9f, 0.2f), (t - 0.25f) / 0.35f);
        if (t < 1f) return Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.3f, 0.1f), (t - 0.6f) / 0.4f);
        return new Color(0.8f, 0.1f, 0.6f);
    }

    Material MakeMaterial(Material template, Color tint)
    {
        Material m;
        if (template != null)
            m = new Material(template);
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
        }
        m.hideFlags = HideFlags.DontSave;
        if (template == null)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        }
        return m;
    }

    LineRenderer EnsureLine(ref LineRenderer line, string name, Color colour)
    {
        if (line != null) return line;
        Transform existing = transform.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        line = go.GetComponent<LineRenderer>();
        if (line == null) line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = 0.02f;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (lineMaterial != null) line.sharedMaterial = lineMaterial;
        line.startColor = line.endColor = colour;
        line.positionCount = 0;
        return line;
    }

    Vector3 LinePoint(Vector2 world) => new Vector3(world.x, VisualHeightAt(world) + 0.012f, world.y);

    T GetOrAdd<T>() where T : Component
    {
        T c = GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    static void Release(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
