using System;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class SoulDrain : Effect
    {
        public static readonly Module Module = new Module("soul-drain", "pixelfx/fx/shadow/soul-drain", 208, 120, 40, 110, 3.2, true, 30,
            new Options { ["palette"] = VOID, ["soul"] = new[] { "#2c3f66", "#7d9fd0", "#cfe6ff", "#ffffff" }, ["seed"] = 8 },
            (o, e) => new SoulDrain(o))
        {
            Title = "Soul drain",
            Desc = "Pale souls are torn loose on the right and dragged down a funnel of streaks into a black crystal vessel, which flares violet as each one lands.",
            Tags = new[] { "shadow", "soul", "drain", "necromancy", "funnel", "directional", "magic", "loop" },
            Bg = "ground",
        };

        // the vessel's centre; the loop; the funnel's streamlines (start points out on the right); four souls a loop
        const double VY = -50, D = 3.2, TRAVEL = 1.45;
        const int LINES = 11, SOULS = 4;
        static double WrapD(double x) => ((x % D) + D) % D;

        sealed class Line
        {
            public double sx, sy, c1x, c1y, c2x, c2y;
        }

        static Line MakeLine(int i)
        {
            double sx = 96 + Rand(i, 1) * 64, sy = -88 + Rand(i, 2) * 72, bend = (Rand(i, 3) - 0.5) * 50;
            return new Line { sx = sx, sy = sy, c1x = sx * 0.55, c1y = sy + bend, c2x = 22 + Rand(i, 4) * 14, c2y = VY + (sy - VY) * 0.25 };
        }

        static readonly Line[] LN = MakeLines();

        static Line[] MakeLines()
        {
            var l = new Line[LINES];
            for (int i = 0; i < LINES; i++) l[i] = MakeLine(i);
            return l;
        }

        // a point along streamline i at u (0 at its start, 1 at the vessel), cubic bezier
        static (double x, double y) Along(Line L, double u)
        {
            double v = 1 - u, a = v * v * v, b = 3 * v * v * u, c = 3 * v * u * u, d = u * u * u;
            return (a * L.sx + b * L.c1x + c * L.c2x, a * L.sy + b * L.c1y + c * L.c2y + d * (VY - L.sy) + d * L.sy);
        }

        static (double born, int line) Soul(int j) => ((j + 0.15 + Rand(j, 7) * 0.5) * (D / SOULS), (int)Math.Floor(Rand(j, 8) * LINES));

        static double Landings(double t)
        {
            double f = 0;
            for (int j = 0; j < SOULS; j++)
            {
                double since = WrapD(t - Soul(j).born - TRAVEL);
                f += Exp(-since / 0.14) * (since < 1 ? 1 : 0);
            }
            return Math.Min(1.4, f);
        }

        // the vessel: a black crystal, two facets lit violet from inside, a hard ridge, a heart of stolen light
        static double Vessel(Painter p, uint[] C, double t, double f)
        {
            double y0 = VY + Px.Sin((TAU * t) / D) * 1.5, W = 9, UP = 14, DN = 12;
            p.Shade(-W - 1, y0 - UP - 1, W + 1, y0 + DN + 1, (x, y, o) =>
            {
                double dy = y - y0, half = dy < 0 ? W * (1 + dy / UP) : W * (1 - dy / DN);
                if (half <= 0 || Math.Abs(x) > half + 0.6) return 0;
                if (Math.Abs(x) > half - 0.4) return f > 0.5 ? C[6] : C[1];
                bool ridge = Math.Abs(x + dy * 0.12) < 0.6, heart = Math.Abs(x) + Math.Abs(dy - 1) < 2.6 + f * 1.5;
                if (heart) return f > 0.4 ? WHITE : C[6];
                if (ridge) return f > 0.3 ? C[7] : C[5];
                if (x < 0) return dy < 0 ? (f > 0.2 ? C[5] : C[4]) : C[3];
                return dy < 0 ? C[2] : C[1];
            });
            p.Glow(0, y0, 30 + 14 * f, C[5], 0.5 + 0.5 * f);
            p.Glow(0, y0 + 1, 8, WHITE, 0.25 + 0.6 * f);
            return y0;
        }

        readonly uint[] C, S;
        readonly double seed;

        SoulDrain(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            S = Fit(o.Ramp("soul"), 4);
            seed = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            // the funnel: streamlines bunching into the vessel, dashes running down them, brighter as they close in
            for (int i = 0; i < LINES; i++)
            {
                Line L = LN[i];
                double ph = WrapD(t + i * 0.37) / D;
                bool has = false;
                double px = 0, py = 0;
                for (double u = 0; u <= 1.0001; u += 0.01)
                {
                    var (x, y) = Along(L, u);
                    double dash = (((u * 5 - ph * 10 + i * 0.3) % 1) + 1) % 1;
                    // a faint line the whole way, bright dashes riding it inward, all brighter as it narrows
                    if (has) p.Line(px, py, x, y, u > 0.6 ? C[3] : C[2], 0.4 + 0.6 * u);
                    if (dash < 0.18) p.Px(x, y, u > 0.7 ? C[7] : u > 0.35 ? C[6] : C[5]);
                    px = x;
                    py = y;
                    has = true;
                }
            }
            p.Glow(10, VY, 40, C[4], 0.22);
        }

        public override void Draw(Painter p, double t)
        {
            double f = Landings(t);
            // where the souls are torn from: a faint pale haze out on the right
            Cycle(t, D, 10, seed, r => r.Range(0.8, 1.3), (age, Lf, r, i) =>
            {
                double x = r.Range(100, 150), y = r.Range(-80, -20) - age * 18, u = age / Lf;
                p.Dot(x, y, S[2], (1 - u) * 0.8);
                p.Glow(x, y, 4, S[1], 0.2 * (1 - u));
            });
            // streaks racing down the funnel, faster as they close on the vessel, white at the head
            Cycle(t, D, 22, seed + 1, r => r.Range(0.4, 0.7), (age, Lf, r, i) =>
            {
                Line L = LN[(int)Math.Floor(r.Next() * LINES)];
                double u0 = r.Range(0.15, 0.5), q = u0 + (1 - u0) * Px.Pow(age / Lf, 2.2), q2 = u0 + (1 - u0) * Px.Pow(Math.Max(0, age - 0.06) / Lf, 2.2);
                var (x, y) = Along(L, q);
                var (tx, ty) = Along(L, q2);
                p.Line(tx, ty, x, y, v => v > 0.7 ? WHITE : v > 0.35 ? C[6] : C[4]);
                p.Glow(x, y, 4, C[6], 0.35);
            });
            double y0 = Vessel(p, C, t, f);
            // motes circling the vessel, in front and behind it
            for (int k = 0; k < 6; k++)
            {
                double a = (TAU * (2 * t)) / D + (k * TAU) / 6, ex = Px.Cos(a) * 15, ey = Px.Sin(a) * 5 - 2, z = Px.Sin(a);
                p.Px(ex, y0 + ey, z > 0 ? C[7] : C[4]);
                if (z > 0) p.Glow(ex, y0 + ey, 4, C[6], 0.35);
            }
            // the souls: a pale head with hollow eyes and an open mouth, a wavering tail, dragged faster as they go
            for (int j = 0; j < SOULS; j++)
            {
                var s = Soul(j);
                double age = WrapD(t - s.born);
                if (age > TRAVEL) continue;
                Line L = LN[s.line];
                double u = Px.Pow(age / TRAVEL, 1.8);
                var (x, y) = Along(L, u);
                if (age < 0.18)
                {
                    double k = 1 - age / 0.18;
                    RingFast(p, L.sx, L.sy, 3 + 9 * (1 - k), 1, k > 0.5 ? S[2] : S[1]);
                    p.Glow(L.sx, L.sy, 12, S[1], 0.5 * k);
                }
                // the tail: a wavering wisp, pale near the head, thinning to violet
                for (int k = 14; k >= 1; k--)
                {
                    double uu = Px.Pow(Math.Max(0, age - k * 0.03) / TRAVEL, 1.8);
                    var (tx, ty) = Along(L, uu);
                    double w = Px.Sin(t * 12 + k * 0.8 + j) * k * 0.16;
                    double r = Math.Max(0.5, 2.6 - k * 0.17);
                    p.Disc(tx, ty + w, r, k < 4 ? S[2] : k < 9 ? S[1] : C[5], 0.75 * (1 - k / 15.0));
                }
                // the head: a pale round face, hollow eyes and an open mouth, a lit rim, stretched as it nears the vessel
                double near = Smooth(0.82, 1, u), hr = 3.4 - near * 1.6;
                p.Circle(x, y, hr + 0.8, S[1]);
                p.Circle(x, y, hr, S[2]);
                p.Px(x - 1, y - 2, S[3]);
                p.Px(x - 2, y - 1, S[3]);
                if (near < 0.5)
                {
                    p.Px(x - 1.5, y, S[0]);
                    p.Px(x + 1.5, y, S[0]);
                    p.Px(x, y + 1.6, S[0]);
                    p.Px(x, y + 2.4, S[0]);
                }
                p.Glow(x, y, 10, S[1], 0.5);
            }
            // each landing: a flash in the heart, a ring off the crystal, a glint
            for (int j = 0; j < SOULS; j++)
            {
                double since = WrapD(t - Soul(j).born - TRAVEL);
                if (since < 0.3)
                {
                    double k = since / 0.3;
                    RingFast(p, 0, y0, 6 + 18 * Ease.OutCubic(k), Math.Max(1, 2 * (1 - k)), k < 0.4 ? C[7] : k < 0.7 ? C[5] : C[3]);
                }
            }
            Halo(p, 0, y0, 4, 22, C[6], 0.25 * f);
        }
    }
}
