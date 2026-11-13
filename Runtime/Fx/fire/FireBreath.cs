// The flame-puff, smoke and spark recipe is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class FireBreath : Effect
    {
        public static readonly Module Module = new Module("fire-breath", "pixelfx/fx/fire/fire-breath", 160, 116, 12, 72, 1.5, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["spread"] = 0.24,
                ["speed"] = 320,
                ["seed"] = 6,
            },
            (o, e) => new FireBreath(o))
        {
            Title = "Fire breath",
            Desc = "A roaring cone of fire streaming right from the mouth: white-hot at the lips, billowing out and cooling to red and smoke at its edges, sparks thrown out ahead.",
            Tags = new[] { "fire", "breath", "dragon", "stream", "cone", "directional", "loop" },
            Bg = "dark",
        };

        const double D = 1.5;
        static readonly LoopNoise NL = NoiseFor(D);
        static readonly uint SMOKE1 = Hex("#5b5566");
        // the dying licks: red and deep orange, no white heart
        static readonly double[][] COOL_LAYERS = { new[] { 1, 1, 7.0 }, new[] { 0.72, 0.82, 6 }, new[] { 0.44, 0.58, 5 } };
        // the jet at the lips: [length, half-width, angle]
        static readonly double[][] JET = { new[] { 42, 4.2, 0 }, new[] { 32, 3.8, -0.08 }, new[] { 30, 3.8, 0.08 }, new[] { 20, 4.6, 0.0 } };

        readonly Options o;

        FireBreath(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F);
            double S = o.Seed, A = o.Num("spread"), V = o.Num("speed");
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25), SMOKE = Mix(SMOKE1, H[6], 0.3);
            uint[] SPARKS = { H[1], H[2], H[4], H[5] }, EMB = { H[1], H[2], H[4], H[6] };
            // the roar: the stream surges and the cone sways a little
            double Roar(double tt) => 0.85 + 0.15 * NL(tt, 3, S + 1);
            double Sway(double tt) => NL(tt, 1.3, S + 2) * 0.05;

            // the cone: puffs of flame blown out of the mouth that slow, swell, rise and cool; those near the edges
            // cool first, and many burn out into rolling smoke
            var fire = new List<(double age, double x, double y, Q q)>();
            var smoke = new List<(double sa, double x, double y, Q q)>();
            Looped(t, D, 260, S + 100, (r, j) =>
            {
                double e = r.Range(-1, 1), fl = r.Range(0.42, 0.8) * (1 - 0.3 * Math.Abs(e));
                bool sm = r.Chance(0.2 + 0.4 * Math.Abs(e));
                return new Q { e = e, sp = r.Range(0.75, 1.1), fl = fl, sm = sm, sl = r.Range(0.5, 0.85), s = r.Range(3, 5.2), f = r.Next(), ox = r.Range(4, 10), life = fl + (sm ? 0.85 : 0) };
            }, (q, age, j) =>
            {
                double tb = t - age, a = q.e * A + Sway(tb) - 0.04, v = V * q.sp * Roar(tb);
                double ta = Math.Min(age, q.fl);
                var d = Drift(q.ox, q.e * 1.5, Px.Cos(a) * v, Px.Sin(a) * v, ta, 0, -100, 2, 2.4);
                if (age < q.fl)
                {
                    fire.Add((age, d.x, d.y, q));
                    return;
                }
                if (!q.sm || age - q.fl > q.sl) return;
                double sa = age - q.fl;
                var s = Drift(d.x, d.y, d.vx * 0.5, d.vy * 0.6 - 8, sa, 0, -26, 1.8, 1.2);
                smoke.Add((sa, s.x, s.y, q));
            });
            foreach (var (sa, x, y, q) in smoke) SmokePuff(p, x, y, 1 - sa / q.sl, q.s * 1.1, SMOKE, true, 0.7);
            foreach (var (age, x, y, q) in fire.OrderByDescending(f => f.age))
            {
                double u = age / q.fl, edge = Math.Abs(q.e), size = q.s * (0.75 + 1.6 * u);
                // the heart of the stream stays hot longer than its edges
                double heat = Math.Min(0.999, Px.Pow(u, 1.5) * (0.85 + 0.2 * edge) + 0.3 * edge * edge);
                // near the mouth the fire rolls in round billows; further out each billow tears into a tongue licking up
                // and back, thinning to a lick as it cools, before the smoke takes it
                double tear = Clamp((u - 0.32) / 0.2);
                FirePuff(p, x, y, 1 - heat, size * (1 - 0.55 * tear), q.f, H, 0.1, -0.5, -0.86);
                if (tear > 0)
                {
                    double n = Math.Floor(q.f * 1e5), h = size * (1.5 + 0.5 * N(4, n)) * (1.2 - heat * 0.6) * tear;
                    FlameTongue(p, x, y + size * 0.3, h, size * 0.5 * (1.1 - heat * 0.5), -h * 0.4 + N(2.5, n + 1) * 2, N, n, H, heat < 0.45 ? LAYERS : heat < 0.72 ? BACK_LAYERS : COOL_LAYERS);
                }
            }
            // licks curling off the edges of the cone
            for (int i = 0; i < 6; i++)
            {
                double n = (S * 3 + i) * 19, side = i % 2 != 0 ? 1 : -1, d = 18 + (i >> 1) * 16 + N(2, n) * 4, L = (9 + 5 * (0.5 + 0.5 * N(5, n + 1))) * (1 - d / 120);
                double ex = d, ey = side * d * A * 0.85 - d * 0.06;
                FlameBlade(p, ex, ey, 0.75, side * 0.45 - 0.48, L, 1.6 + L * 0.08, -side * 3, N, n, H, LAYERS);
            }
            // the roar: bright streaks racing out through the heart of the cone
            Looped(t, D, 70, S + 250, (r, j) => new Q { e = r.Range(-0.6, 0.6), v = r.Range(330, 470), life = r.Range(0.12, 0.26), l = r.Range(4, 9) }, (q, age, j) =>
            {
                double a = q.e * A - 0.03;
                var d = Drift(6, q.e * 2, Px.Cos(a) * q.v, Px.Sin(a) * q.v, age, 0, -60, 2, 2);
                double sp = Hypot(d.vx, d.vy), k = 1 - age / q.life, L = q.l * (sp / 400);
                p.Line(d.x - (d.vx / sp) * L, d.y - (d.vy / sp) * L, d.x, d.y, u => u > 0.6 ? (k > 0.5 ? H[0] : H[1]) : k > 0.5 ? H[2] : H[3]);
            });
            // the jet at the lips: white-hot, overlapping tongues that flutter as the breath roars
            for (int i = 0; i < JET.Length; i++)
            {
                double len = JET[i][0], w = JET[i][1], a = JET[i][2];
                double n = (S * 5 + i) * 17, L = len * Roar(t) * (0.85 + 0.2 * N(5, n)), aa = a + Sway(t);
                FlameBlade(p, 0, 0, Px.Cos(aa), Px.Sin(aa), L, w, N(3, n + 3) * 3, N, n, H, LAYERS, 0.5, 1.5);
            }

            // sparks flung out ahead and falling; embers lifting off the far end
            Looped(t, D, 40, S + 300, (r, j) => new Q { d = r.Range(4, 60), e = r.Range(-1, 1), v = r.Range(140, 280), life = r.Range(0.25, 0.55) }, (q, age, j) =>
            {
                double a = q.e * A * 1.8 - 0.12;
                var d = Drift(q.d, q.e * q.d * A, Px.Cos(a) * q.v, Px.Sin(a) * q.v - 30, age, 0, 190, 2, 2);
                Spark(p, d.x, d.y, d.vx, d.vy, 1 - age / q.life, SPARKS);
            });
            Looped(t, D, 18, S + 400, (r, j) => new Q { x = r.Range(50, 100), y = r.Range(-14, 6), vx = r.Range(10, 40), vy = -r.Range(20, 50), life = r.Range(0.8, 1.4), s = r.Chance(0.2) ? 2 : 1, ph = r.Range(0, 6.3) }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, 0, -30, 1.6, 1.6);
                Ember(p, d.x + 5 * (Px.Sin(age * 2.3 + q.ph) - Px.Sin(q.ph)), d.y, 1 - age / q.life, q.s, EMB, age * 40 + q.ph);
            });

            // light: a flare at the lips, light all along the cone widening with it, a broad halo
            double kk = Roar(t);
            p.Glow(3, 0, 34 * kk, GLOW, 0.75 * kk);
            p.Glow(3, 0, 10, H[0], 0.55);
            for (double d = 10; d < 126; d += 8) p.Glow(d, -d * 0.1, 10 + d * 0.2, GLOW, 0.32 * kk * (1 - d / 150));
            p.Glow(64, -12, 96, GLOW, 0.16 * kk);
        }
    }
}
