// The flame-puff, smoke and spark recipe is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Fireball : Effect
    {
        public static readonly Module Module = new Module("fireball", "pixelfx/fx/fire/fireball", 136, 56, 112, 28, 1.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["radius"] = 5,
                ["speed"] = 120,
                ["seed"] = 5,
            },
            (o, e) => new Fireball(o))
        {
            Title = "Fireball",
            Desc = "A fireball flying right: a churning white-hot head with a corona of flame, a long torn tail of flame puffs cooling into smoke, embers shed behind.",
            Tags = new[] { "fire", "fireball", "projectile", "magic", "directional", "loop" },
            Bg = "dark",
        };

        const double D = 1.2;
        static readonly LoopNoise NL = NoiseFor(D);
        static readonly uint SMOKE1 = Hex("#5b5566");
        // the comet body: tongues rooted in the head streaming back, the longest behind [length, half-width, y offset]
        static readonly double[][] BODY = { new[] { 30, 4.2, 0 }, new[] { 23, 4.4, -1.6 }, new[] { 22, 4.4, 1.6 }, new[] { 15, 4.6, 0.0 } };
        // the corona: thin licks peeling back off the head [angle on the head (0 is the nose), length, half-width]
        static readonly double[][] CORONA =
        {
            new[] { -1.3, 6, 1.1 }, new[] { -1.8, 8, 1.2 }, new[] { -2.3, 7, 1.1 }, new[] { 1.3, 6, 1.1 }, new[] { 1.8, 8, 1.2 }, new[] { 2.3, 7, 1.1 },
        };

        // the head: a ball whose outline churns and whose heat swirls in a spiral, white at the heart, swept back
        static void Head(Painter p, uint[] H, double t, double R0, double S, double hx, double hy)
        {
            double ph = t / D;
            p.Shade(hx - R0 * 2.4, hy - R0 * 1.7, hx + R0 * 1.5, hy + R0 * 1.7, (x, y, o) =>
            {
                x -= hx;
                y -= hy;
                double d = Hypot(x, y), th = Atan2(y, x), back = Math.Max(0, -Px.Cos(th)), a = th / TAU;
                double bump = 0.55 * PNoise(a * 9 + ph * 9, 9, S) + 0.35 * PNoise(a * 5 - ph * 10, 5, S + 1);
                double rim = R0 * (1 + 0.55 * back * back) + bump;
                if (d > rim) return 0;
                double swirl = PNoise(a * 5 - ph * 10 + d * 0.45, 5, S + 2);
                double h = 1 - d / rim + 0.26 * swirl + 0.1 * (1 - back);
                // the leading edge: a crisp hot rim where it cuts the air
                if (rim - d < 1 && back < 0.3) return h > 0.3 ? H[3] : H[5];
                return h > 0.74 ? H[0] : h > 0.58 ? H[1] : h > 0.42 ? H[2] : h > 0.27 ? H[4] : h > 0.13 ? H[5] : H[6];
            });
        }

        readonly Options o;

        Fireball(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F);
            double S = o.Seed, R0 = o.Num("radius"), V = o.Num("speed");
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25), SMOKE = Mix(SMOKE1, H[6], 0.3);
            uint[] SPARKS = { H[1], H[2], H[4], H[5] }, EMB = { H[1], H[2], H[4], H[6] };
            // the head bobs a hair as it flies
            double hx = N(3, S + 50) * 0.5, hy = N(2.5, S + 51) * 0.7;

            // the tail: flame puffs thrown off the back that the head leaves behind; they cool, swell, rise and some
            // burn out into smoke. Positions are relative to the head, which outruns them.
            var fire = new List<(double age, double x, double y, Q q)>();
            var smoke = new List<(double sa, double x, double y, Q q)>();
            Looped(t, D, 130, S + 100, (r, j) =>
            {
                double fl = r.Range(0.22, 0.55);
                bool sm = r.Chance(0.55);
                double x = -r.Range(2, 24);
                return new Q
                {
                    x = x, y = r.Range(-1, 1) * (R0 - 0.1 * -x), v = V * r.Range(0.3, 0.6), vy = r.Range(-16, 16), fl = fl, sm = sm, sl = r.Range(0.25, 0.45), s = r.Range(1.3, 2.9),
                    f = r.Next() * 0.5 + 0.5 * (-x / 24), life = fl + (sm ? 0.45 : 0),
                };
            }, (q, age, j) =>
            {
                double ta = Math.Min(age, q.fl);
                var w = Drift(0, q.y, q.v, q.vy, ta, 0, -70, 4.5, 3);
                double x = q.x + w.x - V * ta;
                if (age < q.fl)
                {
                    fire.Add((age, x, w.y, q));
                    return;
                }
                if (!q.sm || age - q.fl > q.sl) return;
                double sa = age - q.fl;
                var s = Drift(x, w.y, w.vx - V, w.vy * 0.6 - 6, sa, V * 2, -30, 2, 1.2);
                smoke.Add((sa, s.x, s.y, q));
            });
            foreach (var (sa, x, y, q) in smoke) SmokePuff(p, x, y, 1 - sa / q.sl, q.s * 0.9, SMOKE);
            foreach (var (age, x, y, q) in fire.OrderByDescending(f => f.age))
            {
                double u = age / q.fl;
                FirePuff(p, x, y, 1 - u, q.s * (1 + 1.3 * u), q.f, H, 0.07, -0.86, -0.5);
            }

            // the body: tongues of flame rooted in the head streaming back, and a corona licking round it
            for (int i = 0; i < BODY.Length; i++)
            {
                double len = BODY[i][0], w = BODY[i][1], dy = BODY[i][2];
                double n = (S * 5 + i) * 17, L = len * (0.82 + 0.25 * N(4, n)) * (1 + 0.3 * Math.Max(0, N(2.4, n + 3)));
                FlameBlade(p, hx + 1, hy + dy * 0.5, -1, dy * 0.02, L, w * R0 / 5, N(3, n + 5) * 3 + dy, N, n, H, LAYERS, 0.5, 1.1 + L * 0.05);
            }
            for (int i = 0; i < CORONA.Length; i++)
            {
                double a = CORONA[i][0], len = CORONA[i][1], w = CORONA[i][2];
                double n = (S * 5 + i + 20) * 17, L = len * (0.7 + 0.4 * (0.5 + 0.5 * N(6, n))), ox = Px.Cos(a) * R0 * 0.85, oy = Px.Sin(a) * R0 * 0.85;
                double dx = Px.Cos(a) * 0.55 - 1, dy = Px.Sin(a) * 0.55, l = Hypot(dx, dy);
                FlameBlade(p, hx + ox, hy + oy, dx / l, dy / l, L, w, -Sign(a) * 1.5, N, n, H, LAYERS);
            }
            Head(p, H, t, R0, S, hx, hy);

            // motes flicked off the head's skin: short hot streaks that die at once
            Looped(t, D, 26, S + 200, (r, j) => new Q { a = r.Range(-2.4, 2.4), v = r.Range(30, 70), life = r.Range(0.08, 0.18) }, (q, age, j) =>
            {
                double ca = Px.Cos(q.a), sa = Px.Sin(q.a), d = R0 * 0.9 + q.v * age, x = hx + ca * d - V * 0.4 * age, y = hy + sa * d;
                double k = 1 - age / q.life, L = 1 + 2 * k;
                p.Line(x - ca * L + V * 0.02, y - sa * L, x, y, k > 0.5 ? H[1] : H[3]);
                p.Glow(x, y, 3, H[2], 0.4 * k);
            });
            // embers left behind: streaks that the head outruns, falling as they cool
            Looped(t, D, 30, S + 300, (r, j) => new Q { x = -r.Range(2, 16), y = r.Range(-3, 3), v = V * r.Range(0.3, 0.8), vy = r.Range(-20, 20), life = r.Range(0.25, 0.55), big = r.Chance(0.25), ph = r.Range(0, 6.3) }, (q, age, j) =>
            {
                var w = Drift(0, q.y, q.v, q.vy, age, 0, 50, 3, 2);
                double x = q.x + w.x - V * age, k = 1 - age / q.life;
                if (q.big) Ember(p, x, w.y, k, 2, EMB, age * 40 + q.ph);
                else Spark(p, x, w.y, w.vx - V, w.vy, k, SPARKS);
            });

            // light: a hot heart, a warm halo, light running down the tail
            double fl2 = 0.85 + 0.15 * N(9, S + 7);
            p.GlowLine(hx - 6, hy, -50, -2, 12, GLOW, 0.2 * fl2);
            p.Glow(hx - 2, hy, 42, GLOW, 0.42 * fl2);
            p.Glow(hx, hy, 13, H[1], 0.3 * fl2);
        }
    }
}
