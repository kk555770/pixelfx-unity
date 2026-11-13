// The leaves' tumble (turning as they go, flipping over through edge-on, palettes of autumn and green) is adapted
// from Sprite Fusion's open-source falling leaves (https://www.spritefusion.com/pixel-art-vfx/falling-leaves),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class LeafStorm : Effect
    {
        const double DURATION = 2.4;

        static readonly Pose UP = new Pose { ["turn"] = 1.1, ["crouch"] = 0, ["fx"] = -13, ["fy"] = -37, ["bx"] = 13, ["by"] = -37, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(0, UP),
                new PoseKey(0.6, new Pose(UP) { ["lean"] = 2, ["fy"] = -40, ["by"] = -33 }),
                new PoseKey(1.2, new Pose(UP) { ["crouch"] = -1 }),
                new PoseKey(1.8, new Pose(UP) { ["lean"] = -2, ["fy"] = -33, ["by"] = -40 }),
                new PoseKey(2.4, UP),
            },
        };

        public static readonly Module Module = new Module("leaf-storm", "pixelfx/fx/nature/leaf-storm", 112, 104, 56, 92, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#1a2420", "#2e4038", "#4a6656", "#7a9a84", "#b4d0b8", "#eef8e8" },
                ["leaves"] = new[] { "#123818", "#2f7a2a", "#6cb83a", "#c8f070", "#4a2a08", "#a8701c", "#e8b42e", "#fff09a", "#3a1010", "#9a2e1c", "#e0642a", "#ffb070" },
                ["seed"] = 5,
            },
            (o, e) => new LeafStorm(o), act: Act, actor: 2)
        {
            Title = "Leaf storm",
            Desc = "Leaves tumble up a rising spiral round the character, passing behind and in front, turning and flipping, wrapped in bright bands of wind; litter swirls at their feet.",
            Tags = new[] { "nature", "wind", "leaves", "buff", "aura", "actor", "loop" },
            Bg = "ground",
        };

        const double SY = 0.3, TOP = 64;
        static double RingR(double y) => 15 + 9 * Clamp(-y / TOP);

        // leaves riding the spiral: up from the floor, round two turns or so, flung out off the top
        static void Leaves(Painter p, uint[] L, double t, double seed, bool back)
        {
            Cycle(t, DURATION, 28, seed, g => g.Range(1.5, 2.3), (age, life, R, _) =>
            {
                double k = age / life, turns = R.Range(1.4, 2.2), th = R.Range(0, TAU) + turns * TAU * k, sn = Px.Sin(th);
                if ((sn > 0) == back) return;
                double y = -Lerp(1, TOP, Px.Pow(k, 0.9)) - R.Range(0, 6), fling = Smooth(0.75, 1, k), rr = RingR(y) * R.Range(0.85, 1.15) + fling * 22;
                double x = Px.Cos(th) * rr, yy = y + sn * rr * SY;
                double a = Math.Min(1, k * 7) * (1 - Smooth(0.85, 1, k)), spin = R.Range(-9, 9) * age + th, fl = age * R.Range(5, 11);
                int o = R.Int(0, 2) * 4;
                uint[] pal = back ? new[] { L[o], L[o], L[o + 1], L[o + 2] } : new[] { L[o], L[o + 1], L[o + 2], L[o + 3] };
                double len = R.Range(6.5, 9), wid = R.Range(2.4, 3.3);
                NatureLib.DrawLeaf(p, x, yy, len, wid, spin, fl, pal, a);
                // a glint as it turns face on to the light
                if (!back && Math.Abs(Px.Cos(fl)) > 0.97)
                {
                    p.Px(x, yy, WHITE);
                    p.Glow(x, yy, 5, L[o + 3], 0.35 * a);
                }
            });
        }

        // wind bands: 1 px streaks on tilted rings round the character, bright at the head, fading behind, the far
        // halves dimmer and behind it
        static void Wind(Painter p, uint[] C, double t, double seed, bool back)
        {
            for (int j = 0; j < 16; j++)
            {
                Rng R = NewRng(seed, j);
                double y0 = -R.Range(4, TOP - 4), r = RingR(y0) * R.Range(1.0, 1.25);
                int K = R.Int(2, 3);
                double span = R.Range(1.1, 2.4);
                double head = R.Range(0, TAU) + (TAU * K * t) / DURATION, rise = R.Range(-0.18, 0.18);
                for (double s = 0; s <= span; s += 0.45 / r)
                {
                    double th = head - s, sn = Px.Sin(th);
                    if ((sn > 0) == back) continue;
                    double k = s / span, x = Px.Cos(th) * r, y = y0 + sn * r * SY + (th - head) * rise * 4;
                    uint c = back ? (k < 0.3 ? C[3] : C[2]) : k < 0.06 && R.Next() < 2 ? WHITE : k < 0.15 ? C[5] : k < 0.4 ? C[4] : C[3];
                    if (k < 0.45) p.Dot(x, y, c);
                    else p.Blend(x, y, c, 1.6 * (1 - k));
                }
                if (!back && Px.Sin(head) > 0) p.Glow(Px.Cos(head) * r, y0 + Px.Sin(head) * r * SY, 5, C[4], 0.25);
            }
        }

        // at the feet: litter and dust swept round on the floor
        static void Swirl(Painter p, uint[] C, uint[] L, double t, double seed, bool back)
        {
            Cycle(t, DURATION, 14, seed, g => g.Range(0.6, 1.1), (age, life, R, _) =>
            {
                double k = age / life, th = R.Range(0, TAU) + R.Range(6, 9) * age, sn = Px.Sin(th);
                if ((sn > 0) == back) return;
                double rr = R.Range(10, 22) + k * 8, x = Px.Cos(th) * rr, y = -1 - k * R.Range(2, 8) + sn * rr * 0.25, s = R.Range(2, 3.5) * (0.6 + k);
                p.Disc(x, y, s, back ? C[2] : C[3], (1 - k) * Math.Min(1, k * 6) * 0.3);
            });
            for (int j = 0; j < 6; j++)
            {
                Rng R = NewRng(seed + 9, j);
                double rr = R.Range(12, 24), th0 = R.Range(0, TAU) + (TAU * 3 * t) / DURATION, span = R.Range(0.5, 1);
                for (double s = 0; s < span; s += 0.6 / rr)
                {
                    double th = th0 - s;
                    if ((Px.Sin(th) > 0) == back) continue;
                    p.Dot(Px.Cos(th) * rr, 0.5 + rr * 0.2 * Px.Sin(th), s < span * 0.4 ? C[3] : C[2], 1 - s / span);
                }
            }
        }

        readonly uint[] C, L;
        readonly double seed;

        LeafStorm(Options o)
        {
            C = Fit(o.Ramp("palette"), 6);
            L = Fit(o.Ramp("leaves"), 12);
            seed = o.Seed;
        }

        public override void DrawBack(Painter p, double t)
        {
            p.Glow(0, -30, 46, C[4], 0.12);
            p.Glow(0, -2, 26, L[2], 0.12);
            Swirl(p, C, L, t, seed * 3 + 1, true);
            Wind(p, C, t, seed * 5 + 2, true);
            Leaves(p, L, t, seed * 7 + 3, true);
        }

        public override void Draw(Painter p, double t)
        {
            Swirl(p, C, L, t, seed * 3 + 1, false);
            Wind(p, C, t, seed * 5 + 2, false);
            Leaves(p, L, t, seed * 7 + 3, false);
        }
    }
}
