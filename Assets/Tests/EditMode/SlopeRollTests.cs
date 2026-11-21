using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ARGolf.Putting.Tests
{
    /// <summary>
    /// Sloped greens (stimp 10). Rolling ball on a slope: a = -mu g v̂ - (5/7) g ∇h.
    /// </summary>
    public class SlopeRollTests
    {
        static readonly Vector2 Origin = Vector2.zero;
        static readonly Vector2 Forward = new Vector2(0f, 1f);
        static readonly Vector2 FarAway = new Vector2(50f, 50f);

        RollSettings s;

        [SetUp]
        public void SetUp() => s = new RollSettings();

        RollResult Putt(float speed, Vector2 dir, Vector2 hole, IGreenSurface green) =>
            RollSimulator.Simulate(new LaunchResult(speed, dir, 0.0), Origin, hole, s, green);

        static Vector2 Heading(float degreesRightOfForward)
        {
            float a = degreesRightOfForward * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        [TestCase(2f)]
        [TestCase(-2f)]
        public void StraightUpOrDownhill_StopsWhereTheFormulaSays(float slopePercent)
        {
            var green = PlaneGreen.FromPercent(0f, slopePercent);
            float v0 = 1.48f;
            float decel = s.FrictionDeceleration + RollSettings.RollingGravityFactor * s.gravity * slopePercent / 100f;
            var r = Putt(v0, Forward, FarAway, green);

            Assert.IsTrue(r.Stopped);
            Assert.That(r.Distance, Is.EqualTo(v0 * v0 / (2f * decel)).Within(0.005f));
            Assert.That(r.End.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Uphill_IsShorter_Downhill_IsLonger()
        {
            float flatDistance = Putt(1.48f, Forward, FarAway, PlaneGreen.Flat).Distance;

            Assert.That(Putt(1.48f, Forward, FarAway, PlaneGreen.FromPercent(0f, 2f)).Distance, Is.LessThan(flatDistance));
            Assert.That(Putt(1.48f, Forward, FarAway, PlaneGreen.FromPercent(0f, -2f)).Distance, Is.GreaterThan(flatDistance));
        }

        [Test]
        public void SideSlope_BreaksDownhill()
        {
            // Green falls towards +X (height drops 2 % per metre of X).
            var green = PlaneGreen.FromPercent(-2f, 0f);
            var r = Putt(1.82f, Forward, FarAway, green);

            Assert.That(r.End.x, Is.GreaterThan(0.1f));
        }

        [Test]
        public void SideSlope_BreakIsMirrored()
        {
            var right = Putt(1.82f, Forward, FarAway, PlaneGreen.FromPercent(-2f, 0f));
            var left = Putt(1.82f, Forward, FarAway, PlaneGreen.FromPercent(2f, 0f));

            Assert.That(left.End.x, Is.EqualTo(-right.End.x).Within(1e-4f));
            Assert.That(left.End.y, Is.EqualTo(right.End.y).Within(1e-4f));
        }

        [Test]
        public void SideSlope_MoreBreakOnFasterGreens()
        {
            var green = PlaneGreen.FromPercent(-2f, 0f);
            var slow = Putt(RollSimulator.LaunchSpeedForDistance(3f, s), Forward, FarAway, green);

            var fast = new RollSettings { stimpFeet = 13f };
            var quick = RollSimulator.Simulate(
                new LaunchResult(RollSimulator.LaunchSpeedForDistance(3f, fast), Forward, 0.0), Origin, FarAway, fast, green);

            // Same roll-out distance, but the ball rolls longer on a fast green, so gravity bends it more.
            Assert.That(quick.End.x, Is.GreaterThan(slow.End.x));
        }

        [Test]
        public void SideSlope_HoledPuttsAreAimedUphill()
        {
            // 3 m putt across a 2 % slope falling to the right (+X). Must aim left (uphill) to hole it.
            var green = PlaneGreen.FromPercent(-2f, 0f);
            var hole = new Vector2(0f, 3f);
            var holedAims = new List<float>();

            foreach (float rollOut in new[] { 3.3f, 3.6f, 4.0f })
            {
                float v0 = RollSimulator.LaunchSpeedForDistance(rollOut, s);
                for (float aim = -20f; aim <= 5f; aim += 0.1f)
                    if (Putt(v0, Heading(aim), hole, green).Holed)
                        holedAims.Add(aim);
            }

            Assert.That(holedAims, Is.Not.Empty);
            Assert.That(holedAims, Has.All.LessThan(0f));
            // Aiming straight at the hole misses right (low side).
            var straight = Putt(RollSimulator.LaunchSpeedForDistance(3.6f, s), Forward, hole, green);
            Assert.IsFalse(straight.Holed);
            Assert.That(straight.LateralOffset, Is.GreaterThan(0f));
        }

        [Test]
        public void BallAtRest_StaysOnGentleSlope()
        {
            // (5/7) * 7 % = 5 % < mu (5.6 %): the grass holds the ball.
            var r = Putt(0f, Forward, FarAway, PlaneGreen.FromPercent(0f, 7f));

            Assert.IsTrue(r.Stopped);
            Assert.That(r.Distance, Is.EqualTo(0f));
        }

        [Test]
        public void BallAtRest_RollsOffSteepSlope()
        {
            // (5/7) * 9 % = 6.4 % > mu: the ball starts rolling downhill (towards -Z).
            var r = Putt(0f, Forward, FarAway, PlaneGreen.FromPercent(0f, 9f));

            Assert.That(r.Distance, Is.GreaterThan(1f));
            Assert.That(r.End.y, Is.LessThan(-1f));
        }

        [Test]
        public void MaxHoldingSlope_AtStimp10_IsAbout7Point8Percent()
        {
            Assert.That(s.MaxHoldingSlope, Is.EqualTo(0.078f).Within(0.001f));
        }

        [Test]
        public void UphillPuttThatStopsOnSteepSlope_RollsBack()
        {
            var r = Putt(1.5f, Forward, FarAway, PlaneGreen.FromPercent(0f, 9f));

            Assert.That(r.End.y, Is.LessThan(0f));
        }

        [Test]
        public void VerySteepDownhill_NeverStops_IsCapped()
        {
            var r = Putt(1f, Forward, FarAway, PlaneGreen.FromPercent(0f, -10f));

            Assert.IsFalse(r.Stopped);
            Assert.That(r.Distance, Is.GreaterThanOrEqualTo(s.maxRollDistance));
            Assert.That(r.Duration, Is.LessThan(s.maxRollTime));
        }

        [Test]
        public void FastPuttThatLipsOut_CanStillBreakBackAndStop()
        {
            // Lip-out is not the end: the ball keeps rolling and is judged where it stops.
            var r = Putt(RollSimulator.LaunchSpeedForDistance(5f, s), Forward, new Vector2(0f, 2f), PlaneGreen.Flat);

            Assert.IsFalse(r.Holed);
            Assert.IsTrue(r.Stopped);
            Assert.That(r.SpeedAtHole, Is.GreaterThan(s.maxCaptureSpeed));
        }
    }
}
