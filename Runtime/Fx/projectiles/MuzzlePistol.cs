// The flash lobes, spark, smoke and casing recipes are adapted from Sprite Fusion's open-source
// muzzle-flash (https://www.spritefusion.com/pixel-art-vfx/muzzle-flash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class MuzzlePistol : Effect
    {
        public static readonly Module Module = new Module("muzzle-pistol", "pixelfx/fx/projectiles/muzzle-pistol", 96, 72, 44, 66, 1.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["brass"] = new[] { "#4a2e10", "#8c5c1c", "#d9a441", "#ffe39a" },
                ["smoke"] = new[] { "#26222e", "#3b3644", "#544e5e", "#6f6979", "#8c8596", "#aca5b5", "#cbc4d2" },
                ["height"] = 26,
                ["seed"] = 3,
            },
            (o, e) => new MuzzlePistol(o, e))
        {
            Title = "Pistol shot",
            Desc = "One pistol shot from a muzzle `height` px above the floor (the origin): a spiky white-cored flash, sparks, a curl of smoke hooking up and back, a casing that bounces and settles.",
            Tags = new[] { "projectile", "muzzle", "gun", "pistol", "flash", "smoke", "casing", "directional", "one-shot" },
            Bg = "ground",
        };

        const double D = 1.8, F1 = 1.0 / 30;

        readonly uint[] F, B, SM;
        readonly double H, MY, seed;
        readonly Rng R;
        readonly Particles ps;
        readonly Smoke smoke;
        readonly int shapeSeed;
        readonly double tilt, svx, svy, sva, sa0;
        double t;

        MuzzlePistol(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 9);
            B = Fit(o.Ramp("brass"), 4);
            SM = Fit(o.Ramp("smoke"), 7);
            H = o.Num("height");
            MY = -H;
            seed = o.Seed;
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { F[7], F[6], F[4], F[3] }, emitter: e);
            smoke = new Smoke(R);
            // the flash's shape, fixed for this shot
            shapeSeed = R.Int(0, 1000000);
            tilt = R.Range(-0.05, 0.05);
            // sparks spat forward, a few grains of unburnt powder that drop
            for (int i = 0; i < 6; i++)
            {
                double a = R.Range(-0.3, 0.3), sp = R.Range(180, 400);
                ps.Spawn(P.Spark, 3, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.06, 0.16));
            }
            for (int i = 0; i < 5; i++)
            {
                double a = R.Range(-0.35, 0.25), sp = R.Range(60, 160);
                ps.Spawn(P.Ember, 3, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.5), F[5]);
            }
            // a puff of gas forward, then a chain of wisps that push out, slow and curl up and back over the muzzle
            for (int i = 0; i < 3; i++)
                smoke.Add(new SmokeQ { x = 3, y = MY, vx = R.Range(70, 120), vy = R.Range(-12, 6), r0 = 1.6, r1 = R.Range(4.5, 6), life = R.Range(0.8, 1.05), a = 0.55, drag = 3.5, rise = 10, warm = 0.9, delay = i * 0.012 });
            for (int i = 0; i < 14; i++)
            {
                double k = i / 13.0;
                smoke.Add(new SmokeQ
                {
                    x = 2, y = MY, vx = 115 - 75 * k + R.Range(-12, 12), vy = -6 - 10 * k + R.Range(-5, 5), r0 = 1.4, r1 = (3.6 + k * 2.6) * R.Range(0.75, 1.25), life = 1.15 + k * 0.3,
                    a = 0.62, b = R.Range(0.85, 1), drag = 2.2, rise = 6, curl = -4.4 + k * 1.8 + R.Range(-0.5, 0.5), grow = 1.6, warm = 0.7, delay = 0.02 + i * 0.022,
                });
            }
            smoke.Add(new SmokeQ { x = -7, y = MY - 2, vx = -6, vy = -18, r0 = 1.2, r1 = 4, life = 1.05, a = 0.4, rise = 8, delay = 0.03 });
            svx = -R.Range(25, 45);
            svy = -R.Range(95, 130);
            sva = R.Sign() * R.Range(22, 32);
            sa0 = R.Range(0, 6.28);
        }

        public override void Update(double dt)
        {
            t += dt;
            ps.Update(dt);
            smoke.Update(dt);
        }

        public override void Draw(Painter p)
        {
            double pop = Exp(-(t + 0.008) / 0.035);
            List<Puff> list = smoke.Puffs();
            foreach (Puff q in list) q.w *= 0.4 + pop;
            Cloud(p, list, SM, new[] { SM[0], Mix(F[1], SM[1], 0.5), Mix(F[2], SM[2], 0.4), F[3], F[4], F[5], F[7] }, dens: 1.5, max: 0.8, lit: 1.4);
            // the casing: out of the port, up and back, tumbling, a bounce or two, then it lies there and fades
            if (t < D) DrawCasing(p, Casing(-7, MY - 1, svx, svy, sva, t, 850, -0.5, 0.42, sa0), B, Math.Min(1, (D - 0.05 - t) / 0.3));
            // the flash: the hero frame with a white core, then one cooler, smaller frame
            if (t < 2 * F1)
            {
                int f = t < F1 ? 1 : 2;
                List<Lobe> L = FlashShape(NewRng(shapeSeed), f, 1.15, new double[] { 15, 22 }, new double[] { 7, 12 }, new double[] { 4.5, 8 }, new double[] { 3, 5.5 });
                Flash(p, 0, MY, tilt, L, f == 1 ? new[] { WHITE, F[7], F[6], F[3] } : new[] { F[7], F[6], F[4], F[2] }, f + seed);
            }
            ps.Draw(p);
            // light: the pop, its warm pool, a touch on the floor below
            p.Glow(4, MY, 30, F[4], 0.85 * pop);
            p.Glow(2, MY, 8, WHITE, 0.5 * pop);
            p.Glow(4, 0, 16, F[4], 0.2 * pop);
        }
    }
}
