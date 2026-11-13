// The streaks falling in (a dim body, a bright head) follow Sprite Fusion's open-source rain
// (https://www.spritefusion.com/pixel-art-vfx/rain), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class RainSplash : Effect
    {
        public static readonly Module Module = new Module("rain-splash", "pixelfx/fx/water/rain-splash", 176, 80, 88, 50, 2.4, true, 30,
            new Options { ["palette"] = WATER, ["seed"] = 9 },
            (o, e) => new RainSplash(o, e))
        {
            Title = "Rain splash",
            Desc = "A low close-up on one puddle in the rain: big drops land in crowns that throw beads and a rebound jet, rings spread and interfere, a lamp's reflection wobbles on the water.",
            Tags = new[] { "water", "rain", "splash", "puddle", "close-up", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, SY = 0.3, RX = 92, G = 640, LAMP = 34;
        const int N = 8;

        sealed class Big
        {
            public double x, z, t0, H, J, spin, a, R, k;
            public int i;
        }

        // the big drops: where each lands on the puddle (x across, z into it), when, how tall its crown stands
        static Big[] Drops(double S)
        {
            var list = new Big[N];
            for (int i = 0; i < N; i++)
                list[i] = new Big
                {
                    x = (Rand(i, S) - 0.5) * 120, z = (Rand(i, S + 1) - 0.5) * 50, t0 = ((i + 0.25 * Rand(i, S + 2)) / N) * D,
                    H = 10 + 4 * Rand(i, S + 3), J = 7 + 4 * Rand(i, S + 4), spin = Rand(i, S + 5) * TAU, i = i,
                };
            return list;
        }

        static double AgeOf(double t, double t0) => (((t - t0) % D) + D) % D;
        // a ring's radius and strength at age a
        static double RingR(double a) => 3 + 34 * Px.Pow(a / 1.8, 0.7);
        static double RingK(double a) => a < 1.8 ? 1.3 * Px.Pow(1 - a / 1.8, 1.3) * Math.Min(1, a / 0.06) : 0;
        // the crown at age a: how high its walls stand and how wide its foot is
        static double CrownH(double a, double H) => H * Px.Pow(Px.Sin(Math.PI * Clamp(a / 0.3)), 0.8) * (a < 0.3 ? 1 : 0);
        static double CrownR(double a) => 1.5 + 4.5 * Math.Sqrt(Clamp(a / 0.3));
        // the rebound jet: up and back down after the crown has fallen
        static double JetH(double a, double J) => a > 0.26 && a < 0.56 ? J * Px.Sin((Math.PI * (a - 0.26)) / 0.3) : 0;

        // the water's height at a point: every ring's swell summed, so where two cross they add and cancel
        static double Field(double x, double z, List<Big> list)
        {
            double h = 0;
            foreach (Big q in list)
            {
                double dx = x - q.x;
                if (dx > q.R + 10 || dx < -q.R - 10) continue;
                double d = Hypot(dx, z - q.z) - q.R, w = 3.6 + q.a * 3;
                if (d > 2 * w || d < -3 * w) continue;
                double e = d / w;
                h += q.k * Px.Cos(d * 1.1) * Exp(-(e * e));
            }
            return h;
        }

        readonly Options o;

        RainSplash(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double S = o.Seed, ry = RX * SY, ph = t / D;
            var list = new List<Big>();
            foreach (Big q0 in Drops(S))
            {
                q0.a = AgeOf(t, q0.t0);
                if (q0.a < 1.8)
                {
                    q0.R = RingR(q0.a);
                    q0.k = RingK(q0.a);
                    list.Add(q0);
                }
            }
            for (double Y = Math.Floor(-ry); Y < ry; Y++)
            {
                double y = Y + 0.5, z = y / SY, far = (y + ry) / (2 * ry);
                var act = list.FindAll(q => Math.Abs(z - q.z) <= q.R + 12);
                // the lamp's reflection on this row: a dash of light, its width shimmering, pushed about by the swell
                double dashW = (1.5 + far * 4) * (0.35 + 0.65 * (0.5 + 0.5 * Px.Sin(Y * 1.73 + ph * TAU * 3 + Px.Sin(Y * 0.61) * 3)));
                double hPrev = 0;
                for (double X = -88; X < 88; X++)
                {
                    double x = X + 0.5, qx = x / RX, qy = y / ry, d = qx * qx + qy * qy;
                    if (d > 1) continue;
                    double edge = Math.Min(1, (1 - d) * 6);
                    // the puddle: dark see-through water, the far part holding a little of the sky
                    p.Blend(x, y, C[0], (0.62 + 0.2 * far) * edge);
                    if (far < 0.45) p.Blend(x, y, C[2], 0.3 * (1 - far / 0.45) * edge);
                    // its swell: every ring summed, so crests catch the light, troughs go dark, and crossings beat
                    double h = act.Count > 0 ? Field(x, z, act) : 0, hx = h - hPrev;
                    hPrev = h;
                    if (h > 0.24) p.Blend(x, y, h > 0.75 ? C[5] : C[4], Math.Min(0.95, (h - 0.24) * 2.2) * edge);
                    else if (h < -0.4) p.Blend(x, y, C[0], Math.Min(0.85, (-h - 0.4) * 1.8) * edge);
                    if (x < LAMP - 14 || x > LAMP + 14) continue;
                    double lx = Math.Abs(x - LAMP - hx * 9 - Px.Sin(y * 0.8 + ph * TAU * 2) * 1.4);
                    if (lx < dashW)
                    {
                        double k = (1 - lx / dashW) * edge;
                        p.Px(X, Y, k > 0.72 ? WHITE : k > 0.35 ? C[6] : C[5]);
                        p.Light(x, y, C[5], 0.2 * k);
                    }
                }
            }
            // the far rim of the puddle catches the light
            for (double X = -82; X < 82; X++)
            {
                double x = X + 0.5, qx = x / RX, y = -Math.Sqrt(Math.Max(0, 1 - qx * qx)) * ry;
                p.Px(X, y + 0.5, Math.Abs(x - LAMP) < 10 ? C[6] : C[3]);
            }
            // the back halves of the crowns, behind their own front walls
            foreach (Big q in Drops(S)) Crown(p, C, q, AgeOf(t, q.t0), true);
        }

        // a crown: a ring of fingers thrown up round the drop's hole, a bead on each tip until they tear away
        static void Crown(Painter p, uint[] C, Big q, double a, bool back)
        {
            double h = CrownH(a, q.H);
            if (h < 1) return;
            double r0 = CrownR(a), cy = q.z * SY;
            for (int f = 0; f < 10; f++)
            {
                double ang = q.spin + (f / 10.0) * TAU, s = Px.Sin(ang);
                if (s < 0 != back) continue;
                double tip = h * (0.75 + 0.25 * Rand(q.i, f, 7)), lean = 0.35 * tip, x0 = q.x + Px.Cos(ang) * r0, y0 = cy + s * r0 * SY;
                double x1 = q.x + Px.Cos(ang) * (r0 + lean), y1 = cy + s * (r0 + lean) * SY - tip;
                if (back) p.Line(x0, y0, x1, y1, u => u > 0.7 ? C[5] : C[3]);
                else p.Line(x0, y0, x1, y1, u => u > 0.75 ? WHITE : u > 0.35 ? C[6] : C[5]);
                if (a > 0.08 && a < 0.17) p.Px(x1, y1 - 1, back ? C[5] : WHITE);
            }
            if (!back) p.Glow(q.x, cy - h * 0.6, 6 + h * 0.6, C[5], 0.3 * (h / q.H));
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double S = o.Seed;
            foreach (Big q in Drops(S))
            {
                double a = AgeOf(t, q.t0), cy = q.z * SY;
                // falling in: a streak dropping into the frame just before it lands
                double pre = D - a;
                if (pre < 0.14 && cy - pre * 520 - 10 > -48)
                {
                    double y = cy - pre * 520;
                    p.Line(q.x, y - 10, q.x, y - 4, C[4]);
                    p.Line(q.x, y - 4, q.x, y, C[6]);
                }
                // the hit: a white fleck and a flash of light, then the crown
                if (a < 0.05)
                {
                    p.Px(q.x, cy, WHITE);
                    p.Glow(q.x, cy - 2, 12, C[5], 0.5 * (1 - a / 0.05));
                }
                Crown(p, C, q, a, false);
                // beads torn off the crown's tips arc out and fall back, each with a fleck and a little ring
                if (a > 0.16 && a < 0.8)
                    for (int f = 0; f < 10; f += 2)
                    {
                        double ang = q.spin + (f / 10.0) * TAU, tip = CrownH(0.16, q.H) * (0.75 + 0.25 * Rand(q.i, f, 7)), r0 = CrownR(0.16) + tip * 0.35;
                        double v = 22 + 18 * Rand(q.i, f, 8), vh = 40 + 30 * Rand(q.i, f, 9), tt = a - 0.16, hh = tip + vh * tt - 0.5 * G * tt * tt;
                        double bx = q.x + Px.Cos(ang) * (r0 + v * tt), bz = q.z + Px.Sin(ang) * (r0 + v * tt), by = bz * SY;
                        if (hh > 0) Bead(p, bx, by - hh, Px.Cos(ang) * v, -(vh - G * tt) + Px.Sin(ang) * v * SY, Rand(q.i, f, 10) < 0.4 ? 2 : 1, C);
                        else
                        {
                            double land = (vh + Math.Sqrt(vh * vh + 2 * G * tip)) / G, since = tt - land;
                            if (since < 0.05) p.Px(bx, by, WHITE);
                            if (since < 0.35) Ring(p, bx, by, 1 + since * 12, SY, 1 - since / 0.35, C, n: 1, half: 1, seed: q.i * 10 + f);
                        }
                    }
                // the rebound jet, with a bead that pinches off its top and drops back
                double jh = JetH(a, q.J);
                if (jh > 0.8)
                {
                    p.Line(q.x, cy - 1, q.x, cy - jh, u => u > 0.6 ? WHITE : C[6]);
                    p.Px(q.x + 1, cy - 1, C[5]);
                    if (a < 0.41) p.Circle(q.x, cy - jh - 0.5, 1.2, C[6]);
                    p.Glow(q.x, cy - jh * 0.6, 6, C[5], 0.3);
                }
                double ba = a - 0.41, bh = q.J + 1 + 24 * ba - 0.5 * G * ba * ba;
                if (ba > 0 && bh > 0)
                {
                    p.Circle(q.x, cy - bh, 1.1, C[6]);
                    p.Px(q.x, cy - bh - 1, WHITE);
                }
            }
            // the near halves of the main rings, crisp, so each splash has its own edge in the swell
            foreach (Big q in Drops(S))
            {
                double a = AgeOf(t, q.t0);
                if (a < 1.8) Ring(p, q.x, q.z * SY, RingR(a), SY, 0.75 * RingK(a), C, n: 1, half: 1, hot: a < 0.1, seed: q.i, wob: 0.5 + a);
            }
            // finer rain dimpling the water, and a few streaks falling past
            Cycle(t, D, 16, S + 20, 0.5, (age, life, r, _) =>
            {
                double x = r.Range(-80, 80), z = r.Range(-26, 26), y = z * SY, s = age - 0.1;
                double qx = x / RX, qy = y / (RX * SY);
                if (s < 0)
                {
                    double yy = y - (0.1 - age) * 600;
                    if (yy - 6 < -48) return;
                    p.Line(x, yy - 6, x, yy, u => u > 0.6 ? C[5] : C[3]);
                }
                else if (s < 0.4 && qx * qx + qy * qy < 0.9)
                {
                    if (s < 0.04) p.Px(x, y, C[6]);
                    Ring(p, x, y, 1 + s * 9, SY, 0.8 * (1 - s / 0.4), C, n: 1, half: 1, seed: 40);
                }
            });
            // the lamp's glow on the water
            p.Glow(LAMP, -6, 30, C[4], 0.16);
        }
    }
}
