// The banded fog (a baked tiling noise, warped, cut into a few translucent tones with Bayer edges, a lit crest
// wherever a band's top meets thinner fog) is adapted from Sprite Fusion's open-source fog
// (https://www.spritefusion.com/pixel-art-vfx/fog), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Fog : Effect
    {
        public static readonly Module Module = new Module("fog", "pixelfx/fx/weather/fog", 320, 180, 160, 170, 12, true, 30,
            new Options
            {
                ["palette"] = new[] { "#14121e", "#24213a", "#363250", "#4f4a6c", "#6d688b", "#918cab", "#bdb8d4", "#ece8f6" },
                ["seed"] = 5,
            },
            (o, e) => new Fog(o))
        {
            Title = "Fog",
            Desc = "Slow banks of moonlit fog at three depths drift past each other, crests catching the light, while thin streaks of mist slide by in front.",
            Tags = new[] { "weather", "fog", "mist", "haze", "night", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 12;
        const int W = 320;
        static readonly double[] T = { 0.3, 0.5, 0.7 };
        const double BW = 0.06;

        sealed class Bank
        {
            public double top, bse, bot, sx, sy, lap, alpha, warp;
            public int[] tone;
            public int crest, crestFrom;
        }

        // far to near: where each bank's top thins out (top..base), how far down it reaches, noise scale (the tile it
        // scrolls each loop is 256 / sx px wide), tiles a loop, how opaque, tones [thin, body, dense], crest tone
        static readonly Bank[] BANKS =
        {
            new Bank { top = -84, bse = -40, bot = -20, sx = 1.6, sy = 2.2, lap = 1, alpha = 0.24, tone = new[] { 2, 3, 4 }, crest = 5, warp = 16, crestFrom = 3 },
            new Bank { top = -62, bse = -24, bot = -2, sx = 1.0667, sy = 2.5, lap = 1, alpha = 0.3, tone = new[] { 2, 3, 4 }, crest = 5, warp = 22, crestFrom = 2 },
            new Bank { top = -42, bse = -8, bot = 10, sx = 0.8, sy = 2.8, lap = 1, alpha = 0.36, tone = new[] { 2, 3, 5 }, crest = 6, warp = 28, crestFrom = 1 },
        };

        static readonly float[] BR = new float[W], WARP = new float[W];
        static readonly float[][] P1 = { new float[W], new float[W], new float[W] }, P2 = { new float[W], new float[W], new float[W] };

        readonly Options o;

        Fog(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            DrawBank(p, t, S, C, 0);
            DrawBank(p, t, S, C, 1);
        }

        public override void Draw(Painter p, double t)
        {
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            DrawBank(p, t, S, C, 2);
            Streaks(p, t, S, C, 1);
        }

        static void DrawBank(Painter p, double t, double S, uint[] C, int n)
        {
            Bank B = BANKS[n];
            Field F = MakeField(S * 7 + n, 256, 128), G = MakeField(S * 7 + 11, 256, 128, new[] { 4, 8, 16 });
            double drift = (256 * B.lap * t) / D, boil = (128 * t) / D;
            float[] prev1 = P1[n], prev2 = P2[n];
            uint[] tones = Array.ConvertAll(B.tone, i => C[i]);
            uint crest = Mix(C[B.crest], WHITE, 0.12 + 0.08 * n);
            Array.Clear(prev1, 0, W);
            Array.Clear(prev2, 0, W);
            // the fog breathes: its top rises and falls a little along its length
            for (int i = 0; i < W; i++) BR[i] = (float)(2 * Px.Sin((TAU * t) / D + (i - 160) * 0.006 + n * 1.7));
            Rows(p, Module, B.top - 2, B.bot, (y, R) =>
            {
                double prof = Smooth(B.top, B.bse, y) * (1 - Smooth(B.bot - (B.bot < 10 ? 14 : 4), B.bot + 1, y));
                for (int i = 0; i < W; i += 2) WARP[i] = WARP[i + 1] = (float)(Sample(G, i * 0.35 + n * 70, y * 0.5 + boil) - 0.5);
                for (int i = 0; i < W; i++)
                {
                    double x = i - 160 + 0.5, w = WARP[i];
                    // the noise is pushed hard and clamped so it, not the height, shapes the fog: blobs, not stripes
                    double nn = Clamp((Sample(F, x * B.sx + drift + w * B.warp, (y + BR[i]) * B.sy + w * 8) - 0.5) * 2.6 + 0.5) - 0.5;
                    double f = prof <= 0.01 ? 0 : (prof * 0.72 + nn * 0.95) * (0.6 + 0.5 * prof);
                    double above = prev2[i];
                    prev2[i] = prev1[i];
                    prev1[i] = (float)f;
                    if (f <= T[0]) continue;
                    double th = Bayer(i, y) * BW;
                    int band = f > T[2] + th ? 2 : f > T[1] + th ? 1 : 0, solid = f > T[2] ? 2 : f > T[1] ? 1 : 0;
                    // a lit crest, one pixel thick, where this band's top meets thinner fog
                    if (solid >= B.crestFrom && above < T[solid])
                    {
                        R.c[i] = crest;
                        R.a[i] = (float)(B.alpha * 0.95);
                        R.l[i] = C[6];
                        R.k[i] = 0.07f;
                    }
                    else
                    {
                        // dense fog scatters the most moonlight: it's the brightest part, with a little glow
                        R.c[i] = tones[band];
                        R.a[i] = (float)(B.alpha * (0.55 + 0.25 * band));
                        if (band == 2)
                        {
                            R.l[i] = C[5];
                            R.k[i] = (float)(0.03 + 0.02 * n);
                        }
                    }
                    R.b[i] = 1;
                }
            });
        }

        // thin streaks of mist sliding by faster than the banks, a few behind the near bank and a few in front
        static void Streaks(Painter p, double t, double S, uint[] C, int front)
        {
            // one tile a loop: the back streaks slide at 43 px/s, the front ones at 53
            Field F = MakeField(S * 7 + 20 + front, 256, 128);
            double drift = (256 * t) / D, sx = front != 0 ? 0.4 : 0.5, y0 = front != 0 ? -40 : -96, y1 = front != 0 ? 6 : -30;
            Rows(p, Module, y0, y1, (y, R) =>
            {
                double env = Smooth(y0, y0 + 10, y) * (1 - Smooth(y1 - 10, y1, y));
                for (int i = 0; i < W; i++)
                {
                    double x = i - 160 + 0.5, s = (Sample(F, x * sx + drift, y * 6.5 + 30) - 0.64) * 3 * env;
                    if (s <= 0.05) continue;
                    R.c[i] = s > 0.35 ? C[5] : C[4];
                    R.a[i] = (float)Math.Min(0.4, s);
                    R.b[i] = 1;
                    R.l[i] = C[5];
                    R.k[i] = (float)(s * 0.05);
                }
            });
        }
    }
}
