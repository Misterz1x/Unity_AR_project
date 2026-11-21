using UnityEngine;

namespace ARGolf.Putting
{
    public enum GrassKind { Green, Fringe, Rough }

    /// <summary>
    /// A GreenDefinition baked into a height field, in green-local coordinates.
    /// Heights are shifted so the lowest point on the green is 0.
    /// </summary>
    public sealed class GreenModel : IGreenSurface
    {
        public const float DefaultCellSize = 0.1f;

        public GreenDefinition Definition { get; }
        public HeightField Heights { get; }

        readonly float fringeFriction, roughFriction;

        GreenModel(GreenDefinition definition, HeightField heights)
        {
            Definition = definition;
            Heights = heights;
            fringeFriction = definition.stimpFeet / Mathf.Max(0.5f, definition.fringeStimpFeet);
            roughFriction = definition.stimpFeet / Mathf.Max(0.5f, definition.roughStimpFeet);
        }

        public static GreenModel Build(GreenDefinition d, float cellSize = DefaultCellSize)
        {
            float reach = d.outline.MaxRadius() + d.fringeWidth + 1f;
            Vector2 min = d.outline.center - Vector2.one * reach;
            Vector2 max = d.outline.center + Vector2.one * reach;
            HeightField field = HeightField.Sample(min, max, cellSize, d.FeatureHeightAt);

            float lowest = float.MaxValue;
            for (int j = 0; j < field.CountZ; j++)
                for (int i = 0; i < field.CountX; i++)
                    if (d.outline.Contains(field.PointAt(i, j)))
                        lowest = Mathf.Min(lowest, field[i, j]);
            if (lowest < float.MaxValue)
                field.Offset(-lowest);

            return new GreenModel(d, field);
        }

        /// <summary>Roll settings with this green's speed.</summary>
        public RollSettings MakeRollSettings(RollSettings template = null)
        {
            var s = new RollSettings();
            if (template != null)
            {
                s.holeDiameter = template.holeDiameter;
                s.ballDiameter = template.ballDiameter;
                s.maxCaptureSpeed = template.maxCaptureSpeed;
                s.timeStep = template.timeStep;
                s.maxRollDistance = template.maxRollDistance;
                s.maxRollTime = template.maxRollTime;
                s.gravity = template.gravity;
            }
            s.stimpFeet = Definition.stimpFeet;
            return s;
        }

        public GrassKind GrassAt(Vector2 p)
        {
            float outside = Definition.outline.DistanceOutside(p);
            if (outside <= 0f) return GrassKind.Green;
            return outside <= Definition.fringeWidth ? GrassKind.Fringe : GrassKind.Rough;
        }

        public bool IsOnGreen(Vector2 p) => GrassAt(p) == GrassKind.Green;

        public Vector2 PinPosition(int pin) => Definition.pins[Mathf.Clamp(pin, 0, Definition.pins.Count - 1)].position;

        public float HeightAt(Vector2 p) => Heights.HeightAt(p);
        public Vector2 SlopeAt(Vector2 p) => Heights.SlopeAt(p);

        /// <summary>
        /// Friction relative to the green: 1 on the green, fringe and rough scaled by the stimp ratio.
        /// Blends over GrassBlend metres at each edge, so the ball does not hit a sudden wall of friction.
        /// </summary>
        public float FrictionScaleAt(Vector2 p)
        {
            float outside = Definition.outline.DistanceOutside(p);
            float toFringe = Smooth((outside + 0.5f * GrassBlend) / GrassBlend);
            float toRough = Smooth((outside - Definition.fringeWidth + 0.5f * GrassBlend) / GrassBlend);
            return Mathf.Lerp(Mathf.Lerp(1f, fringeFriction, toFringe), roughFriction, toRough);
        }

        /// <summary>Width of the friction blend at the green and fringe edges (m).</summary>
        public const float GrassBlend = 0.1f;

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Steepest slope within radius of p (dimensionless), sampled on rings.</summary>
        public float MaxSlopeNear(Vector2 p, float radius)
        {
            float max = SlopeAt(p).magnitude;
            for (int ring = 1; ring <= 3; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius * ring / 3f);
                    max = Mathf.Max(max, SlopeAt(q).magnitude);
                }
            return max;
        }
    }

    /// <summary>
    /// Puts a green-local surface into the world: moved to origin and turned by yaw
    /// (degrees, clockwise seen from above, like a Unity transform's Y rotation).
    /// </summary>
    public sealed class PlacedGreen : IGreenSurface
    {
        public readonly IGreenSurface Local;
        public readonly Vector2 Origin;
        public readonly float Yaw;
        public readonly float HeightOffset;

        public PlacedGreen(IGreenSurface local, Vector2 origin, float yaw, float heightOffset = 0f)
        {
            Local = local;
            Origin = origin;
            Yaw = yaw;
            HeightOffset = heightOffset;
        }

        public Vector2 ToLocal(Vector2 world) => GreenMath.Rotate(world - Origin, -Yaw);
        public Vector2 ToWorld(Vector2 local) => Origin + GreenMath.Rotate(local, Yaw);

        public float HeightAt(Vector2 p) => Local.HeightAt(ToLocal(p)) + HeightOffset;
        public Vector2 SlopeAt(Vector2 p) => GreenMath.Rotate(Local.SlopeAt(ToLocal(p)), Yaw);
        public float FrictionScaleAt(Vector2 p) => Local.FrictionScaleAt(ToLocal(p));
    }
}
