// Shared by the melee effects: swings on a tilted circle (the smear, its dotted trace), floor cracks, rocks.
// The crescent and trace recipe is adapted from Sprite Fusion's open-source slash
// (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class MeleeLib
    {
        /// <summary>
        /// A swing: a circle of radius R around (cx, cy), squashed to sy across its plane and turned by rot, swept from
        /// angle a0 in direction dir; k lifts the band into a shallow cone. The slash fields (span, W, t0, sweep, hold,
        /// trail, ramp, hot, lens) are only read by Slash.
        /// </summary>
        internal sealed class Swing
        {
            public double cx, cy, R = 30, sy = 1, rot, a0, dir = 1, k;
            public double span, W, t0, sweep, hold, trail, hot;
            public uint[] ramp;
            public bool lens;

            public Swing With() => (Swing)MemberwiseClone();
        }

        internal struct Travel
        {
            public double x, y, dx, dy, a;
        }

        internal delegate void SmearFn(double u, double r, Painter.Out o, double a);

        /// <summary>A swing on radius R whose arc passes through (x, y) heading along angle th, that point `at` of the way along span.</summary>
        public static Swing Through(double x, double y, double th, double dir, double R, double span, double at = 0.55)
        {
            double a = th - (dir * Math.PI) / 2;
            return new Swing { cx = x - Px.Cos(a) * R, cy = y - Px.Sin(a) * R, R = R, sy = 1, rot = 0, a0 = a - dir * span * at, dir = dir, span = span };
        }

        /// <summary>The screen point at plane radius r, u radians along the swing.</summary>
        public static (double x, double y) OnSwing(Swing s, double r, double u)
        {
            double a = s.a0 + s.dir * u, lx = Px.Cos(a) * r, ly = Px.Sin(a) * r * s.sy - s.k * (s.R - r), c = Px.Cos(s.rot), n = Px.Sin(s.rot);
            return (s.cx + lx * c - ly * n, s.cy + lx * n + ly * c);
        }

        /// <summary>The on-screen unit direction the swing travels at u, and the in-plane angle there.</summary>
        public static Travel TravelAt(Swing s, double u)
        {
            var (x0, y0) = OnSwing(s, s.R, u);
            var (x1, y1) = OnSwing(s, s.R, u + 0.01);
            double l = Hypot(x1 - x0, y1 - y0);
            if (l == 0 || double.IsNaN(l)) l = 1;
            return new Travel { x = x0, y = y0, dx = (x1 - x0) / l, dy = (y1 - y0) / l, a = s.a0 + s.dir * u };
        }

        /// <summary>The plane radius and angle under a screen point (inverts OnSwing, cone included).</summary>
        public static (double r, double a) Plane(Swing s, double x, double y)
        {
            double c = Px.Cos(s.rot), n = Px.Sin(s.rot), dx = x - s.cx, dy = y - s.cy, lx = dx * c + dy * n, ly = -dx * n + dy * c;
            if (s.k == 0 || double.IsNaN(s.k)) return (Hypot(lx, ly / s.sy), Atan2(ly / s.sy, lx));
            double m = ly + s.k * s.R, A = s.sy * s.sy - s.k * s.k, B = 2 * m * s.k, Cc = lx * lx * s.sy * s.sy + m * m;
            double r = (-B + Math.Sqrt(B * B + 4 * A * Cc)) / (2 * A);
            return (r, Atan2((m - s.k * r) / s.sy, lx));
        }

        /// <summary>Shade a swing's band from u0 to u1, plane radius R + pad down to R - wMax - 1. half: 0 all, -1 far, 1 near.</summary>
        public static void Smear(Painter p, Swing s, double u0, double u1, double wMax, SmearFn fn, double half = 0, double pad = 1)
        {
            if (u1 - u0 < 0.005) return;
            double c = Px.Cos(s.rot), n = Px.Sin(s.rot), A = s.R + pad + 1, Bn = A * s.sy + s.k * (wMax + 2);
            double hx = Hypot(A * c, Bn * n) + 1, hy = Hypot(A * n, Bn * c) + 1, rIn = s.R - wMax - 1;
            double over = Math.Abs(n) * A + s.k * (wMax + 2) + 2, y0 = half > 0 ? s.cy - over : s.cy - hy, y1 = half < 0 ? s.cy + over : s.cy + hy;
            p.Shade(s.cx - hx, y0, s.cx + hx, y1, (x, y, o) =>
            {
                var (r, a) = Plane(s, x, y);
                if (r > s.R + pad || r < rIn) return 0;
                if (half != 0 && (Px.Sin(a) < 0 ? -1 : 1) != half) return 0;
                double u = (s.dir * (a - s.a0)) % TAU;
                if (u < 0) u += TAU;
                while (u < u0) u += TAU;
                if (u > u1) return 0;
                fn(u, r, o, a);
                return 0;
            });
        }

        /// <summary>Dots along a swing at plane radius r from u0 to u1, every `step` px, alpha a(u).</summary>
        public static void Trace(Painter p, Swing s, double r, double u0, double u1, Func<double, uint> c, Func<double, double> a, double half = 0, double step = 0.6)
        {
            double du = step / Math.Max(4, r);
            for (double u = u0; u <= u1; u += du)
            {
                double g = s.a0 + s.dir * u;
                if (half != 0 && (Px.Sin(g) < 0 ? -1 : 1) != half) continue;
                var (x, y) = OnSwing(s, r, u);
                double k = a(u);
                if (k > 0) p.Dot(x, y, c(u), k);
            }
        }

        public static void Trace(Painter p, Swing s, double r, double u0, double u1, uint c, Func<double, double> a, double half = 0, double step = 0.6) =>
            Trace(p, s, r, u0, u1, _ => c, a, half, step);

        public static void Trace(Painter p, Swing s, double r, double u0, double u1, uint c, double a, double half = 0, double step = 0.6) =>
            Trace(p, s, r, u0, u1, _ => c, _ => a, half, step);

        /// <summary>Glow riding a swing from u0 to u1 (light along the arc).</summary>
        public static void GlowAlong(Painter p, Swing s, double r, double u0, double u1, double rad, uint c, Func<double, double> k, double half = 0, double step = 0.35)
        {
            for (double u = u0; u <= u1; u += step)
            {
                double g = s.a0 + s.dir * u;
                if (half != 0 && (Px.Sin(g) < 0 ? -1 : 1) != half) continue;
                var (x, y) = OnSwing(s, r, u);
                double kk = k(u);
                if (kk > 0.004) p.Glow(x, y, rad, c, kk);
            }
        }

        public static void GlowAlong(Painter p, Swing s, double r, double u0, double u1, double rad, uint c, double k, double half = 0, double step = 0.35) =>
            GlowAlong(p, s, r, u0, u1, rad, c, _ => k, half, step);

        /// <summary>A slash crescent along swing s (Sprite Fusion's recipe).</summary>
        public static void Slash(Painter p, Swing s, double t, double half = 0)
        {
            double u = t - s.t0;
            if (u < 0) return;
            uint[] R = s.ramp;
            int n = R.Length;
            double life = s.sweep + s.hold + s.trail, hot = s.hot;
            double head = Ease.OutExpo(Clamp(u / s.sweep)), tail = Smooth(s.sweep + s.hold, life, u), age = Clamp(u / life);
            double U = s.span * head, T = s.span * tail, W = s.W * (1 - tail * 0.55);
            if (tail < 1 && U - T > 0.01)
            {
                Smear(p, s, T, U, W, (uu, r, o, _) =>
                {
                    double v = (uu - T) / (U - T), w = W * Px.Pow(Px.Sin(Math.PI * Px.Pow(v, s.lens ? 1 : 2.2)), s.lens ? 0.75 : 0.6);
                    if (w < 0.35) return;
                    double q = (s.R - r) / w;
                    if (q < 0 || q > 1) return;
                    double ci = (q < 0.22 && age < 0.5) || q < hot * 0.7 ? 0 : Round(q * (n - 1) * 0.75 + age * 2.4 - v * 0.8 + 0.4 - hot * 3);
                    o.c = R[n - 1 - (int)Clamp(ci, 0, n - 1)];
                    o.a = Math.Min(1, v / (s.lens ? 0.1 : 0.4));
                }, half);
                GlowAlong(p, s, s.R - W * 0.4, T, U, 5 + W, R[n - 3], uu => (0.22 + hot * 0.3) * (1 - age) * ((uu - T) / (U - T)), half, 0.3);
            }
            double th = 0.7 * (1 - Clamp((u - s.sweep - s.hold) / (s.trail + 0.2)));
            if (tail > 0.02 && th > 0) Trace(p, s, s.R - 0.5, 0, T, R[2], uu => th * (0.4 + 0.6 * uu / s.span), half);
        }

        /// <summary>Where a slash's edge ends up, and which way it's travelling there.</summary>
        public static Travel SlashEnd(Swing s)
        {
            Swing e = s.With();
            e.R = s.R - s.W * 0.3;
            return TravelAt(e, s.span);
        }

        /// <summary>Shavings flung off a slash's edge, outward from its circle.</summary>
        public static void Shavings(Particles ps, Rng R, Swing s, int n, uint[] colors, double from = 0.3)
        {
            for (int i = 0; i < n; i++)
            {
                var (x, y) = OnSwing(s, s.R, s.span * R.Range(from, 1));
                double ox = x - s.cx, oy = y - s.cy, l = Hypot(ox, oy);
                if (l == 0 || double.IsNaN(l)) l = 1;
                double sp = R.Range(30, 100);
                double vx = (ox / l) * sp, vy = Math.Min(25, (oy / l) * sp) - 10, life = R.Range(0.25, 0.55);
                ps.Spawn(P.Mote, x, y, vx, vy, life, R.Pick(colors), 0.6);
            }
        }

        /// <summary>Stone shades for rocks, grit and dust from one body color.</summary>
        internal readonly struct Stone
        {
            public readonly uint light, body, shade, dust, dark;

            public Stone(uint c)
            {
                light = Mix(Scale(c, 1.35), WHITE, 0.08);
                body = c;
                shade = Scale(c, 0.64);
                dust = Mix(Scale(c, 1.2), Rgb(150, 140, 150), 0.3);
                dark = Scale(c, 0.42);
            }
        }

        static readonly uint OUTLINE = Hex("#1a1422");

        /// <summary>A tumbling rock (a Bits CHUNK): a lit side, a shaded side, an outline, fading at the end of its life.</summary>
        public static void Rock(Painter p, Bits.Bit b, Stone st)
        {
            double r = 0.6 + b.s * 0.75, fade = Math.Min(1, b.life / 0.5), lx = Px.Cos(b.a) * r * 0.4, ly = Px.Sin(b.a) * r * 0.4;
            p.Shade(b.x - r - 1, b.y - r - 1, b.x + r + 1, b.y + r + 1, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, d = Hypot(dx * 1.15, dy);
                if (d > r + 0.9) return 0;
                double lit = dx * lx + dy * ly;
                o.c = d > r ? OUTLINE : lit < -0.4 ? st.light : lit > 0.6 ? st.shade : st.body;
                o.a = fade;
                return 0;
            });
        }

        internal sealed class Crack
        {
            public double[][] pts;
            public double at;
            public bool fork;
        }

        /// <summary>Jagged cracks splitting the floor from x0. Built once from an Rng.</summary>
        public static List<Crack> Cracks(Rng R, double x0, int n, double reach, double depth = 7, double bias = 0.3)
        {
            var output = new List<Crack>();
            for (int i = 0; i < n; i++)
            {
                double side = R.Chance(0.5 + bias * 0.5) ? 1 : -1, L = reach * R.Range(0.45, 1) * (i < 2 ? 1 : 0.7);
                var pts = new List<double[]> { new[] { x0 + side * R.Range(0, 1.5), 0 } };
                double x = pts[0][0], y = 0;
                while (Math.Abs(x - x0) < L && y < depth)
                {
                    x += side * R.Range(1.5, 3.5);
                    y = Math.Min(depth, Math.Max(0, y + R.Range(-0.6, 1.6) * (Math.Abs(x - x0) > 3 ? 1 : 0.3)));
                    pts.Add(new[] { Round(x), Round(y) });
                    if (R.Chance(0.18) && pts.Count > 3 && y < depth - 2)
                    {
                        var f = new List<double[]> { new[] { Round(x), Round(y) } };
                        double fx = x, fy = y;
                        for (int j = R.Int(1, 3); j > 0; j--)
                        {
                            fx += side * R.Range(1, 3);
                            fy += R.Range(0.8, 2);
                            f.Add(new[] { Round(fx), Round(Math.Min(depth + 1, fy)) });
                        }
                        output.Add(new Crack { pts = f.ToArray(), at = Math.Abs(x - x0) / reach, fork = true });
                    }
                }
                output.Add(new Crack { pts = pts.ToArray(), at = 0, fork = false });
            }
            return output;
        }

        internal struct Spike
        {
            public double a, len, w;

            public Spike(double a, double len, double w)
            {
                this.a = a;
                this.len = len;
                this.w = w;
            }
        }

        static readonly int[] Passes = { 0, 1, 2 };

        /// <summary>An impact star: tapered spikes around (x, y), in three passes (rim, body, core) with a hot disc.</summary>
        public static void Star(Painter p, double x, double y, Spike[] spikes, double sc, double th, uint core, uint body, uint inner, uint rim)
        {
            foreach (int pass in Passes)
                foreach (Spike s in spikes)
                {
                    double c = Px.Cos(s.a), sn = Px.Sin(s.a), L = s.len * sc;
                    for (double d = 0; d <= L; d += 0.5)
                    {
                        double hw0 = s.w * th * (1 - d / L) * Math.Min(1, 0.5 + d / 3), hw = pass == 0 ? hw0 + 0.8 : pass == 1 ? hw0 : hw0 * 0.45;
                        if (pass == 2 && d > L * 0.55) break;
                        for (double o = -hw; o <= hw; o += 0.5) p.Px(x + c * d - sn * o, y + sn * d + c * o, pass == 0 ? rim : pass == 1 ? (d < L * 0.35 ? inner : body) : core);
                    }
                }
            double r = 2.4 * sc * Math.Sqrt(th);
            p.Circle(x, y, r + 0.8, inner);
            p.Circle(x, y, r, core);
        }

        /// <summary>A thin arc flying out from (x, y): a sliver at radius r around angle a, arc radians long, dithered by k.</summary>
        public static void Sliver(Painter p, double x, double y, double a, double r, double arc, uint c, double k)
        {
            double n = Math.Ceiling(r * arc * 2.2);
            for (double j = 0; j <= n; j++)
            {
                double u = j / n - 0.5, th = a + u * arc, w = 1.4 * (1 - Math.Abs(u) * 2);
                for (double o = -w * 0.5; o <= w * 0.5 + 0.01; o += 0.5) p.Dot(x + Px.Cos(th) * (r + o), y + Px.Sin(th) * (r + o), c, k);
            }
        }

        /// <summary>Part of a polyline: the first `len` px of it. Calls fn(x0, y0, x1, y1, d) for each piece.</summary>
        public static void Along(double[][] pts, double len, Action<double, double, double, double, double> fn)
        {
            double d = 0;
            for (int i = 1; i < pts.Length && d < len; i++)
            {
                double x0 = pts[i - 1][0], y0 = pts[i - 1][1], x1 = pts[i][0], y1 = pts[i][1], l = Hypot(x1 - x0, y1 - y0), f = Math.Min(1, (len - d) / l);
                fn(x0, y0, x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, d);
                d += l;
            }
        }

        /// <summary>JS rng.pick on a list that may be empty (gives null there, still drawing a number).</summary>
        public static T PickFrom<T>(Rng R, List<T> list) where T : class
        {
            int i = (int)Math.Floor(R.Next() * list.Count);
            return i < list.Count ? list[i] : null;
        }
    }
}
