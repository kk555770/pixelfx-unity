// The exhaust (flame puffs cooling down the fire ramp into smoke, a warm light over the smoke) follows Sprite
// Fusion's open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Jetpack : Effect
    {
        public static readonly Module Module = new Module("jetpack", "pixelfx/fx/scifi/jetpack", 96, 116, 48, 30, 2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0e12", "#5c1818", "#a02818", "#e0501a", "#ff8a2a", "#ffc23d", "#fff3a6", "#ffffff" },
                ["metal"] = new[] { "#0e0d14", "#1e1c27", "#312e3e", "#4b4759", "#6f6a82", "#9d98b0" },
                ["length"] = 26,
                ["seed"] = 30,
            },
            (o, e) => new Jetpack(o, e))
        {
            Title = "Jetpack",
            Desc = "Twin thrusters fire straight down: white-hot cores with shock diamonds inside ragged flames, puffs that cool into smoke billowing below, lit by the fire.",
            Tags = new[] { "scifi", "jetpack", "thrust", "rocket", "fire", "smoke", "flight", "loop" },
            Bg = "dark",
        };

        const double D = 2, GAP = 8, FR = 30;
        static readonly int[] SIDES = { -1, 1 };

        readonly uint[] F, H;
        readonly double S, length;
        readonly ScifiLib.Ghosts g;

        Jetpack(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 8);
            H = Fit(o.Ramp("metal"), 6);
            S = o.Seed;
            length = o.Num("length");
            var rn = new Rng(o.Seed);
            double L = length;
            g = new ScifiLib.Ghosts(D, null, new[] { WHITE, F[6], F[5], F[4] }, 0, new double[] { -46, -28, 46, 84 });
            foreach (int s in SIDES)
            {
                // flame shed off the tip, cooling down the ramp into smoke that's driven down, slows and billows
                for (int i = 0; i < 26; i++) g.Add(rn.Range(0, D), P.Fire, s * GAP + rn.Range(-1.5, 1.5), L * rn.Range(0.55, 0.85), rn.Range(-10, 10), rn.Range(70, 130), rn.Range(0.22, 0.4), 0, rn.Range(2.2, 3.4), PF.Smokes | (rn.Chance(0.5) ? PF.Big : PF.None));
                for (int i = 0; i < 10; i++) g.Add(rn.Range(0, D), P.Smoke, s * GAP + rn.Range(-3, 3), L + rn.Range(2, 8), s * rn.Range(0, 14), rn.Range(40, 80), rn.Range(1.1, 1.6), 0, rn.Range(3, 5), PF.Big);
                // sparks spat out of the exhaust, embers drifting off it
                for (int i = 0; i < 10; i++) g.Add(rn.Range(0, D), P.Spark, s * GAP + rn.Range(-1, 1), L * rn.Range(0.3, 0.7), s * rn.Range(-40, 60), rn.Range(80, 200), rn.Range(0.2, 0.45));
                for (int i = 0; i < 5; i++) g.Add(rn.Range(0, D), P.Ember, s * GAP + rn.Range(-2, 2), L * rn.Range(0.6, 1), s * rn.Range(10, 40), rn.Range(20, 60), rn.Range(0.6, 1.1), F[4], rn.Range(0.8, 1.6));
            }
        }

        // the pack: twin tanks with rounded caps, a spine between them, belled nozzles glowing hot inside
        static void Pack(Painter p, uint[] H, uint[] F, double dy)
        {
            foreach (int s in SIDES)
            {
                double cx = s * GAP;
                p.Shade(cx - 5, -24 + dy, cx + 5, -3 + dy, (x, y, o) =>
                {
                    double u = (x - cx) / 4.5, top = -19 + dy, v = y < top ? (y - top) / 4.5 : 0;
                    if (u * u + v * v > 1) return 0;
                    double l = -u * 0.8 - v * 0.6;
                    o.c = u * u + v * v > 0.78 ? H[0] : Math.Abs(y - (-12 + dy)) < 0.6 ? H[1] : l > 0.55 ? H[5] : l > 0.15 ? H[4] : l > -0.35 ? H[3] : H[2];
                    return 0;
                });
                // the nozzle bell, wider at its mouth, its inside lit by the flame
                for (int y = -3; y <= 0; y++)
                {
                    double w = 2 + (y + 3) * 0.7;
                    for (double x = Math.Floor(cx - w); x <= Math.Ceiling(cx + w); x++)
                    {
                        bool inner = Math.Abs(x + 0.5 - cx) < w - 1.2 && y >= -1;
                        p.Px(x, y + dy, inner ? (y == 0 ? F[6] : F[4]) : Math.Abs(x + 0.5 - cx) > w - 0.6 ? H[0] : x < cx ? H[3] : H[1]);
                    }
                }
            }
            for (int y = -20; y <= -6; y++)
            {
                p.Px(-1, y + dy, H[1]);
                p.Px(0, y + dy, H[2]);
                p.Px(1, y + dy, H[0]);
            }
            // a status light on the spine
            p.Px(0, -16 + dy, F[5]);
        }

        public override void Draw(Painter p, double t)
        {
            double ph = t / D, f = Math.Floor(t * FR) % Round(D * FR);
            double dy = Round(Px.Sin(TAU * 12 * ph) * 0.6);
            Particles gp = g.At(t);
            // smoke and spent flame behind, then the plumes
            gp.Draw(p, Layer.Back);
            foreach (int s in SIDES)
            {
                double cx = s * GAP + (Rand(S, f, s) < 0.15 ? s * 0.5 : 0), L = length * (0.92 + 0.08 * Px.Sin(TAU * 7 * ph + s) + 0.06 * (Rand(S, f, s, 1) - 0.5));
                // the flame: a cone of heat torn ragged by noise streaming down it, white at its heart, red at its edges
                p.Shade(cx - 7, dy, cx + 7, dy + L + 4, (x, y, o) =>
                {
                    double v = (y - dy) / L, w = Lerp(3.4, 0.6, v) + 1.4 * Px.Sin(v * Math.PI) * (v < 1 ? 1 : 0);
                    double n = TileFbm((x - cx) / 3 + s * 5, (y - dy) / 4 - ph * 20, 0, 10, S + (s > 0 ? 9 : 0), 2), q = Math.Abs(x - cx) / w + n * 0.45;
                    if (v > 1.15 || q > 1.2) return 0;
                    double heat = (1 - v) * 1.15 - q * 0.9 + n * 0.25;
                    // a dark red rim round the flame, so it reads as a shape and not a smear
                    if (q > 0.95)
                    {
                        o.c = heat > 0.2 ? F[3] : F[2];
                        if (heat < -0.3) o.a = 0.5;
                        return 0;
                    }
                    o.c = heat > 0.82 ? WHITE : heat > 0.62 ? F[6] : heat > 0.42 ? F[5] : heat > 0.22 ? F[4] : heat > 0.02 ? F[3] : F[2];
                    return 0;
                });
                // shock diamonds: bright knots strung down the core, breathing
                for (int i = 0; i < 3; i++)
                {
                    double y = Round(dy + 5 + i * 6.5 + Px.Sin(TAU * 4 * ph + i) * 0.5), r = Round(2.2 - i * 0.45 + 0.4 * Px.Sin(TAU * 8 * ph + i * 2));
                    if (y > dy + L * 0.75 || r < 1) break;
                    for (double yy = -r; yy <= r; yy++)
                        for (double xx = -r; xx <= r; xx++)
                        {
                            double d = Math.Abs(xx) + Math.Abs(yy);
                            if (d <= r) p.Px(cx + xx, y + yy, d < r ? WHITE : i == 0 ? F[6] : F[5]);
                        }
                    p.Glow(cx, y, 6, F[6], 0.35);
                }
                // light: hot at the nozzle, warm down the plume
                p.Glow(cx, dy + 2, 9, WHITE, 0.45);
                p.Glow(cx, dy + L * 0.45, L * 0.8, F[4], 0.35);
            }
            Pack(p, H, F, dy);
            gp.Draw(p, Layer.Front);
            // the fire lights the smoke below it, and its own surroundings
            p.Glow(0, length + 14, 34, F[4], 0.16);
            p.Glow(0, length * 0.4, 30, F[5], 0.18);
        }
    }
}
