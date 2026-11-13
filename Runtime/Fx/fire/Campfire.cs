// The tongue, lick, flame-puff, smoke and ember recipe is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Campfire : Effect
    {
        public static readonly Module Module = new Module("campfire", "pixelfx/fx/fire/campfire", 128, 128, 64, 112, 3, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["wood"] = new[] { "#0e080a", "#24130f", "#3b2216", "#5a3520", "#8a5a34", "#b9884f" },
                ["height"] = 22,
                ["seed"] = 3,
            },
            (o, e) => new Campfire(o))
        {
            Title = "Campfire",
            Desc = "Crossed logs on a bed of coals and a lively fire: tongues that lick and leap, popping sparks, embers drifting up, smoke curling off, a warm pool of light.",
            Tags = new[] { "fire", "campfire", "prop", "light", "smoke", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 3;
        static readonly LoopNoise NL = NoiseFor(D);
        static readonly uint SMOKE1 = Hex("#5b5566");
        static readonly uint[] ASH = { Hex("#2a2530"), Hex("#4a4350"), Hex("#6b6270") };

        // the logs, near end first: they cross in an X over the coals, their near ends showing the cut grain and their
        // far ends poking out past the flames
        static readonly double[][] LOGS =
        {
            new[] { -19, 2, 15, -12, 3.4 },
            new[] { 19, 2, -15, -12, 3.4 },
        };
        const double CROSS = 0.56;
        // big tongues keep more orange and a smaller white heart, so the light doesn't wash them out
        static readonly double[][] BIG =
        {
            new[] { 1, 1, 6.0 }, new[] { 0.8, 0.9, 5 }, new[] { 0.62, 0.78, 4 }, new[] { 0.44, 0.6, 3 }, new[] { 0.26, 0.4, 1 }, new[] { 0.12, 0.22, 0 },
        };

        // the fire: [x, base, height (share of o.height), half-width, back row?]
        static readonly double[][] TONGUES =
        {
            new[] { -7.5, -7, 0.42, 1.8, 1 },
            new[] { 7.5, -7, 0.46, 1.8, 1 },
            new[] { -3, -9, 0.78, 2.0, 1 },
            new[] { 3.2, -9, 0.82, 2.0, 1 },
            new[] { -12, -3, 0.14, 1.3, 0 },
            new[] { 12.5, -3, 0.16, 1.3, 0 },
            new[] { -9.5, -4.5, 0.3, 1.9, 0 },
            new[] { 9.5, -4.5, 0.34, 1.9, 0 },
            new[] { -5, -6, 0.6, 2.6, 0 },
            new[] { 5.2, -6, 0.66, 2.6, 0 },
            new[] { 0, -7, 1, 3.4, 0 },
        };

        // a log from its near end (x0, y0) to its far end, radius r, drawn between u0 and u1 along it: a 1 px dark rim,
        // a lit top, grooves running along the grain, a shadowed underside; charred with glowing cracks in the fire
        static void Log(Painter p, double[] L, uint[] W, uint[] H, double u0, double u1, double t, double S)
        {
            double x0 = L[0], y0 = L[1], x1 = L[2], y1 = L[3], r = L[4], dx = x1 - x0, dy = y1 - y0, len = Hypot(dx, dy), ax = dx / len, ay = dy / len;
            double nx = -ay, ny = ax;
            if (ny < 0)
            {
                nx = -nx;
                ny = -ny;
            }
            double rim = 1 - 1.05 / r;
            p.Shade(Math.Min(x0, x1) - r - 1, Math.Min(y0, y1) - r - 1, Math.Max(x0, x1) + r + 1, Math.Max(y0, y1) + r + 1, (x, y, o) =>
            {
                double along = (x - x0) * ax + (y - y0) * ay, u = along / len, s = ((x - x0) * nx + (y - y0) * ny) / r;
                if (u < u0 || u > u1 || Math.Abs(s) > 1) return 0;
                double burn = Clamp(1.2 - Math.Abs(x) / 8), lane = Round(s * r * 0.85), run = Rand(Math.Floor(along / 3), lane, S);
                if (Math.Abs(s) > rim || along > len - 1) return s < 0 && burn > 0.4 ? H[7] : W[0];
                if (burn > 0.35 && lane < 2)
                {
                    // charred: black, with embers breathing in the cracks along the grain
                    if (run < 0.55 && lane > -2)
                    {
                        double g = 0.5 + 0.5 * Px.Sin((t / D) * Math.PI * 6 + Rand(Math.Floor(along / 2), lane, S + 1) * 6.3);
                        o.k = 0.3 * burn * g;
                        return g > 0.72 ? H[3] : g > 0.4 ? H[4] : H[5];
                    }
                    return lane < -1 ? W[1] : W[0];
                }
                if (lane <= -2) return run < 0.25 ? W[2] : Mix(W[3], H[4], 0.45 * burn);
                if (lane == -1) return run < 0.45 ? W[1] : W[2];
                if (lane == 0) return run < 0.2 ? W[1] : W[2];
                return run < 0.4 ? W[0] : W[1];
            });
            // the near end's cut face: a dark rim, pale sapwood, growth rings round a dark heart
            if (u0 <= 0) p.Ellipse(x0, y0, r * 0.62, r + 0.2, (d, a) => d > 0.78 ? W[0] : d > 0.56 ? W[5] : d > 0.32 ? W[4] : W[3]);
        }

        // the bed of coals under the logs: coals that breathe in the heat, grey ash round the rim
        static void Coals(Painter p, uint[] W, uint[] H, double t, double S)
        {
            p.Shade(-15, -3, 15, 2, (x, y, o) =>
            {
                double dx = x / 14, dy = (y + 0.3) / 2.2, d = dx * dx + dy * dy;
                if (d > 1) return 0;
                double h = Rand(Math.Floor(x / 2), Math.Floor(y), S + 20);
                if (d > 0.55 && h < 0.55) return ASH[h < 0.15 ? 2 : h < 0.32 ? 1 : 0];
                if (h > 0.5)
                {
                    double g = (0.5 + 0.5 * Px.Sin((t / D) * Math.PI * 4 + h * 40)) * (1.1 - d);
                    o.k = 0.35 * g;
                    return g > 0.7 ? H[2] : g > 0.5 ? H[3] : g > 0.3 ? H[4] : H[5];
                }
                return h > 0.3 ? W[1] : W[0];
            });
        }

        sealed class Tongue
        {
            public double dx, y, h, w, env, n;
            public bool back;
        }

        readonly Options o;

        Campfire(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F), W = Fit(o.Ramp("wood"), 6);
            double S = o.Seed, HC = o.Num("height");
            var R = new Rng(S);
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            double Fla(double tt) => 0.74 + 0.16 * NL(tt, 7.3, S + 1) + 0.1 * NL(tt, 19, S + 2);
            double fl = Fla(t);
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25), SMOKE = Mix(SMOKE1, H[6], 0.25);
            uint[] SPARKS = { H[1], H[2], H[4], H[5] }, EMB = { H[1], H[2], H[4], H[6] };

            var T = new Tongue[TONGUES.Length];
            for (int i = 0; i < TONGUES.Length; i++)
            {
                double[] q = TONGUES[i];
                var tg = new Tongue();
                tg.dx = q[0] + R.Range(-0.4, 0.4);
                tg.y = q[1];
                tg.h = 3 + HC * q[2] * R.Range(0.92, 1.08);
                tg.w = q[3] * R.Range(0.9, 1.1);
                tg.back = q[4] != 0;
                tg.env = q[2];
                tg.n = (S * 7 + i) * 13;
                T[i] = tg;
            }
            double Lean(double tt) => NL(tt, 0.67, S + 90) * 0.12;
            double Height(int i, double tt)
            {
                double h = T[i].h, n = T[i].n, leap = Math.Max(0, NL(tt, 2.6, n + 70) - 0.55) * 1.4;
                return (h + 0.25 * h * NL(tt, 3.3, n) + h * leap) * (0.9 + 0.1 * Fla(tt));
            }
            double Sweep(int i, double tt, double h) => NL(tt, 2.2, T[i].n + 40) * (1 + h * 0.08) + Lean(tt) * h * 0.9 - Clamp(T[i].dx * 0.3, -2.5, 2.5);
            (double x, double y, double h) Tip(int i, double tt)
            {
                double h = Height(i, tt);
                return (T[i].dx + Sweep(i, tt, h) * 0.85, T[i].y - h * 0.88 + 3, h);
            }
            void TongueAt(int i)
            {
                Tongue g = T[i];
                double h = Height(i, t);
                FlameTongue(p, g.dx, g.y, h, g.w, Sweep(i, t, h), N, g.n, H, g.back ? BACK_LAYERS : g.w > 2.2 ? BIG : LAYERS, 0.5, 1.1 + h * 0.04);
            }

            // smoke curling off the top, behind everything: each puff keeps the sway the plume had when it left the
            // flames, so the wiggle climbs the plume, and the draught bends it over as it rises
            Looped(t, D, 48, S + 300, (r, j) => new Q { x = r.Range(-5, 5), y = -HC * 0.8 - r.Range(0, 8), vx = r.Range(-2, 2), vy = -r.Range(10, 17), life = r.Range(2, 2.9), s = r.Range(2.4, 3.8) }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, 4, -20, 1.6, 1);
                SmokePuff(p, d.x + NL(t - age, 0.8, S + 60) * (2 + 3 * age), d.y, 1 - age / q.life, q.s, SMOKE);
            });
            // flame puffs shed from the tips: they burn out above the fire, some into smoke
            var puffs = new List<(double x, double y, double k, double s, double f)>();
            Looped(t, D, T.Length * 24, S + 200, (r, j) =>
            {
                int i = j % T.Length;
                double fire = r.Range(0.14, 0.3);
                bool smokes = r.Chance(0.45);
                return new Q
                {
                    i = i, fire = fire, smokes = smokes, sl = r.Range(0.8, 1.4), ox = r.Range(-1, 1), oy = r.Range(0, 4), vx = r.Range(-4, 4), vy = -r.Range(15, 30),
                    s = r.Range(0.8, 1.6) * (0.8 + 0.6 * T[i].env), f = r.Next(), life = fire + (smokes ? 1.4 : 0),
                };
            }, (q, age, j) =>
            {
                var tp = Tip(q.i, t - age);
                var d = Drift(tp.x + q.ox, tp.y + q.oy, q.vx, q.vy, Math.Min(age, q.fire), 0, -95, 5, 3.2);
                if (age < q.fire)
                {
                    puffs.Add((d.x, d.y, 1 - age / q.fire, q.s, q.f));
                    return;
                }
                if (age - q.fire > q.sl) return;
                var sd = Drift(d.x, d.y, d.vx * 0.6, d.vy * 0.6 - 6, age - q.fire, 2, -16, 1.6, 1.2);
                SmokePuff(p, sd.x, sd.y, 1 - (age - q.fire) / q.sl, q.s * 0.7, 0);
            });

            Coals(p, W, H, t, S);
            foreach (double[] L in LOGS) Log(p, L, W, H, CROSS, 1, t, S + 11);
            for (int i = 0; i < T.Length; i++)
                if (T[i].back) TongueAt(i);
            // flames licking up off the burning wood
            Looped(t, D, 90, S + 210, (r, j) => new Q { x = r.Range(-10, 10), y = -r.Range(3, 8), vx = r.Range(-5, 5), vy = -r.Range(20, 40), life = r.Range(0.16, 0.3), s = r.Range(1, 1.8), f = r.Next() }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, 0, -95, 5, 3.2);
                FirePuff(p, d.x, d.y, 1 - age / q.life, q.s, q.f, H);
            });
            for (int i = 0; i < T.Length; i++)
                if (!T[i].back) TongueAt(i);
            // licks torn off the tips, rising and shrinking
            Looped(t, D, T.Length * 12, S + 220, (r, j) => new Q { i = j % T.Length, vx = r.Range(-3, 3), vy = -r.Range(12, 22), s = r.Range(0.9, 1.6) * (0.8 + 0.5 * T[j % T.Length].env), life = 0.26 }, (q, age, j) =>
            {
                var tp = Tip(q.i, t - age);
                Lick(p, tp.x + q.vx * age, tp.y - 2 + q.vy * age, 1 - age / q.life, q.s, H);
            });
            foreach (var (x, y, k, s, f) in puffs) FirePuff(p, x, y, k, s, f, H);
            foreach (double[] L in LOGS) Log(p, L, W, H, 0, CROSS, t, S + 11);

            // pops: a crack, a flash, and a fistful of sparks thrown up out of the wood that bounce on the floor
            Looped(t, D, 4, S + 400, (r, j) => new Q { x = r.Range(-9, 9), y = -r.Range(4, 9), n = r.Int(3, 5), seed = r.Int(0, 1000000), life = 0.7 }, (q, age, j) =>
            {
                if (age < 0.08)
                {
                    p.Px(q.x, q.y, WHITE);
                    p.Glow(q.x, q.y, 12, H[1], 0.7 * (1 - age / 0.08));
                }
                var r = new Rng(q.seed);
                for (int k = 0; k < q.n; k++)
                {
                    double a = -Math.PI / 2 + r.Range(-0.8, 0.8), sp = r.Range(110, 220), life = r.Range(0.25, 0.6);
                    if (age >= life) continue;
                    var s = SparkAt(q.x, q.y, Px.Cos(a) * sp, Px.Sin(a) * sp, age, 0);
                    Spark(p, s.x, s.y, s.vx, s.vy, 1 - age / life, SPARKS);
                }
            });
            // embers carried up by the heat, wandering and cooling
            Looped(t, D, 36, S + 500, (r, j) => new Q
            {
                x = r.Range(-7, 7), y = -r.Range(8, 18), vx = r.Range(-15, 15), vy = -r.Range(25, 60), life = r.Range(1.1, 2.5),
                s = r.Chance(0.15) ? 2 : r.Range(1, 1.6), a = r.Range(0, 6.3), b = r.Range(0, 6.3),
            }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, 0, -30, 1.6, 1.6);
                Ember(p, d.x + 7 * (Px.Sin(age * 2.3 + q.a) - Px.Sin(q.a)), d.y + 2 * (Px.Sin(age * 3 + q.b) - Px.Sin(q.b)), 1 - age / q.life, q.s, EMB, age * 40 + q.a);
            });

            // light: a big warm room light, a hot heart, a pool on the floor, all breathing with the flames
            double br = 0.92 + 0.08 * N(0.67, S + 3);
            double lx = Lean(t) * 12;
            p.Glow(lx, -8, 74 * br * (0.9 + 0.1 * fl), GLOW, 0.3 * fl);
            foreach (double[] g in new[] { new[] { -6, -17, 0.85 }, new[] { 0, -21, 1.0 }, new[] { 6, -17, 0.85 } })
            {
                double x = g[0], y = g[1], k = g[2];
                p.Glow(x + lx, y, 46 * br, GLOW, 0.3 * k * fl);
                p.Glow(x * 0.6, y + 13, 15, H[2], 0.16 * k * fl);
            }
            p.Glow(0, -3, 9, H[0], 0.16 * fl);
            FloorLight(p, 0, 0.5, 48, 8, GLOW, 0.3 * fl);
        }
    }
}
