// Snow driven near-flat by the wind (depth layers of streaks, a gusty wind) after Sprite Fusion's open-source snow
// (https://www.spritefusion.com/pixel-art-vfx/snow), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Blizzard : Effect
    {
        public static readonly Module Module = new Module("blizzard", "pixelfx/fx/weather/blizzard", 320, 180, 160, 170, 8, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0d111f", "#182035", "#27324d", "#3e4d6e", "#64779c", "#9cb0d0", "#d8e4f6", "#ffffff" },
                ["seed"] = 9,
            },
            (o, e) => new Blizzard(o))
        {
            Title = "Blizzard",
            Desc = "Snow driven almost flat by a gale: streaks at three depths race past, gusts throw translucent sheets of snow across the sky and spindrift streams off the drifts.",
            Tags = new[] { "weather", "blizzard", "snow", "wind", "storm", "winter", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 8;
        // gusts: waves of wind rolling left to right, a whole number a loop
        static double GustAt(double x, double t) => 0.5 + 0.5 * Px.Sin(TAU * ((x + 160) / 400 - (2 * t) / D) + 0.8);
        static double Bank(double x) => -2 + 1.2 * Px.Sin(x * 0.05 + 2) + 0.8 * Px.Sin(x * 0.15 + 1);

        sealed class Depth
        {
            public int n;
            public int[] m, col;
            public double[] len, drop;
            public bool near;
        }

        // far to near: how many, crossings a loop, streak length, fall while crossing, colors (tail, body, head)
        static readonly Depth[] LAYERS =
        {
            new Depth { n = 240, m = new[] { 3, 4 }, len = new double[] { 2, 4 }, drop = new double[] { 10, 40 }, col = new[] { 3, 4, 5 } },
            new Depth { n = 130, m = new[] { 4, 5 }, len = new double[] { 5, 8 }, drop = new double[] { 20, 60 }, col = new[] { 4, 5, 6 } },
            new Depth { n = 34, m = new[] { 6, 7 }, len = new double[] { 10, 16 }, drop = new double[] { 30, 70 }, col = new[] { 4, 6, 7 }, near = true },
        };

        readonly Options o;

        Blizzard(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            Sheets(p, t, S, C, 0);
            Streaks(q, t, S, C, 0);
            Streaks(q, t, S, C, 1);
            Ground(p, q, t, S, C);
        }

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            Sheets(p, t, S, C, 1);
            Streaks(q, t, S, C, 2);
        }

        // streaks: each slot crosses the canvas a whole number of times a loop at its own height, dipping as it goes;
        // the gusts stretch and brighten it
        static void Streaks(Clip q, double t, double S, uint[] C, int L)
        {
            Depth Ly = LAYERS[L];
            double sd = S * 17 + L;
            uint c0 = C[Ly.col[0]], c1 = C[Ly.col[1]], c2 = C[Ly.col[2]];
            Trips(t, D, Ly.n, sd, Ly.m[0], Ly.m[1], (u, k, m, i) =>
            {
                double j = Wrap(k, m);
                double R(double n) => Rand(sd, i, j, n);
                double drop = Ly.drop[0] + R(3) * (Ly.drop[1] - Ly.drop[0]);
                double x = -190 + 380 * u, y = -178 + R(1) * 182 + drop * u + 3 * Px.Sin(u * TAU * (1 + R(5)) + R(6) * 6);
                if (y > Bank(x) - 1) return;
                double g = GustAt(x, t);
                // between gusts the far snow thins out; in a gust it's everywhere
                if (!Ly.near && R(7) > 0.45 + 0.75 * g) return;
                double len = (Ly.len[0] + R(2) * (Ly.len[1] - Ly.len[0])) * (0.7 + 0.6 * g), dx = 380 / (D / m), dy = drop / (D / m), l = Hypot(dx, dy);
                double ux = dx / l, uy = dy / l;
                q.Line(x - ux * len, y - uy * len, x - ux * len * 0.45, y - uy * len * 0.45, c0, v => 0.3 + 0.7 * v);
                q.Line(x - ux * len * 0.45, y - uy * len * 0.45, x - ux, y - uy, c1);
                q.Px(x, y, g > 0.6 && Ly.near ? WHITE : c2);
                if (Ly.near) q.Glow(x, y, 5, C[5], 0.12 + 0.12 * g);
            });
        }

        // sheets of blown snow: long thin translucent bands racing with the wind, thick in a gust and thin between
        static readonly float[] GX = new float[320], WV = new float[320];

        static void Sheets(Painter p, double t, double S, uint[] C, int near)
        {
            Field F = MakeField(S * 3 + 7 + near, 256, 128);
            double shift = (256 * (near != 0 ? 3 : 2) * t) / D, y0 = near != 0 ? -110 : -170;
            uint c = near != 0 ? C[6] : C[5], haze = Mix(C[2], C[3], 0.5);
            double sy = near != 0 ? 4.5 : 3.2, th = near != 0 ? 0.58 : 0.54;
            for (int i = 0; i < 320; i++)
            {
                GX[i] = (float)GustAt(i - 160 + 0.5, t);
                WV[i] = (float)(5 * Px.Sin((i - 160) * 0.021 - (TAU * t) / D + near * 2));
            }
            Rows(p, Module, y0, 4, (y, R) =>
            {
                double fade = near != 0 ? Smooth(-110, -40, y) : 1;
                for (int i = 0; i < 320; i++)
                {
                    double g = GX[i], n = Sample(F, (i - 160) * 0.16 - shift, (y + WV[i]) * sy + 40), d = (n - th) * 2.2 * (0.35 + g) * fade;
                    // the far pass is also the whiteout haze behind everything, thicker in a gust
                    if (near != 0)
                    {
                        if (d <= 0.02) continue;
                        R.c[i] = c;
                        R.a[i] = (float)Math.Min(0.5, d);
                    }
                    else
                    {
                        R.c[i] = d > 0.08 ? c : haze;
                        R.a[i] = (float)(0.3 + 0.12 * g + Math.Min(0.42, Math.Max(0, d)));
                    }
                    R.b[i] = 1;
                    if (d > 0.05)
                    {
                        R.l[i] = C[5];
                        R.k[i] = (float)(d * 0.08);
                    }
                }
            });
        }

        // the ground: a low drift with a white crest, spindrift streaming low along it, plumes blown off the crest
        static void Ground(Painter p, Clip q, double t, double S, uint[] C)
        {
            Field F = MakeField(S * 5 + 1, 256, 128);
            double shift = (256 * 4 * t) / D;
            for (int i = 0; i < 320; i++)
            {
                GX[i] = (float)GustAt(i - 160 + 0.5, t);
                WV[i] = (float)Bank(i - 160 + 0.5);
            }
            Rows(p, Module, -16, 9, (y, R) =>
            {
                for (int i = 0; i < 320; i++)
                {
                    double x = i - 160 + 0.5, top = WV[i], d = y - top;
                    if (d >= 0)
                    {
                        R.c[i] = d < 1 ? WHITE : d < 2 ? C[6] : d < 4 ? C[5] : C[4];
                        if (d < 1)
                        {
                            R.l[i] = C[6];
                            R.k[i] = 0.1f;
                        }
                        continue;
                    }
                    // spindrift: a low, fast, streaky haze hugging the drift
                    if (y < -14) continue;
                    double n = Sample(F, x * 0.5 - shift, y * 3 + 9), s = (n - 0.5) * 3 * Smooth(-14, -1, y) * (0.4 + GX[i]);
                    if (s > 0.02)
                    {
                        R.c[i] = C[6];
                        R.a[i] = (float)Math.Min(0.55, s);
                        R.b[i] = 1;
                    }
                }
            });
            // grains skipping along the crest
            Trips(t, D, 40, S * 23, 8, 10, (u, k, m, i) =>
            {
                double j = Wrap(k, m), x = -175 + 350 * u, y = Bank(x) - 1 - Rand(S, i, j, 1) * 6 * Math.Abs(Px.Sin(u * 9 + i));
                q.Px(x, y, Rand(S, i, j, 2) < 0.5 ? WHITE : C[6]);
                q.Px(x - 1, y, C[5]);
            });
        }
    }
}
