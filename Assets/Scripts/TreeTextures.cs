using UnityEngine;

/// <summary>
/// Textures for TreeGenerator, made in code: tiling bark (normal and palm) and cut-out foliage cards
/// (broadleaf leaf cluster, conifer spray, palm frond). Foliage mip levels keep the alpha coverage of the
/// full-size texture, so alpha-clipped leaves don't thin out with distance.
/// </summary>
public static class TreeTextures
{
    /// <summary>Alpha clip threshold for the foliage materials.</summary>
    public const float Cutoff = 0.5f;

    public static Texture2D Bark(int seed)
    {
        const int size = 256;
        var px = new Color[size * size];
        var dark = new Color(0.15f, 0.12f, 0.09f);
        var light = new Color(0.46f, 0.4f, 0.33f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float fibres = GrassTextures.Noise(u, v, 40, 5, seed) - 0.5f;      // stretched along the trunk
                float plates = GrassTextures.Noise(u, v, 10, 3, seed + 1) - 0.5f;
                float fine = GrassTextures.Noise(u, v, 128, 48, seed + 2) - 0.5f;
                float crack = Mathf.Abs(GrassTextures.Noise(u, v, 16, 3, seed + 3) - 0.5f) * 2f;
                float g = 0.55f + 0.45f * fibres + 0.35f * plates + 0.2f * fine;
                if (crack < 0.15f) g *= Mathf.Lerp(0.35f, 1f, crack / 0.15f);
                px[x + y * size] = Color.Lerp(dark, light, Mathf.Clamp01(g));
            }
        return Tiling(px, size, "Bark");
    }

    /// <summary>Palm trunk: grey-brown with a leaf-scar ring every 1/8 of the tile.</summary>
    public static Texture2D PalmBark(int seed)
    {
        const int size = 256;
        var px = new Color[size * size];
        var dark = new Color(0.2f, 0.18f, 0.15f);
        var light = new Color(0.6f, 0.55f, 0.47f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float wobble = GrassTextures.Noise(u, v, 6, 4, seed) - 0.5f;
                float ring = Mathf.Repeat(v * 8f + 0.3f * wobble, 1f);
                float scar = 1f - Mathf.Clamp01(Mathf.Min(ring, 1f - ring) / 0.07f);
                float fine = GrassTextures.Noise(u, v, 96, 32, seed + 1) - 0.5f;
                float fibres = GrassTextures.Noise(u, v, 48, 4, seed + 2) - 0.5f;
                float g = 0.6f + 0.3f * fine + 0.3f * fibres - 0.5f * scar + 0.2f * (ring - 0.5f);
                px[x + y * size] = Color.Lerp(dark, light, Mathf.Clamp01(g));
            }
        return Tiling(px, size, "Palm bark");
    }

    /// <summary>Round cluster of leaves on a few twigs (broadleaf trees, bushes).</summary>
    public static Texture2D LeafCluster(int seed, int leaves, float leafSize)
    {
        const int size = 512;
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        var dark = new Color(0.12f, 0.24f, 0.06f);
        var light = new Color(0.33f, 0.47f, 0.13f);
        var canvas = new Canvas(size, size, Color.Lerp(dark, light, 0.5f));
        float cx = size * 0.5f, cy = size * 0.5f, radius = size * 0.38f;

        var twig = new Color(0.24f, 0.19f, 0.14f);
        for (int i = 0; i < 7; i++)
        {
            float a = R() * 2f * Mathf.PI, l = radius * (0.6f + 0.4f * R());
            canvas.Line(cx, cy, cx + Mathf.Cos(a) * l, cy + Mathf.Sin(a) * l, 2.5f, twig);
        }
        for (int i = 0; i < leaves; i++)
        {
            // dense in the middle, ragged towards the edge (no round outline)
            float a = R() * 2f * Mathf.PI, d = radius * Mathf.Pow(R(), 0.75f) * (0.75f + 0.35f * R());
            float x = cx + Mathf.Cos(a) * d, y = cy + Mathf.Sin(a) * d;
            float angle = a + (R() - 0.5f) * 2f; // roughly outwards
            float len = size * leafSize * (0.8f + 0.4f * R());
            float depth = (float)i / leaves;     // later leaves lie on top: lighter
            Color c = Color.Lerp(dark, light, 0.25f + 0.5f * R()) * Mathf.Lerp(0.7f, 1.15f, depth);
            canvas.Leaf(x, y, angle, len, len * 0.5f, c);
        }
        return Foliage(canvas, "Leaf cluster");
    }

    /// <summary>Conifer branch tip seen from above: u along the branch (0 = trunk), v across.</summary>
    public static Texture2D ConiferSpray(int seed)
    {
        const int w = 512, h = 256;
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        var dark = new Color(0.07f, 0.17f, 0.09f);
        var light = new Color(0.17f, 0.3f, 0.15f);
        var twigColour = new Color(0.22f, 0.17f, 0.12f);
        var canvas = new Canvas(w, h, Color.Lerp(dark, light, 0.5f));
        float cy = h * 0.5f;

        var a0 = new Vector2(0f, cy);
        var b0 = new Vector2(w * 0.97f, cy + (R() - 0.5f) * 10f);
        var twigs = new System.Collections.Generic.List<(Vector2 a, Vector2 b, float scale)> { (a0, b0, 1f) };
        const int sideTwigs = 14;
        for (int i = 0; i < sideTwigs; i++)
        {
            float t = 0.06f + 0.8f * i / (sideTwigs - 1) + (R() - 0.5f) * 0.03f;
            Vector2 s = Vector2.Lerp(a0, b0, t);
            float side = i % 2 == 0 ? 1f : -1f;
            float ang = side * (35f + 20f * R()) * Mathf.Deg2Rad;
            float envelope = t < 0.6f ? 1f : Mathf.Lerp(1f, 0.35f, (t - 0.6f) / 0.4f);
            float len = h * 0.4f * envelope;
            twigs.Add((s, s + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len, 0.7f));
        }
        foreach (var t in twigs) canvas.Line(t.a.x, t.a.y, t.b.x, t.b.y, 2.5f * t.scale + 0.5f, twigColour);
        foreach (var t in twigs)
        {
            Vector2 dir = (t.b - t.a).normalized, perp = new Vector2(-dir.y, dir.x);
            float length = (t.b - t.a).magnitude;
            for (float s = 0f; s < length; s += 1.6f)
            {
                float tip = 1f - 0.55f * (s / length) * (s / length);
                for (int k = -1; k <= 1; k += 2)
                {
                    float ang = (50f + 15f * R()) * Mathf.Deg2Rad;
                    Vector2 nd = dir * Mathf.Cos(ang) + perp * (k * Mathf.Sin(ang));
                    float nl = (20f + 10f * R()) * tip * (t.scale < 1f ? 0.85f : 1f);
                    Vector2 p = t.a + dir * s;
                    Color c = Color.Lerp(dark, light, R()) * Mathf.Lerp(0.8f, 1.1f, R());
                    canvas.Line(p.x, p.y, p.x + nd.x * nl, p.y + nd.y * nl, 2f, c);
                }
            }
        }
        return Foliage(canvas, "Conifer spray");
    }

    /// <summary>Palm frond: u along the frond (0 = trunk), v across; leaflets on both sides of the stem.</summary>
    public static Texture2D PalmFrond(int seed)
    {
        const int w = 512, h = 256;
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        var dark = new Color(0.16f, 0.32f, 0.08f);
        var light = new Color(0.33f, 0.5f, 0.14f);
        var dry = new Color(0.5f, 0.45f, 0.2f);
        var stem = new Color(0.42f, 0.42f, 0.2f);
        var canvas = new Canvas(w, h, Color.Lerp(dark, light, 0.5f));
        float cy = h * 0.5f;

        for (float x = w * 0.06f; x < w * 0.98f; x += 4.5f)
        {
            float t = x / w;
            float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * (0.1f + 0.9f * t)), 0.7f); // widest at ~45 %
            for (int k = -1; k <= 1; k += 2)
            {
                float ang = k * (58f + 12f * R()) * Mathf.Deg2Rad;
                float len = h * 0.47f * envelope * (0.85f + 0.15f * R());
                Color c = R() < 0.07f ? dry : Color.Lerp(dark, light, R());
                canvas.Leaf(x, cy, ang, len, 6f + 2f * R(), c);
            }
        }
        canvas.Line(0f, cy, w * 0.99f, cy, 3f, stem);
        return Foliage(canvas, "Palm frond");
    }

    // ---- helpers ----

    static Texture2D Tiling(Color[] px, int size, string name)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D Foliage(Canvas canvas, string name)
    {
        var tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, true)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.DontSave,
        };
        FillMipsKeepingCoverage(tex, canvas.Px, canvas.W, canvas.H);
        return tex;
    }

    // Box-filtered mips whose alpha is scaled so the clipped area stays the same as on the full-size texture.
    static void FillMipsKeepingCoverage(Texture2D tex, Color[] top, int w, int h)
    {
        float target = Coverage(top, 1f);
        tex.SetPixels(top, 0);
        Color[] prev = top;
        int pw = w, ph = h;
        for (int m = 1; m < tex.mipmapCount; m++)
        {
            int cw = Mathf.Max(1, pw / 2), ch = Mathf.Max(1, ph / 2);
            var cur = new Color[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int x0 = Mathf.Min(2 * x, pw - 1), x1 = Mathf.Min(2 * x + 1, pw - 1);
                    int y0 = Mathf.Min(2 * y, ph - 1), y1 = Mathf.Min(2 * y + 1, ph - 1);
                    cur[x + y * cw] = (prev[x0 + y0 * pw] + prev[x1 + y0 * pw] + prev[x0 + y1 * pw] + prev[x1 + y1 * pw]) * 0.25f;
                }
            float lo = 0.5f, hi = 8f;
            for (int k = 0; k < 14; k++)
            {
                float mid = 0.5f * (lo + hi);
                if (Coverage(cur, mid) < target) lo = mid; else hi = mid;
            }
            for (int i = 0; i < cur.Length; i++) cur[i].a = Mathf.Min(1f, cur[i].a * hi);
            tex.SetPixels(cur, m);
            prev = cur;
            pw = cw;
            ph = ch;
        }
        tex.Apply(false);
    }

    static float Coverage(Color[] px, float alphaScale)
    {
        int n = 0;
        foreach (Color c in px) if (c.a * alphaScale >= Cutoff) n++;
        return (float)n / px.Length;
    }

    // Simple pixel canvas; everything drawn is opaque (alpha 1) on a transparent background of the given colour
    // (so filtering at the edges doesn't bleed in a wrong colour).
    sealed class Canvas
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public Canvas(int w, int h, Color background)
        {
            W = w;
            H = h;
            Px = new Color[w * h];
            background.a = 0f;
            for (int i = 0; i < Px.Length; i++) Px[i] = background;
        }

        // Leaf from its base (x, y) pointing along angle (radians); pointed at both ends, lighter midrib.
        public void Leaf(float x, float y, float angle, float length, float width, Color c)
        {
            float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
            float reach = length + width;
            int xa = Mathf.Max(0, Mathf.FloorToInt(x - reach)), xb = Mathf.Min(W - 1, Mathf.CeilToInt(x + reach));
            int ya = Mathf.Max(0, Mathf.FloorToInt(y - reach)), yb = Mathf.Min(H - 1, Mathf.CeilToInt(y + reach));
            for (int py = ya; py <= yb; py++)
                for (int px = xa; px <= xb; px++)
                {
                    float rx = px + 0.5f - x, ry = py + 0.5f - y;
                    float along = rx * dx + ry * dy;
                    if (along < 0f || along > length) continue;
                    float across = Mathf.Abs(-rx * dy + ry * dx);
                    float t = along / length;
                    float half = 0.5f * width * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
                    if (across > half) continue;
                    float edge = half > 0f ? across / half : 1f;
                    float shade = Mathf.Lerp(1.08f, 0.8f, edge * edge);
                    if (across < 0.7f && t < 0.9f) shade *= 1.12f;
                    Px[px + py * W] = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                }
        }

        public void Line(float xa, float ya, float xb, float yb, float thickness, Color c)
        {
            float r = thickness * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(xa, xb) - r - 1)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(xa, xb) + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ya, yb) - r - 1)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(ya, yb) + r + 1));
            var a = new Vector2(xa, ya);
            Vector2 ab = new Vector2(xb, yb) - a;
            float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
            c.a = 1f;
            for (int py = y0; py <= y1; py++)
                for (int px = x0; px <= x1; px++)
                {
                    var p = new Vector2(px + 0.5f, py + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    if ((p - (a + ab * t)).sqrMagnitude <= r * r) Px[px + py * W] = c;
                }
        }
    }
}
