// The flare, the twinkling glitter and the crackle are adapted from Sprite Fusion's open-source sparkle
// (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class FireworkRocket : Effect
    {
        const int W = 300, H = 300, OX = 150, OY = 288;

        public static readonly Module Module = new Module("firework-rocket", "pixelfx/fx/explosions/firework-rocket", W, H, OX, OY, 3.8, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1406", "#6a3a0c", "#b8701a", "#f0a830", "#ffd860", "#fff2b0", "#ffffff" },
                ["tips"] = new[] { "#0c2a3a", "#1a5a78", "#36a2c8", "#7cd8f0", "#c8f4ff", "#ffffff" },
                ["seed"] = 12,
            },
            (o, e) => new FireworkRocket(o, e))
        {
            Title = "Firework rocket",
            Desc = "A rocket fizzes off the ground on a sparkling gold trail, coasts, and bursts into a palm: heavy gold fronds arc out, droop on glittering trails and crackle at the tips.",
            Tags = new[] { "firework", "rocket", "celebration", "sparkle", "sky", "one-shot" },
            Bg = "sky",
        };

        const double FLY = 1.05, HIGH = 192, DRAG = 1.35, G = 105, TRAIL = 1.15;
        static readonly uint SMOKE = Hex("#4a4458"), PUFF = Hex("#5a5468"), STICK = Hex("#5a4030"), TUBE = Hex("#b8303a"), TUBE_LIT = Hex("#e86070"), CAP = Hex("#d8d0c0");
        static readonly int[][] TIPS = { new[] { -1, 0 }, new[] { 2, 0 }, new[] { 0, -1 }, new[] { 0, 2 } };

        sealed class Frond
        {
            public double x, y, vx, vy, age, life, acc, seed;
            public readonly List<double> pts = new List<double>();
        }

        sealed class Pop
        {
            public double x, y, t0, life;
            public uint c;
        }

        readonly uint[] C, T;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Frond> fronds = new List<Frond>();
        readonly List<Pop> pops = new List<Pop>();
        double t, bx, by, acc;
        bool burst;

        static (double x, double y) At(double t) => (Px.Sin(t * 8) * 1.6 * (t / FLY), -8 - HIGH * Ease.OutQuad(Clamp(t / FLY)));

        FireworkRocket(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            T = Fit(o.Ramp("tips"), 6);
            R = new Rng(o.Seed);
            ps = new Particles(rng: R, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            lights = new Lights(e);
            lights.Add(0, -14, 24, C[4], 0.7, 0.35);
            // ignition: a puff of smoke and a spray of sparks off the ground
            for (int i = 0; i < 18; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(60, 180);
                ps.Spawn(P.Spark, 0, -2, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.6, R.Range(0.15, 0.4));
            }
            for (int i = 0; i < 5; i++) ps.Spawn(P.Vapour, R.Range(-3, 3), -2, R.Range(-20, 20), -R.Range(2, 10), R.Range(0.6, 1), PUFF, R.Range(3, 5));
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < FLY)
            {
                // the climb: a hot tail spitting gold sparks, glitter left hanging, a thread of smoke
                var (x, y) = At(t);
                var (x2, y2) = At(Math.Max(0, t - 0.02));
                double vy = (y - y2) / 0.02, vx = (x - x2) / 0.02;
                for (acc += dt * 90; acc > 1; acc--)
                {
                    double a = Math.PI / 2 + R.Range(-0.45, 0.45), sp = R.Range(30, 110);
                    ps.Spawn(P.Spark, x, y + 4, Px.Cos(a) * sp + vx * 0.2, Px.Sin(a) * sp + vy * 0.15, R.Range(0.15, 0.4));
                    if (R.Chance(0.25)) ps.Spawn(P.Star, x + R.Range(-1.5, 1.5), y + R.Range(2, 6), R.Range(-6, 6), R.Range(5, 20), R.Range(0.4, 0.9), R.Chance(0.5) ? C[5] : WHITE, R.Chance(0.15) ? 1.2 : 0.5);
                    if (R.Chance(0.18)) ps.Spawn(P.Vapour, x, y + 5, R.Range(-4, 4), R.Range(4, 12), R.Range(0.6, 1.1), SMOKE, R.Range(1.2, 2.2));
                }
            }
            else if (!burst)
            {
                burst = true;
                (bx, by) = At(FLY);
                lights.Add(bx, by, 82, C[4], 0.8, 1.6);
                lights.Add(bx, by, 40, WHITE, 0.6, 0.08);
                // the palm: heavy comets thrown up and out, a few low ones to the sides
                for (int i = 0; i < 9; i++)
                {
                    double a = -Math.PI / 2 + ((i - 4) / 4.0) * 1.4 + R.Range(-0.1, 0.1), sp = R.Range(180, 210) * (1 - 0.12 * Math.Abs(i - 4) / 4);
                    fronds.Add(new Frond { x = bx, y = by, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, age = 0, life = R.Range(1.8, 2.15), acc = R.Next(), seed = R.Range(0, 100) });
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Vapour, bx + R.Range(-4, 4), by + R.Range(-4, 4), R.Range(-20, 20), R.Range(-15, 15), R.Range(1.8, 2.6), SMOKE, R.Range(6, 10));
                for (int i = 0; i < 30; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(40, 160);
                    ps.Spawn(P.Spark, bx, by, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.15, 0.35));
                }
            }
            for (int i = fronds.Count - 1; i >= 0; i--)
            {
                Frond f = fronds[i];
                f.age += dt;
                double k = f.age / f.life;
                if (k >= 1)
                {
                    for (int j = 0; j < 4; j++) pops.Add(new Pop { x = f.x + R.Range(-5, 5), y = f.y + R.Range(-5, 5), t0 = t + R.Range(0, 0.2), life = R.Range(0.06, 0.12), c = R.Chance(0.5) ? WHITE : T[3] });
                    fronds.RemoveAt(i);
                    continue;
                }
                f.vx -= f.vx * DRAG * dt;
                f.vy -= f.vy * DRAG * dt - G * dt;
                f.x += f.vx * dt;
                f.y += f.vy * dt;
                f.pts.Add(f.x);
                f.pts.Add(f.y);
                f.pts.Add(t);
                while (f.pts.Count > 3 && t - f.pts[2] > TRAIL) f.pts.RemoveRange(0, 3);
                // glitter shed all along: specks that hang, twinkle and drift down
                for (f.acc += dt * 34; f.acc > 1; f.acc--)
                    ps.Spawn(P.Star, f.x + R.Range(-1, 1), f.y + R.Range(-1, 1), f.vx * 0.1 + R.Range(-5, 5), R.Range(0, 14), R.Range(0.5, 1.1), R.Chance(0.6) ? C[5] : WHITE, R.Chance(0.12) ? R.Range(1, 1.5) : 0.5);
            }
            for (int i = pops.Count - 1; i >= 0; i--)
                if (t > pops[i].t0 + pops[i].life) pops.RemoveAt(i);
            ps.Update(dt);
            Cull(ps, -OX + 2, -OY + 2, W - OX - 2, H - OY - 2);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p) => ps.Draw(p, Layer.Back);

        public override void Draw(Painter p)
        {
            if (t < FLY)
            {
                // the rocket: a little tube on a stick, a white-hot tail flickering under it
                var (x, y) = At(t);
                p.Line(x, y + 4, x, y + 12, STICK);
                p.Rect(x - 1, y - 3, 3, 7, TUBE);
                p.Px(x - 1, y - 3, TUBE_LIT);
                p.Px(x, y - 4, CAP);
                double fl = 2 + (Px.Sin(t * 70) > 0 ? 1 : 0);
                p.Line(x, y + 4, x, y + 4 + fl, u => u < 0.4 ? WHITE : C[5]);
                p.Glow(x, y + 5, 9, C[4], 0.6);
            }
            foreach (Frond f in fronds)
            {
                double k = f.age / f.life;
                List<double> P2 = f.pts;
                int n = P2.Count / 3;
                // the frond: a thick gold trail, hot near the head, cooling and dithering out behind
                for (int j = n - 1; j > 0; j--)
                {
                    double u = (t - P2[j * 3 + 2]) / TRAIL;
                    if (u >= 1) break;
                    uint c = u < 0.08 ? C[6] : u < 0.25 ? C[5] : u < 0.5 ? C[4] : u < 0.75 ? C[3] : C[2];
                    double a = Px.Pow(1 - u, 0.6) * (1.25 - k * 0.45);
                    p.Line(P2[j * 3], P2[j * 3 + 1], P2[j * 3 - 3], P2[j * 3 - 2], c, a);
                    if (u < 0.7) p.Line(P2[j * 3] + 1, P2[j * 3 + 1], P2[j * 3 - 3] + 1, P2[j * 3 - 2], u < 0.25 ? C[5] : u < 0.5 ? C[3] : C[2], a * 0.85);
                    if (u < 0.35) p.Line(P2[j * 3], P2[j * 3 + 1] + 1, P2[j * 3 - 3], P2[j * 3 - 2] + 1, C[3], a * 0.6);
                }
                if (k > 0.8 && Px.Sin(t * 55 + f.seed) < 0) continue;
                p.Rect(f.x - 0.5, f.y - 0.5, 2, 2, k < 0.5 ? WHITE : C[6]);
                if (k < 0.6)
                    foreach (int[] d in TIPS) p.Px(f.x + d[0], f.y + d[1], C[5]);
                p.Glow(f.x, f.y, 10, C[4], 0.6 * (1 - k * 0.5));
            }
            foreach (Pop q in pops)
            {
                double u = (t - q.t0) / q.life;
                if (u < 0 || u > 1) continue;
                Sim.Sparkle(p, q.x, q.y, 2.8 * Px.Sin(Math.PI * u), q.c, T[2], 0.5);
            }
            // the burst: a white heart for two frames, a flare turning
            double b = t - FLY;
            if (b >= 0 && b < 0.26)
            {
                double u = b / 0.26, L = 30 * Px.Pow(1 - u, 1.3);
                for (int i = 0; i < 8; i++)
                {
                    bool diag = i % 2 == 1;
                    double an = u * 0.5 + (i * TAU) / 8, len = diag ? L * 0.45 : L, ca = Px.Cos(an), sa = Px.Sin(an);
                    for (double d = 0; d <= len; d += 0.5)
                    {
                        double v = d / len;
                        p.Dot(bx + ca * d, by + sa * d, v < 0.3 ? WHITE : v < 0.65 ? C[5] : C[4], v < 0.7 ? 1 : 1 - (v - 0.7) / 0.35);
                    }
                }
                if (b < 0.07) p.Circle(bx, by, 2.5, WHITE);
                p.Glow(bx, by, 4 + L * 1.2, C[4], 0.35);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
