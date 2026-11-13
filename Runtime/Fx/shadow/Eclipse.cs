using System;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class Eclipse : Effect
    {
        public static readonly Module Module = new Module("eclipse", "pixelfx/fx/shadow/eclipse", 160, 144, 80, 72, 4, true, 30,
            new Options { ["palette"] = VOID, ["pearl"] = new[] { "#5a4f8a", "#a99be0", "#e6defa", "#fffaf2" }, ["seed"] = 3 },
            (o, e) => new Eclipse(o))
        {
            Title = "Eclipse",
            Desc = "A black moon over the sun: a breathing pearl corona with long streamers, a thin white rim, and one diamond bead blazing on its edge.",
            Tags = new[] { "shadow", "eclipse", "moon", "sun", "corona", "sky", "magic", "loop" },
            Bg = "sky",
        };

        // the moon's radius, the loop, where the bead blazes on the rim (radians, 0 = right, up is negative)
        const double R = 19, D = 4, BEAD = -0.72;
        static double Breathe(double t, double k, double ph) => Px.Sin((TAU * k * t) / D + ph);

        sealed class Stream
        {
            public double a, len, curl, k, ph, w;
        }

        // streamers: crowded toward the equator (left and right), longer there, each curving off as it goes out
        static readonly Stream[] STREAMS = MakeStreams();

        static Stream[] MakeStreams()
        {
            var s = new Stream[30];
            for (int i = 0; i < 30; i++)
            {
                double side = i % 2 != 0 ? 0 : Math.PI, spread = (Rand(i, 1) - 0.5) * (Rand(i, 2) < 0.7 ? 1.3 : 3.1);
                double a = side + spread, eq = Math.Abs(Px.Cos(a));
                s[i] = new Stream { a = a, len = (10 + Rand(i, 3) * 28) * (0.45 + 0.75 * eq), curl = (Rand(i, 4) - 0.5) * 0.00018, k = 1 + (i % 3), ph = Rand(i, 5) * TAU, w = Rand(i, 6) };
            }
            return s;
        }

        readonly uint[] C, Q;
        readonly double S;

        Eclipse(Options o)
        {
            C = Fit(o.Ramp("palette"), 8);
            Q = Fit(o.Ramp("pearl"), 4);
            S = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            // stars twinkling in the dark sky round it
            Cycle(t, D, 26, S, r => r.Range(1.2, 2.6), (age, L, r, i) =>
            {
                double x = r.Range(-78, 78), y = r.Range(-70, 70);
                if (Hypot(x, y) < R + 22) return;
                double k = Px.Sin((Math.PI * age) / L);
                p.Dot(x, y, k > 0.7 ? WHITE : Q[1], k * 1.2);
                if (k > 0.85 && r.Next() < 0.4)
                {
                    p.Dot(x - 1, y, Q[0], 0.8);
                    p.Dot(x + 1, y, Q[0], 0.8);
                }
            });
            // the outer corona's light: a wide pearly bloom, breathing
            double b = 1 + 0.08 * Breathe(t, 1, 0);
            p.Glow(0, 0, 72 * b, C[4], 0.38);
            p.Glow(0, 0, 46 * b, Q[1], 0.34);
            // streamers: fine rays of pearl, white at the root, thinning to violet, curling as they go out
            foreach (Stream s in STREAMS)
            {
                double L = s.len * (1 + 0.18 * Breathe(t, s.k, s.ph));
                for (double d = 4; d <= L; d += 0.5)
                {
                    double u = d / L, a = s.a + s.curl * d * d, r = R + 1 + d, x = Px.Cos(a) * r, y = Px.Sin(a) * r;
                    p.Dot(x, y, u < 0.3 ? Q[2] : u < 0.65 ? Q[1] : C[5], u < 0.75 ? 1 : (1 - u) * 4);
                }
                p.GlowLine(Px.Cos(s.a) * (R + 4), Px.Sin(s.a) * (R + 4), Px.Cos(s.a) * (R + L * 0.6), Px.Sin(s.a) * (R + L * 0.6), 3, Q[1], 0.12);
            }
        }

        public override void Draw(Painter p, double t)
        {
            double b = 1 + 0.08 * Breathe(t, 1, 0);
            // the inner corona: a bright collar hugging the moon, finely streaked, fading out through pearl to violet
            double W = R + 10;
            p.Shade(-W, -W, W, W, (x, y, o) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                if (r < R - 0.5 || r > R + 9 * b) return 0;
                double a = Atan2(y, x), e = (r - R) / (9 * b);
                double streak = 0.55 + 0.3 * Px.Cos(a * 23 + Px.Sin(a * 5) * 2) + 0.15 * Px.Cos(a * 41 - (TAU * t) / D);
                double I = Px.Pow(1 - e, 2.2) * streak * (0.75 + 0.4 * Math.Abs(Px.Cos(a)));
                if (r < R + 0.9) return Q[3];
                if (I < 0.1) return 0;
                if (I > 0.45 && (((int)Math.Floor(x) + (int)Math.Floor(y)) & 1) == 0) o.k = 0.2 * I;
                return I > 0.72 ? Q[2] : I > 0.42 ? Q[1] : I > 0.22 ? C[5] : C[4];
            });
            // the moon: black, a whisper of earthshine on its left limb
            p.Shade(-R, -R, R, R, (x, y, o) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                if (r >= R - 0.5) return 0;
                return r > R - 2.2 && x < -R * 0.45 ? C[1] : C[0];
            });
            // the diamond bead: a blazing point on the rim, rays flaring out and breathing
            double bx = Px.Cos(BEAD) * (R + 0.4), by = Px.Sin(BEAD) * (R + 0.4), k = 0.75 + 0.25 * Breathe(t, 2, 1);
            for (int i = 0; i < 8; i++)
            {
                double a = (i * Math.PI) / 4 + 0.12, len = (i % 2 != 0 ? 7 : 22) * k * (i == 1 || i == 5 ? 1.6 : 1);
                for (double d = 1; d <= len; d += 0.5)
                {
                    double u = d / len, x = bx + Px.Cos(a) * d, y = by + Px.Sin(a) * d;
                    if (x * x + y * y < Px.Pow(R - 0.5, 2)) break;
                    p.Dot(x, y, u < 0.35 ? WHITE : u < 0.7 ? Q[2] : Q[1], u < 0.5 ? 1 : (1 - u) * 2.2);
                }
            }
            p.Circle(bx, by, 3 * k, Q[2]);
            p.Circle(bx, by, 2.2 * k, Q[3]);
            p.Circle(bx, by, 1.4, WHITE);
            p.Glow(bx, by, 46 * k, Q[2], 0.8 * k);
            p.Glow(bx, by, 14, WHITE, 0.8);
            Halo(p, 0, 0, R - 0.5, R + 16, Q[2], 0.35 * b);
            Unlight(p, -R, -R, R, R, (x, y) => x * x + y * y < Px.Pow(R - 0.6, 2));
        }
    }
}
