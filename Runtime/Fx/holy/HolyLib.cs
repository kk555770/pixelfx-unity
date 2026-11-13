// The plus sign's lit and shaded bevel is adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
// Shared by the holy effects: gold, green and ash ramps, the character's silhouette and recolors, floor ovals,
// feathers of light (drawn, and simulated as they rock and sink) and plus signs.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class HolyLib
    {
        /// <summary>Holy gold, dark to bright: a plum shadow, rust, amber, gold, pale gold, cream, white.</summary>
        public static readonly string[] GOLD = { "#3b1d2a", "#7a3a2c", "#c4652e", "#f0a040", "#ffd25e", "#fff0a0", "#fffbe0", "#ffffff" };
        /// <summary>Healing green, warm: deep pine, leaf, spring green, a yellow-green highlight, cream.</summary>
        public static readonly string[] GREEN = { "#0e2a24", "#14472f", "#1d6e3a", "#2f9a45", "#5cc654", "#a0e46c", "#dcffb4", "#ffffff" };
        /// <summary>Ash: the colour of the fallen, a cool grey-violet, dark to light.</summary>
        public static readonly string[] ASH = { "#16131c", "#26222f", "#3a3546", "#524c5e", "#6e6878", "#8c8796" };

        // ---- array helpers (JS slice and pick)

        public static uint[] Slice(uint[] r, int from, int to = int.MaxValue)
        {
            to = Math.Min(to, r.Length);
            var o = new uint[Math.Max(0, to - from)];
            Array.Copy(r, from, o, 0, o.Length);
            return o;
        }

        /// <summary>JS rng.pick: one draw, and undefined (default) for an empty list.</summary>
        public static T Pick<T>(Rng R, IReadOnlyList<T> list) where T : class
        {
            int i = (int)Math.Floor(R.Next() * list.Count);
            return i < list.Count ? list[i] : null;
        }

        // ---- the character

        public sealed class Shape
        {
            public int X, Y, Top, Bottom;
            public List<int[]> Edge, Out;
            public Mask M;

            bool Inside(int i, int j) => i >= 0 && j >= 0 && i < M.W && j < M.H && M.Data[j * M.W + i] > 0;

            public bool At(double x, double y)
            {
                double i = Math.Floor(x) - X, j = Math.Floor(y) - Y;
                return i >= 0 && j >= 0 && i < M.W && j < M.H && M.Data[(int)j * M.W + (int)i] > 0;
            }

            internal static Shape Of(Actor A)
            {
                Mask m = A.Mask;
                var s = new Shape { X = A.X, Y = A.Y, M = m, Edge = new List<int[]>(), Out = new List<int[]>(), Top = A.Y, Bottom = A.Y + m.H };
                for (int j = -1; j <= m.H; j++)
                    for (int i = -1; i <= m.W; i++)
                    {
                        int nb = (s.Inside(i - 1, j) ? 1 : 0) + (s.Inside(i + 1, j) ? 1 : 0) + (s.Inside(i, j - 1) ? 1 : 0) + (s.Inside(i, j + 1) ? 1 : 0);
                        if (s.Inside(i, j))
                        {
                            if (nb < 4) s.Edge.Add(new[] { A.X + i, A.Y + j });
                        }
                        else if (nb != 0) s.Out.Add(new[] { A.X + i, A.Y + j });
                    }
                return s;
            }
        }

        static readonly ConditionalWeakTable<Mask, Shape> SHAPES = new ConditionalWeakTable<Mask, Shape>();

        /// <summary>
        /// The actor's silhouette as local points, cached per mask: Edge (its own rim pixels), Out (the 1 px ring just
        /// outside it), At(x, y) (inside?), Top and Bottom (y of the first row and one past the last).
        /// </summary>
        public static Shape ShapeOf(Actor A)
        {
            if (SHAPES.TryGetValue(A.Mask, out Shape s) && s.X == A.X && s.Y == A.Y) return s;
            s = Shape.Of(A);
            SHAPES.Remove(A.Mask);
            SHAPES.Add(A.Mask, s);
            return s;
        }

        /// <summary>
        /// Draw the actor's own pixels recolored: fn(c, x, y) gives the new color for its pixel c at (x, y), or 0 to
        /// leave that pixel alone (the character shows through). a: alpha.
        /// </summary>
        public static void Recolor(Painter p, Actor A, Func<uint, int, int, uint> fn, double a = 1)
        {
            Mask m = A.Mask;
            uint[] P = A.Pixels;
            if (P == null) return;
            for (int j = 0; j < m.H; j++)
                for (int i = 0; i < m.W; i++)
                {
                    uint c = P[j * m.W + i];
                    if (c == 0) continue;
                    int x = A.X + i, y = A.Y + j;
                    uint n = fn(c, x, y);
                    if (n != 0) p.Dot(x, y, n, a);
                }
        }

        /// <summary>A color mapped onto a ramp by its brightness (a tint that keeps the character's shading).</summary>
        public static uint ToneOf(uint[] R, uint c, double lift = 0) => R[(int)Clamp(Math.Floor((Luma(c) * 1.1 + lift) * R.Length), 0, R.Length - 1)];

        // ---- floor and ovals

        /// <summary>
        /// A 1 px oval (a ring on a tilted plane): centre (cx, cy), radii rx, ry. c and a may be functions of the angle
        /// g round it (sin(g) > 0 is the near side). keep(g) picks the part to draw.
        /// </summary>
        public static void Oval(Painter p, double cx, double cy, double rx, double ry, Func<double, uint> c, Func<double, double> a, Func<double, bool> keep = null)
        {
            if (rx < 0.5) return;
            double n = Math.Ceiling(Math.Max(rx, ry) * TAU * 1.5) + 6;
            for (int j = 0; j < n; j++)
            {
                double g = (j / n) * TAU;
                if (keep != null && !keep(g)) continue;
                p.Dot(cx + Px.Cos(g) * rx, cy + Px.Sin(g) * ry, c(g), a(g));
            }
        }

        public static void Oval(Painter p, double cx, double cy, double rx, double ry, uint c, double a = 1, Func<double, bool> keep = null) => Oval(p, cx, cy, rx, ry, g => c, g => a, keep);
        public static void Oval(Painter p, double cx, double cy, double rx, double ry, Func<double, uint> c, double a = 1, Func<double, bool> keep = null) => Oval(p, cx, cy, rx, ry, c, g => a, keep);
        public static void Oval(Painter p, double cx, double cy, double rx, double ry, uint c, Func<double, double> a, Func<double, bool> keep = null) => Oval(p, cx, cy, rx, ry, g => c, a, keep);

        public static readonly Func<double, bool> FAR = g => Px.Sin(g) < 0;
        public static readonly Func<double, bool> NEAR = g => Px.Sin(g) >= 0;

        // ---- feathers

        /// <summary>
        /// A feather of light from its quill at (x, y) pointing along ang, len px long, with a rounded tip; the
        /// trailing vane, wid px wide, falls on side (1: clockwise of the shaft, -1: the other way), the leading vane
        /// is under half that. R: a ramp, dark to bright (6+ steps). a: alpha. hot 0..1 whitens it (a flash).
        /// </summary>
        public static void Feather(Painter p, double x, double y, double ang, double len, double wid, uint[] R, double a = 1, double side = 1, double hot = 0, double quill = 0.02)
        {
            if (len < 1.5 || a <= 0.02) return;
            int n = R.Length;
            double ca = Px.Cos(ang), sa = Px.Sin(ang), x1 = x + ca * len, y1 = y + sa * len, pad = wid + 1.2;
            bool h = hot > 0.5;
            uint edge = h ? R[n - 3] : R[n - 5], lead = h ? WHITE : R[n - 2];
            uint[] B = h ? new[] { WHITE, WHITE, R[n - 2] } : new[] { R[n - 2], R[n - 3], R[n - 4] };
            p.Each(Math.Min(x, x1) - pad, Math.Min(y, y1) - pad, Math.Max(x, x1) + pad, Math.Max(y, y1) + pad, (px, py, sx, sy) =>
            {
                double dx = px - x, dy = py - y, u = (dx * ca + dy * sa) / len;
                if (u < 0 || u > 1.05) return;
                double v = (-dx * sa + dy * ca) * side, av = Math.Abs(v);
                double w = wid * Math.Sqrt(Clamp((u - quill) / 0.22)) * Math.Sqrt(Math.Max(0, 1 - Px.Pow(Math.Max(0, (u - 0.55) / 0.47), 2)));
                double lim = v < 0 ? w * 0.42 : w, fade = u < 0.72 ? 1 : 1 - (u - 0.72) * 1.6;
                if (av < 0.5 && u < 0.9)
                {
                    p.Put(sx, sy, u < 0.5 ? WHITE : B[0], a);
                    return;
                }
                if (av > lim + 0.05 || w < 0.5) return;
                if (lim - av < 1)
                {
                    if (v > 0) p.Put(sx, sy, u > 0.85 ? B[2] : edge, a * fade);
                    else p.Put(sx, sy, lead, a);
                    return;
                }
                if (u < 0.72)
                {
                    p.Put(sx, sy, u < 0.38 ? B[0] : B[1], a);
                    return;
                }
                p.BlendPx(sx, sy, B[2], a * fade);
            });
        }

        /// <summary>
        /// Loose feathers that are thrown, brake hard, then rock like a pendulum as they sink, tilting with each swing
        /// and sinking fastest through the bottom of it; on the floor they lie flat and fade.
        /// </summary>
        public sealed class Feathers
        {
            /// <summary>What add() is handed: x, y (centre), life, and any of the rest to set instead of the defaults.</summary>
            public sealed class Spec
            {
                public double x, y, life;
                public double? vx, vy, a, len, wid, delay, amp, sink;
                public bool? back;
                public int? side;
            }

            public sealed class F
            {
                public double vx, vy, a, len, wid, delay, ph, w, amp, sink, va, tilt, x, y, life, max, age;
                public bool back, landed;
                public int side;
            }

            readonly Rng R;
            readonly double floor;
            public readonly List<F> List = new List<F>();

            public Feathers(Rng rng, double floor = 0)
            {
                R = rng;
                this.floor = floor;
            }

            public void Add(Spec f)
            {
                int side = R.Sign();
                double ph = R.Range(0, TAU), w = R.Range(3.2, 4.6), amp = R.Range(6, 11), sink = R.Range(14, 24), va = R.Range(-9, 9), tilt = R.Range(-0.35, 0.35);
                List.Add(new F
                {
                    vx = f.vx ?? 0, vy = f.vy ?? 0, a = f.a ?? 0, len = f.len ?? 7, wid = f.wid ?? 2, delay = f.delay ?? 0, back = f.back ?? false,
                    side = f.side ?? side, ph = ph, w = w, amp = f.amp ?? amp, sink = f.sink ?? sink, va = va, tilt = tilt,
                    x = f.x, y = f.y, life = f.life, max = f.life, age = 0, landed = false,
                });
            }

            public int Count => List.Count;

            public void Update(double dt)
            {
                for (int i = List.Count - 1; i >= 0; i--)
                {
                    F f = List[i];
                    if (f.delay > 0)
                    {
                        f.delay -= dt;
                        continue;
                    }
                    if ((f.life -= dt) <= 0)
                    {
                        List.RemoveAt(i);
                        continue;
                    }
                    f.age += dt;
                    if (f.landed)
                    {
                        f.a += (Round(f.a / Math.PI) * Math.PI - f.a) * Math.Min(1, dt * 10);
                        continue;
                    }
                    // the throw: drag kills it fast while it spins
                    f.vx -= f.vx * 3.4 * dt;
                    f.vy -= f.vy * 3.4 * dt;
                    double s = Smooth(0.1, 0.6, f.age), ph = f.age * f.w + f.ph, c = Px.Cos(ph);
                    // the flutter: a swing across, a sink that's fastest through the bottom of each swing
                    f.x += (f.vx + Px.Sin(ph) * f.amp * s * 0.9) * dt;
                    f.y += (f.vy + f.sink * s * (c * c * 1.5 - 0.2)) * dt;
                    // it lies along its swing, tipping up at each end
                    double rest = Round(f.a / Math.PI) * Math.PI + f.tilt + Px.Sin(ph) * 0.6;
                    f.a += f.va * dt * (1 - s) + (rest - f.a) * Math.Min(1, dt * 5 * s);
                    if (f.y >= floor - 0.5)
                    {
                        f.y = floor - 0.5;
                        f.landed = true;
                        f.life = Math.Min(f.life, 0.6);
                    }
                }
            }

            /// <summary>layer: null for both, "back" or "front".</summary>
            public void Draw(Painter p, uint[] R, string layer)
            {
                foreach (F f in List)
                {
                    if (f.delay > 0 || (layer != null && (f.back ? "back" : "front") != layer)) continue;
                    double k = f.life / f.max, a = Math.Min(Math.Min(1, f.life / 0.45), f.age / 0.06), ca = Px.Cos(f.a), sa = Px.Sin(f.a);
                    Feather(p, f.x - (ca * f.len) / 2, f.y - (sa * f.len) / 2, f.a, f.len, f.wid, R, a, f.side, f.age < 0.06 ? 1 : 0, 0.2);
                    p.Glow(f.x, f.y, 4 + f.len * 0.5, R[R.Length - 3], 0.22 * a * (0.4 + 0.6 * k));
                }
            }
        }

        // ---- plus signs

        /// <summary>
        /// A plus sign of light centred on (x, y): arm length L (1..4) and thickness th (1 or 3): a white heart and
        /// spine, colored arms lit on the top and left, shaded on the bottom and right. R: a ramp, dark to bright.
        /// </summary>
        public static void Plus(Painter p, double x, double y, double L, double th, uint[] R, double a = 1, bool glint = false)
        {
            int n = R.Length, w = th > 1 ? 1 : 0;
            double X = Round(x), Y = Round(y);
            bool Ins(double i, double j) => (Math.Abs(i) <= w && Math.Abs(j) <= L) || (Math.Abs(j) <= w && Math.Abs(i) <= L);
            for (double j = -L - 1; j <= L + 1; j++)
                for (double i = -L - 1; i <= L + 1; i++)
                {
                    if (!Ins(i, j))
                    {
                        if (w != 0 && (Ins(i - 1, j) || Ins(i + 1, j) || Ins(i, j - 1) || Ins(i, j + 1))) p.Dot(X + i, Y + j, R[Math.Max(0, n - 6)], a * 0.9);
                        continue;
                    }
                    uint c;
                    if (w == 0) c = i == 0 && j == 0 ? WHITE : Math.Abs(i) + Math.Abs(j) >= L ? R[n - 3] : R[n - 2];
                    else if (i == 0 && j == 0) c = WHITE;
                    else if (i == 0 || j == 0) c = Math.Abs(i) + Math.Abs(j) < L ? R[n - 2] : R[n - 3];
                    else c = i < 0 || j < 0 ? R[n - 3] : R[n - 5];
                    if (glint && ((i == 0 && j == -L) || (i == -L && j == 0))) c = WHITE;
                    p.Dot(X + i, Y + j, c, a);
                }
        }
    }
}
