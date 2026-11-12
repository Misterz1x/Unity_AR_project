using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// The one interface between "how the ball was hit" and the rest of the app.
    /// Every input method (finger swipe, club tracking, ball tracking) produces this,
    /// so the simulator and UI never depend on how the hit was measured. Keep it stable.
    /// </summary>
    public readonly struct LaunchResult
    {
        /// <summary>Launch speed in m/s.</summary>
        public readonly float Speed;

        /// <summary>Unit direction on the floor plane: x = world X, y = world Z.</summary>
        public readonly Vector2 Direction;

        /// <summary>Time of the hit in seconds (Time.timeAsDouble or camera frame time).</summary>
        public readonly double Timestamp;

        public LaunchResult(float speed, Vector2 direction, double timestamp)
        {
            Speed = Mathf.Max(0f, speed);
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
            Timestamp = timestamp;
        }

        public override string ToString() =>
            $"Launch {Speed:F2} m/s, dir ({Direction.x:F3}, {Direction.y:F3}), t={Timestamp:F3}";
    }
}
