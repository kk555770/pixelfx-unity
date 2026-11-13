// The pop recipe (a gathering, a white flash with a ring and flares, twinkling motes that linger and fade) is
// adapted from Sprite Fusion's open-source sparkle (https://www.spritefusion.com/pixel-art-vfx/sparkle),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class SnowflakeBurst : Effect
    {
        const double DURATION = 3.2;

        public static readonly Module Module = new Module("snowflake-burst", "pixelfx/fx/ice/snowflake-burst", 140, 120, 70, 106, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["count"] = 9,
                ["seed"] = 8,
            },
            (o, e) => new SnowflakeBurst(o, e))
        {
            Title = "Snowflake burst",
            Desc = "A cold pop: frost gathers and flashes white in a ring, then crisp six-armed snowflakes spin out, slow, twinkle and drift down to settle and melt.",
            Tags = new[] { "ice", "snow", "snowflake", "burst", "magic", "one-shot" },
            Bg = "ground",
        };

        const double POP = 0.16, CY = -48;
        static readonly int[] SIZES = { 2, 0, 1, 0, 2, 0, 1, 0, 1 };

        sealed class Flake
        {
            public double x, y, vx, vy, r, rot, w0, w1, life, age, ph, tw, tw0;
            public FlakeDesign d;
            public bool rest, popped;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Flake[] flakes;
        double t;
        bool popped;

        SnowflakeBurst(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            lights = new Lights(e);
            // the flakes: two big, a few middling, the rest small; small ones fly further
            double count = o.Num("count");
            var list = new System.Collections.Generic.List<Flake>();
            for (int i = 0; i < count; i++)
            {
                int z = SIZES[i % SIZES.Length];
                double r = z == 2 ? R.Range(9.5, 11.5) : z == 1 ? R.Range(6, 7.5) : R.Range(3, 4.5);
                double a = (i / count) * TAU + R.Range(-0.3, 0.3) + 0.4, sp = R.Range(80, 120) * (1.4 - r / 14);
                list.Add(new Flake
                {
                    x = 0,
                    y = CY,
                    vx = Px.Cos(a) * sp,
                    vy = Px.Sin(a) * sp * 0.85 - 20,
                    r = r,
                    rot = R.Range(0, TAU),
                    w0 = R.Sign() * R.Range(7, 12),
                    w1 = R.Sign() * R.Range(0.4, 1),
                    d = FlakeDesignOf(R),
                    life = R.Range(2.1, 2.8),
                    age = 0,
                    ph = R.Range(0, TAU),
                    tw = R.Range(0.5, 0.85),
                    tw0 = R.Range(0.15, 0.6),
                    rest = false,
                    popped = false,
                });
            }
            flakes = list.ToArray();
        }

        static double Sz(Flake f) => f.r * Math.Min(1, Ease.OutBack(Clamp(f.age / 0.18), 2)) * Math.Min(1, (f.life - f.age) / 0.35);

        public override void Update(double dt)
        {
            t += dt;
            // the cold gathers: streaks of frost spiral in, snow motes drift in
            if (t < POP - 0.03 && R.Chance(0.8))
            {
                double a = R.Range(0, TAU), D = R.Range(22, 44), T = R.Range(0.12, 0.16), v = (D * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, Px.Cos(a) * D, CY + Px.Sin(a) * D, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!popped && t >= POP)
            {
                popped = true;
                lights.Add(0, CY, 44, WHITE, 0.8, 0.1);
                lights.Add(0, CY, 72, C[4], 0.5, 0.6);
                // frost thrown out: streaking motes, snow, glints that come and go round it
                for (int i = 0; i < 22; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(80, 150);
                    ps.Spawn(P.Mote, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.5), R.Chance(0.5) ? C[6] : C[5], R.Range(0.6, 1));
                }
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(-Math.PI, 0.2), sp = R.Range(30, 110);
                    ps.Spawn(P.Flake, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.6), R.Chance(0.6) ? WHITE : C[6], R.Range(0.5, 1.5));
                }
                for (int i = 0; i < 12; i++)
                {
                    double a = R.Range(0, TAU), d = R.Range(10, 44);
                    ps.Spawn(P.Star, Px.Cos(a) * d, CY + Px.Sin(a) * d * 0.85, 0, R.Range(2, 6), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 2.2), R.Range(0.08, 1.6));
                }
            }
            if (popped)
                foreach (Flake f in flakes)
                {
                    f.age += dt;
                    if (f.age >= f.life) continue;
                    if (!f.rest)
                    {
                        f.vx -= f.vx * 2.8 * dt;
                        f.vy -= f.vy * 2.8 * dt - 26 * dt;
                        f.x += f.vx * dt + Px.Sin(t * 1.8 + f.ph) * 5 * dt;
                        f.y += f.vy * dt;
                        f.rot += (f.w1 + (f.w0 - f.w1) * Exp(-f.age * 2.5)) * dt;
                        // it settles on the floor, flat and still, and melts there
                        if (f.y > -Sz(f) * 0.3)
                        {
                            f.rest = true;
                            f.y = -1;
                            f.life = Math.Min(f.life, f.age + 0.6);
                        }
                    }
                    // a melted or spent flake goes out with a glint or two
                    if (!f.popped && f.life - f.age < 0.1)
                    {
                        f.popped = true;
                        for (int i = 0; i < 2; i++) ps.Spawn(P.Star, f.x + R.Range(-2, 2), f.y + R.Range(-2, 2), R.Range(-6, 6), -R.Range(2, 8), R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 1.6));
                    }
                }
            ps.Update(dt);
            lights.Update(dt);
            EndBy(t, DURATION, ps);
        }

        public override void Draw(Painter p)
        {
            // the gathering: a tiny flake spins up at the heart, faster and brighter
            if (t < POP)
            {
                double k = t / POP;
                Snowflake(p, 0, CY, 1 + 3.2 * k * k, t * 20, flakes[0].d, C, hot: k);
                p.Glow(0, CY, 8 + 14 * k, C[5], 0.3 + 0.4 * k);
            }
            // the pop: a white flash, a flare and a ring of frost running out
            double q = t - POP;
            if (q >= 0 && q < 0.4)
            {
                if (q < 0.07) p.Circle(0, CY, 6 * (1 - q / 0.07) + 1, WHITE);
                Sim.Flare(p, 0, CY, 28 * Px.Pow(Math.Max(0, 1 - q / 0.26), 1.5), 1 - q / 0.26, C);
                double e = Ease.OutCubic(q / 0.4), r = 5 + 34 * e;
                p.Ring(0, CY, r, Math.Max(1, 2.4 * (1 - e)), q < 0.08 ? WHITE : q < 0.2 ? C[6] : C[5], 1.15 - e);
                p.GlowRing(0, CY, r, 5, C[5], 0.4 * (1 - e));
            }
            // the cold light left where it burst, fading
            if (popped) p.Glow(0, CY, 46, C[4], 0.3 * (1 - Smooth(POP, POP + 1.6, t)));
            // the flakes: spinning out, twinkling now and then, landing and melting
            foreach (Flake f in flakes)
            {
                if (!popped || f.age >= f.life) continue;
                double r = Sz(f), tw = Px.Pow(Math.Max(0, Px.Sin(((f.age - f.tw0) / f.tw) * TAU)), 8) * (f.age > f.tw0 ? 1 : 0);
                if (f.rest)
                {
                    // lying on the floor: flattened to a sparkling smear that shrinks away
                    double L = Math.Max(1, Round(r * 1.2 * ((f.life - f.age) / 0.6)));
                    for (double i = -L; i <= L; i++) p.Px(f.x + i, -1, Math.Abs(i) == L ? C[5] : i == 0 ? WHITE : C[6]);
                    p.Glow(f.x, -1, 3 + L, C[4], 0.3);
                    continue;
                }
                Snowflake(p, f.x, f.y, r, f.rot, f.d, C, hot: Math.Max(tw, 1 - Clamp(f.age / 0.12)));
                p.Glow(f.x, f.y, 6 + r * 1.4, C[4], 0.12);
                if (tw > 0.5) Sim.Flare(p, f.x, f.y, (3 + r * 0.7) * tw, tw, C);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
