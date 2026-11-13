// The projectile recipe (a hot head, a trail that thins behind it, motes shed along the way, light riding the
// head) is adapted from Sprite Fusion's open-source energy bolt (https://www.spritefusion.com/pixel-art-vfx/energy),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class IceLance : Effect
    {
        const double D = 0.8;

        public static readonly Module Module = new Module("ice-lance", "pixelfx/fx/ice/ice-lance", 176, 64, 146, 32, D, true, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["length"] = 40,
                ["seed"] = 13,
            },
            (o, e) => new IceLance(o))
        {
            Title = "Ice lance",
            Desc = "A faceted ice spear flying right: a light pulse runs through its core, rings of cold air shear off the tip, frost peels off into a wake and crystals shed behind it.",
            Tags = new[] { "ice", "lance", "spear", "projectile", "directional", "magic", "loop" },
            Bg = "dark",
        };

        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        static readonly double[][] Fletching = { new double[] { -1, 10, 4 }, new double[] { 1, 9, 4 }, new[] { -0.2, 6, 3 } };

        readonly Options o;

        IceLance(Options o) => this.o = o;

        // the spear: a hexagonal shaft seen from the side, row by row (a white top edge, a lit upper facet, a bright
        // ridge, a cool lower facet, a deep keel, a dark rim), a long cutting point, a light pulse running through
        // its ridge to the tip, a glint sliding back along the top
        static void Spear(Painter p, uint[] C, double L, double pulse, double glint)
        {
            const double T = 16, H = 3.5;
            double Half(double x) => x > -T ? H * (-x / T) + 0.3 : x < -L + 3 ? H * (0.7 + 0.3 * ((x + L) / 3)) : H;
            p.Shade(-L - 1, -H - 2, 1, H + 2, (x, y, o) =>
            {
                if (x > 0 || x < -L) return 0;
                double h = Half(x);
                if (Math.Abs(y) > h + 0.5)
                {
                    if (Math.Abs(y) < h + 1.5) o.c = C[1];
                    return 0;
                }
                double v = y / Math.Max(0.5, h);
                if (x > -1.5) return o.c = WHITE;
                // three faces lit from above: a pale top facet, a mid front facet with a bright ridge, a deep underside
                if (v > -0.3 && v < 0.35 && Math.Abs(x - pulse) < 2.5) return o.c = WHITE;
                if (v < -0.3 && Math.Abs(x - glint) < 1) return o.c = WHITE;
                o.c = v < -0.72 ? WHITE : v < -0.36 ? C[6] : v < -0.18 ? C[7] : v < 0.38 ? C[4] : v < 0.78 ? C[2] : C[1];
                return 0;
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double S = o.Seed, L = o.Num("length"), ph = t / D;
            // the wake: a ribbon of frost narrowing behind the spear, with cold light in it
            p.Each(-L - 100, -12, -L + 4, 12, (x, y, sx, sy) =>
            {
                double u = (-L + 4 - x) / 104, w = 4.5 * Px.Pow(1 - u, 0.7) + 0.5 + 1.5 * Px.Sin(u * 9 - ph * TAU * 2) * u, v = Math.Abs(y) / w;
                if (v >= 1) return;
                double a = (1 - v * v) * Px.Pow(1 - u, 1.4);
                if (a < 0.04) return;
                p.BlendPx(sx, sy, v < 0.25 && u < 0.4 ? C[6] : v < 0.6 ? C[5] : C[4], a * 0.7);
                if (a > 0.3) p.Add(sx, sy, C[5], 0.12 * a);
            });
            p.GlowLine(-L - 60, 0, 0, 0, 14, C[4], 0.22);
            // frost peeling off the shaft into soft puffs left behind
            Cycle(t, D, 14, S, g => g.Range(0.35, 0.55), (age, life, r, i) =>
            {
                double x0 = r.Range(-L + 2, -8);
                var (x, y) = Fly(x0, r.Range(-2.5, 2.5), -r.Range(150, 230), r.Sign() * r.Range(8, 30), age, 0, 2.2);
                double k = age / life, rad = 1.2 + 5.5 * k, a = 0.5 * Px.Pow(1 - k, 1.3);
                p.Disc(x, y, rad, k < 0.3 ? C[6] : C[5], (d, g) => a * (1 - d * d));
                p.Glow(x, y, rad * 1.6, C[4], 0.1 * (1 - k));
            });
            // crystals shed along the way: short tumbling shards with a white edge, glinting, falling back
            Cycle(t, D, 9, S + 1, g => g.Range(0.4, 0.6), (age, life, r, i) =>
            {
                var (x, y) = Fly(r.Range(-L, -6), r.Range(-3, 3), -r.Range(170, 260), r.Range(-35, 35), age, 60, 1.2);
                double k = age / life, a = r.Range(0, TAU) + age * r.Range(10, 18) * r.Sign(), l = r.Range(1.2, 2.2), ca = Px.Cos(a), sa = Px.Sin(a);
                if (k > 0.85 && (age * 30) % 2 < 1) return;
                p.Line(x - ca * l, y - sa * l, x + ca * l, y + sa * l, C[5]);
                p.Px(x + ca * l, y + sa * l, WHITE);
                if (Px.Cos(a * 2) > 0.9)
                {
                    foreach (int[] q in Cross) p.Px(x + q[0], y + q[1], C[6]);
                    p.Glow(x, y, 6, C[5], 0.45);
                }
            });
            // speed lines: the air streaming past above and below
            Cycle(t, D, 12, S + 2, g => g.Range(0.16, 0.26), (age, life, r, i) =>
            {
                double y = r.Sign() * r.Range(4, 13), x = r.Range(-20, 0) - age * r.Range(280, 380), l = r.Range(6, 16), k = age / life;
                p.Line(x, y, x + l, y, u => u > 0.7 ? C[6] : C[4], u => u * (1 - k) * 1.4);
            });
            // rings of cold air shearing off the point and sliding back along the shaft: the near arc bright, the far dim
            for (int i = 0; i < 3; i++)
            {
                double q = (ph * 3 + i / 3.0) % 1, x = -4 - q * (L + 20), ry = 4.5 + q * 8, rx = 1.2 + q * 2.6;
                if (q > 0.8) continue;
                for (int k = 0; k < 40; k++)
                {
                    double an = (k / 40.0) * TAU, px = x + Px.Cos(an) * rx, py = Px.Sin(an) * ry;
                    if (Math.Abs(py) < 4.6 && px > -L - 2) continue;
                    p.Px(px, py, Px.Cos(an) > 0 ? (q < 0.35 ? WHITE : C[6]) : q < 0.4 ? C[5] : C[4]);
                }
                p.GlowRing(x, 0, ry * 0.75, 3, C[5], 0.14 * (1 - q));
            }
            // the ice fletching at the back: three shards fanning out behind the shaft
            foreach (double[] f in Fletching)
            {
                double dy = f[0], l = f[1], w = f[2];
                Blade(p, -L + 3, dy * 1.5, -L + 3 - l, dy * (2 + l * 0.5), w, C);
            }
            // the spear itself, a pulse of light running through it, a glint sliding back along the top
            double pulse = -L + ((ph * 2) % 1) * (L + 6), glint = -((ph * 2 + 0.5) % 1) * L * 1.2;
            Spear(p, C, L, pulse, glint);
            p.GlowLine(-L, 0, 0, 0, 10, C[4], 0.1);
            p.Glow(pulse, 0, 8, C[6], 0.3);
            p.Glow(0, 0, 8, WHITE, 0.4);
            p.Glow(-L * 0.4, 0, 30, C[3], 0.16);
        }
    }
}
