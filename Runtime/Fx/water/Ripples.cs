using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Ripples : Effect
    {
        public static readonly Module Module = new Module("ripples", "pixelfx/fx/water/ripples", 136, 52, 68, 30, 2, true, 30,
            new Options { ["palette"] = WATER, ["seed"] = 2 },
            (o, e) => new Ripples(o, e))
        {
            Title = "Ripples",
            Desc = "A drop plinks on still dark water and rings spread out across it at a low angle: the swell of each ring catches the light, crests glint, and they fade into the calm.",
            Tags = new[] { "water", "ripples", "rings", "surface", "pond", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 2, SY = 0.26, EVERY = 0.5, LIFE = 2, RMAX = 60, PATCH = 64;

        // one ring at age a: its radius (fast out, slowing) and strength
        static double Radius(double a) => 2 + (RMAX - 2) * Ease.OutCubic(a / LIFE);
        static double Power(double a) => Px.Pow(1 - a / LIFE, 1.5) * Math.Min(1, a / 0.08);

        // the rings alive at t, newest first: (age, index)
        static List<(double a, double i)> Alive(double t)
        {
            var o = new List<(double, double)>();
            for (int k = 0; k * EVERY < LIFE; k++)
            {
                double a = (((t - k * EVERY) % D) + D) % D;
                if (a < LIFE) o.Add((a, Round((((t - a) % D) + D) % D / EVERY)));
            }
            return o;
        }

        // the water: dark and see-through, shading with the slope of the swell each ring carries
        static void Surface(Painter p, uint[] C, double t)
        {
            var rs = new List<(double R, double k, double w)>();
            foreach (var (a, _) in Alive(t)) rs.Add((Radius(a), Power(a), 3 + a * 2.2));
            for (double Y = Math.Floor(-PATCH * SY); Y < PATCH * SY; Y++)
                for (double X = -PATCH; X < PATCH; X++)
                {
                    double x = X + 0.5, y = Y + 0.5, z = y / SY, r = Hypot(x, z), e = r / PATCH;
                    if (e > 1) continue;
                    double edge = 1 - Smooth(0.6, 1, e);
                    p.Blend(x, y, C[1], 0.55 * edge);
                    double slope = 0;
                    foreach (var (R, k, w) in rs)
                    {
                        double d = r - R;
                        if (d > 2.2 * w || d < -2.2 * w) continue;
                        double q = d / w;
                        slope += k * Exp(-(q * q)) * -Px.Sin(d * 1.15) * 1.15;
                    }
                    if (slope == 0) continue;
                    // the side of a swell facing the light (back and left) catches it; the side away from it goes dark
                    double face = slope * (-(x * -0.55 + z * -0.84) / Math.Max(1, r));
                    if (face > 0.18) p.Blend(x, y, face > 0.6 ? C[5] : C[4], Math.Min(0.9, (face - 0.18) * 2.2) * edge);
                    else if (face < -0.25) p.Blend(x, y, C[0], Math.Min(0.8, (-face - 0.25) * 2) * edge);
                }
        }

        static void Rings(Painter p, uint[] C, double t, double half)
        {
            foreach (var (a, i) in Alive(t))
            {
                double R = Radius(a), k = Power(a);
                Ring(p, 0, 0, R, SY, 0.85 * k, C, n: 2, gap: 3 + a * 1.4, half: half, hot: a < 0.14, glint: half > 0 && a > 0.1 ? k : 0, seed: i + 1, wob: 0.4 + a * 1.1);
            }
        }

        readonly Options o;

        Ripples(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Surface(p, C, t);
            Rings(p, C, t, -1);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double S = o.Seed;
            Rings(p, C, t, 1);
            // each ring starts with a drop: a white fleck, a bead thrown up and back down, a little light
            foreach (var (a, _) in Alive(t))
            {
                if (a < 0.08)
                {
                    p.Px(0, 0, WHITE);
                    p.Px(-1, 0, C[6]);
                    p.Px(1, 0, C[6]);
                    p.Glow(0, 0, 8, C[5], 0.5 * (1 - a / 0.08));
                }
                double b = a - 0.04, h = 90 * b - 0.5 * 700 * b * b;
                if (b > 0 && h > 0)
                {
                    p.Px(0, -h, WHITE);
                    p.Px(0, -h + 1, C[5]);
                }
            }
            // glints ride the near crests, winking
            Cycle(t, D, 10, S + 5, g => g.Range(0.25, 0.45), (age, life, r, _) =>
            {
                var all = Alive(t);
                int idx = (int)Math.Floor(r.Next() * all.Count);
                double a = idx >= 0 && idx < all.Count ? all[idx].a : 0;
                double ang = r.Range(0.3, Math.PI - 0.3), R = Radius(a), k = Px.Sin((Math.PI * age) / life) * Power(a);
                if (k < 0.15 || R < 6) return;
                double x = Px.Cos(ang) * R, y = Px.Sin(ang) * R * SY;
                p.Px(x, y, WHITE);
                if (k > 0.5) foreach (int[] v in Sim.Cross) p.Px(x + v[0], y + v[1], C[6]);
                p.Glow(x, y, 5, C[5], 0.4 * k);
            });
            // a cool light on the water, brightest where the rings start
            p.Glow(0, 0, 40, C[4], 0.14);
            p.Glow(0, 0, 14, C[5], 0.12);
        }
    }
}
