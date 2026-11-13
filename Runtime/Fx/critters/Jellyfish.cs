// The bubbles are adapted from Sprite Fusion's open-source bubbles (https://www.spritefusion.com/pixel-art-vfx/bubbles),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Jellyfish : Effect
    {
        public static readonly Module Module = new Module("jellyfish", "pixelfx/fx/critters/jellyfish", 96, 120, 48, 38, 3.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#140b2b", "#2a1650", "#4b2386", "#7a3cb8", "#b066e0", "#e09cf5", "#ffd6ff", "#ffffff" },
                ["glow"] = new[] { "#0b3550", "#1f7fa8", "#4fd2e8", "#b8fbff" },
                ["seed"] = 1,
            },
            (o, e) => new Jellyfish(o))
        {
            Title = "Jellyfish",
            Desc = "A see-through jellyfish pulses: its bell squeezes to shove it up and relaxes as it sinks, frilled arms and long tentacles trailing, a glow inside, bubbles drifting by.",
            Tags = new[] { "critter", "jellyfish", "sea", "underwater", "glow", "swim", "loop" },
            Bg = "dark",
        };

        const double D = 3.2, BEAT = D / 2, W0 = 32, H0 = 18;

        // the bell's squeeze (1 squeezed, below 0 flared out past rest) and its height through one beat
        static double Squeeze(double u)
        {
            if (u < 0.16) return Ease.OutCubic(u / 0.16);
            if (u < 0.22) return 1;
            if (u < 0.72) return Lerp(1, -0.14, Ease.InOutSine((u - 0.22) / 0.5));
            return Lerp(-0.14, 0, Ease.InOutSine((u - 0.72) / 0.28));
        }

        static double Lift(double u) => u < 0.42 ? -6 * Ease.OutCubic(Seg(u, 0.04, 0.42)) : -6 + 6 * Ease.InOutSine((u - 0.42) / 0.58);

        sealed class BellState
        {
            public double x, y, c, W, H;
        }

        // the bell at loop time t: rim center (x, y), width, height, squeeze
        static BellState Bell(double t)
        {
            t = Wrap(t, D);
            double u = (t % BEAT) / BEAT, c = Squeeze(u);
            return new BellState { x = 2 * Px.Sin((t / D) * TAU), y = Lift(u), c = c, W = W0 * (1 - 0.3 * c), H = H0 * (1 + 0.24 * c) };
        }

        // the bell's half-width h px above its rim: a dome whose margin pulls in as it squeezes
        static double HalfW(double h, BellState B)
        {
            if (h > B.H) return -1;
            double m = Math.Max(0, h) / B.H;
            double hw = (B.W / 2) * Math.Sqrt(Math.Max(0, 1 - m * m));
            if (h < B.H * 0.3) hw -= B.c * 2.4 * (1 - h / (B.H * 0.3));
            return hw;
        }

        // the margin hangs in little scallops
        static double Scallop(double dx, BellState B) => 1.1 * Math.Max(0, Px.Sin((dx / (B.W / 2)) * Math.PI * 4 + Math.PI / 2));

        void DrawBell(Painter p, uint[] C, uint[] G, BellState B, double t)
        {
            Grid g = GRID.Clear();
            double bx = Round(B.x), by = Round(B.y);
            bool Inside(double x, double y)
            {
                double dx = x - B.x, h = B.y - y;
                if (h < -Scallop(dx, B)) return false;
                double hw = HalfW(Math.Max(0, h), B);
                return hw >= 0 && Math.Abs(dx) <= hw;
            }
            g.Shade(-20, -26, 20, 3, (x, y, o) =>
            {
                double X = x + bx, Y = y + by;
                if (!Inside(X, Y)) return 0;
                o.t = 1;
                return C[3];
            });
            g.Depths(4);
            // the subumbrella: the hollow under the bell, seen through it as a fainter inner dome
            double iw = B.W * 0.36, ih = B.H * 0.66, lx = B.x - B.W * 0.22, ly = B.y - B.H * 0.78;
            g.Recolor((x, y, c, a, tag) =>
            {
                if (tag != 1) return c;
                double X = x + bx + 0.5, Y = y + by + 0.5;
                int d = g.Depth(x, y);
                double dx = X - B.x, h = B.y - Y, v = h / B.H;
                int i = g.Idx(x, y);
                uint col = C[3];
                double al = 0.26 + 0.2 * Clamp(v);
                double ie = (dx / iw) * (dx / iw) + (Math.Max(0, h) / ih) * (Math.Max(0, h) / ih);
                if (ie > 0.8 && ie < 1.12 && h > 0.5)
                {
                    col = C[4];
                    al = 0.55;
                }
                if (h < 1)
                {
                    col = C[5];
                    al = 0.85;
                }
                if (d == 2)
                {
                    col = v > 0.5 && dx < 0 ? C[5] : C[4];
                    al = 0.75;
                }
                if (d == 1)
                {
                    col = dx < 0 && v > 0.25 ? C[6] : C[5];
                    al = 0.92;
                }
                // the gloss on the top-left of the dome
                double gx = X - lx, gy = Y - ly;
                if (d == 2 && dx < -1 && v > 0.45 && gx * gx + gy * gy < 26)
                {
                    col = C[7];
                    al = 1;
                }
                g.a[i] = (float)al;
                return col;
            });
            // radial canals running down from the crown
            foreach (double s in new[] { -0.62, 0.62 })
            {
                for (double h = 2; h < B.H - 2; h++)
                {
                    double hw = HalfW(h, B), x = B.x + s * hw * 0.9, y = B.y - h;
                    if (g.Depth(x - bx, y - by) >= 3) g.Set(x - bx, y - by, C[4], 0.5);
                }
            }
            // the stomach and gonads: a soft glowing mass hung in the bell, brighter as it squeezes
            double pulse = 0.5 + 0.5 * B.c, cy = B.y - B.H * 0.42, rx = B.W * 0.2, ry = B.H * 0.2;
            for (double y = Math.Floor(cy - ry); y <= cy + ry; y++)
            for (double x = Math.Floor(B.x - rx); x <= B.x + rx; x++)
            {
                double ex = (x + 0.5 - B.x) / rx, ey = (y + 0.5 - cy) / ry, d = ex * ex + ey * ey;
                if (d > 1 || g.Depth(x - bx, y - by) < 2) continue;
                g.Set(x - bx, y - by, d < 0.3 ? C[6] : d < 0.65 ? C[5] : C[4], d < 0.3 ? 0.9 : 0.6);
            }
            g.Outline((x, y, nx, ny) => ny > 0 ? 0 : C[1], 0.9);
            g.Draw(p, 1, bx, by);
            // the light inside, and the little lamps round the margin
            p.Glow(B.x, B.y - B.H * 0.45, 30 + 6 * B.c, C[4], 0.22 + 0.12 * pulse);
            p.Glow(B.x, B.y - B.H * 0.45, 11, C[6], 0.14 + 0.16 * pulse);
            foreach (int s in new[] { -1, 1 })
            foreach (double q in new[] { 0.14, 0.42, 0.78 })
            {
                double x = B.x + s * HalfW(0.5, B) * q, y = B.y + 1, tw = 0.6 + 0.4 * Px.Sin((t / D) * TAU * 3 + q * 9 + s);
                p.Px(x, y, tw > 0.85 ? G[3] : G[2]);
                p.Glow(x, y, 4, G[2], 0.4 * tw);
            }
        }

        // a strand hanging from the bell: each point follows where its root was a moment ago, so the strand trails
        // behind every thrust and billows as the bell sinks; a slow wave runs down it
        static List<double[]> Strand(double t, Func<BellState, double[]> off, double len, double step, double lag, double sway, double ph, double pull)
        {
            var pts = new List<double[]>();
            BellState B1 = Bell(t);
            double[] o1 = off(B1);
            for (double s = 0; s <= len; s += step)
            {
                BellState B = Bell(t - s * lag);
                double relax = 1 - Clamp(B.c), ox = Lerp(o1[0], off(B)[0], 0.4);
                double w = (0.3 + s * sway) * (0.55 + 0.65 * relax) * Px.Sin((t / D) * TAU * 2 - s * 0.11 + ph);
                pts.Add(new[] { B.x + ox + w + ox * s * (0.009 * relax - 0.005 * pull * B.c), B.y + o1[1] + s * (0.88 + 0.05 * B.c) });
            }
            return pts;
        }

        static void DrawTentacle(Painter p, uint[] C, uint[] G, List<double[]> pts, bool back, double t, int i)
        {
            int n = pts.Count;
            for (int k = 1; k < n; k++)
            {
                double u = (double)k / (n - 1), x0 = pts[k - 1][0], y0 = pts[k - 1][1], x1 = pts[k][0], y1 = pts[k][1];
                uint c = back ? (u < 0.3 ? C[3] : u < 0.7 ? C[2] : C[1]) : u < 0.2 ? C[5] : u < 0.45 ? C[4] : u < 0.78 ? C[3] : C[2];
                p.Line(x0, y0, x1, y1, c);
            }
            // stinging cells glinting along it
            if (back) return;
            for (int k = 3; k < n; k += 4)
            {
                double tw = Px.Sin((t / D) * TAU * 2 - k * 0.5 + i * 1.7);
                if (tw < 0.3) continue;
                double x = pts[k][0], y = pts[k][1];
                p.Px(x, y, tw > 0.8 ? G[3] : G[2]);
                p.Glow(x, y, 3.5, G[2], 0.35 * tw);
            }
        }

        sealed class StrandOf
        {
            public bool back;
            public int i;
            public List<double[]> pts;
        }

        // a frilled oral arm: a ribbon lit down its left edge, darker on its right, ruffled along both sides
        void DrawArms(Painter p, uint[] C, List<StrandOf> list, bool back)
        {
            Grid g = ARMS.Clear();
            double a = back ? 0.6 : 0.88;
            foreach (StrandOf arm in list)
            {
                if (arm.back != back) continue;
                List<double[]> pts = arm.pts;
                int n = pts.Count;
                double lastRow = -1e9;
                for (int k = 1; k < n; k++)
                {
                    double x0 = pts[k - 1][0], y0 = pts[k - 1][1], x1 = pts[k][0], y1 = pts[k][1], m = Math.Max(1, Math.Ceiling(Math.Abs(y1 - y0) * 2));
                    for (double j = 0; j < m; j++)
                    {
                        double q = j / m, x = Lerp(x0, x1, q), y = Lerp(y0, y1, q), u = (k - 1 + q) / (n - 1), wd = Lerp(2.2, 0.6, u);
                        for (double i = -Math.Ceiling(wd); i <= Math.Ceiling(wd); i++)
                        {
                            if (Math.Abs(i) > wd + 0.2) continue;
                            g.Set(x + i, y, i < 0 ? C[back ? 4 : 6] : i > 0 ? C[back ? 2 : 4] : C[back ? 3 : 5], a);
                        }
                        double row = Math.Floor(y);
                        if (row != lastRow && row % 3 == 0 && u < 0.85)
                        {
                            double r3 = (row / 3) % 2;
                            g.Set(x + (r3 != 0 && !double.IsNaN(r3) ? -wd - 1.2 : wd + 1.2), y, C[back ? 4 : 6], a);
                        }
                        lastRow = row;
                    }
                }
            }
            g.Draw(p);
        }

        static readonly double[] ARM_Q = { -0.32, -0.1, 0.12, 0.3 };

        readonly Options o;
        readonly Grid GRID = new Grid(-22, -28, 44, 36), ARMS = new Grid(-40, -40, 80, 100);

        Jellyfish(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 8), G = Fit(o.Ramp("glow"), 4);
            BellState B = Bell(t);
            // specks drifting in the water
            Cycle(t, D, 16, o.Seed * 5 + 1, D, (age, L, r, idx) =>
            {
                double x = r.Range(-42, 42) + Px.Sin(age * 0.8 + r.Next() * 6) * 3, y = r.Range(-34, 66) + age * r.Range(1, 4);
                double tw = Px.Sin(age * 2.2 + r.Next() * 6), a = Min(1, age * 2, (L - age) * 2) * (0.35 + 0.35 * tw);
                p.Dot(x, y, tw > 0.8 ? G[2] : C[3], a);
            });
            var tents = new List<StrandOf>();
            var arms = new List<StrandOf>();
            for (int i = 0; i < 9; i++)
            {
                Rng r = NewRng(o.Seed, i, 4);
                double q = -0.88 + (1.76 * i) / 8 + r.Range(-0.05, 0.05);
                bool back = i % 2 == 1;
                double len = r.Range(36, 50);
                double ph = r.Range(0, TAU);
                tents.Add(new StrandOf { back = back, i = i, pts = Strand(t, b => new[] { q * HalfW(0.5, b), 0.5 }, len, 1.5, 0.022, 0.065, ph, 1) });
            }
            for (int i = 0; i < 4; i++)
            {
                Rng r = NewRng(o.Seed, i, 9);
                double q = ARM_Q[i];
                bool back = i == 1 || i == 2;
                double len = r.Range(20, 27);
                double ph = r.Range(0, TAU) + i;
                arms.Add(new StrandOf { back = back, pts = Strand(t, b => new[] { q * b.W * 0.5, -1 }, len, 1, 0.03, 0.06, ph, 0.4) });
            }
            // each squeeze shoves a ring of water out under the bell: a soft swirl of light that swells and thins
            for (int b = 0; b < 2; b++)
            {
                double age = Wrap(t - b * BEAT - 0.05, D);
                if (age > 0.9) continue;
                BellState B0 = Bell(b * BEAT + 0.05);
                double e = Ease.OutCubic(age / 0.9), k = 1 - age / 0.9;
                double cx = B0.x, cy = B0.y + 3 + 14 * e, rx = B0.W * 0.32 + 9 * e, ry = 1.5 + 2.5 * e;
                for (int j = 0; j < 8; j++)
                {
                    double a = (j / 8.0) * TAU + e * 0.8;
                    p.Glow(cx + Px.Cos(a) * rx, cy + Px.Sin(a) * ry, 5 + 3 * e, C[5], 0.14 * k);
                }
                // plankton caught in the swirl, glinting as it spins
                for (int j = 0; j < 7; j++)
                {
                    double a = (j / 7.0) * TAU + e * 2.4 + b, x = cx + Px.Cos(a) * rx * 1.05, y = cy + Px.Sin(a) * ry * 1.4 - Px.Cos(a * 2) * 0.6;
                    if (k < 0.15 && j % 2 != 0) continue;
                    p.Dot(x, y, Px.Sin(a) > 0 ? G[3] : G[2], Math.Min(1, k * 2.5));
                    p.Glow(x, y, 3, G[2], 0.3 * k);
                }
                // and a few bubbles squeezed out of the margin
                for (int i = 0; i < 4; i++)
                {
                    Rng r = NewRng(o.Seed, b, i, 13);
                    double L = r.Range(1.4, 2.2), ag = Wrap(t - b * BEAT - 0.08 - i * 0.04, D);
                    if (ag > L) continue;
                    double rad = r.Range(0.6, 1.5), sx = r.Sign();
                    double x = B0.x + sx * (B0.W * 0.4 + r.Range(0, 4) + ag * r.Range(4, 10)), y = B0.y + 2 - (14 + 10 * rad) * ag + 6 * Exp(-ag * 4);
                    if (y < -33) continue;
                    Bubble(p, x + Px.Sin(ag * 5 + i) * 0.8, y, rad, new[] { G[3], G[3], G[2], G[1] }, Min(1, ag * 6, (L - ag) * 3, (y + 33) / 6));
                }
            }
            foreach (StrandOf a in tents)
                if (a.back)
                    DrawTentacle(p, C, G, a.pts, true, t, a.i);
            DrawArms(p, C, arms, true);
            DrawBell(p, C, G, B, t);
            DrawArms(p, C, arms, false);
            foreach (StrandOf a in tents)
                if (!a.back)
                    DrawTentacle(p, C, G, a.pts, false, t, a.i);
            // bubbles wobbling up past it
            Cycle(t, D, 7, o.Seed * 5 + 2, r => r.Range(1.8, 3), (age, L, r, idx) =>
            {
                double rad = r.Chance(0.6) ? r.Range(0.6, 1.4) : r.Range(1.6, 2.6), x0 = r.Range(-30, 30), y0 = r.Range(10, 70), v = 20 + 13 * rad;
                double end = Math.Min(L, (y0 + 30) / v);
                if (age > end) return;
                double ph = r.Next() * TAU, x = x0 + Px.Sin(age * (4 + 2 / rad) + ph) * (0.6 + rad * 0.8), y = y0 - v * age;
                Bubble(p, x, y, rad, new[] { G[3], G[3], G[2], G[1] }, Min(1, age * 3, (end - age) * 3), 0.1 * Px.Sin(age * 9 + ph));
            });
            // a soft cool glow all round
            p.Glow(B.x, B.y + 6, 58, C[3], 0.14);
        }
    }
}
