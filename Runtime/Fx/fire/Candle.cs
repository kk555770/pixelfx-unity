// The layered flame tongue is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Candle : Effect
    {
        public static readonly Module Module = new Module("candle", "pixelfx/fx/fire/candle", 56, 76, 28, 64, 4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["root"] = new[] { "#1b2a8a", "#3a62e0", "#7fb4ff" },
                ["wax"] = new[] { "#2e201e", "#5e4436", "#8e6e58", "#b8957a", "#d8b896", "#f0dcc0" },
                ["brass"] = new[] { "#2a1a10", "#6a4a1e", "#b4862e", "#e8c060", "#fff0b0" },
                ["seed"] = 2,
            },
            (o, e) => new Candle(o))
        {
            Title = "Candle",
            Desc = "A wax candle in a brass dish: a calm flame with a blue root that sways and stretches, a halo of light, motes drifting in it, a bead of wax running down a drip.",
            Tags = new[] { "fire", "candle", "prop", "light", "calm", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 4, TOP = -16, WICK = TOP - 3;
        static readonly LoopNoise NL = NoiseFor(D);
        // a calm flame: an orange rim round gold, yellow, a pale heart and a small white core low down
        static readonly double[][] CALM = { new[] { 1, 1, 5.0 }, new[] { 0.8, 0.88, 3 }, new[] { 0.6, 0.72, 2 }, new[] { 0.38, 0.52, 1 }, new[] { 0.18, 0.3, 0 } };
        // the bead runs down this drip, from the rim to its bulb
        const double DRIP_X = 4, DRIP_Y0 = TOP + 2, DRIP_Y1 = -7;

        static void Dish(Painter p, uint[] B, uint[] H, double fl)
        {
            p.Ellipse(0, 1, 10, 2.6, (d, a) => d > 0.82 ? B[0] : Px.Sin(a) < -0.2 ? (d > 0.6 ? Mix(B[3], H[3], 0.3 * fl) : B[2]) : d > 0.55 ? B[1] : B[2]);
            p.Rect(-8, 2, 17, 1, B[1]);
            p.Rect(-7, 3, 15, 1, B[0]);
        }

        // the candle: a wax pillar with a dark outline, a highlight band on the lit side, a shadow side, a pool of
        // melted wax lit by the flame, a long glossy drip down the right ending in a bulb and a short one on the left
        static void Pillar(Painter p, uint[] X, uint[] H, double fl)
        {
            const double L = TOP;
            uint[] cols = { X[1], X[4], X[5], X[4], X[4], X[3], X[3], X[2], X[0] };
            for (int i = 0; i < 9; i++)
            {
                double x = i - 4;
                p.Rect(x, L + 1, 1, -L - 2, cols[i]);
                if (i == 2) p.Rect(x, -8, 1, 7, X[4]);
            }
            p.Rect(-3, -1, 7, 1, X[2]);
            p.Px(-4, -1, X[0]);
            p.Px(4, -1, X[0]);
            // the rim and the pool of melted wax round the wick, lit by the flame
            p.Rect(-3, L, 7, 1, Mix(X[5], H[2], 0.25 * fl));
            p.Px(-4, L, X[1]);
            p.Px(4, L, X[1]);
            p.Rect(-2, L + 1, 5, 1, Mix(X[5], H[1], 0.5 * fl));
            p.Rect(-1, L + 1, 3, 1, Mix(X[4], H[2], 0.45 * fl));
            // the long drip: a lighter, glossy run down the right that bulges past the side into a bulb
            const double y1 = DRIP_Y1;
            p.Rect(3, L + 1, 1, y1 - L - 1, X[4]);
            p.Rect(4, L + 2, 1, y1 - L - 2, X[3]);
            p.Px(5, L + 3, X[0]);
            p.Rect(5, L + 4, 1, y1 - L - 4, X[0]);
            p.Rect(3, y1, 2, 2, X[4]);
            p.Px(5, y1, X[3]);
            p.Px(5, y1 + 1, X[2]);
            p.Px(6, y1, X[0]);
            p.Px(6, y1 + 1, X[0]);
            p.Rect(3, y1 + 2, 3, 1, X[0]);
            p.Px(3, L + 2, X[5]);
            // the short drip on the left
            p.Rect(-4, L + 1, 1, 4, X[4]);
            p.Px(-5, L + 3, X[1]);
            p.Px(-5, L + 4, X[1]);
            p.Px(-4, L + 5, X[3]);
        }

        readonly Options o;

        Candle(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F), RT = Fit(o.Ramp("root"), 3), X = Fit(o.Ramp("wax"), 6), B = Fit(o.Ramp("brass"), 5);
            double S = o.Seed;
            // calm: everything moves slowly, the flame now and then stretches tall
            FrameNoise N = (rate, seed, off) => NL(t, rate * 0.35, seed, off);
            double fl = 0.86 + 0.08 * NL(t, 2, S + 1) + 0.06 * NL(t, 7, S + 2), stretch = Math.Max(0, NL(t, 0.75, S + 3) - 0.3) * 0.6;
            double h = 12 * (1 + stretch) * (0.94 + 0.06 * fl), sway = NL(t, 0.5, S + 4) * 1.6 + NL(t, 1.5, S + 5) * 0.5;
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.3);
            double cy = WICK - h * 0.4;

            // a pool of warm light on the floor and the halo behind the flame
            FloorLight(p, 0, 1, 26, 5, GLOW, 0.3 * fl);
            p.Glow(sway * 0.3, cy, 38 * fl, GLOW, 0.42 * fl);
            p.GlowRing(sway * 0.3, cy, 10, 4, H[2], 0.12 * fl);
            Dish(p, B, H, fl);
            Pillar(p, X, H, fl);
            // the bead of wax: it swells at the rim, runs down the drip and sets into its bulb
            double run = (t % D) / D, swell = Smooth(0, 0.3, run), slide = Smooth(0.3, 0.85, run), set = Smooth(0.85, 1, run);
            if (set < 1 && swell > 0.15)
            {
                double by = Round(DRIP_Y0 + (DRIP_Y1 - DRIP_Y0) * slide);
                p.Px(DRIP_X, by, Mix(X[5], H[1], 0.4 * fl));
                if (swell > 0.6 && set < 0.5)
                {
                    p.Px(DRIP_X + 1, by, X[4]);
                    p.Px(DRIP_X + 1, by + 1, X[2]);
                }
            }
            // the wick, glowing at its tip
            p.Line(0, TOP, 0, WICK + 1, X[0]);
            p.Px(0, WICK + 1, H[4]);
            // the flame and its blue root
            FlameTongue(p, 0, WICK + 1, h, 2.6, sway, N, S * 31, H, CALM, 0.25, 0.6);
            for (double dx = -2; dx <= 2; dx++)
            {
                double a = 1 - Math.Abs(dx) * 0.2;
                p.Dot(dx, WICK + 1, Math.Abs(dx) == 2 ? RT[0] : RT[1], a);
                if (Math.Abs(dx) <= 1) p.Dot(dx, WICK + 2, RT[0], 0.7);
            }
            p.Px(0, WICK, RT[2]);
            // a thread of smoke now and then off the tip
            Looped(t, D, 6, S + 300, (r, j) => new Q { vx = r.Range(-1.5, 1.5), vy = -r.Range(8, 12), life = r.Range(1.4, 2.2), s = r.Range(0.8, 1.2) }, (q, age, j) =>
            {
                var d = Drift(sway, WICK - h, q.vx, q.vy, age, 1, -6, 1, 0.8);
                SmokePuff(p, d.x + NL(t - age, 0.6, S + 60) * (1 + age * 2), d.y, 1 - age / q.life, q.s, 0, false, 0.6);
            });
            // motes drifting in the candlelight, catching it as they pass close
            Looped(t, D, 10, S + 400, (r, j) => new Q { x = r.Range(-20, 20), y = r.Range(-44, -8), vx = r.Range(-3, 3), vy = r.Range(-4, 2), life = r.Range(2.5, 4), ph = r.Range(0, 6.3) }, (q, age, j) =>
            {
                double x = q.x + q.vx * age + Px.Sin(age * 1.3 + q.ph) * 2, y = q.y + q.vy * age, d = Hypot(x, y - cy);
                double k = Math.Min(1, Math.Min(age * 2, (q.life - age) * 2)) * Clamp(1.2 - d / 26);
                if (k > 0.15)
                {
                    p.Dot(x, y, k > 0.6 ? H[1] : H[3], k);
                    p.Glow(x, y, 3, H[2], 0.2 * k);
                }
            });
            // the hot core of the light
            p.Glow(sway * 0.5, WICK - h * 0.3, 10, H[1], 0.35 * fl);
        }
    }
}
