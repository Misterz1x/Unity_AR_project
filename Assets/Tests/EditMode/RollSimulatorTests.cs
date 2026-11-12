using NUnit.Framework;
using UnityEngine;

namespace ARGolf.Putting.Tests
{
    /// <summary>
    /// Checks the roll model against the reference table (flat green, stimp 10 ft):
    ///   1 m -> 1.05 m/s, 2 m -> 1.48 m/s, 3 m -> 1.82 m/s, 5 m -> 2.34 m/s.
    /// </summary>
    public class RollSimulatorTests
    {
        static readonly Vector2 Origin = Vector2.zero;
        static readonly Vector2 Forward = new Vector2(0f, 1f);

        RollSettings flat;

        [SetUp]
        public void SetUp() => flat = new RollSettings();

        static RollResult Putt(float speed, Vector2 dir, Vector2 hole, RollSettings s) =>
            RollSimulator.Simulate(new LaunchResult(speed, dir, 0.0), Origin, hole, s);

        [Test]
        public void Mu_AtStimp10_IsAbout0056()
        {
            Assert.That(flat.Mu, Is.EqualTo(0.056f).Within(0.0005f));
        }

        [TestCase(1f, 1.05f)]
        [TestCase(2f, 1.48f)]
        [TestCase(3f, 1.82f)]
        [TestCase(5f, 2.34f)]
        public void LaunchSpeedForDistance_MatchesReferenceTable(float distance, float expectedSpeed)
        {
            Assert.That(RollSimulator.LaunchSpeedForDistance(distance, flat), Is.EqualTo(expectedSpeed).Within(0.01f));
        }

        [TestCase(1.05f, 1f)]
        [TestCase(1.48f, 2f)]
        [TestCase(1.82f, 3f)]
        [TestCase(2.34f, 5f)]
        public void StopDistance_MatchesReferenceTable(float speed, float expectedDistance)
        {
            Assert.That(RollSimulator.StopDistance(speed, flat), Is.EqualTo(expectedDistance).Within(0.03f));
        }

        [TestCase(1f, 0.035f)]
        [TestCase(2f, 0.049f)]
        [TestCase(3f, 0.061f)]
        [TestCase(5f, 0.078f)]
        public void TravelPerFrameAt30Fps_MatchesReferenceTable(float distance, float expectedPerFrame)
        {
            float v0 = RollSimulator.LaunchSpeedForDistance(distance, flat);
            Assert.That(v0 / 30f, Is.EqualTo(expectedPerFrame).Within(0.001f));
        }

        [Test]
        public void StraightPutt_WithGentleSpeed_IsHoled()
        {
            // Speed to roll 2.5 m, hole at 2 m: arrives at ~0.74 m/s.
            float v0 = RollSimulator.LaunchSpeedForDistance(2.5f, flat);
            var r = Putt(v0, Forward, new Vector2(0f, 2f), flat);

            Assert.IsTrue(r.Holed);
            Assert.That(r.SpeedAtHole, Is.LessThanOrEqualTo(flat.maxCaptureSpeed));
            Assert.That(r.End.y, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void StraightPutt_ThatJustReachesTheHoleEdge_IsHoled()
        {
            float v0 = RollSimulator.LaunchSpeedForDistance(1.98f, flat);
            var r = Putt(v0, Forward, new Vector2(0f, 2f), flat);

            Assert.IsTrue(r.Holed);
        }

        [Test]
        public void StraightPutt_TooFast_LipsOutAndIsLong()
        {
            // Would roll 3 m past: ~1.8 m/s at the hole.
            float v0 = RollSimulator.LaunchSpeedForDistance(5f, flat);
            var r = Putt(v0, Forward, new Vector2(0f, 2f), flat);

            Assert.IsFalse(r.Holed);
            Assert.That(r.SpeedAtHole, Is.GreaterThan(flat.maxCaptureSpeed));
            Assert.That(r.LongShort, Is.EqualTo(3f).Within(0.03f));
        }

        [Test]
        public void StraightPutt_TooSoft_IsShort()
        {
            float v0 = RollSimulator.LaunchSpeedForDistance(1.5f, flat);
            var r = Putt(v0, Forward, new Vector2(0f, 2f), flat);

            Assert.IsFalse(r.Holed);
            Assert.IsNaN(r.SpeedAtHole);
            Assert.That(r.LongShort, Is.EqualTo(-0.5f).Within(0.01f));
        }

        [Test]
        public void PuttAimedRight_MissesRight()
        {
            // 2° right over 2 m is ~7 cm sideways, more than the 5.4 cm hole radius.
            float a = 2f * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            float v0 = RollSimulator.LaunchSpeedForDistance(2.3f, flat);
            var r = Putt(v0, dir, new Vector2(0f, 2f), flat);

            Assert.IsFalse(r.Holed);
            Assert.That(r.LateralOffset, Is.EqualTo(2f * Mathf.Sin(a)).Within(0.001f));
            Assert.That(r.LateralOffset, Is.GreaterThan(0f));
        }

        [Test]
        public void PuttAimed1DegreeOff_StillDropsAtShortRange()
        {
            // 1° over 2 m is ~3.5 cm sideways: inside the hole radius.
            float a = 1f * Mathf.Deg2Rad;
            var dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            float v0 = RollSimulator.LaunchSpeedForDistance(2.3f, flat);
            var r = Putt(v0, dir, new Vector2(0f, 2f), flat);

            Assert.IsTrue(r.Holed);
        }

        [Test]
        public void Uphill_StopsShorterThanFlat()
        {
            var uphill = new RollSettings { slopeDegrees = 2f };
            float v0 = 1.48f;
            float expected = v0 * v0 / (2f * uphill.gravity * (uphill.Mu + Mathf.Sin(2f * Mathf.Deg2Rad)));

            Assert.That(RollSimulator.StopDistance(v0, uphill), Is.EqualTo(expected).Within(1e-4f));
            Assert.That(RollSimulator.StopDistance(v0, uphill), Is.LessThan(RollSimulator.StopDistance(v0, flat)));
        }

        [Test]
        public void SteepDownhill_NeverStops_IsCappedAtMaxRollDistance()
        {
            var downhill = new RollSettings { slopeDegrees = -5f };
            var r = Putt(1f, Forward, new Vector2(10f, 10f), downhill);

            Assert.IsFalse(r.Stopped);
            Assert.That(r.Distance, Is.EqualTo(downhill.maxRollDistance));
            Assert.That(r.Duration, Is.GreaterThan(0f));
        }

        [Test]
        public void PositionAt_EndOfRoll_EqualsEnd()
        {
            var r = Putt(1.48f, Forward, new Vector2(1f, 1f), flat);

            Assert.That((r.PositionAt(r.Duration) - r.End).magnitude, Is.LessThan(1e-3f));
            Assert.That((r.PositionAt(r.Duration + 5f) - r.End).magnitude, Is.LessThan(1e-3f));
            Assert.That((r.PositionAt(0f) - r.Start).magnitude, Is.LessThan(1e-6f));
        }

        [Test]
        public void ZeroSpeed_DoesNotMove()
        {
            var r = Putt(0f, Forward, new Vector2(0f, 2f), flat);

            Assert.IsFalse(r.Holed);
            Assert.That(r.Distance, Is.EqualTo(0f));
            Assert.That(r.Duration, Is.EqualTo(0f));
        }
    }
}
