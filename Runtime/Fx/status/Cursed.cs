// The soft lit smoke puffs and the shaded, outlined skull are
// adapted from Sprite Fusion's open-source poison (https://www.spritefusion.com/pixel-art-vfx/poison),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Cursed : Effect
    {
        static readonly Pose HUNCH = new Pose { ["turn"] = 1, ["crouch"] = 4, ["lean"] = 3, ["hy"] = 1, ["fx"] = -7, ["fy"] = -33, ["bx"] = 8, ["by"] = -33, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, HUNCH), new PoseKey(0.6, new Pose(HUNCH) { ["crouch"] = 5 }), new PoseKey(1.2, new Pose(HUNCH) { ["lean"] = 2 }),
                new PoseKey(1.8, new Pose(HUNCH) { ["crouch"] = 5 }), new PoseKey(2.4, HUNCH),
            },
        };

        public static readonly Module Module = new Module("cursed", "pixelfx/fx/status/cursed", 64, 84, 32, 76, 2.4, true, 30,
            new Options { ["palette"] = new[] { "#0c0612", "#1f0f2e", "#3a1a55", "#5e2a85", "#8a3fc0", "#b86ef0", "#e0b0ff", "#ffffff" }, ["seed"] = 9 },
            (o, e) => new Cursed(o), act: Act, actor: 2)
        {
            Title = "Cursed",
            Desc = "Hexed: violet smoke rises off the body in winding trails, a dark stain creeps up from the feet with a glowing edge, the eyes burn, a skull glitches in overhead.",
            Tags = new[] { "status", "curse", "hex", "dark", "debuff", "shadow", "loop" },
            Bg = "ground",
        };

        const double D = 2.4;
        static readonly Func<double, double, char> SKULL = Bitmap(new[]
        {
            "...#####...",
            ".#########.",
            "###########",
            "##eee#eee##",
            "##eEe#eEe##",
            "##eee#eee##",
            "#####.#####",
            ".####.####.",
            "..#######..",
            "..#.#.#.#..",
            "...#####...",
        });
        // the skull's life in the loop: hidden, glitching in, hanging there, glitching out
        const double IN = 0.5, HOLD = 0.75, OUT = 1.85, GONE = 2.1;

        // how high the stain has crept at column x (0 at the feet, 1 at the head), a slow tide with crawling tendrils
        static double Tide(double t) => 0.3 + 0.12 * Px.Sin((t / D) * TAU);
        static double Reach(double x, double t, double seed) => Tide(t) + 0.13 * TileFbm(x * 0.35, (t / D) * 4, 0, 4, seed, 2);

        // smoke trails: each rises from a spot on the body as a ribbon of soft puffs that wind as they climb, swell, cool
        // and thin
        static readonly double[][] SPOTS = { new[] { 0.12, -1 }, new[] { 0.1, 1 }, new[] { 0.5, -1 }, new[] { 0.48, 1 } };

        static void Trails(Painter p, uint[] C, Actor a, StatusShape S, double t, bool front)
        {
            for (int j = 0; j < SPOTS.Length; j++)
            {
                double q = SPOTS[j][0], side = SPOTS[j][1];
                if ((j % 2 == 0) != front) continue;
                double my = Round(S.H * q);
                int[] lr = S.Row(my) ?? new[] { 0, 0 };
                double l = lr[0], r = lr[1];
                double x0 = a.X + (side < 0 ? l : r + 1), y0 = a.Y + my, life = 1.6;
                int n = 18;
                for (int i = n - 1; i >= 0; i--)
                {
                    double age = (((t - ((double)i / n) * D - j * 0.37) % D) + D) % D, tb = t - age, u = age / life;
                    if (u >= 1) continue;
                    double sway = Px.Sin((tb / D) * TAU * 2 + j * 1.7) * 4 * Math.Min(1, u * 1.6);
                    double x = x0 + side * u * 7 + sway + (Rand(i, j, 71) - 0.5) * 2.5 * u, y = y0 - age * 19 - 5 * u * u;
                    double rad = (1.3 + 3.6 * (1 - Px.Pow(1 - u, 3))) * (0.7 + 0.6 * Rand(i, j, 72)), k = Smooth(0, 0.12, u) * (1 - Smooth(0.45, 1, u)) * (front ? 0.5 : 0.62);
                    uint c = Mix(Rand(i, j, 70) < 0.5 ? C[4] : Mix(C[4], C[3], 0.4), C[1], u * 0.85);
                    p.Disc(x, y, rad * 1.08, c, k * 0.4);
                    p.Disc(x + rad * 0.05, y + rad * 0.08, rad * 0.76, c, k * 0.55);
                    p.Disc(x - rad * 0.22, y - rad * 0.3, rad * 0.48, Mix(c, C[6], 0.25), k * 0.38 * (1 - u * 0.6));
                    if (i % 3 == 0) p.Glow(x, y, rad * 1.6, C[4], 0.08 * k);
                }
            }
        }

        readonly Options o;

        Cursed(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            // the stain pooling round the feet, its rim glowing
            int[] flr = S.Rows[S.H - 1];
            double fl = flr[0], fr = flr[1], cx = a.X + (fl + fr + 1) / 2, rx = (fr - fl) / 2 + 7 + 2 * Px.Sin((t / D) * TAU);
            for (double x = Math.Floor(cx - rx - 1); x <= cx + rx + 1; x++)
            {
                double q = Math.Abs(x + 0.5 - cx) / rx, n = Rand(x, 31);
                if (q > 1 + n * 0.12) continue;
                p.Px(x, 0, q > 0.8 ? C[4] : n < 0.2 ? C[3] : C[2]);
                if (q < 0.85) p.Px(x, 1, C[1]);
                if (q < 0.55 && n < 0.7) p.Px(x, 2, C[1]);
            }
            p.Shade(cx - rx - 6, -4, cx + rx + 6, 5, (x, y, out_) =>
            {
                double d = Hypot((x - cx) / (rx + 6), y / 4);
                if (d < 1)
                {
                    out_.l = C[4];
                    out_.k = 0.2 * (1 - d) * (1 - d);
                }
                return 0;
            });
            Trails(p, C, a, S, t, false);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            int H = S.H;
            double seed = o.Seed;
            // the stain on the body: everything below the creeping line drowned in violet, the line itself burning
            foreach (int[] b in S.Body)
            {
                int mx = b[0], my = b[1];
                uint c = ColorAt(a, mx, my);
                double up = 1 - (my + 0.5) / H, lv = Reach(mx, t, seed);
                if (up > lv + 0.035) continue;
                double x = a.X + mx, y = a.Y + my;
                if (up > lv - 0.02) p.Px(x, y, C[6]);
                else if (up > lv - 0.06) p.Px(x, y, C[4]);
                else p.Px(x, y, Mix(C[1], C[3], Clamp(Luma(c) * 1.6)));
            }
            for (int mx = 0; mx < S.W; mx++)
            {
                double lv = Reach(mx, t, seed), my = Math.Floor((1 - lv) * H);
                if (S.On(mx, my)) p.Glow(a.X + mx, a.Y + my, 4, C[5], 0.14);
            }
            // the eyes burn
            foreach (StatusEye e in Eyes(a))
            {
                foreach (int[] xy in e.Px) p.Px(a.X + xy[0], a.Y + xy[1], xy[1] == e.Y0 ? C[7] : C[5]);
                p.Glow(a.X + (e.X0 + e.X1 + 1) / 2.0, a.Y + (e.Y0 + e.Y1 + 1) / 2.0, 5, C[5], 0.4);
            }
            Trails(p, C, a, S, t, true);
            // the skull glitches in, hangs there bobbing, and glitches out
            if (t >= IN && t < GONE)
            {
                double glitch = t < HOLD ? 1 - (t - IN) / (HOLD - IN) : t > OUT ? (t - OUT) / (GONE - OUT) : 0;
                double f = Math.Floor(t * 30);
                bool on = glitch < 0.15 || Rand(f, 3) > glitch * 0.6;
                double x = 0, y = a.Y - 12 + Px.Sin((t / D) * TAU * 2) * 1.2;
                if (on)
                {
                    char Tear(double dx, double dy)
                    {
                        double row = dy + 6;
                        double shift = glitch > 0 && Rand(f, ToInt32(row) >> 1, 7) < glitch * 0.8 ? Round((Rand(f, row, 9) - 0.5) * 6 * glitch) : 0;
                        return SKULL(dx - shift, dy);
                    }
                    if (glitch > 0)
                    {
                        Icon(p, x - 2, y, 7, Tear, new uint[] { 0, C[2], C[2], C[3] }, 0.7 * glitch);
                        Icon(p, x + 2, y, 7, Tear, new uint[] { 0, C[6], C[6], C[6] }, 0.5 * glitch);
                    }
                    Icon(p, x, y, 8, Tear, new[] { C[0], C[4], C[6], C[7] }, 1, new Dictionary<char, uint> { ['e'] = C[0], ['E'] = C[7] });
                    p.Glow(x - 3, y - 1, 5, C[5], 0.6);
                    p.Glow(x + 3, y - 1, 5, C[5], 0.6);
                    p.Glow(x, y, 16, C[4], 0.24 + 0.3 * glitch);
                }
                // scanlines fizzing as it comes and goes
                if (glitch > 0.1)
                    for (int k = 0; k < 3; k++)
                    {
                        double ly = y - 5 + Math.Floor(Rand(f, k, 11) * 11), lx = Round((Rand(f, k, 12) - 0.5) * 10), len = 3 + Math.Floor(Rand(f, k, 13) * 8);
                        p.Line(lx - len / 2, ly, lx + len / 2, ly, k % 2 != 0 ? C[6] : C[4], glitch);
                    }
            }
            p.Glow(0, a.Y + H * (1 - Tide(t)), 24, C[4], 0.2);
            p.Glow(0, a.Y + H * 0.45, 30, C[3], 0.12);
        }
    }
}
