// The shaded, outlined anger mark and the embers are
// adapted from Sprite Fusion's open-source heal and fire (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Rage : Effect
    {
        static readonly Pose SEETHE = new Pose { ["turn"] = 1, ["crouch"] = 4, ["fx"] = -13, ["fy"] = -19, ["bx"] = 13, ["by"] = -19, ["ffx"] = -8, ["fbx"] = 9 };
        static readonly Pose THUD = new Pose(SEETHE) { ["crouch"] = 6, ["fy"] = -23, ["by"] = -23 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, SEETHE), new PoseKey(0.05, THUD, "snap"), new PoseKey(0.17, SEETHE, "snap"), new PoseKey(0.29, new Pose(THUD) { ["crouch"] = 5 }, "snap"),
                new PoseKey(0.4, SEETHE, "snap"), new PoseKey(1.2, SEETHE),
            },
        };

        const double D = 1.2, LUB = 0.05, DUB = 0.29, G = 760;
        static double Pulse(double t, double at, double w = 0.06) => Exp(-Px.Pow((((((t - at) % D) + D + D / 2) % D - D / 2) / w), 2));
        static double Beat(double t) => Pulse(t, LUB) + 0.65 * Pulse(t, DUB);
        // how long after the last lub or dub it is
        static double Since(double t, double at) => (((t - at) % D) + D) % D;
        // the body judders for a moment on each beat
        static bool Shaking(double t) => Since(t, LUB - 0.02) < 0.13 || Since(t, DUB - 0.02) < 0.09;

        public static readonly Module Module = new Module("rage", "pixelfx/fx/status/rage", 80, 80, 40, 70, 1.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0208", "#4a0612", "#8a0c1c", "#c81828", "#ff3040", "#ff7a7a", "#ffc8c8", "#ffffff" },
                ["heat"] = new[] { "#5a0a0a", "#c8281a", "#ff7a2a", "#ffd27a" },
                ["steam"] = "#f2e8ee",
                ["seed"] = 6,
            },
            (o, e) => new Rage(o), act: Act, actor: 2,
            // the stand-in is hidden while the body judders: the effect draws it, shaken
            actorAlpha: (t, o) => Shaking(t) ? 0 : 1)
        {
            Title = "Rage",
            Desc = "Seething: the body judders on every heartbeat, an anger mark throbs at the temple, eyes burn, steam jets off the head, stress lines flash out and the floor cracks hot underfoot.",
            Tags = new[] { "status", "rage", "berserk", "anger", "buff", "loop" },
            Bg = "ground",
        };

        // the anger mark: four curves round a cross-shaped gap, at two sizes for the throb
        static readonly Func<double, double, char>[] MARK =
        {
            Bitmap(new[] { "..#.#..", "..#.#..", "##...##", ".......", "##...##", "..#.#..", "..#.#.." }),
            Bitmap(new[] { "...#.#...", "...#.#...", "..##.##..", "###...###", ".........", "###...###", "..##.##..", "...#.#...", "...#.#..." }),
        };

        // cracks in the floor round the feet, worked out once per seed: jagged runs out from under each foot
        static readonly Dictionary<double, List<double[][]>> CRACKS = new Dictionary<double, List<double[][]>>();

        static List<double[][]> Cracks(Actor a, double seed)
        {
            lock (CRACKS)
            {
                if (CRACKS.TryGetValue(seed, out List<double[][]> got)) return got;
                StatusShape S = Shape(a);
                int[] flr = S.Rows[S.H - 1];
                double fl = flr[0], fr = flr[1];
                var R = new Rng(seed * 31 + 7);
                var out_ = new List<double[][]>();
                foreach (double[] start in new[] { new[] { a.X + fl + 2, -1 }, new[] { a.X + fr - 1, 1 }, new[] { a.X + (fl + fr) / 2, -1 }, new[] { a.X + (fl + fr) / 2 + 1, 1 } })
                {
                    double x = start[0], dir = start[1], y = 0.5;
                    var pts = new List<double[]> { new[] { x, y } };
                    int n = R.Int(5, 9);
                    for (int i = 0; i < n; i++)
                    {
                        x += dir * R.Range(1.2, 2.4);
                        y = Clamp(y + R.Range(-0.9, 0.9), 0, 2.5);
                        pts.Add(new[] { x, y });
                        if (i > 1 && R.Chance(0.3))
                        {
                            double bx = x + dir * R.Range(1, 2);
                            double by = Clamp(y + R.Sign() * R.Range(0.6, 1.4), 0, 3);
                            out_.Add(new[] { new[] { x, y }, new[] { bx, by } });
                        }
                    }
                    out_.Add(pts.ToArray());
                }
                CRACKS[seed] = out_;
                return out_;
            }
        }

        readonly Options o;

        Rage(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), Hh = Fit(o.Ramp("heat"), 4);
            Actor a = o.Actor;
            double b = Beat(t), stamp = Exp(-Since(t, LUB) / 0.18);
            // dark-red heat shimmering up round the body
            Cycle(t, D, 9, o.Seed + 2, r => r.Range(0.7, 1.1), (age, life, r, i) =>
            {
                double u = age / life, x0 = r.Range(-16, 16), y = a.Y + r.Range(10, 40) - age * r.Range(14, 22), len = r.Range(6, 11), ph = r.Range(0, TAU);
                double k = Px.Sin(Math.PI * u) * 0.55;
                for (int d = 0; d < len; d++) p.Dot(x0 - len / 2 + d, y + Round(Px.Sin(d * 0.9 + ph + age * 12) * 0.8), d % 3 != 0 ? C[2] : C[1], k * (1 - Math.Abs(d / len - 0.5) * 1.4));
            });
            // the floor: red light pooling, cracks under the feet glowing hot on each stamp and cooling between
            p.Shade(-30, -4, 30, 8, (x, y, out_) =>
            {
                double d = Hypot(x / 28, (y - 0.5) / 4.5);
                if (d < 1)
                {
                    out_.l = C[3];
                    out_.k = (0.2 + 0.3 * b) * (1 - d) * (1 - d);
                }
                return 0;
            });
            double heat = 0.25 + 0.75 * stamp;
            foreach (double[][] pts in Cracks(a, o.Seed))
                for (int i = 1; i < pts.Length; i++)
                {
                    double x0 = pts[i - 1][0], y0 = pts[i - 1][1], x1 = pts[i][0], y1 = pts[i][1], far = (double)i / pts.Length;
                    p.Line(x0, y0, x1, y1, heat * (1 - far * 0.6) > 0.6 ? Hh[3] : heat * (1 - far * 0.5) > 0.35 ? Hh[2] : Hh[1]);
                    if (i % 2 != 0) p.Glow(x1, y1, 4, Hh[2], 0.25 * heat * (1 - far * 0.5));
                }
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), Hh = Fit(o.Ramp("heat"), 4);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            double b = Beat(t);
            uint steam = o.Color("steam");
            bool shake = Shaking(t);
            int dx = shake ? (Math.Floor(t * 60) % 2 != 0 ? 1 : -1) : 0, dy = shake && Since(t, LUB - 0.02) < 0.06 ? -1 : 0;
            // the body: shaken while the beat lands, the face flushing red with it
            foreach (int[] bp in S.Body)
            {
                int mx = bp[0], my = bp[1];
                uint c = ColorAt(a, mx, my);
                double l = Luma(c);
                bool skin = l > 0.55 && (c & 255) > ((c >> 16) & 255) + 30;
                if (skin) p.Px(a.X + mx + dx, a.Y + my + dy, Mix(c, C[4], 0.22 + 0.3 * b));
                else if (shake) p.Px(a.X + mx + dx, a.Y + my + dy, c);
            }
            // burning eyes under angry brows
            foreach (StatusEye e in Eyes(a))
            {
                int ex = a.X + dx, ey = a.Y + dy, inner = (e.X0 + e.X1) / 2.0 < a.W / 2.0 ? 1 : -1;
                foreach (int[] xy in e.Px) p.Px(ex + xy[0], ey + xy[1], xy[1] == e.Y0 ? C[7] : xy[1] == e.Y0 + 1 ? C[6] : C[4]);
                int xi = inner > 0 ? e.X1 : e.X0, xo = inner > 0 ? e.X0 : e.X1;
                p.Px(ex + xo - inner, ey + e.Y0 - 2, 0xff1f1726);
                p.Px(ex + xo, ey + e.Y0 - 2, 0xff1f1726);
                p.Px(ex + xi, ey + e.Y0 - 1, 0xff1f1726);
                p.Glow(ex + (e.X0 + e.X1 + 1) / 2.0, ey + e.Y0 + 1, 5, C[5], 0.35 + 0.35 * b);
            }
            // the anger mark throbbing at the temple: big on the lub, smaller on the dub
            bool big = Pulse(t, LUB + 0.02, 0.05) > 0.35 || Pulse(t, DUB + 0.02, 0.04) > 0.6;
            double mkx = a.X + a.W * 0.86 + dx, mky = a.Y + 3 + dy;
            Icon(p, mkx, mky, 5, MARK[big ? 1 : 0], new[] { C[0], C[3], C[4], C[5] });
            p.Px(mkx - (big ? 3 : 2), mky - (big ? 3 : 2), C[6]);
            p.Glow(mkx, mky, 8, C[4], 0.25 + 0.4 * b);
            // steam jetting off the head in short bursts after each beat
            foreach (double[] jet in new[] { new[] { LUB + 0.04, 7, 1 }, new[] { DUB + 0.04, 4, 0.75 } })
            {
                double at = jet[0], n = jet[1], power = jet[2];
                foreach (int side in new[] { -1, 1 })
                    for (int i = 0; i < n; i++)
                    {
                        double age = Since(t, at + i * 0.016), life = 0.22 + 0.12 * Rand(i, side, 3);
                        if (age >= life) continue;
                        double u = age / life, ang = -Math.PI / 2 + side * (0.5 + 0.2 * Rand(i, side, 5)), sp = (90 + 40 * Rand(i, side, 7)) * power;
                        double x = side * a.W * 0.36 + Px.Cos(ang) * Travel(sp, 0, 7, age), y = a.Y + 3 + Px.Sin(ang) * Travel(sp, 0, 7, age) - 8 * age;
                        double rad = (1 + 3.2 * Math.Sqrt(u)) * (0.8 + 0.4 * Rand(i, side, 9)), k = Math.Min(1, u * 12) * Px.Pow(1 - u, 2);
                        p.Disc(x, y, rad, steam, 0.7 * k);
                        p.Disc(x - rad * 0.2, y - rad * 0.25, rad * 0.6, 0xffffffff, 0.6 * k);
                        if (u < 0.3) p.Dot(x, y, 0xffffffff, 1 - u / 0.3);
                    }
            }
            // jagged stress lines flashing out round the head on the beat
            foreach (double[] burst in new[] { new double[] { LUB, 8, 7 }, new double[] { DUB, 6, 5 } })
            {
                double at = burst[0], n = burst[1], len = burst[2];
                double age = Since(t, at);
                if (age > 0.2) continue;
                double k = age / 0.2;
                for (int i = 0; i < n; i++)
                {
                    double ang = -Math.PI / 2 + ((i + 0.5) / n - 0.5) * 4.4, r0 = 15 + 6 * k, ca = Px.Cos(ang), sa = Px.Sin(ang), cx = 0, cy = a.Y + 12;
                    var pts = new double[4][];
                    for (int j = 0; j <= 3; j++)
                    {
                        double r = r0 + (j / 3.0) * len, off = j % 2 != 0 ? 1.3 : -0.4;
                        pts[j] = new[] { cx + ca * r - sa * off, cy + sa * r + ca * off };
                    }
                    p.Polyline(pts, u => u > 0.66 ? C[7] : u > 0.33 ? C[5] : C[4], 1.2 - k);
                    p.GlowLine(pts[0][0], pts[0][1], pts[3][0], pts[3][1], 3, C[4], 0.3 * (1 - k));
                }
            }
            // the stamp: embers spat up out of the cracks, grit hopping
            double st = Since(t, LUB);
            for (int i = 0; i < 10; i++)
            {
                double life = 0.5 + 0.5 * Rand(i, 11), age = st - 0.02 * Rand(i, 12);
                if (age < 0 || age > life) continue;
                double x = (Rand(i, 13) - 0.5) * 30 + Px.Sin(age * 9 + i) * 1.5 * age, y = -1 + Travel(-(40 + 50 * Rand(i, 14)), -20, 1.6, age);
                Ember(p, x, y, age / life, new[] { Hh[3], Hh[2], Hh[1] }, age + i);
            }
            for (int i = 0; i < 12; i++)
            {
                double vx = (Rand(i, 21) - 0.5) * 90, vy = -(40 + 70 * Rand(i, 22)), tt = st;
                double x = (Rand(i, 23) - 0.5) * 18 + vx * tt, y = -1 + vy * tt + 0.5 * G * tt * tt;
                if (tt > 0.6 || y > 0) continue;
                p.Px(x, y, i % 3 != 0 ? C[2] : 0xff6a5e72);
            }
            // the beat flares red light round the head and down onto the floor
            p.Glow(0, a.Y + 10, 26, C[4], 0.12 + 0.4 * b);
            p.Glow(0, a.Y + 22, 34, C[3], 0.08 + 0.18 * b);
        }
    }
}
