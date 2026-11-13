// The layered head, rim rays and trail light follow Sprite Fusion's open-source energy bolt and orb
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class PlasmaShot : Effect
    {
        public static readonly Module Module = new Module("plasma-shot", "pixelfx/fx/projectiles/plasma-shot", 144, 60, 108, 30, 0.8, true, 30,
            new Options
            {
                ["palette"] = new[] { "#061a1c", "#0b3b34", "#0f6a4a", "#18a05a", "#3fd46a", "#8cf57e", "#d6ffc4", "#ffffff" },
                ["size"] = 6,
                ["speed"] = 200,
                ["seed"] = 3,
            },
            (o, e) => new PlasmaShot(o))
        {
            Title = "Plasma shot",
            Desc = "A glowing plasma blob: hot knots churning in its core, a corona crackling over its skin, its tail necking into droplets that tear off, cool and wink out behind.",
            Tags = new[] { "projectile", "plasma", "energy", "sci-fi", "directional", "loop" },
            Bg = "sky",
        };

        const double D = 0.8;
        const int OX = 108;
        static readonly double[] SPIN = { 2, -3, 5 };

        sealed class Ball
        {
            public double x, y, r, h, core, k;
        }

        readonly Options o;

        PlasmaShot(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double R = o.Num("size"), V = o.Num("speed"), S = o.Seed, w = (TAU * t) / D;
            int fr = (int)Math.Floor(t * 30 + 1e-6);
            double left = -OX + 2;

            // an ion streak left in the air behind it, rippling and cooling in steps
            double T0 = -R * 3.2;
            double Tail(double x) => Px.Sin(x * 0.2 + w * 2) * (0.3 + ((T0 - x) / 44) * 1.8);
            for (double x = T0; x > T0 - 44; x -= 1)
            {
                double u = (T0 - x) / 44, y = Tail(x);
                p.Dot(x, y, u < 0.2 ? C[6] : u < 0.45 ? C[5] : C[4], u < 0.55 ? 1 : (1 - u) / 0.45);
                if (u < 0.3) p.Px(x, y + 1, u < 0.15 ? C[5] : C[4]);
            }

            // the blob as a field of balls: a body, three hot knots churning round inside it at their own speeds, a wagging
            // tail, and droplets that cling to the tail's end until the air tears them off (the skin necks as they part)
            var balls = new List<Ball> { new Ball { x = 0, y = 0, r = R, h = 1, core = 0.8 } };
            for (int i = 0; i < 3; i++)
            {
                double a = w * SPIN[i] + (i * TAU) / 3, d = R * (0.5 + 0.1 * Px.Sin(w * 3 + i * 2));
                balls.Add(new Ball { x = Px.Cos(a) * d - 0.4, y = Px.Sin(a) * d * 0.8, r = R * 0.32, h = 1, core = 1 });
            }
            // the tail: thick and hot where it leaves the body, tapering and cooling to a point, its axis still glowing
            for (int i = 1; i <= 6; i++)
            {
                double x = -R * (0.5 + i * 0.42), wag = Px.Sin(w * 2 - i * 0.9) * (0.3 + i * 0.28), r = R * (0.62 - i * 0.08);
                balls.Add(new Ball { x = x, y = wag, r = r, h = 0.96 - i * 0.05, core = Math.Sqrt((5.2 - i * 0.65) * 0.3) / r });
            }
            var drops = new List<Ball>();
            Cycle(t, D, 11, S, r => r.Range(0.34, 0.5), (age, life, r, i) =>
            {
                double k = age / life, pull = r.Range(450, 800), x = -R * 2.9 - 0.5 * pull * age * age - r.Range(0, 16) * age;
                double y = Px.Sin(w * 2 - 5.4 + age * 12) * 2 + r.Range(-30, 30) * age * (0.4 + age);
                double rad = R * r.Range(0.3, 0.48) * (1 - k * 0.6);
                drops.Add(new Ball { x = x, y = y, r = rad, h = 0.97 * (1 - k * 0.75), core = Math.Sqrt(Math.Max(0, 1 - k * 1.3) * 7 * 0.3) / rad, k = k });
            });
            var all = new List<Ball>(balls);
            all.AddRange(drops);
            // the skin comes from a compact field (so the tail tapers and droplets neck cleanly as they part); the hot
            // bands inside come only from the body and its churning knots
            p.Shade(-R * 2.6 - 60, -R * 2 - 8, R * 1.6, R * 2 + 8, (x, y, ou) =>
            {
                double f = 0, heat = 0, hh = 0;
                foreach (Ball b in all)
                {
                    double dx = x - b.x, dy = y - b.y, d2 = dx * dx + dy * dy, q = d2 / (4 * b.r * b.r);
                    if (q < 1)
                    {
                        double c = ((1 - q) * (1 - q)) / 0.5625;
                        f += c;
                        heat += c * b.h;
                    }
                    if (b.core != 0 && !double.IsNaN(b.core)) hh += (b.core * b.core * b.r * b.r) / (d2 + 0.3);
                }
                if (f < 1) return 0;
                int lvl = hh > 10 ? 7 : hh > 4.6 ? 6 : hh > 2.2 ? 5 : f > 1.4 ? 4 : 3;
                double cap = Round(2 + (heat / f) * 5.4);
                return C[(int)Math.Max(2, Math.Min(x > R * 0.3 && lvl < 5 ? lvl + 1 : lvl, cap))];
            });

            // the corona: jagged arcs leaping off the skin and back, and rays flickering round the rim, new every frame
            for (int j = 0; j < 3; j++)
            {
                Rng g = NewRng(fr, j, S);
                double a0 = g.Range(-2.3, 2.3), span = g.Range(0.45, 0.9) * g.Sign(), hgt;
                int n = 6;
                hgt = g.Range(1.5, 3.5);
                var pts = new double[n + 1][];
                for (int i = 0; i <= n; i++)
                {
                    double a = a0 + (span * i) / n, rr = R + 0.6 + hgt * Px.Sin((Math.PI * i) / n) + (i % n != 0 ? g.Range(-0.5, 0.5) : 0);
                    pts[i] = new[] { Px.Cos(a) * rr - 0.4, Px.Sin(a) * rr * 0.92 };
                }
                p.Polyline(pts, j != 0 ? C[5] : C[6]);
                p.Px(pts[3][0], pts[3][1], WHITE);
                p.Glow(pts[3][0], pts[3][1], 6, C[5], 0.4);
            }
            for (int j = 0; j < 4; j++)
            {
                Rng g = NewRng(fr, j + 9, S);
                double a = g.Range(-2.2, 2.2), l = g.Range(1.5, 4), r0 = R + 1;
                double x0 = Px.Cos(a) * r0 - 0.4, y0 = Px.Sin(a) * r0 * 0.92;
                p.Line(x0, y0, x0 + Px.Cos(a) * l, y0 + Px.Sin(a) * l, j % 2 != 0 ? C[4] : C[5]);
            }
            // motes flung off the corona, streaking back
            uint[] SP = { WHITE, C[6], C[5], C[3] };
            Beat(t, D, 14, S + 5, r => r.Range(0.12, 0.3), (age, life, r, i) =>
            {
                double a = r.Range(-2.4, 2.4), sp = r.Range(60, 130);
                var (x, y, vx, vy) = Wake(Px.Cos(a) * (R + 1), Px.Sin(a) * (R + 1), Px.Cos(a) * sp, Px.Sin(a) * sp, age, V * 0.7, 4, 0);
                if (x > left + 4 && Math.Abs(y) < 26) Spark(p, x, y, vx, vy, 1 - age / life, SP, 0.4, 0.015, 6);
            });

            // light: a big soft pool, a hot heart that pulses with the churn, a point on every droplet
            double pulse = 0.85 + 0.15 * Px.Sin(w * 4);
            p.Glow(-2, 0, 34, C[3], 0.8 * pulse);
            p.Glow(0, 0, 16, C[4], 0.35 * pulse);
            p.Glow(1, 0, 7, C[6], 0.3 * pulse);
            p.GlowLine(-R, 0, -R * 4.6, 0, 7, C[3], 0.3);
            foreach (Ball d in drops)
            {
                if (d.x < left + 4) continue;
                if (d.k < 0.6 && d.x < -R * 3.5) p.Px(d.x, d.y, d.k < 0.3 ? C[7] : C[6]);
                p.Glow(d.x, d.y, 3 + d.r * 2, C[2], 0.6 * (1 - d.k));
            }
        }
    }
}
