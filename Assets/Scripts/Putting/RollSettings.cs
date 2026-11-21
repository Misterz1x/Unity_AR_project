using System;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// Tunable numbers for the roll model. Units: metres, seconds, m/s.
    /// The slope comes from the green (IGreenSurface), not from here.
    /// </summary>
    [Serializable]
    public class RollSettings
    {
        /// <summary>Ball speed when it leaves a Stimpmeter ramp (m/s).</summary>
        public const float StimpReleaseSpeed = 1.83f;

        public const float FeetToMetres = 0.3048f;

        /// <summary>
        /// Share of the downhill pull a rolling ball feels. A solid ball rolling without slipping
        /// puts 2/7 of the pull into spinning up, so only 5/7 speeds it up along the slope.
        /// </summary>
        public const float RollingGravityFactor = 5f / 7f;

        [Tooltip("Green speed: Stimpmeter reading in feet. 10 = medium-fast green, 13+ = tournament.")]
        [Range(5f, 15f)]
        public float stimpFeet = 10f;

        [Tooltip("Hole diameter in metres (regulation 108 mm).")]
        public float holeDiameter = 0.108f;

        [Tooltip("Ball diameter in metres (regulation 42.7 mm).")]
        public float ballDiameter = 0.0427f;

        [Tooltip("Fastest speed (m/s) at which the ball still drops. 1.0 = would roll ~3 ft past at stimp 10.")]
        public float maxCaptureSpeed = 1.0f;

        [Tooltip("Physics time step (s). 1/240 moves a 2 m/s ball 8 mm per step.")]
        public float timeStep = 1f / 240f;

        [Tooltip("Safety cap on roll distance (m), for slopes where the ball never stops.")]
        public float maxRollDistance = 30f;

        [Tooltip("Safety cap on roll time (s).")]
        public float maxRollTime = 60f;

        [Tooltip("Gravity in m/s².")]
        public float gravity = 9.81f;

        public float StimpMetres => stimpFeet * FeetToMetres;

        /// <summary>Rolling friction coefficient: mu = vs² / (2 g ds).</summary>
        public float Mu => StimpReleaseSpeed * StimpReleaseSpeed / (2f * gravity * StimpMetres);

        /// <summary>Slowing from the grass alone (m/s²): mu g.</summary>
        public float FrictionDeceleration => Mu * gravity;

        /// <summary>
        /// Steepest slope (dimensionless, 0.078 = 7.8 %) on which a ball at rest stays still: (5/7) slope ≤ mu.
        /// </summary>
        public float MaxHoldingSlope => Mu / RollingGravityFactor;

        public float HoleRadius => holeDiameter * 0.5f;
        public float BallRadius => ballDiameter * 0.5f;
    }
}
