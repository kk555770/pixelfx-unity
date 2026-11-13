// The spark and streak recipes are adapted from Sprite Fusion's open-source impact ricochet
// (https://www.spritefusion.com/pixel-art-vfx/impact), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class BulletTracer : Effect
    {
        public static readonly Module Module = new Module("bullet-tracer", "pixelfx/fx/projectiles/bullet-tracer", 200, 40, 184, 18, 0.6, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["smoke"] = new[] { "#2e2a36", "#46404f", "#625b6b", "#817a8a", "#a39caa" },
                ["length"] = 120,
                ["speed"] = 520,
                ["seed"] = 4,
            },
            (o, e) => new BulletTracer(o))
        {
            Title = "Bullet tracer",
            Desc = "A white-hot slug tearing along, its tracer burning behind it in a long streak that sputters and cools from white through gold to red, shedding sparks and a thread of smoke.",
            Tags = new[] { "projectile", "bullet", "tracer", "gun", "directional", "loop" },
            Bg = "sky",
        };

        const double D = 0.6;
        const int OX = 184;
        static readonly string[] SLUG = { "..oyyp..", ".ypwwwwp", "ywwwwwww", ".ypwwwwp", "..oyyp.." };

        readonly Options o;

        BulletTracer(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), SM = Fit(o.Ramp("smoke"), 5);
            double S = o.Seed, L = o.Num("length"), V = o.Num("speed");
            int fr = (int)Math.Floor(t * 30 + 1e-6);
            double ph = (TAU * t) / D, left = -OX + 2;

            // a thread of smoke off the cool end of the trace, wavering as it thins
            var puffs = new List<Puff>();
            Beat(t, D, 18, S + 7, r => r.Range(0.5, 0.7), (age, life, r, i) =>
            {
                double k = age / life, x = -L * r.Range(0.45, 0.8) - age * 90, y = 0.5 + Px.Sin(age * 6 + r.Range(0, TAU)) * age * 3 - age * age * 8;
                if (x < left + 6) return;
                puffs.Add(new Puff { x = x, y = y, r = 1.2 + age * 4, a = 0.55 * (1 - k) * Math.Min(1, age * 8) * Smooth(left, left + 30, x), b = 0.95 - k * 0.4, w = 0 });
            });
            Cloud(p, puffs, SM, SM, dens: 1.2, max: 0.6);

            // the trace: a tapering body of burning gas near the slug, banded white core, gold, an orange rim; past it a
            // one-pixel line cooling to red. The compound sputters, so brightness ripples back along it, and the cold end
            // breaks into dashes
            double Heat(double d) => 1 - Px.Pow(d / L, 0.8) + Noise(d * 0.12 - t * 40, S) * 0.12 + 0.08 * Px.Sin(d * 0.35 - ph * 9);
            double YAt(double d) => 0.5 + Px.Sin(d * 0.09 + ph * 2) * (d / L) * 0.8;
            double HOT = L * 0.32;
            p.Shade(-HOT - 1, -4, 1, 5, (x, y, ou) =>
            {
                double d = -x, u = d / HOT;
                if (u < 0 || u > 1) return 0;
                double w = 0.7 + 2.3 * Px.Pow(1 - u, 1.4) * (0.9 + 0.1 * Px.Sin(d * 0.8 - ph * 12)), v = Math.Abs(y - YAt(d)) / w;
                if (v > 1) return 0;
                double h = Heat(d);
                return v < 0.38 ? (h > 0.8 ? WHITE : F[7]) : v < 0.7 ? (h > 0.75 ? F[7] : h > 0.6 ? F[6] : F[5]) : u < 0.5 ? F[5] : F[4];
            });
            for (double d = HOT; d <= L; d += 0.5)
            {
                double u = d / L, h = Heat(d);
                if (u > 0.55 && Rand(Math.Floor(d / 3), fr, S) < (u - 0.55) * 1.6) continue;
                uint c = h > 0.55 ? F[6] : h > 0.42 ? F[5] : h > 0.3 ? F[4] : h > 0.18 ? F[3] : h > 0.08 ? F[2] : F[1];
                p.Dot(-d, YAt(d), c, u < 0.85 ? 1 : (1 - u) / 0.15);
            }
            p.GlowLine(-2, 1, -L * 0.45, 1, 6, F[4], 0.35);
            p.GlowLine(-2, 1, -L * 0.2, 1, 3, F[7], 0.3);

            // sparks of burning compound shed off the trace, dropping and cooling as they fall behind
            uint[] SP = { F[7], F[6], F[4], F[3] };
            Beat(t, D, 16, S + 2, r => r.Range(0.1, 0.28), (age, life, r, i) =>
            {
                var (x, y, vx, vy) = Wake(-r.Range(4, L * 0.5), 0.5 + r.Range(-0.5, 1), -r.Range(10, 80), r.Range(-60, 50), age, V * 0.35, 3, 260);
                if (x > left + 8) Spark(p, x, y, vx, vy, 1 - age / life, SP, 0.45, 0.012, 6);
            });

            // the slug: a white-hot pointed round, pale-rimmed, its nose a point of light
            p.Sprite(SLUG, new Dictionary<char, uint> { ['w'] = WHITE, ['p'] = F[7], ['y'] = F[6], ['o'] = F[5] }, -1, -1);
            p.Glow(2, 1, 24, F[4], 0.5);
            p.Glow(3, 1, 8, WHITE, 0.55);
        }
    }
}
