using System;
using System.Collections.Generic;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Shows a green from FamousGreens (or any GreenDefinition) and gives the physics its surface.
/// Builds the mesh and texture in code, at runtime and in the Editor (so it also works when the
/// green is placed in AR). The object's position is the green centre, its Y rotation turns the green.
/// Texture: grass with mowing stripes; optional slope colours (green = flat ... red = too steep to stop on)
/// and contour lines every few cm of height. The active hole is cut out of the green (texture alpha +
/// alpha clipping) with a cup below it: soil, white liner and bottom.
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
    [Tooltip("URP Lit material for the rough around the green (use the same as the ground).")]
    public Material roughMaterial;
    [Tooltip("Unlit material for the ideal line and the ball path.")]
    public Material lineMaterial;
    [Tooltip("Grey grass detail (URP Lit Detail Albedo x2). Empty = generated at runtime.")]
    public Texture2D grassDetail;

    [Header("Mesh")]
    [Range(64, 512)] public int segments = 200;
    [Range(8, 80)] public int rings = 40;
    [Tooltip("Upper limit for the green texture; it aims for ~1.5 cm per pixel.")]
    [Range(512, 4096)] public int textureSize = 2048;
    [Tooltip("Width of the rough slope around the fringe that blends down to the ground (m).")]
    public float roughApronWidth = 2.5f;

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
    static readonly Color RoughGrass = new Color(0.2f, 0.38f, 0.13f);
    const float StripeWidth = 1.5f;
    const float TargetTexel = 0.015f;            // green texture: ~1.5 cm per pixel
    const float GreenDetailTile = 0.5f, FringeDetailTile = 0.8f, RoughDetailTile = 1.4f; // m per detail tile
    const float FringeSink = 0.003f, RoughSink = 0.012f; // ball sits down in longer grass (m)

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Texture2D texture;
    Material greenInstance, fringeInstance, roughInstance;
    float groundLevel; // local height the rough apron blends down to
    Color32[] basePixels; // green texture without the hole cut
    GameObject cup;
    Mesh cupMesh;
    Material soilInstance, linerInstance, bottomInstance, rimInstance;
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
        StampHole();
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

    /// <summary>Drawn surface height (world Y) at a world floor position, including the rough apron.</summary>
    public float VisualHeightAt(Vector2 world)
    {
        if (Surface == null || Model == null) return transform.position.y;
        return transform.position.y + LocalVisualHeight(Surface.ToLocal(world));
    }

    /// <summary>How far the ball sits down into the grass here (m): fringe a little, rough more.</summary>
    public float SinkAt(Vector2 world)
    {
        switch (GrassAt(world))
        {
            case GrassKind.Fringe: return FringeSink;
            case GrassKind.Rough: return RoughSink;
            default: return 0f;
        }
    }

    /// <summary>Redraws the green texture only (after changing contours or slope colours).</summary>
    public void RefreshTexture() => RebuildTexture();

    // Green and fringe: the real shape (scaled). Rough: blends from the fringe edge down to the ground.
    float LocalVisualHeight(Vector2 local)
    {
        float h = visualHeightScale * Model.HeightAt(local);
        float beyondFringe = Model.Definition.outline.DistanceOutside(local) - Model.Definition.fringeWidth;
        if (beyondFringe <= 0f) return h;
        float t = Mathf.Clamp01(beyondFringe / Mathf.Max(0.1f, roughApronWidth));
        return Mathf.Lerp(h, groundLevel, t * t * (3f - 2f * t));
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
        Release(roughInstance);
        Release(cupMesh);
        Release(soilInstance);
        Release(linerInstance);
        Release(bottomInstance);
        Release(rimInstance);
        mesh = null;
        texture = null;
        cupMesh = null;
        basePixels = null;
        greenInstance = fringeInstance = roughInstance = null;
        soilInstance = linerInstance = bottomInstance = rimInstance = null;
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
        Release(roughInstance);
        greenInstance = MakeMaterial(greenMaterial, Color.white);
        EnableAlphaClip(greenInstance);
        fringeInstance = MakeMaterial(fringeMaterial, FringeGrass);
        roughInstance = MakeMaterial(roughMaterial, RoughGrass);
        TextureFrame(Model, out _, out float frame);
        Texture2D detail = grassDetail != null ? grassDetail : GrassTextures.Detail;
        ApplyDetail(greenInstance, detail, frame / GreenDetailTile, 0.8f);
        ApplyDetail(fringeInstance, detail, frame / FringeDetailTile, 1f);
        ApplyDetail(roughInstance, detail, frame / RoughDetailTile, 1f);
        meshRenderer.sharedMaterials = new[] { greenInstance, fringeInstance, roughInstance };

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

    // Polar mesh: rings from the centre to the edge (submesh 0, green), a fringe ring (submesh 1),
    // then rough rings that blend down to the ground (submesh 2), so a ball in the rough sits on grass.
    Mesh BuildMesh(GreenModel model, out float lowest)
    {
        GreenOutline o = model.Definition.outline;
        float fringe = model.Definition.fringeWidth;
        int n = segments;
        const int apronRings = 6;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        TextureFrame(model, out Vector2 texMin, out float texSize);

        Vector3 Vertex(Vector2 local, float height)
        {
            uvs.Add((local - texMin) / texSize);
            return new Vector3(local.x, height, local.y);
        }

        // Centre + green rings.
        vertices.Add(Vertex(o.center, visualHeightScale * model.HeightAt(o.center)));
        for (int r = 1; r <= rings; r++)
        {
            float t = (float)r / rings;
            for (int k = 0; k < n; k++)
            {
                Vector2 p = Vector2.Lerp(o.center, o.EdgePoint(2f * Mathf.PI * k / n), t);
                vertices.Add(Vertex(p, visualHeightScale * model.HeightAt(p)));
            }
        }
        int fringeStart = vertices.Count;
        for (int k = 0; k < n; k++)
        {
            Vector2 p = o.EdgePoint(2f * Mathf.PI * k / n, fringe);
            vertices.Add(Vertex(p, visualHeightScale * model.HeightAt(p)));
        }

        lowest = 0f;
        for (int i = 0; i < vertices.Count; i++) lowest = Mathf.Min(lowest, vertices[i].y);
        groundLevel = lowest - (model.Definition.waterAround ? 0.15f : 0.03f);
        lowest = groundLevel;

        // Rough apron rings (heights from LocalVisualHeight, so the ball matches the mesh).
        int apronStart = vertices.Count;
        for (int r = 1; r <= apronRings; r++)
        {
            float extra = fringe + roughApronWidth * r / apronRings;
            for (int k = 0; k < n; k++)
            {
                Vector2 p = o.EdgePoint(2f * Mathf.PI * k / n, extra);
                vertices.Add(Vertex(p, LocalVisualHeight(p)));
            }
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

        var fringeTris = new List<int>();
        int edge = 1 + (rings - 1) * n;
        for (int k = 0; k < n; k++)
            Quad(fringeTris, edge + k, edge + (k + 1) % n, fringeStart + k, fringeStart + (k + 1) % n);

        var rough = new List<int>();
        for (int r = 0; r < apronRings; r++)
        {
            int inner = r == 0 ? fringeStart : apronStart + (r - 1) * n;
            int outer = apronStart + r * n;
            for (int k = 0; k < n; k++)
                Quad(rough, inner + k, inner + (k + 1) % n, outer + k, outer + (k + 1) % n);
        }

        var m = new Mesh { name = "Green " + model.Definition.id, hideFlags = HideFlags.DontSave };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(vertices);
        m.SetUVs(0, uvs);
        m.subMeshCount = 3;
        m.SetTriangles(green, 0);
        m.SetTriangles(fringeTris, 1);
        m.SetTriangles(rough, 2);
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
        int res = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(size / TargetTexel)), 512, Mathf.Max(512, textureSize));
        if (texture == null || texture.width != res)
        {
            Release(texture);
            texture = new Texture2D(res, res, TextureFormat.RGBA32, true)
            {
                name = "Green texture",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
        }

        float holding = Model.MakeRollSettings().MaxHoldingSlope;
        float texel = size / res;
        var pixels = new Color32[res * res];
        bool needHeights = showContours || showSlopeColours;

        // Smooth heights from the feature formulas (not the 10 cm grid) on a grid of up to 1024²,
        // with their slope; sampled smoothly for every texture pixel.
        int n = Mathf.Min(res, 1024);
        float cell = size / n;
        float[] hs = null, gxs = null, gys = null;
        if (needHeights)
        {
            GreenDefinition d = Model.Definition;
            float offset = d.FeatureHeightAt(Model.Heights.Origin) - Model.Heights[0, 0];
            hs = new float[n * n];
            gxs = new float[n * n];
            gys = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    hs[x + y * n] = d.FeatureHeightAt(min + new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell)) - offset;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int xl = Mathf.Max(0, x - 1), xr = Mathf.Min(n - 1, x + 1);
                    int yd = Mathf.Max(0, y - 1), yu = Mathf.Min(n - 1, y + 1);
                    gxs[x + y * n] = (hs[xr + y * n] - hs[xl + y * n]) / ((xr - xl) * cell);
                    gys[x + y * n] = (hs[x + yu * n] - hs[x + yd * n]) / ((yu - yd) * cell);
                }
        }

        float toCoarse = (float)n / res;
        for (int y = 0; y < res; y++)
        {
            float stripeY = min.y + (y + 0.5f) * texel;
            // Mowing stripes with a soft edge.
            float stripe = Mathf.Repeat(stripeY, 2f * StripeWidth) / StripeWidth; // 0..2
            float edgeDist = Mathf.Min(Mathf.Abs(stripe), Mathf.Abs(stripe - 1f), Mathf.Abs(stripe - 2f)) * StripeWidth;
            float blend = Mathf.Clamp01(edgeDist / (3f * texel));
            Color stripeColour = stripe < 1f
                ? Color.Lerp(GrassDark, GrassLight, 0.5f + 0.5f * blend)
                : Color.Lerp(GrassLight, GrassDark, 0.5f + 0.5f * blend);
            float cv = (y + 0.5f) * toCoarse - 0.5f;

            for (int x = 0; x < res; x++)
            {
                Color c = stripeColour;
                if (needHeights)
                {
                    float cu = (x + 0.5f) * toCoarse - 0.5f;
                    float h = Bilinear(hs, n, cu, cv);
                    float gx = Bilinear(gxs, n, cu, cv), gy = Bilinear(gys, n, cu, cv);
                    float slope = Mathf.Sqrt(gx * gx + gy * gy);

                    if (showSlopeColours)
                        c = Color.Lerp(c, SlopeColour(slope / holding), 0.55f);

                    if (showContours && contourInterval > 0f)
                    {
                        float level = h / contourInterval;
                        float nearest = Mathf.Round(level);
                        // Distance to the nearest line on the ground (m), from height distance / slope.
                        float dist = Mathf.Abs(level - nearest) * contourInterval / Mathf.Max(slope, 1e-4f);
                        bool major = Mathf.Abs(Mathf.Repeat(nearest, 5f)) < 0.5f;
                        float half = (major ? 1.8f : 1f) * Mathf.Max(contourWidth, texel) * 0.5f;
                        float coverage = Mathf.Clamp01(1f - (dist - half) / texel); // 1 pixel soft edge
                        c = Color.Lerp(c, new Color(0.08f, 0.2f, 0.05f), coverage * (major ? 0.55f : 0.35f));
                    }
                }
                pixels[x + y * res] = c;
            }
        }
        basePixels = pixels;
        StampHole();

        if (greenInstance != null)
        {
            if (greenInstance.HasProperty("_BaseMap")) greenInstance.SetTexture("_BaseMap", texture);
            if (greenInstance.HasProperty("_MainTex")) greenInstance.SetTexture("_MainTex", texture);
        }
    }

    static float Bilinear(float[] a, int n, float u, float v)
    {
        u = Mathf.Clamp(u, 0f, n - 1);
        v = Mathf.Clamp(v, 0f, n - 1);
        int i = Mathf.Min((int)u, n - 2), j = Mathf.Min((int)v, n - 2);
        float fu = u - i, fv = v - j;
        int k = i + j * n;
        return Mathf.Lerp(Mathf.Lerp(a[k], a[k + 1], fu), Mathf.Lerp(a[k + n], a[k + n + 1], fu), fv);
    }

    // Cuts the active hole out of the green texture (alpha 0; the green material clips it).
    void StampHole()
    {
        if (Model == null || texture == null || basePixels == null) return;
        TextureFrame(Model, out Vector2 min, out float size);
        int res = texture.width;
        float texel = size / res;
        var pixels = (Color32[])basePixels.Clone();
        Vector2 hole = Model.PinPosition(pin);
        float r = Model.MakeRollSettings().HoleRadius;

        int cx = Mathf.FloorToInt((hole.x - min.x) / texel), cy = Mathf.FloorToInt((hole.y - min.y) / texel);
        int reach = Mathf.CeilToInt(r / texel) + 2;
        for (int y = Mathf.Max(0, cy - reach); y <= Mathf.Min(res - 1, cy + reach); y++)
            for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(res - 1, cx + reach); x++)
            {
                Vector2 p = min + new Vector2((x + 0.5f) * texel, (y + 0.5f) * texel);
                if ((p - hole).magnitude < r - 0.25f * texel)
                    pixels[x + y * res].a = 0;
            }
        texture.SetPixels32(pixels);
        texture.Apply(true);
    }

    static void EnableAlphaClip(Material m)
    {
        if (m == null) return;
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
    }

    // The cup under the active hole: a thin cut edge on the surface, soil wall (top 2.5 cm), white liner,
    // and the bottom. Walls face inwards. Follows the green's surface at the rim.
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
            if (cup.GetComponent<MeshRenderer>() == null) cup.AddComponent<MeshRenderer>();
        }
        if (soilInstance == null)
        {
            soilInstance = MakeMaterial(null, new Color(0.27f, 0.2f, 0.13f));
            linerInstance = MakeMaterial(null, new Color(0.93f, 0.93f, 0.9f));
            bottomInstance = MakeMaterial(null, new Color(0.2f, 0.16f, 0.11f));
            rimInstance = MakeMaterial(null, new Color(0.22f, 0.3f, 0.12f));
        }

        RollSettings rules = Model.MakeRollSettings();
        Vector2 hole = Model.PinPosition(pin);
        float r = rules.HoleRadius;
        const int n = 48;
        const float soilBand = 0.025f, rimWidth = 0.004f, lift = 0.0008f;
        float centreY = visualHeightScale * Model.HeightAt(hole);
        float bottomY = centreY - rules.cupDepth;

        var vertices = new List<Vector3>();
        float Surface(Vector2 local) => visualHeightScale * Model.HeightAt(local);
        Vector2 Around(int k, float radius)
        {
            float a = 2f * Mathf.PI * k / n;
            return hole + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        int Ring(System.Func<int, Vector3> point)
        {
            int start = vertices.Count;
            for (int k = 0; k < n; k++) vertices.Add(point(k));
            return start;
        }

        // Rim: thin dark ring on the surface hiding the pixel edge of the cut.
        int rimIn = Ring(k => { Vector2 p = Around(k, r - 0.002f); return new Vector3(p.x, Surface(p) + lift, p.y); });
        int rimOut = Ring(k => { Vector2 p = Around(k, r + rimWidth); return new Vector3(p.x, Surface(p) + lift, p.y); });
        // Wall rings.
        int top = Ring(k => { Vector2 p = Around(k, r); return new Vector3(p.x, Surface(p), p.y); });
        int band = Ring(k => { Vector2 p = Around(k, r); return new Vector3(p.x, Surface(p) - soilBand, p.y); });
        int bandL = Ring(k => vertices[band + k]); // own vertices for the liner (sharp colour edge)
        int low = Ring(k => { Vector2 p = Around(k, r); return new Vector3(p.x, bottomY, p.y); });
        int bottomRing = Ring(k => vertices[low + k]);
        int centre = vertices.Count;
        vertices.Add(new Vector3(hole.x, bottomY, hole.y));

        var rim = new List<int>();
        var soil = new List<int>();
        var liner = new List<int>();
        var bottom = new List<int>();
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            Quad(rim, rimIn + k, rimIn + k2, rimOut + k, rimOut + k2);   // facing up
            QuadInward(soil, top + k, top + k2, band + k, band + k2);
            QuadInward(liner, bandL + k, bandL + k2, low + k, low + k2);
            bottom.Add(centre); bottom.Add(bottomRing + k2); bottom.Add(bottomRing + k); // facing up
        }

        if (cupMesh == null) cupMesh = new Mesh { name = "Cup", hideFlags = HideFlags.DontSave };
        cupMesh.Clear();
        cupMesh.SetVertices(vertices);
        cupMesh.subMeshCount = 4;
        cupMesh.SetTriangles(rim, 0);
        cupMesh.SetTriangles(soil, 1);
        cupMesh.SetTriangles(liner, 2);
        cupMesh.SetTriangles(bottom, 3);
        cupMesh.RecalculateNormals();
        cupMesh.RecalculateBounds();
        cup.GetComponent<MeshFilter>().sharedMesh = cupMesh;
        cup.GetComponent<MeshRenderer>().sharedMaterials = new[] { rimInstance, soilInstance, linerInstance, bottomInstance };
    }

    // Like Quad, but facing the other way (for the inside of the cup wall).
    static void QuadInward(List<int> t, int a, int b, int c, int d)
    {
        t.Add(a); t.Add(c); t.Add(b);
        t.Add(b); t.Add(c); t.Add(d);
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

    // URP Lit "Detail Albedo x2": grey detail multiplies the base colour by 2 × detail (0.5 = no change).
    static void ApplyDetail(Material m, Texture2D detail, float tiling, float strength)
    {
        if (m == null || detail == null || !m.HasProperty("_DetailAlbedoMap")) return;
        m.SetTexture("_DetailAlbedoMap", detail);
        m.SetTextureScale("_DetailAlbedoMap", Vector2.one * tiling);
        if (m.HasProperty("_DetailAlbedoMapScale")) m.SetFloat("_DetailAlbedoMapScale", strength);
        m.EnableKeyword("_DETAIL_MULX2");
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
