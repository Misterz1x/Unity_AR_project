using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// One green as data. Green-local coordinates in metres: x = right, y = away from the player's
    /// usual approach (the "front" of the green is at -y). Heights = sum of the features below.
    /// Serializable, so it works with the Inspector and JsonUtility.
    /// </summary>
    [Serializable]
    public class GreenDefinition
    {
        public string id = "green";
        public string name = "Green";
        [Tooltip("Shown in the app. Say \"inspired by\"; these are approximations, not surveys.")]
        public string description = "";

        [Header("Speed")]
        public float stimpFeet = 10f;
        public float fringeStimpFeet = 6f;
        public float roughStimpFeet = 3f;

        [Header("Shape")]
        public GreenOutline outline = new GreenOutline();
        [Tooltip("Width of the fringe ring around the green (m).")]
        public float fringeWidth = 0.9f;
        [Tooltip("Ball that leaves the green and fringe is in the water (island green).")]
        public bool waterAround;

        [Header("Height features")]
        [Tooltip("Whole green tilt in percent. x: + = rises to the right, y: + = rises to the back.")]
        public Vector2 tiltPercent;
        public List<GreenTier> tiers = new List<GreenTier>();
        public List<GreenMound> mounds = new List<GreenMound>();
        public List<GreenRidge> ridges = new List<GreenRidge>();
        public List<GreenCrown> crowns = new List<GreenCrown>();

        [Header("Holes and putts")]
        public List<GreenPin> pins = new List<GreenPin>();
        public List<GreenPutt> putts = new List<GreenPutt>();

        /// <summary>Height from the features (m), before normalising. Defined everywhere, also off the green.</summary>
        public float FeatureHeightAt(Vector2 p)
        {
            float h = Vector2.Dot(tiltPercent * 0.01f, p);
            foreach (var t in tiers) h += t.HeightAt(p);
            foreach (var m in mounds) h += m.HeightAt(p);
            foreach (var r in ridges) h += r.HeightAt(p);
            foreach (var c in crowns) h += c.HeightAt(p);
            return h;
        }
    }

    /// <summary>Oval outline with gentle bulges. Must contain its centre (star-shaped).</summary>
    [Serializable]
    public class GreenOutline
    {
        public Vector2 center;
        [Tooltip("Half size before rotation (m): x = half width, y = half depth.")]
        public Vector2 halfSize = new Vector2(8f, 10f);
        [Tooltip("Rotation in degrees, clockwise seen from above.")]
        public float rotation;
        public List<OutlineWobble> wobbles = new List<OutlineWobble>();

        /// <summary>Distance from the centre to the edge in direction angle phi (radians, outline frame).</summary>
        public float RadiusAt(float phi)
        {
            float c = Mathf.Cos(phi) / halfSize.x, s = Mathf.Sin(phi) / halfSize.y;
            float r = 1f / Mathf.Sqrt(c * c + s * s);
            float k = 1f;
            foreach (var w in wobbles) k += w.amount * Mathf.Cos(w.harmonic * phi + w.phase);
            return r * k;
        }

        /// <summary>Edge point in direction phi, pushed out by extra metres.</summary>
        public Vector2 EdgePoint(float phi, float extra = 0f) =>
            center + GreenMath.Rotate(new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * (RadiusAt(phi) + extra), rotation);

        /// <summary>How far p is outside the edge, measured from the centre (m). Negative = inside.</summary>
        public float DistanceOutside(Vector2 p)
        {
            Vector2 q = GreenMath.Rotate(p - center, -rotation);
            float d = q.magnitude;
            if (d < 1e-6f) return -RadiusAt(0f);
            return d - RadiusAt(Mathf.Atan2(q.y, q.x));
        }

        public bool Contains(Vector2 p) => DistanceOutside(p) <= 0f;

        /// <summary>Largest distance from the centre to the edge (m).</summary>
        public float MaxRadius()
        {
            float max = 0f;
            for (int k = 0; k < 360; k++) max = Mathf.Max(max, RadiusAt(k * Mathf.Deg2Rad));
            return max;
        }
    }

    [Serializable]
    public struct OutlineWobble
    {
        public int harmonic;
        [Tooltip("Size of the bulge as a share of the radius, e.g. 0.06.")]
        public float amount;
        [Tooltip("Radians.")]
        public float phase;

        public OutlineWobble(int harmonic, float amount, float phase)
        {
            this.harmonic = harmonic;
            this.amount = amount;
            this.phase = phase;
        }
    }

    /// <summary>A step between two levels. Smooth S-shaped transition.</summary>
    [Serializable]
    public class GreenTier
    {
        [Tooltip("A point in the middle of the step.")]
        public Vector2 at;
        [Tooltip("Direction towards the upper level (any length).")]
        public Vector2 upDirection = Vector2.up;
        [Tooltip("Height difference between the levels (m). Real tiers: 0.15 - 0.4.")]
        public float rise = 0.2f;
        [Tooltip("Width of the slope between the levels (m). Steepest slope = 1.5 rise / width.")]
        public float width = 2f;

        public float HeightAt(Vector2 p)
        {
            Vector2 n = upDirection.sqrMagnitude > 0f ? upDirection.normalized : Vector2.up;
            float s = Vector2.Dot(p - at, n) / Mathf.Max(width, 1e-3f) + 0.5f;
            s = Mathf.Clamp01(s);
            return rise * (s * s * (3f - 2f * s)) - rise * 0.5f;
        }
    }

    /// <summary>Round bump (height &gt; 0) or hollow (height &lt; 0).</summary>
    [Serializable]
    public class GreenMound
    {
        public Vector2 center;
        [Tooltip("Size (m): at this distance only ~14 % of the height is left.")]
        public float radius = 2f;
        public float height = 0.1f;

        public float HeightAt(Vector2 p)
        {
            float q = (p - center).magnitude / Mathf.Max(radius, 1e-3f);
            return height * Mathf.Exp(-2f * q * q);
        }
    }

    /// <summary>Long spine (height &gt; 0) or swale / gully (height &lt; 0) from a to b.</summary>
    [Serializable]
    public class GreenRidge
    {
        public Vector2 a, b;
        [Tooltip("Full width (m): at the side edge only ~14 % of the height is left.")]
        public float width = 2f;
        public float height = 0.08f;

        public float HeightAt(Vector2 p)
        {
            Vector2 d = b - a;
            float len2 = Vector2.Dot(d, d);
            float u = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / len2) : 0f;
            float q = (p - (a + d * u)).magnitude / Mathf.Max(width * 0.5f, 1e-3f);
            return height * Mathf.Exp(-2f * q * q);
        }
    }

    /// <summary>
    /// Dome over the whole green ("turtleback"): height at the centre, falling as a parabola,
    /// and still falling past the edge, so balls run off.
    /// </summary>
    [Serializable]
    public class GreenCrown
    {
        public Vector2 center;
        [Tooltip("Half size (m) where the dome has dropped by its full height.")]
        public Vector2 radii = new Vector2(10f, 12f);
        [Tooltip("Rotation in degrees, clockwise seen from above.")]
        public float rotation;
        public float height = 0.3f;

        public float HeightAt(Vector2 p)
        {
            Vector2 q = GreenMath.Rotate(p - center, -rotation);
            float x = q.x / Mathf.Max(radii.x, 1e-3f), y = q.y / Mathf.Max(radii.y, 1e-3f);
            return height * (1f - (x * x + y * y));
        }
    }

    [Serializable]
    public class GreenPin
    {
        public string name;
        public Vector2 position;

        public GreenPin(string name, Vector2 position)
        {
            this.name = name;
            this.position = position;
        }
    }

    /// <summary>A putt to practise: ball spot and which pin.</summary>
    [Serializable]
    public class GreenPutt
    {
        public string name;
        public Vector2 ball;
        public int pin;

        public GreenPutt(string name, Vector2 ball, int pin)
        {
            this.name = name;
            this.ball = ball;
            this.pin = pin;
        }
    }

    public static class GreenMath
    {
        /// <summary>
        /// Rotates a floor-plane vector clockwise seen from above, like Unity's Quaternion.Euler(0, degrees, 0)
        /// applied to (x, 0, y).
        /// </summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }
    }
}
