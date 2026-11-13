// The spark, star and light recipes are adapted from Sprite Fusion's open-source explosion and sparkle
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class Flare : Effect
    {
        public static readonly Module Module = new Module("flare", "pixelfx/fx/projectiles/flare", 152, 232, 76, 226, 5, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0610", "#5c0d1c", "#9a1428", "#d8243a", "#ff4d5e", "#ff8a94", "#ffd0d4", "#ffffff" },
                ["smoke"] = new[] { "#1e1a26", "#2e2938", "#433d4d", "#5b5466", "#77707f", "#958e9c", "#b3acb9" },
                ["height"] = 150,
                ["seed"] = 2,
            },
            (o, e) => new Flare(o, e))
        {
            Title = "Signal flare",
            Desc = "A signal flare streaks up off the ground trailing sparks and smoke, bursts, and hangs as a pulsing red star that drips sparks under its own red-lit smoke, then gutters out.",
            Tags = new[] { "projectile", "flare", "signal", "fire", "smoke", "light", "one-shot" },
            Bg = "sky",
        };

        const double RISE = 0.85, BURN = 3.1;

        readonly uint[] C, SM, WR;
        readonly double H;
        readonly Rng R;
        readonly Particles ps;
        readonly Smoke smoke;
        readonly Lights lights;
        double t, emit, drip;
        bool burst;

        // the rise: fast off the ground, slowing to the top; then the star hangs, sways and sinks slowly
        (double x, double y) Rise(double tt) => (Px.Sin(tt * 3) * 2 * (tt / RISE), -H * Ease.OutCubic(Clamp(tt / RISE)));

        (double x, double y) Star(double tt)
        {
            double u = tt - RISE;
            return (Px.Sin(u * 1.6) * 3 + Rise(RISE).x, -H + u * 9 + Px.Sin(u * 2.3) * 1.5);
        }

        Flare(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            SM = Fit(o.Ramp("smoke"), 7);
            H = o.Num("height");
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[4], C[2] }, emitter: e);
            smoke = new Smoke(R);
            lights = new Lights(e);
            WR = new[] { C[0], C[1], Mix(C[2], SM[2], 0.45), Mix(C[3], SM[3], 0.35), C[4], C[5], C[6] };
            lights.Add(0, -3, 26, C[4], 0.8, 0.25);
            for (int i = 0; i < 5; i++)
                smoke.Add(new SmokeQ { x = R.Range(-3, 3), y = -2, vx = R.Range(-25, 25), vy = -R.Range(5, 20), r0 = 2, r1 = R.Range(4, 6), life = R.Range(1, 1.6), a = 0.5, warm = 0.8, rise = 6 });
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < RISE)
            {
                var (x, y) = Rise(t);
                var (px, py) = Rise(Math.Max(0, t - dt));
                for (emit += Hypot(x - px, y - py) / 1.5; emit > 1; emit--)
                    smoke.Add(new SmokeQ { x = x + R.Range(-0.6, 0.6), y = y + 4, vx = R.Range(-6, 6), vy = R.Range(0, 8), r0 = 1.3, r1 = R.Range(3, 4.8), life = R.Range(1.4, 2.1), a = 0.5, b = 0.95, warm = 0.9, rise = 3, drag = 2, grow = 1.4 });
                if (R.Chance(0.5)) ps.Spawn(P.Spark, x, y + 3, R.Range(-40, 40), R.Range(20, 90), R.Range(0.2, 0.45));
            }
            else if (!burst)
            {
                burst = true;
                var (x, y) = Star(t);
                lights.Add(x, y, 64, C[4], 0.9, 0.5);
                lights.Add(x, y, 24, WHITE, 0.8, 0.12);
                for (int i = 0; i < 30; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(70, 190);
                    ps.Spawn(P.Spark, x, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.25, 0.7));
                }
                for (int i = 0; i < 10; i++)
                    smoke.Add(new SmokeQ { x = x + R.Range(-3, 3), y = y + R.Range(-3, 3), vx = R.Range(-40, 40), vy = R.Range(-40, 10), r0 = 2.5, r1 = R.Range(6, 9), life = R.Range(1.6, 2.4), a = 0.55, warm = 1, rise = 10, drag = 2.5 });
            }
            else if (t < RISE + BURN)
            {
                // the star burns: sparks drip off it, smoke boils up off it into a plume it lights from below
                var (x, y) = Star(t);
                double k = (t - RISE) / BURN, power = 1 - Smooth(0.75, 1, k);
                for (drip += dt * 26 * power; drip > 1; drip--) ps.Spawn(P.Spark, x + R.Range(-1.5, 1.5), y + 2, R.Range(-30, 30), R.Range(10, 60), R.Range(0.35, 0.8));
                if (R.Chance(dt * 18 * power))
                    smoke.Add(new SmokeQ { x = x + R.Range(-1.5, 1.5), y = y - 2, vx = R.Range(-8, 8), vy = -R.Range(15, 30), r0 = 2, r1 = R.Range(6, 10), life = R.Range(1.6, 2.4) * (1 - 0.4 * k), a = 0.5, b = 0.85, warm = 1, rise = 14, drag = 1.6, grow = 1.2 });
            }
            ps.Update(dt);
            smoke.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            // the smoke lit red by the star, more the closer and the brighter it burns
            List<Puff> list = smoke.Puffs();
            double lit = t > RISE && t < RISE + BURN ? 1 - Smooth(0.75, 1, (t - RISE) / BURN) : 0;
            if (lit > 0)
            {
                var (sx, sy) = Star(t);
                foreach (Puff q in list) q.w = Math.Max(q.w, lit * Exp(-Hypot(q.x - sx, q.y - sy) / 26));
            }
            Cloud(p, list, SM, WR, dens: 1.4, max: 0.8, lit: 1.3);
            ps.Draw(p);
            if (t < RISE)
            {
                // the rising flare: a white-hot head, a short red flame under it
                var (x, y) = Rise(t);
                p.Line(x, y + 1, x, y + 5, u => u < 0.3 ? C[6] : u < 0.7 ? C[4] : C[3]);
                p.Px(x, y, WHITE);
                p.Px(x, y - 1, C[6]);
                p.Glow(x, y, 14, C[4], 0.6);
                p.Glow(x, y, 4, WHITE, 0.4);
            }
            else if (t < RISE + BURN)
            {
                var (x, y) = Star(t);
                double u = t - RISE, k = u / BURN;
                int fr = (int)Math.Floor(t * 30);
                double fl = 0.8 + 0.2 * Rand(fr, 3) + 0.08 * Px.Sin(u * 19), die = 1 - Smooth(0.78, 1, k);
                // the burst: a flash of white and rays for a moment
                if (u < 0.25)
                {
                    double e = u / 0.25;
                    Sim.Flare(p, x, y, 22 * (1 - e) + 3, 1 - e, C);
                    if (e < 0.25) p.Circle(x, y, 5 * (1 - e * 3) + 1, WHITE);
                }
                // the star: a hot ball with a long cross of light that pulses, guttering at the end
                double r = (2 + 0.6 * fl) * (0.4 + 0.6 * die);
                p.Circle(x, y, r + 1.2, C[3]);
                p.Circle(x, y, r, die > 0.4 ? C[5] : C[4]);
                if (die > 0.25) p.Circle(x - 0.3, y - 0.3, r * 0.55, die > 0.6 ? WHITE : C[6]);
                if (die > 0.15 && Rand(fr, 9) > 0.15) Sim.Sparkle(p, x, y, Round((5 + 3 * fl) * die), C[6], C[4], 0.5 * die);
                p.Glow(x, y, 70 * (0.6 + 0.4 * die), C[3], 0.55 * fl * die);
                p.Glow(x, y, 22, C[4], 0.5 * fl * die);
                p.Glow(x, y, 6, WHITE, 0.35 * die);
            }
            lights.Draw(p);
        }
    }
}
