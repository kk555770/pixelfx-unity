// The flame-puff, smoke and ember recipe is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class FireTornado : Effect
    {
        public static readonly Module Module = new Module("fire-tornado", "pixelfx/fx/fire/fire-tornado", 120, 152, 60, 136, 2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["height"] = 80,
                ["seed"] = 4,
            },
            (o, e) => new FireTornado(o))
        {
            Title = "Fire tornado",
            Desc = "A spinning funnel of fire: spiral bands turning round it, licks flung off its sides, a ring of flame whirling at its foot, embers orbiting it in and out of view.",
            Tags = new[] { "fire", "tornado", "vortex", "spell", "aoe", "loop" },
            Bg = "ground",
        };

        const double D = 2, SPIN = 3; // turns of the surface a loop
        static readonly LoopNoise NL = NoiseFor(D);
        static readonly uint SMOKE1 = Hex("#5b5566");

        sealed class Orbit
        {
            public double x, y, z, k, vx, vy;
            public bool big;
        }

        readonly Options o;

        FireTornado(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F);
            double S = o.Seed, HT = o.Num("height"), ph = t / D;
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25), SMOKE = Mix(SMOKE1, H[6], 0.25);
            uint[] EMB = { H[1], H[2], H[4], H[6] };
            // the funnel: narrow at the foot, flaring at the top, its spine snaking as it turns
            double Cx(double v, double tt) => 3.2 * Px.Sin(v * 4.2 - (tt / D) * TAU * 2) * v + 2 * Px.Sin((tt / D) * TAU) * v;
            double Rad(double v) => 3.5 + 15 * Px.Pow(v, 1.35);
            double Spin(double v, double tt) => (tt / D) * TAU * SPIN + v * 5.5; // the surface turns, twisted up the column

            // embers orbiting: behind the column first, in front after it
            var orbit = new List<Orbit>();
            Looped(t, D, 30, S + 500, (r, j) => new Q { v0 = r.Range(0.02, 0.7), rise = r.Range(0.12, 0.3), a = r.Range(0, TAU), w = r.Range(4, 7), @out = r.Range(4, 12), life = r.Range(1.2, 2.4), big = r.Chance(0.2) }, (q, age, j) =>
            {
                double v = q.v0 + q.rise * age, R = Rad(Math.Min(1, v)) + q.@out + age * 4, a = q.a + q.w * age, k = 1 - age / q.life;
                double x = Cx(Math.Min(1, v), t) + Px.Cos(a) * R, y = -v * HT + Px.Sin(a) * R * 0.28, z = Px.Sin(a);
                orbit.Add(new Orbit { x = x, y = y, z = z, k = k, vx = -Px.Sin(a) * R * q.w, vy = Px.Cos(a) * R * q.w * 0.28, big = q.big });
            });
            void Embers(bool front)
            {
                foreach (Orbit e in orbit)
                {
                    if (e.z >= 0 != front || e.k <= 0) continue;
                    double sp = Hypot(e.vx, e.vy);
                    if (sp == 0 || double.IsNaN(sp)) sp = 1;
                    double L = Math.Min(5, 1 + sp * 0.025);
                    uint c = e.k > 0.6 ? EMB[0] : e.k > 0.3 ? EMB[1] : EMB[2];
                    p.Line(e.x - (e.vx / sp) * L, e.y - (e.vy / sp) * L, e.x, e.y, u => u > 0.6 ? c : front ? EMB[3] : H[7]);
                    if (front) p.Glow(e.x, e.y, 4, c, 0.4 * e.k);
                    else p.Glow(e.x, e.y, 3, c, 0.15 * e.k);
                }
            }
            // the ring of flame whirling at the foot: tongues round an ellipse, the far half dimmer
            void Ring(bool front)
            {
                for (int i = 0; i < 12; i++)
                {
                    double a = (i / 12.0) * TAU + (t / D) * TAU * 2, z = Px.Sin(a);
                    if (z >= 0 != front) continue;
                    double n = (S * 3 + i) * 19, R = 12 + 2 * N(2, n), x = Px.Cos(a) * R, y = z * R * 0.25, h = (5 + 5 * (0.5 + 0.5 * N(5, n + 1))) * (front ? 1 : 0.8);
                    FlameTongue(p, x, y, h, 1.6, -Px.Cos(a + Math.PI / 2) * 3, N, n, H, front ? LAYERS : BACK_LAYERS);
                }
            }

            // smoke spiralling off the top
            Looped(t, D, 20, S + 300, (r, j) => new Q { a = r.Range(0, TAU), life = r.Range(1.2, 2), s = r.Range(2.5, 4), vy = -r.Range(12, 22) }, (q, age, j) =>
            {
                double R = Rad(1) * (0.8 + age * 0.5), a = q.a + age * 3, x = Cx(1, t - age) + Px.Cos(a) * R, y = -HT - 2 + q.vy * age + Px.Sin(a) * R * 0.25;
                SmokePuff(p, x, y, 1 - age / q.life, q.s, SMOKE);
            });
            FloorLight(p, 0, 0.5, 40, 8, GLOW, 0.4);
            Embers(false);
            Ring(false);

            // the column: each pixel's place round the funnel picks its heat; spiral bands turn up it; licks at its edges
            double fl = 0.85 + 0.15 * N(7, S + 1);
            for (double y = 0; y >= -HT * 1.2; y--)
            {
                double v = -y / HT, vv = Math.Min(1, v), c = Cx(vv, t), r = Rad(vv) * (1 + 0.08 * N(3, S + 2, y * 0.1)), sp = Spin(v, t);
                // near the top the funnel comes apart into spiral strands of flame climbing past its rim
                double cut = Clamp((v - 0.72) / 0.42);
                double lickL = 0.3 * Math.Max(0, PNoise(-y * 0.22 + ph * 18, 18, S + 3)), lickR = 0.3 * Math.Max(0, PNoise(-y * 0.22 + ph * 18 + 9, 18, S + 4));
                for (double x = Math.Floor(c - r * 1.35); x <= Math.Ceiling(c + r * 1.35); x++)
                {
                    double dx = (x + 0.5 - c) / r, edge = 1 + (dx < 0 ? lickL : lickR);
                    if (Math.Abs(dx) > edge) continue;
                    double th = Px.Asin(Clamp(dx, -1, 1)), depth = Px.Cos(th), tear = 0.18 * PNoise(y * 0.45 + dx * 2 - ph * 30, 30, S + 6);
                    double band = 0.5 + 0.5 * Px.Sin((th + sp + tear) * 3), streak = band * band * band;
                    if (cut > 0 && band < 0.35 + cut * 0.6 + 0.1 * PNoise(x * 0.7 + ph * 20, 20, S + 5)) continue;
                    // a darker body with thin bright streaks spiralling up it, hottest where it faces us, cooling up the column
                    double heat = 0.22 + 0.36 * depth + 0.5 * streak - 0.22 * vv + 0.1 * (1 - vv) - 0.25 * cut;
                    if (Math.Abs(dx) > 1) heat = 0.3 - (Math.Abs(dx) - 1) * 1.5;
                    heat *= fl;
                    uint col = heat > 0.95 ? H[0] : heat > 0.82 ? H[1] : heat > 0.7 ? H[2] : heat > 0.58 ? H[3] : heat > 0.46 ? H[4] : heat > 0.34 ? H[5] : heat > 0.2 ? H[6] : H[7];
                    p.Px(x, y, col);
                }
                if (y % 10 == 0 && v <= 1) p.Glow(c, y, r * 2 + 8, GLOW, 0.22 * fl);
            }

            // flame flung off the spinning sides, cooling as it flies, some into smoke
            Looped(t, D, 70, S + 200, (r, j) => new Q { v = r.Range(0.05, 0.95), e = r.Sign(), sp = r.Range(20, 50), life = r.Range(0.25, 0.5), s = r.Range(1.4, 2.8), f = r.Next() }, (q, age, j) =>
            {
                double tb = t - age, side = q.e, x0 = Cx(q.v, tb) + side * Rad(q.v), y0 = -q.v * HT;
                var d = Drift(x0, y0, side * q.sp, -q.sp * 0.6, age, 0, -60, 3, 3);
                FirePuff(p, d.x, d.y, 1 - age / q.life, q.s, q.f, H, 0.1, -side * 0.6, -0.8);
            });
            Ring(true);
            Embers(true);
            // light: the column's own glow and a wash round it
            p.Glow(0, -HT * 0.5, 80, GLOW, 0.28 * fl);
            p.Glow(0, -6, 18, H[2], 0.3 * fl);
        }
    }
}
