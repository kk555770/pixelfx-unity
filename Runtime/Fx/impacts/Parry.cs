// The star's key frames and the spark spray are adapted from Sprite Fusion's open-source impact
// (https://www.spritefusion.com/pixel-art-vfx/impact), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Parry : Effect
    {
        public static readonly Module Module = new Module("parry", "pixelfx/fx/impacts/parry", 200, 112, 100, 96, 1, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1a08", "#6a4410", "#b07a1a", "#e8b030", "#ffe066", "#fff6c4", "#ffffff" },
                ["height"] = 30,
                ["seed"] = 11,
            },
            (o, e) => new Parry(o, e))
        {
            Title = "Parry",
            Desc = "Two blades clash: their edges flash in an X, a white four-point star rings out, a ring breaks into arcs and a fan of gold sparks sprays up and rains down.",
            Tags = new[] { "impact", "parry", "block", "clash", "sword", "metal", "sparks", "one-shot" },
            Bg = "ground",
        };

        readonly uint[] C;
        readonly double X, Y;
        readonly Particles ps;
        readonly Lights lights;
        readonly ImpactsLib.Spike[] sp;
        readonly ImpactsLib.Seg[] segs;
        readonly double[] edges;
        double t;

        Parry(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            var R = new Rng(o.Seed);
            X = 0;
            Y = -o.Num("height");
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[4], C[4], C[3], C[2] }, emitter: e);
            lights = new Lights(e);
            // a hard four-point star: long arms on the axes, short ones between
            sp = new ImpactsLib.Spike[8];
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + R.Range(-0.04, 0.04), len = i % 2 != 0 ? R.Range(6, 8) : i % 4 == 0 ? R.Range(19, 22) : R.Range(14, 17);
                sp[i] = new ImpactsLib.Spike(a, len, i % 2 != 0 ? 1.2 : 2.2);
            }
            segs = new ImpactsLib.Seg[6];
            for (int i = 0; i < 6; i++)
            {
                double a = (double)i / 6 * TAU + R.Range(-0.1, 0.1), gap = R.Range(0.55, 0.8);
                segs[i] = new ImpactsLib.Seg(a, gap, R.Range(0.1, 0.35));
            }
            // the two edges that met: lines crossing in an X
            double e0 = -0.62 + R.Range(-0.08, 0.08);
            edges = new[] { e0, Math.PI + 0.62 + R.Range(-0.08, 0.08) };

            lights.Add(X, Y, 60, C[4], 0.65, 0.4);
            lights.Add(X, Y, 22, WHITE, 0.8, 0.08);
            // the fan: sparks thrown up and out to both sides, arcing over and raining down to bounce on the floor
            for (int i = 0; i < 24; i++)
            {
                int s = i % 2 != 0 ? 1 : -1;
                double a = -Math.PI / 2 + s * R.Range(0.3, 1.3), v = R.Range(130, 290);
                ps.Spawn(P.Spark, X + s * R.Range(1, 3), Y, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.45, 0.85), WHITE, 1, i > 17 ? R.Range(0.02, 0.07) : 0);
            }
            uint[] moteCols = { C[5], C[6] };
            for (int i = 0; i < 8; i++)
            {
                double a = R.Range(0, TAU), v = R.Range(40, 110);
                ps.Spawn(P.Mote, X, Y, Px.Cos(a) * v, Px.Sin(a) * v - 20, R.Range(0.3, 0.55), R.Pick(moteCols), 0.6, R.Range(0, 0.04));
            }
            for (int i = 0; i < 3; i++)
            {
                double a = R.Range(0, TAU), d = R.Range(12, 26);
                ps.Spawn(P.Star, X + Px.Cos(a) * d, Y + Px.Sin(a) * d, 0, -3, R.Range(0.22, 0.34), WHITE, R.Range(1.6, 2.6), R.Range(0.1, 0.3));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            // the ring: rings out off the star and breaks into arcs that fly on and cool
            double u = (t - 0.03) / 0.36;
            if (u >= 0 && u < 1)
            {
                double r = 8 + 24 * Ease.OutCubic(u), brk = Smooth(0.15, 0.6, u);
                int i = u < 0.15 ? 6 : u < 0.35 ? 5 : u < 0.6 ? 4 : 3;
                ImpactsLib.BrokenRing(p, X, Y, r, Math.Max(1, 2.6 * Px.Pow(1 - u, 1.2)), segs, brk, C[i], C[Math.Max(2, i - 2)], u < 0.6 ? 1 : (1 - u) / 0.4, 1, 0);
                if (u < 0.5) p.GlowRing(X, Y, r, 5, C[4], 0.3 * (1 - u / 0.5));
            }
            ps.Draw(p);
            // the edges flash where they met
            if (t < 0.058)
                foreach (double a in edges)
                {
                    double ca = Px.Cos(a), sa = Px.Sin(a), L = t < 0.025 ? 26 : 32, k = t < 0.025 ? 1 : 0.6;
                    for (double d = -L; d <= L; d += 0.5)
                    {
                        double q = Math.Abs(d) / L;
                        p.Dot(X + ca * d, Y + sa * d, q < 0.4 ? WHITE : q < 0.75 ? C[5] : C[4], q < 0.6 ? 1 : k * (1.3 - q));
                    }
                    p.GlowLine(X - ca * L, Y - sa * L, X + ca * L, Y + sa * L, 4, C[5], 0.35 * k);
                }
            // the star: white, then bigger and gold as it thins, then just a glint
            if (t < 0.025) ImpactsLib.Star(p, X, Y, sp, 1, 1, new[] { C[4], WHITE, WHITE, WHITE }, 1, 2.6);
            else if (t < 0.058) ImpactsLib.Star(p, X, Y, sp, 1.25, 0.65, new[] { C[2], C[5], C[6], WHITE }, 1, 2.6);
            else if (t < 0.092) ImpactsLib.Star(p, X, Y, sp, 1.4, 0.3, new[] { C[2], C[4], C[5], C[6] }, 1, 2);
            double pop = Exp(-t / 0.05);
            if (pop > 0.02)
            {
                p.Glow(X, Y, 30, C[4], 0.5 * pop);
                p.Glow(X, Y, 10, WHITE, 0.4 * pop);
            }
            lights.Draw(p);
        }
    }
}
