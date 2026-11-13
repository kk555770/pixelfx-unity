// The glints (a cross of rays with a white heart and soft light, little twinkling stars around it) are adapted from
// Sprite Fusion's open-source sparkle (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WeatherLib;

namespace Pixelfx
{
    public sealed class ShootingStars : Effect
    {
        public static readonly Module Module = new Module("shooting-stars", "pixelfx/fx/weather/shooting-stars", 320, 180, 160, 180, 10, true, 30,
            new Options
            {
                ["palette"] = new[] { "#3a1e5a", "#7a3a8a", "#e0607a", "#ffa86a", "#ffe6a8", "#ffffff" },
                ["sky"] = new[] { "#141428", "#2a2a4a", "#4c4c7c", "#8e8ec4", "#dcdcff" },
                ["seed"] = 4,
            },
            (o, e) => new ShootingStars(o))
        {
            Title = "Shooting stars",
            Desc = "Meteors burn across a starry sky, white heads trailing gold and rose; the big ones flare and break up, and each leaves a glowing train that twists and fades.",
            Tags = new[] { "weather", "meteor", "shooting star", "night", "sky", "stars", "ambient", "loop" },
            Bg = "sky",
        };

        const double D = 10, TRAIN = 1.6;

        sealed class Meteor
        {
            public double t0, x, y, a, v, life, s;
            public bool flare;
        }

        // the meteors: when, where they start, which way, how fast, how long they burn, how big; the last flares
        static readonly Meteor[] METEORS =
        {
            new Meteor { t0 = 0.5, x = 60, y = -170, a = 2.45, v = 260, life = 0.55, s = 0.6 },
            new Meteor { t0 = 1.9, x = -40, y = -176, a = 2.2, v = 220, life = 0.7, s = 0.8 },
            new Meteor { t0 = 3.4, x = 150, y = -150, a = 2.62, v = 300, life = 0.45, s = 0.5 },
            new Meteor { t0 = 4.6, x = 130, y = -178, a = 2.35, v = 240, life = 1.05, s = 1.4, flare = true },
            new Meteor { t0 = 6.7, x = -90, y = -168, a = 2.05, v = 200, life = 0.6, s = 0.7 },
            new Meteor { t0 = 7.9, x = 20, y = -160, a = 2.5, v = 280, life = 0.5, s = 0.6 },
            new Meteor { t0 = 9.2, x = -10, y = -176, a = 2.28, v = 230, life = 0.8, s = 1, flare = true },
        };

        readonly Options o;

        ShootingStars(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            var q = new Clip(p, Module);
            double S = o.Seed;
            uint[] C = Fit(o.Ramp("palette"), 6), K = Fit(o.Ramp("sky"), 5);
            Sky(p, q, t, S, K);
            for (int i = 0; i < METEORS.Length; i++) DrawMeteor(q, Wrap(t - METEORS[i].t0, D), METEORS[i], i, S, C);
        }

        // the starfield: a faint milky band across the sky, dim specks, steady stars, a few that twinkle with a glint
        static void Sky(Painter p, Clip q, double t, double S, uint[] K)
        {
            Rows(p, Module, -180, -1, (y, R) =>
            {
                for (int i = 0; i < 320; i++)
                {
                    double x = i - 160 + 0.5, d = Math.Abs((y + 110) - (x * -0.32)) / 36;
                    if (d >= 1) continue;
                    double k = (1 - d) * (1 - d);
                    R.l[i] = K[2];
                    R.k[i] = (float)(0.1 * k);
                    if (Rand(i, Math.Floor(y), S, 5) < 0.05 * k)
                    {
                        R.c[i] = K[2];
                        R.a[i] = 0.7f;
                    }
                }
            });
            for (int i = 0; i < 130; i++)
            {
                double x = -160 + Rand(S, i, 1) * 320, y = -180 + Rand(S, i, 2) * 178, r = Rand(S, i, 3), c = 1 + Math.Floor(Rand(S, i, 4) * 4);
                double tw = 0.5 + 0.5 * Px.Sin((TAU * c * t) / D + Rand(S, i, 5) * TAU);
                if (r < 0.6) q.Dot(x, y, K[3], 0.35 + 0.5 * tw);
                else if (r < 0.94)
                {
                    q.Px(x, y, r < 0.8 ? K[4] : WHITE);
                    q.Glow(x + 0.5, y + 0.5, 3, K[3], 0.22 * tw);
                }
                else Sparkle(q, x, y, 1 + Round(tw * 2), K[4], K[3], 0.3 * tw);
            }
        }

        // one meteor, age seconds after it lights: a head that brightens and burns out, a trail cooling down the ramp
        // behind it, a flare and fragments for the big ones, and the train it leaves glowing and twisting as it fades
        static void DrawMeteor(Clip q, double age, Meteor m, int i, double S, uint[] C)
        {
            double ca = Px.Cos(m.a), sa = Px.Sin(m.a), end = m.life + (m.flare ? 0.5 : 0) + TRAIN;
            if (age > end) return;
            (double, double) Pos(double tt) => (m.x + ca * m.v * tt, m.y + sa * m.v * tt);
            double burn = Math.Min(age, m.life);
            var (hx, hy) = Pos(burn);
            // the train: the whole path it has burned, lingering, widening, drifting and twisting
            double trainK = age < m.life ? 1 : 1 - (age - m.life) / (TRAIN + (m.flare ? 0.5 : 0));
            if (trainK > 0)
            {
                double len = m.v * burn, nx = -sa, ny = ca;
                for (double d = 0; d < len; d += 1)
                {
                    double passed = age - d / m.v;
                    if (passed < 0.08) continue;
                    double g = trainK * Math.Min(1, (passed - 0.08) * 4) * (0.35 + 0.65 * Px.Sin((Math.PI * d) / len)) * m.s, tw = Px.Sin(d * 0.09 + passed * 1.7) * Math.Min(4, passed * 3);
                    double x = m.x + ca * d + nx * tw + passed * 4, y = m.y + sa * d + ny * tw;
                    q.Dot(x, y, passed < 0.6 ? C[2] : C[1], g * 0.75);
                    if (d % 6 == 0) q.Glow(x, y, 6 + passed * 4, C[1], 0.12 * g);
                }
            }
            if (age > m.life + (m.flare ? 0.5 : 0)) return;
            // the head brightens fast, holds, and burns out; the big ones flare as they go
            double u = age / m.life, k = age < m.life ? Math.Min(1, u * 6) * (u > 0.75 ? 1 - (u - 0.75) / 0.25 : 1) : 0;
            if (k > 0)
            {
                double L = (24 + 40 * m.s) * Math.Min(1, u * 3);
                uint[] cool = new uint[C.Length - 1];
                for (int n = 0; n < cool.Length; n++) cool[n] = C[C.Length - 1 - n];
                for (double d = 0; d <= L; d += 0.5)
                {
                    double v = d / L, x = hx - ca * d, y = hy - sa * d;
                    q.Dot(x, y, v < 0.08 ? WHITE : At(cool, v * 0.95), k * (v < 0.6 ? 1 : (1 - v) / 0.4));
                    if (m.s > 0.9 && v < 0.4) q.Dot(x - sa * 0.9, y + ca * 0.9, v < 0.15 ? C[4] : C[3], k * (1 - v / 0.4));
                }
                q.Px(hx, hy, WHITE);
                if (m.s > 0.9)
                {
                    q.Px(hx + 1, hy, WHITE);
                    q.Px(hx, hy + 1, C[4]);
                }
                q.Glow(hx, hy, 8 + 10 * m.s, C[4], 0.45 * k);
                q.Glow(hx - ca * L * 0.3, hy - sa * L * 0.3, 10 + 8 * m.s, C[3], 0.2 * k);
            }
            if (!m.flare || age < m.life * 0.82) return;
            // the flare: a burst of light and rays, fragments carrying on and fading
            double f = (age - m.life * 0.82) / (m.life * 0.18 + 0.5);
            var (fx, fy) = Pos(m.life * 0.86);
            if (f < 0.35)
            {
                Sparkle(q, fx, fy, 9 * Px.Sin((Math.PI * f) / 0.35) + 2, C[4], C[3], 0.6);
                q.Glow(fx, fy, 40, C[3], 0.5 * (1 - f / 0.35));
            }
            // little stars scattered round the burst, each twinkling once and gone
            for (int j = 0; j < 7; j++)
            {
                double ang = Rand(S, i, j, 5) * TAU, r = 5 + Rand(S, i, j, 6) * 16, d0 = Rand(S, i, j, 7) * 0.3, g = (f - d0) / 0.45;
                if (g <= 0 || g >= 1) continue;
                double sx = fx + Px.Cos(ang) * r * (0.6 + g * 0.5), sy = fy + Px.Sin(ang) * r * (0.6 + g * 0.5) - g * 3, arm = Round(2.5 * Px.Sin(Math.PI * g));
                if (arm >= 1) Sparkle(q, sx, sy, arm, j % 3 != 0 ? C[4] : WHITE, C[3], 0.3 * (1 - g));
                else q.Px(sx, sy, C[4]);
            }
            for (int j = 0; j < 4; j++)
            {
                double da = (Rand(S, i, j, 1) - 0.5) * 0.5, sp = m.v * (0.5 + Rand(S, i, j, 2) * 0.4), tt = f * (m.life * 0.18 + 0.5), g = 1 - f;
                double x = fx + Px.Cos(m.a + da) * sp * tt, y = fy + Px.Sin(m.a + da) * sp * tt;
                if (g <= 0) continue;
                q.Line(x - Px.Cos(m.a + da) * 5 * g, y - Px.Sin(m.a + da) * 5 * g, x, y, C[3], g);
                q.Px(x, y, g > 0.5 ? WHITE : C[4]);
                q.Glow(x, y, 5, C[3], 0.35 * g);
            }
        }
    }
}
