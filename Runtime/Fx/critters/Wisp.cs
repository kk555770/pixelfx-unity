// The flame tail, puffs and embers are adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class Wisp : Effect
    {
        public static readonly Module Module = new Module("wisp", "pixelfx/fx/critters/wisp", 128, 104, 64, 96, 3.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0a1f2e", "#0f3d52", "#14707e", "#1fa8a8", "#4fe0c8", "#a8fff0", "#ffffff" },
                ["hover"] = 36,
                ["seed"] = 1,
            },
            (o, e) => new Wisp(o))
        {
            Title = "Wisp",
            Desc = "A will-o'-the-wisp loops a figure-eight over the floor: a little soul-flame with a face, its flame tail licking up and streaming behind, shedding puffs and sparks.",
            Tags = new[] { "critter", "wisp", "spirit", "flame", "magic", "flying", "loop" },
            Bg = "ground",
        };

        const double D = 3.2;

        static (double x, double y, double vx, double vy) At(double t, double hover)
        {
            double a = (t / D) * TAU;
            return (34 * Px.Sin(a), -hover - 10 * Px.Sin(2 * a), ((34 * TAU) / D) * Px.Cos(a), ((-20 * TAU) / D) * Px.Cos(2 * a));
        }

        static readonly double[][] TAILS = { new[] { -3.5, 13, 2.5, 3 }, new[] { 3.5, 14, 2.5, 7 }, new[] { 0, 20, 3.2, 11 } };

        readonly Options o;
        readonly Swarm SWARM = new Swarm();

        Wisp(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double hover = o.Num("hover");
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            var S = At(t, hover);
            uint[] heat = { C[6], C[6], C[5], C[5], C[4], C[4], C[3], C[3], C[3] };
            double lean = Clamp(-S.vx / 80, -0.9, 0.9), stretch = Clamp(S.vy / 120, -0.3, 0.4), X = S.x, Y = S.y;
            // flame puffs shed along its path, cooling down the ramp as they drift up
            Cycle(t, D, 30, o.Seed * 7 + 1, r => r.Range(0.3, 0.6), (age, L, r, i) =>
            {
                var S0 = At(Wrap(t - age, D), hover);
                var (x, y) = Fly(S0.x + r.Range(-3, 3), S0.y - r.Range(0, 4), r.Range(-8, 8), -r.Range(6, 16), age, -40, 2.5);
                FirePuff(p, x, y, 1 - age / L, r.Range(1.2, 2.6), r.Next() * 100, heat);
            });
            // the tail: three tongues off the top of its head, streaming back as it flies
            for (int li = 0; li < 4; li++)
                foreach (double[] tl in TAILS)
                {
                    double dx = tl[0], h = tl[1], w = tl[2], n = tl[3];
                    double hh = (h + 0.25 * h * Lnoise(t, D, 9, n, 5)) * (1 + stretch);
                    Tongue(p, X + dx, Y - 2, hh, w, Lnoise(t, D, 6, n, 40) * 1.5 + lean * hh * 0.9, t, D, n, heat, li);
                }
            // the head: a ball of cold fire, white at the heart, its rim flickering
            const double R = 7.5;
            p.Shade(X - R - 2, Y - R - 2, X + R + 2, Y + R + 2, (x, y, oo) =>
            {
                double dx = x - X, dy = y - Y, ang = Atan2(dy, dx), rr = R + 0.7 * Lnoise(t, D, 16, ang * 1.6 + 10, 3) + (dy > 0 ? -0.6 : 0);
                double d = Hypot(dx, dy) / rr;
                if (d > 1) return 0;
                return d < 0.42 ? C[6] : d < 0.68 ? C[5] : d < 0.88 ? C[4] : C[3];
            });
            // its face: eyes looking where it's going, a blink, a little mouth
            double look = Round(Clamp(S.vx / 60, -1, 1));
            bool blink = Wrap(t, D) > 2.2 && Wrap(t, D) < 2.28;
            foreach (int s in new[] { -1, 1 })
            {
                double ex = Round(X) + s * 2 - (s > 0 ? 0 : 1) + look, ey = Round(Y) - 1;
                if (blink)
                {
                    p.Px(ex, ey + 1, C[1]);
                    p.Px(ex + 1, ey + 1, C[1]);
                }
                else
                {
                    for (int k = 0; k < 3; k++)
                    {
                        p.Px(ex, ey + k, C[1]);
                        p.Px(ex + 1, ey + k, C[1]);
                    }
                    p.Px(ex + (look > 0 ? 1 : 0), ey, C[6]);
                }
            }
            p.Px(Round(X) + look, Round(Y) + 3, C[2]);
            p.Px(Round(X) - 1 + look, Round(Y) + 3, C[2]);
            // sparks wander up off it
            Swarm sw = SWARM.Begin(t);
            sw.Sparks = new[] { C[6], C[5], C[4], C[3] };
            Cycle(t, D, 26, o.Seed * 7 + 2, r => r.Range(0.8, 1.6), (age, L, r, i) =>
            {
                var S0 = At(Wrap(t - age, D), hover);
                double s = r.Next() * 100;
                var (x, y) = Fly(S0.x + r.Range(-5, 5), S0.y - r.Range(4, 12), r.Range(-20, 20), -r.Range(20, 45), age, -15, 1.6);
                sw.Add(P.Ember, x + Px.Sin(age * 2.3 + s) * 3 * Math.Min(1, age), y, 0, 0, age, L, C[4], r.Chance(0.3) ? 2 : 1, PF.None, s);
            });
            sw.Draw(p);
            // light: a cold glow round it, a hot heart
            double fl = 0.85 + 0.15 * Lnoise(t, D, 22, 0, 9);
            p.Glow(X + lean * 3, Y - 6, 46 * fl, C[4], 0.38);
            p.Glow(X, Y - 1, 13, C[5], 0.35 * fl);
        }

        public override void DrawBack(Painter p, double time)
        {
            double hover = o.Num("hover");
            double t = Shift(time, D, o.Seed);
            uint[] C = Fit(o.Ramp("palette"), 7);
            var S = At(t, hover);
            double near = Clamp(1 - (-S.y - 24) / 30);
            // the floor lit cold beneath it, brighter as it dips
            Shadow(p, S.x, 0, 7, 1.2, C[0], 0.4);
            for (double x = -24; x <= 24; x += 6) p.Glow(S.x + x, -1, 16, C[4], (0.08 + 0.1 * near) * (1 - Math.Abs(x) / 32));
        }
    }
}
