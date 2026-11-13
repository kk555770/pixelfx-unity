// Shared by the arcane effects: a dithered light ramp, closed-form motes and streaks for loops, crackling
// arcs, floor rings and an original rune alphabet. Files starting with _ are helpers, not effects.
// The dithered ramp (tone) is adapted from Sprite Fusion's open-source black hole
// (https://www.spritefusion.com/pixel-art-vfx/black-hole), and the arc drawing from its energy
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class ArcaneLib
    {
        static readonly double[] BAYER = Array.ConvertAll(new[] { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }, v => (v + 0.5) / 16);
        static readonly int[][] CROSS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        static readonly IReadOnlyDictionary<char, uint> NoPal = new Dictionary<char, uint>();

        /// <summary>The 4x4 ordered-dither threshold (0..1) of the pixel cell holding (x, y).</summary>
        public static double Bayer(double x, double y) => BAYER[((ToInt32(Math.Floor(y)) & 3) << 2) | (ToInt32(Math.Floor(x)) & 3)];

        /// <summary>
        /// Light intensity I (0..1) to a step of ramp R (dark to bright), dithering between neighbouring steps so a
        /// gradient reads smooth at 1x; below lo it thins out through the dither and returns 0 (draw nothing).
        /// </summary>
        public static uint Tone(uint[] R, double I, double x, double y, double lo = 0.06, double sharp = 0)
        {
            double b = Bayer(x, y);
            if (I < lo && b > I / lo) return 0;
            int n = R.Length - 1;
            double f = Clamp(I) * n;
            double i = Math.Floor(f);
            // sharp (0..1) narrows the dithered zone between two steps, so broad areas stay clean and only edges dither
            double q = sharp > 0 ? Clamp((f - i - 0.5) / (1 - sharp) + 0.5) : f - i;
            if (q > b) i = Math.Min(n, i + 1);
            return R[(int)i];
        }

        /// <summary>A drifting mote at (x, y) moving (vx, vy) px/s, drawn like the sim's MOTE: bright head, darker tail, light.</summary>
        public static void Mote(Painter p, double x, double y, double vx, double vy, uint c, double size = 1, double k = 1, double glow = 0.4)
        {
            double sp = Hypot(vx, vy);
            if (sp == 0 || double.IsNaN(sp)) sp = 1;
            double ux = vx / sp, uy = vy / sp, len = Math.Min(16, 0.5 + sp * 0.026) * size, a = k < 0.35 ? k / 0.35 : 1;
            if (len > 1.2) p.Line(x - ux * len, y - uy * len, x - ux * len * 0.35, y - uy * len * 0.35, Scale(c, 0.55), a);
            p.Line(x - ux * len * 0.4, y - uy * len * 0.4, x, y, c, a);
            if (glow > 0) p.Glow(x, y, 2.5 + size, c, glow * k);
        }

        /// <summary>Energy rushing in, drawn like the sim's STREAK: a white tip, a colored body, a dim dotted tail.</summary>
        public static void Streak(Painter p, double x, double y, double vx, double vy, uint c, double a = 1, double maxLen = 7, double glow = 0.4)
        {
            double sp = Hypot(vx, vy);
            if (sp == 0 || double.IsNaN(sp)) sp = 1;
            double ux = vx / sp, uy = vy / sp, L = Clamp(sp / 30, 1, maxLen);
            for (double d = L; d >= 0; d -= 0.5) p.Dot(x - ux * d, y - uy * d, d < 1 ? WHITE : d < L * 0.5 ? c : Scale(c, 0.6), (d < L * 0.5 ? 1 : 1.2 - d / L) * a);
            if (glow > 0) p.Glow(x, y, 4, c, glow * a);
        }

        public sealed class ArcPath
        {
            public double[][] pts;
            public double w;
        }

        public struct ArcCols
        {
            public uint core, body, halo, glow;
        }

        /// <summary>
        /// A crackling arc from (x0, y0) to (x1, y1): a jagged main path plus forks. r is an Rng, so
        /// re-seed it every couple of frames for flicker.
        /// </summary>
        public static List<ArcPath> Arc(Rng r, double x0, double y0, double x1, double y1, double jag = 0.3, int forks = 1, double forkLen = 0.5)
        {
            double L = Hypot(x1 - x0, y1 - y0);
            int depth = (int)Math.Max(1, Math.Min(5, Round(Px.Log2(L / 1.5))));
            double[][] main = Shapes.Bolt(r, x0, y0, x1, y1, jag, depth);
            var o = new List<ArcPath> { new ArcPath { pts = main, w = 1 } };
            for (int k = 0; k < forks && main.Length > 3; k++)
            {
                if (r.Next() > 0.7) continue;
                int i = 1 + (int)Math.Floor(r.Next() * (main.Length - 2));
                double x = main[i][0], y = main[i][1], px = main[i - 1][0], py = main[i - 1][1];
                double a = Atan2(y - py, x - px) + (r.Next() < 0.5 ? -1 : 1) * (0.4 + r.Next() * 0.6);
                double l = L * forkLen * (0.4 + 0.6 * r.Next()) * (1 - (double)i / main.Length);
                if (l > 1.5) o.Add(new ArcPath { pts = Shapes.Bolt(r, x, y, x + Px.Cos(a) * l, y + Px.Sin(a) * l, jag * 1.1, Math.Max(1, depth - 2)), w = 0.55 });
            }
            return o;
        }

        /// <summary>
        /// Draw arc paths the way Sprite Fusion draws its bolts: a dithered halo while it's fresh, a body beside the
        /// core, a white core, light along the path. k: 1 fresh .. 0 gone.
        /// </summary>
        public static void DrawArc(Painter p, List<ArcPath> paths, ArcCols cols, double k = 1, double glowK = 0.5)
        {
            void Steps(Action<double, double, ArcPath, int, bool> fn)
            {
                foreach (ArcPath path in paths)
                    for (int i = 0; i < path.pts.Length - 1; i++)
                    {
                        double ax = path.pts[i][0], ay = path.pts[i][1], bx = path.pts[i + 1][0], by = path.pts[i + 1][1], l = Math.Max(1, Hypot(bx - ax, by - ay));
                        for (double d = 0; d <= l; d += 0.5) fn(ax + ((bx - ax) * d) / l, ay + ((by - ay) * d) / l, path, i, d == 0);
                    }
            }
            if (k > 0.45)
                Steps((x, y, path, i, first) =>
                {
                    if (path.w >= 1)
                        foreach (int[] v in CROSS)
                            p.Dot(x + v[0] * 1.5, y + v[1] * 1.5, cols.halo, (k - 0.45) * 1.6);
                });
            if (k > 0.25)
                Steps((x, y, path, i, first) =>
                {
                    if (path.w > 0.4)
                    {
                        p.Dot(x + 1, y, cols.body);
                        p.Dot(x, y + 1, cols.body);
                    }
                });
            Steps((x, y, path, i, first) =>
            {
                p.Px(x, y, path.w >= 1 && k > 0.3 ? cols.core : cols.body);
                if (first && i % 3 == 0) p.Glow(x, y, (path.w >= 1 ? 9 : 5) * (0.5 + k * 0.5), cols.glow, glowK * k * path.w);
            });
        }

        /// <summary>
        /// A ring lying on the floor (or any tilted plane): centre (cx, cy), radius r, squashed by sy, 1 px thick at
        /// any size. c and a may be functions of the angle g around it (sin(g) > 0 is the near side, towards the viewer).
        /// keep(g) picks which part to draw.
        /// </summary>
        public static void FloorRing(Painter p, double cx, double cy, double r, double sy, Func<double, uint> c, Func<double, double> a, Func<double, bool> keep = null)
        {
            if (r < 0.5) return;
            double ry = r * sy;
            p.Shade(cx - r - 1, cy - ry - 1, cx + r + 1, cy + ry + 1, (x, y, o) =>
            {
                double nx = (x - cx) / r, ny = (y - cy) / Math.Max(0.3, ry), d = Hypot(nx, ny);
                if (d < 1e-6) return 0;
                double g = Hypot(nx / r, ny / Math.Max(0.3, ry)) / d;
                if (Math.Abs(d - 1) / g > 0.5) return 0;
                double ang = Atan2(ny, nx);
                if (keep != null && !keep(ang)) return 0;
                o.c = c(ang);
                o.a = a(ang);
                return 0;
            });
        }

        public static void FloorRing(Painter p, double cx, double cy, double r, double sy, Func<double, uint> c, double a = 1, Func<double, bool> keep = null) =>
            FloorRing(p, cx, cy, r, sy, c, g => a, keep);

        // An original rune alphabet: angular 3 x 5 glyphs that stay readable standing upright at 1x.
        public static readonly string[][] GLYPHS =
        {
            new[] { ".#.", "#.#", ".#.", ".#.", "#.#" },
            new[] { "#.#", "#.#", ".#.", "#.#", "#.#" },
            new[] { "##.", "#.#", "##.", "#..", "#.." },
            new[] { "#..", "#.#", "###", "#.#", "..#" },
            new[] { ".##", "#..", ".#.", "..#", "##." },
            new[] { "###", ".#.", "###", ".#.", ".#." },
            new[] { "#.#", "###", "#.#", "..#", "..#" },
            new[] { ".#.", ".##", ".#.", "##.", ".#." },
        };

        /// <summary>
        /// Rune i standing on (x, y): its bottom row sits on y, centred on x. c body color, a alpha (dithered), under an
        /// optional darker color drawn a pixel lower so the glyph has a little thickness.
        /// </summary>
        public static void Glyph(Painter p, int i, double x, double y, uint c, double a = 1, uint under = 0)
        {
            string[] g = GLYPHS[((i % GLYPHS.Length) + GLYPHS.Length) % GLYPHS.Length];
            double X = Round(x) - 1, Y = Round(y) - g.Length + 1;
            if (under != 0) p.Sprite(g, NoPal, X, Y + 1, a: a, tint: under);
            p.Sprite(g, NoPal, X, Y, a: a, tint: c);
        }

        // Larger original sigils, 5 x 7, for runes that fly or hover on their own.
        public static readonly string[][] SIGILS =
        {
            new[] { "..#..", ".#.#.", "#.#.#", "..#..", ".###.", "..#..", ".#.#." },
            new[] { "#...#", ".#.#.", "..#..", ".###.", "..#..", ".#.#.", "#...#" },
            new[] { "#....", "##...", "#.#..", "#..#.", "#.#..", "##...", "#...." },
            new[] { "..#..", "#.#.#", ".###.", "..#..", "..#..", ".#.#.", "#...#" },
            new[] { "#.#.#", "#.#.#", "#####", "..#..", "..#..", "..#..", ".###." },
            new[] { "#...#", "##.##", "#.#.#", "#...#", "#.#.#", "##.##", "#...#" },
            new[] { "..#..", ".#.#.", "#...#", ".#.#.", "..#..", "..#..", "..#.." },
            new[] { "#..#.", "#.#..", "##...", "#....", "##...", "#.#..", "#..#." },
        };

        /// <summary>Sigil i centred on (x, y) in color c, ringed by a 1 px edge in a darker color so it reads against anything.</summary>
        public static void Sigil(Painter p, int i, double x, double y, uint c, double a = 1, uint edge = 0)
        {
            string[] g = SIGILS[((i % SIGILS.Length) + SIGILS.Length) % SIGILS.Length];
            double X = Round(x) - 2, Y = Round(y) - 3;
            if (edge != 0)
                foreach (int[] v in CROSS)
                    p.Sprite(g, NoPal, X + v[0], Y + v[1], a: a, tint: edge);
            p.Sprite(g, NoPal, X, Y, a: a, tint: c);
        }

        /// <summary>A character's silhouette shaded by how light each pixel is: 1 its dark outline, 2 mid, 3 light (0 empty).</summary>
        public static byte[] Shades(Actor A)
        {
            var s = new byte[A.W * A.H];
            for (int i = 0; i < s.Length; i++)
                if (A.Mask.Data[i] != 0)
                    s[i] = A.Pixels != null ? (byte)(Luma(A.Pixels[i]) < 0.24 ? 1 : Luma(A.Pixels[i]) < 0.62 ? 2 : 3) : (byte)2;
            return s;
        }

        /// <summary>
        /// Silhouette A (an actor) squeezed to sx of its width about its middle and shifted by dx: body color on its
        /// pixels, edge color on its dark outline pixels. sh is Shades(A). The blink's squeeze, adapted from Sprite Fusion.
        /// </summary>
        public static void Squeezed(Painter p, Actor A, byte[] sh, double sx, uint body, uint edge, double a = 1, double dx = 0)
        {
            if (sx <= 0.02) return;
            double cx = A.X + A.W / 2.0, half = A.W / 2.0 + 1;
            for (int y = 0; y < A.H; y++)
                for (double x = Math.Floor(cx - half * sx); x <= Math.Ceiling(cx + half * sx); x++)
                {
                    double i = Round(cx + (x + 0.5 - cx) / sx - 0.5) - A.X;
                    int v = i < 0 || i >= A.W ? 0 : sh[y * A.W + (int)i];
                    if (v != 0) p.Dot(x + dx, A.Y + y, v == 1 ? edge : body, a);
                }
        }
    }
}
