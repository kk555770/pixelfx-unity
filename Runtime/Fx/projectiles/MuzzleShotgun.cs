// The flash lobes, spark, smoke and casing recipes are adapted from Sprite Fusion's open-source
// muzzle-flash (https://www.spritefusion.com/pixel-art-vfx/muzzle-flash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class MuzzleShotgun : Effect
    {
        public static readonly Module Module = new Module("muzzle-shotgun", "pixelfx/fx/projectiles/muzzle-shotgun", 160, 88, 44, 82, 3.2, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["smoke"] = new[] { "#221e2a", "#36313f", "#4e4858", "#6a6474", "#888192", "#a8a1b1", "#c8c1cf" },
                ["shell"] = new[] { "#4a0f14", "#9a1e22", "#d8423a", "#8c5c1c", "#e9b54a" },
                ["height"] = 26,
                ["seed"] = 5,
            },
            (o, e) => new MuzzleShotgun(o, e))
        {
            Title = "Shotgun blast",
            Desc = "A shotgun blast from a muzzle `height` px above the floor (the origin): a wide ragged fan of flame, pellet streaks, a heavy rolling smoke cloud, a red shell kicked out on the pump.",
            Tags = new[] { "projectile", "muzzle", "gun", "shotgun", "flash", "smoke", "casing", "directional", "one-shot" },
            Bg = "ground",
        };

        const double D = 3.2, F1 = 1.0 / 30;

        // the fan: a fat fireball at the muzzle and a spray of ragged tongues across the cone, each its own length
        static List<Lobe> Fan(double[] R, int f)
        {
            double k = f == 1 ? 1 : f == 2 ? 0.82 : 0.55, w = f == 1 ? 1 : 1.25;
            var L = new List<Lobe> { new Lobe { off = 0.5, a = 0, len = 11 * k, wid = 3.6 * w, core = 0.5 } };
            for (int i = 0; i < 9; i++)
            {
                double u = i / 8.0 - 0.5, a = u * 1.15 + R[i * 3] * 0.12, len = (17 + 11 * (1 - Math.Abs(u) * 1.4) + R[i * 3 + 1] * 7) * k;
                L.Add(new Lobe { off = 2, a = a, len = len, wid = (1.7 + R[i * 3 + 2]) * w, core = f == 1 ? 0.32 : 0 });
            }
            if (f == 1)
                foreach (int s in new[] { -1, 1 })
                    L.Add(new Lobe { off = 1, a = s * (1.6 + R[27 + (s > 0 ? 1 : 0)] * 0.4), len = 5 + R[29] * 3, wid = 1.2, core = 0 });
            return L;
        }

        sealed class Pellet
        {
            public double a, v, d0;
        }

        readonly uint[] F, SM, SH;
        readonly double H, MY, right;
        readonly Rng R;
        readonly Particles ps;
        readonly Smoke smoke;
        readonly double[] jit = new double[32];
        readonly Pellet[] pellets = new Pellet[10];
        readonly double st0, svx, svy, sva, sa0;
        double t;

        MuzzleShotgun(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 9);
            SM = Fit(o.Ramp("smoke"), 7);
            SH = Fit(o.Ramp("shell"), 5);
            H = o.Num("height");
            MY = -H;
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { F[7], F[6], F[4], F[3] }, emitter: e);
            smoke = new Smoke(R);
            right = 160 - 44 - 2;
            for (int i = 0; i < 32; i++) jit[i] = R.Next();
            // pellets: a tight spray, gone off the canvas in a few frames, each dragging a hot streak
            for (int i = 0; i < 10; i++) pellets[i] = new Pellet { a = R.Range(-0.13, 0.13), v = R.Range(900, 1150), d0 = R.Range(0, 4) };
            // sparks and burning wad, flung wide
            for (int i = 0; i < 16; i++)
            {
                double a = R.Range(-0.55, 0.55), sp = R.Range(180, 460);
                ps.Spawn(P.Spark, 4, MY, Px.Cos(a) * sp, Px.Sin(a) * sp - 20, R.Range(0.08, 0.3));
            }
            for (int i = 0; i < 7; i++)
            {
                double a = R.Range(-0.5, 0.3), sp = R.Range(70, 200);
                ps.Spawn(P.Ember, 4, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.4, 0.9), F[5]);
            }
            // the smoke: a heavy shove of gas forward that slows, rolls, swells and lifts; the first of it lit by the blast
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(-0.45, 0.35), sp = R.Range(60, 230) * (i < 8 ? 1.15 : 0.8);
                smoke.Add(new SmokeQ
                {
                    x = 4 + R.Range(0, 4), y = MY + R.Range(-1.5, 1.5), vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, r0 = R.Range(2.5, 3.5), r1 = R.Range(7, 12.5), life = R.Range(2.1, 2.9),
                    a = R.Range(0.55, 0.72), b = R.Range(0.9, 1), drag = R.Range(2.6, 3.6), rise = R.Range(9, 15), grow = R.Range(1.8, 2.8), warm = 1, delay = R.Range(0, 0.04),
                });
            }
            for (int i = 0; i < 6; i++)
                smoke.Add(new SmokeQ { x = 2, y = MY, vx = R.Range(15, 50), vy = -R.Range(10, 25), r0 = 1.5, r1 = R.Range(4, 6), life = R.Range(1.8, 2.4), a = 0.5, b = 1, drag = 2, rise = 10, curl = -R.Range(1.5, 3), warm = 0.6, delay = 0.08 + i * 0.05 });
            // the pump throws the spent shell a beat later
            st0 = 0.42;
            svx = -R.Range(30, 55);
            svy = -R.Range(110, 150);
            sva = R.Sign() * R.Range(16, 24);
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
            double pop = Exp(-(t + 0.008) / 0.05);
            List<Puff> list = smoke.Puffs();
            foreach (Puff q in list) q.w *= 0.25 + pop * 1.2;
            Cloud(p, list, SM, new[] { SM[0], Mix(F[1], SM[1], 0.5), Mix(F[2], SM[2], 0.4), F[3], F[4], F[5], F[7] }, dens: 1.4, max: 0.86, lit: 1.4);

            // the shell: red hull, brass head, tumbling up and back, bouncing, lying a moment
            if (t > st0 && t < D - 0.05)
            {
                CasingAt k = Casing(-9, MY, svx, svy, sva, t - st0, 850, -1, 0.35, sa0);
                double ca = Px.Cos(k.a), sa = Px.Sin(k.a), fade = Clamp((D - 0.05 - t) / 0.3);
                for (double d = -1.5; d <= 1.5; d += 0.5) p.Dot(k.x + ca * d, k.y + sa * d - 0.5, d > 0.8 ? (d > 1.2 ? SH[4] : SH[3]) : d > -0.6 ? SH[2] : SH[1], fade);
                for (double d = -1; d <= 1; d += 0.5) p.Dot(k.x + ca * d - sa, k.y + sa * d + ca - 0.5, d > 0.6 ? SH[3] : SH[0], fade);
                if (k.hit >= 0 && k.hit < 0.07) p.Glow(k.x, k.y, 5, SH[4], 0.5 * (1 - k.hit / 0.07));
            }

            // the flash: a white-hot hero frame, then the fan cooling and spreading as the gas balloons
            if (t < 3 * F1)
            {
                int f = t < F1 ? 1 : t < 2 * F1 ? 2 : 3;
                Flash(p, 0, MY, 0, Fan(jit, f), f == 1 ? new[] { WHITE, F[7], F[6], F[3] } : f == 2 ? new[] { F[7], F[6], F[4], F[2] } : new[] { F[5], F[4], F[3], F[1] }, f + 9);
            }
            // the pellets: hot heads with tails that cool to orange, clipped at the canvas edge
            if (t < 0.2)
                foreach (Pellet q in pellets)
                {
                    double head = 6 + q.d0 + q.v * t, tail = Math.Max(4, head - 26), ca = Px.Cos(q.a), sa = Px.Sin(q.a);
                    for (double d = tail; d <= head; d += 0.5)
                    {
                        double x = ca * d;
                        if (x > right) break;
                        double u = (d - tail) / Math.Max(1, head - tail);
                        p.Dot(x, MY + sa * d, u > 0.85 ? WHITE : u > 0.5 ? F[6] : F[4], u > 0.3 ? 1 : u / 0.3);
                    }
                    if (ca * head < right) p.Glow(ca * head, MY + sa * head, 4, F[6], 0.5);
                }
            ps.Draw(p);
            // light: a big pop that floods the floor, a white heart
            p.Glow(8, MY, 46, F[4], 0.95 * pop);
            p.Glow(4, MY, 12, WHITE, 0.6 * pop);
            p.Glow(10, 0, 30, F[4], 0.28 * pop);
        }
    }
}
