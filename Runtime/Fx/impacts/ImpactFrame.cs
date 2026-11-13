using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class ImpactFrame : Effect
    {
        const int W = 320, H = 180, OX = 160, OY = 90;

        public static readonly Module Module = new Module("impact-frame", "pixelfx/fx/impacts/impact-frame", W, H, OX, OY, 0.3, false, 30,
            new Options
            {
                ["palette"] = new[] { "#000000", "#ffffff" },
                ["lines"] = 64,
                ["seed"] = 19,
            },
            (o, e) => new ImpactFrame(o, e))
        {
            Title = "Impact frame",
            Desc = "An anime impact: two frames of white with black burst lines rushing at the hit, two frames inverted, then the lines pull out past the edges and vanish.",
            Tags = new[] { "impact", "hit", "anime", "manga", "flash", "speed-lines", "screen", "one-shot" },
            Bg = "black",
        };

        const double FRAME = 1.0 / 30; // the beats are whole frames: 2 white, 2 inverted, 4 pulling out

        readonly struct BurstLine
        {
            public readonly double a, inner, w;

            public BurstLine(double a, double inner, double w)
            {
                this.a = a;
                this.inner = inner;
                this.w = w;
            }
        }

        // burst lines: wedges aimed at the hit from past the edges, each its own width and reach, in angle order
        static BurstLine[] Burst(Rng R, int n)
        {
            var list = new BurstLine[n];
            for (int i = 0; i < n; i++)
            {
                bool thick = R.Chance(0.18);
                double a = (i + R.Range(0.1, 0.9)) / n * TAU;
                double inner = thick ? R.Range(30, 48) : R.Range(18, 70);
                list[i] = new BurstLine(a, inner, thick ? R.Range(5, 8) : R.Range(1.2, 3.6));
            }
            return list;
        }

        // the jagged burst at the middle: a spiky star whose points shift every frame
        static double Spikes(double a, double ph) => 10 + 6 * Math.Abs(Px.Sin(a * 5 + ph)) + 4 * Math.Abs(Px.Sin(a * 11 + ph * 2.7)) + 2 * Px.Sin(a * 23 + ph);

        readonly uint INK, PAPER;
        readonly byte[][] masks = new byte[8][];
        double t;

        ImpactFrame(Options o, Emitter e)
        {
            uint[] C = Fit(o.Ramp("palette"), 3);
            var R = new Rng(o.Seed);
            INK = C[0];
            PAPER = C[2];
            int x0 = -OX, y0 = -OY, lines = (int)o.Num("lines");
            BurstLine[][] sets = { Burst(R, lines), Burst(R, lines) };
            var jit = new double[2][];
            for (int i = 0; i < 2; i++)
            {
                double jx = Round(R.Range(-2, 2));
                jit[i] = new[] { jx, Round(R.Range(-1, 1)) };
            }
            double[] ph = { R.Range(0, TAU), R.Range(0, TAU), R.Range(0, TAU), R.Range(0, TAU) };
            // every frame is fixed, so work each one out once: which pixels are ink (1) or paper (2), 0 for nothing
            for (int f = 0; f < 8; f++)
            {
                var m = new byte[W * H];
                BurstLine[] set = sets[f % 2];
                int n = set.Length;
                double jx = f < 4 ? jit[f % 2][0] : 0, jy = f < 4 ? jit[f % 2][1] : 0;
                double reach = f < 4 ? 0 : Px.Pow((f - 3) / 4.5, 1.5), thin = f < 4 ? 1 : 1 - (f - 3) / 5.5;
                for (int j = 0; j < H; j++)
                    for (int i = 0; i < W; i++)
                    {
                        double X = x0 + i + 0.5 - jx, Y = y0 + j + 0.5 - jy, rho = Hypot(X, Y * 1.35), ang = Atan2(Y * 1.35, X);
                        int slot = (int)Math.Floor((ang % TAU + TAU) % TAU / (TAU / n));
                        bool line = false;
                        for (int k = -1; k <= 1 && !line; k++)
                        {
                            BurstLine l = set[(slot + k + n) % n];
                            double d = ((ang - l.a) % TAU + TAU) % TAU;
                            if (d > Math.PI) d = TAU - d;
                            double inner = l.inner + reach * 150, hw = l.w * Clamp((rho - inner) / 40) * thin;
                            line = rho > inner && d * rho < Math.Max(0.5, hw);
                        }
                        if (f < 4)
                        {
                            double r2 = Hypot(X, Y * 1.1), core = Spikes(Atan2(Y * 1.1, X), ph[f]) * (f == 0 ? 0.85 : 1 + f * 0.08);
                            m[j * W + i] = (byte)(r2 < core ? (r2 < 4 + f ? 2 : 1) : line ? 1 : 2);
                        }
                        else m[j * W + i] = (byte)(line ? 2 : 0);
                    }
                masks[f] = m;
            }
        }

        public override void Update(double dt)
        {
            t += dt;
        }

        public override void Draw(Painter p)
        {
            int f = (int)Math.Floor(t / FRAME + 1e-6);
            if (f >= 8) return;
            // two frames of ink on paper, two inverted, then paper lines pulling out over the scene
            byte[] m = masks[f];
            bool inv = f == 2 || f == 3;
            uint ink = inv ? PAPER : INK, paper = inv ? INK : PAPER;
            double x0 = -OX, y0 = -OY;
            p.Shade(x0, y0, x0 + W, y0 + H, (x, y, o) =>
            {
                double i = Math.Floor(x - x0), j = Math.Floor(y - y0);
                if (i < 0 || j < 0 || i >= W || j >= H) return 0;
                byte v = m[(int)(j * W + i)];
                return v == 1 ? ink : v == 2 ? (f < 4 ? paper : PAPER) : 0;
            });
        }
    }
}
