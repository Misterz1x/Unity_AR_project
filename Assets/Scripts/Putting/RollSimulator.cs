using System.Collections.Generic;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>One point of the simulated ball path.</summary>
    public readonly struct PathPoint
    {
        public readonly float Time;
        public readonly Vector2 Position;
        public readonly float Speed;

        public PathPoint(float time, Vector2 position, float speed)
        {
            Time = time;
            Position = position;
            Speed = speed;
        }
    }

    /// <summary>Outcome of one simulated putt. Positions are on the floor plane (x = world X, y = world Z).</summary>
    public sealed class RollResult
    {
        public bool Holed { get; internal set; }
        /// <summary>False if the ball was still rolling at the distance or time cap.</summary>
        public bool Stopped { get; internal set; }
        public Vector2 Start { get; internal set; }
        /// <summary>Launch direction (unit).</summary>
        public Vector2 Direction { get; internal set; }
        public Vector2 End { get; internal set; }
        public Vector2 Hole { get; internal set; }
        /// <summary>Distance travelled along the path until the ball stopped or dropped (m).</summary>
        public float Distance { get; internal set; }
        /// <summary>Time until the ball stopped or dropped (s).</summary>
        public float Duration { get; internal set; }
        public float LaunchSpeed { get; internal set; }
        /// <summary>Speed when entering the hole, or when passing closest to it (m/s). NaN if it stopped short.</summary>
        public float SpeedAtHole { get; internal set; }
        /// <summary>Closest the ball centre came to the hole centre (m).</summary>
        public float ClosestApproach { get; internal set; }
        /// <summary>Where on its path the ball came closest to the hole centre.</summary>
        public Vector2 ClosestPoint { get; internal set; }
        /// <summary>
        /// Where the ball stopped, measured from the hole sideways to the start→hole line (m).
        /// Positive = right of the hole, seen from the start. 0 if holed.
        /// </summary>
        public float LateralOffset { get; internal set; }
        /// <summary>Where the ball stopped along the start→hole line (m). Positive = long, negative = short. 0 if holed.</summary>
        public float LongShort { get; internal set; }
        /// <summary>Ball path, one point per physics step.</summary>
        public IReadOnlyList<PathPoint> Path { get; internal set; }

        public float EndDistanceToHole => (End - Hole).magnitude;

        /// <summary>Ball position at time t after launch (clamped to the start and end of the roll).</summary>
        public Vector2 PositionAt(float t)
        {
            if (Path == null || Path.Count == 0) return Start;
            if (t <= Path[0].Time) return Path[0].Position;
            if (t >= Path[Path.Count - 1].Time) return Path[Path.Count - 1].Position;

            int lo = 0, hi = Path.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Path[mid].Time <= t) lo = mid; else hi = mid;
            }
            PathPoint a = Path[lo], b = Path[hi];
            float span = b.Time - a.Time;
            return span > 0f ? Vector2.Lerp(a.Position, b.Position, (t - a.Time) / span) : b.Position;
        }

        public override string ToString() =>
            Holed
                ? $"HOLED. Launch {LaunchSpeed:F2} m/s, {SpeedAtHole:F2} m/s at the hole"
                : $"Missed: {Mathf.Abs(LongShort) * 100f:F0} cm {(LongShort >= 0f ? "long" : "short")}, " +
                  $"{Mathf.Abs(LateralOffset) * 100f:F0} cm {(LateralOffset >= 0f ? "right" : "left")}. " +
                  $"Launch {LaunchSpeed:F2} m/s, rolled {Distance:F2} m";
    }

    /// <summary>
    /// Our own roll model (no Rigidbody). The ball moves on the floor plane, step by step:
    ///   a = -mu k g v̂ - (5/7) g ∇h      ∇h = slope of the green (small-slope approximation, fine up to ~8 %)
    ///                                    k = friction scale of the grass (1 on the green, more on the fringe)
    /// A ball at rest stays still while (5/7)|∇h| ≤ mu k, otherwise it starts rolling downhill.
    /// Holed rule: the ball centre enters the hole circle at ≤ maxCaptureSpeed. Faster = lip-out, it rolls on.
    /// </summary>
    public static class RollSimulator
    {
        const float RestSpeed = 1e-4f;
        /// <summary>Speed used to animate the last bit from the hole edge to the hole centre (m/s).</summary>
        const float MinDropSpeed = 0.3f;

        /// <summary>Distance (m) a ball launched at v0 rolls on a flat green.</summary>
        public static float StopDistance(float v0, RollSettings s) =>
            v0 * v0 / (2f * s.FrictionDeceleration);

        /// <summary>Launch speed (m/s) that stops exactly at the given distance on a flat green.</summary>
        public static float LaunchSpeedForDistance(float distance, RollSettings s) =>
            Mathf.Sqrt(2f * s.FrictionDeceleration * Mathf.Max(0f, distance));

        /// <summary>Simulates one putt. surface = null means a flat green.</summary>
        public static RollResult Simulate(LaunchResult launch, Vector2 start, Vector2 hole, RollSettings s,
            IGreenSurface surface = null)
        {
            surface ??= PlaneGreen.Flat;

            float grassFriction = s.FrictionDeceleration;
            float pull = RollSettings.RollingGravityFactor * s.gravity;
            float dt = Mathf.Max(1e-4f, s.timeStep);
            float r = s.HoleRadius;

            Vector2 p = start;
            Vector2 v = launch.Direction * launch.Speed;
            float t = 0f, distance = 0f;
            bool holed = false, stopped = false;
            float speedAtHole = float.NaN;
            bool lippedOut = false;

            float closest = (p - hole).magnitude, closestSpeed = v.magnitude;
            Vector2 closestPoint = p;
            bool insideHole = closest <= r; // a ball placed in the hole is ignored until it leaves

            var path = new List<PathPoint> { new PathPoint(0f, p, v.magnitude) };

            while (t < s.maxRollTime && distance < s.maxRollDistance)
            {
                Vector2 gravity = -pull * surface.SlopeAt(p);
                float friction = grassFriction * surface.FrictionScaleAt(p);
                float speed = v.magnitude;
                float h = dt;
                Vector2 a;

                if (speed < RestSpeed)
                {
                    // At rest: does the slope beat the grass?
                    float g = gravity.magnitude;
                    if (g <= friction)
                    {
                        stopped = true;
                        break;
                    }
                    v = Vector2.zero;
                    a = gravity - friction * (gravity / g);
                }
                else
                {
                    a = gravity - friction * (v / speed);
                    // If the ball stops during this step, shorten the step to end exactly when it does.
                    float along = Vector2.Dot(a, v) / speed;
                    if (along < 0f && speed + along * dt <= 0f)
                        h = -speed / along;
                }

                Vector2 next = p + v * h + 0.5f * a * h * h;
                Vector2 vNext = h < dt ? Vector2.zero : v + a * h;

                // Hole: did the ball centre enter the hole circle on this step?
                if (!insideHole && SegmentEntersCircle(p, next, hole, r, out float u))
                {
                    Vector2 entry = Vector2.Lerp(p, next, u);
                    float entrySpeed = (v + a * (u * h)).magnitude;
                    speedAtHole = entrySpeed;
                    if (entrySpeed <= s.maxCaptureSpeed)
                    {
                        holed = true;
                        stopped = true;
                        distance += (entry - p).magnitude;
                        t += u * h;
                        path.Add(new PathPoint(t, entry, entrySpeed));

                        // Drop: finish at the hole centre.
                        float toCentre = (hole - entry).magnitude;
                        distance += toCentre;
                        t += toCentre / Mathf.Max(entrySpeed, MinDropSpeed);
                        p = hole;
                        path.Add(new PathPoint(t, p, 0f));
                        closest = 0f;
                        closestPoint = hole;
                        break;
                    }
                    lippedOut = true;
                    insideHole = true;
                }
                else if (insideHole && (next - hole).sqrMagnitude > r * r)
                {
                    insideHole = false;
                }

                TrackClosest(p, next, v, vNext, hole, ref closest, ref closestSpeed, ref closestPoint);

                distance += (next - p).magnitude;
                t += h;
                p = next;
                v = vNext;
                path.Add(new PathPoint(t, p, v.magnitude));
            }

            if (!holed && !lippedOut)
                speedAtHole = closestSpeed > 1e-3f ? closestSpeed : float.NaN;

            Vector2 toHole = hole - start;
            Vector2 line = toHole.sqrMagnitude > 0f ? toHole.normalized
                : launch.Direction != Vector2.zero ? launch.Direction : Vector2.up;
            Vector2 right = new Vector2(line.y, -line.x);
            Vector2 miss = p - hole;

            return new RollResult
            {
                Holed = holed,
                Stopped = stopped,
                Start = start,
                Direction = launch.Direction,
                End = p,
                Hole = hole,
                Distance = distance,
                Duration = t,
                LaunchSpeed = launch.Speed,
                SpeedAtHole = speedAtHole,
                ClosestApproach = closest,
                ClosestPoint = closestPoint,
                LateralOffset = holed ? 0f : Vector2.Dot(miss, right),
                LongShort = holed ? 0f : Vector2.Dot(miss, line),
                Path = path,
            };
        }

        // First u in [0, 1] where the segment a→b is inside the circle (centre c, radius r), entering from outside.
        static bool SegmentEntersCircle(Vector2 a, Vector2 b, Vector2 c, float r, out float u)
        {
            u = 0f;
            Vector2 d = b - a, f = a - c;
            float A = Vector2.Dot(d, d);
            float C = Vector2.Dot(f, f) - r * r;
            if (A < 1e-12f) return false;
            float B = 2f * Vector2.Dot(f, d);
            float disc = B * B - 4f * A * C;
            if (disc < 0f) return false;
            u = (-B - Mathf.Sqrt(disc)) / (2f * A);
            return u >= 0f && u <= 1f;
        }

        // Closest point of segment a→b to c, with the speed there (linear in between).
        static void TrackClosest(Vector2 a, Vector2 b, Vector2 va, Vector2 vb, Vector2 c,
            ref float closest, ref float closestSpeed, ref Vector2 closestPoint)
        {
            Vector2 d = b - a;
            float len2 = Vector2.Dot(d, d);
            float u = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(c - a, d) / len2) : 0f;
            Vector2 point = a + d * u;
            float dist = (point - c).magnitude;
            if (dist < closest)
            {
                closest = dist;
                closestSpeed = Vector2.Lerp(va, vb, u).magnitude;
                closestPoint = point;
            }
        }
    }
}
