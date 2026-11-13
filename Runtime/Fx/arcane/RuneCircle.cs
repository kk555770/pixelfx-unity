// The floor glow and the specks rising off it follow Sprite Fusion's open-source mana heal
// (https://www.spritefusion.com/pixel-art-vfx/heal), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class RuneCircle : Effect
    {
        // turning a quarter a loop keeps every part seamless: 16 ticks, a square, and 12 runes in a pattern of three
        const double D = 4, R = 46, RR = 39, RI = 23, RC = 7, SY = 0.3;
        const int NR = 12;

        public static readonly Module Module = new Module("rune-circle", "pixelfx/fx/arcane/rune-circle", 136, 100, 68, 80, D, true, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 8 },
            (o, e) => new RuneCircle(o))
        {
            Title = "Rune circle",
            Desc = "A rune circle turns on the floor in perspective: a carousel of runes lit by chasing light, a turning square, ticks, motes rising off it.",
            Tags = new[] { "arcane", "rune", "circle", "magic", "aura", "ground", "loop" },
            Bg = "ground",
        };

        static (double x, double y) At(double g, double r) => (Px.Cos(g) * r, Px.Sin(g) * r * SY);
        static double Gap(double a, double b) => Math.Abs(Atan2(Px.Sin(a - b), Px.Cos(a - b)));
        static readonly int[] RUNE_OF = { 0, 3, 5 };

        static void Circle(Painter p, uint[] C, double t, bool far)
        {
            double ph = t / D, spin = (ph * TAU) / 4;
            Func<double, bool> keep = far ? (Func<double, bool>)(g => Px.Sin(g) < 0) : g => Px.Sin(g) >= 0;
            // two sparks of light chase round the outer ring, twice a loop
            double chase = ph * TAU * 2;
            bool Lit(double g, double w) => Gap(g, chase) < w || Gap(g, chase + Math.PI) < w;
            uint Tone(double g, uint hi, uint lo) => Px.Sin(g) < 0 ? lo : hi;
            FloorRing(p, 0, 0, R, SY, g => Lit(g, 0.22) ? WHITE : Lit(g, 0.5) ? C[6] : Tone(g, C[6], C[5]), 1, keep);
            FloorRing(p, 0, 0, RI, SY, g => Tone(g, C[5], C[4]), 1, keep);
            FloorRing(p, 0, 0, RC, SY, g => Tone(g, C[6], C[5]), 1, keep);
            // studs set round the outside of the ring, turning with it
            for (int j = 0; j < 16; j++)
            {
                double g = spin + ((j + 0.5) * TAU) / 16;
                if (Px.Sin(g) < 0 != far) continue;
                var (x, y) = At(g, R + 3.5);
                bool hot = Lit(g, 0.3);
                p.Px(x, y, hot ? WHITE : Tone(g, j % 2 != 0 ? C[5] : C[6], C[4]));
                if (j % 2 == 0)
                    foreach (int dx in new[] { 1, -1 })
                        p.Px(x + dx, y, hot ? C[6] : Tone(g, C[4], C[3]));
            }
            // a square turning the other way inside the inner ring, its corners touching it
            var pts = new (double x, double y)[4];
            for (int k = 0; k < 4; k++) pts[k] = At(-spin + (k * TAU) / 4 + Math.PI / 4, RI);
            for (int k = 0; k < 4; k++)
            {
                var (ax, ay) = pts[k];
                var (bx, by) = pts[(k + 1) % 4];
                if ((ay < 0) != (by < 0))
                {
                    double u = ay / (ay - by), mx = ax + (bx - ax) * u;
                    if ((ay < 0) == far)
                    {
                        bx = mx;
                        by = 0;
                    }
                    else
                    {
                        ax = mx;
                        ay = 0;
                    }
                }
                else if ((ay < 0) != far) continue;
                p.Line(ax, ay, bx, by, far ? C[4] : C[5]);
            }
            // the runes stand in the band, riding round; bright at the front, dim behind, flaring as the light passes
            var gs = Enumerable.Range(0, NR).Select(k => (k, g: spin + ((k + 0.5) * TAU) / NR)).Where(e => Px.Sin(e.g) < 0 == far).OrderBy(e => Px.Sin(e.g)).ToArray();
            foreach (var (k, g) in gs)
            {
                var (x, y) = At(g, RR);
                bool hot = Lit(g, 0.2);
                double bob = Px.Sin(ph * TAU * 4 + 4 * g) * 0.5;
                Glyph(p, RUNE_OF[k % 3], x, y - 1 + bob, hot ? WHITE : far ? C[5] : C[6], 1, hot ? C[5] : far ? C[2] : C[3]);
                p.Glow(x, y - 3, hot ? 10 : 6, C[5], hot ? 0.5 : far ? 0.12 : 0.22);
                p.Glow(x, y, 5, C[4], 0.15);
            }
        }

        // motes rising off the circle on a slow helix round its axis, swaying in; twinkles hanging over it
        static void Motes(Painter p, uint[] C, double t, double S, bool far)
        {
            Cycle(t, D, 76, S * 5 + 1, g => g.Range(1.1, 1.9), (age, life, r, _) =>
            {
                double g0 = r.Range(0, TAU), r0 = r.Pick(new[] { R, R, RR, RR, RI }), q = age / life, g = g0 + q * r.Range(0.6, 1.4);
                if (Px.Sin(g) < 0 != far) return;
                double rr = r0 * (1 - 0.35 * q), h = 2 + Ease.OutQuad(q) * r.Range(26, 52), x = Px.Cos(g) * rr, y = Px.Sin(g) * rr * SY - h;
                double a = Math.Min(1, q * 5) * (1 - Smooth(0.65, 1, q));
                bool big = r.Chance(0.2);
                Mote(p, x, y, -Px.Sin(g) * rr * 0.8, -36 * (1 - q) - 10, r.Chance(0.3) ? WHITE : r.Chance(0.6) ? C[6] : C[5], big ? 1.1 : r.Range(0.55, 0.9), a, big ? 0.5 : 0.3);
            });
            Cycle(t, D, 14, S * 5 + 2, 0.45, (age, life, r, _) =>
            {
                double g = r.Range(0, TAU);
                if (Px.Sin(g) < 0 != far) return;
                double rr = r.Range(8, R), x = Px.Cos(g) * rr, y = Px.Sin(g) * rr * SY - r.Range(6, 34), k = Px.Sin((age / life) * Math.PI);
                Sim.Sparkle(p, x, y, 1 + 2.4 * k, C[6], C[5], 0.35 * k);
            });
        }

        readonly Options o;

        RuneCircle(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double pulse = 0.85 + 0.15 * Px.Sin((t / D) * TAU * 4);
            // the floor glows under the circle, and a soft light stands over it
            p.Glow(0, 0, R * 1.4, C[4], 0.24 * pulse);
            foreach (int s in new[] { -1, 1 }) p.Glow(s * R * 0.55, 1, R * 0.7, C[5], 0.12 * pulse);
            // a soft column of light standing over it, fading as it rises
            for (int h = 0; h < 66; h += 6) p.Glow(0, -h, 26, C[4], 0.11 * pulse * (1 - h / 80.0));
            Circle(p, C, t, true);
            Motes(p, C, t, o.Seed, true);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double chase = (t / D) * TAU * 2;
            Circle(p, C, t, false);
            Motes(p, C, t, o.Seed, false);
            // the two sparks of light running round the ring
            foreach (double g in new[] { chase, chase + Math.PI })
            {
                var (x, y) = At(g, R);
                Sim.Sparkle(p, x, y, 2.5 + Px.Sin(t * 30) * 0.8, C[6], C[5], 0.5);
                p.Glow(x, y, 14, C[5], 0.4);
            }
        }
    }
}
