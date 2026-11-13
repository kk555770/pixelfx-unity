// The plasma ball (a lit rim, filaments from a white-hot heart, motes riding round it, zaps that spark where
// they land) follows Sprite Fusion's open-source energy orb (https://www.spritefusion.com/pixel-art-vfx/energy),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class EnergyCore : Effect
    {
        public static readonly Module Module = new Module("energy-core", "pixelfx/fx/scifi/energy-core", 112, 100, 56, 88, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0516", "#3d0a33", "#701560", "#b0228f", "#f53cd1", "#ff86e6", "#ffd2f6", "#ffffff" },
                ["metal"] = new[] { "#0e0d14", "#1e1c27", "#312e3e", "#4b4759", "#6f6a82", "#9d98b0" },
                ["seed"] = 35,
            },
            (o, e) => new EnergyCore(o, e))
        {
            Title = "Energy core",
            Desc = "A plasma core beating in a steel cage: it swells and throws a shock ring on each beat, two tilted rings orbit it, and arcs jump to the bars and spark.",
            Tags = new[] { "scifi", "reactor", "core", "power", "plasma", "machine", "prop", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, CY = -36, R0 = 11, RC = 24, TOPY = -64, BOTY = -9, SLOT = 0.1;
        static readonly double NS = Round(D / SLOT);

        sealed class Beat
        {
            public double b, k;
            public bool strong;
        }

        // two heartbeats a loop, each a strong thump and a softer echo
        static readonly Beat[] BEATS =
        {
            new Beat { b = 0.05, k = 1, strong = true }, new Beat { b = 0.25, k = 0.5, strong = false },
            new Beat { b = 1.25, k = 1, strong = true }, new Beat { b = 1.45, k = 0.5, strong = false },
        };

        static double Env(double a) => a < 0.03 ? a / 0.03 : Exp(-(a - 0.03) / 0.12);

        static double PulseAt(double t)
        {
            double m = 0;
            foreach (Beat bt in BEATS) m = Math.Max(m, bt.k * Env(ScifiLib.Since(t, bt.b, D)));
            return m;
        }

        sealed class Bar
        {
            public double x, z;
        }

        // the cage's bars round the core: angle, x, depth (> 0 in front)
        static readonly Bar[] BARS = Enumerable.Range(0, 6).Select(i =>
        {
            double a = (i / 6.0) * TAU + Math.PI / 6;
            return new Bar { x = RC * Px.Sin(a), z = Px.Cos(a) };
        }).ToArray();

        // a point on an orbiting ring, tilted and turning: [x, y, depth]
        static (double x, double y, double z) RingPt(double r, double tilt, double spin, double a)
        {
            double x = r * Px.Cos(a), z0 = r * Px.Sin(a), y = -z0 * Px.Sin(tilt), z = z0 * Px.Cos(tilt);
            return (x * Px.Cos(spin) + z * Px.Sin(spin), CY - y, -x * Px.Sin(spin) + z * Px.Cos(spin));
        }

        sealed class RingSpec
        {
            public double r, tilt;
            public Func<double, double> spin;
        }

        static readonly RingSpec[] RINGS =
        {
            new RingSpec { r = 17, tilt = 1.15, spin = t => TAU * (t / D) },
            new RingSpec { r = 19, tilt = -0.9, spin = t => -TAU * 2 * (t / D) + 1 },
        };

        sealed class ArcSpec
        {
            public double s;
            public double[][] path;
            public double[][][] fk;
        }

        readonly uint[] C, H;
        readonly double seed;
        readonly List<ArcSpec> arcs = new List<ArcSpec>();
        readonly ScifiLib.Ghosts g;

        EnergyCore(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            H = Fit(o.Ramp("metal"), 6);
            seed = o.Seed;
            var rn = new Rng(o.Seed);
            g = new ScifiLib.Ghosts(D, x => 0, new[] { WHITE, C[6], C[5], C[4] }, C[5], new double[] { -54, -98, 54, 11 });
            // arcs: each slot of the loop may fire one or two, more just after a thump
            Bar[] front = BARS.Where(b => b.z > -0.2).ToArray();
            for (int s = 0; s < NS; s++)
            {
                double ts = s * SLOT;
                bool hot = BEATS.Any(bt => bt.strong && ScifiLib.Since(ts, bt.b, D) < 0.16);
                for (int n = 0; n < (hot ? 2 : 1); n++)
                {
                    if (!hot && !rn.Chance(0.4)) continue;
                    Bar bar = rn.Pick(front);
                    double ty = rn.Range(TOPY + 8, BOTY - 6), ang = Atan2(ty - CY, bar.x);
                    double sx = Px.Cos(ang) * R0, sy = CY + Px.Sin(ang) * R0;
                    double[][] path = Shapes.Bolt(rn, sx, sy, bar.x - Sign(bar.x) * 1.5, ty, 0.3, 4);
                    arcs.Add(new ArcSpec { s = s, path = path, fk = Shapes.Forks(rn, path, rn.Int(0, 1), 7, 0.35) });
                    for (int i = 0; i < 5; i++)
                    {
                        double a = Atan2(ty - sy, bar.x - sx) + Math.PI + rn.Range(-1.2, 1.2), sp = rn.Range(60, 170);
                        g.Add(ts, P.Spark, bar.x, ty, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, rn.Range(0.2, 0.5));
                    }
                }
            }
            // each thump throws motes off the core
            foreach (Beat bt in BEATS)
            {
                if (!bt.strong) continue;
                for (int i = 0; i < 12; i++)
                {
                    double a = rn.Range(0, TAU), sp = rn.Range(40, 110);
                    g.Add(bt.b + 0.02, P.Mote, Px.Cos(a) * R0, CY + Px.Sin(a) * R0, Px.Cos(a) * sp, Px.Sin(a) * sp, rn.Range(0.25, 0.5), rn.Pick(new[] { WHITE, C[6], C[5] }), 0.7);
                }
            }
        }

        // the plinth on the floor: a steel drum with a lit top and a row of status lights
        static void Plinth(Painter p, uint[] C, uint[] H, double t, double pulse)
        {
            p.Shade(-30, -10, 30, 2, (x, y, o) =>
            {
                bool top = (Px.Pow(x / 29, 2) + Px.Pow((y + 7) / 4, 2)) <= 1, side = Math.Abs(x) <= 29 && y > -7 && y <= 0;
                if (!top && !side) return 0;
                if (top) o.c = (Px.Pow(x / 29, 2) + Px.Pow((y + 7) / 4, 2)) > 0.82 ? H[0] : y < -8 ? H[5] : x < 0 ? H[4] : H[3];
                else o.c = Math.Abs(x) > 28 || y == 0 ? H[0] : y == -6 ? H[4] : x < -14 ? H[3] : x > 14 ? H[1] : H[2];
                return 0;
            });
            for (int i = 0; i < 7; i++)
            {
                double x = -18 + i * 6, k = 0.5 + 0.5 * Px.Cos(TAU * (2 * (t / D) - i / 7.0));
                p.Px(x, -3, k > 0.75 ? WHITE : k > 0.4 ? C[5] : C[2]);
                if (k > 0.6) p.Glow(x, -3, 4, C[4], 0.3 * k);
            }
            p.Glow(0, -8, 26, C[4], 0.25 + 0.3 * pulse);
        }

        // a cage bar: steel, lit on the face toward the core
        static void BarDraw(Painter p, Bar b, uint[] C, uint[] H, double pulse, bool back)
        {
            uint lit = back ? C[3] : pulse > 0.5 ? C[6] : C[5];
            int inward = -Sign(b.x);
            for (double y = TOPY + 2; y < BOTY; y++)
                for (int i = -1; i <= 1; i++)
                {
                    bool face = i == inward;
                    p.Px(b.x + i, y, back ? (face ? lit : H[1]) : face ? lit : i == 0 ? H[3] : H[1]);
                }
        }

        // the cage's rings top and bottom: the far half or the near half of a steel hoop
        static void Hoop(Painter p, double y, double rx, uint[] H, uint[] C, bool near, double pulse)
        {
            for (int i = 0, n = (int)Math.Ceiling(rx * 8); i < n; i++)
            {
                double g = ((double)i / n) * TAU, s = Px.Sin(g);
                if (s >= 0 != near) continue;
                double x = Px.Cos(g) * rx, yy = y + s * 4;
                p.Px(x, yy, near ? (s > 0.6 ? H[4] : H[3]) : H[1]);
                p.Px(x, yy + 1, near ? H[1] : H[0]);
                if (near && s < 0.3) p.Px(x, yy - 1, pulse > 0.5 ? C[5] : C[3]);
            }
        }

        // the core: dark plasma held in a hard rim, thin bright bands churning round a white-hot heart, filaments
        // flickering out from it; a thump floods it with light
        static void Core(Painter p, double t, uint[] C, double R, double pulse, double seed)
        {
            double ph = t / D;
            p.Shade(-R - 2, CY - R - 2, R + 2, CY + R + 2, (x, y, o) =>
            {
                double dx = x, dy = y - CY, d = Math.Sqrt(dx * dx + dy * dy) / R;
                if (d > 1.1) return 0;
                if (d > 1) return o.c = C[2];
                if (d > 0.9) return o.c = dy < -R * 0.3 && dx < R * 0.3 ? C[6] : C[4];
                double a = Atan2(dy, dx), n = 0.5 + 0.5 * TileFbm((a / TAU) * 6 + ph * 6 + d * 1.5, d * 2.2 - ph * 4, 6, 4, seed, 2);
                bool band = Math.Abs(n - 0.5) < 0.05 + 0.05 * pulse;
                double heat = Px.Pow(1 - d, 2) + pulse * 0.45;
                if (band) o.c = heat > 0.5 ? WHITE : heat > 0.2 ? C[6] : C[5];
                else o.c = heat > 0.7 ? C[6] : heat > 0.42 ? C[5] : heat > 0.2 ? C[3] : n > 0.5 ? C[2] : C[1];
                return 0;
            });
            // filaments, redrawn every other frame
            double f = Math.Floor(t * 15);
            Rng r = NewRng(seed, f, 5);
            for (int i = 0; i < 3; i++)
            {
                double a = (i / 3.0) * TAU + f * 0.7 + r.Range(-0.4, 0.4);
                double[][] pts = Shapes.Bolt(r, 0, CY, Px.Cos(a) * R * 0.88, CY + Px.Sin(a) * R * 0.88, 0.32, 3);
                p.Polyline(pts, u => u < 0.4 ? WHITE : C[6]);
                double ex = pts[pts.Length - 1][0], ey = pts[pts.Length - 1][1];
                p.Px(ex, ey, WHITE);
                p.Glow(ex, ey, 4, C[5], 0.4);
            }
            p.Circle(0, CY, 1.8 + pulse * 2, WHITE);
            p.Circle(0, CY, 3 + pulse * 2, (d, gg) => d > 0.6 ? C[6] : WHITE, 0.0001);
            // a glassy highlight top left
            p.Px(-R * 0.45, CY - R * 0.55, WHITE);
            p.Px(-R * 0.45 + 1, CY - R * 0.55, C[6]);
        }

        static void Ring(Painter p, double t, RingSpec rg, uint[] C, bool front, double pulse)
        {
            double spin = rg.spin(t), bead = (t / D) * TAU * 3;
            for (int i = 0, n = (int)Math.Ceiling(rg.r * 9); i < n; i++)
            {
                double a = ((double)i / n) * TAU;
                var (x, y, z) = RingPt(rg.r, rg.tilt, spin, a);
                if (z >= 0 != front) continue;
                double u = ((((a - bead) / TAU) % 1) + 1) % 1;
                bool head = u > 0.9;
                p.Px(x, y, head ? WHITE : !front ? C[3] : z > 0.5 * rg.r || pulse > 0.6 ? C[6] : C[5]);
            }
            if (front)
            {
                var (x, y, _) = RingPt(rg.r, rg.tilt, spin, bead);
                p.Glow(x, y, 6, C[5], 0.5);
            }
        }

        public override void DrawBack(Painter p, double t)
        {
            double pulse = PulseAt(t);
            Hoop(p, BOTY, RC, H, C, false, pulse);
            Hoop(p, TOPY, RC, H, C, false, pulse);
            foreach (Bar b in BARS)
                if (b.z < 0) BarDraw(p, b, C, H, pulse, true);
            foreach (RingSpec rg in RINGS) Ring(p, t, rg, C, false, pulse);
        }

        public override void Draw(Painter p, double t)
        {
            double pulse = PulseAt(t), R = R0 * (1 + 0.14 * pulse);
            Plinth(p, C, H, t, pulse);
            Core(p, t, C, R, pulse, seed);
            foreach (RingSpec rg in RINGS) Ring(p, t, rg, C, true, pulse);
            foreach (Bar b in BARS)
                if (b.z >= 0) BarDraw(p, b, C, H, pulse, false);
            Hoop(p, BOTY, RC, H, C, true, pulse);
            Hoop(p, TOPY, RC, H, C, true, pulse);
            // the cap: a steel dome with an emitter on top
            p.Shade(-15, TOPY - 7, 15, TOPY, (x, y, o) =>
            {
                double d = Px.Pow(x / 14.5, 2) + Px.Pow((y - TOPY) / 6.5, 2);
                if (d > 1) return 0;
                double l = -x * 0.05 - (y - TOPY) * 0.12;
                o.c = d > 0.8 ? H[0] : l > 0.55 ? H[5] : l > 0.3 ? H[4] : H[3];
                return 0;
            });
            p.Px(0, TOPY - 7, pulse > 0.4 ? WHITE : C[5]);
            p.Glow(0, TOPY - 7, 5, C[4], 0.4 + 0.4 * pulse);
            // the arcs firing now
            double s = Math.Floor(t / SLOT) % NS, k = 1 - (t - Math.Floor(t / SLOT) * SLOT) / SLOT;
            uint[] AC = { C[2], C[5], C[4] };
            foreach (ArcSpec a in arcs)
            {
                if (a.s != s) continue;
                ScifiLib.StrokeArc(p, a.path, AC, k);
                foreach (double[][] f in a.fk) ScifiLib.StrokeArc(p, f, AC, k, 0.6);
                double ex = a.path[a.path.Length - 1][0], ey = a.path[a.path.Length - 1][1];
                p.Glow(ex, ey, 8, C[5], 0.6 * k);
            }
            // each thump: a shock ring rolls out from the core
            foreach (Beat bt in BEATS)
            {
                double a = ScifiLib.Since(t, bt.b, D);
                if (!bt.strong || a > 0.45) continue;
                double q = a / 0.45, r = R0 + 34 * Ease.OutCubic(q);
                p.Ring(0, CY, r, Math.Max(1, 3 * (1 - q)), q < 0.25 ? WHITE : q < 0.5 ? C[6] : C[5], 1.1 - q);
                p.GlowRing(0, CY, r, 6, C[4], 0.35 * (1 - q));
            }
            g.At(t).Draw(p);
            // light: the core's heat, its white heart, the steel lit round it
            p.Glow(0, CY, 46, C[4], 0.2 + 0.3 * pulse);
            p.Glow(0, CY, 9, WHITE, 0.35 + 0.4 * pulse);
        }
    }
}
