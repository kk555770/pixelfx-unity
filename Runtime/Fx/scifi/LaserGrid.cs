// The beam's build (the ramp across it, a hot core, light along it) is adapted from Sprite Fusion's open-source
// energy beam (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class LaserGrid : Effect
    {
        public static readonly Module Module = new Module("laser-grid", "pixelfx/fx/scifi/laser-grid", 128, 84, 64, 76, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0408", "#4a0a12", "#8a1020", "#d01c2a", "#ff4a4a", "#ff9a90", "#ffe4e0", "#ffffff" },
                ["metal"] = new[] { "#0e0d14", "#1e1c27", "#312e3e", "#4b4759", "#6f6a82", "#9d98b0" },
                ["seed"] = 31,
            },
            (o, e) => new LaserGrid(o, e))
        {
            Title = "Laser grid",
            Desc = "Security lasers between two steel posts: red beams slide and scissor across, dust and haze light up only where they cut through, and the floor sweepers scorch and spark.",
            Tags = new[] { "scifi", "laser", "security", "trap", "hazard", "beam", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 3, PX = 48, TOP = -64, IN = PX - 4; // posts at ±PX, TOP high; beams leave their inner faces
        static readonly int[] SIDES = { -1, 1 };
        // the sliding beams: [middle height, swing, sweeps a loop, phase]
        static readonly double[][] SLIDE = { new double[] { -14, 8, 1, 0 }, new double[] { -31, 12, 2, 1.3 }, new double[] { -48, 9, 1, 3.6 } };

        // the sweepers: from each post's head down to a spot on the floor that runs to and fro
        static double SweepX(double t, double s) => s * (34 - 68 * (0.5 - 0.5 * Px.Cos(TAU * (t / D) + (s > 0 ? 0 : 1.9))));
        static double SlideY(double t, double[] b) => b[0] + b[1] * Px.Sin(TAU * b[2] * (t / D) + b[3]);

        static double[][] Beams(double t)
        {
            var o = new List<double[]>();
            foreach (double[] b in SLIDE) o.Add(new[] { -IN, SlideY(t, b), IN, SlideY(t, b) });
            foreach (int s in SIDES) o.Add(new[] { s * IN, TOP + 6, SweepX(t, s), 0 });
            return o.ToArray();
        }

        // a laser: a white-hot core, a red body either side, a dithered fringe, haze lighting up along it
        static void Laser(Painter p, double[] b, uint[] C, double t, double seed)
        {
            double x0 = b[0], y0 = b[1], x1 = b[2], y1 = b[3];
            bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
            double ox = steep ? 1 : 0, oy = steep ? 0 : 1;
            foreach (int s in SIDES) p.Line(x0 + s * ox, y0 + s * oy, x1 + s * ox, y1 + s * oy, s < 0 ? C[4] : C[3]);
            p.Line(x0, y0, x1, y1, u => ((((u * 5 - t * 3) % 1) + 1) % 1 < 0.12 ? C[6] : WHITE));
            // the haze it passes through scatters its light, so its glow is uneven and drifts
            double L = Hypot(x1 - x0, y1 - y0);
            for (double d = 0; d <= L; d += 5)
            {
                double x = Lerp(x0, x1, d / L), y = Lerp(y0, y1, d / L), h = 0.5 + 0.5 * TileFbm(x / 14 + (t / D) * 4, y / 10, 4, 0, seed);
                p.Glow(x, y, 6 + 4 * h, C[3], 0.14 + 0.3 * h * h);
            }
        }

        // where two beams cross, if they do
        static double[] Cross(double[] a, double[] b)
        {
            double x1 = a[0], y1 = a[1], x2 = a[2], y2 = a[3], x3 = b[0], y3 = b[1], x4 = b[2], y4 = b[3], d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(d) < 1e-6) return null;
            double u = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d, v = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / d;
            return u >= 0 && u <= 1 && v >= 0 && v <= 1 ? new[] { x1 + u * (x2 - x1), y1 + u * (y2 - y1) } : null;
        }

        static double Near(double[] b, double x, double y)
        {
            double x0 = b[0], y0 = b[1], x1 = b[2], y1 = b[3], dx = x1 - x0, dy = y1 - y0, u = Clamp(((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy)), ex = x0 + dx * u - x, ey = y0 + dy * u - y;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        // a steel post: a lit edge, a shaded side, a groove the emitters ride in, red light where the beams meet it
        static void Post(Painter p, int s, uint[] H, uint[] C, double t)
        {
            double x0 = s * PX - 4;
            double[] B = Array.ConvertAll(SLIDE, b => SlideY(t, b));
            for (double y = TOP + 4; y < 0; y++)
            {
                double glint = double.NegativeInfinity;
                foreach (double by in B) glint = Math.Max(glint, Exp(-Px.Pow((y - by) / 2.5, 2)));
                for (int i = 0; i < 8; i++)
                {
                    double x = x0 + i;
                    bool inner = s < 0 ? i >= 6 : i <= 1;
                    uint c = i == 0 || i == 7 ? H[0] : i == 1 ? H[4] : i == 2 ? H[3] : i == 4 ? H[1] : i == 6 ? H[2] : H[3];
                    if (inner && i != 0 && i != 7 && glint > 0.35) c = glint > 0.75 ? C[5] : C[3];
                    p.Px(x, y, c);
                }
            }
            // bolts down the post, the head with its lens, the foot plate
            for (double y = TOP + 12; y < -4; y += 9) p.Px(x0 + 3, y, H[5]);
            for (double y = TOP; y < TOP + 10; y++)
                for (int i = -1; i < 9; i++) p.Px(x0 + i, y, i == -1 || i == 8 || y == TOP || y == TOP + 9 ? H[0] : y == TOP + 1 ? H[5] : i < 3 ? H[4] : H[2]);
            p.Px(s * IN, TOP + 6, WHITE);
            p.Px(s * IN, TOP + 5, C[5]);
            p.Px(s * IN, TOP + 7, C[4]);
            for (int i = -3; i < 11; i++)
            {
                p.Px(x0 + i, -2, i == -3 || i == 10 ? H[0] : H[4]);
                p.Px(x0 + i, -1, i == -3 || i == 10 ? H[0] : H[2]);
                p.Px(x0 + i, 0, H[1]);
            }
            // a carriage for each sliding beam, riding the groove
            foreach (double by in B)
            {
                double y = Round(by);
                for (int j = -2; j <= 2; j++)
                    for (int i = 0; i < 4; i++) p.Px(s < 0 ? x0 + 5 + i : x0 - 1 + i, y + j, Math.Abs(j) == 2 || i == (s < 0 ? 3 : 0) ? H[0] : j < 0 ? H[5] : H[3]);
                p.Px(s * IN, y, WHITE);
                p.Glow(s * IN, y, 7, C[4], 0.55);
            }
            p.Glow(s * IN, TOP + 6, 8, C[4], 0.55);
        }

        readonly uint[] C, H;
        readonly double S;
        readonly ScifiLib.Ghosts g;

        LaserGrid(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            H = Fit(o.Ramp("metal"), 6);
            S = o.Seed;
            var rn = new Rng(o.Seed);
            g = new ScifiLib.Ghosts(D, x => 0, new[] { WHITE, C[6], C[4], C[3] }, 0, new double[] { -62, -80, 62, 7 });
            // the sweepers kick up sparks and wisps of smoke where they burn the floor
            for (int i = 0; i < 36; i++)
            {
                double tb = (i / 36.0) * D + rn.Range(0, 0.05), s = i % 2 != 0 ? 1 : -1, x = SweepX(tb, s);
                int sg = Sign(SweepX(tb + 0.02, s) - x);
                double dir = sg != 0 ? sg : 1, a = -Math.PI / 2 - dir * rn.Range(0.2, 1.1);
                g.Add(tb, P.Spark, x, -1, Px.Cos(a) * rn.Range(50, 140), Px.Sin(a) * rn.Range(50, 140), rn.Range(0.2, 0.45));
                if (i % 4 == 0) g.Add(tb, P.Smoke, x, -2, rn.Range(-4, 4), -rn.Range(6, 14), rn.Range(0.8, 1.2), Hex("#3a2a30"), rn.Range(1.6, 2.4));
            }
        }

        public override void Draw(Painter p, double t)
        {
            double[][] B = Beams(t);
            // the floor where the sweepers have just been: a scorch that cools from white to deep red
            foreach (int s in SIDES)
                for (int k = 0; k < 14; k++)
                {
                    double ago = k * 0.025, x = SweepX(t - ago, s);
                    uint c = k < 2 ? WHITE : k < 5 ? C[6] : k < 9 ? C[4] : C[3];
                    p.Dot(x, 0, c, 1 - k / 14.0);
                    p.Dot(x, 1, C[3], 0.6 - k / 24.0);
                }
            // a low red pool on the floor under it all
            for (double x = -40; x <= 40; x += 20) p.Glow(x, -2, 20, C[3], 0.12);
            // haze hanging in the air, thickest low down, seen only where a beam passes through it and lights it red
            double ph = t / D;
            for (int y = -60; y < 0; y++)
                for (double x = -IN + 1; x < IN; x++)
                {
                    double d = 99;
                    foreach (double[] b in B) d = Math.Min(d, Near(b, x + 0.5, y + 0.5));
                    if (d > 11) continue;
                    double low = 0.35 + 0.65 * Clamp((y + 60) / 50.0), n = 0.5 + 0.5 * TileFbm((x + 0.5) / 18 + ph * 3, (y + 0.5) / 7 + 0.4 * Px.Sin(TAU * ph), 3, 0, S + 7, 2), dens = Clamp((n - 0.42) * 2.2) * low;
                    if (dens <= 0.02) continue;
                    double lit = Exp(-d / 4.5);
                    p.Blend(x + 0.5, y + 0.5, lit > 0.5 ? C[4] : lit > 0.15 ? C[3] : C[1], dens * 0.8 * lit);
                }
            foreach (int s in SIDES) Post(p, s, H, C, t);
            foreach (double[] b in B) Laser(p, b, C, t, S);
            // dust and haze motes, invisible until a beam catches them
            for (int i = 0; i < 90; i++)
            {
                double x = (Rand(S, i) * 2 - 1) * (IN - 3) + 4 * Px.Sin(TAU * (t / D) + i), y = -2 - Rand(S, i, 1) * 62 + 3 * Px.Sin(TAU * 2 * (t / D) + i * 1.7);
                double d = 9;
                foreach (double[] b in B) d = Math.Min(d, Near(b, x, y));
                if (d < 1.6)
                {
                    p.Px(x, y, d < 0.9 ? WHITE : C[6]);
                    p.Glow(x, y, 3, C[5], 0.45);
                }
            }
            // where beams cross, the haze between them flares
            for (int i = 0; i < B.Length; i++)
                for (int j = i + 1; j < B.Length; j++)
                {
                    double[] c = Cross(B[i], B[j]);
                    if (c == null) continue;
                    p.Glow(c[0], c[1], 9, C[5], 0.4);
                    Sim.Sparkle(p, c[0], c[1], 2, C[6], C[5], 0.3);
                }
            // the sweepers' hot spots on the floor
            foreach (int s in SIDES)
            {
                double x = SweepX(t, s);
                p.Px(x, -1, WHITE);
                p.Px(x - 1, 0, C[6]);
                p.Px(x + 1, 0, C[6]);
                p.Glow(x, -1, 10, C[4], 0.55);
                p.Glow(x, -1, 4, WHITE, 0.35);
            }
            g.At(t).Draw(p);
        }
    }
}
