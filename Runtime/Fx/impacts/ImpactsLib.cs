// Shared by the impacts: the hit star, a ring that breaks into flying arcs, floor cracks that glow and cool,
// standing shock fronts, soft lumpy dust rings, faceted tumbling rocks, and the stone colors they share.
// The star and flying-arc recipes are adapted from Sprite Fusion's open-source impact
// (https://www.spritefusion.com/pixel-art-vfx/impact), and the stone colors from its explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public static class ImpactsLib
    {
        public static readonly uint SOOT = Hex("#0d0a12");
        public static readonly uint OUTLINE = Hex("#1a1422");
        static readonly double LIGHT = Atan2(-1, -1);

        public readonly struct Stone
        {
            public readonly uint light, body, shade, dust, lip;

            public Stone(uint c)
            {
                light = Mix(Scale(c, 1.35), WHITE, 0.08);
                body = c;
                shade = Scale(c, 0.64);
                dust = Mix(Scale(c, 1.2), Rgb(150, 140, 150), 0.3);
                lip = Mix(Scale(c, 1.55), WHITE, 0.12);
            }
        }

        public readonly struct Spike
        {
            public readonly double a, len, w;

            public Spike(double a, double len, double w)
            {
                this.a = a;
                this.len = len;
                this.w = w;
            }
        }

        /// <summary>
        /// Spikes for a hit star: n of them round the circle, longer towards aim (fwd) and a little behind it (back).
        /// len and w: [min, max] base length and root half-width; jag scales every other spike (short teeth between).
        /// </summary>
        public static Spike[] Spikes(Rng R, int n, double aim, double len0 = 7, double len1 = 10, double w0 = 1.5, double w1 = 2.1, double fwd = 1.8, double back = 0.6, double jag = 1, double jit = 0.18)
        {
            var list = new Spike[n];
            for (int i = 0; i < n; i++)
            {
                double a = aim + (double)i / n * TAU + R.Range(-jit, jit), cb = Px.Cos(a - aim);
                double L = R.Range(len0, len1) * (1 + fwd * Px.Pow(Math.Max(0, cb), 3) + back * Px.Pow(Math.Max(0, -cb), 3)) * (i % 2 != 0 ? jag : 1);
                list[i] = new Spike(a, L, R.Range(w0, w1) * (i % 2 != 0 ? Math.Sqrt(jag) : 1));
            }
            return list;
        }

        /// <summary>The spike with its length multiplied by k (the JS mutates s.len in place).</summary>
        public static Spike Stretched(Spike s, double k) => new Spike(s.a, s.len * k, s.w);

        public static void Star(Painter p, double x, double y, Spike[] list, double sc, double th, uint[] cols, double a = 1, double heart = 2.4)
        {
            uint rim = cols[0], body = cols[1], inner = cols[2], core = cols[3];
            for (int pass = 0; pass < 3; pass++)
                foreach (Spike s in list)
                {
                    double c = Px.Cos(s.a), sn = Px.Sin(s.a), L = s.len * sc;
                    for (double d = 0; d <= L; d += 0.5)
                    {
                        if (pass == 2 && d > L * 0.55) break;
                        double hw0 = s.w * th * (1 - d / L) * Math.Min(1, 0.5 + d / 3);
                        double hw = pass == 0 ? hw0 + 0.8 : pass == 1 ? hw0 : hw0 * 0.45;
                        uint col = pass == 0 ? rim : pass == 1 ? (d < L * 0.35 ? inner : body) : core;
                        for (double o = -hw; o <= hw; o += 0.5) p.Dot(x + c * d - sn * o, y + sn * d + c * o, col, a);
                    }
                }
            double r = heart * sc * Math.Sqrt(th);
            p.Circle(x, y, r + 0.8, inner, a);
            p.Circle(x, y, r, core, a);
        }

        /// <summary>One arc of a broken ring: its centre angle, how much of its share it gives up (gap), how fast it flies out (dr), its thickness scale (w).</summary>
        public readonly struct Seg
        {
            public readonly double a, gap, dr, w;

            public Seg(double a, double gap, double dr, double w = 1)
            {
                this.a = a;
                this.gap = gap;
                this.dr = dr;
                this.w = w;
            }
        }

        /// <summary>
        /// A ring that breaks into arcs: segs evenly round the ring. brk 0 draws a whole ring th thick; as brk grows each arc shrinks,
        /// tapers at both ends into a crescent and drifts out at its own speed. c is the outer edge's color, c2 the inner's; sy squashes
        /// it onto a plane; nothing is drawn below maxY (a floor).
        /// </summary>
        public static void BrokenRing(Painter p, double x, double y, double r, double th, Seg[] segs, double brk, uint c, uint c2, double k = 1, double sy = 1, double maxY = double.PositiveInfinity)
        {
            if (r <= 0 || k <= 0) return;
            double full = TAU / segs.Length;
            foreach (Seg s in segs)
            {
                double span = full * (1 - s.gap * brk) + 0.02, rr = r * (1 + s.dr * brk), m = Math.Max(4, Math.Ceiling(rr * span * 1.8));
                for (int j = 0; j <= m; j++)
                {
                    double u = j / m, ang = s.a + (u - 0.5) * span, w = Math.Max(0.5, th * s.w * (1 - brk * Px.Pow(Math.Abs(2 * u - 1), 1.6)));
                    double ca = Px.Cos(ang), sa = Px.Sin(ang);
                    for (double o = -w / 2; o <= w / 2 + 0.01; o += 0.5)
                        if (y + sa * (rr + o) * sy < maxY) p.Dot(x + ca * (rr + o), y + sa * (rr + o) * sy, o > -0.26 ? c : c2, k);
                }
            }
        }

        public sealed class Crack
        {
            public double[][] pts;
            public double len, t0;
            public readonly List<double[][]> kids = new List<double[][]>();
        }

        static double[][] Walk(Rng R, double x, double y, double a, double L, double maxY, double jit = 0.55, double steer = 0.45, double step0 = 2, double step1 = 4, double d0 = 0)
        {
            var pts = new List<double[]> { new[] { x, y, d0 } };
            double d = 0, cur = a;
            while (d < L - 0.01)
            {
                double s = Math.Min(L - d, R.Range(step0, step1));
                cur += R.Range(-jit, jit) + (a - cur) * steer;
                x += Px.Cos(cur) * s;
                y = Clamp(y + Px.Sin(cur) * s, 0.5, maxY);
                d += s;
                pts.Add(new[] { x, y, d0 + d });
            }
            return pts.ToArray();
        }

        public static Crack[] MakeCracks(Rng R, int n, double len0, double len1, double dive, double maxY, double forks, double x00, double x01)
        {
            var output = new Crack[n];
            for (int i = 0; i < n; i++)
            {
                int side = i % 2 != 0 ? 1 : -1;
                double dip = 0.06 + Px.Pow(R.Next(), 1.7) * dive, L = R.Range(len0, len1) * (1 - 0.5 * dip / (dive + 0.06));
                double a = side > 0 ? dip : Math.PI - dip;
                double[][] pts = Walk(R, side * R.Range(x00, x01), R.Range(0.5, 1.5), a, L, maxY);
                var c = new Crack { pts = pts, len = L, t0 = R.Range(0, 0.035) };
                for (int j = 2; j < pts.Length - 1; j++)
                    if (R.Chance(forks * 0.4))
                    {
                        double fx = pts[j][0], fy = pts[j][1], fd = pts[j][2];
                        c.kids.Add(Walk(R, fx, fy, a + R.Sign() * R.Range(0.45, 1), R.Range(3, 9) * (1 - 0.5 * j / pts.Length), maxY, jit: 0.7, step0: 1.5, step1: 3, d0: fd));
                    }
                output[i] = c;
            }
            return output;
        }

        public static void DrawCracks(Painter p, Crack[] cracks, Func<Crack, double> g, Func<double, double> heat, uint[] coolingRamp, uint dark, uint lip, double k = 1)
        {
            if (k <= 0) return;
            uint Col(double d)
            {
                double h = heat(d);
                return h > 0.75 ? coolingRamp[0] : h > 0.5 ? coolingRamp[1] : h > 0.3 ? coolingRamp[2] : h > 0.12 ? coolingRamp[3] : dark;
            }
            foreach (Crack c in cracks)
            {
                double G = g(c);
                if (G <= 0) continue;
                var paths = new List<double[][]> { c.pts };
                paths.AddRange(c.kids);
                foreach (double[][] path in paths)
                    for (int i = 1; i < path.Length; i++)
                    {
                        double x0 = path[i - 1][0], y0 = path[i - 1][1], d0 = path[i - 1][2];
                        double x1 = path[i][0], y1 = path[i][1], d1 = path[i][2];
                        if (d0 >= G) break;
                        double f = d1 > G ? (G - d0) / (d1 - d0) : 1, ex = x0 + (x1 - x0) * f, ey = y0 + (y1 - y0) * f;
                        p.Line(x0, y0 + 1, ex, ey + 1, lip, u => Rand(RoundInt(x0 + (ex - x0) * u), RoundInt(y0 + (ey - y0) * u), 5) < 0.3 ? k * 0.8 : 0);
                        p.Line(x0, y0, ex, ey, u => Col(d0 + (d1 - d0) * f * u), k);
                        double h = heat(d0);
                        if (h > 0.12) p.Glow((x0 + ex) / 2, (y0 + ey) / 2, 4 + h * 4, coolingRamp[2], 0.22 * h * k);
                    }
            }
        }

        public static void FrontCrescent(Painter p, double x, int dir, double h, double w, uint[] cols, double k = 1)
        {
            if (h < 1 || w < 0.3 || k <= 0) return;
            double ry = h * 0.8, cy = -h * 0.2, rx = Math.Max(2, h * 0.55);
            double x0 = dir > 0 ? x - rx - w - 1 : x - 1, x1 = dir > 0 ? x + 1 : x + rx + w + 1;
            p.Shade(x0, -h - 1, x1, 0, (px, py, o) =>
            {
                if (py > 0) return 0;
                double ey = (py - cy) / ry;
                if (ey <= -1 || ey >= 1) return 0;
                double e2 = 1 - ey * ey, edge = rx * Math.Sqrt(e2) - rx, wt = w * Px.Pow(e2, 0.45), q = (edge - (px - x) * dir) / Math.Max(0.5, wt);
                if (q < 0 || q > 1) return 0;
                o.c = q < 0.28 ? cols[0] : q < 0.62 ? cols[1] : cols[2];
                o.a = k * (ey < -0.8 ? 0.7 : 1) * (q > 0.7 ? 1.25 - q * 0.8 : 1);
                return 0;
            });
        }

        public static void DustRing(Painter p, double cx, double cy, double r, double w, double k, uint c, double sy = 1, double ph = 0, double brk = 0, double lit = 0.35, double maxY = double.PositiveInfinity, int half = 0)
        {
            if (k <= 0.01 || r <= 0) return;
            double W = w * 1.6;
            uint lite = Mix(c, WHITE, lit);
            for (int y = Floor(cy - (r + W) * sy) - 1; y <= Math.Min(maxY - 1, Math.Ceiling(cy + (r + W) * sy) + 1); y++)
            for (int x = Floor(cx - r - W) - 1; x <= Math.Ceiling(cx + r + W) + 1; x++)
            {
                double dx = x + 0.5 - cx, dy = (y + 0.5 - cy) / sy, d = Hypot(dx, dy), g = Atan2(dy, dx);
                if ((half < 0 && dy >= 0) || (half > 0 && dy < 0)) continue;
                double lumpy = 0.6 + 0.3 * Px.Sin(5 * g + ph) + 0.18 * Px.Sin(11 * g + ph * 2.1) + 0.12 * Px.Sin(17 * g + ph * 0.7);
                double wt = w * lumpy, off = Math.Abs(d - r - w * 0.25 * Px.Sin(7 * g + ph));
                if (off >= wt) continue;
                double tear = brk > 0 ? Clamp((0.55 + 0.45 * Px.Sin(6 * g + ph * 1.7) - brk) / 0.25) : 1;
                double a = k * (1 - Px.Pow(off / wt, 2)) * tear;
                if (a > 0.01) p.Blend(x, y, dy < 0 && d > r - wt * 0.3 ? lite : c, a);
            }
        }

        public static double[] RockShape(Rng R, int n = 6)
        {
            var pts = new double[n];
            for (int i = 0; i < n; i++) pts[i] = R.Range(0.7, 1.12);
            return pts;
        }

        public static void Rock(Painter p, Emitter e, Bits.Bit b, double[] pts, Stone st)
        {
            double r = 0.75 + b.s * 0.8, fade = Math.Min(1, b.life / 0.5), ca = Px.Cos(b.a), sa = Px.Sin(b.a);
            int n = pts.Length;
            e.Shift(p, b.bornX, b.bornY);
            p.Shade(b.x - r - 2, b.y - r - 2, b.x + r + 2, b.y + r + 2, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, u = dx * ca + dy * sa, v = -dx * sa + dy * ca, d = Hypot(u, v);
                double ang = Atan2(v, u);
                if (ang < 0) ang += TAU;
                double f = ang / TAU * n, w = f - Math.Floor(f);
                int i = (int)Math.Floor(f) % n;
                double edge = r * (pts[i] * (1 - w) + pts[(i + 1) % n] * w);
                if (d > edge + 0.85) return 0;
                o.a = fade;
                if (d > edge) return OUTLINE;
                double lit = Px.Cos((i + 0.5) / n * TAU + b.a - LIGHT);
                if (d < edge * 0.42) return lit > 0.55 ? st.light : st.body;
                return lit > 0.35 ? st.light : lit < -0.4 ? st.shade : st.body;
            });
            p.Shift(0, 0);
        }

        public static void BornWith(Particles ps, Bits.Bit b)
        {
            ps.BornAt(b.bornX, b.bornY);
        }
    }
}
