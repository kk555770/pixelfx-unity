using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Frog : Effect
    {
        public static readonly Module Module = new Module("frog", "pixelfx/fx/critters/frog", 112, 88, 56, 80, 2.6, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0a160c", "#173a18", "#256024", "#3a8a2e", "#62b440", "#a8e070", "#e8ffc0" },
                ["belly"] = new[] { "#5e5a24", "#bdb45a", "#efe9a8" },
                ["seed"] = 1,
            },
            (o, e) => new Frog(o))
        {
            Title = "Frog",
            Desc = "A frog puffs its throat, blinks, crouches and leaps to snap a buzzing fly out of the air, then lands with a squash and a puff of dust, gulps and settles.",
            Tags = new[] { "critter", "frog", "animal", "hop", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 2.6, T_CROUCH = 0.95, T_UP = 1.2, T_LAND = 1.62, T_SNAP = 1.4, T_BACK = 1.95, G = 1000;
        static readonly double HOP = (G * ((T_LAND - T_UP) * (T_LAND - T_UP))) / 8;
        static readonly uint[] EYE = Ramp("#3a2a08", "#c89420", "#ffd860");
        static readonly uint TONGUE = Hex("#e8607a"), FLY = Hex("#141018"), WINGS = Hex("#c8d0e0"), DUST = Hex("#8a7f92");
        static readonly uint GRIT_DARK = Hex("#6a6074"), GRIT_LIGHT = Hex("#9a90a4");

        sealed class FrogPose
        {
            public double by, rot, sq, leg, throat, shut;
            public bool air;
        }

        static readonly double[][] THROATS = { new[] { 0.05, 0.45 }, new[] { 0.5, 0.85 }, new[] { 1.75, 1.95 } };

        // the pose at loop time t: lift off the floor, nose tilt, squash, hind legs out, throat puffed, eye shut
        static FrogPose PoseAt(double t)
        {
            double by = 0, rot = 0, sq = 0, leg = 0, throat = 0, shut = 0;
            double age = Wrap(t - T_LAND, D);
            foreach (double[] ab in THROATS)
            {
                double a = ab[0], b = ab[1];
                if (t > a && t < b) throat = Px.Pow(Px.Sin(Math.PI * ((t - a) / (b - a))), 0.7) * (a > 1 ? 0.6 : 1);
            }
            if (t > 0.88 && t < 0.95) shut = 1;
            if (t >= T_CROUCH && t < T_UP)
            {
                double u = Ease.InOutSine(Seg(t, T_CROUCH, T_UP));
                sq = -0.18 * u;
                rot = -0.12 * u;
            }
            else if (t >= T_UP && t < T_LAND)
            {
                double q = (t - T_UP) / (T_LAND - T_UP);
                by = -HOP * 4 * q * (1 - q);
                rot = Lerp(0.42, -0.25, q);
                sq = Lerp(0.18, 0.02, Math.Min(1, q * 2));
                leg = q < 0.75 ? 1 : 1 - (q - 0.75) / 0.25;
            }
            else
            {
                sq = -0.24 * Spring(age, 2.4, 6.5);
                rot = -0.08 * Exp(-age * 6);
            }
            return new FrogPose { by = by, rot = rot, sq = sq, leg = leg, throat = throat, shut = shut, air = t >= T_UP && t < T_LAND };
        }

        // the fly: buzzing a little loop over the frog, snapped up at T_SNAP, buzzing back in from the side at T_BACK
        static double[] FlyAt(double t)
        {
            if (t > T_SNAP && t < T_BACK) return null;
            double a = (t / D) * TAU * 3;
            double[] home = { 16 + 6 * Px.Sin(a), -40 + 3 * Px.Sin(2 * a) };
            double inn = Wrap(t - T_BACK, D);
            if (inn < 0.45)
            {
                double u = Ease.OutCubic(inn / 0.45);
                return new[] { Lerp(-52, home[0], u), Lerp(-46, home[1], u) + Px.Sin(inn * 30) * 1.5 };
            }
            return home;
        }

        static double Sq(double x) => x * x;
        static bool InBody(double x, double y) => Sq((x + 1) / 10.5) + Sq((y + 7) / 6) <= 1 || Sq((x - 6) / 6.2) + Sq((y + 8.6) / 4.8) <= 1;
        static bool InEye(double x, double y) => Sq(x - 5.5) + Sq(y + 13.2) <= Sq(3.2);
        static readonly double[][] SPOTS = { new double[] { -5, -10, 1.6 }, new[] { 0, -11.5, 1.3 }, new double[] { -8, -7, 1.2 }, new[] { 3, -12.5, 0.9 } };

        static void DrawFrog(Grid g, uint[] C, uint[] Bl, FrogPose Pz, double t)
        {
            g.Clear();
            double cs = Px.Cos(Pz.rot), sn = Px.Sin(Pz.rot), sx = 1 + Pz.sq * -0.6, sy = 1 + Pz.sq;
            // into the frog's own frame: centred on its body, turned with it, squashed with it
            (double u, double v) Local(double x, double y)
            {
                double dx = x, dy = y + 7;
                return ((dx * cs + dy * sn) / sx, (-dx * sn + dy * cs) / sy - 7);
            }
            // the body and head: lit on top, a pale belly, dark spots on the back
            bool Lit(double x, double y, double k) => InBody(-4 + (x + 4) / k, -13 + (y + 13) / k);
            g.Shade(-15, -22, 15, 1, (x, y, o) =>
            {
                var (u, v) = Local(x, y);
                if (!InBody(u, v) && !InEye(u, v)) return 0;
                if (InEye(u, v))
                {
                    double d = Hypot(u - 5.5, v - 13.2 * -1);
                    if (Pz.shut != 0) return v < -13.2 ? C[4] : C[3];
                    if (Math.Abs(v + 13) < 0.7 && Math.Abs(u - 6) < 1.8) return C[0];
                    if (u < 4.7 && v < -14) return EYE[2];
                    return d < 2.4 ? EYE[1] : EYE[0];
                }
                if (v > -5 && u > -6) return v > -3 ? Bl[1] : Bl[2];
                foreach (double[] s in SPOTS)
                    if (Hypot(u - s[0], v - s[1]) < s[2])
                        return C[2];
                if (!InBody(u + 2, v + 2.4)) return C[2];
                return Lit(u, v, 0.5) ? C[5] : Lit(u, v, 0.8) ? C[4] : C[3];
            });
            // hind leg: a thigh folded at the rump, or kicked out straight when it leaps
            var legs = new List<double[][]>();
            if (Pz.leg < 0.5)
            {
                g.Ellipse(-7.5, -4.5 + (1 - sy) * 3, 5, 3.6, (nx, ny) => nx * -0.6 + ny * -0.8 > 0.35 ? C[4] : ny > 0.4 ? C[2] : C[3]);
                legs.Add(new[] { new[] { -4, -1.5 }, new double[] { -9, -1 }, new[] { -3, -0.5 }, new[] { 4, -0.5 } });
            }
            else
            {
                double ex = -8 - 10 * Pz.leg, ey = 4 + 6 * Pz.leg;
                legs.Add(new[] { new double[] { -6, -5 }, new double[] { -10, 0 }, new[] { ex, ey }, new[] { ex - 5, ey + 2 } });
            }
            foreach (double[][] L in legs)
            {
                // a tapering tube: a thick thigh, a thinner shin, a long foot; lit along its top
                g.Tube(L, u => Lerp(2, 0.7, u), (u, v) => v > 0.55 ? C[2] : u > 0.66 ? C[3] : C[4]);
                // webbed toes spread at the end of the foot
                double fx = L[L.Length - 1][0], fy = L[L.Length - 1][1], px = L[L.Length - 2][0], py = L[L.Length - 2][1], dx = fx - px, dy = fy - py, l = Hypot(dx, dy);
                if (l == 0 || double.IsNaN(l)) l = 1;
                foreach (int k in new[] { -1, 0, 1 }) g.Line(fx, fy, fx + (dx / l) * 2 - (dy / l) * k * 1.6, fy + (dy / l) * 2 + (dx / l) * k * 1.6, C[3]);
            }
            // glint in the eye, the mouth line
            if (Pz.shut == 0)
            {
                const double gx = 4.6, gy = -14.4;
                double X = gx * cs - (gy + 7) * sn * 1, Y = gx * sn + (gy + 7) * cs - 7;
                g.Set(X, Y, C[6]);
            }
            for (double u = 3; u <= 11; u++)
            {
                double v = -7.8 + (u - 3) * 0.05, X = (u * sx) * cs - ((v + 7) * sy) * sn, Y = (u * sx) * sn + ((v + 7) * sy) * cs - 7;
                g.Set(X, Y, C[1]);
            }
            // the front leg props it up, or tucks in when it leaps
            double[][] fl = Pz.air ? new[] { new double[] { 7, -4 }, new double[] { 9, -1 } } : new[] { new double[] { 7, -4 }, new[] { 8, -0.5 } };
            g.Line(fl[0][0], fl[0][1], fl[1][0], fl[1][1], C[3]);
            g.Set(fl[1][0] + 1, fl[1][1], C[3]);
            g.Set(fl[1][0] - 1, fl[1][1], C[3]);
            // the throat sac swells into a glossy bubble under the chin
            if (Pz.throat > 0.05)
            {
                double r = 0.8 + 2.8 * Pz.throat, cx = 7, cy = -5.2 + r * 0.25;
                g.Ellipse(cx, cy, r * 1.1, r, (nx, ny) => nx * -0.6 + ny * -0.8 > 0.55 ? Bl[2] : ny > 0.5 ? Bl[1] : Mix(Bl[2], C[5], 0.4), 0.9);
                g.Set(cx - r * 0.4, cy - r * 0.45, C[6]);
            }
            g.Outline(C[0]);
        }

        static readonly double[][] PUFFS = { new[] { T_UP, 7, 0.6 }, new[] { T_LAND, 18, 1 } };

        readonly Options o;
        readonly Grid GRID = new Grid(-22, -40, 44, 44);
        readonly Swarm SWARM = new Swarm();

        Frog(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7), Bl = Fit(o.Ramp("belly"), 3);
            FrogPose Pz = PoseAt(t);
            Grid g = GRID;
            DrawFrog(g, C, Bl, Pz, t);
            g.Draw(p, 1, 0, Round(Pz.by));
            // the tongue whips out to the fly and back
            double ts = (t - T_SNAP + 0.06) / 0.14;
            if (ts > 0 && ts < 1)
            {
                double[] f0 = FlyAt(T_SNAP - 0.001);
                double e = Px.Sin(Math.PI * ts), mx = 11, my = Pz.by - 8;
                double x1 = Lerp(mx, f0[0], e), y1 = Lerp(my, f0[1], e);
                p.Line(mx, my, x1, y1, TONGUE);
                p.Line(mx, my + 1, x1, y1 + 1, Mix(TONGUE, C[0], 0.4));
                p.Circle(x1, y1, 1.4, TONGUE);
            }
            // the fly, buzzing
            double[] f = FlyAt(t);
            if (f != null)
            {
                bool flap = Px.Sin(t * 90) > 0;
                p.Px(f[0], f[1], FLY);
                p.Px(f[0] + 1, f[1], FLY);
                p.Dot(f[0], f[1] - (flap ? 1 : 0), WINGS, 0.8);
                p.Dot(f[0] + 1, f[1] - (flap ? 0 : 1), WINGS, 0.8);
                p.Glow(f[0], f[1], 3, WINGS, 0.15);
            }
            // dust thrown out along the floor as it takes off and lands, and grit bouncing
            Swarm sw = SWARM.Begin(t);
            foreach (double[] pf in PUFFS)
            {
                double t0 = pf[0], n = pf[1], pow = pf[2];
                double age = Wrap(t - t0, D);
                for (int i = 0; i < n; i++)
                {
                    Rng r = NewRng(o.Seed, i, t0 * 100);
                    double L = r.Range(0.6, 1.1) * pow;
                    if (age > L) continue;
                    double s = r.Sign(), v = r.Range(24, 70) * pow, x = s * (7 + (v / 3.5) * (1 - Exp(-age * 3.5))), y = -1.5 - r.Range(1, 5) * age;
                    sw.Add(P.Smoke, x, y, 0, 0, age, L, DUST, r.Range(2.2, 4.2) * (0.65 + 0.35 * pow), PF.Big);
                }
                if (pow == 1)
                    for (int i = 0; i < 8; i++)
                    {
                        Rng r = NewRng(o.Seed, i, 909);
                        Flight b = Toss(r.Range(-8, 8), -1, r.Range(-40, 40), -r.Range(30, 70), age, 700, -0.5);
                        if (age < 0.7) p.Dot(b.x, b.y, i % 2 != 0 ? GRIT_DARK : GRIT_LIGHT, b.landed ? 1.4 - age * 2 : 1);
                    }
            }
            sw.Draw(p);
        }

        public override void DrawBack(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            FrogPose Pz = PoseAt(t);
            double hgt = Clamp(-Pz.by / HOP);
            Shadow(p, 0, -0.2, 13 * (1 - 0.35 * hgt), 1.6, C[0], 0.6 - 0.3 * hgt);
        }
    }
}
