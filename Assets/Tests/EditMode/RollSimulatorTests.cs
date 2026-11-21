using NUnit.Framework;
using UnityEngine;

namespace ARGolf.Putting.Tests
{
    /// <summary>
    /// Flat green: checks the roll model against the reference table (stimp 10 ft):
    ///   1 m -> 1.05 m/s, 2 m -> 1.48 m/s, 3 m -> 1.82 m/s, 5 m -> 2.34 m/s.
    /// </summary>
    public class RollSimulatorTests
    {
        static readonly Vector2 Origin = Vector2.zero;
        static readonly Vector2 Forward = new Vector2(0f, 1f);

        RollSettings flat;

        [SetUp]
        public void SetUp() => flat = new RollSettings();

        static RollResult Putt(float speed, Vector2 dir, Vector2 hole, RollSettings s, IGreenSurface green = null) =>
            RollSimulator.Simulate(new LaunchResult(speed, dir, 0.0), Origin, hole, s, green);

        static Vector2 Heading(float degreesRightOfForward)
        {
            float a = degreesRightOfForward * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

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
        public void SimulatedStopDistance_MatchesReferenceTable(float speed, float expectedDistance)
        {
            var r = Putt(speed, Forward, new Vector2(10f, 0f), flat);

            Assert.IsTrue(r.Stopped);
            Assert.That(r.Distance, Is.EqualTo(expectedDistance).Within(0.03f));
            Assert.That(r.Distance, Is.EqualTo(RollSimulator.StopDistance(speed, flat)).Within(0.002f));
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
            // Speed to roll 2.5 m, hole at 2 m: arrives at ~0.78 m/s.
            float v0 = RollSimulator.LaunchSpeedForDistance(2.5f, flat);
            var r = Putt(v0, Forward, new Vector2(0f, 2f), flat);

            Assert.IsTrue(r.Holed);
            Assert.That(r.SpeedAtHole, Is.LessThanOrEqualTo(flat.maxCaptureSpeed));
            Assert.That((r.End - new Vector2(0f, 2f)).magnitude, Is.LessThan(0.001f));
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
            Assert.That(r.LongShort, Is.EqualTo(3f).Within(0.01f));
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
            // 2° right passes ~7 cm from the hole centre, more than the 5.4 cm hole radius.
            float v0 = RollSimulator.LaunchSpeedForDistance(2.3f, flat);
            var r = Putt(v0, Heading(2f), new Vector2(0f, 2f), flat);

            Assert.IsFalse(r.Holed);
            Assert.That(r.ClosestApproach, Is.EqualTo(2f * Mathf.Sin(2f * Mathf.Deg2Rad)).Within(0.001f));
            Assert.That(r.LateralOffset, Is.EqualTo(2.3f * Mathf.Sin(2f * Mathf.Deg2Rad)).Within(0.002f));
            Assert.That(r.SpeedAtHole, Is.GreaterThan(0f));
        }

        [Test]
        public void PuttAimed1DegreeOff_StillDropsAtShortRange()
        {
            // 1° over 2 m is ~3.5 cm sideways: inside the hole radius.
            float v0 = RollSimulator.LaunchSpeedForDistance(2.3f, flat);
            var r = Putt(v0, Heading(-1f), new Vector2(0f, 2f), flat);

            Assert.IsTrue(r.Holed);
        }

        [Test]
        public void PositionAt_FollowsThePath()
        {
            var r = Putt(1.48f, Forward, new Vector2(1f, 1f), flat);

            Assert.That((r.PositionAt(0f) - r.Start).magnitude, Is.LessThan(1e-6f));
            Assert.That((r.PositionAt(r.Duration) - r.End).magnitude, Is.LessThan(1e-4f));
            Assert.That((r.PositionAt(r.Duration + 5f) - r.End).magnitude, Is.LessThan(1e-4f));

            // Halfway in time on a flat green: s = v0 t - mu g t² / 2.
            float t = r.Duration * 0.5f;
            float expected = 1.48f * t - 0.5f * flat.FrictionDeceleration * t * t;
            Assert.That(r.PositionAt(t).y, Is.EqualTo(expected).Within(0.002f));
        }

        [Test]
        public void ZeroSpeed_DoesNotMove()
        {
            var r = Putt(0f, Forward, new Vector2(0f, 2f), flat);

            Assert.IsFalse(r.Holed);
            Assert.IsTrue(r.Stopped);
            Assert.That(r.Distance, Is.EqualTo(0f));
            Assert.That(r.Duration, Is.EqualTo(0f));
        }
    }
}
