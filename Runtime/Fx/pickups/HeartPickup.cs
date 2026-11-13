// The hover, floor shadow and glints follow Sprite Fusion's open-source pickup
// (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class HeartPickup : Effect
    {
        const double DURATION = 1.2, HOVER = 18, W = 7.2;
        const double LUB = 0.04, DUB = 0.24; // the two beats, as loop phase
        static readonly uint INK = Hex("#12060c");
        static readonly string[] MINI = { ".a.a.", "abbba", ".aba.", "..a.." };

        public static readonly Module Module = new Module("heart-pickup", "pixelfx/fx/pickups/heart-pickup", 56, 60, 28, 52, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0408", "#4a0a14", "#8a1222", "#d02436", "#ff4a56", "#ff8a8e", "#ffd0cc", "#ffffff" },
                ["seed"] = 4,
            },
            (o, e) => new HeartPickup(o, e))
        {
            Title = "Heart pickup",
            Desc = "A plump glossy heart hovering over its shadow beats lub-dub: a big squash and stretch, a smaller echo, light and a thin ring pulsing out, tiny hearts drifting off.",
            Tags = new[] { "pickup", "heart", "health", "life", "loop" },
            Bg = "ground",
        };

        readonly ScratchLayer L = new ScratchLayer(40, 40);
        readonly Options o;

        HeartPickup(Options o, Emitter e) => this.o = o;

        // a beat: a sharp squash that springs back through a stretch
        static double Beat(double ph, double at, double s)
        {
            double u = ph - at;
            if (u < 0 || u > 0.3) return 0;
            return s * Exp(-u * 16) * Px.Cos(u * 34);
        }

        static double Pulse(double ph) => Beat(ph, LUB, 1) + Beat(ph, DUB, 0.55) + Beat(ph + 1, LUB, 1);

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION, y = -HOVER - 1.2 * Px.Sin(ph * TAU);
            BlobShadow(p, 0, 8 + 1.5 * Pulse(ph), 0.8);
            // tiny hearts float off from behind it, wobbling, and fade
            Cycle(t, DURATION, 4, o.Seed, 0.95, (age, life, r, i) =>
            {
                double side = i % 2 != 0 ? 1 : -1, k = age / life;
                double x = side * (r.Range(2, 5) + age * r.Range(9, 15)) + Px.Sin(age * 7 + r.Range(0, TAU)) * 1.5, yy = y - 5 - age * r.Range(14, 19);
                double a = Math.Min(1, age / 0.12) * (1 - k * k);
                var pal = new Dictionary<char, uint> { ['a'] = k < 0.5 ? C[5] : C[4], ['b'] = k < 0.3 ? WHITE : C[6] };
                p.Sprite(MINI, pal, Round(x - 2.5), Round(yy - 2), a: a * 1.3);
                p.Glow(x, yy, 5, C[4], 0.3 * a);
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / DURATION, y = -HOVER - 1.2 * Px.Sin(ph * TAU), b = Pulse(ph);
            // squash wide on the beat, then spring back through a stretch
            double sx = 1 + 0.16 * b, sy = 1 - 0.13 * b, cy = Round(y);
            // light swells on each beat
            p.Glow(0, cy, 22 + 6 * Math.Max(0, b), C[4], 0.26 + 0.3 * Math.Max(0, b));
            L.Begin(-20, cy - 20);
            Pillow(L, -12, cy - 11, 12, cy + 11, (x, yy) => HeartSdf(x, yy, 0, cy, W * sx, W * sy), (lt, e, x, yy) =>
            {
                if (lt > 0.92 && e > 1.2) return WHITE;
                return lt > 0.8 ? C[6] : lt > 0.6 ? C[5] : lt > 0.38 ? C[4] : lt > 0.12 ? C[3] : C[2];
            }, bevel: 3.6);
            L.Outline(INK).Draw(p);
            // a thin ring of light snaps out on the big beat
            double u = ((ph - LUB) % 1 + 1) % 1;
            if (u < 0.28)
            {
                double k = u / 0.28, r = 9 + 8 * (1 - Px.Pow(1 - k, 3));
                p.Ring(0, cy, r, 1, k < 0.2 ? C[6] : C[5], 0.85 * (1 - k));
                p.GlowRing(0, cy, r, 4, C[4], 0.25 * (1 - k));
            }
            // a glint on its shoulder after each beat
            double g = ((ph - LUB - 0.08) % 0.5 + 0.5) % 0.5;
            if (g < 0.12) Glint(p, -3.5, cy - 4, 2.6 * Px.Sin(g / 0.12 * Math.PI), WHITE, 0.45);
        }
    }
}
