using System.Collections.Generic;
using UnityEngine;

public enum TreeKind { Broadleaf, Conifer, Palm, Bush }

/// <summary>
/// Procedural trees for the scenery, made entirely in code (no third-party assets): branching tube trunks with
/// alpha-clipped foliage cards (textures from TreeTextures). Submesh 0 = bark, 1 = foliage. Natural size in
/// metres with the base at the origin; GreenScenery rescales them. Foliage normals point out of the crown, so
/// the cards light like one soft volume instead of flat planes.
/// </summary>
public static class TreeGenerator
{
    public static Mesh Build(TreeKind kind, int seed)
    {
        var b = new Builder(seed);
        switch (kind)
        {
            case TreeKind.Broadleaf: b.Broadleaf(); break;
            case TreeKind.Conifer: b.Conifer(); break;
            case TreeKind.Palm: b.Palm(); break;
            default: b.Bush(); break;
        }
        return b.ToMesh($"{kind} {seed}");
    }

    sealed class Builder
    {
        readonly System.Random rng;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> bark = new List<int>();
        readonly List<int> leaves = new List<int>();
        readonly List<int> foliageVerts = new List<int>();

        public Builder(int seed) { rng = new System.Random(seed); }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        Vector3 RandomDir()
        {
            Vector3 v;
            do v = new Vector3(Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f));
            while (v.sqrMagnitude > 1f || v.sqrMagnitude < 1e-4f);
            return v.normalized;
        }

        static Vector3 Perpendicular(Vector3 d) =>
            Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;

        // ---- tree kinds ----

        public void Broadleaf()
        {
            float height = Rand(9f, 12f);
            float trunk = height * Rand(0.3f, 0.42f);
            float r0 = height * Rand(0.026f, 0.032f);
            var spots = new List<Vector3>();
            Vector3 dir = (Vector3.up + new Vector3(Rand(-0.08f, 0.08f), 0f, Rand(-0.08f, 0.08f))).normalized;
            Branch(Vector3.zero, dir, trunk, r0, 0, height - trunk, spots);

            float cardSize = height * 0.16f;
            foreach (Vector3 s in spots) RandomCards(s, 4, cardSize * Rand(0.8f, 1.2f));
            Bounds crown = BoundsOf(spots);
            CrownNormals(crown.center, crown.extents + Vector3.one * cardSize * 0.5f, 0.35f);
        }

        public void Conifer()
        {
            float height = Rand(10f, 15f);
            float unit = height / 12f;
            float r0 = height * Rand(0.016f, 0.02f);
            float crownBase = height * Rand(0.06f, 0.18f);
            float maxReach = height * Rand(0.2f, 0.26f);

            var pts = new List<Vector3>();
            var radii = new List<float>();
            Vector3 wobble = Vector3.zero;
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                wobble += new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)) * 0.03f;
                pts.Add(new Vector3(wobble.x, t * height, wobble.z));
                radii.Add(Mathf.Lerp(r0 * 1.3f, r0 * 0.08f, Mathf.Pow(t, 0.8f)));
            }
            Tube(pts, radii, 7, 1.2f, 2f);

            float y = crownBase;
            while (y < height * 0.97f)
            {
                float f = 1f - (y - crownBase) / (height - crownBase); // 1 at the lowest whorl, 0 at the top
                float reach = maxReach * Mathf.Pow(f, 0.9f) + 0.3f * unit;
                int n = Mathf.RoundToInt(Rand(4f, 6.4f));
                float az0 = Rand(0f, 360f);
                Vector3 axis = PointAt(pts, y / height);
                for (int i = 0; i < n; i++)
                {
                    float az = (az0 + i * 360f / n + Rand(-15f, 15f)) * Mathf.Deg2Rad;
                    float droop = Mathf.Lerp(-5f, 28f, f) * Mathf.Deg2Rad + Rand(-0.08f, 0.08f); // lower branches hang more
                    var outward = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
                    Vector3 dir = (outward * Mathf.Cos(droop) - Vector3.up * Mathf.Sin(droop)).normalized;
                    float len = reach * Rand(0.85f, 1.1f);
                    if (f > 0.25f)
                        Tube(new List<Vector3> { axis, axis + dir * len * 0.8f },
                             new List<float> { r0 * 0.22f * f + 0.01f, 0.008f }, 4, 1f, 1f);
                    Vector3 flat = Vector3.Cross(Vector3.up, dir).normalized;
                    float width = Mathf.Lerp(0.8f, 1.5f, f) * unit;
                    Vector3 across = Quaternion.AngleAxis(Rand(-20f, 20f), dir) * flat;
                    Card(axis, dir, across, len * 1.1f, width);
                    Card(axis, dir, Quaternion.AngleAxis(Rand(50f, 70f), dir) * across, len * 1.05f, width * 0.85f);
                    Card(axis, dir, Quaternion.AngleAxis(Rand(-70f, -50f), dir) * across, len * 1.0f, width * 0.85f);
                }
                y += Rand(0.3f, 0.45f) * unit;
            }
            Vector3 top = pts[pts.Count - 1];
            for (int i = 0; i < 3; i++)
                Card(top - Vector3.up * 0.9f * unit, Vector3.up, Quaternion.AngleAxis(i * 60f, Vector3.up) * Vector3.right, 1.1f * unit, 0.45f * unit);

            foreach (int i in foliageVerts)
            {
                Vector3 d = verts[i];
                d.y = 0f;
                normals[i] = (d.normalized + Vector3.up * 0.45f).normalized;
            }
        }

        public void Palm()
        {
            float height = Rand(7f, 11f);
            float unit = height / 9f;
            float r0 = Rand(0.15f, 0.2f);
            Vector3 lean = new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)).normalized * Rand(0.05f, 0.3f);

            var pts = new List<Vector3> { Vector3.zero };
            var radii = new List<float> { r0 * 1.35f };
            Vector3 p = Vector3.zero, d = (Vector3.up + lean).normalized;
            const int segments = 12;
            float step = height * 0.93f / segments;
            for (int i = 1; i <= segments; i++)
            {
                d = (d - lean * 0.04f + Vector3.up * 0.015f).normalized; // curves back up towards the crown
                p += d * step;
                pts.Add(p);
                radii.Add(r0 * Mathf.Lerp(1.05f, 0.8f, (float)i / segments));
            }
            Tube(pts, radii, 9, 0.96f, 2f); // palm bark: 8 rings per tile -> a ring every 12 cm

            int fronds = Mathf.RoundToInt(Rand(11f, 16f));
            float az0 = Rand(0f, 360f);
            for (int f = 0; f < fronds; f++)
            {
                float az = (az0 + f * 137.5f) * Mathf.Deg2Rad; // golden angle: even spread
                var outward = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
                float e0 = Rand(10f, 60f);
                Frond(p, outward, e0, e0 - Rand(55f, 85f), Rand(2.6f, 3.6f) * unit, Rand(0.9f, 1.2f) * unit);
            }
            foreach (int i in foliageVerts)
                normals[i] = (Vector3.up * 0.9f + (verts[i] - p).normalized * 0.5f).normalized;
        }

        public void Bush()
        {
            float r = Rand(0.7f, 1.1f), h = r * Rand(0.75f, 1.05f);
            int n = Mathf.RoundToInt(Rand(26f, 38f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = RandomDir();
                d.y = Mathf.Abs(d.y);
                float k = Rand(0.3f, 0.85f);
                var c = new Vector3(d.x * r * k, 0.15f * h + d.y * h * 0.75f * k, d.z * r * k);
                Vector3 a = RandomDir();
                CentredCard(c, a, Vector3.Cross(a, RandomDir()).normalized, r * Rand(0.7f, 1f));
            }
            CrownNormals(new Vector3(0f, h * 0.3f, 0f), new Vector3(r, h, r), 0.3f);
        }

        // ---- parts ----

        // Recursive branch: depth 0 = trunk, 1 = limbs, 2 = branches, 3 = twigs (leaf spots at the twigs).
        void Branch(Vector3 start, Vector3 dir, float length, float radius, int depth, float crown, List<Vector3> spots)
        {
            const int maxDepth = 3;
            int segments = depth == 0 ? 5 : depth == 1 ? 4 : 3;
            int sides = depth == 0 ? 9 : depth == 1 ? 7 : depth == 2 ? 5 : 4;
            float tipRatio = depth == maxDepth ? 0.3f : 0.65f;
            var pts = new List<Vector3> { start };
            var radii = new List<float> { radius * (depth == 0 ? 1.35f : 1f) }; // root flare
            Vector3 p = start, d = dir;
            for (int i = 1; i <= segments; i++)
            {
                d = (d + RandomDir() * 0.18f + Vector3.up * (depth == 0 ? 0.02f : 0.07f)).normalized;
                p += d * (length / segments);
                pts.Add(p);
                radii.Add(radius * Mathf.Lerp(1f, tipRatio, (float)i / segments));
            }
            Tube(pts, radii, sides, 1.2f, depth == 0 ? 2f : 1f);

            if (depth == maxDepth)
            {
                spots.Add(PointAt(pts, 0.55f));
                spots.Add(p);
                return;
            }
            if (depth == maxDepth - 1) spots.Add(p);

            int children = depth == 0 ? Mathf.RoundToInt(Rand(3f, 5f)) : Mathf.RoundToInt(Rand(3f, 4.4f));
            float az0 = Rand(0f, 360f);
            for (int c = 0; c < children; c++)
            {
                bool leader = depth == 0 && c == 0; // the trunk continues upwards
                float t = depth == 0 ? (leader ? 1f : Rand(0.82f, 1f)) : Rand(0.3f, 0.95f);
                Vector3 at = PointAt(pts, t);
                Vector3 pd = DirAt(pts, t);
                Vector3 side = Quaternion.AngleAxis(az0 + c * 360f / children + Rand(-25f, 25f), pd) * Perpendicular(pd);
                float angle = (leader ? Rand(3f, 12f) : depth == 0 ? Rand(20f, 45f) : Rand(30f, 55f)) * Mathf.Deg2Rad;
                Vector3 cd = (pd * Mathf.Cos(angle) + side * Mathf.Sin(angle)).normalized;
                float len = depth == 0 ? crown * (leader ? Rand(0.65f, 0.8f) : Rand(0.5f, 0.7f)) : length * Rand(0.55f, 0.75f);
                float r = radius * Mathf.Lerp(1f, tipRatio, t) * Rand(0.55f, 0.7f);
                Branch(at, cd, len, r, depth + 1, crown, spots);
            }
        }

        // Palm frond: an arching strip (3 vertices across, edges drooping), u along, v across.
        void Frond(Vector3 start, Vector3 outward, float e0, float e1, float length, float width)
        {
            const int rows = 9;
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
            Vector3 droop = -Vector3.up * width * 0.18f;
            Vector3 p = start;
            int first = verts.Count;
            for (int j = 0; j <= rows; j++)
            {
                float t = (float)j / rows;
                float e = Mathf.Lerp(e0, e1, t * t) * Mathf.Deg2Rad; // arches over
                Vector3 dir = outward * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
                if (j > 0) p += dir * (length / rows);
                float half = width * 0.5f * Mathf.Lerp(1f, 0.6f, t);
                AddFoliageVertex(p - side * half + droop, new Vector2(t, 0f));
                AddFoliageVertex(p, new Vector2(t, 0.5f));
                AddFoliageVertex(p + side * half + droop, new Vector2(t, 1f));
            }
            for (int j = 0; j < rows; j++)
            {
                int a = first + j * 3;
                leaves.Add(a); leaves.Add(a + 3); leaves.Add(a + 1);
                leaves.Add(a + 1); leaves.Add(a + 3); leaves.Add(a + 4);
                leaves.Add(a + 1); leaves.Add(a + 4); leaves.Add(a + 2);
                leaves.Add(a + 2); leaves.Add(a + 4); leaves.Add(a + 5);
            }
        }

        void AddFoliageVertex(Vector3 position, Vector2 uv)
        {
            foliageVerts.Add(verts.Count);
            verts.Add(position);
            normals.Add(Vector3.up);
            uvs.Add(uv);
        }

        // Tube along a polyline (bark). u around (uRepeat tiles), v along (one tile per metresPerTile).
        void Tube(List<Vector3> pts, List<float> radii, int sides, float metresPerTile, float uRepeat)
        {
            int start = verts.Count;
            Vector3 n = Vector3.zero;
            float v = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                n = i == 0 ? Perpendicular(t) : Vector3.ProjectOnPlane(n, t).normalized; // carried along the tube
                Vector3 bn = Vector3.Cross(t, n);
                if (i > 0) v += Vector3.Distance(pts[i], pts[i - 1]) / metresPerTile;
                for (int k = 0; k <= sides; k++)
                {
                    float a = 2f * Mathf.PI * k / sides;
                    Vector3 dir = Mathf.Cos(a) * n + Mathf.Sin(a) * bn;
                    verts.Add(pts[i] + dir * radii[i]);
                    normals.Add(dir);
                    uvs.Add(new Vector2(uRepeat * k / sides, v));
                }
            }
            int row = sides + 1;
            for (int i = 0; i + 1 < pts.Count; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * row + k, b = a + 1, c = a + row, d = c + 1;
                    bark.Add(a); bark.Add(b); bark.Add(c);
                    bark.Add(b); bark.Add(d); bark.Add(c);
                }
        }

        // Foliage quad from origin along 'along' (u 0..1), centred across (v 0..1). Two-sided material.
        void Card(Vector3 origin, Vector3 along, Vector3 across, float length, float width)
        {
            int i = verts.Count;
            Vector3 hw = across * (width * 0.5f);
            AddFoliageVertex(origin - hw, new Vector2(0f, 0f));
            AddFoliageVertex(origin + along * length - hw, new Vector2(1f, 0f));
            AddFoliageVertex(origin + hw, new Vector2(0f, 1f));
            AddFoliageVertex(origin + along * length + hw, new Vector2(1f, 1f));
            leaves.Add(i); leaves.Add(i + 1); leaves.Add(i + 2);
            leaves.Add(i + 1); leaves.Add(i + 3); leaves.Add(i + 2);
        }

        void CentredCard(Vector3 centre, Vector3 along, Vector3 across, float size) =>
            Card(centre - along * size * 0.5f, along, across, size, size);

        void RandomCards(Vector3 centre, int count, float size)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 a = RandomDir();
                CentredCard(centre + RandomDir() * size * 0.3f, a, Vector3.Cross(a, RandomDir()).normalized, size * Rand(0.85f, 1.15f));
            }
        }

        // Foliage normals pointing out of an ellipsoidal crown (plus a bit up: lit from the sky).
        void CrownNormals(Vector3 centre, Vector3 radii, float upBias)
        {
            foreach (int i in foliageVerts)
            {
                Vector3 d = verts[i] - centre;
                d = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
                normals[i] = (d.normalized + Vector3.up * upBias).normalized;
            }
        }

        static Bounds BoundsOf(List<Vector3> points)
        {
            var b = new Bounds(points.Count > 0 ? points[0] : Vector3.zero, Vector3.zero);
            foreach (Vector3 p in points) b.Encapsulate(p);
            return b;
        }

        static Vector3 PointAt(List<Vector3> pts, float t)
        {
            float f = Mathf.Clamp01(t) * (pts.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), pts.Count - 2);
            return Vector3.Lerp(pts[i], pts[i + 1], f - i);
        }

        static Vector3 DirAt(List<Vector3> pts, float t)
        {
            int i = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(t) * (pts.Count - 1)), pts.Count - 2);
            return (pts[i + 1] - pts[i]).normalized;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bark, 0);
            mesh.SetTriangles(leaves, 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

/// <summary>
/// A set of generated tree models (meshes, textures, materials) for GreenScenery. Not saved; call Dispose.
/// </summary>
public sealed class TreeKit : System.IDisposable
{
    public sealed class Proto
    {
        public Mesh mesh;
        public Material[] materials;
    }

    public readonly List<Proto> Broadleaf = new List<Proto>();
    public readonly List<Proto> Conifers = new List<Proto>();
    public readonly List<Proto> Palms = new List<Proto>();
    public readonly List<Proto> Bushes = new List<Proto>();
    public readonly int Variants;
    readonly List<Object> owned = new List<Object>();

    public TreeKit(int variants)
    {
        Variants = variants;
        Material bark = Lit(TreeTextures.Bark(3), false, 0.05f);
        Material palmBark = Lit(TreeTextures.PalmBark(5), false, 0.08f);
        Material leaf = Lit(TreeTextures.LeafCluster(7, 300, 0.085f), true, 0.12f);
        Material needles = Lit(TreeTextures.ConiferSpray(11), true, 0.08f);
        Material frond = Lit(TreeTextures.PalmFrond(13), true, 0.15f);
        Material bush = Lit(TreeTextures.LeafCluster(17, 340, 0.06f), true, 0.1f);
        for (int i = 0; i < variants; i++)
        {
            Add(Broadleaf, TreeKind.Broadleaf, 100 + i, bark, leaf);
            Add(Conifers, TreeKind.Conifer, 200 + i, bark, needles);
            Add(Palms, TreeKind.Palm, 300 + i, palmBark, frond);
            Add(Bushes, TreeKind.Bush, 400 + i, bark, bush);
        }
    }

    void Add(List<Proto> list, TreeKind kind, int seed, Material barkMat, Material foliageMat)
    {
        Mesh mesh = TreeGenerator.Build(kind, seed);
        owned.Add(mesh);
        list.Add(new Proto { mesh = mesh, materials = new[] { barkMat, foliageMat } });
    }

    /// <summary>New object with the model; foliage tint varies a little per tree.</summary>
    public GameObject Spawn(Proto p, Transform parent, System.Random rng)
    {
        var go = new GameObject(p.mesh.name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = p.materials;
        float b = 0.82f + 0.28f * (float)rng.NextDouble();
        float warm = 0.3f * (float)rng.NextDouble();
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(b * (1f + 0.25f * warm), b, b * (1f - 0.3f * warm), 1f));
        r.SetPropertyBlock(block, 1);
        return go;
    }

    Material Lit(Texture2D tex, bool foliage, float smoothness)
    {
        owned.Add(tex);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader) { name = tex.name, hideFlags = HideFlags.DontSave };
        owned.Add(m);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (foliage)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", TreeTextures.Cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); // leaf cards are seen from both sides
            m.doubleSidedGI = true;
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        return m;
    }

    public void Dispose()
    {
        foreach (Object o in owned)
        {
            if (o == null) continue;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
        owned.Clear();
    }
}
