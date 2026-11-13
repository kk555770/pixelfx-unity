// The swirl's texture (streaks laid along a differentially wound flow, light on the brightest pixels) is adapted
// from Sprite Fusion's open-source black hole (https://www.spritefusion.com/pixel-art-vfx/black-hole), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Whirlpool : Effect
    {
        public static readonly Module Module = new Module("whirlpool", "pixelfx/fx/water/whirlpool", 128, 84, 64, 42, 3, true, 30,
            new Options { ["palette"] = WATER, ["seed"] = 5 },
            (o, e) => new Whirlpool(o, e))
        {
            Title = "Whirlpool",
            Desc = "Seen from above, dark water winds into a vortex: foam arms spiral in, darkening as they're drawn down into a deep eye with a lit far rim, flecks of foam circling in after them.",
            Tags = new[] { "water", "whirlpool", "vortex", "maelstrom", "swirl", "top-down", "loop" },
            Bg = "dark",
        };

        const double D = 3, R = 60, SY = 0.6, K = 2.4, EYE = 0.16;

        // the flow at radius rho (0 eye, 1 rim), angle th: thin streaks of foam laid along a log spiral that turns once a
        // loop, dashed and brightest where two sets of streaks cross
        static double Foam(double rho, double th, double ph)
        {
            double s = Px.Log(Math.Max(rho, 0.02)), a = th + K * s + ph * TAU, b = th + K * 1.35 * s + ph * TAU * 2;
            double one = Px.Pow(0.5 + 0.5 * Px.Cos(7 * a + 0.9 * Px.Sin(3 * a + s * 4)), 26);
            double two = Px.Pow(0.5 + 0.5 * Px.Cos(11 * b + 1.3), 34) * 0.7;
            // finer streaks crowd in near the eye, where the water runs fastest
            double three = Px.Pow(0.5 + 0.5 * Px.Cos(17 * (th + K * 0.8 * s + ph * TAU * 3)), 30) * 0.8 * Math.Max(0, 1 - (rho - EYE) * 2.2);
            double dash = 0.55 + 0.45 * Px.Sin(s * 22 - ph * TAU * 4 + a * 2);
            return (one + two + three) * dash;
        }

        readonly Options o;

        Whirlpool(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, ry = R * SY, seed = o.Seed;
            p.Shade(-R, -ry, R, ry, (x, y, @out) =>
            {
                double dx = x / R, dy = y / ry, rho = Hypot(dx, dy);
                if (rho > 1) return 0;
                double th = Atan2(dy, dx);
                // the eye: a hole down into the dark, its far wall lit, a ring of foam racing round its lip
                if (rho < EYE)
                {
                    double far = -dy / rho;
                    double wall = (rho / EYE - 0.35) / 0.65;
                    if (far > 0) @out.c = wall > 0.8 ? (far > 0.6 ? C[5] : C[4]) : wall > 0.45 ? C[3] : wall > 0.15 ? C[2] : C[0];
                    else @out.c = wall > 0.85 && far > -0.5 ? C[2] : C[0];
                    return 0;
                }
                double k = (rho - EYE) / (1 - EYE), fade = 1 - Smooth(0.62, 1, k);
                // dark see-through water, darker toward the eye, a faint sheen mid-way
                p.Blend(x, y, k < 0.25 ? C[0] : C[1], (0.75 - 0.25 * k) * (1 - Smooth(0.8, 1, k)));
                if (k > 0.3 && k < 0.8) p.Blend(x, y, C[2], 0.18 * Px.Sin(Math.PI * (k - 0.3) / 0.5));
                // the foam ring round the eye
                if (k < 0.035)
                {
                    double g = 0.5 + 0.5 * Px.Cos(th * 9 - ph * TAU * 6);
                    @out.c = g > 0.7 ? WHITE : g > 0.3 ? C[6] : C[4];
                    return 0;
                }
                double I = Foam(rho, th, ph) * fade * Math.Min(1, (k - 0.08) * 4) * (0.45 + 0.55 * Px.Sin(Math.PI * Math.Min(1, k * 1.25)));
                if (I > 0.12)
                {
                    @out.c = I > 0.75 ? WHITE : I > 0.45 ? C[6] : I > 0.25 ? C[5] : C[4];
                    if (I > 0.25)
                    {
                        @out.l = C[5];
                        @out.k = 0.08 + 0.14 * Math.Min(1, I);
                    }
                }
                return 0;
            });
            // flecks of foam drawn round and in along the arms, each a short streak along its path
            Cycle(t, D, 30, seed, g => g.Range(1.2, 2.2), (age, life, r, _) =>
            {
                double u = age / life, rho = 0.8 - 0.62 * u * u, a0 = r.Range(0, TAU), th = a0 - K * Px.Log(rho);
                double ang = th - age * (1.5 + 2.2 / rho), x = Px.Cos(ang) * rho * R, y = Px.Sin(ang) * rho * ry;
                double ang2 = ang + 0.12 / rho, x2 = Px.Cos(ang2) * rho * R, y2 = Px.Sin(ang2) * rho * ry;
                if (u > 0.85 && r.Next() < (u - 0.85) * 6) return;
                p.Line(x2, y2, x, y, u < 0.2 ? C[5] : u < 0.6 ? WHITE : C[6]);
            });
            // glints winking on the foam
            Cycle(t, D, 8, seed + 3, g => g.Range(0.25, 0.45), (age, life, r, _) =>
            {
                double rho = r.Range(0.35, 0.8), th = r.Range(0, TAU) - age * 2.5, k = Px.Sin((Math.PI * age) / life);
                double x = Px.Cos(th) * rho * R, y = Px.Sin(th) * rho * ry;
                if (Foam(rho, th, ph) < 0.35) return;
                p.Px(x, y, WHITE);
                if (k > 0.55) foreach (int[] v in Sim.Cross) p.Px(x + v[0], y + v[1], C[6]);
                p.Glow(x, y, 5, C[5], 0.4 * k);
            });
            // light: a cool sheen on the swirl, a glow off the eye's lit rim
            p.Glow(0, 0, R * 0.9, C[4], 0.2);
            p.Glow(0, 0, R * EYE * 1.6 + 6, C[5], 0.28);
            p.Glow(0, -ry * EYE * 0.8, 14, C[5], 0.32);
        }
    }
}
