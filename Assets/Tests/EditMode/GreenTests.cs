using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ARGolf.Putting.Tests
{
    public class GreenTests
    {
        // ---------- Height field ----------

        [Test]
        public void HeightField_OfAPlane_GivesExactHeightAndSlope()
        {
            var plane = PlaneGreen.FromPercent(2f, -1f);
            var field = HeightField.Sample(new Vector2(-5f, -5f), new Vector2(5f, 5f), 0.1f, plane.HeightAt);

            foreach (var p in new[] { new Vector2(0.03f, 0.07f), new Vector2(-2.31f, 3.9f), new Vector2(4.2f, -4.44f) })
            {
                Assert.That(field.HeightAt(p), Is.EqualTo(plane.HeightAt(p)).Within(1e-5f));
                Assert.That((field.SlopeAt(p) - plane.Slope).magnitude, Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void HeightField_InterpolatesBetweenGridPoints()
        {
            var field = new HeightField(Vector2.zero, 1f, 2, 2, new[] { 0f, 1f, 2f, 3f });

            Assert.That(field.HeightAt(new Vector2(0.5f, 0.5f)), Is.EqualTo(1.5f).Within(1e-6f));
            Assert.That(field.HeightAt(new Vector2(1f, 0f)), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(field.HeightAt(new Vector2(5f, 5f)), Is.EqualTo(3f).Within(1e-6f), "outside: edge continues flat");
        }

        // ---------- Features ----------

        [Test]
        public void Tier_HasRiseAndSteepestSlopeAsDocumented()
        {
            var tier = new GreenTier { at = Vector2.zero, upDirection = Vector2.up, rise = 0.2f, width = 2f };

            Assert.That(tier.HeightAt(new Vector2(0f, 5f)) - tier.HeightAt(new Vector2(0f, -5f)), Is.EqualTo(0.2f).Within(1e-5f));
            float steepest = (tier.HeightAt(new Vector2(0f, 0.01f)) - tier.HeightAt(new Vector2(0f, -0.01f))) / 0.02f;
            Assert.That(steepest, Is.EqualTo(1.5f * 0.2f / 2f).Within(1e-3f));
        }

        [Test]
        public void Crown_IsHighestInTheMiddleAndFallsTowardsTheEdge()
        {
            var crown = new GreenCrown { center = Vector2.zero, radii = new Vector2(10f, 10f), height = 0.5f };

            Assert.That(crown.HeightAt(Vector2.zero), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(crown.HeightAt(new Vector2(10f, 0f)), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(crown.HeightAt(new Vector2(12f, 0f)), Is.LessThan(0f));
        }

        [Test]
        public void Outline_ContainsCentre_EdgePointsAreOnTheEdge()
        {
            var o = new GreenOutline { center = new Vector2(1f, 2f), halfSize = new Vector2(6f, 9f), rotation = 25f };
            o.wobbles.Add(new OutlineWobble(3, 0.05f, 1f));

            Assert.IsTrue(o.Contains(o.center));
            Assert.IsFalse(o.Contains(o.center + new Vector2(20f, 0f)));
            for (int k = 0; k < 12; k++)
            {
                float phi = k * 30f * Mathf.Deg2Rad;
                Assert.That(o.DistanceOutside(o.EdgePoint(phi)), Is.EqualTo(0f).Within(1e-3f));
                Assert.That(o.DistanceOutside(o.EdgePoint(phi, 0.5f)), Is.EqualTo(0.5f).Within(1e-3f));
            }
        }

        // ---------- Baked green ----------

        [Test]
        public void GreenModel_LowestPointOnTheGreenIsZero_AndMatchesTheFeatures()
        {
            var d = FamousGreens.Find("funnel");
            var model = GreenModel.Build(d);

            float lowest = float.MaxValue, offset = float.NaN;
            for (int j = 0; j < model.Heights.CountZ; j += 3)
                for (int i = 0; i < model.Heights.CountX; i += 3)
                {
                    Vector2 p = model.Heights.PointAt(i, j);
                    if (!d.outline.Contains(p)) continue;
                    lowest = Mathf.Min(lowest, model.HeightAt(p));
                    float o = d.FeatureHeightAt(p) - model.HeightAt(p);
                    if (float.IsNaN(offset)) offset = o;
                    Assert.That(o, Is.EqualTo(offset).Within(1e-4f));
                }
            Assert.That(lowest, Is.GreaterThanOrEqualTo(-1e-5f));
            Assert.That(lowest, Is.LessThan(0.01f));
        }

        [Test]
        public void GreenModel_FrictionIsHigherOffTheGreen()
        {
            var d = FamousGreens.Find("practice");
            var model = GreenModel.Build(d);
            Vector2 edge = d.outline.EdgePoint(0f);
            Vector2 outward = (edge - d.outline.center).normalized;

            Assert.That(model.FrictionScaleAt(d.outline.center), Is.EqualTo(1f));
            Assert.That(model.FrictionScaleAt(edge + outward * 0.3f), Is.EqualTo(d.stimpFeet / d.fringeStimpFeet).Within(1e-5f));
            Assert.That(model.FrictionScaleAt(edge + outward * 3f), Is.EqualTo(d.stimpFeet / d.roughStimpFeet).Within(1e-5f));
        }

        // Flat round green, radius 5 m, fringe 0.9 m, stimp 10 / fringe 5 / rough 1.5.
        static GreenModel FlatRoundGreen() =>
            GreenModel.Build(new GreenDefinition { outline = new GreenOutline { halfSize = new Vector2(5f, 5f) } });

        [Test]
        public void Friction_ChangesSmoothlyAtTheGrassEdges()
        {
            var model = FlatRoundGreen();
            float last = model.FrictionScaleAt(Vector2.zero), maxStep = 0f;
            for (float x = 4.5f; x < 7f; x += 0.001f)
            {
                float f = model.FrictionScaleAt(new Vector2(x, 0f));
                maxStep = Mathf.Max(maxStep, Mathf.Abs(f - last));
                last = f;
            }
            Assert.That(maxStep, Is.LessThan(0.15f), "no sudden jump in friction");
            Assert.That(model.FrictionScaleAt(new Vector2(5.45f, 0f)), Is.EqualTo(2f).Within(1e-4f), "fringe = 10 / 5");
            Assert.That(model.FrictionScaleAt(new Vector2(7f, 0f)), Is.EqualTo(10f / 1.5f).Within(1e-3f), "rough = 10 / 1.5");
        }

        [Test]
        public void BallLeavingTheGreenAt1MetrePerSecond_StopsInTheFringe()
        {
            var model = FlatRoundGreen();
            var s = model.MakeRollSettings();
            // Arrive at the edge (5 m) at 1 m/s: v0² = 1 + 2 mu g 5.
            float v0 = Mathf.Sqrt(1f + 2f * s.FrictionDeceleration * 5f);
            var r = RollSimulator.Simulate(new LaunchResult(v0, Vector2.right, 0.0), Vector2.zero, new Vector2(0f, -30f), s, model);

            Assert.That(model.GrassAt(r.End), Is.EqualTo(GrassKind.Fringe));
            // On the fringe (friction x2) 1 m/s stops in 1 / (2 · 2 mu g) ≈ 0.45 m; on the green it would be 0.91 m.
            Assert.That(r.End.x - 5f, Is.EqualTo(1f / (4f * s.FrictionDeceleration)).Within(0.05f));
        }

        [Test]
        public void BallEnteringTheRough_StopsWithinAFewCentimetres()
        {
            var model = FlatRoundGreen();
            var s = model.MakeRollSettings();
            // Arrive at the rough (5.9 m) at 1 m/s: 1 + 2 mu g (5 + 2 · 0.9).
            float v0 = Mathf.Sqrt(1f + 2f * s.FrictionDeceleration * (5f + 2f * 0.9f));
            var r = RollSimulator.Simulate(new LaunchResult(v0, Vector2.right, 0.0), Vector2.zero, new Vector2(0f, -30f), s, model);

            Assert.That(model.GrassAt(r.End), Is.EqualTo(GrassKind.Rough));
            Assert.That(r.End.x - 5.9f, Is.LessThan(0.2f), "rough friction x6.7 stops a 1 m/s ball in ~14 cm");
        }

        [Test]
        public void BallRunningOffTheTurtleback_StopsSoonInTheCollectionArea()
        {
            var d = FamousGreens.Find("turtleback");
            var model = GreenModel.Build(d);
            var s = model.MakeRollSettings();
            Vector2 start = new Vector2(0f, 1f);
            var r = RollSimulator.Simulate(new LaunchResult(3f, Vector2.right, 0.0), start, new Vector2(-30f, -30f), s, model);

            Assert.IsTrue(r.Stopped);
            Assert.That(model.GrassAt(r.End), Is.Not.EqualTo(GrassKind.Green), "a firm putt off the dome runs off the green");
            Assert.That(r.Distance, Is.LessThan(25f));
        }

        // ---------- Placing a green in the world ----------

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(-120f)]
        public void PlacedGreen_SlopeMatchesItsHeights(float yaw)
        {
            var placed = new PlacedGreen(GreenModel.Build(FamousGreens.Find("funnel")), new Vector2(3f, -2f), yaw, 0.5f);
            Vector2 p = placed.ToWorld(new Vector2(2f, 3f));
            const float d = 0.05f;
            var numeric = new Vector2(
                (placed.HeightAt(p + new Vector2(d, 0f)) - placed.HeightAt(p - new Vector2(d, 0f))) / (2f * d),
                (placed.HeightAt(p + new Vector2(0f, d)) - placed.HeightAt(p - new Vector2(0f, d))) / (2f * d));

            Assert.That((placed.SlopeAt(p) - numeric).magnitude, Is.LessThan(0.002f));
            Assert.That((placed.ToLocal(placed.ToWorld(new Vector2(1f, 2f))) - new Vector2(1f, 2f)).magnitude, Is.LessThan(1e-5f));
        }

#if UNITY_EDITOR
        [Test]
        public void GreenMathRotate_MatchesUnityYRotation()
        {
            var v = new Vector2(1.5f, -0.4f);
            Vector3 unity = Quaternion.Euler(0f, 33f, 0f) * new Vector3(v.x, 0f, v.y);
            Vector2 ours = GreenMath.Rotate(v, 33f);

            Assert.That(ours.x, Is.EqualTo(unity.x).Within(1e-5f));
            Assert.That(ours.y, Is.EqualTo(unity.z).Within(1e-5f));
        }
#endif

        // ---------- Ideal putt ----------

        [Test]
        public void Solver_OnFlatGreen_AimsStraight_AndArrivesGently()
        {
            var s = new RollSettings();
            var sol = PuttSolver.Solve(Vector2.zero, new Vector2(0f, 3f), s, PlaneGreen.Flat);

            Assert.IsTrue(sol.Found);
            Assert.IsTrue(sol.Result.Holed);
            Assert.That(sol.AimOffsetDegrees(Vector2.zero, new Vector2(0f, 3f)), Is.EqualTo(0f).Within(0.3f));
            Assert.That(sol.Speed, Is.EqualTo(RollSimulator.LaunchSpeedForDistance(3.4f, s)).Within(0.05f));
        }

        [Test]
        public void Solver_OnSideSlope_AimsUphill()
        {
            var s = new RollSettings();
            var hole = new Vector2(0f, 3f);
            var sol = PuttSolver.Solve(Vector2.zero, hole, s, PlaneGreen.FromPercent(-2f, 0f));

            Assert.IsTrue(sol.Found);
            Assert.That(sol.AimOffsetDegrees(Vector2.zero, hole), Is.LessThan(-2f), "green falls to the right: aim left");
        }

        // ---------- Built-in greens ----------

        static IEnumerable<GreenDefinition> Greens() => FamousGreens.All;

        [Test]
        public void Greens_HaveUniqueIds()
        {
            var ids = new HashSet<string>();
            foreach (var g in FamousGreens.All)
                Assert.IsTrue(ids.Add(g.id), $"duplicate id {g.id}");
        }

        [TestCaseSource(nameof(Greens))]
        public void Green_PinsAreOnTheGreen_OnSlopesThatHoldABall(GreenDefinition d)
        {
            var model = GreenModel.Build(d);
            float holding = model.MakeRollSettings().MaxHoldingSlope;
            Assert.That(d.pins, Is.Not.Empty);

            foreach (var pin in d.pins)
            {
                Assert.That(d.outline.DistanceOutside(pin.position), Is.LessThan(-1f), $"{d.id}/{pin.name}: at least 1 m inside the edge");
                Assert.That(model.MaxSlopeNear(pin.position, 0.3f), Is.LessThan(0.75f * holding),
                    $"{d.id}/{pin.name}: hole location too steep");
            }
        }

        [TestCaseSource(nameof(Greens))]
        public void Green_PuttsStartOnTheGreen_AndCanBeHoled(GreenDefinition d)
        {
            var model = GreenModel.Build(d);
            var s = model.MakeRollSettings();
            Assert.That(d.putts, Is.Not.Empty);

            foreach (var putt in d.putts)
            {
                Assert.That(putt.pin, Is.InRange(0, d.pins.Count - 1), $"{d.id}/{putt.name}: pin index");
                Vector2 hole = model.PinPosition(putt.pin);
                Assert.That(d.outline.DistanceOutside(putt.ball), Is.LessThan(-0.3f), $"{d.id}/{putt.name}: ball on the green");
                Assert.That(model.SlopeAt(putt.ball).magnitude, Is.LessThan(0.9f * s.MaxHoldingSlope),
                    $"{d.id}/{putt.name}: ball would roll away before the putt");

                var m = Regex.Match(putt.name, @"(\d+(?:\.\d+)?) m\b");
                Assert.IsTrue(m.Success, $"{d.id}/{putt.name}: name should say the distance, e.g. \"3 m\"");
                float named = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                float actual = (hole - putt.ball).magnitude;
                Assert.That(actual, Is.EqualTo(named).Within(Mathf.Max(0.6f, 0.1f * named)), $"{d.id}/{putt.name}: distance");

                var sol = PuttSolver.Solve(putt.ball, hole, s, model);
                Assert.IsTrue(sol.Found, $"{d.id}/{putt.name}: no holed putt found");
            }
        }
    }
}
