// The shaded, outlined icons and the twinkles are
// adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Stunned : Effect
    {
        static readonly Pose DAZE = new Pose { ["turn"] = 1, ["fx"] = -7, ["fy"] = -15, ["bx"] = 8, ["by"] = -15, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, new Pose(DAZE) { ["x"] = -1, ["lean"] = -3, ["hx"] = -1 }),
                new PoseKey(0.5, new Pose(DAZE) { ["x"] = 0, ["lean"] = 0, ["crouch"] = 2, ["hy"] = 1, ["ffx"] = -2 }),
                new PoseKey(1, new Pose(DAZE) { ["x"] = 1, ["lean"] = 3, ["hx"] = 1 }),
                new PoseKey(1.5, new Pose(DAZE) { ["x"] = 0, ["lean"] = 0, ["crouch"] = 2, ["hy"] = 1, ["fbx"] = 3 }),
                new PoseKey(2, new Pose(DAZE) { ["x"] = -1, ["lean"] = -3, ["hx"] = -1 }),
            },
        };

        public static readonly Module Module = new Module("stunned", "pixelfx/fx/status/stunned", 60, 64, 30, 56, 2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#3a1e0c", "#8a4a12", "#d88a1c", "#f5c030", "#ffe36e", "#fff6c8", "#ffffff" },
                ["birds"] = new[] { "#1a1422", "#6a7fae", "#b8c8e8", "#f4f8ff" },
                ["beak"] = "#ffae35",
                ["seed"] = 2,
            },
            (o, e) => new Stunned(o), act: Act, actor: 2)
        {
            Title = "Stunned",
            Desc = "Seeing stars: three spinning stars and two little birds circle the head on a tilted ring, ducking behind it and sweeping round in front, trailing glitter.",
            Tags = new[] { "status", "stun", "dizzy", "daze", "stars", "birds", "debuff", "loop" },
            Bg = "ground",
        };

        const double D = 2, TURNS = 2;
        const int N = 5;
        static readonly string[] STAR7 = { "...#...", "..###..", "#######", ".#####.", "..###..", ".##.##.", ".#...#." };
        static readonly int[][] WING_UP = { new[] { -1, -1 }, new[] { -1, -2 }, new[] { -2, -2 }, new[] { -2, -3 }, new[] { -3, -3 }, new[] { -3, -4 } };
        static readonly int[][] WING_DOWN = { new[] { -1, 1 }, new[] { -1, 2 }, new[] { -2, 2 }, new[] { -2, 3 }, new[] { 0, 1 } };

        // a little bird facing right: body, head, eye, beak, tail, a wing that beats
        static Func<double, double, char> Bird(bool up) => (dx, dy) =>
        {
            double x = dx + 0.5, y = dy + 0.5;
            if (dx == 3 && dy == -2) return 'e';
            if ((dx == 5 || dx == 4) && dy == -1) return dx == 5 ? 'b' : '#';
            foreach (int[] w in up ? WING_UP : WING_DOWN)
                if (w[0] == dx && w[1] == dy) return 'w';
            if (Px.Pow(x / 3.3, 2) + Px.Pow((y - 0.6) / 1.9, 2) <= 1 || Px.Pow(x - 2.8, 2) + Px.Pow(y + 1.2, 2) <= Px.Pow(1.7, 2)) return '#';
            if ((dx == -4 && (dy == -1 || dy == 0)) || (dx == -5 && dy == -1)) return '#';
            return '\0';
        };

        static readonly Func<double, double, char>[] BIRD = { Bird(true), Bird(false) };
        static readonly Func<double, double, bool> STAR_SHAPE = StarShape(0.44);
        static Func<double, double, char> STAR(double s, double ang) => Shaped(STAR_SHAPE, s, ang);

        sealed class Item
        {
            public int i;
            public double th, x, y, depth, rx, ry, cy;
            public bool near, bird;
        }

        // the ring round the head: stars at 0, 2, 4, birds between
        static Item[] Ring(double t, Actor a)
        {
            double cy = a.Y + 2, rx = a.W * 0.62, ry = 4.5;
            var items = new Item[N];
            for (int i = 0; i < N; i++)
            {
                double th = TAU * ((TURNS * t) / D + (double)i / N), s = Px.Sin(th);
                items[i] = new Item { i = i, th = th, x = Px.Cos(th) * rx, y = cy + s * ry, near = s >= 0, depth = 0.5 + 0.5 * s, bird = i % 2 == 1, rx = rx, ry = ry, cy = cy };
            }
            return items;
        }

        static void Draw1(Painter p, double t, Options o, bool far)
        {
            uint[] C = Fit(o.Ramp("palette"), 7), Bd = Fit(o.Ramp("birds"), 4);
            Actor a = o.Actor;
            uint Dim(uint c) => far ? Mix(c, Bd[0], 0.3) : c;
            foreach (Item it in Ring(t, a))
            {
                if (it.near == far) continue;
                // glitter left along the ring behind each star
                if (!it.bird)
                    for (int k = 1; k <= 12; k++)
                    {
                        double th = it.th - k * 0.085, x = Px.Cos(th) * it.rx, y = it.cy - 1 + Px.Sin(th) * it.ry;
                        if ((Px.Sin(th) >= 0) == far || Rand(it.i, k, Math.Floor(((t / D) * TURNS + k * 0.37) * 9)) < 0.25) continue;
                        p.Dot(x, y, Dim(k < 4 ? C[6] : k < 8 ? C[5] : C[4]), 1.1 - k / 13.0);
                        if (k < 6) p.Light(x, y, C[4], 0.3 * (1 - k / 6.0));
                    }
                if (it.bird)
                {
                    bool up = Math.Floor(t * 8 + it.i * 0.5) % 2 == 0;
                    double bob = Px.Sin((t / D) * TAU * 4 + it.i) * 0.7;
                    bool flip = it.near;
                    Func<double, double, char> f = BIRD[up ? 0 : 1], ins = flip ? (dx, dy) => f(-dx, dy) : f;
                    Icon(p, it.x, it.y - 3 + bob, 7, ins, new[] { Bd[0], Dim(Bd[1]), Dim(Bd[2]), Dim(Bd[3]) }, 1,
                        new Dictionary<char, uint> { ['e'] = Bd[0], ['b'] = Dim(o.Color("beak")), ['w'] = Dim(Bd[1]) });
                }
                else
                {
                    double spin = (t / D) * TAU * 3 + it.i;
                    uint[] R = { C[0], Dim(C[3]), Dim(C[4]), Dim(C[5]) };
                    if (it.depth > 0.55) Icon(p, it.x, it.y - 1, 7, STAR(4 + 0.8 * it.depth, spin), R);
                    else Icon(p, it.x, it.y - 1, 5, Bitmap(STAR7), R);
                    p.Px(it.x, it.y - 1, far ? C[5] : C[6]);
                    p.Glow(it.x, it.y - 1, 11, C[4], 0.24 + 0.3 * it.depth);
                    p.Glow(it.x, it.y - 1, 4, C[6], 0.3 * it.depth);
                    // a flare as it sweeps past the front, nearest the eye
                    double fr = Exp(-Px.Pow(Atan2(Px.Sin(it.th - Math.PI / 2), Px.Cos(it.th - Math.PI / 2)) / 0.32, 2));
                    if (!far && fr > 0.08) Sim.Flare(p, it.x, it.y - 1, 9 * fr, fr, new[] { C[3], C[4], C[5], C[6] });
                }
            }
        }

        readonly Options o;

        Stunned(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t) => Draw1(p, t, o, true);

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 7);
            Actor a = o.Actor;
            Draw1(p, t, o, false);
            // little twinkles popping round the ring
            Cycle(t, D, 14, o.Seed + 3, r => r.Range(0.25, 0.5), (age, life, r, i) =>
            {
                double th = r.Range(0, TAU), u = age / life, x = Px.Cos(th) * a.W * r.Range(0.45, 0.95), y = a.Y + 1 + Px.Sin(th) * 7 - r.Range(0, 9);
                double arm = Round(r.Range(1, 2.6) * Px.Sin(Math.PI * u));
                if (arm < 1) p.Dot(x, y, C[6], Px.Sin(Math.PI * u) * 1.4);
                Sim.Sparkle(p, x, y, arm, C[5], C[4], 0.35 * Px.Sin(Math.PI * u));
            });
            p.Glow(0, a.Y + 1, 26, C[4], 0.13);
        }
    }
}
