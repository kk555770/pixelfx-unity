using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Eyeball : Effect
    {
        public static readonly Module Module = new Module("eyeball", "pixelfx/fx/critters/eyeball", 96, 108, 48, 100, 3.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1420", "#6a3040", "#b07078", "#dcbcb4", "#f4e9de", "#ffffff" },
                ["flesh"] = new[] { "#1e0c16", "#4a1a2c", "#7c2c40", "#b04a58", "#e07a80" },
                ["iris"] = new[] { "#0c2a14", "#1f6a2a", "#3fae3a", "#9be05a", "#e8ffb0" },
                ["hover"] = 48,
                ["seed"] = 1,
            },
            (o, e) => new Eyeball(o))
        {
            Title = "Eyeball",
            Desc = "A bloodshot eyeball floats in its fleshy lids: its glowing iris snaps from one look to the next, it blinks, and its nerve sways behind it as it bobs.",
            Tags = new[] { "critter", "eyeball", "eye", "monster", "spooky", "float", "loop" },
            Bg = "ground",
        };

        const double D = 3.2, R = 13;
        static readonly uint VEIN = Hex("#c42a3a"), FLOOR_SHADOW = Hex("#0d0a12");

        // where it looks: snaps between targets with a little overshoot, holds, snaps on
        static readonly double[][] LOOKS =
        {
            new double[] { 0, 0, 0 }, new[] { 0.55, 4.2, -1.2 }, new[] { 1.15, -4.4, 0.8 }, new[] { 1.95, 1.2, 3.4 }, new[] { 2.5, -2, -3.4 }, new[] { 3.0, 0, 0 },
        };

        static (double x, double y) Look(double t)
        {
            int i = LOOKS.Length - 1;
            while (i > 0 && t < LOOKS[i][0]) i--;
            double t0 = LOOKS[i][0], x1 = LOOKS[i][1], y1 = LOOKS[i][2];
            double[] prev = LOOKS[(i + LOOKS.Length - 1) % LOOKS.Length];
            double x0 = prev[1], y0 = prev[2], u = Ease.OutBack(Clamp((t - t0) / 0.08), 2.2);
            return (Lerp(x0, x1, u), Lerp(y0, y1, u));
        }

        // the lids: open at rest, a quick blink before it looks down
        static double Lid(double t) => t > 1.74 && t < 1.92 ? Px.Sin(Math.PI * ((t - 1.74) / 0.18)) : 0;

        static (double x, double y) At(double t, double hover)
        {
            double a = (t / D) * TAU;
            return (1.5 * Px.Sin(a), -hover - 3 * Px.Sin(2 * a + 0.4));
        }

        // veins: branching red threads creeping in from the rim, fixed to the eyeball's surface
        static List<double[][]> Veins(double seed)
        {
            var output = new List<double[][]>();
            Rng r = NewRng(seed, 77);
            for (int i = 0; i < 7; i++)
            {
                double a = (i / 7.0) * TAU + r.Range(-0.3, 0.3), d = R - 0.5;
                var pts = new List<double[]> { new[] { Px.Cos(a) * d, Px.Sin(a) * d } };
                for (int k = 0; k < 4; k++)
                {
                    a += r.Range(-0.35, 0.35);
                    d -= r.Range(1.2, 2.1);
                    pts.Add(new[] { Px.Cos(a) * d, Px.Sin(a) * d });
                }
                output.Add(pts.ToArray());
            }
            return output;
        }

        static double Sq(double x) => x * x;

        static void DrawEye(Grid g, uint[] C, uint[] Fl, uint[] I, double t, double seed)
        {
            g.Clear();
            var (lx, ly) = Look(t);
            double L = Lid(t);
            List<double[][]> V = Veins(seed);
            bool Lit(double x, double y, double k) => Hypot(-4 + (x + 4) / k, -4.5 + (y + 4.5) / k) <= R;
            // the sclera: a wet sphere lit from the top-left, pinker in its shadow
            g.Shade(-R, -R, R, R, (x, y, o) =>
            {
                double d = Hypot(x, y);
                if (d > R) return 0;
                if (!(Hypot(x + 2, y + 2.2) <= R)) return C[2];
                return Lit(x, y, 0.5) ? C[5] : Lit(x, y, 0.8) ? C[4] : C[3];
            });
            // veins ride the eyeball as it turns
            foreach (double[][] pts in V)
            {
                var sh = new double[pts.Length][];
                for (int k = 0; k < pts.Length; k++)
                {
                    double x = pts[k][0], y = pts[k][1];
                    sh[k] = new[] { x + lx * 0.35 * (1 - Hypot(x, y) / R), y + ly * 0.35 * (1 - Hypot(x, y) / R) };
                }
                for (int k = 1; k < sh.Length; k++) g.Line(sh[k - 1][0], sh[k - 1][1], sh[k][0], sh[k][1], k < 2 ? VEIN : Mix(VEIN, C[3], 0.45));
            }
            // the iris, foreshortened as it turns away, a dark ring, a green that brightens toward the pupil
            double fx = 1 - 0.32 * Px.Pow(Math.Abs(lx) / R, 1.4) * 3, fy = 1 - 0.32 * Px.Pow(Math.Abs(ly) / R, 1.4) * 3;
            double ir = 6.4, pr = 2 + 0.4 * Px.Sin((t / D) * TAU * 2);
            g.Shade(lx - ir - 1, ly - ir - 1, lx + ir + 1, ly + ir + 1, (x, y, o) =>
            {
                double dx = (x - lx) / (ir * fx), dy = (y - ly) / (ir * fy), d = Hypot(dx, dy);
                if (d > 1 || Hypot(x, y) > R - 0.5) return 0;
                double pd = Hypot((x - lx) / fx, (y - ly) / fy);
                if (pd < pr) return C[0];
                if (d > 0.86) return I[0];
                double ang = Atan2(dy, dx);
                bool streak = Px.Sin(ang * 9 + d * 4) > 0.5;
                if (d < 0.5) return streak ? I[4] : I[3];
                if (d < 0.7) return streak ? I[3] : I[2];
                return streak ? I[2] : I[1];
            });
            // a fixed glint from the light, whichever way it looks
            if (L < 0.5)
            {
                g.Set(-4, -5, C[5]);
                g.Set(-3, -5, C[5]);
                g.Set(-4, -4, C[5]);
                g.Set(1, -2, C[5]);
            }
            // the lids: fleshy shells over the top and bottom, lit like the ball they wrap, the upper one sweeping down to blink
            double up = Lerp(-R + 3, R * 0.42, L), dn = Lerp(R - 2.2, R * 0.42, L);
            bool Flit(double x, double y, double k) => Hypot(-4 + (x + 4) / k, -5 + (y + 5) / k) <= R + 0.6;
            g.Shade(-R - 1, -R - 1, R + 1, R + 1, (x, y, o) =>
            {
                double d = Hypot(x, y);
                if (d > R + 0.6) return 0;
                double ue = up + 1.8 * Sq(x / R), de = dn - 1.4 * Sq(x / R);
                uint tone = !(Hypot(x + 2, y + 2.2) <= R + 0.6) ? Fl[1] : Flit(x, y, 0.55) ? Fl[4] : Flit(x, y, 0.85) ? Fl[3] : Fl[2];
                if (y < ue) return y > ue - 1.2 ? Fl[0] : tone;
                if (y > de) return y < de + 1.1 ? Fl[0] : tone == Fl[4] ? Fl[3] : tone;
                return 0;
            });
            g.Outline(C[0]);
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-18, -18, 36, 38), NERVE = new Grid(-20, -70, 40, 64);

        Eyeball(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double hover = o.Num("hover");
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 6), Fl = Fit(o.Ramp("flesh"), 5), I = Fit(o.Ramp("iris"), 5);
            var S = At(t, hover);
            // the optic nerve hangs from the back, trailing the bob a beat behind: one tube, lit down its left side
            Grid ng = NERVE.Clear();
            for (double s = 0; s <= 20; s += 0.5)
            {
                var B = At(t - s * 0.035, hover);
                double sw = 2.6 * Px.Pow(s / 20, 1.3) * Px.Sin((t / D) * TAU * 2 - s * 0.16), r = Lerp(2, 0.6, s / 20);
                double cx = B.x + 1 + sw, cy = B.y + R - 2 + s;
                ng.Shade(cx - r - 1, cy - 1, cx + r + 1, cy + 1, (x, y, oo) =>
                {
                    double v = (x - cx) / Math.Max(0.6, r);
                    if (Math.Abs(v) > 1 || Math.Abs(y - cy) > 0.5) return 0;
                    return v < -0.35 ? Fl[4] : v < 0.4 ? Fl[3] : Fl[2];
                });
            }
            ng.Outline(Fl[0]);
            ng.Draw(p);
            Grid g = GRID;
            DrawEye(g, C, Fl, I, t, o.Seed);
            g.Draw(p, 1, S.x, S.y);
            // the iris glows, an eerie halo round the whole eye, motes circling it
            var (lx, ly) = Look(t);
            double snap = double.NegativeInfinity;
            foreach (double[] l in LOOKS) snap = Math.Max(snap, Math.Max(0, 1 - Math.Abs(Wrap(t - l[0] + D / 2, D) - D / 2) / 0.2));
            double open = 1 - Lid(t);
            p.Glow(S.x + lx, S.y + ly, 15, I[3], (0.5 + 0.3 * snap) * open);
            p.Glow(S.x + lx, S.y + ly, 5, I[4], 0.4 * open);
            p.Glow(S.x, S.y, 50, I[1], 0.3);
            // motes of green light wheel round it on a tilted orbit, trailing short streaks
            for (int i = 0; i < 8; i++)
            {
                double sp = (i % 2 != 0 ? 1 : -1) * (i % 3 != 0 ? 1 : 2), a0 = (i * TAU) / 8, rr = 19 + (i % 3) * 2;
                double[] prev = null;
                for (int k = 5; k >= 0; k--)
                {
                    double a = (t / D) * TAU * sp + a0 - sp * k * 0.035, z = Px.Sin(a), x = S.x + Px.Cos(a) * rr, y = S.y + z * 5 - Px.Cos(a) * 3;
                    if (z < 0 && Hypot(x - S.x, y - S.y) < R + 1)
                    {
                        prev = null;
                        continue;
                    }
                    if (prev != null && k > 0) p.Line(prev[0], prev[1], x, y, I[2], 0.4 + (5 - k) * 0.12);
                    if (k == 0)
                    {
                        p.Px(x, y, z > 0 ? I[4] : I[3]);
                        p.Glow(x, y, 5, I[3], 0.45);
                    }
                    prev = new[] { x, y };
                }
            }
        }

        public override void DrawBack(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] I = Fit(o.Ramp("iris"), 5);
            var S = At(t, o.Num("hover"));
            Shadow(p, S.x, 0, 10, 1.5, FLOOR_SHADOW, 0.5);
            for (double x = -26; x <= 26; x += 6) p.Glow(S.x + x, -1, 18, I[2], 0.15 * (1 - Math.Abs(x) / 34));
        }
    }
}
