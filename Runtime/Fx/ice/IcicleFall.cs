// The break recipe (a white frame, the ice flung apart as shards with ice dust and glints) is adapted from
// Sprite Fusion's open-source ice (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class IcicleFall : Effect
    {
        const double DURATION = 2.6;

        public static readonly Module Module = new Module("icicle-fall", "pixelfx/fx/ice/icicle-fall", 152, 120, 76, 100, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["seed"] = 4,
            },
            (o, e) => new IcicleFall(o, e))
        {
            Title = "Icicle fall",
            Desc = "An icicle drips and trembles, snaps off its frosted root, drops, stabs the floor and bursts into skidding shards, splinters and rolling frost.",
            Tags = new[] { "ice", "icicle", "fall", "trap", "shatter", "one-shot" },
            Bg = "ground",
        };

        const double ROOT = -80, LEN = 36, W = 12; // where it hangs, how long, how wide at the root
        const double DRIP = 0.16, TREMBLE = 0.3, SNAP = 0.6, FREE = SNAP + 0.04;
        static readonly double FALL_T = Math.Sqrt((2 * (-ROOT - LEN)) / Sim.GRAVITY), IMPACT = FREE + FALL_T;
        static readonly double[][] Streams = { new double[] { -3, 0.7 }, new double[] { 0.5, 1 }, new double[] { 4, 0.6 } };
        static readonly double[] Cuts = { 0.3, 0.58, 0.8 };

        // the icicle: a rippled taper hanging from (x, top), lit down its left side with a bright core streak, rings of
        // light at the ripples, deep on the right, a dark rim. white flashes it; cut hides the top rows (snapped root).
        static void Icicle(Painter p, uint[] C, double x, double top, double len, double w, bool white = false, double glint = -1, double tilt = 0, double cut = 0)
        {
            double Hw(double u) => (w / 2) * Px.Pow(1 - u, 0.75) * (1 + 0.14 * Px.Sin(u * 13 + 0.5)) * (u < 0.08 ? 1.15 : 1) + 0.3;
            p.Shade(x - w / 2 - 4, top - 1, x + w / 2 + 4 + Math.Abs(tilt) * len, top + len + 1, (px, py, o) =>
            {
                double u = (py - top) / len, cx = x + tilt * len * u + 0.7 * Px.Sin(u * 6 + 1), s = px - cx;
                if (u < cut || u > 1) return 0;
                double h = Hw(u);
                if (Math.Abs(s) > h + 1) return 0;
                if (Math.Abs(s) > h)
                {
                    o.c = C[1];
                    return 0;
                }
                if (white) return o.c = WHITE;
                double q = s / h;
                bool ring = Px.Sin(u * 13 + 0.5) > 0.7;
                uint c = u > 0.88 ? WHITE : q < -0.72 ? C[6] : q < -0.2 ? (ring ? C[7] : C[5]) : q < 0.18 ? (u < 0.72 ? C[7] : C[6]) : q > 0.8 ? C[2] : Math.Abs(q - 0.45) < 0.1 && u > 0.15 && u < 0.6 ? C[5] : q < 0.6 ? C[4] : C[3];
                if (glint >= 0 && q < 0.25 && Math.Abs(u - glint) < 0.06) c = WHITE;
                o.c = c;
                return 0;
            });
        }

        sealed class Rime
        {
            public double x, h, stub;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Panes panes;
        readonly Rime[] rime;
        double t;
        bool snapped, landed, dripped;

        IcicleFall(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            panes = new Panes(floor,
                onLand: (q, x, y, v) =>
                {
                    if (v < 100) return;
                    ps.Spawn(P.Vapour, x, y - 1, R.Range(-15, 15), -R.Range(2, 8), R.Range(0.4, 0.6), C[6], R.Range(1.4, 2.2));
                    Splinter(x, y, q.vx, 2);
                },
                onCrumble: q => Splinter(q.x, 0, q.vx, 3),
                emitter: e);
            // the rime it hangs from: a frosted crust with stubs of older icicles
            var list = new List<Rime>();
            for (int x = -18; x <= 18; x++)
            {
                double h = Rand(x, o.Seed, 2), d = Math.Abs(x) / 18.0;
                list.Add(new Rime { x = x, h = 1 + Round((0.6 + h * 1.8) * (1 - d * 0.7)), stub = h < 0.3 && Math.Abs(x) > 7 ? 2 + Round(Rand(x, 5) * 5 * (1 - d * 0.5)) : 0 });
            }
            rime = list.ToArray();
        }

        double Top() => ROOT + (t < FREE ? 0 : 0.5 * Sim.GRAVITY * Px.Pow(t - FREE, 2));

        void Splinter(double x, double y, double vx, int n)
        {
            for (int i = 0; i < n; i++)
                bits.Add(new Bits.Bit { kind = B.Shard, x = x + R.Range(-2, 2), y = y - 1, vx = vx * 0.5 + R.Range(-45, 45), vy = -R.Range(30, 100), g = 0.8, drag = 0.6, bounce = 0.35, life = R.Range(0.4, 0.75), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1.2, 2.4), c = C[R.Int(4, 7)] });
        }

        public override void Update(double dt)
        {
            t += dt;
            // a drop of meltwater swells at the tip and falls
            if (!dripped && t >= DRIP)
            {
                dripped = true;
                bits.Add(new Bits.Bit { kind = B.Drip, x = 0.5, y = ROOT + LEN + 1, vy = 20, g = 0.9, life = 1.2, c = C[6] });
            }
            // it trembles: frost sifts down from the root
            if (t > TREMBLE && t < SNAP && R.Chance(0.25)) ps.Spawn(P.Flake, R.Range(-6, 6), ROOT + R.Range(0, 3), R.Range(-8, 8), R.Range(5, 20), R.Range(0.6, 1.1), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.1));
            // it snaps: a crack flashes across the root, chips and a puff burst off
            if (!snapped && t >= SNAP)
            {
                snapped = true;
                lights.Add(0, ROOT + 4, 24, WHITE, 0.6, 0.1);
                lights.Add(0, ROOT + 4, 32, C[5], 0.4, 0.3);
                for (int i = 0; i < 7; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(30, 110);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = R.Range(-4, 4), y = ROOT + 2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - 20, g = 0.8, drag = 0.8, bounce = 0.3, life = R.Range(0.5, 0.9), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1, 2), c = C[R.Int(5, 7)] });
                }
                for (int i = 0; i < 3; i++) ps.Spawn(P.Vapour, R.Range(-5, 5), ROOT + 2, R.Range(-25, 25), R.Range(-5, 5), R.Range(0.4, 0.7), C[6], R.Range(1.6, 2.6));
                for (int i = 0; i < 5; i++) ps.Spawn(P.Flake, R.Range(-6, 6), ROOT + 2, R.Range(-30, 30), R.Range(-20, 20), R.Range(0.6, 1.2), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.3));
            }
            // falling: it sheds frost motes that hang in the air behind it
            if (t > FREE && t < IMPACT && R.Chance(0.5))
            {
                double tp = Top();
                ps.Spawn(P.Mote, R.Range(-3, 3), tp + R.Range(0, LEN * 0.6), R.Range(-10, 10), R.Range(-40, -10), R.Range(0.2, 0.35), R.Chance(0.5) ? C[6] : C[5], 0.6);
            }
            // it stabs the floor and bursts
            if (!landed && t >= IMPACT)
            {
                landed = true;
                double tp = -LEN;
                lights.Add(0, -16, 46, WHITE, 0.85, 0.12);
                lights.Add(0, -16, 58, C[4], 0.5, 0.5);
                // the icicle in pieces: cut across its length, then down the middle
                var parts = new List<double[][]> { new[] { new[] { -W / 2, tp }, new[] { W / 2, tp }, new[] { 0.5, 0 } } };
                foreach (double v in Cuts) parts = parts.SelectMany(poly => SplitPoly(poly, 0, tp + LEN * v + R.Range(-2, 2), R.Range(-0.4, 0.4))).ToList();
                parts = parts.SelectMany(poly => Centroid(poly).area > 22 ? SplitPoly(poly, Centroid(poly).x, Centroid(poly).y, Math.PI / 2 + R.Range(-0.5, 0.5)) : new List<double[][]> { poly }).ToList();
                foreach (double[][] part in parts)
                {
                    var m = Centroid(part);
                    double side = m.x < 0.5 ? -1 : 1, low = Clamp(-m.y / LEN);
                    panes.Add(new Pane
                    {
                        pts = part.Select(q => new[] { q[0] - m.x, q[1] - m.y }).ToArray(),
                        x = m.x,
                        y = m.y,
                        vx = side * R.Range(30, 110) + R.Range(-15, 15),
                        vy = -R.Range(70, 200) * (0.4 + low),
                        a = 0,
                        va = R.Range(-12, 12),
                        f = R.Range(-0.7, 0.7),
                        vf = R.Range(-8, 8),
                        lit = 0.75,
                        g = 0.9,
                        drag = 0.6,
                        bounce = 0.35,
                        solid = true,
                        ridge = Math.PI / 2,
                        brittle = 2,
                        crumble = R.Range(0.15, 0.45),
                        life = R.Range(1, 1.4),
                    });
                }
                for (int i = 0; i < 52; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.25, 1.25), sp = R.Range(60, 230);
                    bits.Add(new Bits.Bit { kind = R.Chance(0.15) ? B.Chunk : B.Shard, x = R.Range(-3, 3), y = -R.Range(1, 10), vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 0.85, drag = 0.6, bounce = 0.32, life = R.Range(0.6, 1.2), a = R.Range(0, TAU), va = R.Range(-22, 22), s = R.Range(1.2, 3), c = C[R.Int(4, 7)], ramp = new[] { C[1], C[3], C[4], C[6], C[7] } });
                }
                for (int i = 0; i < 10; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(0, 8), -R.Range(1, 6), s * R.Range(25, 90), -R.Range(2, 14), R.Range(0.6, 1.1), C[6], R.Range(2.2, 3.8));
                }
                for (int i = 0; i < 12; i++) ps.Spawn(P.Flake, R.Range(-6, 6), -R.Range(2, 16), R.Range(-70, 70), -R.Range(30, 110), R.Range(1, 1.5), R.Chance(0.6) ? WHITE : C[6], R.Range(0.5, 1.5));
                for (int i = 0; i < 6; i++) ps.Spawn(P.Star, R.Range(-24, 24), -R.Range(4, 34), R.Range(-10, 10), -R.Range(4, 14), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.2));
            }
            if (landed && t < IMPACT + 0.9 && R.Chance(0.08) && bits.List.Count > 0)
            {
                Bits.Bit b = R.Pick(bits.List.ToArray());
                ps.Spawn(P.Star, b.x, b.y - 1, 0, -3, R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.4));
            }
            ps.Update(dt);
            bits.Update(dt);
            panes.Update(dt);
            lights.Update(dt);
            EndBy(t, DURATION, ps, bits, panes);
        }

        public override void DrawBack(Painter p)
        {
            // its shadow gathers on the floor as it comes down
            if (t > TREMBLE && t < IMPACT + 0.05)
            {
                double k = t < FREE ? Seg(t, TREMBLE, FREE) * 0.35 : 0.35 + 0.65 * Seg(t, FREE, IMPACT), rx = 3 + 7 * k;
                for (double x = Math.Floor(-rx); x <= rx; x++)
                {
                    double f = 1 - Px.Pow(x / rx, 2);
                    p.Blend(x + 0.5, 0, C[0], 0.7 * k * f);
                    if (Math.Abs(x) < rx * 0.7) p.Blend(x + 0.5, 1, C[0], 0.45 * k * f);
                }
            }
            // frost splashed across the floor where it struck, melting away
            if (t >= IMPACT)
            {
                double g = Ease.OutCubic(Seg(t, IMPACT, IMPACT + 0.25)), outA = 1 - Smooth(IMPACT + 0.8, IMPACT + 1.5, t);
                for (int x = -26; x <= 26; x++)
                {
                    double d = Math.Abs(x + 0.5) / 26, h = Rand(x, 23), a = outA * Clamp((g * 1.15 - d) / 0.15);
                    if (a <= 0) continue;
                    p.Dot(x, 0, h < 0.35 || d > g - 0.12 ? WHITE : C[6], a);
                    if (h < 0.4)
                        for (int j = 1; j <= Round((1 + Rand(x, 3) * 2.5) * (1 - d)); j++) p.Dot(x + Sign(x) * (j > 1 ? 1 : 0), -j, j > 1 ? WHITE : C[5], a);
                    for (int j = 1; j <= Round((2 + Rand(x, 7) * 3) * (1 - d * 0.6)); j++) p.Dot(x, j, j < 2 ? C[5] : C[4], a * (1.1 - j / 6.0));
                }
                double k = Seg(t, IMPACT, IMPACT + 0.35);
                if (k < 1) Sim.Band(p, 0, 0.5, 6 + 34 * Ease.OutCubic(k), 0.18, Math.Max(1, 4 * (1 - k)), 1.1 - k, new[] { C[3], C[4], C[5], C[6] }, a => Px.Sin(a) < 0);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            // the rime it hangs from, fading at the end
            double ra = 1 - Smooth(DURATION - 0.7, DURATION - 0.2, t);
            foreach (Rime r in rime)
            {
                // a lumpy crust: white on top, pale below, a deep underside; little icicles hang off it
                for (int j = 0; j < r.h + 1; j++) p.Dot(r.x, ROOT - 1 - j, j == r.h ? WHITE : j == r.h - 1 ? C[6] : j == 0 ? C[3] : C[5], ra);
                for (int j = 0; j < r.stub; j++) p.Dot(r.x, ROOT + j, j == r.stub - 1 ? WHITE : j == 0 ? C[5] : C[4], ra);
                if (r.stub > 4) p.Dot(r.x + 1, ROOT, C[3], ra);
            }
            // the snapped root: a jagged stub left behind
            if (snapped)
                for (double x = -W / 2 + 1; x <= W / 2 - 1; x++)
                {
                    double L = 1 + Round(Rand(x, 77) * 2.5);
                    for (int j = 0; j < L; j++) p.Dot(x, ROOT + j, j == L - 1 ? (t < SNAP + 0.15 ? WHITE : C[6]) : x < 0 ? C[5] : C[3], ra);
                }
            p.Glow(0, ROOT, 18, C[4], 0.3 * ra);
            if (t >= IMPACT)
            {
                double k = Seg(t, IMPACT, IMPACT + 0.35);
                if (k < 1) Sim.Band(p, 0, 0.5, 6 + 34 * Ease.OutCubic(k), 0.18, Math.Max(1, 4 * (1 - k)), 1.1 - k, new[] { C[3], C[4], C[5], C[6] }, a => Px.Sin(a) >= 0, k < 0.15);
                if (t < IMPACT + 0.22) Sim.Sparkle(p, 0.5, -3, 10 * Ease.OutBack(Math.Min(1, (t - IMPACT) / 0.05)) * (1 - (t - IMPACT) / 0.22), C[6], C[4], 0.7);
            }
            if (t < IMPACT)
            {
                // the icicle: trembling before it snaps, then falling with a streak of air behind it
                double tp = Top(), jx = t > TREMBLE && t < SNAP ? (Math.Floor(t * 36) % 2 != 0 ? 1 : 0) * Round(Seg(t, TREMBLE, SNAP) * 1.4 + 0.4) : 0;
                bool falling = t >= FREE;
                double sp = falling ? Sim.GRAVITY * (t - FREE) : 0;
                if (falling)
                    foreach (double[] st in Streams)
                    {
                        double dx = st[0], L = Math.Min(30, sp * 0.07) * st[1];
                        if (L > 2) p.Line(dx, tp - L, dx, tp + 3, u => u > 0.75 ? C[6] : u > 0.4 ? C[4] : C[3]);
                    }
                double gl = (t - 0.04) / 0.3;
                Icicle(p, C, jx + 0.5, tp, LEN, W, white: t >= IMPACT - 0.025 || (t >= SNAP && t < SNAP + 0.03), glint: gl >= 0 && gl <= 1 ? gl : -1);
                if (snapped && t < SNAP + 0.12) p.Line(-W / 2 - 1, ROOT + 1, W / 2 + 1, ROOT + 1, WHITE);
                p.Glow(0.5, tp + LEN * 0.5, 20, C[4], 0.28);
                // the drop of meltwater swelling at the tip
                if (t < DRIP)
                {
                    double s = Seg(t, 0, DRIP);
                    p.Dot(0.5, ROOT + LEN + 1, C[6], s);
                    if (s > 0.6) p.Px(0.5, ROOT + LEN + 2, WHITE);
                }
            }
            panes.Draw(p, C);
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
