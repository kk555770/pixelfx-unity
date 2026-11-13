// The bubble shading (a lit rim top-left, a shaded rim bottom-right, a see-through body, a highlight and a
// refraction fleck, 2x2 and 1 px forms when small) and the surface line are adapted from Sprite Fusion's
// open-source bubbles (https://www.spritefusion.com/pixel-art-vfx/bubbles), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Bubbles : Effect
    {
        public static readonly Module Module = new Module("bubbles", "pixelfx/fx/water/bubbles", 104, 112, 52, 104, 4, true, 30,
            new Options { ["palette"] = WATER, ["seed"] = 4 },
            (o, e) => new Bubbles(o, e))
        {
            Title = "Bubbles",
            Desc = "Bubbles stream up from a vent of pebbles, wobbling and swelling as they rise, flare as they cross shafts of light from the surface, then dome up and pop there.",
            Tags = new[] { "water", "bubbles", "underwater", "vent", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 4, TOP = -92, HALF = 48;
        static readonly double SQRT2 = Math.Sqrt(2);

        // the surface: a wavy lit line, a darker band under it
        static double SurfaceY(double x, double ph) => TOP + Px.Sin(x * 0.13 + ph * TAU * 2) * 1.1 + Px.Sin(x * 0.051 - ph * TAU) * 1.3;

        // light shafts slanting down from the surface, drifting: how much of one a point is in
        static double Shaft(double x, double y, double ph)
        {
            double u = x + (y - TOP) * 0.32, s = Px.Sin(u * 0.16 + ph * TAU) * 0.6 + Px.Sin(u * 0.07 - ph * TAU + 1) * 0.4;
            return Math.Max(0, s - 0.35) / 0.65;
        }

        // one bubble at (x, y), radius r, squash q
        static void Bubble(Painter p, uint[] C, double x, double y, double r, double q, double lit)
        {
            if (r < 0.9)
            {
                p.Px(x, y, lit > 0.3 ? WHITE : r < 0.7 ? C[5] : C[6]);
                return;
            }
            if (r < 1.5)
            {
                double X = Math.Floor(x), Y = Math.Floor(y);
                p.Px(X, Y, WHITE);
                p.Px(X + 1, Y, C[6]);
                p.Px(X, Y + 1, C[5]);
                p.Px(X + 1, Y + 1, C[4]);
                p.Glow(x + 0.5, y + 0.5, 3, C[5], 0.12 + 0.2 * lit);
                return;
            }
            double rx = r * (1 + q), ry = r * (1 - q);
            p.Shade(x - rx - 1, y - ry - 1, x + rx + 1, y + ry + 1, (px, py, @out) =>
            {
                double dx = (px - x) / rx, dy = (py - y) / ry, d = Hypot(dx, dy) * r;
                if (d > r + 0.5) return 0;
                double l = (-dx - dy) / SQRT2 / Math.Max(0.01, d / r);
                if (d > r - 0.6) @out.c = l > 0.45 ? C[6] : l < -0.55 ? C[3] : C[4];
                else p.Blend(px, py, C[5], 0.16 + 0.2 * lit);
                return 0;
            });
            p.Px(Round(x - rx * 0.45), Round(y - ry * 0.45), WHITE);
            if (r >= 2.5) p.Px(Round(x - rx * 0.45) + 1, Round(y - ry * 0.45), C[6]);
            if (r >= 2) p.Px(Round(x + rx * 0.5), Round(y + ry * 0.5), C[6]);
            p.Glow(x, y, r * 2.4 + 2, C[5], 0.12 + 0.3 * lit);
        }

        struct B
        {
            public double x, y, r, q, age, vy;
            public int i;
        }

        // the bubbles, from the vent's three mouths: each rises faster the bigger it is, swells as the pressure drops,
        // wobbles side to side, and pops at the surface
        static readonly double[] MOUTHS = { -5, 1, 6 };

        static void Each(double t, double S, Action<B> fn)
        {
            Cycle(t, D, 46, S, g => g.Range(2.2, 3.4), (age, life, r, i) =>
            {
                double m = MOUTHS[i % 3], r0 = r.Next() < 0.7 ? r.Range(0.5, 1.4) : r.Range(1.6, 2.6), vy = 30 + 9 * r0 + r.Range(-4, 4);
                double wf = r.Range(3, 6), wa = 0.8 + r0 * 1.2, ph0 = r.Range(0, TAU), drift = r.Range(-4, 4);
                double y = -2 - vy * age, k = Clamp(-y / -TOP), x = m + drift * k + Px.Sin(age * wf + ph0) * wa * k;
                fn(new B { x = x, y = y, r = r0 * (1 + 0.45 * k), q = 0.12 * Px.Sin(age * wf * 2 + ph0) * Math.Min(1, r0 / 2), i = i, age = age, vy = vy });
            });
        }

        static readonly double[][] PEBBLES = { new[] { -9, -1.5, 2.4 }, new[] { -4, -1, 1.8 }, new[] { 3.5, -1.2, 2.2 }, new[] { 9, -1.4, 2.6 }, new[] { 0, -0.6, 1.4 }, new[] { -12.5, -0.8, 1.6 } };

        readonly Options o;

        Bubbles(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D;
            // the water: deeper toward the floor, light falling through it in shafts
            for (double Y = TOP - 2; Y < 0; Y++)
                for (double X = -HALF; X < HALF; X++)
                {
                    double x = X + 0.5, y = Y + 0.5, sy = SurfaceY(x, ph);
                    if (y < sy) continue;
                    double e = 1 - Smooth(HALF - 14, HALF, Math.Abs(x)), depth = (y - TOP) / -TOP;
                    p.Blend(x, y, C[1], (0.22 + 0.22 * depth) * e);
                    double s = Shaft(x, y, ph) * (1 - depth * 0.7) * e;
                    if (s > 0.05) p.Light(x, y, C[4], 0.2 * s);
                }
            // where the shafts reach the floor, caustic light dances on it
            for (double X = -HALF; X < HALF; X++)
            {
                double x = X + 0.5, e = 1 - Smooth(HALF - 14, HALF, Math.Abs(x)), s = Shaft(x, -2, ph);
                double c = Math.Max(0, 1 - Math.Abs(Px.Sin(x * 0.42 + Px.Sin(x * 0.11 + ph * TAU * 2) * 2.2)) * 2.4);
                if (s * c * e > 0.05)
                {
                    p.Light(x, -1, C[5], 0.5 * s * c * e);
                    p.Light(x, -2, C[4], 0.25 * s * c * e);
                    p.Light(x, 0, C[4], 0.3 * s * c * e);
                }
            }
            // the vent: a little heap of pebbles round a dark mouth
            uint[] ST = Rock();
            foreach (double[] pb in PEBBLES)
            {
                double x = pb[0], y = pb[1], rr = pb[2];
                p.Shade(x - rr - 1, y - rr - 1, x + rr + 1, y + rr + 1, (px, py, @out) =>
                {
                    double dx = px - x, dy = py - y, d = Hypot(dx * 1.1, dy);
                    if (d > rr + 0.7 || py > 0) return 0;
                    @out.c = d > rr ? ST[0] : dx + dy < -rr * 0.6 ? ST[5] : dx + dy < rr * 0.3 ? ST[3] : ST[2];
                    return 0;
                });
            }
            foreach (double m in MOUTHS) p.Px(m, -1, ST[0]);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, S = o.Seed;
            // specks drifting in the water, flaring in the light
            Cycle(t, D, 26, S + 7, D, (age, life, r, _) =>
            {
                double x = r.Range(-HALF + 6, HALF - 6) + Px.Sin((age / D) * TAU + r.Range(0, TAU)) * 3, y = TOP + 6 + ((r.Range(0, 86) + age * 4) % 86);
                double s = Shaft(x, y, ph), tw = Px.Sin((age / D) * TAU * 3 + r.Range(0, TAU));
                if (tw < -0.3 && s < 0.2) return;
                p.Px(x, y, s > 0.4 ? WHITE : tw > 0.6 ? C[5] : C[4]);
                if (s > 0.4) p.Glow(x, y, 3, C[5], 0.25 * s);
            });
            // the bubbles; when one reaches the surface it domes up, then pops with a ring and a couple of flecks
            Each(t, S, b =>
            {
                double sy = SurfaceY(b.x, ph);
                if (b.y > sy + b.r) Bubble(p, C, b.x, b.y, b.r, b.q, Shaft(b.x, b.y, ph));
                else
                {
                    double since = (sy + b.r - b.y) / b.vy;
                    if (since < 0.12 && b.r >= 1.4)
                    {
                        for (double a = Math.PI; a <= TAU + 0.01; a += 0.4 / b.r) p.Px(b.x + Px.Cos(a) * b.r, sy + Px.Sin(a) * b.r * 0.9, C[6]);
                        p.Px(b.x - b.r * 0.4, sy - b.r * 0.6, WHITE);
                    }
                    else if (since < (b.r >= 1.4 ? 0.32 : 0.2))
                    {
                        double k = Clamp((since - (b.r >= 1.4 ? 0.12 : 0)) / 0.2), w = Math.Max(1, b.r + 1 + k * 4);
                        for (double a = 0; a < Math.PI; a += 0.5 / w)
                            if (Px.Sin(a) > 0.15) p.Dot(b.x + Px.Cos(a) * w, sy + Px.Sin(a) * w * 0.3, k < 0.4 ? WHITE : C[5], 1.2 - k);
                        if (b.r >= 1.4 && k < 0.6)
                        {
                            p.Px(b.x - 2, sy - 2 - k * 4, C[6]);
                            p.Px(b.x + 2, sy - 3 - k * 5, C[6]);
                        }
                    }
                }
            });
            // the surface: a lit line with glints running along it, a darker band under it
            for (int X = -(int)HALF; X < HALF; X++)
            {
                double x = X + 0.5, y = SurfaceY(x, ph), e = 1 - Smooth(HALF - 12, HALF, Math.Abs(x));
                bool glint = Px.Sin(x * 0.37 - ph * TAU * 3) * Px.Sin(x * 0.13 + ph * TAU) > 0.55;
                p.Dot(X, y, glint ? WHITE : C[5], e * 1.3);
                p.Dot(X, y + 1, C[3], e * 1.3);
                if ((X & 3) == 0) p.Glow(x, y + 1, 7, C[5], (glint ? 0.22 : 0.1) * e);
            }
        }
    }
}
