using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Snake : Effect
    {
        public static readonly Module Module = new Module("snake", "pixelfx/fx/critters/snake", 136, 56, 68, 48, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#081410", "#123a1e", "#1d5e2a", "#2f8a36", "#5cb848", "#a8e070", "#e8ffc0" },
                ["belly"] = new[] { "#4a3a14", "#b08a34", "#f0d878" },
                ["seed"] = 1,
            },
            (o, e) => new Snake(o))
        {
            Title = "Snake",
            Desc = "A snake slithers along the floor, head raised: humps rise and roll back down its patterned body to the tail, dust puffs where it touches down, its forked tongue flicks.",
            Tags = new[] { "critter", "snake", "reptile", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, HEAD = 30, LEN = 70;
        static readonly uint TONGUE = Hex("#e8304a"), DUST = Hex("#8a7f92");
        const double K = TAU / 30, W = (TAU * 2) / D;

        // the body's centre height u px behind the head: humps rolling back toward the tail, the neck raised to the head
        static double Lift(double u, double t)
        {
            double wave = Px.Pow(Math.Max(0, Px.Sin(K * u - W * t)), 0.85), env = Smooth(8, 18, u) * (1 - Smooth(LEN - 14, LEN - 2, u));
            double neck = u < 12 ? 6 * ((1 - u / 12) * (1 - u / 12)) : 0;
            return 7 * wave * env + neck;
        }

        static double Radius(double u) => u < 6 ? 3.3 : Lerp(3.4, 0.6, Px.Pow((u - 6) / (LEN - 6), 1.3));

        // the head's bob with the rhythm, and the tongue's flicks: two bursts of three a loop
        static double Bob(double t) => 0.8 * Px.Sin(W * t);

        static double TongueAt(double t)
        {
            foreach (double t0 in new[] { 0.3, 1.5 })
            {
                double k = (t - t0) / 0.5;
                if (k >= 0 && k < 1) return Px.Pow(Math.Max(0, Px.Sin(k * Math.PI * 3)), 0.6);
            }
            return 0;
        }

        static void DrawSnake(Grid g, uint[] C, uint[] B, double t)
        {
            g.Clear();
            double hb = Bob(t);
            // the body, column by column from the tail to the neck: lit along its back, a gold belly, dark diamonds, glints on the humps
            for (double u = LEN; u >= 2; u -= 0.5)
            {
                double x = HEAD - u, r0 = Radius(u), cy = -r0 - Lift(u, t) + (u < 12 ? hb * (1 - u / 12) : 0), hump = Lift(u, t);
                // keep the body as thick across its slope as along the floor
                double slope = Lift(u + 0.5, t) - Lift(u - 0.5, t), r = r0 * Math.Sqrt(1 + slope * slope);
                for (double y = Math.Floor(cy - r); y <= Math.Ceiling(cy + r); y++)
                {
                    double v = (y + 0.5 - cy) / r;
                    if (Math.Abs(v) > 1) continue;
                    uint c = v < -0.5 ? C[4] : v < 0.1 ? C[3] : v < 0.45 ? C[2] : v < 0.8 ? B[1] : B[0];
                    double dm = Math.Abs(((u + 2) % 7) - 3.5) / 3.5 + Math.Abs(v + 0.25) / 0.55;
                    if (dm < 0.85 && v < 0.35 && u > 4) c = dm < 0.5 ? C[1] : C[2];
                    if (v < -0.62 && hump > 4.5 && dm >= 0.85) c = hump > 6 ? C[6] : C[5];
                    if (v > 0.45 && v < 0.8 && Math.Floor(u) % 3 == 0) c = B[2];
                    g.Set(x, y, c);
                }
            }
            // the head: a blunt wedge raised off the floor, lit on top, an eye with a slit pupil, a nostril, a mouth line
            double hx = HEAD + 2, hy = -9.5 + hb;
            g.Shade(hx - 5, hy - 4, hx + 6, hy + 4, (x, y, o) =>
            {
                double dx = (x - hx) / (x > hx ? 5.6 : 4.6), dy = (y - hy) / (3.6 - Math.Max(0, x - hx) * 0.18);
                if (dx * dx + dy * dy > 1) return 0;
                if (dy > 0.55) return B[1];
                return dy < -0.3 ? (dx < 0.3 ? C[5] : C[4]) : C[3];
            });
            g.Line(hx - 1, hy + 1.5, hx + 5, hy + 1.2, C[1]);
            g.Set(hx + 1, hy - 2, C[0]);
            g.Set(hx + 2, hy - 2, B[2]);
            g.Set(hx + 1, hy - 1, B[2]);
            g.Set(hx + 2, hy - 1, C[0]);
            g.Set(hx + 1, hy - 3, C[6]);
            g.Set(hx + 5, hy - 1, C[1]);
            g.Outline(C[0]);
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-44, -24, 88, 26);
        readonly Swarm SWARM = new Swarm();

        Snake(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7), B = Fit(o.Ramp("belly"), 3);
            double hb = Bob(t);
            // the tongue darts out, forked, and flicks back
            double e = TongueAt(t), tx = HEAD + 7.5, ty = -8.2 + hb;
            if (e > 0.05)
            {
                double L = 7 * e, wig = Px.Sin(t * 60) * 0.6 * e;
                p.Line(tx, ty, tx + L, ty + wig, TONGUE);
                if (L > 3)
                {
                    p.Px(tx + L + 1, ty + wig - 1, TONGUE);
                    p.Px(tx + L + 1, ty + wig + 1, TONGUE);
                }
            }
            Grid g = GRID;
            DrawSnake(g, C, B, t);
            g.Draw(p);
            // dust scuffed along the floor where each hump presses back down, drifting off low and thin
            Swarm sw = SWARM.Begin(t);
            Cycle(t, D, 26, o.Seed * 7 + 1, r => r.Range(0.6, 1.1), (age, L, r, i) =>
            {
                double born = Wrap(t - age, D);
                int m = r.Int(0, 2);
                double u = (Math.PI + W * born + m * TAU) / K;
                if (u < 18 || u > LEN - 10) return;
                double x = HEAD - u + r.Range(-2, 2) - r.Range(6, 16) * age;
                sw.Add(P.Smoke, x, -0.5 - age * r.Range(3, 8), 0, 0, age, L, DUST, r.Range(1.8, 3.4));
            });
            sw.Draw(p);
            // a glint in its eye
            p.Glow(HEAD + 3.5, -11 + hb, 4, B[2], 0.35);
        }

        public override void DrawBack(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            // a shadow along its length, softer under the humps
            for (double u = 2; u <= LEN; u += 2)
            {
                double h = Lift(u, t), r = Radius(u);
                Shadow(p, HEAD - u, -0.2, 2.2 + r * 0.4, 1.2, C[0], 0.5 * (1 - Clamp(h / 10)) + 0.1);
            }
            Shadow(p, HEAD + 2, -0.2, 5, 1.2, C[0], 0.35);
        }
    }
}
