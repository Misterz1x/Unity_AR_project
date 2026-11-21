using System.Collections.Generic;
using UnityEngine;

namespace ARGolf.Putting
{
    /// <summary>
    /// Built-in greens. The famous ones are "inspired by" approximations made from public descriptions,
    /// not surveys of the real greens; keep course logos out of the app.
    /// Green-local metres: x = right, y = away from the player's usual approach.
    /// </summary>
    public static class FamousGreens
    {
        static List<GreenDefinition> all;

        public static IReadOnlyList<GreenDefinition> All => all ??= new List<GreenDefinition>
        {
            Practice(),
            IslandGreen(),
            AmphitheatreGreen(),
            Turtleback(),
            LightningGreen(),
            DoubleGreen(),
        };

        public static GreenDefinition Find(string id)
        {
            foreach (var g in All)
                if (g.id == id) return g;
            return null;
        }

        static GreenOutline Oval(float halfWidth, float halfDepth, float rotation = 0f, params OutlineWobble[] wobbles)
        {
            var o = new GreenOutline { halfSize = new Vector2(halfWidth, halfDepth), rotation = rotation };
            o.wobbles.AddRange(wobbles);
            return o;
        }

        static GreenDefinition Practice()
        {
            var g = new GreenDefinition
            {
                id = "practice",
                name = "Practice Green",
                description = "Gentle practice green: 1 % uphill to the back and a soft bump. Learn the speed here.",
                stimpFeet = 10f,
                outline = Oval(6f, 7.5f, 0f, new OutlineWobble(2, 0.05f, 0.6f), new OutlineWobble(3, 0.03f, 2.1f)),
                tiltPercent = new Vector2(0f, 1f),
            };
            g.mounds.Add(new GreenMound { center = new Vector2(-2.5f, 1f), radius = 2.5f, height = 0.05f });
            g.pins.Add(new GreenPin("Centre", new Vector2(0.5f, 0.5f)));
            g.pins.Add(new GreenPin("Back left", new Vector2(-2.5f, 4.5f)));
            g.pins.Add(new GreenPin("Front right", new Vector2(2.5f, -4f)));
            g.putts.Add(new GreenPutt("Straight uphill, 3 m", new Vector2(0.5f, -2.5f), 0));
            g.putts.Add(new GreenPutt("Over the bump, 4.5 m", new Vector2(-3.5f, -1.5f), 0));
            g.putts.Add(new GreenPutt("Long downhill, 8 m", new Vector2(1f, 4f), 2));
            g.putts.Add(new GreenPutt("Across, 5 m", new Vector2(2.5f, 3f), 1));
            return g;
        }

        // Inspired by the island green of the 17th at TPC Sawgrass: small, fairly gentle, water all around.
        static GreenDefinition IslandGreen()
        {
            var g = new GreenDefinition
            {
                id = "island",
                name = "Island Green",
                description = "Inspired by the island green at TPC Sawgrass (17th). Small target, gentle back-to-front " +
                              "slope, small step to the back right. Miss the green and it is in the water.",
                stimpFeet = 12f,
                fringeWidth = 0.6f,
                waterAround = true,
                outline = Oval(8.5f, 13f, -8f, new OutlineWobble(2, 0.04f, 1.2f), new OutlineWobble(3, 0.03f, 0.3f)),
                tiltPercent = new Vector2(0f, 1.2f),
            };
            g.tiers.Add(new GreenTier { at = new Vector2(2.5f, 5f), upDirection = new Vector2(0.6f, 1f), rise = 0.12f, width = 3f });
            g.mounds.Add(new GreenMound { center = new Vector2(-4f, -6f), radius = 2.5f, height = 0.06f });
            g.pins.Add(new GreenPin("Front", new Vector2(-1f, -8f)));
            g.pins.Add(new GreenPin("Middle", new Vector2(0f, 0f)));
            g.pins.Add(new GreenPin("Back right", new Vector2(3.5f, 7.5f)));
            g.putts.Add(new GreenPutt("Short and firm, 3 m", new Vector2(0.5f, -3f), 1));
            g.putts.Add(new GreenPutt("Up the step, 7 m", new Vector2(-1f, 2f), 2));
            g.putts.Add(new GreenPutt("Downhill to the front, 10 m", new Vector2(2f, 1.5f), 0));
            return g;
        }

        // Inspired by the 16th green at Augusta National: strong right-to-left slope, ridge through the middle,
        // putts from the upper right funnel down to a back-left hollow.
        static GreenDefinition AmphitheatreGreen()
        {
            var g = new GreenDefinition
            {
                id = "funnel",
                name = "Funnel Green",
                description = "Inspired by the 16th green at Augusta National. Strong right-to-left slope towards the " +
                              "water and a ridge through the middle: putts from the upper right curl down to the back left.",
                stimpFeet = 13f,
                outline = Oval(11f, 15f, -15f, new OutlineWobble(2, 0.06f, 0.4f), new OutlineWobble(3, 0.04f, 1.9f)),
                tiltPercent = new Vector2(2.5f, 0.3f),
            };
            g.ridges.Add(new GreenRidge { a = new Vector2(-3f, -12f), b = new Vector2(4f, 11f), width = 4f, height = 0.1f });
            g.mounds.Add(new GreenMound { center = new Vector2(-6f, 8f), radius = 4f, height = -0.1f });
            g.pins.Add(new GreenPin("Back left hollow", new Vector2(-6f, 8f)));
            g.pins.Add(new GreenPin("Front right", new Vector2(5f, -8f)));
            g.pins.Add(new GreenPin("Upper right", new Vector2(6f, 2f)));
            g.putts.Add(new GreenPutt("Down the slope, 13 m", new Vector2(6f, 4f), 0));
            g.putts.Add(new GreenPutt("Sidehill, 5 m", new Vector2(-1f, 6f), 0));
            g.putts.Add(new GreenPutt("Uphill over the ridge, 14 m", new Vector2(-5f, -6f), 2));
            g.putts.Add(new GreenPutt("Short downhill slider, 3 m", new Vector2(6f, -5f), 1));
            return g;
        }

        // Inspired by the domed greens at Pinehurst No. 2: crowned in the middle, falling off at every edge
        // into short-mown run-offs.
        static GreenDefinition Turtleback()
        {
            var g = new GreenDefinition
            {
                id = "turtleback",
                name = "Turtleback Green",
                description = "Inspired by the domed greens of Pinehurst No. 2. High in the middle and falling away at " +
                              "every edge: too firm and the ball runs off into the collection area.",
                stimpFeet = 11.5f,
                fringeStimpFeet = 9f,
                fringeWidth = 2.5f,
                outline = Oval(10f, 13f, 5f, new OutlineWobble(2, 0.04f, 2.4f), new OutlineWobble(4, 0.02f, 0.8f)),
            };
            g.crowns.Add(new GreenCrown { center = new Vector2(0f, 1f), radii = new Vector2(12f, 15f), rotation = 5f, height = 0.5f });
            g.pins.Add(new GreenPin("Top of the dome", new Vector2(0f, 1f)));
            g.pins.Add(new GreenPin("Right shoulder", new Vector2(5f, 3f)));
            g.pins.Add(new GreenPin("Front left", new Vector2(-4f, -6f)));
            g.putts.Add(new GreenPutt("Up onto the dome, 6 m", new Vector2(-2f, -5f), 0));
            g.putts.Add(new GreenPutt("Over the top, 9 m", new Vector2(-4f, 6f), 1));
            g.putts.Add(new GreenPutt("Downhill off the dome, 8 m", new Vector2(0f, 1f), 2));
            return g;
        }

        // Inspired by Oakmont: among the fastest greens in golf, sloping away from the player.
        static GreenDefinition LightningGreen()
        {
            var g = new GreenDefinition
            {
                id = "lightning",
                name = "Lightning Green",
                description = "Inspired by Oakmont, known for some of the fastest greens in golf. Stimp 14 and sloping " +
                              "away from you: a downhill putt barely needs a touch.",
                stimpFeet = 14f,
                outline = Oval(12f, 16f, 10f, new OutlineWobble(2, 0.05f, 2.8f), new OutlineWobble(3, 0.04f, 0.2f)),
                tiltPercent = new Vector2(0.4f, -1.8f),
            };
            g.ridges.Add(new GreenRidge { a = new Vector2(-6f, -4f), b = new Vector2(8f, 6f), width = 3f, height = -0.07f });
            g.mounds.Add(new GreenMound { center = new Vector2(4f, -8f), radius = 3f, height = 0.08f });
            g.pins.Add(new GreenPin("Back middle", new Vector2(0f, 9f)));
            g.pins.Add(new GreenPin("Left", new Vector2(-6f, 1f)));
            g.pins.Add(new GreenPin("Front right", new Vector2(5f, -5f)));
            g.putts.Add(new GreenPutt("Downhill, 5 m", new Vector2(0f, 4f), 0));
            g.putts.Add(new GreenPutt("Across the swale, 10 m", new Vector2(1f, -6f), 1));
            g.putts.Add(new GreenPutt("Uphill, 11 m", new Vector2(-2f, 4f), 2));
            return g;
        }

        // Inspired by the huge shared greens of the Old Course at St Andrews (a part of one).
        static GreenDefinition DoubleGreen()
        {
            var g = new GreenDefinition
            {
                id = "double",
                name = "Double Green",
                description = "Inspired by the huge double greens of the Old Course, St Andrews (a part of one). " +
                              "Two holes share one rolling green: very long putts over humps and hollows.",
                stimpFeet = 9.5f,
                outline = Oval(14f, 22f, 12f, new OutlineWobble(2, 0.07f, 0.9f), new OutlineWobble(3, 0.05f, 2.6f),
                    new OutlineWobble(5, 0.02f, 1.3f)),
                tiltPercent = new Vector2(0.5f, 0.6f),
            };
            g.mounds.Add(new GreenMound { center = new Vector2(-6f, -9f), radius = 5f, height = 0.16f });
            g.mounds.Add(new GreenMound { center = new Vector2(6f, 5f), radius = 6f, height = 0.14f });
            g.mounds.Add(new GreenMound { center = new Vector2(-3f, 11f), radius = 4f, height = -0.12f });
            g.ridges.Add(new GreenRidge { a = new Vector2(-10f, 1f), b = new Vector2(9f, -4f), width = 5f, height = 0.08f });
            g.pins.Add(new GreenPin("Front hole", new Vector2(2f, -14f)));
            g.pins.Add(new GreenPin("Back hole", new Vector2(-3f, 14f)));
            g.pins.Add(new GreenPin("Middle", new Vector2(-1f, 4f)));
            g.putts.Add(new GreenPutt("Across the whole green, 26 m", new Vector2(2f, -12f), 1));
            g.putts.Add(new GreenPutt("Over the ridge, 11 m", new Vector2(4f, -6f), 2));
            g.putts.Add(new GreenPutt("Through the hollow, 7 m", new Vector2(-5f, 7f), 1));
            return g;
        }
    }
}
