// Banded volumes with a lit crest (light on the top edge, tones stepping down into the body, Bayer at the steps)
// after Sprite Fusion's open-source fog (https://www.spritefusion.com/pixel-art-vfx/fog), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class Clouds : Effect
    {
        public static readonly Module Module = new Module("clouds", "pixelfx/fx/weather/clouds", 320, 180, 160, 180, 12, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0c0c18", "#16172a", "#232540", "#363a5a", "#545a80", "#8088ae", "#bcc4e2", "#f2f4ff" },
                ["seed"] = 7,
            },
            (o, e) => new Clouds(o))
        {
            Title = "Clouds",
            Desc = "Layers of clouds drift past a bright moon at night: silver-lit tops, dark bellies, edges that flare where they cross the moon, forming and thinning as they go.",
            Tags = new[] { "weather", "clouds", "moon", "night", "sky", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 12, MX = 92, MY = -128, MR = 12;

        sealed class Deck
        {
            public int n;
            public double[] w, h, y, life;
            public double v, alpha;
            public int[] tone;
        }

        // far to near: how many clouds, how wide, how tall their puffs, how high, drift speed, how long they live,
        // tones [belly, body, upper, top], how see-through
        static readonly Deck[] LAYERS =
        {
            new Deck { n = 7, w = new double[] { 40, 70 }, h = new double[] { 6, 11 }, y = new double[] { -150, -110 }, v = 3, life = new double[] { 8, 11 }, tone = new[] { 2, 3, 4, 5 }, alpha = 0.7 },
            new Deck { n = 6, w = new double[] { 60, 100 }, h = new double[] { 9, 15 }, y = new double[] { -120, -70 }, v = 6, life = new double[] { 8, 11 }, tone = new[] { 1, 2, 4, 6 }, alpha = 0.88 },
            new Deck { n = 5, w = new double[] { 90, 140 }, h = new double[] { 12, 20 }, y = new double[] { -80, -30 }, v = 10, life = new double[] { 9, 12 }, tone = new[] { 1, 2, 3, 6 }, alpha = 1 },
        };

        readonly Options o;

        Clouds(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 8);
            Moon(q, C);
            for (int j = 0; j < LAYERS.Length; j++)
                for (int i = 0; i < LAYERS[j].n; i++)
                    Cloud(q, t, S, C, LAYERS[j], j, i);
            // the moon's light over everything: a halo, brightest close in, catching any cloud that passes
            q.Glow(MX, MY, 110, C[4], 0.14);
            q.Glow(MX, MY, 40, C[5], 0.25);
            q.Glow(MX, MY, 18, C[6], 0.3);
        }

        static void Moon(Clip q, uint[] C)
        {
            q.Shade(MX - MR - 1, MY - MR - 1, MX + MR + 1, MY + MR + 1, (x, y, o) =>
            {
                double dx = x - MX, dy = y - MY, d = Hypot(dx, dy) / MR;
                if (d > 1) return 0;
                // a pale disc with darker seas, a bright limb on the lit side
                bool sea = Px.Pow(dx + 4, 2) + Px.Pow(dy + 2, 2) < 14 || Px.Pow(dx - 3, 2) + Px.Pow(dy - 4, 2) < 9 || Px.Pow(dx - 5, 2) + Px.Pow(dy + 5, 2) < 4;
                o.c = d > 0.86 ? (dx + dy < 0 ? C[6] : WHITE) : sea ? C[5] : d < 0.5 && dx + dy < 0 ? WHITE : C[7];
                return 0;
            });
        }

        // one cloud: born once a loop, it swells into shape, drifts, and thins away. Its outline is a row of puffs on a
        // flat base; shading steps down from a silver top to a dark belly, and edges near the moon flare
        static void Cloud(Clip q, double t, double S, uint[] C, Deck L, int j, int i)
        {
            double R(double k) => Rand(S, j, i, k);
            double life = L.life[0] + R(1) * (L.life[1] - L.life[0]), age = Wrap(t - R(2) * D, D);
            if (age >= life) return;
            double u = age / life, grow = Math.Min(1, u * 4) * Math.Min(1, (1 - u) * 4), w = (L.w[0] + R(3) * (L.w[1] - L.w[0])) * (0.6 + 0.4 * grow);
            double x0 = -200 + R(4) * 330 + L.v * age, bse = L.y[0] + R(5) * (L.y[1] - L.y[0]), H = (L.h[0] + R(6) * (L.h[1] - L.h[0])) * grow;
            if (H < 1.5) return;
            // a row of puffs along the base, and a tier of bigger ones heaped in the middle
            double n = 4 + Math.Floor(R(7) * 4);
            var puffs = new List<double[]>();
            for (int k = 0; k < n; k++)
            {
                double f = (k + 0.5) / n, r = H * (0.5 + 0.35 * Px.Sin(Math.PI * f)) * (0.75 + 0.5 * R(10 + k)) * (1 + 0.06 * Px.Sin((TAU * t) / D + k + i));
                puffs.Add(new[] { x0 + w * (f - 0.5) + (R(20 + k) - 0.5) * 6, bse - r * 0.3 - R(30 + k) * H * 0.15, r });
            }
            for (int k = 0; k < 1 + Math.Floor(R(8) * 3); k++)
            {
                double f = 0.3 + R(40 + k) * 0.4, r = H * (0.75 + 0.35 * R(50 + k)) * (1 + 0.05 * Px.Sin((TAU * t) / D + k * 2 + i));
                puffs.Add(new[] { x0 + w * (f - 0.5), bse - H * 0.55 - r * 0.2, r });
            }
            double x1 = x0 - w / 2 - H, x2 = x0 + w / 2 + H;
            var B = q.B;
            for (double x = Math.Max(B.x0, Math.Floor(x1)); x <= Math.Min(B.x1 - 1, Math.Ceiling(x2)); x++)
            {
                double cx = x + 0.5;
                double top = double.PositiveInfinity, top2 = double.PositiveInfinity;
                foreach (double[] pf in puffs)
                {
                    double px = pf[0], py = pf[1], r = pf[2], dx = cx - px;
                    if (dx * dx >= r * r) continue;
                    double y = py - Math.Sqrt(r * r - dx * dx);
                    if (y < top)
                    {
                        top2 = top;
                        top = y;
                    }
                    else if (y < top2) top2 = y;
                }
                if (double.IsPositiveInfinity(top)) continue;
                // a belly that's nearly flat, lifting at the ends and rippling a little
                double end = Math.Min(cx - x1, x2 - cx) / H, bot = bse + 1.5 + Px.Sin(cx * 0.21 + i) * 0.8 - (end < 1 ? (1 - end) * 4 : 0);
                if (bot <= top) continue;
                double h = bot - top, inner = top2 - top > 2.5 && top2 < bot - 2 ? Math.Floor(top2) : -999;
                for (double y = Math.Floor(top); y < bot; y++)
                {
                    double d = y + 0.5 - top, v = d / h, th = Bayer(x, y) * 0.12, md = Hypot(cx - MX, y - MY);
                    int c = d < 1 ? L.tone[3] : v < 0.25 + th ? L.tone[2] : v < 0.62 + th ? L.tone[1] : L.tone[0];
                    // the top of a lower puff shows inside the body as a lit line: the cloud's lobes
                    if (y == inner) c = Math.Max(c, L.tone[2]);
                    if (bot - y - 0.5 < 1 && v > 0.5) c = Math.Max(0, L.tone[0] - 1);
                    // the silver lining: near the moon the edges light right up
                    if (md < 46 && (d < 2 || y == inner || v < 0.3)) c = Math.Min(7, c + (md < 24 ? 2 : 1));
                    double a = L.alpha * Math.Min(1, grow * 1.5);
                    if (a >= 1) q.Px(x, y, C[c]);
                    else q.Blend(x, y, C[c], a);
                    if (d < 1 && md < 46) q.Light(x, y, C[6], 0.35 * (1 - md / 46));
                }
            }
        }
    }
}
