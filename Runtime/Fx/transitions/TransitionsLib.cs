// Shared by the screen transitions: a 320 x 180 screen with its origin at the top-left. Each one covers the
// screen in o.color by about 45% of its duration, holds, then uncovers so the last frame is empty.
// glint() is adapted from Sprite Fusion's open-source sparkle (https://www.spritefusion.com/pixel-art-vfx/sparkle),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class TransitionsLib
    {
        public const int W = 320, H = 180;

        /// <summary>The meta every transition shares (the JS screenMeta); pass what differs. options go after color and seed.</summary>
        public static Module Screen(string file, string name, string desc, string[] tags, double duration, Options options, Func<Options, Emitter, Effect> create)
        {
            var o = new Options { ["color"] = "#0b0a10", ["seed"] = 1 };
            foreach (KeyValuePair<string, object> kv in options.All) o[kv.Key] = kv.Value;
            return new Module(file, "pixelfx/fx/transitions/" + file, W, H, 0, 0, duration, false, 30, o, create)
            {
                Title = name,
                Desc = desc,
                Tags = new[] { "transition", "screen" }.Concat(tags).Concat(new[] { "one-shot" }).ToArray(),
                Bg = "sky",
            };
        }

        /// <summary>
        /// The painter, clipped to the screen: sparks, smoke and light that fly past an edge are cut there instead of spilling
        /// outside the canvas. Every drawing call goes through it unchanged.
        /// </summary>
        public static Painter ClipTo(Painter p) => p.ClipTo(W, H);

        /// <summary>p.Rect(x, y, w, h, c) for opaque fills (the JS rect: the same cells as the painter's own rect).</summary>
        public static void Rect(Painter p, double x, double y, double w, double h, uint c) => p.Rect(x, y, w, h, c);

        /// <summary>
        /// Fill a convex polygon [[x, y], ...] row by row, clipped to the screen. c is a color or fn(x, y) -> color
        /// (0 skips the pixel); a is the alpha (dithered).
        /// </summary>
        public static void FillConvex(Painter p, IList<double[]> pts, uint c, double a = 1) => FillConvex(p, pts, (x, y) => c, a);

        public static void FillConvex(Painter p, IList<double[]> pts, Func<double, double, uint> c, double a = 1)
        {
            double y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
            foreach (double[] pt in pts)
            {
                y0 = Math.Min(y0, pt[1]);
                y1 = Math.Max(y1, pt[1]);
            }
            double r0 = Math.Max(0, Math.Ceiling(y0 - 0.5)), r1 = Math.Min(H - 1, Math.Floor(y1 - 0.5));
            int n = pts.Count;
            for (double y = r0; y <= r1; y++)
            {
                double yc = y + 0.5, xl = double.PositiveInfinity, xr = double.NegativeInfinity;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                    if (yi > yc == yj > yc) continue;
                    double x = xi + ((yc - yi) * (xj - xi)) / (yj - yi);
                    if (x < xl) xl = x;
                    if (x > xr) xr = x;
                }
                double c0 = Math.Max(0, Math.Ceiling(xl - 0.5)), c1 = Math.Min(W - 1, Math.Floor(xr - 0.5));
                for (double x = c0; x <= c1; x++)
                {
                    uint col = c(x + 0.5, yc);
                    if (col != 0) p.Put(p.CellX(x, y), p.CellY(x, y), col, a);
                }
            }
        }

        /// <summary>The part of polygon pts on the side of the line through (cx, cy) that normal (nx, ny) points to.</summary>
        public static List<double[]> HalfPlane(IList<double[]> pts, double cx, double cy, double nx, double ny)
        {
            var o = new List<double[]>();
            double Side(double[] q) => (q[0] - cx) * nx + (q[1] - cy) * ny;
            for (int i = 0; i < pts.Count; i++)
            {
                double[] a = pts[i], b = pts[(i + 1) % pts.Count];
                double sa = Side(a), sb = Side(b);
                if (sa >= 0) o.Add(a);
                if (sa >= 0 != sb >= 0)
                {
                    double k = sa / (sa - sb);
                    o.Add(new[] { a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k });
                }
            }
            return o;
        }

        /// <summary>
        /// A scalar field over the screen (the JS field()): f, g (one over the gradient's length), ny (the gradient's vertical
        /// share), lo, hi, order (the pixels from the lowest value to the highest) and Below(v).
        /// </summary>
        public sealed class Field
        {
            public readonly float[] f = new float[W * H], g = new float[W * H], ny = new float[W * H];
            public double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            public int[] order;
            float[] sorted;
            public double ox, oy;

            public Field(Func<double, double, double> fn, double gmin = 0.45, double gmax = 3)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        double v = fn(x + 0.5, y + 0.5);
                        f[y * W + x] = (float)v;
                        if (v < lo) lo = v;
                        if (v > hi) hi = v;
                    }
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int xa = Math.Max(0, x - 1), xb = Math.Min(W - 1, x + 1), ya = Math.Max(0, y - 1), yb = Math.Min(H - 1, y + 1);
                        double gx = ((double)f[y * W + xb] - f[y * W + xa]) / (xb - xa), gy = ((double)f[yb * W + x] - f[ya * W + x]) / (yb - ya);
                        double L = Hypot(gx, gy);
                        g[y * W + x] = (float)(1 / Clamp(L, gmin, gmax));
                        ny[y * W + x] = (float)(L > 1e-6 ? gy / L : 0);
                    }
                order = Enumerable.Range(0, W * H).OrderBy(i => f[i]).ToArray();
                sorted = Array.ConvertAll(order, i => f[i]);
            }

            /// <summary>The slice of order whose values are below v: pixels f &lt; v are order[0 .. Below(v)).</summary>
            public int Below(double v)
            {
                int a = 0, b = sorted.Length;
                while (a < b)
                {
                    int m = (a + b) >> 1;
                    if (sorted[m] < v) a = m + 1;
                    else b = m;
                }
                return a;
            }
        }

        /// <summary>Write whole screen pixels by index: At(i) picks pixel i = y * W + x, then Put, Add and Blend write it.</summary>
        public sealed class Pixels
        {
            readonly Painter p;
            int sx, sy;

            public Pixels(Painter p) => this.p = p;

            public void At(int i)
            {
                double x = i % W, y = i / W;
                sx = p.CellX(x, y);
                sy = p.CellY(x, y);
            }

            public void Put(uint c, double a = 1) => p.Put(sx, sy, c, a);
            public void Add(uint c, double k) => p.Add(sx, sy, c, k);
            public void Blend(uint c, double a) => p.BlendPx(sx, sy, c, a);
        }

        /// <summary>
        /// A spinning 8-ray glint at (x, y): long rays white at the heart, c1 in the middle and c2 at the tips, short
        /// diagonals, the long rays thickened near the heart, and two lights. ang spins it; a fades it (dithered).
        /// </summary>
        public static void Glint(Painter p, double x, double y, double L, double ang, uint c1, uint c2, double a = 1)
        {
            if (L < 0.8 || a <= 0) return;
            for (int i = 0; i < 8; i++)
            {
                bool diag = i % 2 == 1;
                double an = ang + (i * TAU) / 8, len = diag ? L * 0.42 : L, ca = Px.Cos(an), sa = Px.Sin(an);
                for (double d = 0; d <= len; d += 0.5)
                {
                    double u = d / len;
                    uint c = diag ? (u < 0.5 ? c1 : c2) : u < 0.3 ? WHITE : u < 0.65 ? c1 : c2;
                    p.Dot(x + ca * d, y + sa * d, c, a * (u < 0.7 ? 1 : 1 - (u - 0.7) / 0.35));
                    if (!diag && u < 0.22 && L > 4)
                    {
                        p.Dot(x + ca * d - sa, y + sa * d + ca, c1, a);
                        p.Dot(x + ca * d + sa, y + sa * d - ca, c1, a);
                    }
                }
            }
            p.Dot(x, y, WHITE, a);
            p.Glow(x, y, 4 + L * 1.4, c1, 0.5 * a);
            p.Glow(x, y, 2 + L * 0.5, WHITE, 0.4 * a);
        }

        /// <summary>Particle lives cut so nothing outlives the effect: the smaller of life and what's left before end.</summary>
        public static double LifeLeft(double life, double t, double end) => Math.Min(life, end - t);
    }
}
