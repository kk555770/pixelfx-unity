using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Butterfly : Effect
    {
        public static readonly Module Module = new Module("butterfly", "pixelfx/fx/critters/butterfly", 128, 88, 64, 44, 3.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0b0f24", "#16224f", "#1f3f8a", "#2f74c8", "#4fb4f0", "#a0e6ff", "#ffffff" },
                ["spots"] = new[] { "#3a1a08", "#c8641a", "#ffb43c", "#fff0b0" },
                ["seed"] = 1,
            },
            (o, e) => new Butterfly(o))
        {
            Title = "Butterfly",
            Desc = "An iridescent butterfly wanders a loop in bursts of quick wingbeats and open-winged glides, its veined, spotted wings shedding a twinkling trail of glowing dust.",
            Tags = new[] { "critter", "butterfly", "insect", "flying", "magic", "glow", "loop" },
            Bg = "sky",
        };

        const double D = 3.2;

        static double[][] Pts(params double[] v)
        {
            var o = new double[v.Length / 2][];
            for (int i = 0; i < o.Length; i++) o[i] = new[] { v[i * 2], v[i * 2 + 1] };
            return o;
        }

        // the right wings, unfolded, in px from the wing root (y forward is negative)
        static readonly double[][] FORE = Pts(0, -1, 3, -6, 8, -11, 13, -12.5, 16.5, -10.5, 16.5, -6, 13, -1.5, 7, 1, 2, 1.5);
        static readonly double[][] HIND = Pts(0, 1, 6, 1.5, 11, 4, 12.5, 8.5, 10.5, 12.5, 6.5, 13.5, 3, 11, 0.5, 6);

        // flight: bursts of wingbeats (rising) and glides (sinking), wandering round a loop
        static readonly double[][] BURSTS = { new[] { 0, 0.95 }, new[] { 1.5, 2.55 } };

        static bool Flapping(double t)
        {
            foreach (double[] b in BURSTS)
                if (t >= b[0] && t < b[1])
                    return true;
            return false;
        }

        // the wing's fold, 1 flat open .. 0 edge-on: quick beats while flapping, held open while gliding
        static double Fold(double t)
        {
            t = Wrap(t, D);
            foreach (double[] ab in BURSTS)
            {
                double a = ab[0], b = ab[1];
                if (t >= a && t < b)
                {
                    double n = Round((b - a) * 6.3), ph = ((t - a) / (b - a)) * n;
                    return 0.14 + 0.86 * (0.5 + 0.5 * Px.Cos(ph * TAU));
                }
            }
            return 1;
        }

        static (double x, double y, double vx) At(double t)
        {
            t = Wrap(t, D);
            double a = (t / D) * TAU;
            // a lift while it beats, a sink while it glides, so the path bobs in steps
            double lift = 0;
            foreach (double[] se in BURSTS) lift += -3 * Px.Sin(Math.PI * Clamp((t - se[0]) / (se[1] - se[0])));
            return (30 * Px.Sin(a) + 6 * Px.Sin(3 * a), 12 * Px.Sin(2 * a + 0.4) + lift, Px.Cos(a));
        }

        static bool InPoly(double[][] pts, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                if (yi > y != yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        static double EdgeDist(double[][] pts, double x, double y)
        {
            double m = double.PositiveInfinity;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                double ax = pts[j][0], ay = pts[j][1], bx = pts[i][0], by = pts[i][1], dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
                if (L2 == 0 || double.IsNaN(L2)) L2 = 1;
                double u = Clamp(((x - ax) * dx + (y - ay) * dy) / L2), px = ax + dx * u - x, py = ay + dy * u - y;
                m = Math.Min(m, Hypot(px, py));
            }
            return m;
        }

        // a wing's pattern at an unfolded point: dark at the root, iridescent blue brightest along the leading edge,
        // thin dark veins, a black apex dotted white on the forewing, a gold eyespot on the hindwing
        static readonly double[][] FSPOTS = Pts(13.2, -10.2, 15.2, -8.4, 14.6, -5.6, 11.6, -11.4);
        static readonly double[] HIND_VEINS = { 0.5, 1.0, 1.45 }, FORE_VEINS = { -1.2, -0.75, -0.3 };

        static uint WingColor(uint[] C, uint[] Sp, bool hind, double u, double v)
        {
            double[][] pts = hind ? HIND : FORE;
            double e = EdgeDist(pts, u, v), r = Hypot(u, v), ang = Atan2(v, u);
            if (e < 1) return C[0];
            if (!hind)
            {
                if (u + v * -0.45 > 14.2 || (r > 12.5 && v < -4))
                {
                    foreach (double[] sp in FSPOTS)
                        if (Hypot(u - sp[0], v - sp[1]) < 0.9)
                            return C[6];
                    return C[0];
                }
            }
            else
            {
                if (e < 2 && r > 8) return Px.Sin(ang * 10) > 0.5 ? C[5] : C[1];
                double d = Hypot(u - 7.6, v - 9.4);
                if (d < 0.7) return C[6];
                if (d < 1.4) return C[0];
                if (d < 2.2) return Sp[2];
            }
            foreach (double va in hind ? HIND_VEINS : FORE_VEINS)
                if (Math.Abs(ang - va) * r < 0.45 && r > 3.5)
                    return C[1];
            if (r < 3) return C[2];
            if (r < 6) return C[3];
            if (!hind && e < 2.4 && v < -3) return C[5];
            return C[4];
        }

        static void DrawButterfly(Grid g, uint[] C, uint[] Sp, double w, double t)
        {
            g.Clear();
            foreach (int s in new[] { -1, 1 })
            foreach (bool hind in new[] { true, false })
            {
                double[][] pts = hind ? HIND : FORE;
                g.Shade(-20, -15, 20, 16, (x, y, o) =>
                {
                    if (Sign(x + 0.001) != s) return 0;
                    double u = (Math.Abs(x) - 1) / w, v = y;
                    if (u < 0 || !InPoly(pts, u, v)) return 0;
                    uint c = WingColor(C, Sp, hind, u, v);
                    // folding up, the far side of each wing turns away from the light
                    if (w < 0.5 && c != C[0]) c = Mix(c, C[1], (0.5 - w) * 1.6);
                    return c;
                });
            }
            // the body: a fuzzy thorax, a tapering abdomen, a round head, curled antennae with bright tips
            g.Ellipse(0, 0.5, 1.6, 2.6, (nx, ny) => nx + ny < -0.4 ? C[3] : C[1]);
            for (int y = 3; y < 10; y++)
            {
                g.Set(-1, y, y < 6 ? C[2] : C[1]);
                if (y < 8) g.Set(0, y, C[1]);
            }
            g.Set(-1, -3, C[3]);
            g.Set(0, -3, C[2]);
            g.Set(-1, -4, C[2]);
            g.Set(0, -4, C[1]);
            foreach (int s in new[] { -1, 1 })
            {
                double tx = s * 4 - (s > 0 ? 0 : 1), ty = -9.5;
                g.Line(s > 0 ? 0 : -1, -5, tx, ty, C[2]);
                g.Set(tx, ty, C[6]);
            }
            g.Outline((x, y, nx, ny) => C[0]);
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-22, -20, 44, 40);

        Butterfly(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7), Sp = Fit(o.Ramp("spots"), 4);
            var S = At(t);
            double w = Fold(t);
            // a trail of glowing dust shaken off the wings, drifting down and twinkling out
            Cycle(t, D, 56, o.Seed * 7 + 1, r => r.Range(1, 1.8), (age, L, r, i) =>
            {
                double born = Wrap(t - age, D);
                var B = At(born);
                double k = age / L;
                if (!Flapping(born) && r.Chance(0.45)) return;
                double x = B.x + r.Range(-9, 9) + Px.Sin(age * 3 + i) * 1.5, y = B.y + r.Range(-4, 8) + 7 * age + 3 * age * age;
                double tw = 0.5 + 0.5 * Px.Sin(age * 11 + i * 2.1), a = Math.Min(1, k * 8) * (1 - k);
                p.Dot(x, y, tw > 0.7 ? C[6] : r.Chance(0.3) ? Sp[2] : C[5], a * 1.2);
                p.Glow(x, y, 6, C[4], 0.5 * a * tw);
                if (i % 9 == 0 && tw > 0.85 && k < 0.5) Sim.Sparkle(p, x, y, 2, C[6], C[5], 0.3 * a);
            });
            Grid g = GRID;
            DrawButterfly(g, C, Sp, w, t);
            g.Draw(p, 1, S.x, S.y);
            // the wings shine: a soft blue light round it, brighter when they open
            p.Glow(S.x, S.y, 38, C[4], 0.16 + 0.14 * w);
            p.Glow(S.x, S.y, 12, C[5], 0.12 + 0.1 * w);
        }
    }
}
