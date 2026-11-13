// The banded after-images, the dotted speed lines with bright heads and the kicked-up dust are
// adapted from Sprite Fusion's open-source movement dash (https://www.spritefusion.com/pixel-art-vfx/movement),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Haste : Effect
    {
        static readonly Pose RUN = new Pose { ["turn"] = 0.25, ["lean"] = 4 };
        static readonly Pose R1 = new Pose(RUN) { ["crouch"] = 3, ["ffx"] = 12, ["ffy"] = -5, ["fbx"] = -10, ["fby"] = -8, ["toeB"] = true, ["fx"] = -7, ["fy"] = -19, ["bx"] = 12, ["by"] = -27 };
        static readonly Pose R2 = new Pose(RUN) { ["crouch"] = 1, ["ffx"] = 4, ["ffy"] = -8, ["fbx"] = -1, ["fby"] = -5, ["fx"] = 3, ["fy"] = -18, ["bx"] = 5, ["by"] = -19 };
        static readonly Pose R3 = new Pose(RUN) { ["crouch"] = 3, ["ffx"] = -10, ["ffy"] = -8, ["toeF"] = true, ["fbx"] = 12, ["fby"] = -5, ["fx"] = 14, ["fy"] = -27, ["bx"] = -5, ["by"] = -19 };
        static readonly Pose R4 = new Pose(RUN) { ["crouch"] = 1, ["ffx"] = -1, ["ffy"] = -5, ["fbx"] = 4, ["fby"] = -8, ["fx"] = 5, ["fy"] = -19, ["bx"] = 3, ["by"] = -18 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(0, R1), new PoseKey(0.1, R2, "snap"), new PoseKey(0.2, R3, "snap"), new PoseKey(0.3, R4, "snap"), new PoseKey(0.4, R1, "snap"),
                new PoseKey(0.5, R2, "snap"), new PoseKey(0.6, R3, "snap"), new PoseKey(0.7, R4, "snap"), new PoseKey(0.8, R1, "snap"), new PoseKey(0.9, R2, "snap"),
                new PoseKey(1, R3, "snap"), new PoseKey(1.1, R4, "snap"), new PoseKey(1.2, R1, "snap"),
            },
        };

        public static readonly Module Module = new Module("haste", "pixelfx/fx/status/haste", 100, 56, 64, 48, 1.2, true, 30,
            new Options { ["palette"] = new[] { "#4c25ab", "#773ae4", "#5b6cff", "#5eb0ff", "#a3dcff", "#ccf6ff", "#ffffff" }, ["dust"] = "#8a8090", ["seed"] = 4 },
            (o, e) => new Haste(o), act: Act, actor: 2)
        {
            Title = "Haste",
            Desc = "Sped up: striped after-images stream out behind, speed lines whip past, dust kicks off the heels and the leading edge catches the wind. Faces right.",
            Tags = new[] { "status", "haste", "speed", "fast", "buff", "directional", "loop" },
            Bg = "ground",
        };

        const double D = 1.2;

        // the character's shading in three levels (dark outline, mid, light), for the after-images
        static readonly ConditionalWeakTable<Mask, List<int[]>> LEVELS = new ConditionalWeakTable<Mask, List<int[]>>();

        static List<int[]> Levels(Actor a)
        {
            if (LEVELS.TryGetValue(a.Mask, out List<int[]> l)) return l;
            StatusShape S = Shape(a);
            l = S.Body.ConvertAll(b =>
            {
                double v = Luma(ColorAt(a, b[0], b[1]));
                return new[] { b[0], b[1], v < 0.24 ? 1 : v < 0.66 ? 2 : 3 };
            });
            LEVELS.Add(a.Mask, l);
            return l;
        }

        // streaks: a bright head, a coloured body, a dotted tail thinning out behind (some cut across the body in front)
        static void Lines(Painter p, uint[] C, Actor a, double t, double seed, bool front)
        {
            Cycle(t, D, front ? 5 : 11, seed + (front ? 9 : 7), r => r.Range(0.12, 0.26), (age, life, r, i) =>
            {
                double u = age / life, y = a.Y + r.Range(2, a.H - 3), len = r.Range(10, 26) * (front ? 0.7 : 1);
                double x = r.Range(-14, 4) - age * r.Range(160, 240);
                for (int d = 0; d < len; d++)
                {
                    if (d > len * 0.45 && d % 2 != 0) continue;
                    p.Dot(x + d, y, d < 2 ? C[6] : d < len * 0.4 ? C[5] : C[3], (1 - d / len) * (1 - u) * (front ? 0.7 : 1.1));
                }
                p.Light(x, y, C[4], 0.4 * (1 - u));
            });
        }

        readonly Options o;

        Haste(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 7);
            Actor a = o.Actor;
            List<int[]> L = Levels(a);
            StatusShape S = Shape(a);
            int f = (int)Math.Floor(t * 30);
            // three after-images trailing back, each a step colder and cut into thinner stripes that scroll, jittering
            int[] everies = { 0, 3, 4, 0 };
            for (int g = 3; g >= 1; g--)
            {
                double dx = -g * 14 - Round(Rand(f >> 1, g, 3) * 1.6);
                int every = everies[g];
                // its outline, solid near, broken further back, and a few scrolling stripes across it
                int gg = g;
                p.Outline(a.Mask, a.X + dx, a.Y, (mx, my) => gg == 3 && (mx + my) % 2 != 0 ? 0 : C[5 - gg]);
                if (every != 0)
                    foreach (int[] l in L)
                    {
                        int x = l[0], y = l[1], v = l[2];
                        if ((((y + f) % every) + every) % every != 0) continue;
                        p.Px(a.X + x + dx, a.Y + y, C[Math.Max(0, Math.Min(6, 5 - g + v - 2))]);
                    }
                p.Glow(a.X + a.W / 2.0 + dx, a.Y + a.H * 0.45, 16, C[3], 0.12 * (1 - g * 0.25));
            }
            // a smear streaming off the trailing edge
            foreach (int[] s in S.Side)
            {
                int x = s[0], y = s[1], dir = s[2];
                if (dir > 0 || (y + (f >> 1)) % 3 != 0) continue;
                double len = 3 + Math.Floor(Rand(y, f >> 1, 5) * 6);
                for (int d = 1; d <= len; d++) p.Dot(a.X + x - d, a.Y + y, d < 3 ? C[5] : C[4], 1 - d / (len + 1));
            }
            // speed lines whipping past behind
            Lines(p, C, a, t, o.Seed, false);
            // dust kicked off the heels, rolling away behind
            uint dust = o.Color("dust");
            Cycle(t, D, 12, o.Seed + 5, r => r.Range(0.45, 0.7), (age, life, r, i) =>
            {
                double u = age / life, foot = i % 2 != 0 ? -7 : 3, x = foot + Travel(-r.Range(50, 100), 0, 3.5, age), y = -1.5 - Travel(r.Range(6, 18), 0, 3.5, age);
                double rad = r.Range(2.6, 4.6) * (0.6 + 1 * u), k = Px.Pow(1 - u, 1.2) * Math.Min(1, u * 10);
                p.Disc(x, y, rad, dust, k * 0.5);
                p.Disc(x + rad * 0.05, y + rad * 0.08, rad * 0.74, dust, k * 0.62);
                p.Disc(x - rad * 0.22, y - rad * 0.3, rad * 0.5, C[5], k * 0.25);
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 7);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            // the leading edge catches the wind: a bright rim, flickering
            foreach (int[] s in S.Side)
            {
                int x = s[0], y = s[1], dir = s[2];
                if (dir > 0 && Rand(x, y, Math.Floor(t * 15)) < 0.85) p.Px(a.X + x, a.Y + y, Rand(y, Math.Floor(t * 15), 2) < 0.3 ? C[6] : C[5]);
            }
            Lines(p, C, a, t, o.Seed, true);
            // a cool glow streaming off the back
            p.Glow(-6, a.Y + a.H * 0.5, 22, C[3], 0.16);
            p.Glow(0, a.Y + a.H * 0.45, 30, C[3], 0.16);
            p.Glow(a.X + a.W, a.Y + a.H * 0.5, 10, C[5], 0.12 + 0.06 * Px.Sin((t / D) * TAU * 3));
        }
    }
}
