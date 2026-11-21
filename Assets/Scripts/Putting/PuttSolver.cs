using UnityEngine;

namespace ARGolf.Putting
{
    public readonly struct PuttSolution
    {
        public readonly bool Found;
        /// <summary>Launch direction (unit, floor plane).</summary>
        public readonly Vector2 Direction;
        /// <summary>Launch speed (m/s).</summary>
        public readonly float Speed;
        /// <summary>Simulated roll of this putt (its Path is the ideal line).</summary>
        public readonly RollResult Result;

        public PuttSolution(Vector2 direction, float speed, RollResult result)
        {
            Found = true;
            Direction = direction;
            Speed = speed;
            Result = result;
        }

        /// <summary>Aim angle relative to the straight line to the hole (degrees, + = right).</summary>
        public float AimOffsetDegrees(Vector2 ball, Vector2 hole)
        {
            Vector2 line = (hole - ball).normalized;
            float cross = line.x * Direction.y - line.y * Direction.x; // + = Direction is left of line
            return -Mathf.Atan2(cross, Vector2.Dot(line, Direction)) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Finds the "ideal" putt: the one that drops with a chosen speed at the hole
    /// (by default the speed that would roll ~40 cm past on a flat green).
    /// Nested bisection: for a speed, find the aim whose path passes through the hole centre;
    /// then adjust the speed until the ball arrives at the target speed.
    /// </summary>
    public static class PuttSolver
    {
        const int SpeedSteps = 18, AimSteps = 16;
        const float MaxAimDegrees = 60f, MaxLaunchSpeed = 8f;

        public static PuttSolution Solve(Vector2 ball, Vector2 hole, RollSettings s, IGreenSurface surface,
            float rollPastMetres = 0.4f)
        {
            surface ??= PlaneGreen.Flat;
            float distance = (hole - ball).magnitude;
            if (distance < s.HoleRadius)
                return default;

            float target = Mathf.Min(Mathf.Sqrt(2f * s.FrictionDeceleration * rollPastMetres), 0.9f * s.maxCaptureSpeed);
            Vector2 line = (hole - ball) / distance;

            float vLo = 0.02f, vHi = MaxLaunchSpeed;
            RollResult best = null;
            Vector2 bestDir = default;
            float bestSpeed = 0f, bestError = float.MaxValue;

            for (int i = 0; i < SpeedSteps; i++)
            {
                float v = 0.5f * (vLo + vHi);
                RollResult r = AimThroughHole(ball, hole, line, v, s, surface, out Vector2 dir);

                bool tooSlow;
                if (r.Holed)
                {
                    float error = Mathf.Abs(r.SpeedAtHole - target);
                    if (error < bestError)
                    {
                        bestError = error;
                        best = r;
                        bestDir = dir;
                        bestSpeed = v;
                    }
                    tooSlow = r.SpeedAtHole < target;
                }
                else
                {
                    // The aim search steers the ball through the hole centre, so a ball that is not holed
                    // either arrived too fast (lip-out) or never got there (too slow; SpeedAtHole small or NaN).
                    tooSlow = !(r.LippedOut || r.SpeedAtHole > s.maxCaptureSpeed);
                }

                if (tooSlow) vLo = v; else vHi = v;
            }

            return best != null ? new PuttSolution(bestDir, bestSpeed, best) : default;
        }

        // For launch speed v, bisect the aim so the path passes through the hole centre.
        static RollResult AimThroughHole(Vector2 ball, Vector2 hole, Vector2 line, float v, RollSettings s,
            IGreenSurface surface, out Vector2 dir)
        {
            Vector2 right = new Vector2(line.y, -line.x);
            float lo = -MaxAimDegrees, hi = MaxAimDegrees;
            RollResult r = null;
            dir = line;

            for (int i = 0; i < AimSteps; i++)
            {
                float aim = 0.5f * (lo + hi);
                dir = GreenMath.Rotate(line, aim);
                r = RollSimulator.Simulate(new LaunchResult(v, dir, 0.0), ball, hole, s, surface);
                if (r.Holed)
                    return r;
                float side = Vector2.Dot(r.ClosestPoint - hole, right);
                if (side > 0f) hi = aim; else lo = aim;
            }
            return r;
        }
    }
}
