// The flame tongue and fire puff recipes are adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), and the bubble from its open-source bubbles
// (https://www.spritefusion.com/pixel-art-vfx/bubbles), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>
    /// The critters' kit. A creature is built in a small offscreen grid from shaded parts, given a 1 px outline (picked per side,
    /// so it can be darker in shadow and break into a highlight), then put on the painter, opaque or translucent. Pure loops also
    /// get loop-safe noise, springs, closed-form flight with a floor, and the toolkit's own particle looks for particles whose
    /// state is a function of time.
    /// </summary>
    internal static class CrittersLib
    {
        /// <summary>
        /// The loop time for this critter: seeds other than 1 start it at a different point in its loop, so a crowd of the same
        /// critter doesn't move in lockstep.
        /// </summary>
        public static double Shift(double t, double D, double seed = 1) => Wrap(t + (seed - 1) * 0.618 * D, D);

        /// <summary>A curve through keyframes [[x, value], ...] that doesn't wrap: held flat before the first and after the last.</summary>
        public static double Curve(double x, double[][] list)
        {
            if (x <= list[0][0]) return list[0][1];
            for (int i = 1; i < list.Length; i++)
            {
                double x0 = list[i - 1][0], v0 = list[i - 1][1], x1 = list[i][0], v1 = list[i][1];
                if (x <= x1)
                {
                    double span = x1 - x0;
                    double u = (x - x0) / (span != 0 && !double.IsNaN(span) ? span : 1);
                    return v0 + (v1 - v0) * u * u * (3 - 2 * u);
                }
            }
            return list[list.Length - 1][1];
        }

        /// <summary>Smooth noise in -1..1 that repeats every D seconds: F bumps a loop (a whole number), off shifts it along.</summary>
        public static double Lnoise(double t, double D, double F, double off = 0, double seed = 0) => TileNoise((t / D) * F + off, 0.5, F, 0, seed);

        /// <summary>A damped spring's swing after a kick: 1 at age 0, ringing at hz, dying at rate damp.</summary>
        public static double Spring(double age, double hz, double damp) => age < 0 ? 0 : Exp(-age * damp) * Px.Cos(age * hz * TAU);

        public sealed class Flight
        {
            public double x, y, vx, vy, at, lx;
            public bool landed;
        }

        /// <summary>A ballistic body thrown from (x, y) at (vx, vy) px/s under gravity g, stopping on the floor y = floor.</summary>
        public static Flight Toss(double x, double y, double vx, double vy, double age, double g, double floor = 0)
        {
            double tl = g > 0 ? (-vy + Math.Sqrt(Math.Max(0, vy * vy + 2 * g * (floor - y)))) / g : double.PositiveInfinity;
            if (age < tl) return new Flight { x = x + vx * age, y = y + vy * age + 0.5 * g * age * age, vx = vx, vy = vy + g * age, landed = false, at = tl, lx = x + vx * tl };
            return new Flight { x = x + vx * tl, y = floor, vx = 0, vy = 0, landed = true, at = tl, lx = x + vx * tl };
        }

        public sealed class Bounced
        {
            public double x, y, vx, vy;
            public int hops;
            public bool rest;
        }

        /// <summary>
        /// A ballistic body thrown from (x, y) at (vx, vy) under gravity g that bounces on the floor y = floor, keeping e of its speed
        /// each bounce (and 70% of its run), until it rests.
        /// </summary>
        public static Bounced Bounce(double x, double y, double vx, double vy, double age, double g, double floor = 0, double e = 0.45)
        {
            int hops = 0;
            for (; hops < 6; hops++)
            {
                Flight f = Toss(x, y, vx, vy, age, g, floor);
                if (!f.landed) return new Bounced { x = f.x, y = f.y, vx = f.vx, vy = f.vy, hops = hops, rest = false };
                double vl = vy + g * f.at;
                age -= f.at;
                x = f.lx;
                y = floor - 0.01;
                vx *= 0.7;
                vy = -vl * e;
                if (Math.Abs(vy) < 25) break;
            }
            return new Bounced { x = x, y = floor - 0.25, vx = 0, vy = 0, hops = hops, rest = true };
        }

        /// <summary>What a Grid shade function may set besides the color: alpha and tag.</summary>
        public sealed class GOut
        {
            public double a = 1;
            public int t;
        }

        /// <summary>A creature-sized canvas in local coordinates: cells [x0, x0 + w) x [y0, y0 + h), each a color and an alpha.</summary>
        public sealed class Grid
        {
            public readonly int x0, y0, w, h;
            public readonly uint[] c;
            public readonly float[] a;
            public readonly byte[] tag;
            public byte[] d;
            readonly GOut o = new GOut();

            public Grid(double x0, double y0, int w, int h)
            {
                this.x0 = (int)Math.Floor(x0);
                this.y0 = (int)Math.Floor(y0);
                this.w = w;
                this.h = h;
                c = new uint[w * h];
                a = new float[w * h];
                tag = new byte[w * h];
            }

            public int Idx(double x, double y)
            {
                double ix = Math.Floor(x) - x0, iy = Math.Floor(y) - y0;
                return ix >= 0 && iy >= 0 && ix < w && iy < h ? (int)iy * w + (int)ix : -1;
            }

            /// <summary>Paint a cell: color c, alpha a (below 1 is drawn translucent), tag (any 0..255 label for later passes).</summary>
            public void Set(double x, double y, uint col, double al = 1, int tg = 0)
            {
                int i = Idx(x, y);
                if (i >= 0 && col != 0)
                {
                    c[i] = col;
                    a[i] = (float)al;
                    tag[i] = (byte)tg;
                }
            }

            public uint Get(double x, double y)
            {
                int i = Idx(x, y);
                return i < 0 ? 0 : c[i];
            }

            /// <summary>fn(x, y, out) at each cell center in the box returns a color (or 0 to leave it); it may set out.a and out.t.</summary>
            public void Shade(double x0, double y0, double x1, double y1, Func<double, double, GOut, uint> fn)
            {
                for (double y = Math.Floor(y0); y <= Math.Floor(y1); y++)
                for (double x = Math.Floor(x0); x <= Math.Floor(x1); x++)
                {
                    o.a = 1;
                    o.t = 0;
                    uint col = fn(x + 0.5, y + 0.5, o);
                    if (col != 0) Set(x, y, col, o.a, o.t);
                }
            }

            public void Ellipse(double cx, double cy, double rx, double ry, uint col, double al = 1) => Ellipse(cx, cy, rx, ry, (nx, ny) => col, al);

            /// <summary>Filled ellipse; c may be fn(nx, ny) of the normalized offset (-1..1), returning a color.</summary>
            public void Ellipse(double cx, double cy, double rx, double ry, Func<double, double, uint> col, double al = 1)
            {
                if (rx <= 0 || ry <= 0) return;
                Shade(cx - rx, cy - ry, cx + rx, cy + ry, (x, y, oo) =>
                {
                    double nx = (x - cx) / rx, ny = (y - cy) / ry;
                    if (nx * nx + ny * ny > 1) return 0;
                    oo.a = al;
                    return col(nx, ny);
                });
            }

            public void Line(double x0, double y0, double x1, double y1, uint col, double al = 1) => Line(x0, y0, x1, y1, u => col, al);

            /// <summary>1 px line; c may be fn(u).</summary>
            public void Line(double x0, double y0, double x1, double y1, Func<double, uint> col, double al = 1)
            {
                double n = Math.Max(1, Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))));
                for (double i = 0; i <= n; i++) Set(x0 + ((x1 - x0) * i) / n, y0 + ((y1 - y0) * i) / n, col(i / n), al);
            }

            /// <summary>A line of discs, r fn(u): tails, tentacles, legs. c is fn(u, v) with v 0 center .. 1 edge.</summary>
            public void Tube(double[][] pts, Func<double, double> r, Func<double, double, uint> col, double al = 1)
            {
                for (int k = 1; k < pts.Length; k++)
                {
                    double ax = pts[k - 1][0], ay = pts[k - 1][1], bx = pts[k][0], by = pts[k][1], L = Math.Max(1, Hypot(bx - ax, by - ay));
                    for (double s = 0; s <= L; s += 0.5)
                    {
                        double u = (k - 1 + s / L) / (pts.Length - 1), rr = r(u);
                        double x = ax + ((bx - ax) * s) / L, y = ay + ((by - ay) * s) / L;
                        if (rr < 0.75)
                        {
                            Set(x, y, col(u, 0), al);
                            continue;
                        }
                        Shade(x - rr, y - rr, x + rr, y + rr, (px, py, oo) =>
                        {
                            double dd = Hypot(px - x, py - y) / rr;
                            oo.a = al;
                            return dd > 1 ? 0 : col(u, dd);
                        });
                    }
                }
            }

            public void Poly(double[][] pts, uint col, double al = 1) => Poly(pts, (x, y) => col, al);

            /// <summary>Filled polygon (even-odd); c may be fn(x, y).</summary>
            public void Poly(double[][] pts, Func<double, double, uint> col, double al = 1)
            {
                double px0 = double.PositiveInfinity, py0 = double.PositiveInfinity, px1 = double.NegativeInfinity, py1 = double.NegativeInfinity;
                foreach (double[] pt in pts)
                {
                    px0 = Math.Min(px0, pt[0]);
                    px1 = Math.Max(px1, pt[0]);
                    py0 = Math.Min(py0, pt[1]);
                    py1 = Math.Max(py1, pt[1]);
                }
                int n = pts.Length;
                Shade(px0, py0, px1, py1, (x, y, oo) =>
                {
                    bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                        if (yi > y != yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
                    }
                    oo.a = al;
                    return inside ? col(x, y) : 0;
                });
            }

            /// <summary>How deep each filled cell sits: 1 on the edge (next to an empty cell, 4-neighbour), 2 one in, ... up to max.</summary>
            public byte[] Depths(int max = 4)
            {
                var dd = new byte[w * h];
                var q = new List<int>();
                for (int i = 0; i < w * h; i++)
                {
                    if (c[i] == 0) continue;
                    int x = i % w, y = i / w;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1 || c[i - 1] == 0 || c[i + 1] == 0 || c[i - w] == 0 || c[i + w] == 0)
                    {
                        dd[i] = 1;
                        q.Add(i);
                    }
                }
                for (int k = 0; k < q.Count; k++)
                {
                    int i = q[k], v = dd[i];
                    if (v >= max) continue;
                    foreach (int j in new[] { i - 1, i + 1, i - w, i + w })
                        if (j >= 0 && j < w * h && c[j] != 0 && dd[j] == 0)
                        {
                            dd[j] = (byte)(v + 1);
                            q.Add(j);
                        }
                }
                for (int i = 0; i < w * h; i++)
                    if (c[i] != 0 && dd[i] == 0)
                        dd[i] = (byte)max;
                d = dd;
                return dd;
            }

            public int Depth(double x, double y)
            {
                int i = Idx(x, y);
                return i < 0 || d == null ? 0 : d[i];
            }

            public void Outline(uint col, double al = 1) => Outline((x, y, nx, ny) => col, al);

            /// <summary>
            /// Wrap everything drawn so far in a 1 px outline (4-neighbour). fn(x, y, nx, ny) with (nx, ny) the outward direction there
            /// picks the color per side; 0 leaves a gap.
            /// </summary>
            public void Outline(Func<int, int, int, int, uint> col, double al = 1)
            {
                uint[] src = (uint[])c.Clone();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (src[i] != 0) continue;
                    bool l = x > 0 && src[i - 1] != 0, r = x < w - 1 && src[i + 1] != 0, u = y > 0 && src[i - w] != 0, dn = y < h - 1 && src[i + w] != 0;
                    if (!(l || r || u || dn)) continue;
                    uint cc = col(x0 + x, y0 + y, (l ? 1 : 0) - (r ? 1 : 0), (u ? 1 : 0) - (dn ? 1 : 0));
                    if (cc != 0)
                    {
                        c[i] = cc;
                        a[i] = (float)al;
                        tag[i] = 255;
                    }
                }
            }

            /// <summary>Rework every filled cell: fn(x, y, color, alpha, tag) returns its new color (0 erases it).</summary>
            public void Recolor(Func<int, int, uint, double, int, uint> fn)
            {
                for (int i = 0; i < w * h; i++)
                {
                    if (c[i] == 0) continue;
                    c[i] = fn(x0 + i % w, y0 + i / w, c[i], a[i], tag[i]);
                }
            }

            /// <summary>Put the grid on the painter, moved by (dx, dy): opaque cells dithered by fade, translucent ones blended.</summary>
            public void Draw(Painter p, double fade = 1, double dx = 0, double dy = 0)
            {
                double X = x0 + Round(dx), Y = y0 + Round(dy);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    uint col = c[i];
                    if (col == 0) continue;
                    double al = a[i];
                    if (al >= 0.999) p.Dot(X + x, Y + y, col, fade);
                    else p.Blend(X + x, Y + y, col, al * fade);
                }
            }

            public Grid Clear()
            {
                Array.Clear(c, 0, c.Length);
                Array.Clear(tag, 0, tag.Length);
                d = null;
                return this;
            }
        }

        /// <summary>
        /// The toolkit's particle looks for particles whose state is a function of time (pure loops): Begin(t), then Add for each one
        /// alive now, then Draw.
        /// </summary>
        public sealed class Swarm
        {
            public Particles ps = new Particles(new Rng(1));

            public Swarm Begin(double t)
            {
                ps.List.Clear();
                ps.T = t;
                return this;
            }

            /// <summary>The spark colors, head to tail (the JS ps.sparks = [...]).</summary>
            public uint[] Sparks
            {
                set
                {
                    var next = new Particles(new Rng(1), sparks: value) { T = ps.T };
                    next.List.AddRange(ps.List);
                    ps = next;
                }
            }

            public void Add(P k, double x, double y, double vx, double vy, double age, double life, uint col = WHITE, double size = 1, PF flag = PF.None, double seed = 0)
            {
                if (age < 0 || age >= life) return;
                ps.List.Add(new Particles.Particle { k = k, x = x, y = y, vx = vx, vy = vy, life = life - age, max = life, col = col, size = size, seed = seed, delay = 0, flag = flag });
            }

            public void Draw(Painter p, Layer layer = Layer.Both) => ps.Draw(p, layer);
        }

        // Hugo's flame layers: [width, height, ramp step] from the widest dark outer flame to the white-hot heart
        static readonly double[][] LAYERS = { new double[] { 1, 1, 6 }, new[] { 0.76, 0.86, 4 }, new[] { 0.54, 0.68, 2 }, new[] { 0.3, 0.42, 0 } };

        /// <summary>
        /// A tongue of flame standing on (x, y), h tall, w half-wide at its root, its tip swaying sx px; four nested layers down a
        /// 9-step fire ramp given bright-first (heat[0] white). n seeds its flicker; t, D keep it looping. only: draw just that layer.
        /// </summary>
        public static void Tongue(Painter p, double x, double y, double h, double w, double sx, double t, double D, double n, uint[] heat, int only = -1)
        {
            if (h < 1) return;
            double F1 = Math.Max(1, Round(9 * D)), F2 = Math.Max(1, Round(4 * D));
            for (int li = 0; li < LAYERS.Length; li++)
            {
                if (only >= 0 && li != only) continue;
                double lw = LAYERS[li][0], lh = LAYERS[li][1];
                int ri = (int)LAYERS[li][2];
                double H = h * lh;
                uint col = heat[ri];
                for (double r = 0; r <= H; r++)
                {
                    double u = r / Math.Max(1, H);
                    double half = w * lw * Px.Pow(1 - u, 0.85) * Math.Min(1, (r + 1.5) / 3.5) + Lnoise(t, D, F1, r * 0.45, n) * 0.5;
                    double cx = x + sx * u * u * lh + Lnoise(t, D, F2, r * 0.2, n + 7) * u * 1.1;
                    if (half < 0.3) continue;
                    for (double px = Round(cx - half); px <= Round(cx + half); px++) p.Px(px, Round(y - r), col);
                }
            }
        }

        /// <summary>A puff of flame k of its life left (1 new .. 0 gone), size px, cooling down heat (bright-first, 9 steps).</summary>
        public static void FirePuff(Painter p, double x, double y, double k, double size, double seed, uint[] heat)
        {
            double age = 1 - k;
            int n = heat.Length;
            double hot = Math.Min(0.999, Px.Pow(age, 0.62) + (seed % 1) * 0.1);
            int idx = (int)Math.Min(n - 2, Math.Floor(hot * (n - 1)));
            double r = size * (0.55 + 0.45 * Math.Min(1, age * 7)) * (1 - age * 0.55);
            uint c = heat[idx];
            if (idx >= n - 2)
            {
                p.Disc(x, y, r, c, Math.Min(1, k * 3) * 0.85);
                return;
            }
            p.Circle(x, y, r, heat[Math.Min(n - 1, idx + 2)]);
            p.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, c);
            if (r > 1.6) p.Circle(x, y - r * 0.9, r * 0.42, c);
            if (idx <= 3 && r > 1.8) p.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, heat[Math.Max(0, idx - 1)]);
            if (idx < 6) p.Glow(x, y, r * 2.4 + 2, c, 0.18 * k);
        }

        /// <summary>
        /// A bubble of radius r: a rim lit top-left and shaded bottom-right, a faint fill, a white glint, light.
        /// cols: [highlight, lit, rim, shade].
        /// </summary>
        public static void Bubble(Painter p, double cx, double cy, double r, uint[] cols, double a = 1, double wob = 0)
        {
            uint HI = cols[0], LIT = cols[1], RIM = cols[2], SHADE = cols[3];
            if (r < 0.9)
            {
                p.Dot(cx, cy, r < 0.7 ? RIM : LIT, a);
                return;
            }
            if (r < 1.5)
            {
                double px = Math.Floor(cx), py = Math.Floor(cy);
                p.Dot(px, py, HI, a);
                p.Dot(px + 1, py, LIT, a);
                p.Dot(px, py + 1, RIM, a);
                p.Dot(px + 1, py + 1, SHADE, a);
                p.Glow(cx + 0.5, cy + 0.5, 3, LIT, 0.12 * a);
                return;
            }
            double rx = r * (1 + wob), ry = r * (1 - wob);
            for (double y = Math.Floor(cy - ry - 1); y <= Math.Ceiling(cy + ry + 1); y++)
            for (double x = Math.Floor(cx - rx - 1); x <= Math.Ceiling(cx + rx + 1); x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry, d = Math.Sqrt(dx * dx + dy * dy) * r;
                if (d > r + 0.5) continue;
                double l = (-dx - dy) / 1.4142135623730951 / Math.Max(0.01, d / r);
                if (d > r - 0.6) p.Dot(x, y, l > 0.45 ? LIT : l < -0.55 ? SHADE : RIM, a);
            }
            double hx = Round(cx - rx * 0.45), hy = Round(cy - ry * 0.45);
            p.Dot(hx, hy, HI, a);
            if (r >= 2.5)
            {
                p.Dot(hx + 1, hy, LIT, a);
                p.Dot(hx, hy + 1, LIT, a);
            }
            if (r >= 2) p.Dot(Round(cx + rx * 0.5), Round(cy + ry * 0.5), LIT, a);
            p.Glow(cx, cy, r * 2.4 + 2, RIM, 0.14 * a);
        }

        /// <summary>A soft shadow: an ellipse blended over what's there, a alpha at the center fading to nothing at the rim.</summary>
        public static void Shadow(Painter p, double cx, double cy, double rx, double ry, uint c, double a)
        {
            if (rx <= 0 || ry <= 0 || a <= 0) return;
            for (double y = Math.Floor(cy - ry); y <= Math.Ceiling(cy + ry); y++)
            for (double x = Math.Floor(cx - rx); x <= Math.Ceiling(cx + rx); x++)
            {
                double ex = (x + 0.5 - cx) / rx, ey = (y + 0.5 - cy) / ry, d = ex * ex + ey * ey;
                if (d < 1) p.Blend(x, y, c, a * Px.Pow(1 - d, 0.7));
            }
        }

        /// <summary>The JS Math.min/Math.max over several numbers (NaN if any is NaN).</summary>
        public static double Min(params double[] v)
        {
            double m = double.PositiveInfinity;
            foreach (double x in v) m = Math.Min(m, x);
            return m;
        }

        public static double Max(params double[] v)
        {
            double m = double.NegativeInfinity;
            foreach (double x in v) m = Math.Max(m, x);
            return m;
        }
    }
}
