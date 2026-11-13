// The bolt shape and the way a bolt is inked (a deep halo, a pale body, a white core, light along it) are
// adapted from Sprite Fusion's open-source lightning (https://www.spritefusion.com/pixel-art-vfx/lightning),
// MIT License, Copyright (c) 2026 Hugo Duprez.
//
// Shared by the lightning effects: bolts that fork and thin as they split, inked at the 2x character scale,
// thin crackles, bowed arcs, heat scorch, and trajectories for pure loops.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class LightningLib
    {
        /// <summary>Default palette, dark to bright: deep violet shadow, electric blue, icy white.</summary>
        public static readonly string[] VOLT = { "#120a33", "#2a1b78", "#4630c0", "#5a62f0", "#58a6ff", "#9ed8ff", "#e4f6ff", "#ffffff" };
        /// <summary>Plasma sparks, head to tail: cyan that reads apart from the violet bolt.</summary>
        public static readonly string[] PLASMA = { "#ffffff", "#c8f8ff", "#7ff3ff", "#1ab8e6" };
        public static readonly uint SOOT = Hex("#110c16"), SOOT2 = Hex("#1c1522");

        /// <summary>One stroke of a bolt: its points and its weight (1 the trunk, less for forks).</summary>
        public sealed class BoltPath
        {
            public double[][] pts;
            public double w;

            public BoltPath(double[][] pts, double w)
            {
                this.pts = pts;
                this.w = w;
            }
        }

        /// <summary>A bolt: its paths, the trunk first.</summary>
        public sealed class Bolt
        {
            public readonly List<BoltPath> paths = new List<BoltPath>();

            public Bolt() { }

            public Bolt(params BoltPath[] list) => paths.AddRange(list);
        }

        /// <summary>stroke()'s options; null / 0 / false mean the option is left out, as in JS.</summary>
        public sealed class StrokeOpt
        {
            public bool hero;
            public double? light, a, upTo, minY;
            public double core;
        }

        static readonly StrokeOpt NONE = new StrokeOpt();

        // JS: x || 1
        static double Or1(double x) => x != 0 && !double.IsNaN(x) ? x : 1;

        /// <summary>
        /// A bolt from (x0, y0) to (x1, y1): midpoint displacement down to ~3 px segments (jag: how far the first
        /// midpoint may swing, as a share of the length), with forks that split off along the way, each thinner (w)
        /// and forking again up to two levels. The trunk first.
        /// </summary>
        public static Bolt Zigzag(Rng R, double x0, double y0, double x1, double y1, double jag = 0.32, double forks = 0.35, double w = 1, int depth = 0)
        {
            var o = new Bolt();
            double L = Hypot(x1 - x0, y1 - y0);
            var pts = new List<double[]> { new[] { x0, y0 }, new[] { x1, y1 } };
            double off = L * jag;
            while (pts.Count < 2 + L / 3)
            {
                var nxt = new List<double[]> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    double ax = pts[i][0], ay = pts[i][1], bx = pts[i + 1][0], by = pts[i + 1][1], l = Or1(Hypot(bx - ax, by - ay)), q = R.Range(-off, off);
                    nxt.Add(new[] { (ax + bx) / 2 + (-(by - ay) / l) * q, (ay + by) / 2 + ((bx - ax) / l) * q });
                    nxt.Add(new[] { bx, by });
                }
                pts = nxt;
                off *= 0.52;
            }
            double[][] P = pts.ToArray();
            o.paths.Add(new BoltPath(P, w));
            if (depth < 2)
                for (int i = 2; i < P.Length - 3; i++)
                    if (R.Chance(forks / (1 + depth) / Math.Max(4, P.Length / 6.0)))
                    {
                        double ax = P[i][0], ay = P[i][1], bx = P[Math.Min(P.Length - 1, i + 3)][0], by = P[Math.Min(P.Length - 1, i + 3)][1];
                        double a = Atan2(by - ay, bx - ax) + R.Sign() * R.Range(0.35, 0.9), l = (L - ((double)i / P.Length) * L) * R.Range(0.25, 0.55);
                        o.paths.AddRange(Zigzag(R, ax, ay, ax + Px.Cos(a) * l, ay + Px.Sin(a) * l, jag * 1.1, forks, w * 0.55, depth + 1).paths);
                    }
            return o;
        }

        /// <summary>A bolt bowed off the straight line by bow px at its middle (negative: up for a left-to-right arc).</summary>
        public static Bolt Bowed(Rng R, double x0, double y0, double x1, double y1, double bow, double jag = 0.22, double forks = 0.4)
        {
            double L = Or1(Hypot(x1 - x0, y1 - y0)), nx = -(y1 - y0) / L, ny = (x1 - x0) / L;
            double mx = (x0 + x1) / 2 + nx * bow + R.Range(-2, 2), my = (y0 + y1) / 2 + ny * bow;
            Bolt a = Zigzag(R, x0, y0, mx, my, jag, forks), b = Zigzag(R, mx, my, x1, y1, jag, forks);
            double[][] pa = a.paths[0].pts, pb = b.paths[0].pts;
            var trunk = new double[pa.Length + pb.Length - 1][];
            Array.Copy(pa, trunk, pa.Length);
            Array.Copy(pb, 1, trunk, pa.Length, pb.Length - 1);
            var o = new Bolt(new BoltPath(trunk, 1));
            for (int i = 1; i < a.paths.Count; i++) o.paths.Add(a.paths[i]);
            for (int i = 1; i < b.paths.Count; i++) o.paths.Add(b.paths[i]);
            return o;
        }

        /// <summary>Move every point of a bolt: fn(x, y) => [x, y].</summary>
        public static Bolt Bend(Bolt b, Func<double, double, double[]> fn)
        {
            var o = new Bolt();
            foreach (BoltPath q in b.paths) o.paths.Add(new BoltPath(Array.ConvertAll(q.pts, pt => fn(pt[0], pt[1])), q.w));
            return o;
        }

        // ---- inking. Pixels are gathered in a grid at a level (higher wins), then drawn once each, so a fork's
        // halo never covers the trunk's body and overlapping passes cost nothing extra.
        const int N = 1024, HALF = 512;
        static readonly byte[] GRID = new byte[N * N];
        static readonly List<int> HIT = new List<int>();

        static void Mark(double x, double y, int lvl)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y);
            if (double.IsNaN(fx) || double.IsNaN(fy)) return; // JS: a NaN index lands nowhere
            double X = fx + HALF, Y = fy + HALF;
            if (X < 0 || Y < 0 || X >= N || Y >= N) return;
            int i = (int)Y * N + (int)X, v = GRID[i];
            if (v == 0) HIT.Add(i);
            if (lvl > v) GRID[i] = (byte)lvl;
        }

        static void Stamp(double x, double y, double r, int lvl)
        {
            if (r <= 0.5)
            {
                Mark(Round(x), Round(y), lvl);
                return;
            }
            double r2 = r * r;
            for (double py = Math.Floor(y - r); py <= Math.Ceiling(y + r); py++)
            {
                double dy = py + 0.5 - y;
                for (double px = Math.Floor(x - r); px <= Math.Ceiling(x + r); px++)
                {
                    double dx = px + 0.5 - x;
                    if (dx * dx + dy * dy <= r2) Mark(px, py, lvl);
                }
            }
        }

        /// <summary>Draw what was gathered: level n in cols[n - 1] at alpha alphas[n - 1] (dithered below 1).</summary>
        static void Flush(Painter p, uint[] cols, double[] alphas)
        {
            foreach (int i in HIT)
            {
                int v = GRID[i];
                GRID[i] = 0;
                int x = (i % N) - HALF, y = (i / N) - HALF;
                double a = alphas != null ? alphas[v - 1] : 1;
                if (a >= 1) p.Px(x, y, cols[v - 1]);
                else if (a > 0) p.Dot(x, y, cols[v - 1], a);
            }
            HIT.Clear();
        }

        static void Walk(double[][] pts, int count, Action<double, double> fn)
        {
            for (int i = 0; i < count - 1; i++)
            {
                double ax = pts[i][0], ay = pts[i][1], bx = pts[i + 1][0], by = pts[i + 1][1], l = Math.Max(1, Hypot(bx - ax, by - ay));
                for (double d = 0; d <= l; d += 0.5) fn(ax + ((bx - ax) * d) / l, ay + ((by - ay) * d) / l);
            }
        }

        /// <summary>
        /// A bolt at full power: a deep halo, a pale body, a white core on the trunk, light every few points. k is the
        /// stroke's brightness (forks drop out below 0.5, the trunk thins below 0.6), s scales the thickness.
        /// o: hero (a white frame: the body goes white), light (glow strength), a (alpha), upTo (draw the trunk only
        /// to this share of its length), core (radius of a thicker white core), minY (no glow above this y).
        /// </summary>
        public static void Stroke(Painter p, Bolt b, double k, uint[] C, double s = 1, StrokeOpt o = null)
        {
            if (k <= 0.02) return;
            o = o ?? NONE;
            double lk = o.light ?? 0.5;
            foreach (BoltPath q in b.paths)
            {
                bool trunk = q.w >= 1;
                if (!trunk && k < 0.5) continue;
                double rh = (trunk ? (k > 0.6 ? 2.1 : 1.6) : 1.1) * s, rb = (trunk ? (k > 0.6 ? 1.3 : 0.8) : 0.5) * s;
                double[][] pts = q.pts;
                int count = pts.Length;
                if (o.upTo.HasValue && trunk) count = Math.Min(pts.Length, (int)Math.Max(2, Math.Ceiling(pts.Length * o.upTo.Value)));
                Walk(pts, count, (x, y) =>
                {
                    Stamp(x, y, rh, 1);
                    Stamp(x, y, rb, trunk ? 3 : 2);
                    if (trunk && k > 0.3)
                    {
                        if (o.core != 0 && !double.IsNaN(o.core)) Stamp(x, y, o.core, 4);
                        else Mark(x, y, 4);
                    }
                });
                for (int i = 0; i < count - 1; i += 3)
                    if (!o.minY.HasValue || pts[i][1] > o.minY.Value) p.Glow(pts[i][0], pts[i][1], (trunk ? 14 : 7) * s, C[4], lk * k * q.w);
            }
            double a = o.a ?? 1;
            Flush(p, o.hero ? new[] { C[3], C[6], C[7], C[7] } : new[] { C[2], C[4], C[5], C[7] }, new[] { a, a, a, a });
        }

        static readonly int[][] CROSS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        /// <summary>
        /// A thin crackle: a 1 px core (white on the trunk while bright), a 2 px pale body, a dithered halo while k is
        /// high, light along it. For short arcs that live a frame or two.
        /// </summary>
        public static void Crackle(Painter p, Bolt b, double k, uint[] C, double glowK = 0.5, double a = 1)
        {
            if (k <= 0.02) return;
            foreach (BoltPath q in b.paths)
            {
                bool thick = q.w >= 1;
                Walk(q.pts, q.pts.Length, (x, y) =>
                {
                    if (k > 0.45 && thick)
                        foreach (int[] d in CROSS) Mark(x + d[0] * 1.5, y + d[1] * 1.5, 1);
                    if (k > 0.25 && (thick || q.w > 0.4))
                    {
                        Mark(x + 1, y, 2);
                        Mark(x, y + 1, 2);
                    }
                    Mark(x, y, thick && k > 0.3 ? 4 : 3);
                });
                for (int i = 0; i < q.pts.Length - 1; i += 3) p.Glow(q.pts[i][0], q.pts[i][1], (thick ? 9 : 5) * (0.5 + k * 0.5), C[4], glowK * k * q.w);
            }
            Flush(p, new[] { C[3], C[5], C[5], C[7] }, new[] { Math.Min(1, (k - 0.45) * 1.6) * a, a, a, a });
        }

        /// <summary>A bolt's trunk as a 1 px line in one color (leaders, afterimages, cooling channels).</summary>
        public static void Trace(Painter p, double[][] pts, uint c, double a = 1, double from = 0, double to = double.NaN)
        {
            if (double.IsNaN(to)) to = pts.Length - 1;
            for (double i = Math.Max(0, from); i < Math.Min(to, pts.Length - 1); i++)
            {
                int j = (int)i;
                p.Line(pts[j][0], pts[j][1], pts[j + 1][0], pts[j + 1][1], c, a);
            }
        }

        /// <summary>
        /// Heat left on the floor: a flat ellipse, white-hot in the middle cooling through the fire ramp to soot.
        /// T0 is the heat (1 fresh, 0 cold), fade takes the soot away.
        /// </summary>
        public static void Scorch(Painter p, double cx, double cy, double rx, double T0, double fade = 1, double ry = double.NaN)
        {
            if (double.IsNaN(ry)) ry = Math.Max(2.5, rx * 0.12);
            if (fade <= 0) return;
            uint[] F = Sim.FIRE_RAMP;
            for (double y = Math.Floor(cy); y <= cy + ry; y++)
                for (double x = Math.Floor(cx - rx); x <= Math.Ceiling(cx + rx); x++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > 1) continue;
                    double n = Rand(x, y, 41), T = T0 * (1.25 - d) - n * 0.35;
                    if (T > 0.06)
                    {
                        uint c = F[(int)Math.Min(F.Length - 1, Math.Floor((1 - Math.Min(1, T)) * (F.Length - 1)))];
                        p.Dot(x, y, c, fade);
                        if ((ToInt32(x) & 3) == 0) p.Glow(x + 0.5, y, 6, c, 0.18 * T * fade);
                    }
                    else p.Dot(x, y, d < 0.5 ? SOOT : SOOT2, (d < 0.95 - n * 0.3 ? 1 : 1 - d) * fade);
                }
        }

        /// <summary>A heat-ramp color for T (1 white-hot .. 0 cold), or 0 when it's cold.</summary>
        public static uint Heat(double T)
        {
            uint[] F = Sim.FIRE_RAMP;
            return T > 0.06 ? F[(int)Math.Min(F.Length - 1, Math.Floor((1 - Math.Min(1, T)) * (F.Length - 1)))] : 0;
        }

        /// <summary>
        /// Where a spark launched at (x, y, vx, vy) is after age seconds, stepped like the simulation's SPARK (gravity,
        /// drag, bouncing on a floor at floor), so pure loops get the same physics. Returns [x, y, vx, vy].
        /// </summary>
        public static double[] Flight(double x, double y, double vx, double vy, double age, double g = 0.45, double drag = 2.2, double floor = double.PositiveInfinity, double bounce = 0.45)
        {
            const double dt = 1.0 / 120;
            for (double s = 0; s < age; s += dt)
            {
                double h = Math.Min(dt, age - s);
                vy += Sim.GRAVITY * g * h;
                vx -= vx * drag * h;
                vy -= vy * drag * h;
                double ny = y + vy * h;
                x += vx * h;
                if (vy > 0 && y <= floor && ny >= floor)
                {
                    vy = -vy * bounce;
                    vx *= 0.7;
                    y = floor - 0.25;
                }
                else y = ny;
            }
            return new[] { x, y, vx, vy };
        }

        /// <summary>A spark drawn the way the simulation draws one (a streak stretched by speed, a hot head, light), for pure loops.</summary>
        public static void Spark(Painter p, double x, double y, double vx, double vy, double k, uint[] S)
        {
            double sp = Or1(Hypot(vx, vy)), ux = vx / sp, uy = vy / sp, len = Math.Min(9, 1 + sp * 0.022);
            uint head = k > 0.6 ? S[0] : k > 0.3 ? S[1] : S[2];
            p.Line(x - ux * len, y - uy * len, x - ux * len * 0.4, y - uy * len * 0.4, k > 0.5 ? head : S[S.Length - 1]);
            p.Line(x - ux * len * 0.45, y - uy * len * 0.45, x, y, head);
            p.Glow(x, y, 3, head, 0.45 * k);
        }

        /// <summary>A soft smoke puff at age u (0..1) of its life, drawn like the simulation's SMOKE, for pure loops.</summary>
        public static void Puff(Painter p, double x, double y, double size, double u, uint c)
        {
            double k = 1 - u, r = size * (0.65 + Math.Min(1, u * 2.5) * 0.45 + u * 0.4), a = Math.Min(1, u * 12) * Math.Min(1, k * 2.4) * 0.58;
            if (a <= 0.01) return;
            uint[] S = Sim.SMOKE_RAMP;
            uint col = Mix(c, S[(int)Math.Min(S.Length - 1, Math.Floor(u * S.Length))], u);
            p.Disc(x, y, r, col, a * 0.45);
            p.Disc(x + r * 0.05, y + r * 0.06, r * 0.74, col, a * 0.62);
            if (u < 0.5) p.Disc(x - r * 0.22, y - r * 0.3, r * 0.5, Mix(col, Hex("#ffffff"), 0.12), a * 0.4 * (1 - u * 2));
        }


        static readonly ConditionalWeakTable<Mask, double[][]> EDGES = new ConditionalWeakTable<Mask, double[][]>(), EDGES_MID = new ConditionalWeakTable<Mask, double[][]>();

        /// <summary>The silhouette's edge pixels, in local space; mid: at pixel centres (+0.5).</summary>
        public static double[][] EdgeOf(Actor A, bool mid)
        {
            var table = mid ? EDGES_MID : EDGES;
            if (table.TryGetValue(A.Mask, out double[][] e)) return e;
            Mask m = A.Mask;
            bool On(int x, int y) => x >= 0 && y >= 0 && x < m.W && y < m.H && m.Data[y * m.W + x] != 0;
            var list = new List<double[]>();
            double h = mid ? 0.5 : 0;
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++)
                    if (On(x, y) && (!On(x - 1, y) || !On(x + 1, y) || !On(x, y - 1) || !On(x, y + 1))) list.Add(new[] { A.X + x + h, A.Y + y + h });
            e = list.ToArray();
            table.Add(A.Mask, e);
            return e;
        }
    }
}
