using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class HitSpark : Effect
    {
        public static readonly Module Module = new Module("hit-spark", "pixelfx/fx/impacts/hit-spark", 136, 80, 44, 64, 0.5, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffe26b", "#fff3a6", "#ffffff" },
                ["height"] = 20.0,
                ["aim"] = 0.1,
                ["seed"] = 4.0,
            },
            (o, e) => new HitSpark(o, e));

        readonly struct Ray
        {
            public readonly double A, L, W;

            public Ray(double a, double l, double w)
            {
                A = a;
                L = l;
                W = w;
            }
        }

        readonly uint[] C;
        readonly double X, Y, A;
        readonly Ray[] rays;
        readonly Particles ps;
        readonly Lights lights;
        double t;

        HitSpark(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            var R = new Rng(o.Seed);
            X = 0;
            Y = -o.Num("height");
            A = o.Num("aim");
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[6], C[5], C[4], C[2] }, emitter: e);
            lights = new Lights(e);
            double tilt = R.Range(-0.08, 0.08);
            rays = new[]
            {
                new Ray(A + tilt, R.Range(33, 37), 2.2),
                new Ray(A + Math.PI + tilt, R.Range(8, 10), 2.2),
                new Ray(A - Math.PI / 2 + tilt, R.Range(15, 18), 2.6),
                new Ray(A + Math.PI / 2 + tilt, R.Range(10, 12), 2.3),
                new Ray(A - Math.PI / 4 + tilt, R.Range(5, 7), 1),
            };

            lights.Add(X, Y, 38, C[4], 0.35, 0.18);
            lights.Add(X + Math.Cos(A) * 10, Y + Math.Sin(A) * 10, 22, C[5], 0.45, 0.1);
            lights.Add(X, -2, 26, C[3], 0.22, 0.22);
            for (int i = 0; i < 9; i++)
            {
                double a = A + R.Range(-0.6, 0.6) - 0.15, v = R.Range(170, 360);
                double life = R.Range(0.25, 0.45);
                ps.Spawn(P.Spark, X + 1, Y, Math.Cos(a) * v, Math.Sin(a) * v - 40, life, WHITE, 1, i > 6 ? R.Range(0.01, 0.03) : 0);
            }
            uint[] chips = { C[5], C[4], C[4], C[3] };
            for (int i = 0; i < 6; i++)
            {
                double a = A + R.Range(-1.1, 0.5) - 0.3, v = R.Range(70, 170);
                double vy = Math.Sin(a) * v - R.Range(30, 80), life = R.Range(0.35, 0.45);
                ps.Spawn(P.Grit, X + 1, Y, Math.Cos(a) * v, vy, life, R.Pick(chips), 1);
            }
        }

        static void DrawRay(Painter p, double x, double y, double a, double L, double w, uint[] C, double from = 0)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            for (double d = from * L; d <= L; d += 0.5)
            {
                double u = d / L, hw = w * Math.Pow(1 - u, 1.6);
                uint col = u < 0.3 ? WHITE : u < 0.62 ? C[5] : u < 0.88 ? C[4] : C[3];
                p.Px(x + c * d, y + s * d, col);
                if (hw > 0.45)
                    for (double o = 0.5; o <= hw; o += 0.5)
                        foreach (double e in new[] { -o, o })
                            p.Px(x + c * d - s * e, y + s * d + c * e, u < 0.3 && o < 1 ? C[5] : C[4]);
            }
        }

        static void Diamond(Painter p, double x, double y, int r, uint[] C)
        {
            for (int dy = -r - 1; dy <= r + 1; dy++)
                for (int dx = -r - 1; dx <= r + 1; dx++)
                {
                    int m = Math.Abs(dx) + Math.Abs(dy);
                    if (m <= r) p.Px(x + dx, y + dy, WHITE);
                    else if (m == r + 1) p.Px(x + dx, y + dy, C[3]);
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
            ps.Draw(p);
            if (t < 0.025)
            {
                foreach (Ray r in rays) DrawRay(p, X, Y, r.A, r.L, r.W, C);
                Diamond(p, X, Y, 4, C);
            }
            else if (t < 0.058)
            {
                foreach (Ray r in rays) DrawRay(p, X, Y, r.A, r.L * 1.2, r.W * 0.55, C, 0.12);
                Diamond(p, X, Y, 3, C);
            }
            else if (t < 0.092)
            {
                Ray r = rays[0];
                DrawRay(p, X, Y, r.A, r.L * 1.3, 0.6, new[] { C[0], C[1], C[2], C[3], C[3], C[4] }, 0.4);
            }
            double pop = Math.Exp(-t / 0.045);
            if (pop > 0.02)
            {
                p.Glow(X, Y, 22, C[4], 0.3 * pop);
                p.Glow(X, Y, 7, WHITE, 0.15 * pop);
                p.GlowLine(X, Y, X + Math.Cos(A) * 26, Y + Math.Sin(A) * 26, 4, C[5], 0.25 * pop);
            }
            lights.Draw(p);
        }
    }
}
