// The tangent spark streaks are adapted from Sprite Fusion's open-source energy and impact effects
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class EnergyDisc : Effect
    {
        public static readonly Module Module = new Module("energy-disc", "pixelfx/fx/projectiles/energy-disc", 120, 52, 92, 26, 0.5, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0a0f2e", "#172a78", "#2550d0", "#3f8cff", "#7cc8ff", "#c8f0ff", "#ffffff" },
                ["size"] = 9,
                ["teeth"] = 8,
                ["speed"] = 220,
                ["seed"] = 5,
            },
            (o, e) => new EnergyDisc(o))
        {
            Title = "Energy disc",
            Desc = "A sawblade of crackling energy spinning flat out as it flies: hooked teeth bright on their leading edges, a white hub, spiral arms churning, sparks flung off the teeth.",
            Tags = new[] { "projectile", "disc", "saw", "energy", "sci-fi", "directional", "loop" },
            Bg = "sky",
        };

        const double D = 0.5;
        const int OX = 92;

        readonly Options o;

        EnergyDisc(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 7);
            double R = o.Num("size"), N = o.Num("teeth"), V = o.Num("speed"), S = o.Seed, left = -OX + 2;
            double rot = (TAU * 3 * t) / D, seg = TAU / N;
            double Frac(double x) => x - Math.Floor(x);

            // speed lines peeling off the rim, top and bottom, stretching back and cooling
            Beat(t, D, 8, S + 4, r => r.Range(0.12, 0.22), (age, life, r, i) =>
            {
                double k = age / life, side = r.Chance(0.5) ? -1 : 1, x0 = -2 - age * r.Range(180, 260), len = r.Range(8, 16) * (0.5 + k), y = side * (R + r.Range(0.5, 3));
                if (x0 - len < left) return;
                p.Line(x0, y, x0 - len, y, k < 0.35 ? C[5] : k < 0.7 ? C[4] : C[3], u => u < 0.65 ? 1 : (1 - u) / 0.35);
            });
            p.GlowLine(-2, 0, -26, 0, 8, C[3], 0.35);

            // sparks flung off the tooth tips along the spin, then left behind in the air
            uint[] SP = { WHITE, C[5], C[4], C[3] };
            Beat(t, D, 20, S, r => r.Range(0.12, 0.3), (age, life, r, i) =>
            {
                double a = rot - r.Range(0, TAU) * 0 + r.Range(0, TAU), tip = R + 3.5, x0 = Px.Cos(a) * tip, y0 = Px.Sin(a) * tip, v = r.Range(120, 230);
                var (x, y, vx, vy) = Wake(x0, y0, -Px.Sin(a) * v, Px.Cos(a) * v, age, V * 0.6, 2.4, 60);
                if (x > left + 8 && Math.Abs(y) < 23) Spark(p, x, y, vx, vy, 1 - age / life, SP, 0.45, 0.014, 7);
            });

            // the disc: a white hub in a dark collar, spiral arms of energy churning, a bright rim; past the rim, hooked
            // teeth that run white on their leading faces, and a dim blur ring where the teeth have just been
            p.Shade(-R - 5, -R - 5, R + 5, R + 5, (x, y, ou) =>
            {
                double r = Hypot(x, y), th = Atan2(y, x);
                if (r > R + 4.6) return 0;
                if (r <= R)
                {
                    if (r < 1.8) return WHITE;
                    if (r < 2.9) return C[1];
                    if (r > R - 1.1) return C[5];
                    double arm = Px.Sin(3 * (th - rot * 0.5) - r * 0.85);
                    return arm > 0.65 ? C[5] : arm > 0.2 ? C[4] : arm > -0.3 ? C[3] : C[2];
                }
                double ph = Frac((th - rot) / seg), h = 4.2 * Px.Pow(ph, 1.4);
                if (r - R < h) return ph > 0.86 ? WHITE : ph > 0.6 ? C[6] : ph > 0.35 ? C[5] : C[4];
                if (r - R < 1.6) return C[2];
                return 0;
            });
            // filaments of energy crackling from the hub to the rim, new every frame
            int fr = (int)Math.Floor(t * 30 + 1e-6);
            for (int j = 0; j < 3; j++)
            {
                Rng g = NewRng(fr, j, S);
                double a0 = g.Range(0, TAU);
                var pts = new double[5][];
                for (int i = 0; i <= 4; i++)
                {
                    double rr = 2.5 + ((R - 3) * i) / 4, a = a0 + i * 0.25 + (i != 0 ? g.Range(-0.35, 0.35) : 0);
                    pts[i] = new[] { Px.Cos(a) * rr, Px.Sin(a) * rr };
                }
                p.Polyline(pts, j != 0 ? C[6] : WHITE);
            }
            p.Ring(0, 0, R + 0.5, 1, C[6]);
            // light: a soft pool, a hot hub, a glint riding the top of the rim
            p.Glow(0, 0, 30, C[3], 0.4);
            p.Glow(0, 0, 6, C[5], 0.35);
            p.Glow(Px.Cos(rot) * R, Px.Sin(rot) * R, 5, C[6], 0.4);
        }
    }
}
