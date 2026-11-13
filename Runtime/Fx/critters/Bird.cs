using System;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Bird : Effect
    {
        public static readonly Module Module = new Module("bird", "pixelfx/fx/critters/bird", 112, 80, 56, 40, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0a0e1e", "#18244a", "#24407a", "#3466aa", "#5a94d4", "#9cc8f0", "#eaf6ff" },
                ["breast"] = new[] { "#4a1a0e", "#a8441c", "#e8783a", "#ffb070" },
                ["seed"] = 1,
            },
            (o, e) => new Bird(o))
        {
            Title = "Bird",
            Desc = "A songbird flies in place: bursts of wingbeats that lift its body on each downstroke, a glide on spread wings, its tail fanning, a loose feather drifting away.",
            Tags = new[] { "critter", "bird", "flying", "animal", "loop" },
            Bg = "sky",
        };

        const double D = 2.4;
        static readonly uint BEAK = Hex("#f0b030"), BEAKD = Hex("#a86a18");

        static double[][] Pts(params double[] v)
        {
            var o = new double[v.Length / 2][];
            for (int i = 0; i < o.Length; i++) o[i] = new[] { v[i * 2], v[i * 2 + 1] };
            return o;
        }

        // the near wing's key poses, points from the shoulder: leading edge to the tip, then the trailing feathers back
        static readonly double[][] UP = Pts(0, 0, -1, -8, -5, -15, -8, -13.5, -10.5, -10.5, -11, -7, -9, -3, -6, 1);
        static readonly double[][] MID = Pts(0, 0, -3, -2.5, -9, -2.5, -10.5, -0.5, -10, 1.2, -8.5, 2, -7, 2.2, -5, 2);
        static readonly double[][] DOWN = Pts(0, 0, -1, 6, -3.5, 12, -6, 11, -7.5, 8.5, -8, 6, -7, 3, -5, 2);
        static readonly double[][] FOLD = Pts(0, 0, -3, -4, -10, -5.5, -11, -4, -10, -2.5, -8.5, -1, -7, 0.5, -5, 1.5);
        static readonly double[][] GLIDEP = Pts(0, 0, -2, -5, -7, -10, -10, -9, -12, -6.5, -11.5, -3.5, -9, -1, -6, 1);

        static double[][] MixPose(double[][] a, double[][] b, double u) => a.Select((pt, i) => new[] { Lerp(pt[0], b[i][0], u), Lerp(pt[1], b[i][1], u) }).ToArray();

        // flapping until GLIDE, then a glide on spread wings
        const double FLAPS = 4, GLIDE = 1.5;

        sealed class WingState
        {
            public double[][] pts;
            public bool down;
            public double u;
        }

        static WingState Wing(double t)
        {
            t = Wrap(t, D);
            if (t < GLIDE)
            {
                double u = ((t / GLIDE) * FLAPS) % 1;
                // a quick downstroke through the stretched-out middle, a slower upstroke folded close
                if (u < 0.4)
                {
                    double k = Ease.InOutSine(u / 0.4);
                    return new WingState { pts = k < 0.5 ? MixPose(UP, MID, k * 2) : MixPose(MID, DOWN, k * 2 - 1), down = true, u = u };
                }
                double k2 = Ease.InOutSine((u - 0.4) / 0.6);
                return new WingState { pts = k2 < 0.5 ? MixPose(DOWN, FOLD, k2 * 2) : MixPose(FOLD, UP, k2 * 2 - 1), down = false, u = u };
            }
            double g = (t - GLIDE) / (D - GLIDE), into = Smooth(0, 0.15, g), outv = Smooth(0.85, 1, g);
            return new WingState { pts = MixPose(MixPose(UP, GLIDEP, into), UP, outv), down = false, u = 0 };
        }

        // the body rises as the wings push down, and sinks a touch through the glide
        static double Lift(double t)
        {
            t = Wrap(t, D);
            double y = 0;
            for (int k = 0; k < 6; k++)
                if (Wing(t - k * 0.02).down)
                    y -= 0.45;
            if (t >= GLIDE) y += 2.2 * Px.Sin(Math.PI * ((t - GLIDE) / (D - GLIDE)));
            return y + 1.2;
        }

        static double[][] WingPts(WingState w, bool far) =>
            w.pts.Select(pt => new[] { 1 + pt[0] - (far ? 1 : 0), -2 + pt[1] * (far ? 0.85 : 1) - (far ? 1 : 0) }).ToArray();

        static double Sq(double x) => x * x;

        static bool InBody(double x, double y) => Sq((x + 1) / 8.5) + Sq(y / 5.2) <= 1 || Sq(x - 6.5) + Sq(y + 3) <= Sq(4.6);

        static void DrawBird(Grid g, uint[] C, uint[] Br, double t, WingState w)
        {
            g.Clear();
            // the far wing, behind the body and in shadow
            g.Poly(WingPts(w, true), C[1]);
            // the tail: three feathers fanning as it brakes and lifts
            double tf = 0.25 + 0.35 * Px.Sin((t / D) * TAU * 2);
            for (int i = 0; i < 3; i++)
            {
                double a = Math.PI + (i - 1) * tf * 0.5 + 0.1, L = 9 - Math.Abs(i - 1);
                g.Poly(new[]
                {
                    new[] { -7, -2.4 }, new[] { -7 + Px.Cos(a) * L, -1 + Px.Sin(a) * L - 1.2 }, new[] { -7 + Px.Cos(a) * L, -1 + Px.Sin(a) * L + 1.2 }, new[] { -7, 0.6 },
                }, i == 0 ? C[3] : C[2]);
            }
            // body and head: a blue back lit from above, a rust breast, a pale belly
            g.Shade(-10, -9, 12, 6, (x, y, o) =>
            {
                if (!InBody(x, y)) return 0;
                bool top = !InBody(x, y - 2.2), under = !InBody(x + 0.5, y + 1.6);
                if (y > -0.5 && x > -2.5) return under ? Br[1] : x > 3 && y < 2 ? Br[3] : Br[2];
                if (y > 1.5) return under ? C[4] : C[5];
                return top ? (x > 2 ? C[5] : C[4]) : y < -3.5 && x > 3 ? C[4] : C[3];
            });
            // beak, eye with a glint, a dark cap stripe
            g.Poly(new[] { new[] { 10.5, -4.5 }, new[] { 14.5, -3 }, new[] { 10.5, -2 } }, (x, y) => y < -3 ? BEAK : BEAKD);
            g.Set(8, -4, C[0]);
            g.Set(8, -5, C[6]);
            g.Set(9, -4, C[0]);
            g.Line(5, -7, 9, -6.5, C[2]);
            // the near wing: lit along its leading edge, the flight feathers splayed at the tips
            double[][] wp = WingPts(w, false);
            double Edge(double x, double y)
            {
                double m = double.PositiveInfinity;
                for (int i = 1; i < 3; i++)
                {
                    double ax = wp[i - 1][0], ay = wp[i - 1][1], bx = wp[i][0], by = wp[i][1], dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
                    if (L2 == 0 || double.IsNaN(L2)) L2 = 1;
                    double u = Clamp(((x - ax) * dx + (y - ay) * dy) / L2);
                    m = Math.Min(m, Hypot(ax + dx * u - x, ay + dy * u - y));
                }
                return m;
            }
            g.Poly(wp, (x, y) =>
            {
                double d = Edge(x, y);
                return d < 1.2 ? C[5] : d < 3 ? C[4] : C[3];
            });
            // the flight feathers: dark lines splaying from the wrist to the trailing edge
            for (int i = 3; i < 7; i++) g.Line(wp[1][0], wp[1][1], Lerp(wp[1][0], wp[i][0], 0.92), Lerp(wp[1][1], wp[i][1], 0.92), C[2]);
            g.Outline(C[0]);
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-26, -26, 52, 46);

        Bird(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7), Br = Fit(o.Ramp("breast"), 4);
            WingState w = Wing(t);
            double y = Lift(t);
            Grid g = GRID;
            // on the downstroke, a see-through echo of the wing a moment before smears the beat
            if (w.down && w.u > 0.1)
            {
                double s = t - 0.05;
                WingState ws = Wing(s);
                g.Clear();
                g.Poly(WingPts(ws, false), C[6]);
                for (int i = 0; i < g.a.Length; i++) g.a[i] = 0.42f;
                g.Draw(p, 1, 0, Round(Lift(s)));
            }
            DrawBird(g, C, Br, t, w);
            g.Draw(p, 1, 0, Round(y));
            // a loose feather drifts off and rocks down out of sight
            double fa = Wrap(t - 0.45, D);
            if (fa < 1.8)
            {
                double fx = -6 - 14 * fa - 3 * Px.Sin(fa * 5), fy = 4 + 9 * fa + 1.5 * Px.Cos(fa * 5) * fa, rock = Px.Sin(fa * 5) * 0.8;
                double a = Math.Min(1, (1.8 - fa) * 2);
                for (int i = -2; i <= 2; i++) p.Dot(fx + i * Px.Cos(rock), fy + i * Px.Sin(rock) * 0.8, i < 0 ? C[5] : C[4], a);
                p.Dot(fx + 2.6 * Px.Cos(rock), fy + 2.6 * Px.Sin(rock) * 0.8, C[3], a);
            }
            // a little moonlight on its back
            p.Glow(2, y - 6, 16, C[5], 0.12);
        }
    }
}
