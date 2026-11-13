// The banded light (posterized steps from a white-hot core out to deep color, fine strands running through it,
// pulses travelling along, light laid over the top) is adapted from Sprite Fusion's open-source holy light
// (https://www.spritefusion.com/pixel-art-vfx/holy-light), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Aurora : Effect
    {
        public static readonly Module Module = new Module("aurora", "pixelfx/fx/weather/aurora", 320, 180, 160, 180, 12, true, 30,
            new Options
            {
                ["palette"] = new[] { "#081a1e", "#0d3a36", "#13664e", "#1fa06a", "#3fd88e", "#9af7bf", "#e6fff0" },
                ["upper"] = new[] { "#120b2a", "#26164f", "#41208a", "#6a34c0", "#9c62e8", "#d2b4ff", "#f6eeff" },
                ["seed"] = 3,
            },
            (o, e) => new Aurora(o))
        {
            Title = "Aurora",
            Desc = "Rippling curtains of green-to-violet light over a starfield: folding hems burn bright, rays shimmer up into violet, and the whole sky slowly breathes.",
            Tags = new[] { "weather", "aurora", "northern lights", "sky", "night", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 12;
        const int W = 320, H = 180;
        static readonly double[] BAY = Array.ConvertAll(new[] { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }, v => (v + 0.5) / 16);
        static readonly float[] I = new float[W * H], HV = new float[W * H], BL = new float[W * H], TMP = new float[W * H];

        // a curtain's light from its hem (v = 0) up its rays (v = 1): a bright hem, a body that holds, rays fading up
        // into the dark. Tabled in steps of 1/200. Below the hem there's only a skirt of glow a few pixels deep.
        static readonly float[] PROF = MakeProf();

        static float[] MakeProf()
        {
            var r = new float[201];
            for (int i = 0; i < 201; i++)
            {
                double v = i / 200.0;
                r[i] = (float)(v < 0.035 ? 2.6 : v < 0.22 ? 1.15 - v : Px.Pow(1 - v, 1.5) * 0.9);
            }
            return r;
        }

        // a soft bloom of the light: a box blur of radius r, across then down, by running sums
        static void Blur(float[] src, float[] dst, int r)
        {
            int n = 2 * r + 1;
            for (int y = 0; y < H; y++)
            {
                double acc = 0;
                int o = y * W;
                for (int x = -r; x < W + r; x++)
                {
                    if (x + r < W) acc += src[o + Math.Min(W - 1, x + r)];
                    if (x - r - 1 >= 0) acc -= src[o + x - r - 1];
                    if (x >= 0 && x < W) TMP[o + x] = (float)(acc / n);
                }
            }
            for (int x = 0; x < W; x++)
            {
                double acc = 0;
                for (int y = -r; y < H + r; y++)
                {
                    if (y + r < H) acc += TMP[Math.Min(H - 1, y + r) * W + x];
                    if (y - r - 1 >= 0) acc -= TMP[(y - r - 1) * W + x];
                    if (y >= 0 && y < H) dst[y * W + x] = (float)(acc / n);
                }
            }
        }

        static double Cyc(double t, double c, double ph = 0) => (TAU * c * t) / D + ph;

        sealed class Curtain
        {
            public double y, slope, amp, lam, fold, flam, height, k, s0, s1, rip, hue;
            public double[] c;
        }

        // the curtains, back to front: where each hangs, how it folds and ripples, how tall its rays reach
        static readonly Curtain[] CURTAINS =
        {
            new Curtain { y = -132, slope = -0.08, amp = 8, lam = 210, fold = 14, flam = 150, height = 40, k = 0.6, s0 = -210, s1 = 210, rip = 1.6, c = new double[] { 1, 1, 2 }, hue = 0.35 },
            new Curtain { y = -88, slope = 0.22, amp = 13, lam = 250, fold = 19, flam = 165, height = 82, k = 1.5, s0 = -195, s1 = 180, rip = 2.4, c = new double[] { 1, 2, 3 }, hue = 0 },
            new Curtain { y = -38, slope = -0.05, amp = 5, lam = 150, fold = 10, flam = 110, height = 20, k = 0.45, s0 = -210, s1 = 210, rip = 1.4, c = new double[] { 2, 1, 3 }, hue = 0 },
        };

        sealed class Ray
        {
            public double x, hem, h, k;
        }

        readonly Options o;

        Aurora(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            var B = q.B;
            double S = o.Seed;
            uint[] G = Fit(o.Ramp("palette"), 7), U = Fit(o.Ramp("upper"), 7);
            Stars(q, t, S, G, U);
            Array.Clear(I, 0, I.Length);
            Array.Clear(HV, 0, HV.Length);
            Field F = MakeField(S * 5 + 1, 256, 128, new[] { 8, 16, 32 });
            double breath = 0.75 + 0.25 * Px.Sin(Cyc(t, 1, -1.2));
            var rays = new List<Ray>();
            for (int n = 0; n < CURTAINS.Length; n++) DrawCurtain(CURTAINS[n], n, t, F, breath, rays);
            Blur(I, BL, 5);
            // shade: hue by height (green at the hem, violet up the rays), brightness by intensity in posterized steps.
            // Faint light is only light, so the stars shine through it; the bright parts get crisp color too.
            var GM = new uint[G.Length];
            for (int i = 0; i < G.Length; i++) GM[i] = Mix(G[i], U[i], 0.5);
            Rows(p, Module, B.y0, -12, (y, R) =>
            {
                int fy = ToInt32(Math.Floor(y)), bs = (fy - B.y0) * W, by = (fy & 3) << 2;
                for (int xi = 0; xi < W; xi++)
                {
                    int i = bs + xi;
                    double raw = I[i], bl = BL[i];
                    double k = bl > 0.015 ? Math.Min(0.4, bl * 0.6) : 0;
                    if (raw >= 0.03)
                    {
                        double e = (raw * 1.5) / (1 + raw * 1.5), v = HV[i] / raw, th = e > 0.45 ? BAY[by | (xi & 3)] * 0.06 : 0;
                        int lv = e > 0.86 + th ? 6 : e > 0.72 + th ? 5 : e > 0.56 + th ? 4 : e > 0.4 + th ? 3 : e > 0.24 ? 2 : 1;
                        if (v > 0.35) lv = Math.Min(lv, 5);
                        uint c = v < 0.3 ? G[lv] : v < 0.5 ? GM[lv] : U[lv];
                        if (lv >= 3)
                        {
                            R.c[xi] = c;
                            R.a[xi] = (float)Clamp((e - 0.3) * 2.2);
                            R.b[xi] = (byte)(lv < 5 ? 1 : 0);
                        }
                        R.l[xi] = lv >= 3 ? (v < 0.4 ? G[4] : U[4]) : c == G[lv] ? G[4] : c == U[lv] ? U[4] : GM[4];
                        k += e * (lv >= 3 ? 0.28 : 0.72);
                    }
                    else R.l[xi] = G[4];
                    R.k[xi] = (float)k;
                }
            });
            // the brightest rays as fine strands of light, fading up into violet
            foreach (Ray r in rays)
            {
                double n = Round(r.h);
                for (int j = 1; j < n; j++)
                {
                    double u = j / n;
                    q.Blend(r.x, r.hem - j, u < 0.35 ? G[5] : u < 0.6 ? Mix(G[5], U[5], 0.5) : U[5], r.k * Px.Pow(1 - u, 1.3));
                }
                q.Px(r.x, r.hem, G[6]);
            }
        }

        // one curtain: walk along it, drop each ray's light into the columns it covers; where it folds back on itself
        // along the line of sight the light piles up, so folds burn brightest
        static void DrawCurtain(Curtain c, int n, double t, Field F, double breath, List<Ray> rays)
        {
            const double X0 = -160, Y0 = -180;
            double fast = (256 * 2 * t) / D, slow = (256 * t) / D;
            double p1 = 0, p2 = 0, lastX = -999;
            for (double s = c.s0; s < c.s1; s += 0.5)
            {
                double x = s + c.fold * Px.Sin((TAU * s) / c.flam + Cyc(t, c.c[0], n * 2)) + 5 * Px.Sin((TAU * s) / 64 + Cyc(t, c.c[2], n));
                if (x < X0 - 2 || x >= X0 + W + 1) continue;
                double hem = c.y + c.slope * s + c.amp * Px.Sin((TAU * s) / c.lam + Cyc(t, c.c[1], n * 1.7)) + c.rip * Px.Sin((TAU * s) / 23 - Cyc(t, 7, n));
                double env = Smooth(c.s0, c.s0 + 60, s) * (1 - Smooth(c.s1 - 70, c.s1, s));
                // rays: fine strands drifting along the curtain, each with its own height; bright patches running along it
                double r1 = Clamp((Sample(F, s * 5 - fast + n * 40, 10 + n * 30) - 0.38) * 3.2), r2 = Clamp((Sample(F, s * 4.3 + fast * 0.7 + n * 90, 50 + n * 13) - 0.35) * 3);
                double patch = Sample(F, s * 0.7 + slow + n * 17, 80 + n * 9);
                double k = c.k * env * breath * (0.2 + 0.8 * r1) * (0.3 + 1.4 * Math.Max(0, patch - 0.3));
                if (k < 0.01) continue;
                double h = c.height * (0.45 + 0.75 * r2) * (0.75 + 0.5 * patch), xf = Math.Floor(x - X0), fr = x - X0 - xf;
                // a ray peaks here: keep it as a strand; and a soft bloom along the bright hem
                if (p1 > 0.75 && p1 >= p2 && p1 >= r1 && Math.Abs(x - lastX) > 2 && k > 0.25)
                {
                    rays.Add(new Ray { x = x, hem = hem - 1, h = h * 0.9, k = Math.Min(0.85, k * 0.9) });
                    lastX = x;
                }
                p2 = p1;
                p1 = r1;
                if (xf < -1 || xf >= W) continue;
                int xi = (int)xf;
                double top = Math.Max(0, Math.Floor(hem - h - Y0)), bot = Math.Min(H - 1, Math.Floor(hem - Y0) + 3), inv = 1 / h, k0 = k * 0.5 * (1 - fr), k1 = k * 0.5 * fr;
                double v = (hem - Y0 - 0.5 - top) * inv;
                for (double yf = top; yf <= bot; yf++, v -= inv)
                {
                    // above the hem: the ray; below it: a skirt of glow gone within three pixels
                    double pr = v >= 0 ? PROF[Math.Min(200, ToInt32(v * 200))] : Math.Max(0, 0.45 + v * h * 0.16);
                    int o = (int)yf * W + xi;
                    double hv = v + c.hue < 0 ? 0 : v + c.hue > 1 ? 1 : v + c.hue;
                    if (pr <= 0) continue;
                    if (xi >= 0)
                    {
                        I[o] = (float)(I[o] + k0 * pr);
                        HV[o] = (float)(HV[o] + k0 * pr * hv);
                    }
                    if (xi + 1 < W)
                    {
                        I[o + 1] = (float)(I[o + 1] + k1 * pr);
                        HV[o + 1] = (float)(HV[o + 1] + k1 * pr * hv);
                    }
                }
            }
        }

        // the starfield: dim specks, steady stars, a few bright ones that twinkle with a glint
        static void Stars(Clip q, double t, double S, uint[] G, uint[] U)
        {
            for (int i = 0; i < 150; i++)
            {
                double x = -160 + Rand(S, i, 1) * 320, y = -180 + Rand(S, i, 2) * 175, r = Rand(S, i, 3), c = 1 + Math.Floor(Rand(S, i, 4) * 4);
                double tw = 0.5 + 0.5 * Px.Sin(Cyc(t, c, Rand(S, i, 5) * TAU));
                uint col = r < 0.6 ? Mix(U[3], WHITE, 0.2) : r < 0.9 ? Mix(U[5], WHITE, 0.5) : WHITE;
                if (r < 0.6) q.Dot(x, y, col, 0.35 + 0.5 * tw);
                else if (r < 0.95)
                {
                    q.Px(x, y, col);
                    q.Glow(x + 0.5, y + 0.5, 3, U[5], 0.25 * tw);
                }
                else Sparkle(q, x, y, 1 + Round(tw * 2), Mix(U[5], WHITE, 0.6), U[4], 0.35 * tw);
            }
        }
    }
}
