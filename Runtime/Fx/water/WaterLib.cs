// Shared by the water effects: the water ramp, droplets that fly over a water surface seen at a low angle and
// land with rings, ripple trains, lacy foam, foam billows, wet stone.
// The droplet sprites (a pixel, a shaded 2x2, a round bead with a highlight, each trailing a tail along its
// motion) are adapted from Sprite Fusion's open-source blood (https://www.spritefusion.com/pixel-art-vfx/blood),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class WaterLib
    {
        /// <summary>Water, deep to white: deep color only in shadow, tinted middles, white crests.</summary>
        public static readonly string[] WATER = { "#0a1630", "#123063", "#1b5596", "#2a7fc4", "#45a9e3", "#86d3f5", "#cdf1ff", "#ffffff" };
        /// <summary>Wet stone for props (lips, basins, vents), outline to highlight.</summary>
        public static readonly string[] ROCK = { "#141219", "#221f2b", "#34303f", "#4a4558", "#655f74", "#8a8498" };

        public static uint[] Rock() => Ramp(ROCK);

        /// <summary>
        /// A droplet at (x, y) moving at (vx, vy) px/s: s 1 is a pixel, 2 a shaded 2x2, 3 a round bead with a
        /// highlight; a tail trails back along its motion, longer the faster it goes. C: an 8-step water ramp.
        /// </summary>
        public static void Bead(Painter p, double x, double y, double vx, double vy, double s, uint[] C, double a = 1)
        {
            double sp = Hypot(vx, vy);
            if (sp == 0 || double.IsNaN(sp)) sp = 1;
            double ux = vx / sp, uy = vy / sp, tail = Math.Min(5, sp * 0.011);
            if (tail >= 1) p.Line(x - ux * (tail + 1), y - uy * (tail + 1), x - ux * 1.2, y - uy * 1.2, u => u < 0.45 ? C[3] : s > 1 ? C[5] : C[4], a);
            if (s >= 3)
            {
                p.Circle(x, y, 1.75, C[4], a);
                p.Circle(x - 0.25, y - 0.25, 1.15, C[6], a);
                p.Dot(x - 0.7, y - 0.7, WHITE, a);
            }
            else if (s >= 2)
            {
                double X = Math.Floor(x), Y = Math.Floor(y);
                p.Dot(X, Y, WHITE, a);
                p.Dot(X + 1, Y, C[6], a);
                p.Dot(X, Y + 1, C[5], a);
                p.Dot(X + 1, Y + 1, C[4], a);
            }
            else p.Dot(x, y, sp > 160 ? C[6] : C[5], a);
        }

        public sealed class Drop
        {
            public double x, z, h, vx, vz, vh, s = 1, delay, life = 4, max;
            public bool small;
        }

        /// <summary>
        /// Droplets in flight over a flat surface seen at a low angle: x across, z into the screen (negative is far),
        /// h height above the surface; on screen they sit at (x, z * sy - h). Gravity pulls h down; when a drop
        /// reaches the surface it calls onLand(drop) and is gone.
        /// </summary>
        public sealed class Drops
        {
            public readonly double sy, g, drag;
            public Action<Drop> onLand;
            public readonly List<Drop> list = new List<Drop>();

            public Drops(double sy = 0.22, double g = 820, double drag = 0.35, Action<Drop> onLand = null)
            {
                this.sy = sy;
                this.g = g;
                this.drag = drag;
                this.onLand = onLand;
            }

            public void Add(Drop d)
            {
                d.max = d.life;
                list.Add(d);
            }

            public int Count => list.Count;

            public void Update(double dt)
            {
                double k = drag * dt;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Drop d = list[i];
                    if (d.delay > 0)
                    {
                        d.delay -= dt;
                        continue;
                    }
                    if ((d.life -= dt) <= 0)
                    {
                        list.RemoveAt(i);
                        continue;
                    }
                    d.vh -= g * dt;
                    d.vx -= d.vx * k;
                    d.vz -= d.vz * k;
                    d.vh -= d.vh * k * 0.5;
                    d.x += d.vx * dt;
                    d.z += d.vz * dt;
                    d.h += d.vh * dt;
                    if (d.h <= 0 && d.vh < 0)
                    {
                        d.h = 0;
                        list.RemoveAt(i);
                        onLand?.Invoke(d);
                    }
                }
            }

            /// <summary>Draw the drops behind z = 0 (layer -1), in front of it (1), or all (0).</summary>
            public void Draw(Painter p, uint[] C, int layer)
            {
                foreach (Drop d in list)
                {
                    if (d.delay > 0 || (layer < 0 && d.z >= 0) || (layer > 0 && d.z < 0)) continue;
                    double x = d.x, y = d.z * sy - d.h;
                    Bead(p, x, y, d.vx, d.vz * sy - d.vh, d.s, C, Math.Min(1, d.life / 0.2));
                    p.Glow(x, y, 2.5 + d.s, C[5], 0.1 + 0.05 * d.s);
                }
            }
        }

        /// <summary>
        /// A ripple on a surface seen at a low angle: a train of n crests gap px apart around (cx, cy), squashed by sy,
        /// the outer crest brightest. k 0..1+ strength. half: 0 the whole ring, -1 the far half, 1 the near half.
        /// hot: a white crest. glint: a white catch-light on the near side. wob: px the crest wanders in and out.
        /// </summary>
        public static void Ring(Painter p, double cx, double cy, double r, double sy, double k, uint[] C, int n = 2, double gap = 3, double half = 0, bool hot = false, double glint = 0, double seed = 0, double wob = 0)
        {
            if (k <= 0.02 || r < 0.5) return;
            for (int j = 0; j < n; j++)
            {
                double rr = r - j * gap;
                if (rr < 0.6) break;
                double kj = k * (1 - j * 0.42), m = Math.Max(6, Math.Ceiling(rr * TAU * 1.3)), need = 1.15 - 2.3 * Math.Min(1, kj * 1.25);
                for (int i = 0; i < m; i++)
                {
                    double a = (i / m) * TAU, s = Px.Sin(a);
                    bool near = s > 0;
                    if ((half > 0 && !near) || (half < 0 && near)) continue;
                    // as it weakens the crest breaks into glinting dashes (whole pixels, no dither), fixed to the ring
                    if (Noise(a * 4.7 + seed * 13.1 + j * 5.3, seed) * 0.65 + Noise(a * 11.3 + seed * 7.7, seed + 3) * 0.35 < need) continue;
                    // the near side and the side toward the light (upper left) read brighter
                    double lit = kj * (near ? 1 : 0.7) * (0.88 - 0.12 * Px.Cos(a));
                    uint c = hot && near && j == 0 ? WHITE : C[lit > 0.7 ? 6 : lit > 0.38 ? 5 : 4];
                    double ra = wob != 0 && !double.IsNaN(wob) ? rr + wob * Noise(a * 2.6 + seed * 1.9 + j, seed + 11) : rr;
                    double x = cx + Px.Cos(a) * ra, y = cy + s * ra * sy;
                    p.Px(x, y, c);
                    if (j == 0) p.Light(x, y, C[4], 0.3 * lit);
                }
            }
            if (glint > 0 && half >= 0)
            {
                double x = cx + Px.Cos(2.05) * r, y = cy + Px.Sin(2.05) * r * sy;
                p.Px(x, y, WHITE);
                p.Glow(x, y, 3 + glint * 2, C[5], 0.35 * glint);
            }
        }

        /// <summary>Ripples that spread and fade: Add(x, z, r0, r1, life, ...); Update(dt); Draw(p, C, half) with half as in Ring().</summary>
        public sealed class Ripples
        {
            sealed class Q
            {
                public double x, z, r0, r1, life, age, k, gap, hot, glint, seed;
                public int n;
            }

            readonly double sy;
            readonly List<Q> list = new List<Q>();
            double seq;

            public Ripples(double sy = 0.22) => this.sy = sy;

            public void Add(double x, double z, double r0, double r1, double life, int n = 2, double gap = 3, double k = 1, double hot = 0.1, double glint = 0, double delay = 0) =>
                list.Add(new Q { x = x, z = z, r0 = r0, r1 = r1, life = life, age = -delay, k = k, n = n, gap = gap, hot = hot, glint = glint, seed = (seq = seq + 1) });

            public int Count => list.Count;

            public void Update(double dt)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                    if ((list[i].age += dt) >= list[i].life) list.RemoveAt(i);
            }

            public void Draw(Painter p, uint[] C, double half = 0)
            {
                foreach (Q q in list)
                {
                    if (q.age < 0) continue;
                    double u = q.age / q.life, r = q.r0 + (q.r1 - q.r0) * (1 - Px.Pow(1 - u, 2.6)), k = q.k * Px.Pow(1 - u, 1.7);
                    Ring(p, q.x, q.z * sy, r, sy, k, C, n: q.n, gap: q.gap * (0.55 + 0.6 * u), half: half, hot: q.age < q.hot, glint: q.glint * k, seed: q.seed);
                }
            }
        }

        /// <summary>
        /// Lacy foam floating on the surface around (cx, cy), over an rx x ry patch: the crests of a noise field make
        /// a white net with holes, thinning out as k falls (1 thick, 0 gone). t drifts it; seed picks the pattern.
        /// </summary>
        public static void Lace(Painter p, double cx, double cy, double rx, double ry, double k, uint[] C, double t, double seed = 0)
        {
            if (k <= 0.01 || rx < 1) return;
            double sy = ry / rx;
            p.Shade(cx - rx, cy - ry, cx + rx, cy + ry, (x, y, o) =>
            {
                double dx = (x - cx) / rx, dy = (y - cy) / ry, d = dx * dx + dy * dy;
                if (d > 1) return 0;
                double u = (x - cx) * 0.36, v = ((y - cy) / sy) * 0.36;
                double n = Noise2(u + seed * 7.13, v - t * 0.35, seed), m = Noise2(u * 2.2 - t * 0.3, v * 2.2 + seed * 3.1, seed + 9);
                double net = 1 - Math.Abs(n) * 4.6 + m * 0.3, need = 1.02 - k * (1.15 - d * 0.9);
                if (net < need) return 0;
                o.c = net > need + 0.4 ? WHITE : C[6];
                return 0;
            });
        }

        /// <summary>
        /// Mist: soft puffs with no hard edge (alpha falls off to the rim), lit on top, that swell, drift on a slowing
        /// push and thin away. Add(x, y, vx, vy, r0, r1, life, color, a, delay); Update(dt); Draw(p, haze).
        /// </summary>
        public sealed class Mist
        {
            sealed class M
            {
                public double x, y, vx, vy, r0, r1, life, max, a, delay;
                public uint c;
            }

            readonly List<M> list = new List<M>();

            public void Add(double x, double y, double vx, double vy, double r0, double r1, double life, uint c, double a = 0.25, double delay = 0) =>
                list.Add(new M { x = x, y = y, vx = vx, vy = vy, r0 = r0, r1 = r1, life = life, max = life, c = c, a = a, delay = delay });

            public int Count => list.Count;

            public void Update(double dt)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    M m = list[i];
                    if (m.delay > 0)
                    {
                        m.delay -= dt;
                        continue;
                    }
                    if ((m.life -= dt) <= 0)
                    {
                        list.RemoveAt(i);
                        continue;
                    }
                    m.vx -= m.vx * 2.4 * dt;
                    m.vy -= m.vy * 2.4 * dt + 7 * dt;
                    m.x += m.vx * dt;
                    m.y += m.vy * dt;
                }
            }

            public void Draw(Painter p, uint haze)
            {
                foreach (M m in list)
                {
                    if (m.delay > 0) continue;
                    double age = 1 - m.life / m.max, r = m.r0 + (m.r1 - m.r0) * (1 - Px.Pow(1 - age, 2.2));
                    double a = m.a * Math.Min(1, age / 0.12) * Px.Pow(1 - age, 1.4);
                    if (a < 0.01) continue;
                    p.Disc(m.x, m.y, r, m.c, (d, g) => a * (1 - d * d) * (1 - d * 0.5));
                    p.Disc(m.x - r * 0.2, m.y - r * 0.32, r * 0.55, WHITE, (d, g) => a * 0.25 * (1 - d) * (1 - d));
                    if (haze != 0) p.Glow(m.x, m.y, r * 1.5, haze, (0.16 * a) / m.a * Math.Min(1, m.a * 3));
                }
            }
        }

        /// <summary>A billow of churning foam: a shadowed underside, a white body, a lit cap. a &lt; 1 dithers it away.</summary>
        public static void Billow(Painter p, double x, double y, double r, uint[] C, double a = 1)
        {
            if (r < 0.9)
            {
                p.Dot(x, y, C[6], a);
                return;
            }
            p.Circle(x + r * 0.12, y + r * 0.2, r, C[4], a);
            p.Circle(x, y, r * 0.84, C[6], a);
            if (r > 1.6) p.Circle(x - r * 0.3, y - r * 0.32, r * 0.46, WHITE, a);
        }

        /// <summary>A pebble or rock: lit upper left, shaded lower right, a dark rim. ST: a 6-step stone ramp (ROCK).</summary>
        public static void Pebble(Painter p, double x, double y, double r, uint[] ST)
        {
            p.Shade(x - r - 1, y - r - 1, x + r + 1, y + r + 1, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, d = Hypot(dx * 1.1, dy);
                if (d > r + 0.8) return 0;
                double lit = (dx + dy) / Math.Max(0.5, r);
                o.c = d > r ? ST[0] : lit < -0.7 ? ST[5] : lit < 0.2 ? ST[4] : lit < 0.8 ? ST[3] : ST[2];
                return 0;
            });
        }
    }
}
