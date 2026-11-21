using System.Collections.Generic;
using ARGolf.Putting;
using UnityEngine;

/// <summary>
/// Trees and bushes around the current green. Re-placed whenever the GreenView rebuilds
/// (same layout every time for the same green). Generated objects are not saved with the scene.
/// Island greens get their trees on the far shore, beyond the water.
/// </summary>
[ExecuteAlways]
public class GreenScenery : MonoBehaviour
{
    public GreenView green;
    [Tooltip("Tree models (e.g. Kenney Nature Pack).")]
    public GameObject[] trees;
    [Tooltip("Bush models.")]
    public GameObject[] bushes;

    [Header("Amount and size")]
    public int nearTrees = 18;
    public int farTrees = 40;
    public int bushCount = 26;
    public Vector2 treeScale = new Vector2(1.3f, 1.9f);
    public Vector2 bushScale = new Vector2(1.5f, 2.5f);

    [Header("Distances beyond the rough slope (m)")]
    public Vector2 nearTreeGap = new Vector2(3f, 9f);
    public Vector2 farTreeGap = new Vector2(12f, 35f);
    public Vector2 bushGap = new Vector2(0.5f, 5f);
    [Tooltip("Island greens: radius (m from the green centre) of the far shore with trees.")]
    public Vector2 shoreRadius = new Vector2(68f, 90f);

    readonly List<GameObject> spawned = new List<GameObject>();

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

        void Scatter(int count, GameObject[] models, Vector2 scale, System.Func<float, Vector3> position)
        {
            if (models == null || models.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                GameObject model = models[rng.Next(models.Length)];
                float phi = (i + (float)rng.NextDouble()) / count * 2f * Mathf.PI;
                Vector3 pos = position(phi); // consume random numbers in the same order every time
                float yaw = (float)rng.NextDouble() * 360f;
                float size = Range(scale);
                if (model == null) continue;
                GameObject go = Instantiate(model, pos, Quaternion.Euler(0f, yaw, 0f), transform);
                go.transform.localScale = Vector3.one * size;
                go.hideFlags = HideFlags.DontSave;
                spawned.Add(go);
            }
        }

        if (d.waterAround)
        {
            Scatter(farTrees + nearTrees, trees, treeScale, OnShore);
            Scatter(bushCount, bushes, bushScale, OnShore);
        }
        else
        {
            Scatter(nearTrees, trees, treeScale, phi => AroundGreen(phi, Range(nearTreeGap)));
            Scatter(farTrees, trees, treeScale, phi => AroundGreen(phi, Range(farTreeGap)));
            Scatter(bushCount, bushes, bushScale, phi => AroundGreen(phi, Range(bushGap)));
        }
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
