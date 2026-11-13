// The layered tongue, its noise-torn edges and the shed puffs are adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class SpiritFlame : Effect
    {
        public static readonly Module Module = new Module("spirit-flame", "pixelfx/fx/fire/spirit-flame", 80, 124, 40, 94, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#040a16", "#08203a", "#0b3a52", "#0f5e6e", "#16908e", "#2cc4b4", "#7ef0dc", "#c8fff2", "#ffffff" },
                ["seed"] = 7,
            },
            (o, e) => new SpiritFlame(o))
        {
            Title = "Spirit flame",
            Desc = "A cold blue-teal ghost fire floating in the air: a bright rim round a hollow dark heart, a wave climbing its wispy tail, curls breaking off, cold sparks rising.",
            Tags = new[] { "fire", "spirit", "ghost", "magic", "cold", "ambient", "loop" },
            Bg = "dark",
        };

        const double D = 3;
        static readonly LoopNoise NL = NoiseFor(D);
        // inverted layers: the rim burns brightest, a teal band inside it, a dark hollow heart, a pale core low down
        static readonly double[][] GHOST = { new[] { 1, 1, 1.0 }, new[] { 0.8, 0.92, 3 }, new[] { 0.6, 0.78, 6 }, new[] { 0.3, 0.42, 2 }, new[] { 0.14, 0.22, 0 } };

        // a tongue whose centre line carries a wave climbing it: drawn row by row in layers like a fire tongue
        static void GhostTongue(Painter p, double x, double y, double h, double w, Func<double, double, double> centre, FrameNoise N, double n, uint[] H, double[][] layers, double jit = 0.5)
        {
            foreach (double[] layer in layers)
            {
                double lw = layer[0], lh = layer[1];
                double L = h * lh;
                uint c = H[(int)layer[2]];
                for (double r = 0; r <= L; r++)
                {
                    double u = r / Math.Max(1, L), v = r / h;
                    // a teardrop: round at the bottom, swelling, then drawn out thin
                    double half = w * lw * Px.Pow(1 - u, 0.75) * Math.Min(1, (r + 1.5) / 4) + N(6, n, r * 0.4) * jit * u;
                    if (half < 0.3) continue;
                    double cx = x + centre(v, r), Y = Round(y - r);
                    for (double px = Round(cx - half), x1 = Round(cx + half); px <= x1; px++) p.Px(px, Y, c);
                }
            }
        }

        readonly Options o;

        SpiritFlame(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F);
            double S = o.Seed, ph = t / D;
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            uint COLD = Mix(H[3], WHITE, 0.15);
            double bob = Px.Sin(ph * TAU) * 1.5;
            // the wave: it climbs the flame twice a loop, growing toward the tip, and the whole flame leans with a slow drift
            double lean = N(0.67, S + 2) * 3;
            double Wave(double v, double r, double k = 0) => (Px.Sin(r * 0.24 - ph * TAU * 2 + k) * 2.2 + Px.Sin(r * 0.11 - ph * TAU + k * 0.5) * 3.2) * v * v + lean * v * v;
            double H0 = 44 * (0.92 + 0.08 * N(1.3, S + 3)), y0 = bob;

            // the glow it casts, cold and slow
            double br = 0.9 + 0.1 * N(2, S + 4);
            p.Glow(0, y0 - 16, 60, COLD, 0.36 * br);
            p.Glow(Wave(0.5, 20) * 0.5, y0 - 20, 26, H[2], 0.2 * br);
            // curls breaking off the tips and drifting up, thinning out
            Looped(t, D, 40, S + 300, (r, j) => new Q { x = r.Range(-4, 4), y = -H0 * r.Range(0.55, 0.95), vx = r.Range(-7, 7), vy = -r.Range(10, 22), life = r.Range(1, 1.9), s = r.Range(1.4, 2.8), a = r.Range(0, TAU), w = r.Range(2, 3.5) }, (q, age, j) =>
            {
                var d = Drift(q.x + Wave(0.9, 26, 0) * 0.5, q.y + y0, q.vx, q.vy, age, 0, -6, 1.2, 0.6);
                double k = 1 - age / q.life;
                double cx = d.x + Px.Sin(age * q.w + q.a) * 3 * (age + 0.2), a = Math.Min(1, age * 5) * k;
                FireLib.Soft(p, cx, d.y, q.s * (0.8 + age * 0.8), H[4], 0.35 * a);
                FireLib.Soft(p, cx - 0.4, d.y - 0.6, q.s * 0.5 * (0.8 + age * 0.5), H[2], 0.4 * a * k);
                p.Glow(cx, d.y, 6, H[3], 0.15 * a);
            });
            // two thin wisps trailing off the sides, then the body
            // side wisps that flicker out and curl, now long, now gone
            double wl = Math.Max(0, 0.25 + 0.55 * N(1.7, S + 6)), wr = Math.Max(0, 0.25 + 0.55 * N(1.7, S + 7));
            if (wl > 0.12) GhostTongue(p, -4, y0 - 4, H0 * wl, 2.4, (v, r) => Wave(v, r, 2.1) - 6 * v * v + 3 * Px.Sin(v * 5 - ph * TAU * 2) * v, N, S * 11 + 1, H, GHOST);
            if (wr > 0.12) GhostTongue(p, 4.2, y0 - 4, H0 * wr, 2.4, (v, r) => Wave(v, r, -1.8) + 6 * v * v - 3 * Px.Sin(v * 5 - ph * TAU * 2 + 1) * v, N, S * 11 + 2, H, GHOST);
            GhostTongue(p, 0, y0, H0, 8.6, (v, r) => Wave(v, r), N, S * 11, H, GHOST, 0.6);
            // bright strands drifting up through the hollow heart
            foreach (int k in new[] { 0, 1 })
            {
                for (double r = 4; r < H0 * 0.72; r++)
                {
                    double v = r / H0, x = Wave(v, r) + Px.Sin(r * 0.35 - ph * TAU * 3 + k * Math.PI) * 2.2 * (1 - v);
                    bool on = Px.Sin(r * 0.2 - ph * TAU * 4 + k * 2) > 0.2;
                    if (on) p.Px(x, y0 - r, k != 0 ? H[2] : H[1]);
                }
            }
            // light along the body
            foreach (double v in new[] { 0.2, 0.45, 0.7 }) p.Glow(Wave(v, H0 * v), y0 - H0 * v, 12, H[2], 0.16 * br);
            // the pale heart breathing low in the flame
            p.Glow(0, y0 - 4, 9, H[1], 0.22 + 0.06 * N(3, S + 5));
            // cold sparks rising off it, twinkling
            Looped(t, D, 34, S + 400, (r, j) => new Q { x = r.Range(-7, 7), y = -r.Range(4, 28), vx = r.Range(-12, 12), vy = -r.Range(14, 34), life = r.Range(0.9, 1.8), a = r.Range(0, TAU) }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y + y0, q.vx, q.vy, age, 0, -10, 1.2, 1.2);
                double k = 1 - age / q.life, tw = 0.5 + 0.5 * Px.Sin(age * 14 + q.a);
                double sx = d.x + Px.Sin(age * 3 + q.a) * 3, y = d.y;
                if (k * (0.4 + 0.6 * tw) < 0.2) return;
                p.Px(sx, y, tw > 0.7 && k > 0.5 ? H[0] : k > 0.5 ? H[1] : H[3]);
                if (tw > 0.85 && k > 0.6)
                {
                    p.Px(sx - 1, y, H[3]);
                    p.Px(sx + 1, y, H[3]);
                    p.Px(sx, y - 1, H[3]);
                    p.Px(sx, y + 1, H[3]);
                }
                p.Glow(sx, y, 4, H[2], 0.3 * k);
            });
        }
    }
}
