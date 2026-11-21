using System.Collections.Generic;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>One point of the simulated ball path.</summary>
    public readonly struct PathPoint
    {
        public readonly float Time;
        public readonly Vector2 Position;
        /// <summary>Horizontal speed (m/s).</summary>
        public readonly float Speed;
        /// <summary>How far the ball centre is below its rolling height (m): 0 on the grass, up to the cup depth in the hole.</summary>
        public readonly float Depth;

        public PathPoint(float time, Vector2 position, float speed, float depth = 0f)
        {
            Time = time;
            Position = position;
            Speed = speed;
            Depth = depth;
        }
    }

    /// <summary>Outcome of one simulated putt. Positions are on the floor plane (x = world X, y = world Z).</summary>
    public sealed class RollResult
    {
        public bool Holed { get; internal set; }
        /// <summary>The ball ran over the hole too fast and came out again (lip-out or hop).</summary>
        public bool LippedOut { get; internal set; }
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

        /// <summary>How far the ball is below its rolling height at time t (m): dips and drops at the hole.</summary>
        public float DepthAt(float t)
        {
            if (Path == null || Path.Count == 0) return 0f;
            if (t <= Path[0].Time) return Path[0].Depth;
            if (t >= Path[Path.Count - 1].Time) return Path[Path.Count - 1].Depth;
            int lo = 0, hi = Path.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Path[mid].Time <= t) lo = mid; else hi = mid;
            }
            PathPoint a = Path[lo], b = Path[hi];
            float span = b.Time - a.Time;
            return span > 0f ? Mathf.Lerp(a.Depth, b.Depth, (t - a.Time) / span) : b.Depth;
        }

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
                : (LippedOut ? "Lip-out! " : "") + $"Missed:  {Mathf.Abs(LongShort) * 100f:F0} cm {(LongShort >= 0f ? "long" : "short")}, " +
                  $"{Mathf.Abs(LateralOffset) * 100f:F0} cm {(LateralOffset >= 0f ? "right" : "left")}. " +
                  $"Launch {LaunchSpeed:F2} m/s, rolled {Distance:F2} m";
    }

    /// <summary>
    /// Our own roll model (no Rigidbody). The ball moves on the floor plane, step by step:
    ///   a = -mu k g v̂ - (5/7) g ∇h      ∇h = slope of the green (small-slope approximation, fine up to ~8 %)
    ///                                    k = friction scale of the grass (1 on the green, more on the fringe)
    /// A ball at rest stays still while (5/7)|∇h| ≤ mu k, otherwise it starts rolling downhill.
    /// Hole: when the ball centre enters the hole circle it drops if its speed is at most
    ///   maxCaptureSpeed × chord / hole diameter   (it must fall far enough while crossing the hole),
    /// so the capture speed is highest over the centre and falls towards the edge. Faster = it runs over:
    /// over the centre it hops the hole, off-centre it lips out (loses speed, turned away from the hole).
    /// A captured ball falls into the cup, hits the back wall and settles on the bottom (Path depth).
    /// </summary>
    public static class RollSimulator
    {
        const float RestSpeed = 1e-4f;
        /// <summary>Largest turn of a lip-out (degrees), for a ball just a little too fast at mid offset.</summary>
        const float MaxLipTurn = 110f;

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
                    Vector2 vEntry = v + a * (u * h);
                    float entrySpeed = vEntry.magnitude;
                    speedAtHole = entrySpeed;
                    distance += (entry - p).magnitude;
                    t += u * h;
                    path.Add(new PathPoint(t, entry, entrySpeed));
                    closest = Mathf.Min(closest, r);

                    Vector2 dir = entrySpeed > 1e-6f ? vEntry / entrySpeed : (hole - entry).normalized;
                    Vector2 toCentre = hole - entry;
                    float halfChord = Mathf.Max(0f, Vector2.Dot(toCentre, dir));
                    float offset = Mathf.Sqrt(Mathf.Max(0f, toCentre.sqrMagnitude - halfChord * halfChord));
                    float capture = s.maxCaptureSpeed * halfChord / r; // chord / diameter

                    if (entrySpeed <= capture)
                    {
                        holed = true;
                        stopped = true;
                        closest = 0f;
                        closestPoint = hole;
                        p = DropIntoCup(entry, vEntry, hole, s, path, ref t, ref distance);
                        break;
                    }

                    // Too fast: runs over the hole, dipping a little, and comes out on the far side.
                    lippedOut = true;
                    insideHole = true;
                    float chord = 2f * halfChord;
                    float cross = chord / Mathf.Max(entrySpeed, 1e-3f);
                    float dip = Mathf.Min(0.5f * s.gravity * cross * cross, s.BallRadius);
                    Vector2 exit = entry + dir * chord;
                    path.Add(new PathPoint(t + 0.5f * cross, entry + dir * halfChord, entrySpeed, dip));

                    // Off-centre balls catch the lip and turn away from the hole; centred ones hit the back and hop.
                    float frac = Mathf.Clamp01(offset / r);
                    float turn = MaxLipTurn * frac * Mathf.Clamp01(capture / entrySpeed);
                    float side = dir.x * toCentre.y - dir.y * toCentre.x; // > 0: hole centre is left of the ball's path
                    Vector2 outDir = GreenMath.Rotate(dir, side >= 0f ? turn : -turn); // turn away from the centre
                    Vector2 outward = (exit - hole).normalized;
                    if (Vector2.Dot(outDir, outward) < 0.1f)
                        outDir = (outDir - Vector2.Dot(outDir, outward) * outward + 0.1f * outward).normalized;
                    float outSpeed = entrySpeed * (0.55f + 0.35f * frac);

                    t += cross;
                    distance += chord;
                    p = exit;
                    v = outDir * outSpeed;
                    path.Add(new PathPoint(t, p, outSpeed));
                    continue;
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
            if (holed) lippedOut = false;

            Vector2 toHole = hole - start;
            Vector2 line = toHole.sqrMagnitude > 0f ? toHole.normalized
                : launch.Direction != Vector2.zero ? launch.Direction : Vector2.up;
            Vector2 right = new Vector2(line.y, -line.x);
            Vector2 miss = p - hole;

            return new RollResult
            {
                Holed = holed,
                LippedOut = lippedOut,
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

        // The ball falls into the cup: keeps its speed across, falls with gravity, bounces off the cup wall
        // (once its centre is below the rim) and the bottom, and settles. Adds path points with depth.
        static Vector2 DropIntoCup(Vector2 entry, Vector2 velocity, Vector2 hole, RollSettings s,
            List<PathPoint> path, ref float t, ref float distance)
        {
            const float dt = 1f / 480f, wallRestitution = 0.3f, bottomRestitution = 0.25f, maxTime = 2f;
            float wall = Mathf.Max(0.001f, s.HoleRadius - s.BallRadius);
            Vector2 pos = entry, hv = velocity;
            float depth = 0f, vz = 0f, elapsed = 0f, sinceSample = 0f;

            while (elapsed < maxTime)
            {
                vz += s.gravity * dt;
                depth += vz * dt;
                if (depth >= s.cupDepth)
                {
                    depth = s.cupDepth;
                    vz = Mathf.Abs(vz) > 0.25f ? -vz * bottomRestitution : 0f;
                    hv *= 0.6f; // rolling on the cup bottom
                }

                Vector2 before = pos;
                pos += hv * dt;
                // Below the rim the ball is inside the cup: keep its centre off the wall.
                Vector2 fromCentre = pos - hole;
                if (depth > 0.3f * s.BallRadius && fromCentre.magnitude > wall)
                {
                    Vector2 n = fromCentre.normalized;
                    pos = hole + n * wall;
                    float radial = Vector2.Dot(hv, n);
                    if (radial > 0f) hv -= (1f + wallRestitution) * radial * n;
                }
                hv *= 1f - 1.5f * dt;
                distance += (pos - before).magnitude;
                elapsed += dt;
                sinceSample += dt;

                bool settled = depth >= s.cupDepth && vz == 0f && hv.magnitude < 0.01f;
                if (sinceSample >= 1f / 120f || settled)
                {
                    path.Add(new PathPoint(t + elapsed, pos, hv.magnitude, depth));
                    sinceSample = 0f;
                }
                if (settled) break;
            }
            t += elapsed;
            return pos;
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
