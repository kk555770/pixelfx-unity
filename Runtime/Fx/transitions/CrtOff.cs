// The glowing line (a white core, a pale fringe, an outer fringe while it's fresh, light along it) follows
// Sprite Fusion's open-source lightning strike (https://www.spritefusion.com/pixel-art-vfx/lightning),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class CrtOff : Effect
    {
        public static readonly Module Module = Screen("crt-off", "CRT off",
            "The picture squeezes into a white-hot glowing line, the line into a dot that lingers as a fading afterglow; then it switches back on, dot to line to picture, with a warm-up glow.",
            new[] { "crt", "tv", "retro", "switch", "wipe" }, 2.0,
            new Options { ["palette"] = new[] { "#060a14", "#0e1a33", "#1a3360", "#2a5a9a", "#4a8fd0", "#8cc4f0", "#d4ecff", "#ffffff" }, ["seed"] = 1 },
            (o, e) => new CrtOff(o));

        const double YC = H / 2.0, XC = W / 2.0;
        // off: squeeze to a line, the line to a dot, the dot fades; on: a dot, a line, the picture
        const double SQ0 = 0.06, SQ1 = 0.34, LN1 = 0.56, DOT1 = 0.86, ON0 = 1.06, ON1 = 1.16, OPEN1 = 1.42;

        // the visible strip's height and the line's half-length at time t
        static (double h, double w) Shape(double t)
        {
            if (t < SQ0) return (H, XC);
            if (t < SQ1) return (Math.Max(1, H * (1 - Ease.InCubic((t - SQ0) / (SQ1 - SQ0)))), XC);
            if (t < LN1) return (1, XC * (1 - Ease.InQuad((t - SQ1) / (LN1 - SQ1))));
            if (t < ON0) return (0, 0);
            if (t < ON1) return (1, XC * Ease.OutExpo((t - ON0) / (ON1 - ON0)));
            return (Math.Max(1, H * Ease.OutCubic(Clamp((t - ON1) / (OPEN1 - ON1)))), XC);
        }

        readonly Options o;

        CrtOff(Options o) => this.o = o;

        public override void Draw(Painter p0, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            uint ink = o.Color("color");
            Painter p = ClipTo(p0);
            var (h, w) = Shape(t);
            double seed = o.Seed, fl = 0.85 + 0.15 * Rand(Math.Floor(t * 30), 7, seed);
            if (t >= OPEN1 + 0.02 && t < OPEN1 + 0.6)
            {
                // switched on: a warm-up glow over the picture, a bright refresh band rolling down it once, scanlines
                double u = (t - OPEN1) / 0.6, band = -20 + u * (H + 40);
                for (int y = 0; y < H; y++)
                {
                    double k = 0.22 * Px.Pow(1 - u, 2) + 0.3 * Px.Pow(Math.Max(0, 1 - Math.Abs(y - band) / 10), 2) * (1 - u) + ((y & 1) != 0 ? 0.05 * (1 - u) : 0);
                    if (k > 0.01)
                        for (int x = 0; x < W; x += 1) p0.Light(x, y, C[5], k);
                }
                return;
            }
            if (h >= H) return;
            double top = Round(YC - h / 2), bot = Round(YC + h / 2);
            // the dark closing in from above and below
            if (top > 0) Rect(p0, 0, 0, W, top, ink);
            if (bot < H) Rect(p0, 0, bot, W, H - bot, ink);
            if (h > 2)
            {
                // the squeezed picture: brighter as it narrows, with scanlines, its edges glowing where the dark meets it
                double sq = 1 - h / H;
                for (double y = top; y < bot; y++)
                {
                    double e = Math.Min(y - top, bot - 1 - y), k = 0.55 * sq * sq + ((ToInt32(y) & 1) != 0 ? 0.08 * sq : 0) + 0.35 * Px.Pow(Math.Max(0, 1 - e / 6), 2);
                    for (int x = 0; x < W; x++) p0.Light(x, y, C[5], k * fl);
                }
                foreach (double y in new[] { top - 1, bot })
                    if (y >= 0 && y < H) Rect(p0, 0, y, W, 1, sq > 0.6 ? C[6] : C[5]);
                return;
            }
            // the line (and the dot it shrinks to): white-hot core, a pale fringe, an outer fringe while it's fresh, light
            bool dotPhase = t >= LN1 && t < ON0, fresh = t < SQ1 + 0.08 || (t > ON0 && t < ON1 + 0.04);
            double x0 = Math.Floor(XC - w), x1 = Math.Ceiling(XC + w);
            if (h > 0 && w >= 1)
            {
                double fr = Math.Floor(t * 30);
                for (double x = Math.Max(0, x0); x < Math.Min(W, x1); x++)
                {
                    // it hums: the line wavers a pixel here and there, and crackles with flecks of static
                    double u = Math.Abs(x + 0.5 - XC) / w, y = YC + Round(Noise(x / 23 + t * 9, seed) * (0.7 + 0.5 * u)); // 0 middle .. 1 ends
                    p0.Px(x, y, u < 0.85 ? WHITE : C[6]);
                    if (u < 0.92)
                    {
                        p0.Px(x, y - 1, C[6]);
                        p0.Px(x, y + 1, u < 0.6 ? C[6] : C[5]);
                    }
                    if (fresh && u < 0.8)
                    {
                        p0.Dot(x, y - 2, C[4], 0.5);
                        p0.Dot(x, y + 2, C[4], 0.5);
                    }
                    if (Rand(x, fr, seed + 3) < 0.06) p.Px(x, y + (Rand(x, fr, seed + 4) < 0.5 ? -1 : 1) * (2 + Math.Floor(Rand(x, fr, seed + 5) * 4)), Rand(x, fr, 6) < 0.5 ? WHITE : C[5]);
                    p0.Light(x, y, C[6], 0.5 * fl * (1 - u * 0.6));
                }
                for (double x = x0; x <= x1; x += 9) p.Glow(x, YC, 10, C[5], 0.35 * fl);
                p.Glow(XC, YC, Math.Min(70, 16 + w * 0.4), C[5], 0.4 * fl);
            }
            // the dot: a glint that holds a moment, then an afterglow cooling to nothing
            if (dotPhase)
            {
                double u = (t - LN1) / (DOT1 - LN1);
                if (u < 0.35) Glint(p, XC, YC, 10 * (1 - u / 0.35) + 3, 0.2 + u * 2, C[6], C[5]);
                if (u < 1)
                {
                    p.Px(XC, YC, u < 0.5 ? WHITE : u < 0.75 ? C[6] : C[4]);
                    p.Px(XC - 1, YC, u < 0.4 ? C[6] : C[4]);
                    // the phosphor smears the dot sideways as it cools
                    double sm = Round(9 * (1 - u));
                    for (int k = 2; k <= sm; k++)
                    {
                        p.Dot(XC - k, YC, C[4], 1 - k / (sm + 1));
                        p.Dot(XC + k - 1, YC, C[4], 1 - k / (sm + 1));
                    }
                    p.Glow(XC, YC, 22 * (1 - u * 0.5), C[5], 0.55 * Px.Pow(1 - u, 1.5));
                }
            }
            // switching on: a pop of light at the heart before the line runs out
            double s = t - ON0;
            if (s >= 0 && s < 0.14)
            {
                Glint(p, XC, YC, 12 * (1 - s / 0.14) + 2, 0.4 + s * 6, C[6], C[5]);
                p.Glow(XC, YC, 40, WHITE, 0.5 * (1 - s / 0.14));
            }
        }
    }
}
