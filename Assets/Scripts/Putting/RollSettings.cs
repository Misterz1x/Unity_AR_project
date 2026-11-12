using System;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// Tunable numbers for the roll model. Units: metres, seconds, m/s, degrees.
    /// </summary>
    [Serializable]
    public class RollSettings
    {
        /// <summary>Ball speed when it leaves a Stimpmeter ramp (m/s).</summary>
        public const float StimpReleaseSpeed = 1.83f;

        public const float FeetToMetres = 0.3048f;

        [Tooltip("Green speed: Stimpmeter reading in feet. 10 = medium-fast green.")]
        [Range(5f, 15f)]
        public float stimpFeet = 10f;

        [Tooltip("Slope along the roll direction in degrees. Positive = uphill.")]
        [Range(-5f, 5f)]
        public float slopeDegrees = 0f;

        [Tooltip("Hole diameter in metres (regulation 108 mm).")]
        public float holeDiameter = 0.108f;

        [Tooltip("Ball diameter in metres (regulation 42.7 mm).")]
        public float ballDiameter = 0.0427f;

        [Tooltip("Fastest speed (m/s) at which the ball still drops. 1.0 = would roll ~3 ft past at stimp 10.")]
        public float maxCaptureSpeed = 1.0f;

        [Tooltip("Safety cap on roll distance (m), for downhill slopes where the ball never stops.")]
        public float maxRollDistance = 30f;

        [Tooltip("Gravity in m/s².")]
        public float gravity = 9.81f;

        public float StimpMetres => stimpFeet * FeetToMetres;

        /// <summary>Rolling friction coefficient: mu = vs² / (2 g ds).</summary>
        public float Mu => StimpReleaseSpeed * StimpReleaseSpeed / (2f * gravity * StimpMetres);

        /// <summary>Deceleration along the roll (m/s²): mu g + g sin(theta). Can be ≤ 0 on steep downhill.</summary>
        public float Deceleration => gravity * (Mu + Mathf.Sin(slopeDegrees * Mathf.Deg2Rad));

        public float HoleRadius => holeDiameter * 0.5f;
        public float BallRadius => ballDiameter * 0.5f;
    }
}
