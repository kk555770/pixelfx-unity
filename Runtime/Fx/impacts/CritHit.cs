// The star, its key frames and the spark spray are adapted from Sprite Fusion's open-source impact
// (https://www.spritefusion.com/pixel-art-vfx/impact), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class CritHit : Effect
    {
        public static readonly Module Module = new Module("crit-hit", "pixelfx/fx/impacts/crit-hit", 216, 136, 84, 106, 0.9, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0510", "#5c0a1c", "#a3142a", "#e0302c", "#ff7a2a", "#ffc23d", "#fff09a", "#ffffff" },
                ["height"] = 26,
                ["aim"] = 0.12,
                ["seed"] = 3,
            },
            (o, e) => new CritHit(o, e))
        {
            Title = "Critical hit",
            Desc = "A critical hit: a jagged starburst slams in white and overshoots, a ring breaks into flying arcs, sparks streak off, shards tumble to the floor under a big flare.",
            Tags = new[] { "impact", "hit", "crit", "critical", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        // the star's key frames: from t, scale, thickness, alpha, colors (rim, body, inner, core) as steps of the ramp
        sealed class Key
        {
            public double t, sc, th, a;
            public int[] cols;
        }

        static readonly Key[] KEYS =
        {
            new Key { t = 0, sc = 1, th = 1, a = 1, cols = new[] { 6, 7, 7, 7 } },
            new Key { t = 0.025, sc = 1.3, th = 1, a = 1, cols = new[] { 2, 5, 6, 7 } },
            new Key { t = 0.058, sc = 1.05, th = 0.72, a = 1, cols = new[] { 2, 4, 5, 6 } },
            new Key { t = 0.092, sc = 1.22, th = 0.3, a = 1, cols = new[] { 1, 2, 3, 4 } },
            new Key { t = 0.125 },
        };

        static readonly double[][] FlareArms = { new[] { 1, 0, 1.0 }, new[] { 0, 1, 0.45 }, new[] { 0.7, 0.7, 0.16 }, new[] { 0.7, -0.7, 0.16 } };

        // a lens flare: a long level streak, thick and white at the heart, a shorter upright one, on a bed of light
        static void Flare(Painter p, double x, double y, double L, double k, uint[] C)
        {
            for (int arm = 0; arm < FlareArms.Length; arm++)
            {
                double dx = FlareArms[arm][0], dy = FlareArms[arm][1], len = arm == 0 ? L : L * FlareArms[arm][2];
                foreach (int s in new[] { -1, 1 })
                    for (int d = 1; d <= len; d++)
                    {
                        double u = d / len, a = u < 0.7 ? 1 : k * (1.6 - u);
                        p.Dot(x + s * dx * d, y + s * dy * d, u < 0.35 ? WHITE : u < 0.7 ? C[6] : C[5], a);
                        if (dy == 0 && u < 0.42)
                            foreach (int e in new[] { -1, 1 })
                                p.Dot(x + s * d, y + e, u < 0.2 ? C[6] : C[5], u < 0.3 ? 1 : k);
                    }
            }
            p.GlowLine(x - L, y, x + L, y, 6, C[6], 0.5 * k);
            p.GlowLine(x, y - L * 0.45, x, y + L * 0.45, 4, C[6], 0.3 * k);
        }

        readonly uint[] C;
        readonly double X, Y, A;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly ImpactsLib.Spike[] sp;
        readonly ImpactsLib.Seg[] segs;
        double t;

        CritHit(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            var R = new Rng(o.Seed);
            X = 0;
            Y = -o.Num("height");
            A = o.Num("aim");
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            sp = ImpactsLib.Spikes(R, 14, A, len0: 11, len1: 15, w0: 1.8, w1: 2.5, fwd: 1.4, back: 0.6, jag: 0.5);
            // the two longest spikes reach further (a stable sort, longest first)
            foreach (int i in Enumerable.Range(0, sp.Length).OrderByDescending(i => sp[i].len).Take(2).ToArray()) sp[i] = ImpactsLib.Stretched(sp[i], 1.3);
            segs = new ImpactsLib.Seg[7];
            for (int i = 0; i < 7; i++)
            {
                double a = A + (double)i / 7 * TAU;
                double gap = R.Range(0.55, 0.85), dr = R.Range(0.08, 0.4) + 0.15 * Math.Max(0, Px.Cos(a - A));
                segs[i] = new ImpactsLib.Seg(a, gap, dr, 0.7 + 0.5 * Math.Max(0, Px.Cos(a - A)));
            }

            lights.Add(X, Y, 84, C[5], 0.6, 0.5);
            lights.Add(X, Y, 30, WHITE, 0.7, 0.1);
            lights.Add(X, -4, 44, C[4], 0.3, 0.45);
            // sparks: most sprayed on along the blow, a few kicked back
            for (int i = 0; i < 36; i++)
            {
                bool back = R.Chance(0.22);
                double a = (back ? A + Math.PI : A) + R.Range(-1, 1) * (back ? 0.6 : 0.8), v = R.Range(170, 430) * (back ? 0.6 : 1);
                ps.Spawn(P.Spark, X, Y, Px.Cos(a) * v, Px.Sin(a) * v - 50, R.Range(0.15, 0.5), WHITE, 1, R.Chance(0.3) ? R.Range(0, 0.04) : 0);
            }
            // shards: bright splinters that tumble down, bounce and settle
            uint[] shardCols = { C[7], C[6], C[6], C[5] };
            for (int i = 0; i < 10; i++)
            {
                double a = A + R.Range(-1, 1) * 1.1 - 0.35, v = R.Range(110, 280);
                bits.Add(new Bits.Bit
                {
                    kind = B.Shard, x = X, y = Y, vx = Px.Cos(a) * v, vy = Px.Sin(a) * v - 40, g = 0.8, drag = 0.7, bounce = 0.35, life = R.Range(0.55, 0.85),
                    a = R.Range(0, TAU), va = R.Range(-25, 25), s = R.Range(2, 4.5), c = R.Pick(shardCols),
                });
            }
            // embers of light drifting up and off, and glints that pop round the ring after the hit
            uint[] moteCols = { C[6], C[5], C[5] };
            for (int i = 0; i < 12; i++)
            {
                double a = A - Math.PI / 2 + R.Range(-1.4, 1.4), v = R.Range(35, 110);
                ps.Spawn(P.Mote, X + Px.Cos(a) * 4, Y + Px.Sin(a) * 4, Px.Cos(a) * v, Px.Sin(a) * v - 15, R.Range(0.35, 0.7), R.Pick(moteCols), 0.6, R.Range(0, 0.05));
            }
            for (int i = 0; i < 5; i++)
            {
                double a = R.Range(0, TAU), d = R.Range(14, 34);
                ps.Spawn(P.Star, X + Px.Cos(a) * d, Y + Px.Sin(a) * d * 0.8, 0, -4, R.Range(0.22, 0.38), R.Chance(0.5) ? C[6] : WHITE, R.Range(1.6, 3), R.Range(0.08, 0.32));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            ps.Update(dt);
            bits.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            // the ring: it swells off the star, then breaks into arcs that fly on and cool
            double u = Clamp((t - 0.03) / 0.37);
            if (t >= 0.03 && u < 1)
            {
                double r = 10 + 30 * Ease.OutCubic(u), brk = Smooth(0.12, 0.6, u), th = Math.Max(1, 3.6 * Px.Pow(1 - u, 1.2));
                int i = u < 0.12 ? 7 : u < 0.3 ? 6 : u < 0.5 ? 5 : u < 0.75 ? 4 : 3;
                ImpactsLib.BrokenRing(p, X, Y, r, th, segs, brk, C[i], C[Math.Max(2, i - 2)], u < 0.6 ? 1 : (1 - u) / 0.4, 1, 0);
                if (u < 0.6) p.GlowRing(X, Y, r, 6, C[5], 0.35 * Px.Pow(1 - u / 0.6, 2));
            }
            bits.Draw(p);
            ps.Draw(p);
            // the flare blooms as the star collapses
            double f = (t - 0.04) / 0.28;
            if (f > 0 && f < 1)
            {
                Flare(p, X, Y, 64 * Px.Pow(1 - f, 1.3), 1 - f, C);
                p.Glow(X, Y, 56, C[5], 0.4 * (1 - f));
            }
            // the starburst slams in: a white frame, an overshoot, a settle, then it breaks up
            for (int i = 0; i < KEYS.Length - 1; i++)
                if (t >= KEYS[i].t && t < KEYS[i + 1].t)
                {
                    Key k = KEYS[i];
                    ImpactsLib.Star(p, X, Y, sp, k.sc, k.th, Array.ConvertAll(k.cols, j => C[j]), k.a, 2.6);
                }
            double pop = Exp(-t / 0.05);
            if (pop > 0.02)
            {
                p.Glow(X, Y, 44, C[5], 0.4 * pop);
                p.Glow(X, Y, 12, WHITE, 0.6 * pop);
            }
            lights.Draw(p);
        }
    }
}
