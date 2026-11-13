// The open-sided star, the spark cone off the surface, the chips, dust and smoke are adapted from Sprite Fusion's
// open-source impact (https://www.spritefusion.com/pixel-art-vfx/impact), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class BulletImpact : Effect
    {
        const double Duration = 1.4;

        public static readonly Module Module = new Module("bullet-impact", "pixelfx/fx/impacts/bullet-impact", 152, 100, 116, 84, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffe26b", "#fff3a6", "#ffffff" },
                ["stone"] = "#5b5079",
                ["height"] = 24,
                ["seed"] = 6,
            },
            (o, e) => new BulletImpact(o, e))
        {
            Title = "Bullet impact",
            Desc = "A bullet hits a wall on the right: a hot half-star, sparks sprayed back in a cone, a puff of grit, chips that fall and bounce, a pock mark cooling to soot.",
            Tags = new[] { "impact", "bullet", "gun", "wall", "sparks", "debris", "directional", "one-shot" },
            Bg = "ground",
        };

        // the wall's face looks left (N); the bullet came in level, a touch downward, and the spray bounces back off it
        const double N = Math.PI, IN = 0.08, BACK = Math.PI - IN;

        static bool Open(double a) => Px.Cos(a - N) > -0.25;

        readonly uint[] C;
        readonly ImpactsLib.Stone st;
        readonly double X, Y;
        readonly Particles ps;
        readonly Lights lights;
        readonly ImpactsLib.Spike[] sp;
        readonly List<int[]> rim = new List<int[]>();
        readonly double[][] cracks;
        double t;

        BulletImpact(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            st = new ImpactsLib.Stone(o.Color("stone"));
            var R = new Rng(o.Seed);
            X = 0;
            Y = -o.Num("height");
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[6], C[5], C[4], C[2] }, emitter: e);
            lights = new Lights(e);
            sp = ImpactsLib.Spikes(R, 9, BACK, len0: 7, len1: 10.5, w0: 1.6, w1: 2.1, fwd: 1.8, back: 0.4);
            for (int i = 0; i < sp.Length; i++)
                if (Px.Cos(sp[i].a - N) < -0.2) sp[i] = ImpactsLib.Stretched(sp[i], 0.3);
            // the pock mark: a ragged hole with a rim that glows and cools
            double seed = o.Num("seed");
            for (int y = -2; y <= 2; y++)
                for (int x = 0; x <= 3; x++)
                {
                    double d = Hypot((x - 1) / 1.6, y / 2.1) + Rand(x, y, seed) * 0.35;
                    if (d < 0.75) rim.Add(new[] { x, y, 0 });
                    else if (d < 1.25) rim.Add(new[] { x, y, 1 });
                }
            cracks = new double[3][];
            for (int i = 0; i < 3; i++)
            {
                double a = R.Range(-1.2, 1.2), L = R.Range(2, 4.5);
                cracks[i] = new[] { Px.Cos(a) * 2.2 + 1, Px.Sin(a) * 2.2, Px.Cos(a) * (2.2 + L) + 1, Px.Sin(a) * (2.2 + L) };
            }

            lights.Add(X - 4, Y, 40, C[4], 0.6, 0.22);
            lights.Add(X - 2, Y, 14, WHITE, 0.6, 0.06);
            // sparks bounce back off the wall in a cone
            for (int i = 0; i < 18; i++)
            {
                double a = BACK + R.Range(-1, 1) * 0.8, v = R.Range(170, 450);
                ps.Spawn(P.Spark, X - 1, Y, Px.Cos(a) * v, Px.Sin(a) * v - 30, R.Range(0.12, 0.4), WHITE, 1, i > 13 ? R.Range(0.01, 0.04) : 0);
            }
            // chips of the wall: flung out, falling, bouncing once and settling
            for (int i = 0; i < 8; i++)
            {
                double a = N + R.Range(-0.8, 0.6), v = R.Range(60, 190);
                ps.Spawn(P.Grit, X - 1, Y + R.Range(-1, 1), Px.Cos(a) * v, Px.Sin(a) * v - R.Range(20, 70), R.Range(0.9, 1.3), R.Pick(new[] { st.light, st.body, st.body, st.lip }), i < 3 ? 2 : 1);
            }
            // a puff of grit dust and a wisp of smoke drifting off
            for (int i = 0; i < 9; i++)
            {
                double a = N + R.Range(-1.1, 1.1), v = R.Range(15, 60);
                ps.Spawn(P.Dust, X - 2, Y + R.Range(-1.5, 1.5), Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.3, 0.55), R.Chance(0.5) ? st.dust : Mix(st.dust, WHITE, 0.25), R.Range(1.2, 2.3));
            }
            ps.Spawn(P.Smoke, X - 4, Y, R.Range(-10, -4), R.Range(-16, -8), R.Range(0.8, 1.2), st.dust, R.Range(1.8, 2.4), 0.05);
        }

        public override void Update(double dt)
        {
            t += dt;
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            // the pock mark: white-hot, cooling through the ramp to a dark rim, fading at the very end
            double heat = Px.Pow(Clamp(1 - t / 0.9), 1.3), fade = 1 - Smooth(Duration - 0.45, Duration - 0.1, t);
            if (fade > 0)
            {
                foreach (double[] c in cracks) p.Line(X + c[0], Y + c[1], X + c[2], Y + c[3], heat > 0.5 ? C[3] : st.shade, fade * (heat > 0.5 ? 1 : 0.85));
                foreach (int[] m in rim)
                {
                    int x = m[0], y = m[1], k = m[2];
                    double h = heat * (k != 0 ? 0.85 : 1.15);
                    p.Dot(X + x, Y + y, k == 0 && h < 0.75 ? ImpactsLib.SOOT : h > 0.85 ? WHITE : h > 0.6 ? C[5] : h > 0.4 ? C[4] : h > 0.22 ? C[3] : h > 0.1 ? C[2] : st.shade, fade);
                }
                if (heat > 0) p.Glow(X + 1, Y, 6 + 6 * heat, C[3], 0.5 * heat);
            }
            // a half ring opening away from the wall
            double u = (t - 0.03) / 0.18;
            if (u >= 0 && u < 1)
            {
                double r = 5 + 12 * Ease.OutCubic(u);
                uint c = u < 0.3 ? C[5] : u < 0.6 ? C[4] : C[3];
                for (double j = 0, n = Math.Ceiling(r * TAU * 1.3); j < n; j++)
                {
                    double g = j / n * TAU;
                    if (Open(g)) p.Dot(X + Px.Cos(g) * r, Y + Px.Sin(g) * r, c, 1.15 - u);
                }
            }
            ps.Draw(p, Layer.Front);
            if (t < 0.03) ImpactsLib.Star(p, X, Y, sp, 1, 1, new[] { C[2], C[5], C[4], WHITE }, 1, 2.2);
            else if (t < 0.06) ImpactsLib.Star(p, X, Y, sp, 1.25, 0.55, new[] { C[1], C[4], C[3], C[5] }, 1, 2.2);
            double pop = Exp(-t / 0.045);
            if (pop > 0.02)
            {
                p.Glow(X - 2, Y, 22, C[3], 0.6 * pop);
                p.Glow(X - 1, Y, 8, WHITE, 0.5 * pop);
                p.GlowLine(X + 1, Y - 16, X + 1, Y + 16, 4, C[4], 0.4 * pop); // light grazing the wall's face
            }
            lights.Draw(p);
        }
    }
}
