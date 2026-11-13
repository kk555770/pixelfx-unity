// The shaded, outlined hearts are
// adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Charmed : Effect
    {
        static readonly Pose SWOON = new Pose { ["turn"] = 1.1, ["fx"] = -2, ["fy"] = -28, ["bx"] = 2, ["by"] = -28, ["armsFront"] = true, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, new Pose(SWOON) { ["lean"] = -2 }),
                new PoseKey(0.6, new Pose(SWOON) { ["lean"] = 0, ["crouch"] = 1 }),
                new PoseKey(1.2, new Pose(SWOON) { ["lean"] = 2 }),
                new PoseKey(1.8, new Pose(SWOON) { ["lean"] = 0, ["crouch"] = 1 }),
                new PoseKey(2.4, new Pose(SWOON) { ["lean"] = -2 }),
            },
        };

        public static readonly Module Module = new Module("charmed", "pixelfx/fx/status/charmed", 60, 76, 30, 68, 2.4, true, 30,
            new Options { ["palette"] = new[] { "#3a0a1e", "#7a1238", "#c4245a", "#f04e86", "#ff8ab0", "#ffc8da", "#ffffff" }, ["seed"] = 8 },
            (o, e) => new Charmed(o), act: Act, actor: 2)
        {
            Title = "Charmed",
            Desc = "Smitten: a big heart beats over the head, throwing a ring and a flush of light with every beat, little hearts float up, the eyes go pink and the cheeks blush.",
            Tags = new[] { "status", "charm", "love", "hearts", "debuff", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, BEAT = D / 2;
        static readonly Func<double, double, char> HEART5 = Bitmap(new[] { ".#.#.", "#####", "#####", ".###.", "..#.." });
        static readonly Func<double, double, char> HEART7 = Bitmap(new[] { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." });

        // a heartbeat: a strong lub and a softer dub
        static double Thump(double ph) => Exp(-Px.Pow((ph - 0.04) / 0.07, 2)) + 0.6 * Exp(-Px.Pow((ph - 0.26) / 0.07, 2));

        // little hearts drifting up (half of them behind the character)
        static void Hearts(Painter p, uint[] C, Actor a, double t, double seed, bool back)
        {
            Cycle(t, D, 12, seed + 4, r => r.Range(1.3, 1.9), (age, life, r, i) =>
            {
                if ((i % 3 == 0) != back) return;
                double u = age / life, side = r.Next() < 0.5 ? -1 : 1, ph = r.Range(0, TAU);
                bool big = r.Next() < 0.3;
                double x = side * r.Range(4, 15) + Px.Sin(age * 4 + ph) * 2.2, y = a.Y + r.Range(8, 30) - age * r.Range(14, 22);
                double k = Math.Min(1, u * 6);
                if (u > 0.88)
                {
                    // it pops into a twinkle
                    double q = (u - 0.88) / 0.12;
                    Sim.Sparkle(p, x, y, Round(2 * (1 - q)), C[5], C[4], 0.4 * (1 - q));
                    return;
                }
                uint[] R = back ? new[] { C[0], C[1], C[2], C[3] } : new[] { C[0], C[2], C[3], C[4] };
                Icon(p, x, y, 4, big ? HEART7 : HEART5, R, k);
                if (!back) p.Px(x - (big ? 2 : 1), y - 1, C[6]);
                p.Glow(x, y, 6, C[3], (back ? 0.12 : 0.22) * k);
            });
        }

        readonly Options o;

        Charmed(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 7);
            Actor a = o.Actor;
            double ph = ((t % BEAT) + BEAT) % BEAT, th = Thump(ph);
            p.Glow(0, a.Y - 8, 26 + 8 * th, C[3], 0.16 + 0.22 * th);
            Hearts(p, C, a, t, o.Seed, true);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 7);
            Actor a = o.Actor;
            double ph = ((t % BEAT) + BEAT) % BEAT, th = Thump(ph);
            // lovestruck: pink eyes with a bright catchlight, flushed cheeks
            foreach (StatusEye e in Eyes(a))
            {
                foreach (int[] xy in e.Px) p.Px(a.X + xy[0], a.Y + xy[1], xy[1] == e.Y0 ? C[3] : C[2]);
                p.Px(a.X + e.X0, a.Y + e.Y0 + 1, C[6]);
                uint blush = Mix(e.Skin, C[3], 0.55);
                int out_ = e.X0 < a.W / 2.0 ? -1 : 1, bx = out_ < 0 ? e.X0 - 1 : e.X1;
                p.Px(a.X + bx, a.Y + e.Y1 + 1, blush);
                p.Px(a.X + bx + 1, a.Y + e.Y1 + 1, blush);
            }
            Hearts(p, C, a, t, o.Seed, false);
            // the big heart, beating
            double hx = 0, hy = a.Y - 10, s = 5.6 * (1 + 0.24 * th);
            // a heart-shaped ring thrown off with each beat
            foreach (double[] ring in new[] { new[] { 0.04, 1 }, new[] { 0.26, 0.6 } })
            {
                double t0 = ring[0], kk = ring[1], u = (ph - t0) / 0.5;
                if (u < 0 || u > 1) continue;
                double S1 = 5.6 * (1.25 + 1.3 * u);
                Func<double, double, char> outer = Shaped(HeartShape, S1), inner = Shaped(HeartShape, S1 - 1.3);
                double E = Math.Ceiling(S1 * 1.2) + 1;
                for (double dy = -E; dy <= E; dy++)
                    for (double dx = -E; dx <= E; dx++)
                        if (outer(dx, dy) != '\0' && inner(dx, dy) == '\0') p.Dot(hx + dx, hy + dy, u < 0.3 ? C[5] : C[4], (1 - u) * kk * 1.3);
            }
            Icon(p, hx, hy, 9, Shaped(HeartShape, s), new[] { C[0], C[2], C[3], C[4] });
            // a glint on the left lobe
            p.Px(hx - Round(s * 0.55), hy - Round(s * 0.45), C[6]);
            p.Px(hx - Round(s * 0.55) + 1, hy - Round(s * 0.45), C[5]);
            p.Px(hx - Round(s * 0.55), hy - Round(s * 0.45) + 1, C[5]);
            p.Glow(hx, hy, 12 + 6 * th, C[4], 0.3 + 0.4 * th);
            if (th > 0.5) Sim.Sparkle(p, hx + s * 0.9, hy - s * 0.8, Round(3 * th), C[5], C[4], 0.4 * th);
            // twinkles round the heart
            Cycle(t, D, 8, o.Seed + 2, r => r.Range(0.3, 0.5), (age, life, r, i) =>
            {
                double an = r.Range(0, TAU), d = r.Range(8, 15), u = age / life;
                Sim.Sparkle(p, hx + Px.Cos(an) * d, hy + Px.Sin(an) * d * 0.8, Round(1.6 * Px.Sin(Math.PI * u)), C[5], C[4], 0.3 * Px.Sin(Math.PI * u));
            });
        }
    }
}
