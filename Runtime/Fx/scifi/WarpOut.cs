// The breakup (the outline flickering on, a white hero frame, the body bursting into bits of light) follows
// Sprite Fusion's open-source blink (https://www.spritefusion.com/pixel-art-vfx/movement), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class WarpOut : Effect
    {
        static readonly Pose SET = new Pose { ["turn"] = 1, ["crouch"] = 3, ["fx"] = -9, ["fy"] = -15, ["bx"] = 10, ["by"] = -15, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose RAISED = new Pose(SET) { ["crouch"] = 0, ["fx"] = -11, ["fy"] = -29, ["bx"] = 12, ["by"] = -29 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(-0.3, Poses.READY),
                new PoseKey(0.1, SET),
                new PoseKey(0.42, RAISED, "snap"),
                new PoseKey(2.6, RAISED),
            },
        };

        // the beats: it charges, flashes white, breaks up into the beam, the beam lifts away
        const double FLASH = 0.42, BREAK0 = 0.5, BREAK1 = 1.1, LIFT = 1.3, GONE = 1.62, TOP = -128;

        public static readonly Module Module = new Module("warp-out", "pixelfx/fx/scifi/warp-out", 128, 168, 64, 128, 2.6, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0b1030", "#152a63", "#1d4f9e", "#2582d2", "#36b9ee", "#8ae8ff", "#d4fbff", "#ffffff" },
                ["seed"] = 4,
            },
            (o, e) => new WarpOut(o, e), act: Act, actor: 2,
            actorAlpha: (t, o) => t < FLASH ? 1 : 0)
        {
            Title = "Warp out",
            Desc = "The hero's outline flickers on, they flash white and break into light from the head down, streaming up a beam that lifts off the floor and pinches shut.",
            Tags = new[] { "scifi", "teleport", "warp", "beam", "despawn", "leave", "one-shot" },
            Bg = "ground",
        };

        sealed class Bit
        {
            public double x, y, x0, y0, vx, vy, tr, j;
            public uint c;
            public bool free, rim;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Bit> bits = new List<Bit>();
        readonly List<int[]> edge = new List<int[]>();
        double t;
        bool flashed, lifted;

        WarpOut(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            Actor A = o.Actor;
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            // the body: every pixel, its own color, whether it's on the edge, when it lets go (head first)
            bool On(int x, int y) => x >= 0 && y >= 0 && x < A.W && y < A.H && A.Mask.Data[y * A.W + x] != 0;
            for (int my = -1; my <= A.H; my++)
                for (int mx = -1; mx <= A.W; mx++)
                {
                    if (On(mx, my))
                    {
                        double x = A.X + mx, y = A.Y + my, tr = BREAK0 + ((double)my / A.H) * (BREAK1 - BREAK0 - 0.12) + R.Range(0, 0.12);
                        bool rim = !On(mx - 1, my) || !On(mx + 1, my) || !On(mx, my - 1) || !On(mx, my + 1);
                        uint px = A.Pixels[my * A.W + mx];
                        bits.Add(new Bit { x = x, y = y, x0 = x, y0 = y, vx = 0, vy = 0, tr = tr, c = px != 0 ? px : C[3], free = false, j = R.Next(), rim = rim });
                    }
                    else if (On(mx - 1, my) || On(mx + 1, my) || On(mx, my - 1) || On(mx, my + 1)) edge.Add(new[] { A.X + mx, A.Y + my });
                }
        }

        // the beam's half width and how far its foot has lifted off the floor
        double Width() => t < BREAK0 ? 0 : t < LIFT ? 2 + 7 * Ease.OutCubic(Clamp((t - BREAK0) / 0.35)) : Lerp(9, 0.6, Ease.InCubic(Clamp((t - LIFT) / (GONE - LIFT))));
        double Foot() => t < LIFT ? 0 : Lerp(0, TOP, Ease.InQuad(Clamp((t - LIFT) / (GONE - LIFT))));

        public override void Update(double dt)
        {
            t += dt;
            // charging: motes gather to the body, the floor target closes in
            if (t < FLASH - 0.05 && R.Chance(0.7))
            {
                double a = R.Range(0, TAU), D = R.Range(26, 48), T = R.Range(0.16, 0.26), v = (D * 3) / (Exp(3 * T) - 1), cy = -19;
                ps.Spawn(P.Streak, Px.Cos(a) * D, cy + Px.Sin(a) * D * 0.8, -Px.Cos(a) * v, -Px.Sin(a) * v * 0.8, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (t >= FLASH && !flashed)
            {
                flashed = true;
                lights.Add(0, -20, 80, C[4], 0.9, 0.5);
                lights.Add(0, -20, 30, WHITE, 0.8, 0.12);
                for (int i = 0; i < 14; i++) ps.Spawn(P.Star, R.Range(-14, 14), -R.Range(2, 40), R.Range(-20, 20), -R.Range(10, 40), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 1.8));
            }
            // the body lets go bit by bit: a little burst outward, then hauled up the beam, drawn in to its middle
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                if (t < b.tr) continue;
                if (!b.free)
                {
                    b.free = true;
                    double ox = b.x + 0.5, oy = b.y + 19;
                    b.vx = ox * R.Range(1.5, 4) + R.Range(-10, 10);
                    b.vy = oy * R.Range(0.5, 1.5) - R.Range(10, 40);
                }
                b.vy -= 560 * dt;
                b.vx += (-(b.x + 0.5) * 14 - b.vx * 3) * dt;
                b.x += b.vx * dt;
                b.y += b.vy * dt;
                if (b.y < TOP + 2) bits.RemoveAt(i);
            }
            if (t >= LIFT && !lifted)
            {
                lifted = true;
                lights.Add(0, -2, 40, C[4], 0.7, 0.3);
                for (int i = 0; i < 22; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(80, 220);
                    ps.Spawn(P.Spark, R.Range(-3, 3), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.55));
                }
                for (int i = 0; i < 16; i++) ps.Spawn(P.Star, R.Range(-10, 10), -R.Range(4, 70), R.Range(-6, 6), -R.Range(10, 30), R.Range(0.5, 0.95), R.Chance(0.4) ? WHITE : C[6], R.Range(0.6, 1.6), R.Range(0, 0.3));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // the floor target: a ring closing in on the feet, then the beam's foot glowing
            double k = Clamp(t / FLASH), fade = 1 - Smooth(LIFT, LIFT + 0.6, t);
            if (fade > 0) Sim.Band(p, 0, 0, Lerp(30, 14, Ease.OutCubic(k)), 0.16, 1.5, (0.4 + 0.6 * k) * fade, new[] { C[3], C[4], C[5], C[6] }, g => Px.Sin(g) < 0);
            // the beam: thin bright walls, a glassy inside, from its foot to the sky
            double w = Width(), f = Foot();
            if (w > 0.3 && t < GONE)
                for (int y = (int)Math.Floor(TOP); y < f + (t < LIFT ? 0 : 0); y++)
                {
                    if (y >= f && t >= LIFT) break;
                    for (double x = Math.Floor(-w) - 1; x <= Math.Ceiling(w); x++)
                    {
                        double q = Math.Abs(x + 0.5) / w;
                        if (q > 1 + 1 / w) continue;
                        if (w < 2.2) p.Px(x, y, q < 0.7 ? WHITE : C[6]);
                        else if (q > 1) p.Dot(x, y, C[4], 0.5);
                        else if (q > 1 - 1.2 / w) p.Px(x, y, C[6]);
                        else p.Blend(x + 0.5, y + 0.5, C[5], 0.12 + 0.25 * q * q);
                    }
                    if ((y & 7) == 0) p.Glow(0, y, Math.Min(w * 2.5 + 6, y - TOP + 2), C[4], 0.28);
                }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            double k = Clamp(t / FLASH), fade = 1 - Smooth(LIFT, LIFT + 0.6, t);
            if (fade > 0) Sim.Band(p, 0, 0, Lerp(30, 14, Ease.OutCubic(k)), 0.16, 1.5, (0.4 + 0.6 * k) * fade, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) >= 0);
            // charging: the outline flickers on round the hero, denser as it builds
            if (t < FLASH)
            {
                double fr = Math.Floor(t * 24);
                foreach (int[] xy in edge)
                    if (Rand(xy[0], xy[1], fr) < 0.25 + 0.7 * k) p.Px(xy[0], xy[1], k > 0.7 && Rand(xy[0], xy[1], fr, 1) < 0.5 ? WHITE : C[5]);
                p.Glow(0, -20, 26, C[4], 0.4 * k);
            }
            // the body, still held together: white in the flash, then cooling to the beam's color as it waits its turn
            foreach (Bit b in bits)
            {
                if (b.free || t < FLASH) continue;
                // white in the flash, then light with the body's own shading under it, a bright rim, a rolling scanline
                double a = t - FLASH;
                bool scan = ((b.y + Math.Floor(t * 40)) % 4 + 4) % 4 == 0;
                uint c = a < 0.07 ? WHITE : a < 0.16 ? C[6] : b.rim ? C[6] : Mix(b.c, scan ? C[6] : C[5], scan ? 0.6 : 0.45);
                p.Px(b.x, b.y, b.tr - t < 0.06 ? WHITE : c);
            }
            // the bits on their way up: streaks, longer the faster they go, cooling as they climb
            foreach (Bit b in bits)
            {
                if (!b.free) continue;
                double sp = Math.Abs(b.vy), L = Math.Min(8, sp * 0.02), u = Clamp((b.y - TOP) / (b.y0 - TOP));
                uint head = u > 0.75 ? WHITE : u > 0.4 ? C[6] : C[5];
                // one in three draws a streak, the rest a single spark, so the stream reads as light, not static
                if (L >= 1 && b.j < 0.33) p.Line(b.x, b.y, b.x - b.vx * 0.01, b.y + L, s => s < 0.25 ? head : s < 0.6 ? C[5] : C[3]);
                else if (b.j < 0.75 || u > 0.6) p.Px(b.x, b.y, b.j > 0.9 ? WHITE : head);
            }
            if (t >= FLASH && t < FLASH + 0.12) Sim.Sparkle(p, 0, -20, 10 * (1 - (t - FLASH) / 0.12), C[6], C[5], 0.6);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
