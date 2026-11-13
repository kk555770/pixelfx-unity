using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Bat : Effect
    {
        public static readonly Module Module = new Module("bat", "pixelfx/fx/critters/bat", 112, 80, 56, 40, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0c0912", "#1f1629", "#382645", "#5b3f66", "#866089", "#b88fb0", "#e8cfe0" },
                ["eyes"] = new[] { "#8a1020", "#ff3b30", "#ffb04a", "#fff0c0" },
                ["seed"] = 1,
            },
            (o, e) => new Bat(o))
        {
            Title = "Bat",
            Desc = "A bat flaps in four snappy wing frames along a figure-eight, its body lifting on each downbeat, moonlight on its wing bones, red eyes glowing and glinting.",
            Tags = new[] { "critter", "bat", "night", "flying", "spooky", "loop" },
            Bg = "sky",
        };

        const double D = 2.4, FLAPS = 6;

        sealed class WingFrame
        {
            public double arm, al, lift;
            public double[] f, fl;
        }

        // the four wing frames for the right wing: arm angle and length, three fingers' angles and lengths, the lift
        static readonly WingFrame[] FRAMES =
        {
            new WingFrame { arm = -58, al = 8, f = new double[] { -112, -76, -38 }, fl = new double[] { 9, 11, 9 }, lift = 1 },
            new WingFrame { arm = -18, al = 9, f = new double[] { -40, -12, 16 }, fl = new double[] { 10, 11, 9 }, lift = 0 },
            new WingFrame { arm = 28, al = 8, f = new double[] { 50, 72, 94 }, fl = new double[] { 9, 10, 8 }, lift = -2 },
            new WingFrame { arm = -42, al = 6, f = new double[] { -66, -38, -10 }, fl = new double[] { 7, 8, 6 }, lift = -1 },
        };

        static double Rad(double d) => (d * Math.PI) / 180;

        // figure-eight flight, and which wing frame it's on
        static (double x, double y, double vx) At(double t)
        {
            double a = (t / D) * TAU;
            return (22 * Px.Sin(a), 7 * Px.Sin(2 * a), Px.Cos(a));
        }

        static int FrameAt(double t) => (int)Math.Floor(Wrap((t / D) * FLAPS, 1) * 4) % 4;

        sealed class Wing
        {
            public double[] sh, wr, hip;
            public double[][] tips, poly;
        }

        static Wing WingPts(WingFrame F, double s)
        {
            double[] sh = { s * 2.5, -2 };
            double ar = Rad(F.arm);
            double[] wr = { sh[0] + s * Px.Cos(ar) * F.al, sh[1] + Px.Sin(ar) * F.al };
            var tips = new double[3][];
            for (int i = 0; i < 3; i++) tips[i] = new[] { wr[0] + s * Px.Cos(Rad(F.f[i])) * F.fl[i], wr[1] + Px.Sin(Rad(F.f[i])) * F.fl[i] };
            double[] Pull(double[] a, double[] b, double k) => new[] { Lerp((a[0] + b[0]) / 2, wr[0], k), Lerp((a[1] + b[1]) / 2, wr[1], k) };
            double[] hip = { s * 2, 3.5 };
            return new Wing
            {
                sh = sh,
                wr = wr,
                tips = tips,
                hip = hip,
                poly = new[] { sh, wr, tips[0], Pull(tips[0], tips[1], 0.22), tips[1], Pull(tips[1], tips[2], 0.22), tips[2], Pull(tips[2], hip, 0.38), hip },
            };
        }

        static void DrawBat(Grid g, uint[] C, uint[] E, WingFrame F, double t)
        {
            g.Clear();
            // wings: a membrane, darker near the body, lit along the leading edge; finger bones over it
            foreach (int s in new[] { -1, 1 })
            {
                Wing w = WingPts(F, s);
                double Lead(double x, double y)
                {
                    double ax = w.wr[0], ay = w.wr[1], bx = w.tips[0][0], by = w.tips[0][1], dx = bx - ax, dy = by - ay, L = Hypot(dx, dy);
                    if (L == 0 || double.IsNaN(L)) L = 1;
                    return Math.Abs(((x - ax) * dy - (y - ay) * dx) / L);
                }
                g.Poly(w.poly, (x, y) =>
                {
                    double near = Hypot(x - w.sh[0], y - w.sh[1]);
                    if (Lead(x, y) < 1.2 && F.arm < 20) return C[3];
                    return near < 4.5 ? C[1] : C[2];
                });
                g.Line(w.sh[0], w.sh[1], w.wr[0], w.wr[1], C[4]);
                foreach (double[] tip in w.tips) g.Line(w.wr[0], w.wr[1], tip[0], tip[1], u => u < 0.6 ? C[4] : C[3]);
                g.Set(w.wr[0], w.wr[1] - 1, C[5]);
            }
            // the body: fur lit top-left, a paler belly, big ears with pink insides
            g.Ellipse(0, 0.5, 3.8, 4.8, (nx, ny) => nx * -0.6 + ny * -0.8 > 0.45 ? C[4] : nx * 0.6 + ny * 0.8 > 0.55 ? C[2] : ny > 0.1 && Math.Abs(nx) < 0.5 ? C[4] : C[3]);
            foreach (int s in new[] { -1, 1 })
            {
                g.Poly(new[] { new[] { s * 0.6, -3.5 }, new[] { s * 3.6, -9.5 }, new[] { s * 3.8, -3 } }, s < 0 ? C[4] : C[3]);
                g.Line(s * 2.6, -4, s * 3.2, -7.5, C[5]);
            }
            // eyes, a snout, two fangs, tucked feet
            g.Set(-2, -1, E[1]);
            g.Set(1, -1, E[1]);
            g.Set(-1, 0, C[1]);
            g.Set(0, 0, C[1]);
            g.Set(-1, 1, C[6]);
            g.Set(0, 1, C[6]);
            g.Set(-2, 5, C[3]);
            g.Set(1, 5, C[3]);
            g.Outline(C[0]);
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-24, -22, 48, 40);

        Bat(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7), E = Fit(o.Ramp("eyes"), 4);
            var S = At(t);
            int fi = FrameAt(t);
            WingFrame F = FRAMES[fi];
            double X = Round(S.x), Y = Round(S.y + F.lift);
            // its glowing eyes leave a fading streak of red light along its flight
            for (int k = 2; k <= 16; k++)
            {
                var A = At(t - (k - 1) * 0.022);
                var B2 = At(t - k * 0.022);
                double u = 1 - k / 17.0;
                foreach (double ex in new[] { -1.5, 1.5 }) p.GlowLine(A.x + ex, A.y - 0.5, B2.x + ex, B2.y - 0.5, 2.6, E[1], 0.16 * u);
            }
            // moonlit dust drifting in the night air
            Cycle(t, D, 9, o.Seed * 11 + 1, D, (age, L, r, i) =>
            {
                double x = r.Range(-50, 50) + Px.Sin(age * 1.1 + r.Next() * 6) * 3, y = r.Range(-34, 34) + age * r.Range(-2, 3);
                double tw = Px.Sin(age * 2.6 + r.Next() * 6), a = Min(1, age * 2, (L - age) * 2) * (0.4 + 0.5 * tw);
                p.Dot(x, y, tw > 0.7 ? C[6] : C[5], a);
                if (tw > 0.5) p.Glow(x, y, 3, C[5], 0.2 * a);
            });
            Grid g = GRID;
            // on the downbeat the wings leave a see-through echo of where they just were
            if (fi == 2)
            {
                DrawBat(g, C, E, FRAMES[1], t);
                g.Recolor((x, y, c, al, tg) => Mix(c, C[4], 0.35));
                for (int i = 0; i < g.a.Length; i++) g.a[i] = 0.35f;
                g.Draw(p, 1, X, Y - FRAMES[1].lift + F.lift);
            }
            DrawBat(g, C, E, F, t);
            g.Draw(p, 1, X, Y);
            // moonlight catches the top of the head and wings
            p.Glow(X - 3, Y - 6, 10, C[5], 0.12);
            // the eyes glow, and glint now and then
            foreach (double ex in new[] { -1.5, 1.5 })
            {
                p.Glow(X + ex, Y - 0.5, 7, E[1], 0.6);
                p.Glow(X + ex, Y - 0.5, 2.5, E[2], 0.5);
            }
            p.Glow(X, Y, 22, E[0], 0.25);
            foreach (double g0 in new[] { 0.3, 1.5 })
            {
                double k = Wrap(t - g0, D) / 0.24;
                if (k < 1) Sim.Sparkle(p, X - 2, Y - 1, 1 + 2.5 * Px.Sin(Math.PI * k), E[3], E[2], 0.4 * Px.Sin(Math.PI * k));
            }
        }
    }
}
