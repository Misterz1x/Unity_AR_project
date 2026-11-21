using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// Shape of the green. Positions are on the floor plane (x = world X, y = world Z), heights in metres.
    /// </summary>
    public interface IGreenSurface
    {
        /// <summary>Height of the surface at p (m).</summary>
        float HeightAt(Vector2 p);

        /// <summary>Slope (dh/dx, dh/dz) at p, dimensionless: (0.02, 0) = rises 2 % towards +X.</summary>
        Vector2 SlopeAt(Vector2 p);

        /// <summary>
        /// Grass friction relative to the green at p: 1 on the green, more on the fringe or rough
        /// (fringe at stimp 6 on a stimp-10 green = 10/6 ≈ 1.7).
        /// </summary>
        float FrictionScaleAt(Vector2 p);
    }

    /// <summary>A green that is one tilted plane, all green grass. Flat green = slope (0, 0).</summary>
    public sealed class PlaneGreen : IGreenSurface
    {
        public static readonly PlaneGreen Flat = new PlaneGreen(Vector2.zero);

        public readonly Vector2 Slope;

        public PlaneGreen(Vector2 slope) => Slope = slope;

        /// <summary>Slope in percent, as greenkeepers state it. + = rises towards +X / +Z.</summary>
        public static PlaneGreen FromPercent(float alongX, float alongZ) =>
            new PlaneGreen(new Vector2(alongX, alongZ) / 100f);

        public float HeightAt(Vector2 p) => Vector2.Dot(Slope, p);
        public Vector2 SlopeAt(Vector2 p) => Slope;
        public float FrictionScaleAt(Vector2 p) => 1f;
    }
}
