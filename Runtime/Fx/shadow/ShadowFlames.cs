// Tongues built from nested layers, each narrower and shorter than the last, are adapted from Sprite Fusion's
// open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
// Here the layers run the other way: a pale skin outside, violet within, a black heart at the core.
using System;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class ShadowFlames : Effect
    {
        public static readonly Module Module = new Module("shadow-flames", "pixelfx/fx/shadow/shadow-flames", 104, 112, 52, 102, 2.4, true, 30,
            new Options { ["palette"] = VOID, ["seed"] = 12 },
            (o, e) => new ShadowFlames(o))
        {
            Title = "Shadow flames",
            Desc = "Inside-out fire on the floor: a black heart wrapped in violet flame with pale skin and tips, shedding embers and dark smoke.",
            Tags = new[] { "shadow", "fire", "flame", "ground", "dark", "magic", "loop" },
            Bg = "ground",
        };

        const double D = 2.4;
        const int GX = 22, GY = 60;
        // tongues: offset, base height, width, seed; the middle tallest
        static readonly double[][] TONGUES = { new[] { -11, 20, 4.2, 1 }, new[] { 11, 22, 4.4, 2 }, new[] { -6, 31, 5.4, 3 }, new[] { 6, 29, 5.2, 4 }, new[] { 0, 40, 6.6, 5 } };
        // layers from the outside in: width share, height share, which tone (3 is the black heart)
        static readonly double[][] LAYERS = { new double[] { 1, 1, 0 }, new[] { 0.8, 0.88, 1 }, new[] { 0.54, 0.68, 2 }, new[] { 0.32, 0.48, 3 } };
        // the tongues, outermost first
        static readonly int[] ORDER = Enumerable.Range(0, TONGUES.Length).OrderByDescending(i => Math.Abs(TONGUES[i][0])).ToArray();

        // noise that comes back to where it started each loop: a circle through 2D noise, offset by v
        static double LNoise(double t, double v, double seed, double speed = 5) => Noise2(Px.Cos((TAU * t) / D) * speed + v, Px.Sin((TAU * t) / D) * speed, seed);

        readonly uint[] C;
        readonly double S;

        ShadowFlames(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            S = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            // the scorched floor: a dark burn, violet light pooled on it
            p.Ellipse(0, 0.5, 22, 3.2, (d, g) => d < 0.6 ? C[0] : C[1], (d, g) => d < 0.85 ? 1 : (1 - d) * 6);
            p.Glow(0, -2, 46, C[4], 0.3);
            // dark smoke climbing off the tips, a lit rim on its underside
            Cycle(t, D, 14, S + 1, r => r.Range(1.2, 1.9), (age, L, r, i) =>
            {
                double u = age / L, x = r.Range(-6, 6) + Px.Sin(u * 4 + r.Range(0, 6)) * 5 * u, y = -30 - u * r.Range(36, 52), rad = 3 + u * 6;
                double a = Math.Min(1, u * 6) * (1 - u) * 0.7;
                p.Disc(x, y, rad, Mix(Mix(C[3], C[2], 0.5), C[1], u), a);
                p.Disc(x - rad * 0.2, y - rad * 0.3, rad * 0.5, C[3], a * 0.4);
                p.Disc(x, y + rad * 0.45, rad * 0.55, C[5], a * 0.45 * (1 - u));
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] TONE = { C[6], C[4], C[2], C[0] };
            // rasterize the tongues, outermost first, into a grid of tones
            const int GW = GX * 2;
            var grid = new sbyte[GW * GY];
            for (int q = 0; q < grid.Length; q++) grid[q] = -1;
            void Set(double x, double y, int v)
            {
                double i = Round(x) + GX, j = Round(y) + GY;
                if (i >= 0 && j >= 0 && i < GW && j < GY) grid[(int)j * GW + (int)i] = (sbyte)v;
            }
            foreach (int ti in ORDER)
            {
                double dx = TONGUES[ti][0], H0 = TONGUES[ti][1], w = TONGUES[ti][2], n = TONGUES[ti][3];
                double h = H0 * (0.78 + 0.22 * LNoise(t, n * 3, n, 4)), sway = LNoise(t, n * 7, n + 9, 3) * 3;
                foreach (double[] layer in LAYERS)
                {
                    double lw = layer[0], lh = layer[1];
                    int tone = (int)layer[2];
                    double Hl = h * lh;
                    for (int r = 0; r <= Hl; r++)
                    {
                        double u = r / Math.Max(1, Hl);
                        double half = w * lw * Px.Pow(1 - u, 0.85) * Math.Min(1, (r + 1.5) / 3.5) + LNoise(t, r * 0.45 + n, n + 20, 6) * 1.1 + LNoise(t, r * 1.3 + n * 5, n + 50, 9) * 0.6;
                        double cx = dx + sway * u * u * lh + LNoise(t, r * 0.2 + n * 2, n + 30, 5) * u * 1.4;
                        if (half < 0.3) continue;
                        for (double x = Round(cx - half); x <= Round(cx + half); x++)
                        {
                            // the skin breaks up near the top: pixels flicker out of it
                            if (tone == 0 && u > 0.45 && LNoise(t, x * 0.9 + r * 0.7 + n, n + 60, 8) > 0.35) continue;
                            Set(x, -r, tone);
                        }
                    }
                }
            }
            // draw it: the pale skin whitening to its tips, the black heart pure black
            for (int j = 0; j < GY; j++)
            for (int i = 0; i < GW; i++)
            {
                int v = grid[j * GW + i];
                if (v < 0) continue;
                int x = i - GX, y = j - GY;
                uint c = TONE[v];
                int above = (j - 1) * GW + i;
                if (v == 0 && y < -24 && (above >= 0 ? grid[above] : -1) < 0) c = y < -32 ? WHITE : C[7];
                p.Px(x + 0.5, y + 0.5, c);
            }
            // licks of pale flame breaking off the tips
            Cycle(t, D, 34, S, r => r.Range(0.25, 0.5), (age, L, r, i) =>
            {
                double u = age / L;
                int k = r.Int(0, 4);
                double dx = TONGUES[k][0], H0 = TONGUES[k][1];
                double x = dx + r.Range(-4, 4) + u * r.Range(-5, 5), y = -H0 * r.Range(0.45, 0.85) - u * r.Range(10, 20);
                double rad = r.Range(1, 2.2) * (1 - u) + 0.3;
                p.Circle(x, y, rad + 0.6, u < 0.5 ? C[6] : C[4]);
                if (rad > 1) p.Circle(x, y + 0.4, rad * 0.5, u < 0.3 ? C[2] : C[4]);
            });
            // embers drifting up, flickering, cooling to violet
            Cycle(t, D, 30, S + 2, r => r.Range(1, 1.8), (age, L, r, i) =>
            {
                double u = age / L, x = r.Range(-12, 12) + Px.Sin(u * 6 + r.Range(0, 6)) * 5 * u, y = -10 - u * r.Range(40, 70);
                if (u > 0.7 && Px.Sin(age * 40 + x) < 0) return;
                p.Px(x, y, u < 0.3 ? WHITE : u < 0.6 ? C[7] : C[6]);
                p.Glow(x, y, 3, C[6], 0.35 * (1 - u));
            });
            // light: a violet bloom round it, but none on the black heart
            double fl = 0.85 + 0.15 * LNoise(t, 0, 77, 6);
            p.Glow(0, -16, 52, C[4], 0.5 * fl);
            p.Glow(0, -12, 26, C[6], 0.34 * fl);
            Frame f = p.Frame;
            for (int j = 0; j < GY; j++)
            for (int i = 0; i < GW; i++)
                if (grid[j * GW + i] == 3)
                {
                    long sx = (long)p.CellX(i - GX + 0.5, j - GY + 0.5) - f.Ox, sy = (long)p.CellY(i - GX + 0.5, j - GY + 0.5) - f.Oy;
                    if (sx >= 0 && sy >= 0 && sx < f.W && sy < f.H) f.Light[sy * f.W + sx] = 0;
                }
        }
    }
}
