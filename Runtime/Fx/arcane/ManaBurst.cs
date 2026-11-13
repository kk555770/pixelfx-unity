// The release (a white disc, a cross, two rings, a big light and a spray of motes and stars) is adapted from
// Sprite Fusion's open-source energy charge (https://www.spritefusion.com/pixel-art-vfx/energy),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class ManaBurst : Effect
    {
        const double CY = -28, GATHER = 0.22;
        const int N = 9;

        public static readonly Module Module = new Module("mana-burst", "pixelfx/fx/arcane/mana-burst", 208, 150, 104, 124, 1.8, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 9 },
            (o, e) => new ManaBurst(o, e))
        {
            Title = "Mana burst",
            Desc = "Energy rushes into a point and blows out in a white flash and rings, flinging glowing runes that flare and pop into sparks one after another.",
            Tags = new[] { "arcane", "burst", "mana", "rune", "nova", "magic", "ground", "one-shot" },
            Bg = "ground",
        };

        sealed class Rune
        {
            public int g;
            public double x, y, vx, vy, pop, ph;
            public bool popped;
        }

        sealed class Pop
        {
            public double t, x, y;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Rune[] runes = new Rune[N];
        readonly List<Pop> pops = new List<Pop>();
        double t;
        bool released;

        ManaBurst(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            // the runes: flung out round the burst, slowing hard, bobbing while they burn, each popping in its turn
            for (int i = 0; i < N; i++)
            {
                double a = ((double)i / N) * TAU + R.Range(-0.25, 0.25) - Math.PI / 2, sp = R.Range(150, 210);
                // thrown less far downward, so the low ones stay clear of the floor
                var r = new Rune { g = i, x = 0, y = CY, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp * (Px.Sin(a) > 0 ? 0.42 : 0.85) };
                r.pop = GATHER + 0.42 + i * 0.065 + R.Range(0, 0.05);
                r.ph = R.Range(0, TAU);
                runes[i] = r;
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < GATHER - 0.03)
            {
                // the gather: streaks rush in from all round
                for (int i = 0; i < 2; i++)
                {
                    double a = R.Range(0, TAU), D = R.Range(24, 52), T = R.Range(0.12, 0.18), v = (D * 3) / (Exp(3 * T) - 1);
                    double x = Px.Cos(a) * D, y = Math.Min(-2, CY + Px.Sin(a) * D * 0.85), l = Hypot(x, y - CY);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    ps.Spawn(P.Streak, x, y, (-x / l) * v, ((CY - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
                }
            }
            if (t >= GATHER && !released)
            {
                released = true;
                lights.Add(0, CY - 12, 96, C[5], 1, 0.5);
                lights.Add(0, CY, 40, WHITE, 0.9, 0.1);
                // a spray of motes and stars, thrown less hard at the floor
                for (int i = 0; i < 96; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(100, 250), dn = Px.Sin(a) > 0 ? 0.45 : 1;
                    ps.Spawn(R.Chance(0.65) ? P.Mote : P.Star, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp * dn, R.Range(0.25, 0.55), R.Pick(new[] { WHITE, C[6], C[5] }), R.Range(0.8, 1.8));
                }
                for (int i = 0; i < 20; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(130, 280);
                    ps.Spawn(P.Spark, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.45));
                }
            }
            if (released)
                foreach (Rune r in runes)
                {
                    if (r.popped) continue;
                    r.vx -= r.vx * 4.2 * dt;
                    r.vy -= r.vy * 4.2 * dt + 8 * dt;
                    r.x += r.vx * dt;
                    r.y += r.vy * dt;
                    if (R.Chance(0.35)) ps.Spawn(P.Mote, r.x + R.Range(-1.5, 1.5), r.y, r.vx * 0.2 + R.Range(-10, 10), r.vy * 0.2 - R.Range(5, 25), R.Range(0.2, 0.4), C[5], 0.5);
                    if (t >= r.pop)
                    {
                        r.popped = true;
                        pops.Add(new Pop { t = t, x = r.x, y = r.y });
                        lights.Add(r.x, r.y, 34, C[5], 0.9, 0.22);
                        lights.Add(r.x, r.y, 12, WHITE, 0.8, 0.06);
                        for (int i = 0; i < 9; i++)
                        {
                            double a = R.Range(0, TAU), sp = R.Range(70, 200);
                            ps.Spawn(P.Spark, r.x, r.y, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.15, 0.4));
                        }
                        for (int i = 0; i < 5; i++)
                        {
                            double a = R.Range(0, TAU), sp = R.Range(20, 80);
                            ps.Spawn(i % 2 != 0 ? P.Star : P.Mote, r.x, r.y, Px.Cos(a) * sp, Px.Sin(a) * sp - 20, R.Range(0.3, 0.6), R.Pick(new[] { WHITE, C[6] }), R.Range(0.8, 1.6));
                        }
                    }
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the floor beneath catches the burst
            double k = t < GATHER ? 0.4 * (t / GATHER) : Math.Max(0, 1 - (t - GATHER) / 1.1);
            if (k > 0) p.Glow(0, -1, 50, C[4], 0.3 * k);
        }

        public override void Draw(Painter p)
        {
            if (t < GATHER)
            {
                // the point it gathers into: a swelling ball with a white heart
                double k = t / GATHER, r = 1 + 3.5 * k * k;
                p.Circle(0, CY, r + 1.2, C[4]);
                p.Circle(0, CY, r, C[6]);
                p.Circle(0, CY, r * 0.5, WHITE);
                p.Glow(0, CY, 10 + 20 * k, C[5], 0.4 + 0.4 * k);
                double q = Clamp((t - GATHER * 0.35) / (GATHER * 0.65));
                if (q > 0 && q < 1) p.Ring(0, CY, r + 2 + 16 * (1 - Ease.OutCubic(q)), 1, q > 0.6 ? WHITE : C[6], 1 - q * 0.5);
            }
            else
            {
                double q = (t - GATHER) / 0.6;
                if (q < 1)
                {
                    double e = Ease.OutCubic(q);
                    if (q < 0.12)
                    {
                        p.Circle(0, CY, 12 * (1 - q / 0.12) + 2, C[5]);
                        p.Circle(0, CY, 12 * (1 - q / 0.12), WHITE);
                    }
                    if (q < 0.32) Sim.Flare(p, 0, CY, 30 * (1 - q / 0.32) + 4, 1 - q / 0.32, C);
                    // the rings, cut off where they meet the floor
                    p.Ring(0, CY, 4 + 62 * e, Math.Max(1, 5 * (1 - q)), q < 0.2 ? WHITE : C[6], (v, ang) => CY + Px.Sin(ang) * (4 + 62 * e) > 0 ? 0 : 1.2 - q);
                    p.Ring(0, CY, 2 + 38 * Ease.OutCubic(Math.Min(1, q * 1.4)), Math.Max(1, 2 * (1 - q)), C[5], (v, ang) => CY + Px.Sin(ang) * 40 > 0 ? 0 : 1 - q);
                    p.Glow(0, CY, 60 * (0.4 + 0.6 * e), C[5], 0.9 * (1 - q));
                }
            }
            // the runes: white as they fly, settling to lilac, flaring white just before they pop
            foreach (Rune r in runes)
            {
                if (!released || r.popped) continue;
                double left = r.pop - t, bob = Px.Sin(t * 9 + r.ph) * 0.6;
                bool hot = t - GATHER < 0.08 || left < 0.06;
                Sigil(p, r.g, r.x, r.y + bob, hot ? WHITE : C[6], 1, hot ? C[4] : C[3]);
                p.Glow(r.x, r.y + bob, 10, C[5], hot ? 0.7 : 0.35);
                if (left < 0.06) Sim.Sparkle(p, r.x, r.y + bob, 3 + 4 * (1 - left / 0.06), C[6], C[5], 0.5);
            }
            // each pop: a small flare and a ring
            foreach (Pop q in pops)
            {
                double k = (t - q.t) / 0.32;
                if (k >= 1) continue;
                if (k < 0.35) Sim.Flare(p, q.x, q.y, 11 * (1 - k / 0.35) + 2, 1 - k / 0.35, C);
                p.Ring(q.x, q.y, 2 + 11 * Ease.OutCubic(k), Math.Max(1, 2 * (1 - k)), k < 0.3 ? WHITE : C[6], 1.15 - k);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
