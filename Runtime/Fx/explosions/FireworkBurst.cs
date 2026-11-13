// The flare, the twinkling crosses and the glitter are adapted from Sprite Fusion's open-source sparkle
// (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class FireworkBurst : Effect
    {
        public static readonly Module Module = new Module("firework-burst", "pixelfx/fx/explosions/firework-burst", 240, 248, 120, 100, 3.6, false, 30,
            new Options
            {
                ["palette"] = new[] { "#3d0a33", "#701560", "#b0228f", "#f53cd1", "#ff86e6", "#ffd2f6", "#ffffff" },
                ["core"] = new[] { "#4a2c08", "#86520c", "#c48412", "#f0b424", "#ffe066", "#fff6c4", "#ffffff" },
                ["seed"] = 9,
            },
            (o, e) => new FireworkBurst(o, e))
        {
            Title = "Firework burst",
            Desc = "A shell climbs, slows and bursts into a sphere of coloured trailing stars round a gold core; they droop, crackle into white pops and fade, leaving glitter.",
            Tags = new[] { "firework", "celebration", "sparkle", "sky", "burst", "one-shot" },
            Bg = "sky",
        };

        const double RISE = 0.42, Y0 = 72, DRAG = 2.6, G = 88, TRAIL = 0.34, TURN = 0.22;
        static readonly uint SMOKE = Hex("#4a4458");
        static readonly int[][] CROSS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        sealed class Star
        {
            public double x, y, vx, vy, life, age, acc, seed;
            public uint[] ramp, first;
            public bool big;
            public readonly List<double> pts = new List<double>();
        }

        sealed class Pop
        {
            public double x, y, t0, life;
            public uint c;
        }

        readonly uint[] C, K;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Star> stars = new List<Star>();
        readonly List<Pop> pops = new List<Pop>();
        double t;
        bool burst;

        FireworkBurst(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            K = Fit(o.Ramp("core"), 7);
            R = new Rng(o.Seed);
            ps = new Particles(rng: R, sparks: new[] { K[6], K[5], K[4], K[3] }, emitter: e);
            lights = new Lights(e);
        }

        // a shell of stars thrown over a sphere: seen side-on, the ones flying at us or away look slower
        // the outer stars burn gold for a beat, then turn to their color
        void Shell(int n, double speed, uint[] ramp, double life0, double life1, uint[] first)
        {
            for (int i = 0; i < n; i++)
            {
                double z = R.Range(-1, 1), a = R.Range(0, TAU), s = Math.Sqrt(1 - z * z), v = speed * R.Range(0.93, 1.05);
                stars.Add(new Star
                {
                    x = 0, y = 0, vx = Px.Cos(a) * s * v, vy = Px.Sin(a) * s * v, life = R.Range(life0, life1), age = 0, ramp = ramp, first = first, big = R.Chance(0.2),
                    acc = R.Next(), seed = R.Range(0, 100),
                });
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the shell climbs, slowing, shedding a fizz of gold sparks
            if (t < RISE)
            {
                double y = Y0 * (1 - Ease.OutCubic(t / RISE));
                if (R.Chance(50 * dt)) ps.Spawn(P.Spark, R.Range(-0.5, 0.5), y + 2, R.Range(-25, 25), R.Range(20, 70), R.Range(0.15, 0.35));
                if (R.Chance(14 * dt)) ps.Spawn(P.Vapour, R.Range(-1, 1), y + 3, R.Range(-3, 3), R.Range(5, 15), R.Range(0.5, 0.8), SMOKE, R.Range(1.5, 2.5));
            }
            if (!burst && t >= RISE)
            {
                burst = true;
                lights.Add(0, 0, 110, C[4], 0.6, 1.5);
                lights.Add(0, 0, 30, WHITE, 0.5, 0.07);
                Shell(66, 236, C, 1.35, 1.9, K);
                Shell(26, 120, K, 1.0, 1.4, K);
                // the burst charge's smoke, drifting where the shell broke
                for (int i = 0; i < 7; i++)
                {
                    double a = R.Range(0, TAU), d = R.Range(0, 8);
                    ps.Spawn(P.Vapour, Px.Cos(a) * d, Px.Sin(a) * d, Px.Cos(a) * R.Range(10, 30), Px.Sin(a) * R.Range(10, 30), R.Range(2.2, 3), SMOKE, R.Range(6, 11));
                }
                for (int i = 0; i < 24; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(40, 140);
                    ps.Spawn(P.Spark, 0, 0, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.15, 0.35));
                }
            }
            for (int i = stars.Count - 1; i >= 0; i--)
            {
                Star s = stars[i];
                s.age += dt;
                double k = s.age / s.life;
                if (k >= 1)
                {
                    // the star crackles out in a few white pops
                    for (int j = 0; j < 2 + (s.big ? 1 : 0); j++)
                        pops.Add(new Pop { x = s.x + R.Range(-3, 3), y = s.y + R.Range(-3, 3), t0 = t + R.Range(0, 0.18), life = R.Range(0.06, 0.12), c = R.Chance(0.6) ? WHITE : s.ramp[5] });
                    stars.RemoveAt(i);
                    continue;
                }
                s.vx -= s.vx * DRAG * dt;
                s.vy -= s.vy * DRAG * dt - G * dt;
                s.x += s.vx * dt;
                s.y += s.vy * dt;
                s.pts.Add(s.x);
                s.pts.Add(s.y);
                s.pts.Add(t);
                while (s.pts.Count > 3 && t - s.pts[2] > TRAIL) s.pts.RemoveRange(0, 3);
                // glitter shed along the way: specks that hang, twinkle and fall
                for (s.acc += dt * (k < 0.25 ? 4 : 11); s.acc > 1; s.acc--)
                    ps.Spawn(P.Star, s.x, s.y, s.vx * 0.15 + R.Range(-6, 6), s.vy * 0.15 + R.Range(0, 10), R.Range(0.35, 0.8), R.Chance(0.5) ? K[5] : WHITE, R.Chance(0.15) ? R.Range(1, 1.6) : 0.5);
            }
            for (int i = pops.Count - 1; i >= 0; i--)
                if (t > pops[i].t0 + pops[i].life) pops.RemoveAt(i);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p) => ps.Draw(p, Layer.Back);

        public override void Draw(Painter p)
        {
            if (t < RISE)
            {
                // the shell: a hot dot with a short tail
                double u = t / RISE, y = Y0 * (1 - Ease.OutCubic(u));
                p.Line(0, y, 0, y + 3 + 6 * (1 - u), q => q < 0.3 ? K[5] : K[3]);
                p.Px(0, y, WHITE);
                p.Glow(0, y, 7, K[4], 0.5);
            }
            foreach (Star s in stars)
            {
                double k = s.age / s.life;
                List<double> P2 = s.pts;
                int n = P2.Count / 3;
                // the trail: a 1 px line from the head back along where it has been, cooling and dithering out; the part
                // laid while the star burned gold stays gold
                for (int j = n - 1; j > 0; j--)
                {
                    double u = (t - P2[j * 3 + 2]) / TRAIL;
                    if (u >= 1) break;
                    uint[] tr = s.age - (t - P2[j * 3 + 2]) < TURN ? s.first : s.ramp;
                    uint c = u < 0.3 ? tr[5] : u < 0.6 ? tr[4] : tr[3];
                    p.Line(P2[j * 3], P2[j * 3 + 1], P2[j * 3 - 3], P2[j * 3 - 2], c, Px.Pow(1 - u, 0.8) * (1.3 - k * 0.6));
                }
                // the head: white-hot, then its color; late on it flickers before it crackles out
                if (k > 0.72 && Px.Sin(t * 60 + s.seed) < -0.2) continue;
                // white only on the burst frame; the glow comes in as the stars spread, so the heart never blooms into a blob
                uint[] ramp = s.age < TURN ? s.first : s.ramp;
                uint head = s.age < 0.04 ? WHITE : k < 0.55 ? ramp[s.age < TURN ? 5 : 6] : ramp[5];
                p.Px(s.x, s.y, head);
                if (s.big && k < 0.6 && s.age > 0.06)
                    foreach (int[] d in CROSS) p.Px(s.x + d[0], s.y + d[1], ramp[5]);
                p.Glow(s.x, s.y, s.big ? 7 : 5, ramp[4], (s.big ? 0.7 : 0.5) * (1 - k * 0.6) * Math.Min(1, s.age / 0.2));
            }
            foreach (Pop q in pops)
            {
                double u = (t - q.t0) / q.life;
                if (u < 0 || u > 1) continue;
                Sim.Sparkle(p, q.x, q.y, 2.6 * Px.Sin(Math.PI * u), q.c, Mix(q.c, C[3], 0.5), 0.5);
            }
            // the burst: a flare and a white heart, the sky lit up behind it
            double b = t - RISE;
            if (b >= 0 && b < 0.28)
            {
                double u = b / 0.28, L = 32 * Px.Pow(1 - u, 1.3);
                FlareRays(p, 0, 0, L, u * 0.6, C, K);
                if (b < 0.07) p.Circle(0, 0, 2.6 - b * 20, WHITE);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }

        // an 8-ray flare: long rays on the axes with a white core and tinted ends, short gold diagonals, turning slowly
        static void FlareRays(Painter p, double x, double y, double L, double ang, uint[] C, uint[] K)
        {
            if (L < 1) return;
            for (int i = 0; i < 8; i++)
            {
                bool diag = i % 2 == 1;
                double an = ang + (i * TAU) / 8, len = diag ? L * 0.42 : L, ca = Px.Cos(an), sa = Px.Sin(an);
                for (double d = 0; d <= len; d += 0.5)
                {
                    double u = d / len;
                    p.Dot(x + ca * d, y + sa * d, diag ? (u < 0.5 ? K[5] : K[4]) : u < 0.3 ? WHITE : u < 0.65 ? C[5] : C[4], u < 0.7 ? 1 : 1 - (u - 0.7) / 0.35);
                }
            }
            p.Glow(x, y, 4 + L * 1.2, C[4], 0.3);
            p.Glow(x, y, 2 + L * 0.3, WHITE, 0.25);
        }
    }
}
