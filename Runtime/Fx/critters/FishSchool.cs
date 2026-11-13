// The caustic light and the bubbles are adapted from Sprite Fusion's open-source bubbles
// (https://www.spritefusion.com/pixel-art-vfx/bubbles), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class FishSchool : Effect
    {
        public static readonly Module Module = new Module("fish-school", "pixelfx/fx/critters/fish-school", 136, 80, 68, 40, 4.8, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0a1a2a", "#123552", "#1d5a7e", "#2f8aa8", "#62bccc", "#bfe8e6", "#f4fff8" },
                ["stripe"] = new[] { "#5a1e14", "#d8582a", "#ffa04a" },
                ["seed"] = 1,
            },
            (o, e) => new FishSchool(o))
        {
            Title = "Fish school",
            Desc = "A school of fish holds station in two depth rows: bodies wag into swinging tails, one darts ahead now and then, bubbles rise and caustic light ripples over them.",
            Tags = new[] { "critter", "fish", "school", "sea", "underwater", "swim", "loop" },
            Bg = "dark",
        };

        const double D = 4.8;

        sealed class Fish
        {
            public double x, y, size, phase;
            public bool back;
        }

        // the school: [x, y, size, back row?, phase]
        static readonly Fish[] SCHOOL =
        {
            new Fish { x = -34, y = -16, size = 0.72, back = true, phase = 0.1 }, new Fish { x = 2, y = -20, size = 0.68, back = true, phase = 0.55 },
            new Fish { x = 30, y = -10, size = 0.7, back = true, phase = 0.3 },
            new Fish { x = -22, y = 4, size = 1, back = false, phase = 0.8 }, new Fish { x = 12, y = -4, size = 1, back = false, phase = 0.2 },
            new Fish { x = -40, y = -2, size = 0.95, back = false, phase = 0.45 }, new Fish { x = 26, y = 14, size = 0.96, back = false, phase = 0.65 },
        };

        // one fish, its nose at (0, 0), facing right, its body trailing back to the left
        static void DrawFish(Grid g, uint[] C, uint[] Sp, double s, double t, double ph, double dart)
        {
            g.Clear();
            double L = 15 * s, H = 8 * s, beats = 12 + (dart > 0 ? 6 : 0), w = (t / D) * TAU * beats + ph * TAU;
            double amp = (1.3 + 1.2 * dart) * s;
            double Bend(double u) => amp * Px.Pow(u, 1.8) * Px.Sin(w - 4 * u);
            double Prof(double u) => u < 0.3 ? Math.Sqrt(u / 0.3) : 1 - Px.Pow((u - 0.3) / 0.7, 1.5) * 0.72;
            double Hh(double u) => (H / 2) * Prof(u);
            // body: dark back, lighter flank, silver belly; a stripe down the side; light rippling on its back
            g.Shade(-L, -H, 0, H, (x, y, o) =>
            {
                double u = -x / L, dy = y - Bend(u), h = Hh(u);
                if (u < 0 || u > 1 || Math.Abs(dy) > h) return 0;
                double v = dy / h;
                if (v > -0.12 && v < 0.12 && u > 0.22 && u < 0.92) return u < 0.3 ? Sp[1] : Sp[v < 0 ? 2 : 1];
                if (v < -0.6) return u < 0.5 && v < -0.8 ? C[3] : C[2];
                if (v < -0.12) return C[3];
                if (v < 0.5) return C[4];
                return v > 0.86 ? C[4] : C[5];
            });
            // the tail: a forked fin swinging off the end of the wave, lit along its upper lobe
            double tb = Bend(1), ta = Atan(amp * 1.8 * 0.9 * Px.Cos(w - 4)) * 0.8, TL = 6 * s, hb = Hh(1);
            double[] Rot(double dx, double dy) => new[] { -(L + dx * Px.Cos(ta) - dy * Px.Sin(ta)), tb + dx * Px.Sin(ta) + dy * Px.Cos(ta) };
            g.Poly(new[] { Rot(-0.5, -hb), Rot(TL, -hb - 3.4 * s), Rot(TL * 0.62, 0), Rot(TL, hb + 3.4 * s), Rot(-0.5, hb) }, (x, y) => y < tb - 1 ? C[4] : C[3], 0.88);
            // the dorsal fin: swept back, ribbed; a pectoral fin paddling, the gill, the eye
            const double d0 = 0.34, d1 = 0.66;
            g.Poly(new[] { new[] { -d0 * L, Bend(d0) - Hh(d0) + 0.5 }, new[] { -(d0 + 0.08) * L, Bend(d0) - Hh(d0) - 3.6 * s }, new[] { -d1 * L, Bend(d1) - Hh(d1) + 0.5 } },
                (x, y) => Round(x) % 2 != 0 ? C[3] : C[4], 0.85);
            double pf = Px.Sin(w * 0.5) * 1.2;
            g.Set(-0.34 * L, Bend(0.34) + 1.5 * s + pf * 0.3, C[4], 0.85);
            g.Set(-0.34 * L - 1, Bend(0.34) + 2 * s + pf * 0.6, C[4], 0.85);
            for (int y = -2; y <= 1; y++) g.Set(-0.26 * L, Bend(0.26) + y * s, C[2]);
            double ex = -0.13 * L - 1, ey = Bend(0.13) - 0.25 * H;
            g.Set(ex, ey, C[0]);
            g.Set(ex + 1, ey, C[0]);
            g.Set(ex, ey + 1, C[1]);
            g.Set(ex + 1, ey + 1, C[0]);
            g.Set(ex + 1, ey, C[6]);
            g.Outline(C[0]);
        }

        // where a fish is: holding station in the current, swaying; one darts ahead and drifts back once a loop
        static (double x, double y, double dart) Place(Fish f, int i, double t)
        {
            double a = (t / D) * TAU;
            double dt = Wrap(t - (i * 0.71 * D) % D, D);
            double dart = i == 4 ? (dt < 0.25 ? Ease.OutCubic(dt / 0.25) : dt < 1.6 ? 1 - Ease.InOutSine((dt - 0.25) / 1.35) : 0) : 0;
            return (f.x + 3 * Px.Sin(a + f.phase * TAU) + 8 * dart, f.y + 1.8 * Px.Sin(2 * a + f.phase * 5), i == 4 && dt < 0.6 ? 1 - dt / 0.6 : 0);
        }

        // caustics: the light that comes down through rippling water, as bright crossing lines
        static void Caustics(Painter p, uint[] C, double t)
        {
            double a = (t / D) * TAU;
            for (double y = -38; y < 38; y++)
            for (double x = -66; x < 66; x++)
            {
                double fall = Clamp(1 - ((x / 64) * (x / 64) + (y / 40) * (y / 40))) * (0.6 + 0.4 * Clamp(-y / 40 + 0.6));
                if (fall <= 0) continue;
                double u = x * 0.16 + 1.9 * Px.Sin(y * 0.12 + a), v = y * 0.19 + 1.9 * Px.Sin(x * 0.1 - a);
                double n1 = Px.Sin(u) + Px.Sin(v), sh = 0.75 + 0.25 * Px.Sin(x * 0.05 - y * 0.04 + 2 * a);
                double f = Math.Max(0, 1 - Math.Abs(n1) * 5) * fall * sh;
                if (f > 0.55) p.Light(x, y, C[4], 0.3);
                else if (f > 0.25) p.Light(x, y, C[3], 0.14);
            }
        }

        static readonly double[][] SHAFTS = { new double[] { -30, 10, 0 }, new[] { 14, 7, 2.1 }, new double[] { 44, 12, 4 } };

        readonly Options o;
        readonly Grid GRID = new Grid(-28, -12, 34, 24);

        FishSchool(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7), Sp = Fit(o.Ramp("stripe"), 3);
            Grid g = GRID;
            // shafts of light slanting down through the water
            foreach (double[] sh in SHAFTS)
            {
                double x0 = sh[0], w = sh[1], ph = sh[2];
                double sw = Px.Sin((t / D) * TAU + ph) * 4;
                p.GlowLine(x0 + sw - 12, -40, x0 + sw + 8, 40, w, C[4], 0.05);
            }
            // the back row first, dimmed into the water; then the front row
            foreach (bool back in new[] { true, false })
                for (int i = 0; i < SCHOOL.Length; i++)
                {
                    Fish f = SCHOOL[i];
                    if (f.back != back) continue;
                    var P = Place(f, i, t);
                    DrawFish(g, C, Sp, f.size, t, f.phase, P.dart);
                    if (back) g.Recolor((x, y, c, al, tg) => Mix(c, C[1], 0.42));
                    g.Draw(p, 1, P.x, P.y);
                }
            // bubbles let go from their mouths now and then
            Cycle(t, D, 9, o.Seed * 13 + 1, r => r.Range(1.6, 2.6), (age, L, r, i) =>
            {
                Fish f = SCHOOL[i % SCHOOL.Length];
                var P = Place(f, i % SCHOOL.Length, Wrap(t - age, D));
                double rad = r.Range(0.6, 1.6) * f.size;
                double ph = r.Next() * TAU, x = P.x + 1 + Px.Sin(age * (4 + 2 / rad) + ph) * (0.5 + rad * 0.7), y = P.y - 1 - (16 + 12 * rad) * age;
                if (y < -38) return;
                Bubble(p, x, y, rad, new[] { C[6], C[5], C[4], C[3] }, Min(1, age * 4, (L - age) * 3, (y + 38) / 4));
            });
            Caustics(p, C, t);
            p.Glow(0, -8, 74, C[2], 0.2);
            p.Glow(-6, -18, 40, C[3], 0.1);
        }
    }
}
