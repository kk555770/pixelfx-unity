using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Ghost : Effect
    {
        public static readonly Module Module = new Module("ghost", "pixelfx/fx/critters/ghost", 96, 100, 48, 90, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#10142a", "#232c52", "#3e4f86", "#6b84bb", "#a3b8e2", "#d8e4ff", "#ffffff" },
                ["hover"] = 14,
                ["seed"] = 1,
            },
            (o, e) => new Ghost(o))
        {
            Title = "Ghost",
            Desc = "A sheet ghost bobs over the floor: its scalloped hem ripples and trails its drift, little arms flutter, it glances about and blinks, motes trickle off its hem.",
            Tags = new[] { "critter", "ghost", "spirit", "undead", "spooky", "float", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, R = 10.5;

        // where the ghost is: a lazy drift side to side and a bob, both looping
        static (double x, double y) At(double t, double hover)
        {
            double a = (t / D) * TAU;
            return (4 * Px.Sin(a), -hover - 3 * Px.Sin(2 * a + 0.8));
        }

        static double Sq(double x) => x * x;

        // the sheet's half-width h px above the hem: a dome over a body that flares toward the hem
        static double HalfW(double h)
        {
            const double top = 2 * R + 9;
            if (h > top || h < -1) return -1;
            if (h > top - R) return Math.Sqrt(Math.Max(0, R * R - Sq(h - (top - R))));
            return Lerp(R + 2.2, R, Px.Pow(h / (top - R), 0.7));
        }

        static double DrawSheet(Grid g, uint[] C, double t, double lagX)
        {
            g.Clear();
            const double top = 2 * R + 9, NL = 4, HW = R + 2.2, LW = (2 * HW) / NL;
            double ph = (t / D) * TAU * 2;
            // the lower sheet trails behind the head as it drifts
            double Off(double h) => lagX * Px.Pow(1 - Clamp(h / top), 1.4);
            // four rounded lobes along the hem, lifting one after another in a ripple
            (double j, double u, double d) Lobe(double X)
            {
                double j = Clamp(Math.Floor((X + HW) / LW), 0, NL - 1), c = -HW + (j + 0.5) * LW, u = (X - c) / (LW / 2);
                return (j, u, (2.2 + 1.1 * Px.Sin(ph - j * 1.5)) * Math.Sqrt(Math.Max(0, 1 - u * u)));
            }
            bool Inside(double x, double y)
            {
                double h = -y, X = x - Off(h);
                if (h < 0 && -h > Lobe(X).d) return false;
                double hw = HalfW(Math.Max(0, h));
                return hw >= 0 && Math.Abs(X) <= hw;
            }
            // little arms: rounded nubs that flutter
            bool Arm(double x, double y)
            {
                foreach (int s in new[] { -1, 1 })
                {
                    double ax = s * (R + 1.2) + Off(13), ay = -13 + 1.6 * Px.Sin(ph + s * 1.3);
                    if (Sq((x - ax) / 3) + Sq((y - ay) / 2.2) <= 1) return true;
                }
                return false;
            }
            bool Any(double x, double y) => Inside(x, y) || Arm(x, y);
            const double lx = -9, ly = -top - 1;
            bool Lit(double x, double y, double k) => Any(lx + (x - lx) / k, ly + (y - ly) / k);
            g.Shade(-19, -top - 2, 19, 5, (x, y, o2) =>
            {
                if (!Any(x, y)) return 0;
                double h = -y, X = x - Off(h);
                int i = Lit(x, y, 0.62) ? 5 : 4;
                if (!Any(x + 2.4, y + 1.8)) i = 3;
                if (!Any(x + 1.2, y + 1) && X > 2) i = 2;
                // the gloss: a thin bright arc just inside the top-left of the dome
                if (h > top - R - 2 && X < 1 && !Any(x - 1.5, y - 1.5) && Any(x - 0.6, y - 0.6)) i = 6;
                // the hem: each lobe darker underneath and to its right, a crease where two lobes meet
                if (h < 1)
                {
                    var L = Lobe(X);
                    if (L.u > 0.1 || -h > L.d - 1.2) i = Math.Min(i, 3);
                }
                // folds: short creases running down into the notches between the lobes, lit on their left
                if (h < 7 && h >= -0.5)
                {
                    for (int j = 1; j < NL; j++)
                    {
                        double cx = -HW + j * LW + 0.6 * Px.Sin(ph - j), w = (7 - h) * 0.09;
                        if (Math.Abs(X - cx) <= w + 0.3) i = Math.Min(i, 3);
                        else if (cx - X > w + 0.3 && cx - X < w + 1.3 && i == 4) i = 5;
                    }
                }
                o2.a = h < 2 ? 0.86 : 0.95;
                return C[i];
            });
            return top;
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-20, -40, 40, 46);

        Ghost(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double hover = o.Num("hover");
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            var S = At(t, hover);
            double lagX = At(t - 0.3, hover).x - S.x;
            Grid g = GRID;
            double top = DrawSheet(g, C, t, lagX);
            // the face: two eyes that glance about and blink, a little mouth that oohs
            double look = Wrap(t, D) < 0.7 ? 0 : Wrap(t, D) < 1.3 ? 1 : Wrap(t, D) < 1.9 ? -1 : 0;
            bool blink = Wrap(t, D) > 1.95 && Wrap(t, D) < 2.03;
            double ey = -top + R - 1, mo = 0.5 + 0.5 * Px.Sin((t / D) * TAU * 2);
            foreach (int s in new[] { -1, 1 })
            {
                double ex = s * 4 - (s > 0 ? 1 : 0) + look;
                if (blink)
                {
                    g.Set(ex, ey + 2, C[1]);
                    g.Set(ex + 1, ey + 2, C[1]);
                }
                else
                {
                    for (int k = 0; k < 4; k++)
                    {
                        g.Set(ex, ey + k, C[0]);
                        g.Set(ex + 1, ey + k, C[0]);
                    }
                    g.Set(ex + (look < 0 ? 0 : 1), ey, C[2]);
                }
            }
            double my = ey + 6;
            g.Set(look - 1, my, C[1]);
            g.Set(look, my, C[1]);
            if (mo > 0.4)
            {
                g.Set(look - 1, my + 1, C[0]);
                g.Set(look, my + 1, C[0]);
            }
            g.Outline((x, y, nx, ny) => ny > 0 ? C[2] : C[1], 0.95);
            g.Draw(p, 1, S.x, S.y);
            // motes trickle off the hem and fade as they sink and drift
            Cycle(t, D, 12, o.Seed * 3 + 1, r => r.Range(1, 1.7), (age, L, r, i) =>
            {
                var S0 = At(Wrap(t - age, D), hover);
                double k = age / L, x0 = S0.x + r.Range(-10, 10), y0 = S0.y + 1.5;
                double x = x0 + Px.Sin(age * 2.2 + i) * 2.4, y = y0 + r.Range(5, 11) * age - 3 * age * age;
                double a = Math.Min(1, k * 6) * Math.Min(1, (1 - k) * 2.2), tw = 0.6 + 0.4 * Px.Sin(age * 8 + i * 1.7);
                p.Dot(x, y, tw > 0.8 ? C[6] : C[5], a);
                if (r.Chance(0.3)) p.Dot(x, y - 1, C[4], a * 0.7);
                p.Glow(x, y, 7, C[4], 0.42 * a * tw);
            });
            // a cold glow round it
            p.Glow(S.x, S.y - top * 0.55, 54, C[3], 0.3);
            p.Glow(S.x - 3, S.y - top * 0.72, 14, C[5], 0.12);
        }

        public override void DrawBack(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            var S = At(t, o.Num("hover"));
            double hgt = Clamp((-S.y - 8) / 16);
            // its shadow on the floor, and the floor lit cold under it
            Shadow(p, S.x, 0, 11 - 3 * hgt, 1.6, C[0], 0.55 - 0.2 * hgt);
            for (double x = -24; x <= 24; x += 6) p.Glow(S.x + x, -1, 16, C[4], 0.14 * (1 - Math.Abs(x) / 34) * (1 - 0.4 * hgt));
        }
    }
}
