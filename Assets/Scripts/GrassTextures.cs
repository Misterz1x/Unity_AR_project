using UnityEngine;

/// <summary>
/// Generated grass detail texture: neutral grey (0.5) with fine grain, for the URP Lit
/// "Detail Albedo" (multiply x2), so it adds texture without changing the colour.
/// Tiles seamlessly (periodic noise).
/// </summary>
public static class GrassTextures
{
    const int DefaultSize = 512;
    const int DefaultSeed = 11;

    static Texture2D detail;

    /// <summary>Shared runtime copy (not saved with the scene).</summary>
    public static Texture2D Detail
    {
        get
        {
            if (detail == null) detail = Make(DefaultSize, DefaultSeed, true);
            return detail;
        }
    }

    public static Texture2D Make(int size, int seed, bool dontSave)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Grass detail",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8,
        };
        if (dontSave) tex.hideFlags = HideFlags.DontSave;
        tex.SetPixels32(Pixels(size, seed));
        tex.Apply(true);
        return tex;
    }

    public static Color32[] Pixels(int size = DefaultSize, int seed = DefaultSeed)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float blades = Noise(u, v, 256, 40, seed) - 0.5f;      // thin, stretched along the mowing direction
                float fine = Noise(u, v, 160, 160, seed + 1) - 0.5f;   // single leaves
                float mid = Noise(u, v, 24, 24, seed + 2) - 0.5f;
                float clumps = Noise(u, v, 6, 6, seed + 3) - 0.5f;
                float warm = (Noise(u, v, 10, 10, seed + 4) - 0.5f) * 0.05f;

                float g = 0.5f + 0.17f * blades + 0.12f * fine + 0.06f * mid + 0.05f * clumps;
                px[x + y * size] = new Color(Mathf.Clamp01(g + warm), Mathf.Clamp01(g), Mathf.Clamp01(g - 0.5f * warm), 1f);
            }
        }
        return px;
    }

    // Value noise on a px × py lattice over the unit square; wraps at the edges, so it tiles.
    internal static float Noise(float u, float v, int px, int py, int seed)
    {
        float x = u * px, y = v * py;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int xa = Wrap(x0, px), xb = Wrap(x0 + 1, px), ya = Wrap(y0, py), yb = Wrap(y0 + 1, py);
        float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
