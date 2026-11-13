// The shaded, outlined letters are
// adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Sleep : Effect
    {
        static readonly Pose NOD = new Pose { ["turn"] = 0.9, ["crouch"] = 1, ["lean"] = 1, ["hy"] = 2, ["fx"] = -6, ["fy"] = -14, ["bx"] = 7, ["by"] = -14, ["ffx"] = -2, ["fbx"] = 3 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[] { new PoseKey(0, NOD), new PoseKey(1.2, new Pose(NOD) { ["crouch"] = 2, ["lean"] = 2, ["hy"] = 3 }), new PoseKey(2.4, NOD) },
        };

        public static readonly Module Module = new Module("sleep", "pixelfx/fx/status/sleep", 60, 82, 26, 74, 2.4, true, 30,
            new Options { ["palette"] = new[] { "#1a1636", "#3c3a7a", "#6f78c8", "#a9b8f0", "#dfe6ff", "#ffffff" }, ["seed"] = 3 },
            (o, e) => new Sleep(o), act: Act, actor: 2)
        {
            Title = "Sleep",
            Desc = "Fast asleep: eyes shut, soft outlined Zs float up off the head one after another, swaying and growing as they rise, dream motes drift, a slow breathing glow.",
            Tags = new[] { "status", "sleep", "zzz", "rest", "debuff", "loop" },
            Bg = "ground",
        };

        const double D = 2.4;
        const int NZ = 3;

        // the letter at four sizes: it grows a step at a time as it rises
        static readonly Func<double, double, char>[] ZS = Array.ConvertAll(new[]
        {
            new[] { "#####", "...#.", "..#..", ".#...", "#####" },
            new[] { "#######", ".....#.", "....#..", "...#...", "..#....", ".#.....", "#######" },
            new[] { ".#######.", "#########", "......##.", ".....##..", "....##...", "...##....", "..##.....", "#########", ".#######." },
            new[] { ".#########.", "###########", ".......###.", "......###..", ".....###...", "....###....", "...###.....", "..###......", ".###.......", "###########", ".#########." },
        }, rows => Bitmap(rows));

        // a Z at age u (0..1): where it is, how big, how solid
        static (double x, double y, int size, double k) ZAt(Actor a, int i, double u)
        {
            double side = i % 2 != 0 ? 1 : 0.6, x0 = a.X + a.W * 0.78, y0 = a.Y + 7;
            double x = x0 + 13 * side * u + 2.5 * Px.Sin(TAU * 1.1 * u + i * 2.1), y = y0 - 32 * (1 - Px.Pow(1 - u, 1.35));
            int size = u < 0.18 ? 0 : u < 0.42 ? 1 : u < 0.68 ? 2 : 3;
            double k = Math.Min(1, u / 0.06);
            return (x, y, size, k);
        }

        // shut the eyes: paint them over with skin and draw a soft lid line
        static void Shut(Painter p, Actor a)
        {
            foreach (StatusEye e in Eyes(a))
            {
                foreach (int[] xy in e.Px) p.Px(a.X + xy[0], a.Y + xy[1], e.Skin);
                double y = a.Y + e.Y1 - 1;
                for (int x = e.X0; x <= e.X1; x++) p.Px(a.X + x, y, 0xff1f1726);
                p.Px(a.X + e.X0 - 1, y - 1, 0xff1f1726);
                p.Px(a.X + e.X1 + 1, y - 1, 0xff1f1726);
            }
        }

        readonly Options o;

        Sleep(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 6);
            Actor a = o.Actor;
            double breath = 0.5 + 0.5 * Px.Sin((t / D) * TAU * 2);
            Shut(p, a);
            // a slow breathing glow round the head, and soft dream light drifting about it
            p.Glow(0, a.Y + 10, 24, C[3], 0.1 + 0.1 * breath);
            Cycle(t, D, 5, o.Seed + 6, D, (age, life, r, i) =>
            {
                double u = age / life, x = a.X + a.W * r.Range(0.3, 1.1) + Px.Sin(u * TAU + r.Next() * TAU) * 3, y = a.Y + r.Range(-6, 8) - u * 10;
                double rad = r.Range(3, 5) * (0.8 + 0.4 * u), k = Px.Sin(Math.PI * u);
                p.Glow(x, y, rad * 2.4, C[3], 0.1 * k);
            });
            // dream motes drifting up
            Cycle(t, D, 14, o.Seed + 4, r => r.Range(1.2, 2), (age, life, r, i) =>
            {
                double u = age / life, x = a.X + a.W * r.Range(0.4, 1.1) + Px.Sin(age * 2 + r.Next() * TAU) * 2, y = a.Y + r.Range(0, 10) - age * r.Range(10, 18);
                double tw = Px.Sin(Math.PI * u) * (0.6 + 0.4 * Px.Sin(age * 9 + r.Next() * TAU));
                if (tw < 0.15) return;
                Sim.Sparkle(p, x, y, tw > 0.7 && r.Next() < 0.4 ? 1 : 0, C[4], C[3], 0.3 * tw);
                p.Dot(x, y, tw > 0.6 ? C[5] : C[4], tw * 1.3);
            });
            // the Zs, oldest (biggest) first so the young ones sit on top
            var zs = new List<(int i, double u)>();
            for (int i = 0; i < NZ; i++) zs.Add((i, (((t - (i * D) / NZ) % D) + D) % D / D));
            zs = StableSort(zs, (p1, p2) => p2.u - p1.u);
            foreach (var (i, u) in zs)
            {
                var z = ZAt(a, i, u);
                if (u > 0.9)
                {
                    // it pops into motes
                    double k = (u - 0.9) / 0.1;
                    for (int j = 0; j < 7; j++)
                    {
                        double an = (j / 7.0) * TAU + i, d = 3 + 6 * Math.Sqrt(k);
                        p.Dot(z.x + Px.Cos(an) * d, z.y + Px.Sin(an) * d * 0.8, j % 2 != 0 ? C[4] : C[5], 1.2 - k);
                    }
                    p.Glow(z.x, z.y, 10, C[3], 0.25 * (1 - k));
                    continue;
                }
                // a dotted trail where it has been
                for (int j = 1; j <= 6; j++)
                {
                    var q = ZAt(a, i, Math.Max(0, u - j * 0.025));
                    if (j % 2 == 0 || u - j * 0.025 < 0.05) continue;
                    p.Dot(q.x - 1, q.y + 4 + q.size, C[3], (1 - j / 7.0) * 0.9);
                }
                // it dims down the ramp as it gets old
                bool old = u > 0.78;
                uint[] R = old ? new[] { C[0], C[1], C[2], C[3] } : new[] { C[1], C[2], C[3], C[4] };
                Icon(p, z.x, z.y, 7, ZS[z.size], R, z.k);
                if (!old) p.Px(z.x - 2 - z.size, z.y - 2 - z.size * 0.8, C[5]);
                p.Glow(z.x, z.y, 7 + z.size * 3, C[3], (old ? 0.12 : 0.26) * z.k);
            }
        }
    }
}
