// The flash, spark and smoke recipes are adapted from Sprite Fusion's open-source muzzle-flash and explosion
// (https://www.spritefusion.com/pixel-art-vfx/muzzle-flash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class Cannonball : Effect
    {
        public static readonly Module Module = new Module("cannonball", "pixelfx/fx/projectiles/cannonball", 208, 116, 48, 102, 3.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["smoke"] = new[] { "#3a3542", "#55505e", "#736d7c", "#938d9b", "#b3adba", "#d2cdd6", "#ece8ee" },
                ["iron"] = new[] { "#141218", "#2c2a34", "#4c4a58", "#8a8898" },
                ["height"] = 12,
                ["seed"] = 4,
            },
            (o, e) => new Cannonball(o, e))
        {
            Title = "Cannon shot",
            Desc = "A cannon fires from a muzzle `height` px above the floor (the origin): a white flash, a tongue of flame, the ball arcing off trailing smoke, a big gunsmoke cloud rolling out.",
            Tags = new[] { "projectile", "cannon", "cannonball", "smoke", "flash", "directional", "one-shot" },
            Bg = "ground",
        };

        const double ANG = -0.32, F1 = 1.0 / 30;

        readonly uint[] F, SM, IR;
        readonly double H, MY, right, top;
        readonly Rng R;
        readonly Particles ps;
        readonly Smoke smoke;
        readonly Lights lights;
        readonly double[] jit = new double[16];
        double bx, by, bvx, bvy;
        bool bout;
        double t, wisp;

        Cannonball(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 9);
            SM = Fit(o.Ramp("smoke"), 7);
            IR = Fit(o.Ramp("iron"), 4);
            H = o.Num("height");
            MY = -H;
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { F[7], F[6], F[4], F[3] }, emitter: e);
            smoke = new Smoke(R);
            lights = new Lights(e);
            double ca = Px.Cos(ANG), sa = Px.Sin(ANG);
            right = 208 - 48 - 3;
            top = -102 + 3;
            bx = 4;
            by = MY;
            bvx = ca * 330;
            bvy = sa * 330;
            for (int i = 0; i < 16; i++) jit[i] = R.Next();
            lights.Add(12, MY - 2, 44, Hex("#ffb060"), 1, 0.35);
            lights.Add(6, MY, 18, WHITE, 0.9, 0.08);
            // burning wadding and sparks thrown out with the shot
            for (int i = 0; i < 22; i++)
            {
                double a = ANG + R.Range(-0.45, 0.45), sp = R.Range(140, 380);
                ps.Spawn(P.Spark, 6, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.1, 0.4));
            }
            for (int i = 0; i < 10; i++)
            {
                double a = ANG + R.Range(-0.3, 0.3), sp = R.Range(70, 170);
                ps.Spawn(P.Spark, 6, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.5, 0.9));
            }
            for (int i = 0; i < 8; i++)
            {
                double a = ANG + R.Range(-0.5, 0.3), sp = R.Range(40, 110);
                ps.Spawn(P.Ember, 8, MY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.7, 1.4), F[5], 2);
            }
            // the gunsmoke: a great white shove of it out of the bore that slows, rolls over itself, swells and lifts
            for (int i = 0; i < 34; i++)
            {
                double a = ANG + R.Range(-0.4, 0.35), sp = R.Range(40, 260) * (i < 12 ? 1.1 : 0.75);
                smoke.Add(new SmokeQ
                {
                    x = 6 + R.Range(0, 6), y = MY + R.Range(-2, 2), vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, r0 = R.Range(3, 4.5), r1 = R.Range(9, 16), life = R.Range(2.7, 3.5),
                    a = R.Range(0.6, 0.75), b = R.Range(0.9, 1), drag = R.Range(2.2, 3.2), rise = R.Range(7, 13), grow = R.Range(1.5, 2.4), curl = R.Range(-1.2, 0.4), warm = 1, delay = R.Range(0, 0.05),
                });
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!bout)
            {
                double x0 = bx, y0 = by;
                bvy += 260 * dt;
                bx += bvx * dt;
                by += bvy * dt;
                // the ball leaves a thin wisp of smoke off its hot skin, one puff every two pixels of flight
                for (wisp += Hypot(bx - x0, by - y0) / 2; wisp > 1; wisp--)
                {
                    double f = R.Next(), x = x0 + (bx - x0) * f, y = y0 + (by - y0) * f;
                    if (x < right - 8 && x > 24 && t < 0.6)
                        smoke.Add(new SmokeQ { x = x - 3, y = y, vx = -bvx * 0.04, vy = -bvy * 0.04, r0 = 1, r1 = R.Range(2.5, 3.6) * (1 - t), life = R.Range(0.7, 1.1), a = 0.5 * (1 - t), b = 0.95, drag = 2, rise = 5, grow = 2 });
                }
                if (bx > right || by < top) bout = true;
            }
            ps.Update(dt);
            smoke.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            double pop = Exp(-(t + 0.008) / 0.06);
            List<Puff> list = smoke.Puffs();
            foreach (Puff q in list) q.w *= 0.2 + pop * 1.3;
            Cloud(p, list, SM, new[] { SM[0], Mix(F[1], SM[1], 0.5), Mix(F[2], SM[2], 0.45), F[3], F[4], F[6], F[7] }, dens: 1.35, max: 0.86, lit: 1.3);
            ps.Draw(p);
            // the ball: dark iron, a lit crescent, a hot glint where the flash still burns on it
            if (!bout)
            {
                double x = bx, y = by;
                p.Circle(x, y, 3.2, IR[0]);
                p.Circle(x, y, 2.6, IR[1]);
                p.Circle(x - 0.7, y - 0.7, 1.8, IR[2]);
                p.Px(x - 1, y - 1.5, IR[3]);
                p.Px(x - 2, y - 1, IR[3]);
                p.Px(x + 1.5, y + 1, t < 0.3 ? F[5] : IR[2]);
                p.Glow(x, y, 6, F[4], 0.45 * Clamp(1 - t / 0.5));
            }
            // the flash: a white ball with a tongue of flame for a frame, then a cooler, bigger burst as the gas spills
            if (t < 3 * F1)
            {
                int f = t < F1 ? 1 : t < 2 * F1 ? 2 : 3;
                double k = (f == 1 ? 1 : f == 2 ? 1.15 : 0.8) * 1.3;
                var L = new List<Lobe> { new Lobe { off = 0, a = 0, len = 12 * k, wid = 5 * k, core = 0.55 }, new Lobe { off = 2, a = 0, len = (24 + jit[0] * 8) * k, wid = 2.2 * k, core = 0.4 } };
                for (int i = 0; i < 6; i++)
                {
                    double a = (i < 3 ? -1 : 1) * (0.4 + (i % 3) * 0.45 + jit[i + 1] * 0.2), down = Px.Sin(ANG + a);
                    double len = (8 + jit[i + 7] * 7) * k * (f == 3 ? 0.7 : 1);
                    if (down > 0.1) len = Math.Min(len, (H - 3) / down);
                    L.Add(new Lobe { off = 2, a = a, len = len, wid = 1.6 * k, core = 0.25 });
                }
                Flash(p, 4, MY, ANG, L, f == 1 ? new[] { WHITE, F[7], F[6], F[3] } : f == 2 ? new[] { F[7], F[6], F[4], F[2] } : new[] { F[5], F[4], F[3], F[1] }, f);
                if (f == 1)
                {
                    p.Circle(7, MY - 1, 6.5, WHITE);
                    p.Ring(7, MY - 1, 7.5, 1, F[7]);
                }
            }
            lights.Draw(p);
            p.Glow(14, 0, 22, F[4], 0.25 * pop);
        }
    }
}
