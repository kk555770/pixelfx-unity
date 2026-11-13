// The beads, drops, drying splats and crowns are
// adapted from Sprite Fusion's open-source blood (https://www.spritefusion.com/pixel-art-vfx/blood),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Bleeding : Effect
    {
        static readonly Pose CLUTCH = new Pose { ["turn"] = 0.7, ["crouch"] = 3, ["lean"] = 2, ["fx"] = 2, ["fy"] = -21, ["bx"] = 8, ["by"] = -14, ["ffx"] = 3, ["fbx"] = -3 };
        static readonly Pose BEAT_ = new Pose(CLUTCH) { ["crouch"] = 5, ["lean"] = 4, ["hy"] = 1 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, BEAT_), new PoseKey(0.18, CLUTCH), new PoseKey(1.2, CLUTCH), new PoseKey(1.2, BEAT_, "snap"), new PoseKey(1.38, CLUTCH),
                new PoseKey(2.4, CLUTCH), new PoseKey(2.4, BEAT_, "snap"),
            },
        };

        public static readonly Module Module = new Module("bleeding", "pixelfx/fx/status/bleeding", 60, 56, 30, 46, 2.4, true, 30,
            new Options { ["palette"] = new[] { "#350711", "#5e0c1b", "#931327", "#c71e33", "#f2384a", "#ff8a7a" }, ["seed"] = 5 },
            (o, e) => new Bleeding(o), act: Act, actor: 2)
        {
            Title = "Bleeding",
            Desc = "Bleeding out: beads swell at three wounds, run, let go and fall to spatter into a glossy pool that ripples; each heartbeat spurts a little and flashes the body red.",
            Tags = new[] { "status", "bleed", "blood", "dot", "debuff", "wound", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, BEAT = D / 2, G = 900;

        sealed class Wound
        {
            public double x, y0, yb, phase;
            public int mx, drops, side;
        }

        // the wounds: two on the edges of the arms and one on the front of the body. Blood runs down the body from each
        // and beads at the end of the run; drops fall in front of the body to the floor
        static readonly ConditionalWeakTable<Mask, Wound[]> WOUNDS = new ConditionalWeakTable<Mask, Wound[]>();

        static Wound[] Wounds(Actor a)
        {
            if (WOUNDS.TryGetValue(a.Mask, out Wound[] w)) return w;
            StatusShape S = Shape(a);
            int H = S.H, W = S.W;
            Wound Make(int[] at, int drops, double phase)
            {
                int mx = at[0], my = at[1], run = 1;
                while (run < 3 && S.On(mx, my + run + 1)) run++;
                return new Wound { x = a.X + mx + 0.5, mx = mx, y0 = a.Y + my, yb = a.Y + my + run + 1, drops = drops, phase = phase, side = mx < W / 2.0 ? -1 : 1 };
            }
            int[] Edge(double q, int side)
            {
                int my = RoundInt(H * q);
                int[] lr = S.Rows[my];
                return new[] { side < 0 ? lr[0] + 1 : lr[1] - 1, my };
            }
            int[] front = S.Body.Find(b => b[1] > H * 0.5 && b[1] < H * 0.62 && b[0] > W * 0.55 && b[0] < W * 0.75 && S.On(b[0], b[1] + 3)) ?? Edge(0.55, 1);
            w = new[] { Make(Edge(0.57, -1), 3, 0), Make(front, 2, 0.35), Make(Edge(0.6, 1), 2, 0.7) };
            WOUNDS.Add(a.Mask, w);
            return w;
        }

        // a drop in flight: a bead with a tail when it is fast (Hugo's drop)
        static void Drop(Painter p, uint[] C, double x, double y, double v, int s)
        {
            double tail = Math.Min(5, v * 0.012);
            if (tail >= 1) p.Line(x, y - tail, x, y - 1, s >= 2 ? C[3] : C[2]);
            if (s >= 2)
            {
                p.Px(x, y, C[5]);
                p.Px(x + 1, y, C[4]);
                p.Px(x, y + 1, C[3]);
                p.Px(x + 1, y + 1, C[2]);
            }
            else p.Px(x, y, C[4]);
        }

        // a splat where a drop lands: it spreads, throws a crown of droplets, then dries darker and fades
        static void Splat(Painter p, uint[] C, double x, double ground, double age, bool big, double seed)
        {
            double life = D * 0.85;
            if (age < 0 || age > life) return;
            double spread = Smooth(0, 0.1, age), hw = (big ? 2.2 : 1.4) * (0.5 + 0.5 * spread), fade = 1 - Smooth(life - 0.5, life, age);
            int dry = age < 0.35 ? 4 : age < 1 ? 3 : 2;
            for (double xx = Math.Floor(x - hw); xx <= Math.Ceiling(x + hw); xx++) p.Dot(xx, ground - 1, Math.Abs(xx + 0.5 - x) > hw * 0.75 ? C[dry - 1] : C[dry], fade);
            if (age < 0.3) p.Px(x - 0.5, ground - 1, C[5]);
            // the crown
            if (age < 0.4)
                for (int j = 0; j < (big ? 3 : 2); j++)
                {
                    double vx = (j % 2 != 0 ? 1 : -1) * (18 + 24 * Rand(seed, j, 1)), vy = -(40 + 40 * Rand(seed, j, 2)), tt = age;
                    double dx = vx * tt, dy = vy * tt + 0.5 * G * tt * tt;
                    if (dy <= 0) p.Px(x + dx, ground - 1 + dy, j != 0 ? C[3] : C[4]);
                }
        }

        readonly Options o;

        Bleeding(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 6);
            Actor a = o.Actor;
            Wound[] W = Wounds(a);
            // the pool under the lowest wounds: dark and glossy, a ripple where each drop lands in it
            double px = (W[0].x + W[2].x) / 2 - 1, rx = 6.5, beat = Exp(-((((t % BEAT) + BEAT) % BEAT) / 0.25));
            for (double x = Math.Floor(px - rx - 1); x <= px + rx + 1; x++)
            {
                double q = Math.Abs(x + 0.5 - px) / rx;
                if (q > 1.1) continue;
                p.Px(x, -1, q > 0.85 ? C[2] : C[1]);
                if (q < 0.8) p.Px(x, 0, q < 0.5 ? C[1] : C[0]);
                if (q < 0.45) p.Px(x, 1, C[0]);
            }
            p.Px(px - 2, -1, C[4]);
            p.Px(px - 1, -1, C[3]);
            p.Px(px + 3, -1, C[3]);
            p.Glow(px, -1, 8, C[3], 0.06 + 0.04 * beat);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 6);
            Actor a = o.Actor;
            Wound[] W = Wounds(a);
            StatusShape S = Shape(a);
            // each heartbeat flashes the body red for a moment
            double ph = ((t % BEAT) + BEAT) % BEAT, hurt = Math.Max(0, 1 - ph / 0.12);
            if (hurt > 0)
                foreach (int[] b in S.Body)
                    p.Blend(a.X + b[0], a.Y + b[1], C[4], 0.26 * hurt);
            foreach (Wound w in W)
            {
                // the wound, a dark gash with a wet lip, and the blood running down from it
                p.Px(w.x, w.y0 - 1, C[1]);
                p.Px(w.x - w.side, w.y0 - 1, C[0]);
                p.Px(w.x - w.side, w.y0, C[2]);
                for (double y = w.y0; y < w.yb; y++) p.Px(w.x, y, y == w.y0 ? C[4] : y >= w.yb - 1 ? C[3] : C[2]);
                // drops: a bead swells, sags, lets go, falls, splats
                double period = D / w.drops;
                for (int k = 0; k < w.drops; k++)
                {
                    double born = w.phase * period + k * period, age = (((t - born) % D) + D) % D, swell = period * 0.62;
                    bool big = (k + w.mx) % 2 == 0;
                    double fallH = Math.Max(1, -1 - w.yb), fall = Math.Sqrt((2 * fallH) / G), ground = 0;
                    if (age < swell)
                    {
                        // a bead gathers at the end of the run, sags and stretches
                        double u = age / swell;
                        int len = u < 0.35 ? 1 : u < 0.75 ? 2 : 3;
                        for (int j = 0; j < len; j++) p.Px(w.x, w.yb + j, j == len - 1 ? C[2] : C[3]);
                        if (len == 3) p.Px(w.x - 1, w.yb + 2, C[1]);
                        p.Px(w.x, w.yb + len - 2, u > 0.5 ? C[5] : C[4]);
                    }
                    else if (age < swell + fall)
                    {
                        double f = age - swell, y = w.yb + 2 + 0.5 * G * f * f;
                        Drop(p, C, w.x, y, G * f, big ? 2 : 1);
                    }
                    else Splat(p, C, w.x, ground, age - swell - fall, big, w.mx * 31 + k);
                }
            }
            // the heartbeat squeezes a little spurt from the first wound: droplets streak out and fall
            Wound w0 = W[0];
            if (ph < 0.5)
                for (int j = 0; j < 7; j++)
                {
                    double vx = w0.side * (18 + 42 * Rand(j, 7)), vy = -(15 + 50 * Rand(j, 8)), tt = ph - j * 0.012;
                    if (tt <= 0) continue;
                    double x = w0.x + Travel(vx, 0, 1.2, tt), y = w0.y0 + Travel(vy, G * 0.8, 0.6, tt);
                    double x2 = w0.x + Travel(vx, 0, 1.2, tt - 0.02), y2 = w0.y0 + Travel(vy, G * 0.8, 0.6, tt - 0.02);
                    if (y >= -1) continue;
                    p.Line(x2, y2, x, y, j % 3 != 0 ? C[3] : C[2]);
                    p.Px(x, y, j % 2 != 0 ? C[5] : C[4]);
                }
            p.Glow(w0.x, w0.y0, 6, C[4], 0.25 * hurt);
        }
    }
}
