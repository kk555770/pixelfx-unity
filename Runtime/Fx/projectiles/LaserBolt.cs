// The banded body, travelling pulses and spiralling sparks follow Sprite Fusion's open-source energy beam
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class LaserBolt : Effect
    {
        public static readonly Module Module = new Module("laser-bolt", "pixelfx/fx/projectiles/laser-bolt", 128, 32, 108, 16, 0.5, true, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0610", "#5c0d1c", "#9a1428", "#d8243a", "#ff4d5e", "#ff8a94", "#ffd0d4", "#ffffff" },
                ["length"] = 40,
                ["seed"] = 2,
            },
            (o, e) => new LaserBolt(o))
        {
            Title = "Laser bolt",
            Desc = "A red laser bolt: a white-hot core in a rounded head, red bands tapering back, pulses racing down it, a tail that shimmers apart into wavering dashes.",
            Tags = new[] { "projectile", "laser", "energy", "sci-fi", "directional", "loop" },
            Bg = "sky",
        };

        const double D = 0.5;
        const int OX = 108;

        readonly Options o;

        LaserBolt(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double L = o.Num("length"), S = o.Seed, ph = t / D;
            int fr = (int)Math.Floor(t * 30 + 1e-6);
            double left = -OX + 2;

            // the shimmering tail: dashes shed off the bolt's end, wavering, flickering and cooling as they fall behind
            Beat(t, D, 14, S, r => r.Range(0.18, 0.3), (age, life, r, i) =>
            {
                double k = age / life, x0 = -L * 0.8 - age * r.Range(150, 230), len = r.Range(3, 7) * (1 - k * 0.5), y = 0.5 + Px.Sin(x0 * 0.25 + ph * TAU * 2) * (0.5 + k * 1.5) + r.Range(-0.6, 0.6) * k;
                if (x0 - len < left || Rand(fr, r.Int(0, 999)) < k * 0.5) return;
                p.Line(x0, y, x0 - len, y, k < 0.3 ? C[5] : k < 0.6 ? C[4] : C[3]);
                if (k < 0.4) p.Glow(x0 - len / 2, y, 4, C[4], 0.3 * (1 - k));
            });

            // the body: bands round a white core, thickest just behind the rounded head and tapering to a thread, a
            // pulse running back down it every so often; the edge breaks up a little as the light shimmers
            p.Shade(-L - 1, -5, 4, 6, (x, y, ou) =>
            {
                double d = -x + 3, u = d / (L + 3);
                if (u < 0 || u > 1) return 0;
                double hq = (3 - d) / 3;
                double head = d < 3 ? Math.Sqrt(Math.Max(0, 1 - Px.Pow(hq, 2))) : 1;
                double w = (3.4 * Px.Pow(1 - u, 0.9) + 0.5) * head * (1 + 0.08 * Px.Sin(d * 0.9 - ph * TAU * 4)), v = Math.Abs(y - 0.5) / w;
                if (v > 1.2) return 0;
                if (v > 1 && (u > 0.5 || Rand(Math.Floor(x), fr) > 0.6)) return 0;
                int pulse = ((d * 0.05 - ph * 3) % 1 + 1) % 1 < 0.12 ? 1 : 0;
                int lvl = v < 0.22 ? (u < 0.6 ? 7 : 6) : v < 0.45 ? 6 - (u > 0.5 ? 1 : 0) : v < 0.72 ? 4 : v <= 1 ? 3 : 2;
                return C[Math.Min(7, lvl + (v < 0.72 ? pulse : 0))];
            });
            // sparks spiralling round the core, a bright one on the near side and a dim one behind
            foreach (double s in new[] { 0, Math.PI })
            {
                for (double d = 2; d < L * 0.7; d += 1)
                {
                    double a = d * 0.45 - ph * TAU * 3 + s, y = 0.5 + Px.Sin(a) * (2.2 * (1 - d / L) + 0.6);
                    if (Px.Cos(a) > 0.3) p.Dot(-d, y, Px.Cos(a) > 0.8 ? WHITE : C[6]);
                }
            }
            // the head's glint, flickering, and the light
            double g = 0.75 + 0.25 * Px.Sin(ph * TAU * 2);
            Sim.Sparkle(p, 1, 0.5, 3 + Round(g * 2), C[6], C[4], 0.5);
            p.GlowLine(1, 0.5, -L, 0.5, 9, C[3], 0.4);
            p.Glow(-2, 0.5, 20, C[3], 0.5 * g);
            p.Glow(1, 0.5, 6, WHITE, 0.25);
        }
    }
}
