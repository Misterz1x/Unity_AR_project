using System.Collections.Generic;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Trees and bushes around the current green. Re-placed whenever the GreenView rebuilds
/// (same layout every time for the same green). Spawned objects are not saved with the scene.
/// Island greens get their trees (palms) on the far shore, beyond the water.
/// By default everything is generated in code (TreeGenerator), so every clone of the repo looks the same.
/// With "Generate Trees" off, model lists are used instead (e.g. Asset Store trees, not in git), falling back
/// to the fallback lists when missing; built-in-pipeline tree materials (Tree Creator) get URP Lit copies.
/// Everything is scaled to a target height, so different sources mix.
/// </summary>
[ExecuteAlways]
public class GreenScenery : MonoBehaviour
{
    public GreenView green;

    [Tooltip("Generate trees, palms and bushes in code. Off = use the model lists below.")]
    public bool generateTrees = true;
    [Tooltip("Different generated models per kind.")]
    [Range(1, 12)] public int variants = 5;
    [Tooltip("Share of conifers among the generated trees.")]
    [Range(0f, 1f)] public float coniferShare = 0.35f;

    [Header("Models (used when Generate Trees is off)")]
    [Tooltip("Tree models (e.g. Asset Store trees, not in git).")]
    public GameObject[] trees;
    public GameObject[] bushes;
    [Tooltip("Trees for island greens (empty = trees).")]
    public GameObject[] palms;
    [Tooltip("Used when the lists above have no models (e.g. on a fresh clone).")]
    public GameObject[] fallbackTrees;
    public GameObject[] fallbackBushes;

    [Header("Amount and size")]
    public int nearTrees = 18;
    public int farTrees = 40;
    public int bushCount = 26;
    [Tooltip("Height of a placed tree (m), random in this range.")]
    public Vector2 treeHeight = new Vector2(9f, 16f);
    public Vector2 palmHeight = new Vector2(8f, 13f);
    public Vector2 bushHeight = new Vector2(0.8f, 1.8f);

    [Header("Distances beyond the rough slope (m)")]
    public Vector2 nearTreeGap = new Vector2(3f, 9f);
    public Vector2 farTreeGap = new Vector2(12f, 35f);
    public Vector2 bushGap = new Vector2(0.5f, 5f);
    [Tooltip("Island greens: radius (m from the green centre) of the far shore with trees.")]
    public Vector2 shoreRadius = new Vector2(68f, 90f);

    delegate GameObject Maker(System.Random rng);

    readonly List<GameObject> spawned = new List<GameObject>();
    static readonly Dictionary<Material, Material> urpCopies = new Dictionary<Material, Material>();
    TreeKit kit;

    void OnEnable()
    {
        if (green != null)
        {
            green.Rebuilt += Place;
            if (green.Model != null) Place();
        }
    }

    void OnDisable()
    {
        if (green != null) green.Rebuilt -= Place;
        Clear();
        kit?.Dispose();
        kit = null;
    }

#if UNITY_EDITOR
    // Re-place after Inspector changes (not allowed directly inside OnValidate).
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled && green != null && green.Model != null) Place();
        };
    }
#endif

    public void Place()
    {
        Clear();
        if (green == null || green.Model == null) return;

        GreenDefinition d = green.Definition;
        var rng = new System.Random(StableHash(d.id));
        float Range(Vector2 r) => r.x + (float)rng.NextDouble() * (r.y - r.x);
        float groundY = green.transform.position.y + green.LowestVisualHeight;
        float beyondRough = d.fringeWidth + green.roughApronWidth;

        Vector3 AroundGreen(float phi, float gap)
        {
            Vector2 local = d.outline.EdgePoint(phi, beyondRough + gap);
            Vector2 w = green.LocalToWorld(local);
            return new Vector3(w.x, groundY, w.y);
        }

        Vector3 OnShore(float phi)
        {
            Vector2 local = d.outline.center + new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * Range(shoreRadius);
            Vector2 w = green.LocalToWorld(local);
            return new Vector3(w.x, groundY, w.y);
        }

        void Scatter(int count, List<Maker> makers, Vector2 height, System.Func<float, Vector3> position)
        {
            if (makers.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                Maker make = makers[rng.Next(makers.Count)];
                float phi = (i + (float)rng.NextDouble()) / count * 2f * Mathf.PI;
                Vector3 pos = position(phi); // consume random numbers in the same order every time
                float yaw = (float)rng.NextDouble() * 360f;
                float h = Range(height);
                GameObject go = make(rng);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                float modelHeight = HeightOf(go);
                go.transform.localScale *= modelHeight > 0.01f ? h / modelHeight : 1f;
                spawned.Add(go);
            }
        }

        List<Maker> treeMakers, bushMakers, palmMakers;
        if (generateTrees)
        {
            if (kit == null || kit.Variants != variants)
            {
                kit?.Dispose();
                kit = new TreeKit(variants);
            }
            int conifers = Mathf.RoundToInt(coniferShare * 10f);
            treeMakers = Generated(kit.Broadleaf, 10 - conifers);
            treeMakers.AddRange(Generated(kit.Conifers, conifers));
            bushMakers = Generated(kit.Bushes, 1);
            palmMakers = Generated(kit.Palms, 1);
        }
        else
        {
            treeMakers = Models(Usable(trees, fallbackTrees));
            bushMakers = Models(Usable(bushes, fallbackBushes));
            palmMakers = Models(Usable(palms, null));
        }

        if (d.waterAround)
        {
            if (palmMakers.Count > 0) Scatter(farTrees + nearTrees, palmMakers, palmHeight, OnShore);
            else Scatter(farTrees + nearTrees, treeMakers, treeHeight, OnShore);
            Scatter(bushCount, bushMakers, bushHeight, OnShore);
        }
        else
        {
            Scatter(nearTrees, treeMakers, treeHeight, phi => AroundGreen(phi, Range(nearTreeGap)));
            Scatter(farTrees, treeMakers, treeHeight, phi => AroundGreen(phi, Range(farTreeGap)));
            Scatter(bushCount, bushMakers, bushHeight, phi => AroundGreen(phi, Range(bushGap)));
        }
    }

    // Each generated model 'weight' times (weight 0 = none).
    List<Maker> Generated(List<TreeKit.Proto> protos, int weight)
    {
        var list = new List<Maker>();
        for (int w = 0; w < weight; w++)
            foreach (TreeKit.Proto p in protos)
                list.Add(rng => kit.Spawn(p, transform, rng));
        return list;
    }

    List<Maker> Models(GameObject[] models)
    {
        var list = new List<Maker>();
        foreach (GameObject model in models)
            list.Add(rng =>
            {
                GameObject go = Instantiate(model, transform);
                UseUrpMaterials(go);
                return go;
            });
        return list;
    }

    // The non-missing models of the first list, or of the fallback list if the first has none.
    static GameObject[] Usable(GameObject[] preferred, GameObject[] fallback)
    {
        var list = new List<GameObject>();
        if (preferred != null) foreach (GameObject m in preferred) if (m != null) list.Add(m);
        if (list.Count == 0 && fallback != null) foreach (GameObject m in fallback) if (m != null) list.Add(m);
        return list.ToArray();
    }

    // Height of the object's renderers above its pivot (m).
    static float HeightOf(GameObject go)
    {
        float top = float.MinValue;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
        return top == float.MinValue ? 0f : top - go.transform.position.y;
    }

    // Tree Creator / legacy Nature shaders only work in the built-in pipeline: use URP Lit copies instead
    // (same texture; leaves alpha-clipped and two-sided).
    static void UseUrpMaterials(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !IsBuiltInOnly(m.shader)) continue;
                if (!urpCopies.TryGetValue(m, out Material copy) || copy == null)
                    urpCopies[m] = copy = UrpCopy(m);
                if (copy == null) continue;
                mats[i] = copy;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    static bool IsBuiltInOnly(Shader shader)
    {
        if (shader == null) return true;
        string n = shader.name;
        return n.StartsWith("Nature/") || n.StartsWith("Hidden/Nature/") || n.Contains("Tree Creator")
            || n == "Hidden/InternalErrorShader" || !shader.isSupported;
    }

    static Material UrpCopy(Material source)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return null;
        string shaderName = source.shader != null ? source.shader.name : "";
        bool leaves = shaderName.Contains("Lea") || source.name.Contains("Lea");
        var m = new Material(lit) { name = source.name + " (URP)", hideFlags = HideFlags.DontSave };
        Texture tex = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        Color colour = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Smoothness", leaves ? 0.15f : 0.05f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (leaves)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.3f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); // two-sided leaf cards
            m.doubleSidedGI = true;
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        return m;
    }

    // Same number for the same text on every run (string.GetHashCode is not guaranteed to be).
    static int StableHash(string text)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in text) h = h * 31 + c;
            return h;
        }
    }

    void Clear()
    {
        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        spawned.Clear();
    }
}
