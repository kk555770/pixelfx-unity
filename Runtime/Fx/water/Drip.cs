// The bead swelling at the tip and the falling drop's tail follow Sprite Fusion's open-source blood
// (https://www.spritefusion.com/pixel-art-vfx/blood), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Drip : Effect
    {
        public static readonly Module Module = new Module("drip", "pixelfx/fx/water/drip", 72, 88, 36, 78, 1.6, true, 30,
            new Options { ["palette"] = WATER, ["height"] = 62, ["source"] = true, ["seed"] = 1 },
            (o, e) => new Drip(o, e))
        {
            Title = "Drip",
            Desc = "A bead swells under a wet stone tip and catches the light, pinches off, falls stretched by its speed and plinks into a puddle with a crown, a ring and a tiny rebound.",
            Tags = new[] { "water", "drip", "drop", "puddle", "cave", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 1.6, SY = 0.28, G = 760, LET = 0.78, PRX = 20;
        const double REBOUND = 0.06, BACK = 0.2;
        static readonly uint[] C_WET = Ramp("#5d6a86", "#9fb6d6");

        // the stone the water gathers on: a lump of ceiling rock tapering to a dripstone point
        static void Dripstone(Painter p, double H)
        {
            uint[] ST = Rock();
            double top = -H - 14;
            p.Shade(-24, top, 22, -H + 0.5, (x, y, @out) =>
            {
                // a ragged underside of ceiling rock that hangs down into a short dripstone, its point at x = 0
                double under = top + 7 + Px.Sin(x * 0.45) * 1.2 + Px.Sin(x * 1.3 + 1) * 0.6 - Math.Abs(x + 4) * 0.12, cone = -H - Math.Abs(x) * 1.25;
                double bottom = Math.Max(under, Math.Min(cone, -H)), span = x < 0 ? 24 : 22;
                if (y > bottom || Math.Abs(x) > span - Math.Max(0, top + 3 - y) * 4) return 0;
                bool rim = y > bottom - 1 || Math.Abs(x) > span - 1;
                double lx = (x + 24) / 46, v = (y - top) / 14;
                @out.c = rim ? ST[0] : v < 0.25 ? ST[1] : y > under && Math.Abs(x) < 6 ? (x < 0 ? ST[4] : ST[2]) : lx < 0.3 ? ST[4] : lx < 0.62 ? ST[3] : ST[2];
                if (!rim && Math.Abs(x - 9 - (y - top) * 0.4) < 0.5 && v > 0.3 && v < 0.7) @out.c = ST[0];
                return 0;
            });
            // a wet streak down to the point, catching the light
            for (double y = -H - 7; y < -H; y++) p.Px(-1, y, y > -H - 3 ? C_WET[1] : C_WET[0]);
            p.Px(-1, -H - 2, WHITE);
        }

        // the puddle: dark see-through water with a lit far edge, the drop's light in it
        static void Puddle(Painter p, uint[] C)
        {
            double ry = PRX * SY;
            for (double Y = Math.Floor(-ry); Y < ry; Y++)
                for (double X = -PRX; X < PRX; X++)
                {
                    double x = X + 0.5, y = Y + 0.5, qx = x / PRX, qy = y / ry, d = qx * qx + qy * qy;
                    if (d > 1) continue;
                    p.Blend(x, y, C[1], 0.72 * Math.Min(1, (1 - d) * 5));
                }
            // the far edge catches the light in one fine line
            for (double X = -PRX + 3; X < PRX - 3; X++)
            {
                double x = X + 0.5, qx = x / PRX, y = -Math.Sqrt(1 - qx * qx) * ry;
                p.Dot(x, y + 0.5, Math.Abs(x) < PRX * 0.5 ? C[4] : C[3], 0.95);
            }
        }

        static double LandAt(double H) => LET + Math.Sqrt((2 * H) / G);

        readonly Options o;

        Drip(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Puddle(p, C);
            double u = t - LandAt(o.Num("height"));
            if (u >= 0) Ring(p, 0, 0, 2 + 17 * Ease.OutCubic(u / 0.75), SY, 1.1 * (1 - u / 0.75), C, n: 2, gap: 2.5, half: -1, seed: 1, wob: 0.4);
            double v = t - LandAt(o.Num("height")) - REBOUND - BACK;
            if (v >= 0) Ring(p, 0, 0, 1.5 + 9 * Ease.OutCubic(v / 0.5), SY, 0.9 * (1 - v / 0.5), C, n: 1, half: -1, seed: 2);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double H = o.Num("height"), land = LandAt(H);
            if (o.Bool("source")) Dripstone(p, H);
            // the bead swells at the tip, wobbling, and catches the light; its neck stretches before it lets go
            if (t < LET)
            {
                double k = t / LET, r = 0.7 + 2.3 * Ease.InQuad(k), wob = 0.15 * Px.Sin(t * 26) * k, neck = Smooth(0.82, 1, k) * 2.5;
                double cy = -H + r + neck;
                if (neck > 0.2) p.Line(0, -H, 0, cy - r, C[5]);
                p.Ellipse(0, cy, r * (1 - wob), r * (1 + wob) + 0.3 * k, (d, a) => d > 0.72 ? (Px.Cos(a) < 0 && Px.Sin(a) < 0.3 ? C[5] : C[3]) : d < 0.35 && Px.Cos(a) > 0 ? C[6] : C[4]);
                if (r > 1) p.Px(-r * 0.4, cy - r * 0.45, WHITE);
                p.Glow(0, cy, 4 + r * 2, C[5], 0.14 + 0.14 * k);
            }
            else
            {
                // a little of it springs back up the tip after it lets go
                double s = (t - LET) / 0.15;
                if (s < 1) p.Ellipse(0, -H + 0.8, 1.2 * (1 - s * 0.6), 1 + Px.Sin(s * Math.PI) * 0.8, C[4]);
            }
            // the fall, stretched by its speed
            double f = t - LET;
            if (f > 0 && t < land)
            {
                double y = -H + 3 + 0.5 * G * f * f, vy = G * f;
                Bead(p, 0, y, 0, vy, 3, C);
                p.Glow(0, y, 6, C[5], 0.2);
            }
            // the plink: a white fleck, a little crown thrown up, the near half of the ring, light
            double u = t - land;
            if (u >= 0)
            {
                Ring(p, 0, 0, 2 + 17 * Ease.OutCubic(u / 0.75), SY, 1.1 * (1 - u / 0.75), C, n: 2, gap: 2.5, half: 1, hot: u < 0.08, seed: 1, wob: 0.4, glint: u < 0.5 ? 1 - u * 2 : 0);
                if (u < 0.06)
                {
                    p.Px(0, 0, WHITE);
                    p.Px(-1, 0, C[6]);
                    p.Px(1, 0, C[6]);
                    p.Glow(0, -1, 10, C[5], 0.5 * (1 - u / 0.06));
                }
                // the crown: three flecks thrown out and back
                foreach (double[] fv in new[] { new double[] { -26, 70 }, new double[] { 24, 64 }, new double[] { -6, 90 } })
                {
                    double vx = fv[0], vh = fv[1], h = vh * u - 0.5 * G * u * u;
                    if (h > 0 && u < 0.3) p.Px(vx * u, -h, u < 0.1 ? WHITE : C[6]);
                }
                // the rebound: a bead pops up out of the middle and drops back
                double r = u - REBOUND, hb = 52 * r - 0.5 * G * 0.5 * r * r * 2;
                if (r > 0 && hb > 0)
                {
                    p.Line(0, -1, 0, -hb + 1, C[5]);
                    p.Circle(0, -hb, 1.1, C[6]);
                    p.Px(0, -hb - 1, WHITE);
                }
                double v = u - REBOUND - BACK;
                if (v >= 0) Ring(p, 0, 0, 1.5 + 9 * Ease.OutCubic(v / 0.5), SY, 0.9 * (1 - v / 0.5), C, n: 1, half: 1, seed: 2, hot: v < 0.06);
                p.Glow(0, 0, 18, C[4], 0.22 * (1 - Clamp(u / 0.6)));
            }
            // the tip and the drop shimmer in the puddle
            double sh = Px.Sin(t * 20) > 0 ? 0 : 1;
            p.Px(-1 + sh, 2, C[4]);
            if (f > 0 && t < land)
            {
                double ry = 2 + 0.5 * G * f * f * 0.04;
                p.Px(sh, Math.Min(4, ry), C[5]);
            }
            // the wet light under the tip and in the puddle
            p.Glow(0, -H + 2, 8, C[5], 0.16);
            p.Glow(0, 0, 26, C[3], 0.14);
        }
    }
}
