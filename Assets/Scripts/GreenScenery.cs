using System.Collections.Generic;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Trees and bushes around the current green. Re-placed whenever the GreenView rebuilds
/// (same layout every time for the same green). Generated objects are not saved with the scene.
/// Island greens get their trees (palms if given) on the far shore, beyond the water.
/// Models are scaled to a target height, so packs with different units mix. Missing models (e.g. an Asset Store
/// pack that is not in git) fall back to the fallback lists. Built-in-pipeline tree materials (Tree Creator)
/// are swapped for URP Lit copies at runtime, so they don't render pink.
/// </summary>
[ExecuteAlways]
public class GreenScenery : MonoBehaviour
{
    public GreenView green;
    [Tooltip("Tree models (e.g. Asset Store trees, not in git).")]
    public GameObject[] trees;
    [Tooltip("Bush models.")]
    public GameObject[] bushes;
    [Tooltip("Trees for island greens (empty = trees).")]
    public GameObject[] palms;

    [Header("Fallback (used when the lists above have no models, e.g. on a fresh clone)")]
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

    readonly List<GameObject> spawned = new List<GameObject>();
    static readonly Dictionary<Material, Material> urpCopies = new Dictionary<Material, Material>();

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
    }

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

        void Scatter(int count, GameObject[] models, Vector2 height, System.Func<float, Vector3> position)
        {
            if (models.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                GameObject model = models[rng.Next(models.Length)];
                float phi = (i + (float)rng.NextDouble()) / count * 2f * Mathf.PI;
                Vector3 pos = position(phi); // consume random numbers in the same order every time
                float yaw = (float)rng.NextDouble() * 360f;
                float h = Range(height);
                GameObject go = Instantiate(model, pos, Quaternion.Euler(0f, yaw, 0f), transform);
                go.hideFlags = HideFlags.DontSave;
                float modelHeight = HeightOf(go);
                go.transform.localScale = model.transform.localScale * (modelHeight > 0.01f ? h / modelHeight : 1f);
                UseUrpMaterials(go);
                spawned.Add(go);
            }
        }

        GameObject[] treeModels = Usable(trees, fallbackTrees);
        GameObject[] bushModels = Usable(bushes, fallbackBushes);
        if (d.waterAround)
        {
            GameObject[] palmModels = Usable(palms, null);
            if (palmModels.Length > 0) Scatter(farTrees + nearTrees, palmModels, palmHeight, OnShore);
            else Scatter(farTrees + nearTrees, treeModels, treeHeight, OnShore);
            Scatter(bushCount, bushModels, bushHeight, OnShore);
        }
        else
        {
            Scatter(nearTrees, treeModels, treeHeight, phi => AroundGreen(phi, Range(nearTreeGap)));
            Scatter(farTrees, treeModels, treeHeight, phi => AroundGreen(phi, Range(farTreeGap)));
            Scatter(bushCount, bushModels, bushHeight, phi => AroundGreen(phi, Range(bushGap)));
        }
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
