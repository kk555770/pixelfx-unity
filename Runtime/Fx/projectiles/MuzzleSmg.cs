// The flash lobes, spark, smoke wisp and casing recipes are adapted from Sprite Fusion's open-source
// muzzle-flash (https://www.spritefusion.com/pixel-art-vfx/muzzle-flash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class MuzzleSmg : Effect
    {
        public static readonly Module Module = new Module("muzzle-smg", "pixelfx/fx/projectiles/muzzle-smg", 112, 72, 56, 66, 1, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["brass"] = new[] { "#4a2e10", "#8c5c1c", "#d9a441", "#ffe39a" },
                ["smoke"] = new[] { "#26222e", "#3b3644", "#544e5e", "#6f6979", "#8c8596", "#aca5b5", "#cbc4d2" },
                ["height"] = 26,
                ["seed"] = 6,
            },
            (o, e) => new MuzzleSmg(o))
        {
            Title = "SMG burst",
            Desc = "Sustained fire at twelve rounds a second: each shot a different spiky flash with a white core, sparks, smoke wisps piling up and drifting, brass casings bouncing on the floor.",
            Tags = new[] { "projectile", "muzzle", "gun", "automatic", "flash", "smoke", "casing", "directional", "loop" },
            Bg = "ground",
        };

        const int SHOTS = 12, OY = 66;
        const double F1 = 1.0 / 30;

        // every live copy of every shot: fn(s, age) for shot s fired at s * D / SHOTS, while age < life
        static void Shots(double t, double D, double life, Action<int, double> fn)
        {
            for (int s = 0; s < SHOTS; s++)
                for (double age = Wrap(t - (s * D) / SHOTS + 1e-7, D); age < life; age += D) fn(s, age);
        }

        readonly Options o;

        MuzzleSmg(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), B = Fit(o.Ramp("brass"), 4), SM = Fit(o.Ramp("smoke"), 7);
            double S = o.Seed, D = 1, H = o.Num("height");
            double MX = 0, MY = -H, top = -OY + 1;
            uint[] SP = { F[7], F[6], F[4], F[3] };
            // the latest shot's pop of light, for the smoke it lights up
            double pop = 0;
            Shots(t, D, 0.2, (s, age) => pop = Math.Max(pop, Exp(-(age + 0.008) / 0.035)));

            // smoke: every shot leaves a few wisps that push forward, slow, swell and rise, piling up into a drifting haze
            var puffs = new List<Puff>();
            Shots(t, D, 2.2, (s, age) =>
            {
                for (int j = 0; j < 3; j++)
                {
                    Rng r = NewRng(S, s, j, 3);
                    double life = r.Range(1.5, 2.2), dl = j * 0.03;
                    if (age < dl || age - dl >= life) continue;
                    double a = age - dl, k = a / life;
                    bool port = j == 2;
                    // a push forward that dies fast, then the warm gas lifts it and the air drifts it back over the gun
                    double vx = port ? r.Range(-12, 6) : r.Range(45, 100) * (1 - j * 0.4), vy = port ? -r.Range(14, 26) : -r.Range(4, 16);
                    double x = (port ? -7 : MX + 3) + (vx * (1 - Exp(-3.2 * a))) / 3.2 - r.Range(3, 7) * a * a + Px.Sin(a * 2.1 + r.Range(0, TAU)) * a * 2.5;
                    double y = (port ? MY - 2 : MY) + (vy * (1 - Exp(-2.4 * a))) / 2.4 - 0.5 * r.Range(9, 16) * a * a;
                    double rad = (port ? 1 : 1.2) + r.Range(2, 4.5) * (1 - Exp(-1.4 * a));
                    if (y - rad < top + 1) continue;
                    double edge = Math.Min(1, (y - rad - top) / 12);
                    puffs.Add(new Puff { x = x, y = y, r = rad, a = Math.Min(1, a * 10) * Px.Pow(1 - k, 1.6) * (port ? 0.24 : 0.34) * edge, b = Clamp(0.92 - k * 0.6), w = pop * Exp(-Hypot(x - MX, y - MY) / 9) });
                }
            });
            Cloud(p, puffs, SM, new[] { SM[0], Mix(F[1], SM[1], 0.5), Mix(F[2], SM[2], 0.4), F[3], F[4], F[5], F[7] }, dens: 1.4, max: 0.7, lit: 1.3);

            // casings: thrown up and back out of the ejection port, tumbling, bouncing on the floor, then lying a moment
            Shots(t, D, D, (s, age) =>
            {
                Rng r = NewRng(S, s, 7);
                CasingAt k = Casing(-7, MY - 1, -r.Range(18, 48), -r.Range(85, 135), r.Sign() * r.Range(20, 32), age, 850, -0.5, 0.42, r.Range(0, TAU));
                DrawCasing(p, k, B, Clamp((D - age) / 0.25));
            });

            // the flash: a hero frame with a white core, then a cooler, smaller one; every shot its own shape
            Shots(t, D, 2 * F1, (s, age) =>
            {
                Rng r = NewRng(S, s);
                int f = age < F1 ? 1 : 2;
                double tilt = r.Range(-0.06, 0.06);
                List<Lobe> L = FlashShape(r, f, r.Range(0.95, 1.2), new double[] { 12, 21 }, new double[] { 6, 11 }, new double[] { 4, 7.5 }, new double[] { 2.5, 5 });
                uint[] C = f == 1 ? new[] { WHITE, F[7], F[6], F[3] } : new[] { F[7], F[6], F[4], F[2] };
                Flash(p, MX, MY, tilt, L, C, s * 7 + f);
            });

            // sparks and unburnt grains spat forward with each shot, dropping as they slow
            Shots(t, D, 0.3, (s, age) =>
            {
                for (int j = 0; j < 5; j++)
                {
                    Rng r = NewRng(S, s, j, 11);
                    double life = r.Range(0.05, 0.16) * (j > 2 ? 1.8 : 1);
                    if (age >= life) continue;
                    double a = r.Range(-0.3, 0.3), sp = r.Range(170, 380) * (j > 2 ? 0.5 : 1);
                    var (x, y) = Fly(MX + 2, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, age, 420, 2.2);
                    var (vx, vy) = FlyV(Px.Cos(a) * sp, Px.Sin(a) * sp, age, 420, 2.2);
                    if (j > 2) p.Px(x, y, age / life < 0.5 ? F[6] : F[4]);
                    else Spark(p, x, y, vx, vy, 1 - age / life, SP, 0.45, 0.011);
                }
            });

            // light: a hot pop at the muzzle on every shot, the floor and smoke catching it
            Shots(t, D, 0.15, (s, age) =>
            {
                double k = Exp(-(age + 0.008) / 0.03);
                p.Glow(MX + 4, MY, 26, F[4], 0.8 * k);
                p.Glow(MX + 2, MY, 7, WHITE, 0.45 * k);
                p.Glow(MX + 2, 0, 12, F[4], 0.16 * k);
            });
        }
    }
}
