// Helpers shared by the weather loops. Weather fills its whole canvas edge to edge, so: clip() keeps every pixel
// and every bit of light inside the canvas; pass() is a fast per-pixel shader over the canvas; field() bakes a
// tiling noise that scrolls a whole tile a loop; trips() rains particles that fall a whole number of times a
// loop, somewhere new each time; tree() is a branching lightning path. All of it is a pure function of t.
// The noise bake and the branching bolt are adapted from Sprite Fusion's open-source fog and lightning
// (https://www.spritefusion.com/pixel-art-vfx/fog), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class WeatherLib
    {
        static readonly double[] BAYER = Array.ConvertAll(new[] { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }, v => (v + 0.5) / 16);

        /// <summary>The canvas in local coordinates: x0 &lt;= x &lt; x1, y0 &lt;= y &lt; y1.</summary>
        public readonly struct Box
        {
            public readonly int x0, y0, x1, y1, w, h;

            public Box(Module m)
            {
                x0 = -m.OX;
                y0 = -m.OY;
                x1 = m.W - m.OX;
                y1 = m.H - m.OY;
                w = m.W;
                h = m.H;
            }
        }

        // the painter's placement: rotated or not (JS p.sn !== 0), and the mirror (JS p.fx) when it isn't
        static bool Rotated(Painter p) => p.ToY(1, 0) != p.ToY(0, 0) || p.ToX(0, 1) != p.ToX(0, 0);
        static double FlipOf(Painter p) => p.ToX(1, 0) - p.ToX(0, 0) < 0 ? -1 : 1;

        /// <summary>
        /// A painter that draws only inside the effect's canvas: the same API, with color, blends and light outside it
        /// dropped. Exact under the painter's flip and rotation.
        /// </summary>
        public sealed class Clip
        {
            public readonly Painter P;
            public readonly Box B;
            readonly bool rot;
            readonly double sx0, sx1, sy0, sy1;
            readonly Painter.Out shadeOut = new Painter.Out();

            public Clip(Painter p, Module m)
            {
                P = p;
                B = new Box(m);
                rot = Rotated(p);
                if (!rot)
                {
                    double a = p.ToX(B.x0, 0), b = p.ToX(B.x1, 0);
                    sx0 = Round(Math.Min(a, b));
                    sx1 = Round(Math.Max(a, b));
                    sy0 = Round(p.ToY(0, B.y0));
                    sy1 = Round(p.ToY(0, B.y1));
                }
            }

            bool Ok(int sx, int sy)
            {
                if (!rot) return sx >= sx0 && sx < sx1 && sy >= sy0 && sy < sy1;
                double lx = P.FromX(sx + 0.5, sy + 0.5), ly = P.FromY(sx + 0.5, sy + 0.5);
                return lx >= B.x0 && lx < B.x1 && ly >= B.y0 && ly < B.y1;
            }

            public void Put(int sx, int sy, uint c, double a = 1)
            {
                if (Ok(sx, sy)) P.Put(sx, sy, c, a);
            }

            public void Add(int sx, int sy, uint c, double k = 1)
            {
                if (Ok(sx, sy)) P.Add(sx, sy, c, k);
            }

            public void BlendPx(int sx, int sy, uint c, double a)
            {
                if (Ok(sx, sy)) P.BlendPx(sx, sy, c, a);
            }

            public void Px(double x, double y, uint c) => Put(P.CellX(x, y), P.CellY(x, y), c, 1);

            public void Dot(double x, double y, uint c, double a = 1)
            {
                if (a > 0) Put(P.CellX(x, y), P.CellY(x, y), c, a);
            }

            public void Blend(double x, double y, uint c, double a) => BlendPx(P.CellX(x, y), P.CellY(x, y), c, a);

            public void Light(double x, double y, uint c, double k = 1) => Add(P.CellX(x, y), P.CellY(x, y), c, k);

            // the painter's glow, with its box cut to the canvas first (big soft lights stay cheap)
            public void Glow(double x, double y, double r, uint c, double k)
            {
                if (r < 1 || k <= 0.004) return;
                double x0 = Math.Max(B.x0, Math.Floor(x - r)), x1 = Math.Min(B.x1 - 1, Math.Ceiling(x + r));
                double y0 = Math.Max(B.y0, Math.Floor(y - r)), y1 = Math.Min(B.y1 - 1, Math.Ceiling(y + r));
                for (double py = y0; py <= y1; py++)
                {
                    double dy = (py + 0.5 - y) / r;
                    for (double px = x0; px <= x1; px++)
                    {
                        double dx = (px + 0.5 - x) / r, d = dx * dx + dy * dy;
                        if (d >= 1) continue;
                        double f = 1 - Math.Sqrt(d);
                        Light(px, py, c, f * f * k);
                    }
                }
            }

            public void Shade(double x0, double y0, double x1, double y1, Painter.ShadeFn fn)
            {
                Painter.Out o = shadeOut;
                P.Each(x0, y0, x1, y1, (lx, ly, sx, sy) =>
                {
                    o.c = 0;
                    o.a = 1;
                    o.l = 0;
                    o.k = 0;
                    uint c = fn(lx, ly, o);
                    if (c == 0) c = o.c;
                    if (c != 0) Put(sx, sy, c, o.a);
                    if (o.k > 0) Add(sx, sy, o.l != 0 ? o.l : c, o.k);
                });
            }

            public void Line(double x0, double y0, double x1, double y1, uint c, double a = 1) => Line(x0, y0, x1, y1, c, a, null);
            public void Line(double x0, double y0, double x1, double y1, uint c, Func<double, double> a) => Line(x0, y0, x1, y1, c, 1, a);

            void Line(double x0, double y0, double x1, double y1, uint c, double a, Func<double, double> fa)
            {
                int sx = P.CellX(x0, y0), sy = P.CellY(x0, y0);
                int ex = P.CellX(x1, y1), ey = P.CellY(x1, y1);
                int dx = Math.Abs(ex - sx), dy = -Math.Abs(ey - sy), stepx = sx < ex ? 1 : -1, stepy = sy < ey ? 1 : -1;
                int n = Math.Max(dx, -dy);
                if (n == 0) n = 1;
                int err = dx + dy, i = 0;
                for (;;)
                {
                    double u = (double)i++ / n;
                    Put(sx, sy, c, fa != null ? fa(u) : a);
                    if (sx == ex && sy == ey) break;
                    int e2 = 2 * err;
                    if (e2 >= dy)
                    {
                        err += dy;
                        sx += stepx;
                    }
                    if (e2 <= dx)
                    {
                        err += dx;
                        sy += stepy;
                    }
                }
            }

            public void Polyline(IList<double[]> pts, uint c)
            {
                for (int i = 1; i < pts.Count; i++) Line(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], c);
            }

            public void Circle(double cx, double cy, double r, uint c, double a = 1)
            {
                if (r < 0.6)
                {
                    Dot(cx, cy, c, a);
                    return;
                }
                Shade(cx - r, cy - r, cx + r, cy + r, (x, y, o) =>
                {
                    double dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                    if (d2 > r * r) return 0;
                    o.c = c;
                    o.a = a;
                    return 0;
                });
            }

            public void Disc(double cx, double cy, double r, uint c, double a)
            {
                if (r < 0.5)
                {
                    Blend(cx, cy, c, a);
                    return;
                }
                P.Each(cx - r, cy - r, cx + r, cy + r, (x, y, sx, sy) =>
                {
                    double dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                    if (d2 > r * r) return;
                    BlendPx(sx, sy, c, a);
                });
            }
        }

        /// <summary>Something that shades a local box: a clip's Shade, or a recorder that keeps the pixels to replay.</summary>
        public delegate void Shader(double x0, double y0, double x1, double y1, Painter.ShadeFn fn);

        /// <summary>
        /// A recorder that shades local cells floor(x0)..ceil(x1) by floor(y0)..ceil(y1) and keeps each pixel it colors as
        /// (x, y, color, alpha) to replay with q.Dot. resetAlpha: out.a starts at 1 for every pixel (otherwise only for every call).
        /// </summary>
        public static Shader Recorder(List<double> px, bool resetAlpha)
        {
            return (x0, y0, x1, y1, fn) =>
            {
                var o = new Painter.Out { a = 1 };
                for (double y = Math.Floor(y0); y <= Math.Ceiling(y1); y++)
                    for (double x = Math.Floor(x0); x <= Math.Ceiling(x1); x++)
                    {
                        o.c = 0;
                        if (resetAlpha) o.a = 1;
                        uint c = fn(x + 0.5, y + 0.5, o);
                        if (c == 0) c = o.c;
                        if (c != 0)
                        {
                            px.Add(x + 0.5);
                            px.Add(y + 0.5);
                            px.Add(c);
                            px.Add(o.a);
                        }
                    }
            };
        }

        /// <summary>A star glint (the core sparkle) drawn through the clip, so its light is cut to the canvas too.</summary>
        public static void Sparkle(Clip p, double x, double y, double arm, uint c, uint tip, double k)
        {
            double n = Round(arm);
            if (n < 1) return;
            double X = Math.Floor(x), Y = Math.Floor(y);
            for (int d = 1; d <= n; d++)
            {
                uint cc = d > n * 0.6 ? tip : c;
                foreach (int[] v in Sim.Cross) p.Px(X + v[0] * d, Y + v[1] * d, cc);
            }
            if (n >= 3)
                foreach (int[] v in DIAG)
                    p.Px(X + v[0], Y + v[1], c);
            if (n >= 6)
                foreach (int[] v in DIAG)
                    p.Px(X + v[0] * 2, Y + v[1] * 2, tip);
            p.Px(X, Y, WHITE);
            p.Glow(x, y, 4 + n * 2, c, k);
        }

        static readonly int[][] DIAG = { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };

        /// <summary>The core particles' draw, through the clip: the sparks and vapour the weather throws.</summary>
        public static void DrawParticles(Clip p, Particles ps, uint[] sparks, uint haze)
        {
            foreach (Particles.Particle q in ps.List)
            {
                if (q.delay > 0) continue;
                double k = q.life / q.max, x = q.x, y = q.y, sp = Hypot(q.vx, q.vy);
                if (sp == 0 || double.IsNaN(sp)) sp = 1;
                double ux = q.vx / sp, uy = q.vy / sp;
                switch (q.k)
                {
                    case P.Spark:
                    {
                        uint[] s = sparks;
                        uint head = k > 0.6 ? s[0] : k > 0.3 ? s[1] : s[2];
                        double len = Math.Min(9, 1 + sp * 0.022);
                        p.Line(x - ux * len, y - uy * len, x - ux * len * 0.4, y - uy * len * 0.4, k > 0.5 ? head : s[s.Length - 1]);
                        p.Line(x - ux * len * 0.45, y - uy * len * 0.45, x, y, head);
                        p.Glow(x, y, 3, head, 0.45 * k);
                        break;
                    }
                    case P.Vapour:
                    {
                        double r = q.size * (0.6 + (1 - k) * 0.9);
                        p.Disc(x, y, r, q.col, Px.Pow(k, 1.3) * 0.45);
                        if (haze != 0) p.Glow(x, y, r * 1.8, haze, 0.16 * k);
                        break;
                    }
                    default:
                        throw new NotSupportedException("the weather only throws sparks and vapour");
                }
            }
        }

        /// <summary>What a pass shader sets for one pixel: color c at alpha a (dithered, or truly blended when b), light l at k.</summary>
        public sealed class Out
        {
            public uint c, l;
            public double a, k;
            public bool b;
        }

        static readonly Out OUT = new Out();

        /// <summary>
        /// A per-pixel shader over the canvas rows y0..y1 (local, clipped to the canvas), much faster than p.shade for
        /// whole-sky passes. fn(x, y, out) sets out.c (color) with out.a (alpha: dithered, or truly blended when out.b
        /// is set), and out.l / out.k (light color and amount). x, y are pixel centers. Falls back to p.shade when rotated.
        /// </summary>
        public static void Pass(Painter p, Module meta, double y0, double y1, Action<double, double, Out> fn)
        {
            var B = new Box(meta);
            Out o = OUT;
            y0 = Math.Max(B.y0, Math.Floor(y0));
            y1 = Math.Min(B.y1 - 1, Math.Ceiling(y1));
            if (y0 > y1) return;
            if (Rotated(p))
            {
                var q = new Clip(p, meta);
                for (double y = y0; y <= y1; y++)
                    for (int x = B.x0; x < B.x1; x++)
                    {
                        o.c = 0;
                        o.a = 1;
                        o.b = false;
                        o.l = 0;
                        o.k = 0;
                        fn(x + 0.5, y + 0.5, o);
                        if (o.c != 0)
                        {
                            if (o.b) q.Blend(x, y, o.c, o.a);
                            else q.Dot(x, y, o.c, o.a);
                        }
                        if (o.k > 0) q.Light(x, y, o.l != 0 ? o.l : o.c, o.k);
                    }
                return;
            }
            Frame f = p.Frame;
            uint[] C = f.Color, L = f.Light;
            int W = f.W, Hh = f.H, ox = f.Ox, oy = f.Oy;
            double fade = p.Fade, lk = p.LightK * fade, fx = FlipOf(p), tx = p.ToX(0, 0), ty = p.ToY(0, 0);
            for (double y = y0; y <= y1; y++)
            {
                double sy = Math.Floor(ty + y + 0.5), ry = sy - oy;
                if (ry < 0 || ry >= Hh) continue;
                int row = (int)ry * W, by = (ToInt32(sy) & 3) << 2;
                double sxf = tx + (B.x0 + 0.5) * fx;
                for (int x = B.x0; x < B.x1; x++, sxf += fx)
                {
                    double sx = Math.Floor(sxf), rx = sx - ox;
                    if (rx < 0 || rx >= W) continue;
                    o.c = 0;
                    o.a = 1;
                    o.b = false;
                    o.l = 0;
                    o.k = 0;
                    fn(x + 0.5, y + 0.5, o);
                    int i = row + (int)rx;
                    if (o.c != 0)
                    {
                        double a = o.a * fade;
                        if (o.b)
                        {
                            if (a > 0.004) C[i] = Over(C[i], o.c, a);
                        }
                        else if (a >= 1 || a > BAYER[by | (ToInt32(sx) & 3)]) C[i] = o.c;
                    }
                    if (o.k > 0)
                    {
                        double k = o.k * lk;
                        if (k > 0.004) L[i] = Lit(L[i], o.l != 0 ? o.l : o.c, k);
                    }
                }
            }
        }

        // a true blend of c at alpha a over d, keeping the result's alpha (the painter's blendPx)
        static uint Over(uint d, uint c, double a)
        {
            double da = (d >> 24) / 255.0, oa = a + da * (1 - a), k = a / oa, m = 1 - k;
            uint r = (uint)(ToInt32((c & 255) * k + (d & 255) * m) & 255), g = (uint)(ToInt32(((c >> 8) & 255) * k + ((d >> 8) & 255) * m) & 255);
            uint b = (uint)(ToInt32(((c >> 16) & 255) * k + ((d >> 16) & 255) * m) & 255);
            return ((uint)(ToInt32(oa * 255) & 255) << 24) | (b << 16) | (g << 8) | r;
        }

        // light c at k added to d (the painter's add)
        static uint Lit(uint d, uint c, double k)
        {
            uint r = Channel(Math.Min(255, (d & 255) + (c & 255) * k)), g = Channel(Math.Min(255, ((d >> 8) & 255) + ((c >> 8) & 255) * k));
            uint b = Channel(Math.Min(255, ((d >> 16) & 255) + ((c >> 16) & 255) * k));
            return 0xff000000 | (b << 16) | (g << 8) | r;
        }

        // Math.min(255, v) | 0 for v >= 0 or NaN: NaN gives 0
        static uint Channel(double v) => v >= 0 ? (uint)v : 0;

        /// <summary>One canvas row for Rows: per column i = x - box.x0, color c (0 none), alpha a, blend b, light l at k.</summary>
        public sealed class Row
        {
            public int w;
            public uint[] c, l;
            public float[] a, k;
            public byte[] b;

            public static void Fill(uint[] r, uint v)
            {
                for (int i = 0; i < r.Length; i++) r[i] = v;
            }

            public static void Fill(float[] r, double v)
            {
                float f = (float)v;
                for (int i = 0; i < r.Length; i++) r[i] = f;
            }

            public static void Fill(byte[] r, byte v)
            {
                for (int i = 0; i < r.Length; i++) r[i] = v;
            }
        }

        static readonly Row ROW = new Row();

        /// <summary>
        /// The fastest whole-sky shader: fn(y, R) fills one canvas row at a time (y the row's pixel center), writing
        /// R.c[i] (color, 0 for none), R.a[i] (alpha, default 1), R.b[i] (1: blend, 0: dither), R.l[i] / R.k[i] (light
        /// color and amount) for column i = x - box.x0. One call a row, so the effect's inner loop stays tight.
        /// </summary>
        public static void Rows(Painter p, Module meta, double y0, double y1, Action<double, Row> fn)
        {
            var B = new Box(meta);
            int w = B.w;
            Row R = ROW;
            if (R.w != w)
            {
                R.w = w;
                R.c = new uint[w];
                R.a = new float[w];
                R.b = new byte[w];
                R.l = new uint[w];
                R.k = new float[w];
            }
            y0 = Math.Max(B.y0, Math.Floor(y0));
            y1 = Math.Min(B.y1 - 1, Math.Ceiling(y1));
            bool rot = Rotated(p);
            Clip q = rot ? new Clip(p, meta) : null;
            Frame f = p.Frame;
            uint[] C = f.Color, L = f.Light;
            int W = f.W, Hh = f.H, ox = f.Ox, oy = f.Oy;
            double fade = p.Fade, lk = p.LightK * fade, fx = rot ? 1 : FlipOf(p), tx = p.ToX(0, 0), ty = p.ToY(0, 0);
            for (double y = y0; y <= y1; y++)
            {
                Row.Fill(R.c, 0);
                Row.Fill(R.k, 0);
                Row.Fill(R.a, 1);
                Row.Fill(R.b, 0);
                fn(y + 0.5, R);
                if (rot)
                {
                    for (int i = 0; i < w; i++)
                    {
                        if (R.c[i] != 0)
                        {
                            if (R.b[i] != 0) q.Blend(B.x0 + i, y, R.c[i], R.a[i]);
                            else q.Dot(B.x0 + i, y, R.c[i], R.a[i]);
                        }
                        if (R.k[i] > 0) q.Light(B.x0 + i, y, R.l[i] != 0 ? R.l[i] : R.c[i], R.k[i]);
                    }
                    continue;
                }
                double sy = Math.Floor(ty + y + 0.5), ry = sy - oy;
                if (ry < 0 || ry >= Hh) continue;
                int row = (int)ry * W, by = (ToInt32(sy) & 3) << 2;
                double sxf = tx + (B.x0 + 0.5) * fx;
                for (int i = 0; i < w; i++, sxf += fx)
                {
                    uint c = R.c[i];
                    double kk = R.k[i];
                    if (c == 0 && !(kk > 0)) continue;
                    double sx = Math.Floor(sxf), rx = sx - ox;
                    if (rx < 0 || rx >= W) continue;
                    int j = row + (int)rx;
                    if (c != 0)
                    {
                        double a = R.a[i] * fade;
                        if (R.b[i] != 0)
                        {
                            if (a > 0.004) C[j] = Over(C[j], c, a);
                        }
                        else if (a >= 1 || a > BAYER[by | (ToInt32(sx) & 3)]) C[j] = c;
                    }
                    if (kk > 0)
                    {
                        double k = kk * lk;
                        if (k > 0.004) L[j] = Lit(L[j], R.l[i] != 0 ? R.l[i] : c, k);
                    }
                }
            }
        }

        /// <summary>Ordered-dither threshold of a local pixel (0..1): `a > bayer(x, y)` draws it.</summary>
        public static double Bayer(double x, double y) => BAYER[((ToInt32(Math.Floor(y)) & 3) << 2) | (ToInt32(Math.Floor(x)) & 3)];

        /// <summary>A baked tiling noise: w x h values, row by row.</summary>
        public sealed class Field
        {
            public int W, H;
            public float[] D;
        }

        static readonly Dictionary<string, Field> FIELDS = new Dictionary<string, Field>();
        static readonly int[] CELLS = { 4, 8, 16, 32 };

        /// <summary>
        /// A baked fractal noise that tiles: w x h cells (powers of two), octaves of `cells` lattice squares across.
        /// Values 0..1, mostly near 0.5. Scroll it a whole number of tiles a loop and the loop has no seam.
        /// </summary>
        public static Field MakeField(double seed, int w = 256, int h = 128, int[] cells = null)
        {
            cells = cells ?? CELLS;
            string key = seed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + w + "|" + h + "|" + string.Join(",", cells);
            if (FIELDS.TryGetValue(key, out Field F)) return F;
            var d = new float[w * h];
            double amp = 0.5, total = 0;
            foreach (int n in cells)
            {
                double cs = (double)w / n, rows = Math.Max(1, Round(h / cs));
                double At(double a, double b) => Rand(Wrap(a, n), Wrap(b, rows), seed, n);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        double gx = x / cs, gy = ((double)y / h) * rows, ix = Math.Floor(gx), iy = Math.Floor(gy), u = gx - ix, v = gy - iy;
                        double su = u * u * (3 - 2 * u), sv = v * v * (3 - 2 * v);
                        double top = At(ix, iy) + (At(ix + 1, iy) - At(ix, iy)) * su, bot = At(ix, iy + 1) + (At(ix + 1, iy + 1) - At(ix, iy + 1)) * su;
                        int i = y * w + x;
                        d[i] = (float)(d[i] + (top + (bot - top) * sv) * amp);
                    }
                total += amp;
                amp *= 0.5;
            }
            for (int i = 0; i < d.Length; i++) d[i] = (float)(d[i] / total);
            FIELDS[key] = F = new Field { W = w, H = h, D = d };
            return F;
        }

        /// <summary>A field at (u, v) in its own cells, wrapping, smooth between cells.</summary>
        public static double Sample(Field F, double u, double v)
        {
            double x = Math.Floor(u), y = Math.Floor(v), fx = u - x, fy = v - y;
            int m = F.W - 1, n = F.H - 1;
            float[] D = F.D;
            int x0 = ToInt32(x) & m, x1 = ToInt32(x + 1) & m, y0 = (ToInt32(y) & n) * F.W, y1 = (ToInt32(y + 1) & n) * F.W;
            double a = D[y0 + x0], b = D[y0 + x1], c = D[y1 + x0], e = D[y1 + x1];
            return a + (b - a) * fx + (c - a + (a - b - c + e) * fx) * fy;
        }

        /// <summary>
        /// Falling particles for a loop of D seconds: n slots, each falling m times a loop (m whole, from m0 to m1), each
        /// fall somewhere new. fn(u, k, m, i): u 0..1 through the current fall, k its number (Wrap(k, m) names it), m, the slot i.
        /// </summary>
        public static void Trips(double t, double D, int n, double seed, int m0, int m1, Action<double, double, double, int> fn)
        {
            for (int i = 0; i < n; i++)
            {
                double m = m0 + Math.Floor(Rand(seed, i, 72) * (m1 - m0 + 1)), s = (t / D) * m + Rand(seed, i, 71), k = Math.Floor(s);
                fn(s - k, k, m, i);
            }
        }

        /// <summary>One path of a lightning tree: its points, its weight (the trunk 1, forks thinner) and how deep a fork it is.</summary>
        public sealed class Branch
        {
            public List<double[]> pts;
            public double w;
            public int depth;
        }

        /// <summary>
        /// A branching lightning path from (x0, y0) to (x1, y1): midpoint displacement down to ~3 px segments, with
        /// forks (and forks of forks) peeling off. The trunk first with w = 1, forks thinner (w &lt; 1).
        /// </summary>
        public static List<Branch> Tree(Rng R, double x0, double y0, double x1, double y1, double jag = 0.3, double forks = 0.4, double w = 1, int depth = 0)
        {
            var output = new List<Branch>();
            double L = Hypot(x1 - x0, y1 - y0);
            var pts = new List<double[]> { new[] { x0, y0 }, new[] { x1, y1 } };
            double off = L * jag;
            while (pts.Count < 2 + L / 3)
            {
                var nxt = new List<double[]> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    double ax = pts[i][0], ay = pts[i][1], bx = pts[i + 1][0], by = pts[i + 1][1], l = Hypot(bx - ax, by - ay);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    double o = R.Range(-off, off);
                    nxt.Add(new[] { (ax + bx) / 2 + (-(by - ay) / l) * o, (ay + by) / 2 + ((bx - ax) / l) * o });
                    nxt.Add(new[] { bx, by });
                }
                pts = nxt;
                off *= 0.52;
            }
            output.Add(new Branch { pts = pts, w = w, depth = depth });
            if (depth < 2)
                for (int i = 2; i < pts.Count - 3; i++)
                    if (R.Chance(forks / (1 + depth) / Math.Max(4, pts.Count / 6.0)))
                    {
                        double ax = pts[i][0], ay = pts[i][1];
                        double[] e = pts[Math.Min(pts.Count - 1, i + 3)];
                        double bx = e[0], by = e[1];
                        double a = Atan2(by - ay, bx - ax) + R.Sign() * R.Range(0.35, 0.95), l = (L - ((double)i / pts.Count) * L) * R.Range(0.25, 0.55);
                        output.AddRange(Tree(R, ax, ay, ax + Px.Cos(a) * l, ay + Px.Sin(a) * l, jag * 1.1, forks, w * 0.55, depth + 1));
                    }
            return output;
        }

        /// <summary>Whole cycles per loop: a sine with c full turns in D seconds, phase ph (radians). Seamless by construction.</summary>
        public static double Wave(double t, double D, double c, double ph = 0) => Px.Sin((TAU * c * t) / D + ph);

        /// <summary>
        /// A raindrop's splash on the floor, age seconds after it lands at (x, z): a white crown for a frame or two, a
        /// flat ring spreading (its near arc brighter), and droplets thrown up that fall back. size 0..1 (far to near),
        /// hot and cool colors, h a number unique to this drop. Adapted from Sprite Fusion's rain.
        /// </summary>
        public static void Splash(Clip q, double x, double z, double age, double size, uint hot, uint cool, double h)
        {
            double L = 0.3 + 0.12 * size, u = age / L;
            if (u >= 1) return;
            if (age < 0.07)
            {
                q.Px(x, z - 1, hot);
                if (size > 0.3)
                {
                    q.Px(x - 1, z - 1 - (age > 0.035 ? 1 : 0), hot);
                    q.Px(x + 1, z - 1 - (age > 0.035 ? 1 : 0), hot);
                }
                if (size > 0.65 && age > 0.03)
                {
                    q.Px(x - 2, z - 2, cool);
                    q.Px(x + 2, z - 2, cool);
                }
            }
            double rx = 1 + (1.2 + 3.6 * size) * Math.Sqrt(u), ry = Math.Max(0.5, rx * 0.26), n = Math.Ceiling(rx * 6.5);
            for (int j = 0; j < n; j++)
            {
                double a = (j / n) * TAU, s = Px.Sin(a);
                if (s < -0.25) continue;
                q.Dot(x + Px.Cos(a) * rx, z + s * ry, u < 0.3 && s > 0 ? hot : cool, u < 0.45 ? (s > 0.2 ? 1 : 0.7) : (1 - u) * 1.6 * (s > 0.2 ? 1 : 0.6));
            }
            double nd = size > 0.55 ? 3 : 2, H = Math.Floor(h * 1e6);
            for (int d = 0; d < nd; d++)
            {
                double vx = (Rand(H, d, 1) - 0.5) * 2 * (25 + 35 * size), vy = -(55 + Rand(H, d, 2) * 70 * (0.5 + size)), y = z - 1 + vy * age + 475 * age * age;
                if (y > z - 0.5 || age > 0.34) continue;
                double xx = x + vx * age;
                q.Px(xx, y, age < 0.12 ? hot : cool);
                if (vy + 950 * age < -40) q.Dot(xx - vx * 0.012, y + 1, cool, 0.7);
            }
        }

        /// <summary>
        /// A crisp splash on a floor line, age seconds after a drop lands at (x, z): a crown for a few frames (a white
        /// point, two arms rising), then a flat dash that spreads and breaks into two end dashes, and a droplet or two
        /// arcing up and back. Solid pixels only (no dither), so a line of them stays clean. size 0..1, h a number
        /// unique to the drop. Returns false once it's over.
        /// </summary>
        public static bool Crown(Clip q, double x, double z, double age, double size, uint hot, uint cool, uint white, double h)
        {
            double L = 0.22 + 0.14 * size, u = age / L, X = Round(x), Z = Round(z);
            if (u >= 1) return false;
            if (age < 0.1)
            {
                double up = age < 0.033 ? 1 : age < 0.067 ? 2 : 2 + (size > 0.5 ? 1 : 0);
                q.Px(X, Z - 1, age < 0.05 ? white : hot);
                for (int d = 1; d <= Math.Min(up, size > 0.5 ? 3 : 1); d++)
                {
                    q.Px(X - d, Z - 1 - (d > 1 ? d - 1 : 0), d == 1 ? hot : cool);
                    q.Px(X + d, Z - 1 - (d > 1 ? d - 1 : 0), d == 1 ? hot : cool);
                }
            }
            if (u > 0.15)
            {
                double w = Round(1 + (1.5 + 3.5 * size) * Math.Sqrt(u)), gap = u > 0.55 ? Math.Max(0, w - 2) : -1;
                for (double d = -w; d <= w; d++)
                    if (Math.Abs(d) > gap) q.Px(X + d, Z, Math.Abs(d) >= w - 1 && u < 0.6 ? hot : cool);
            }
            double nd = size > 0.5 ? 2 : 1, H = Math.Floor(h * 1e6);
            for (int d = 0; d < nd; d++)
            {
                double vx = (Rand(H, d, 1) - 0.5) * 2 * (20 + 30 * size), vy = -(50 + Rand(H, d, 2) * 60 * (0.4 + size)), y = Z - 1 + vy * age + 475 * age * age;
                if (y < Z - 0.5 && age < 0.3) q.Px(x + vx * age, y, age < 0.1 ? white : hot);
            }
            return true;
        }
    }
}
