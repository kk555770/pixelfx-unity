// The banded light (posterized steps from a hot core to the edges, fine strands through it, light laid over the top)
// is adapted from Sprite Fusion's open-source holy light (https://www.spritefusion.com/pixel-art-vfx/holy-light),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class GodRays : Effect
    {
        public static readonly Module Module = new Module("god-rays", "pixelfx/fx/weather/god-rays", 320, 180, 160, 170, 10, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a1208", "#3a2810", "#7a5a24", "#c89a44", "#f2d27a", "#fff2c4", "#ffffff" },
                ["seed"] = 3,
            },
            (o, e) => new GodRays(o))
        {
            Title = "God rays",
            Desc = "Shafts of light slant down from high on the left, breathing and swaying, pooling on the floor, as drifting dust motes light up whenever they cross a beam.",
            Tags = new[] { "weather", "god rays", "light", "sunbeam", "dust", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 10, SX = -230, SY = -300;
        const int W = 320;
        // the fan's axis (down and to the right) and its normal; a pixel's place across the fan is its angle from it
        static readonly double AX = Px.Cos(0.94), AY = Px.Sin(0.94), NX = -AY, NY = AX;

        sealed class Shaft
        {
            public double c, h, k, b, sw;
        }

        // the shafts: angle off the axis, half width (radians), strength, breathing (cycles a loop), sway
        static readonly Shaft[] SHAFTS =
        {
            new Shaft { c = -0.27, h = 0.03, k = 0.65, b = 1, sw = 0.006 },
            new Shaft { c = -0.17, h = 0.022, k = 0.55, b = 2, sw = 0.005 },
            new Shaft { c = -0.07, h = 0.05, k = 1, b = 1, sw = 0.008 },
            new Shaft { c = 0.03, h = 0.018, k = 0.6, b = 3, sw = 0.004 },
            new Shaft { c = 0.12, h = 0.042, k = 0.85, b = 2, sw = 0.007 },
            new Shaft { c = 0.23, h = 0.026, k = 0.6, b = 1, sw = 0.005 },
        };

        static readonly float[] STR = new float[512];

        // light at a point: the shafts it's in, softened at their edges, fading with distance, threaded with strands
        static double LightAt(double x, double y, Shaft[] sh)
        {
            double dx = x - SX, dy = y - SY, r = dx * AX + dy * AY, ang = (dx * NX + dy * NY) / r;
            double e = 0;
            foreach (Shaft s in sh)
            {
                double q = Math.Abs(ang - s.c) / s.h;
                if (q < 1) e += s.k * Px.Pow(1 - q * q, 1.5);
            }
            if (e <= 0) return 0;
            double strand = STR[(int)Clamp(Math.Floor((ang + 0.36) * 700), 0, 511)], fall = Clamp(1.25 - (r - 230) / 420);
            return e * fall * (0.7 + 0.45 * strand);
        }

        readonly Options o;

        GodRays(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 7);
            Shaft[] sh = Shafts(t, S);
            // dark air the light falls through, a dark floor it lands on; each beam posterized into a white-hot core, a
            // gold body and a soft translucent edge, brightest near the sun and fading as it falls
            uint[] L = Array.ConvertAll(C, c => Mix(C[0], c, 0.75));
            int n = sh.Length;
            float[] SC = new float[n], SI = new float[n], SK = new float[n];
            for (int j = 0; j < n; j++)
            {
                SC[j] = (float)sh[j].c;
                SI[j] = (float)(1 / sh[j].h);
                SK[j] = (float)sh[j].k;
            }
            Rows(p, Module, -170, 9, (y, R) =>
            {
                bool floor = y > 0;
                double dy = y - SY, air = floor ? 0.8 : 0.66, boost = floor ? 1.6 : 1;
                double r = (-160 + 0.5 - SX) * AX + dy * AY, nn = (-160 + 0.5 - SX) * NX + dy * NY;
                Row.Fill(R.a, air);
                Row.Fill(R.b, 1);
                Row.Fill(R.c, C[0]);
                for (int i = 0; i < W; i++, r += AX, nn += NX)
                {
                    double ang = nn / r;
                    double e = 0;
                    for (int j = 0; j < n; j++)
                    {
                        double q2 = (ang - SC[j]) * SI[j];
                        if (q2 > -1 && q2 < 1)
                        {
                            double w = 1 - q2 * q2;
                            e += SK[j] * w * Math.Sqrt(w);
                        }
                    }
                    if (e <= 0) continue;
                    double fall = 1.35 - (r - 200) / 330;
                    int si = ToInt32((ang + 0.36) * 700);
                    e *= (fall > 1 ? 1 : fall < 0 ? 0 : fall) * (0.7 + 0.45 * STR[si < 0 ? 0 : si > 511 ? 511 : si]) * boost;
                    if (e < 0.06) continue;
                    int lv = e > 1.05 ? 6 : e > 0.78 ? 5 : e > 0.52 ? 4 : e > 0.3 ? 3 : 2;
                    R.c[i] = floor ? C[lv] : L[lv];
                    R.a[i] = (float)(floor ? 0.85 : Math.Min(0.78, 0.12 + e * 0.5));
                    R.l[i] = C[4];
                    R.k[i] = (float)(e * (floor ? 0.32 : 0.26));
                }
            });
            // the sun's glare where the beams come in
            q.Glow(-168, -178, 130, C[3], 0.3);
            q.Glow(-168, -178, 50, C[5], 0.35);
            Motes(q, t, S, C, sh, false);
        }

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 7);
            Motes(q, t, S, C, Shafts(t, S), true);
        }

        // each shaft breathes and sways on whole cycles; the strands drift slowly across the beams
        static Shaft[] Shafts(double t, double S)
        {
            for (int i = 0; i < 512; i++)
            {
                double a = i / 512.0;
                STR[i] = (float)(0.5 + 0.5 * Px.Sin(a * 190 + Px.Sin((TAU * t) / D) * 2 + S) * Px.Sin(a * 77 - (TAU * t) / D + S * 2));
            }
            var r = new Shaft[SHAFTS.Length];
            for (int i = 0; i < SHAFTS.Length; i++)
            {
                Shaft s = SHAFTS[i];
                r[i] = new Shaft
                {
                    c = s.c + s.sw * Px.Sin((TAU * t) / D + i * 1.3),
                    h = s.h * (1 + 0.15 * Px.Sin((TAU * s.b * t) / D + i)),
                    k = s.k * (0.72 + 0.28 * Px.Sin((TAU * s.b * t) / D + i * 2.1)),
                };
            }
            return r;
        }

        // dust motes drifting slowly, invisible in the dark, catching the light as they cross a beam
        static void Motes(Clip q, double t, double S, uint[] C, Shaft[] sh, bool front)
        {
            for (int i = front ? 0 : 50; i < (front ? 50 : 160); i++)
            {
                double R(double k) => Rand(S, i, k);
                double n1 = 1 + Math.Floor(R(1) * 2), n2 = 1 + Math.Floor(R(2) * 2);
                double x = -170 + R(3) * 340 + (8 + R(4) * 14) * Px.Sin((TAU * n1 * t) / D + R(5) * TAU);
                double y = -170 + R(6) * 172 + (6 + R(7) * 10) * Px.Sin((TAU * n2 * t) / D + R(8) * TAU) + 4 * Px.Sin((TAU * 3 * t) / D + i);
                double e = LightAt(x, y, sh);
                if (e < 0.12) continue;
                bool big = front && R(9) < 0.5;
                double k = Math.Min(1, e);
                q.Px(x, y, k > 0.6 ? WHITE : k > 0.3 ? C[5] : C[4]);
                if (big && k > 0.5)
                {
                    q.Px(x + 1, y, C[4]);
                    q.Px(x, y + 1, C[4]);
                }
                q.Glow(x, y, big ? 6 : 4, C[4], 0.4 * k);
            }
        }
    }
}
