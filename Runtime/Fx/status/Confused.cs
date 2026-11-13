// The shaded, outlined question marks are
// adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Confused : Effect
    {
        static readonly Pose SHRUG = new Pose { ["turn"] = 1, ["fx"] = -13, ["fy"] = -26, ["bx"] = 14, ["by"] = -26, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, new Pose(SHRUG) { ["x"] = 0, ["lean"] = -2 }),
                new PoseKey(0.6, new Pose(SHRUG) { ["x"] = 1, ["lean"] = 1, ["crouch"] = 1, ["fy"] = -23, ["by"] = -29, ["fbx"] = 5, ["fby"] = -6 }),
                new PoseKey(1.2, new Pose(SHRUG) { ["x"] = 2, ["lean"] = 2 }),
                new PoseKey(1.8, new Pose(SHRUG) { ["x"] = 1, ["lean"] = -1, ["crouch"] = 1, ["fy"] = -29, ["by"] = -23, ["ffx"] = -4, ["ffy"] = -6 }),
                new PoseKey(2.4, new Pose(SHRUG) { ["x"] = 0, ["lean"] = -2 }),
            },
        };

        public static readonly Module Module = new Module("confused", "pixelfx/fx/status/confused", 60, 78, 30, 70, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1e0f2e", "#4b2070", "#8a3ab0", "#c66ae0", "#f0a8ff", "#ffffff" },
                ["marks"] = new[] { "#2a1608", "#b0701a", "#ffd84a", "#fff4b8" },
                ["seed"] = 6,
            },
            (o, e) => new Confused(o), act: Act, actor: 2)
        {
            Title = "Confused",
            Desc = "Head in a spin: a glowing spiral whirls over the head, question marks pop up round it and bob, the eyes dart left and right.",
            Tags = new[] { "status", "confused", "confusion", "dizzy", "debuff", "loop" },
            Bg = "ground",
        };

        const double D = 2.4;

        static readonly Func<double, double, char>[] QS = Array.ConvertAll(new[]
        {
            new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            new[] { ".####.", "##..##", "....##", "...##.", "..##..", "..##..", "......", "..##..", "..##.." },
            new[] { ".#####.", "##...##", "##...##", ".....##", "....##.", "...##..", "...##..", ".......", "...##..", "...##.." },
        }, rows => Bitmap(rows));

        // three marks round the spiral: where they sit, when they pop, how big they get
        static readonly double[][] MARKS = { new double[] { -12, -5, 0, 2 }, new double[] { 12, -8, 0.8, 1 }, new double[] { 1, -17, 1.6, 2 } };

        // the spiral, seen from the front: an arm winding out from its heart, spinning
        static void Spiral(Painter p, uint[] C, double cx, double cy, double t)
        {
            double rot = (t / D) * TAU * 2, turns = 1.8, breathe = 1 + 0.08 * Px.Sin((t / D) * TAU * 4);
            int n = 72;
            (double, double) At(double u)
            {
                double th = u * turns * TAU - rot, r = (0.8 + 6.6 * u) * breathe;
                return (cx + Px.Cos(th) * r, cy + Px.Sin(th) * r * 0.9);
            }
            // a dark underlay on the outer arm so it reads against anything, then the arm brightening as it winds out
            for (int i = 1; i <= n; i++)
            {
                double u = (double)i / n;
                var (x0, y0) = At((double)(i - 1) / n);
                var (x1, y1) = At(u);
                if (u > 0.45) p.Line(x0 + 0.5, y0 + 0.7, x1 + 0.5, y1 + 0.7, C[1]);
            }
            for (int i = 1; i <= n; i++)
            {
                double u = (double)i / n;
                var (x0, y0) = At((double)(i - 1) / n);
                var (x1, y1) = At(u);
                p.Line(x0, y0, x1, y1, u > 0.88 ? C[5] : u > 0.62 ? C[4] : u > 0.3 ? C[3] : C[2]);
                if (i % 6 == 0) p.Light(x1, y1, C[3], 0.4 * u);
            }
            var (hx, hy) = At(1);
            Sim.Sparkle(p, hx, hy, 2, C[4], C[3], 0.5);
            p.Glow(cx, cy, 13, C[3], 0.28);
        }

        readonly Options o;

        Confused(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 6);
            p.Glow(0, o.Actor.Y - 7, 16, C[3], 0.22);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 6), M = Fit(o.Ramp("marks"), 4);
            Actor a = o.Actor;
            // the eyes dart from side to side
            int look = Math.Floor((t / D) * 6) % 2 != 0 ? 1 : -1;
            foreach (StatusEye e in Eyes(a))
            {
                foreach (int[] xy in e.Px) p.Px(a.X + xy[0], a.Y + xy[1], 0xff17121f);
                p.Px(a.X + (look < 0 ? e.X0 : e.X1), a.Y + e.Y0, 0xffffffff);
            }
            Spiral(p, C, 0, a.Y - 7, t);
            // the question marks pop up, bob, and shrink away
            foreach (double[] m in MARKS)
            {
                double mx = m[0], my = m[1], t0 = m[2], mbig = m[3];
                double age = (((t - t0) % D) + D) % D, life = 1.9;
                if (age > life) continue;
                double u = age / life, pop = Ease.OutBack(Clamp(age / 0.16), 3), out_ = Clamp((u - 0.82) / 0.18);
                double size = Math.Max(0, Round(mbig * pop * (1 - out_) + (pop > 1.1 ? 1 : 0)));
                double bob = Px.Sin(age * 5 + t0 * 3) * 1.6, tilt = Px.Sin(age * 3.3 + t0) * 0.8;
                if (pop < 0.3 || (out_ > 0 && size == 0 && out_ > 0.6)) continue;
                double x = mx + tilt, y = a.Y + my + bob - age * 2;
                // it pops in with a burst of short rays and a flash
                if (age < 0.2)
                {
                    double k = age / 0.2;
                    for (int j = 0; j < 8; j++)
                    {
                        double an = (j / 8.0) * TAU + 0.2, d0 = 4 + 7 * k, d1 = d0 + 3 * (1 - k);
                        p.Line(x + Px.Cos(an) * d0, y + Px.Sin(an) * d0, x + Px.Cos(an) * d1, y + Px.Sin(an) * d1, j % 2 != 0 ? M[2] : M[3], 1 - k);
                    }
                    p.Glow(x, y, 16, M[2], 0.45 * (1 - k));
                }
                Icon(p, x, y, 7, QS[(int)Math.Min(2, size)], new[] { M[0], M[1], M[2], M[3] });
                p.Glow(x, y, 10 + size * 2, M[2], 0.28 * (1 - out_));
            }
            // glitter flicking off the spiral
            Cycle(t, D, 10, o.Seed + 2, r => r.Range(0.35, 0.6), (age, life, r, i) =>
            {
                double th = r.Range(0, TAU), u = age / life, d = 6 + age * r.Range(12, 22);
                double x = Px.Cos(th) * d, y = a.Y - 7 + Px.Sin(th) * d * 0.8;
                if (Px.Sin(Math.PI * u) > 0.6 && r.Next() < 0.5) Sim.Sparkle(p, x, y, 1, C[4], C[3], 0.3);
                else p.Dot(x, y, u < 0.5 ? C[5] : C[4], 1.2 - u);
            });
        }
    }
}
