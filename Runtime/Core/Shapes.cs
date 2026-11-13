using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>Point lists effects keep reaching for: lightning paths, stars, polygons, trails. A port of pixelfx's src/core/shapes.js.</summary>
    public static class Shapes
    {
        /// <summary>
        /// A jagged lightning path from (x0, y0) to (x1, y1) by midpoint displacement. jag is how far a midpoint may move sideways,
        /// as a share of the segment's length.
        /// </summary>
        public static double[][] Bolt(Rng r, double x0, double y0, double x1, double y1, double jag = 0.22, int depth = 5)
        {
            var pts = new List<double[]> { new[] { x0, y0 }, new[] { x1, y1 } };
            for (int d = 0; d < depth; d++)
            {
                var next = new List<double[]> { pts[0] };
                for (int i = 1; i < pts.Count; i++)
                {
                    double ax = pts[i - 1][0], ay = pts[i - 1][1], bx = pts[i][0], by = pts[i][1], dx = bx - ax, dy = by - ay, L = Hypot(dx, dy);
                    double off = (r.Next() * 2 - 1) * jag * L;
                    double n = L != 0 && !double.IsNaN(L) ? L : 1; // JS: L || 1
                    next.Add(new[] { (ax + bx) / 2 + (-dy / n) * off, (ay + by) / 2 + (dx / n) * off });
                    next.Add(pts[i]);
                }
                pts = next;
            }
            return pts.ToArray();
        }

        /// <summary>Forks off a bolt path: count branches, each up to len px long.</summary>
        public static double[][][] Forks(Rng r, double[][] path, int count = 3, double len = 20, double jag = 0.3, double spread = 0.9)
        {
            var o = new double[count][][];
            for (int k = 0; k < count; k++)
            {
                int i = 1 + (int)Math.Floor(r.Next() * (path.Length - 2));
                double x = path[i][0], y = path[i][1], px = path[i - 1][0], py = path[i - 1][1];
                double side = r.Next() < 0.5 ? -1 : 1;
                double a = Px.Atan2(y - py, x - px) + side * spread * (0.4 + 0.6 * r.Next());
                double l = len * (0.4 + 0.6 * r.Next());
                o[k] = Bolt(r, x, y, x + Px.Cos(a) * l, y + Px.Sin(a) * l, jag, 3);
            }
            return o;
        }

        /// <summary>A star: n points between radii r1 (tips) and r0 (inner corners).</summary>
        public static double[][] Star(double cx, double cy, double r0, double r1, int n = 5, double rot = -Math.PI / 2)
        {
            var pts = new double[n * 2][];
            for (int i = 0; i < n * 2; i++)
            {
                double a = rot + (i * Math.PI) / n, r = i % 2 != 0 ? r0 : r1;
                pts[i] = new[] { cx + Px.Cos(a) * r, cy + Px.Sin(a) * r };
            }
            return pts;
        }

        /// <summary>A regular polygon with n sides.</summary>
        public static double[][] Ngon(double cx, double cy, double r, int n = 6, double rot = 0)
        {
            var pts = new double[n][];
            for (int i = 0; i < n; i++) pts[i] = new[] { cx + Px.Cos(rot + (i * TAU) / n) * r, cy + Px.Sin(rot + (i * TAU) / n) * r };
            return pts;
        }

        /// <summary>Positions of pos(t - i * dt) for i = 0..n-1: a moving thing's trail, newest first.</summary>
        public static T[] Trail<T>(Func<double, T> pos, double t, int n, double dt)
        {
            var o = new T[n];
            for (int i = 0; i < n; i++) o[i] = pos(Math.Max(0, t - i * dt));
            return o;
        }
    }
}
