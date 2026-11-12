using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>Outcome of one simulated putt. Positions are on the floor plane (x = world X, y = world Z).</summary>
    public struct RollResult
    {
        public bool Holed;
        /// <summary>False if the ball was still rolling at RollSettings.maxRollDistance (steep downhill).</summary>
        public bool Stopped;
        public Vector2 Start;
        public Vector2 Direction;
        public Vector2 End;
        public Vector2 Hole;
        /// <summary>Distance travelled until the ball stopped or dropped (m).</summary>
        public float Distance;
        /// <summary>Time until the ball stopped or dropped (s).</summary>
        public float Duration;
        public float LaunchSpeed;
        public float Deceleration;
        /// <summary>Speed when the ball reached the hole (m/s); NaN if it stopped short.</summary>
        public float SpeedAtHole;
        /// <summary>Signed sideways miss of the ball's line (m). Positive = passes right of the hole, seen from the ball.</summary>
        public float LateralOffset;
        /// <summary>Signed along-line miss (m). Positive = long, negative = short. 0 if holed.</summary>
        public float LongShort;

        public float EndDistanceToHole => (End - Hole).magnitude;

        /// <summary>Ball position at time t after launch (clamped to the end of the roll).</summary>
        public Vector2 PositionAt(float t)
        {
            float tc = Mathf.Clamp(t, 0f, Duration);
            return Start + Direction * (LaunchSpeed * tc - 0.5f * Deceleration * tc * tc);
        }

        public override string ToString() =>
            Holed
                ? $"HOLED. Launch {LaunchSpeed:F2} m/s, {SpeedAtHole:F2} m/s at the hole"
                : $"Missed: {Mathf.Abs(LongShort) * 100f:F0} cm {(LongShort >= 0f ? "long" : "short")}, " +
                  $"{Mathf.Abs(LateralOffset) * 100f:F0} cm {(LateralOffset >= 0f ? "right" : "left")}. " +
                  $"Launch {LaunchSpeed:F2} m/s, rolled {Distance:F2} m";
    }

    /// <summary>
    /// Our own straight-line roll model (no Rigidbody):
    ///   a = -mu g - g sin(theta),  theta = slope along the roll, + uphill
    ///   flat stop distance = v² / (2 mu g)
    /// Holed rule: the ball's centre passes over the hole at ≤ maxCaptureSpeed.
    /// </summary>
    public static class RollSimulator
    {
        /// <summary>Distance (m) a ball launched at v0 rolls before stopping. +Infinity if it never stops.</summary>
        public static float StopDistance(float v0, RollSettings s)
        {
            float d = s.Deceleration;
            if (d <= 0f) return float.PositiveInfinity;
            return v0 * v0 / (2f * d);
        }

        /// <summary>Launch speed (m/s) needed to stop exactly at the given distance.</summary>
        public static float LaunchSpeedForDistance(float distance, RollSettings s) =>
            Mathf.Sqrt(2f * Mathf.Max(0f, s.Deceleration) * Mathf.Max(0f, distance));

        /// <summary>Speed after rolling the given distance; NaN if the ball stops before it.</summary>
        public static float SpeedAtDistance(float v0, float distance, RollSettings s)
        {
            float v2 = v0 * v0 - 2f * s.Deceleration * distance;
            return v2 < 0f ? float.NaN : Mathf.Sqrt(v2);
        }

        /// <summary>Time (s) to roll the given distance. Assumes the ball reaches it.</summary>
        public static float TimeToDistance(float v0, float distance, RollSettings s)
        {
            float d = s.Deceleration;
            if (Mathf.Abs(d) < 1e-6f) return v0 > 0f ? distance / v0 : 0f;
            float v = Mathf.Sqrt(Mathf.Max(0f, v0 * v0 - 2f * d * distance));
            return (v0 - v) / d;
        }

        public static RollResult Simulate(LaunchResult launch, Vector2 start, Vector2 hole, RollSettings s)
        {
            Vector2 u = launch.Direction;
            float v0 = u == Vector2.zero ? 0f : launch.Speed;

            float stop = StopDistance(v0, s);
            bool stopped = stop <= s.maxRollDistance;
            float travel = stopped ? stop : s.maxRollDistance;

            Vector2 toHole = hole - start;
            Vector2 right = new Vector2(u.y, -u.x);
            float along = Vector2.Dot(toHole, u);
            float lateral = -Vector2.Dot(toHole, right); // + = ball line passes right of the hole

            float r = s.HoleRadius;
            bool holed = false;
            float speedAtHole = float.NaN;

            if (v0 > 0f && Mathf.Abs(lateral) <= r && along + r >= 0f)
            {
                // Ball centre enters the hole circle here.
                float enter = Mathf.Max(0f, along - Mathf.Sqrt(r * r - lateral * lateral));
                if (enter <= travel)
                {
                    speedAtHole = SpeedAtDistance(v0, enter, s);
                    if (speedAtHole <= s.maxCaptureSpeed)
                    {
                        holed = true;
                        // Roll on to the point nearest the hole centre (or stop earlier), then drop.
                        travel = Mathf.Min(Mathf.Max(along, enter), travel);
                        stopped = true;
                    }
                }
            }
            else if (v0 > 0f && along >= 0f && along <= travel)
            {
                speedAtHole = SpeedAtDistance(v0, along, s);
            }

            var result = new RollResult
            {
                Holed = holed,
                Stopped = stopped,
                Start = start,
                Direction = u,
                Hole = hole,
                Distance = travel,
                Duration = v0 > 0f ? TimeToDistance(v0, travel, s) : 0f,
                LaunchSpeed = v0,
                Deceleration = s.Deceleration,
                SpeedAtHole = speedAtHole,
                LateralOffset = lateral,
                LongShort = holed ? 0f : travel - along,
            };
            result.End = start + u * travel;
            return result;
        }
    }
}
