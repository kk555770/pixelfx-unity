using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Frame
    {
        public readonly int W, H;
        public int Ox, Oy;
        public readonly uint[] Color, Light;

        public Frame(int w, int h, int ox = 0, int oy = 0)
        {
            W = w;
            H = h;
            Ox = ox;
            Oy = oy;
            Color = new uint[w * h];
            Light = new uint[w * h];
        }

        public void Clear()
        {
            Array.Clear(Color, 0, Color.Length);
            Array.Clear(Light, 0, Light.Length);
        }

        /// <summary>Color plus light into RGBA bytes, on transparency: over nothing, light becomes color with alpha.</summary>
        public void Composite(byte[] rgba, double lightK = 1) => Composite(rgba, null, null, lightK);

        /// <summary>Color over a background (one color per pixel, W x H) plus light into RGBA bytes.</summary>
        public void Composite(byte[] rgba, uint[] bg, double lightK = 1) => Composite(rgba, bg, null, lightK);

        /// <summary>Color over a flat background color plus light into RGBA bytes.</summary>
        public void Composite(byte[] rgba, uint bg, double lightK = 1) => Composite(rgba, null, bg, lightK);

        void Composite(byte[] rgba, uint[] bgs, uint? flat, double lightK)
        {
            bool none = bgs == null && flat == null;
            for (int i = 0, n = W * H; i < n; i++)
            {
                uint c = Color[i], l = Light[i] & 0xffffff, ca = c >> 24;
                double r = c & 255, g = (c >> 8) & 255, b = (c >> 16) & 255, a = ca;
                if (ca != 255 && !none)
                {
                    uint k = flat ?? bgs[i];
                    double f = ca / 255.0;
                    r = (c & 255) * f + (k & 255) * (1 - f);
                    g = ((c >> 8) & 255) * f + ((k >> 8) & 255) * (1 - f);
                    b = ((c >> 16) & 255) * f + ((k >> 16) & 255) * (1 - f);
                    a = 255;
                }
                if (l != 0)
                {
                    double lr = (l & 255) * lightK, lg = ((l >> 8) & 255) * lightK, lb = ((l >> 16) & 255) * lightK;
                    if (a < 255)
                    {
                        double pa = a / 255, pr = r * pa + lr, pg = g * pa + lg, pb = b * pa + lb;
                        double na = Math.Min(1, Math.Max(pa, Math.Max(lr, Math.Max(lg, lb)) / 255));
                        if (na > 0)
                        {
                            r = Math.Min(255, pr / na);
                            g = Math.Min(255, pg / na);
                            b = Math.Min(255, pb / na);
                            a = na * 255;
                        }
                    }
                    else
                    {
                        r = Math.Min(255, r + lr);
                        g = Math.Min(255, g + lg);
                        b = Math.Min(255, b + lb);
                    }
                }
                int j = i * 4;
                rgba[j] = Uint8Clamped(r);
                rgba[j + 1] = Uint8Clamped(g);
                rgba[j + 2] = Uint8Clamped(b);
                rgba[j + 3] = Uint8Clamped(a);
            }
        }

        static byte Uint8Clamped(double v) => v <= 0 || double.IsNaN(v) ? (byte)0 : v >= 255 ? (byte)255 : (byte)Math.Round(v, MidpointRounding.ToEven);

        public void EdgeFade(int e = 8)
        {
            for (int y = 0; y < H; y++)
            {
                int ey = Math.Min(y, H - 1 - y);
                for (int x = 0; x < W; x++)
                {
                    int edge = Math.Min(ey, Math.Min(x, W - 1 - x));
                    if (edge >= e)
                    {
                        x = W - 1 - e;
                        continue;
                    }
                    int i = y * W + x;
                    uint l = Light[i];
                    if ((l & 0xffffff) == 0) continue;
                    double u = (edge + 0.5) / e, k = u * u * (3 - 2 * u);
                    Light[i] = (l & 0xff000000) | (uint)RoundInt((l & 255) * k) | ((uint)RoundInt(((l >> 8) & 255) * k) << 8) | ((uint)RoundInt(((l >> 16) & 255) * k) << 16);
                }
            }
        }
    }

    public sealed class Mask
    {
        public readonly int W, H;
        public readonly byte[] Data;

        public Mask(int w, int h, byte[] data)
        {
            W = w;
            H = h;
            Data = data;
        }
    }

    public sealed partial class Painter
    {
        public sealed class Out
        {
            public uint c, l;
            public double a, k;
        }

        public delegate uint ShadeFn(double x, double y, Out o);

        static readonly double[] Bayer = Array.ConvertAll(new[] { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 }, v => (v + 0.5) / 16);

        readonly Out shadeOut = new Out();
        Frame frame;
        int w, h, ox, oy;
        uint[] C, L;
        double tx, ty, fx = 1, cs = 1, sn, placedX, placedY;

        public double Fade = 1, LightK = 1;

        public Painter(Frame f) => Reset(f);

        public Frame Frame => frame;

        public Painter Reset(Frame f)
        {
            frame = f;
            w = f.W;
            h = f.H;
            ox = f.Ox;
            oy = f.Oy;
            C = f.Color;
            L = f.Light;
            tx = ty = placedX = placedY = sn = 0;
            fx = cs = 1;
            Fade = LightK = 1;
            Layer = Layer.Front;
            return this;
        }

        public Painter Place(double x, double y, bool flip = false, double angle = 0)
        {
            tx = placedX = x;
            ty = placedY = y;
            fx = flip ? -1 : 1;
            cs = Pixelfx.Px.Cos(angle);
            sn = Pixelfx.Px.Sin(angle);
            return this;
        }

        public void Shift(int dx, int dy)
        {
            tx = placedX + dx;
            ty = placedY + dy;
        }

        public double ToX(double x, double y) => tx + x * fx * cs - y * sn;
        public double ToY(double x, double y) => ty + x * fx * sn + y * cs;

        public double FromX(double sx, double sy)
        {
            double dx = sx - tx, dy = sy - ty;
            return (dx * cs + dy * sn) * fx;
        }

        public double FromY(double sx, double sy)
        {
            double dx = sx - tx, dy = sy - ty;
            return -dx * sn + dy * cs;
        }

        public int CellX(double x, double y) => ScreenInt(Math.Floor(ToX(Math.Floor(x) + 0.5, Math.Floor(y) + 0.5)));
        public int CellY(double x, double y) => ScreenInt(Math.Floor(ToY(Math.Floor(x) + 0.5, Math.Floor(y) + 0.5)));

        // A whole screen coordinate as int. JS keeps NaN, ±Infinity and huge values as doubles, and every write
        // there falls outside the frame; int.MinValue lands outside it too (a plain cast gives 0 on ARM).
        static int ScreenInt(double v) => v >= int.MinValue && v <= int.MaxValue ? (int)v : int.MinValue;

        // Math.min(255, v) | 0 for v >= 0 or NaN: NaN gives 0.
        static uint Channel(double v) => v >= 0 ? (uint)v : 0;

        // a clipped painter (ClipTo) drops every pixel whose centre falls outside its local rectangle 0..clipW, 0..clipH
        double clipW = double.NaN, clipH;

        bool Outside(int sx, int sy)
        {
            if (double.IsNaN(clipW)) return false;
            double lx = FromX(sx + 0.5, sy + 0.5), ly = FromY(sx + 0.5, sy + 0.5);
            return !(lx >= 0 && ly >= 0 && lx < clipW && ly < clipH);
        }

        public void Put(int sx, int sy, uint c, double a = 1)
        {
            if (Outside(sx, sy)) return;
            if (c == 0) return;
            a *= Fade;
            if (a < 1 && a <= Bayer[((sy & 3) << 2) | (sx & 3)]) return;
            int x = sx - ox, y = sy - oy;
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            C[y * w + x] = c;
        }

        public void Add(int sx, int sy, uint c, double k = 1)
        {
            if (Outside(sx, sy)) return;
            k *= Fade * LightK;
            if (k <= 0.004) return;
            int x = sx - ox, y = sy - oy;
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            uint d = L[i];
            uint r = Channel(Math.Min(255, (d & 255) + (c & 255) * k));
            uint g = Channel(Math.Min(255, ((d >> 8) & 255) + ((c >> 8) & 255) * k));
            uint b = Channel(Math.Min(255, ((d >> 16) & 255) + ((c >> 16) & 255) * k));
            L[i] = 0xff000000 | (b << 16) | (g << 8) | r;
        }

        public void BlendPx(int sx, int sy, uint c, double a)
        {
            if (Outside(sx, sy)) return;
            a *= Fade;
            if (c == 0 || a <= 0.004) return;
            int x = sx - ox, y = sy - oy;
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            uint d = C[i];
            double da = (d >> 24) / 255.0, oa = a + da * (1 - a), k = a / oa;
            uint Ch(int s) => (uint)(ToInt32(((c >> s) & 255) * k + ((d >> s) & 255) * (1 - k)) & 255);
            C[i] = ((uint)(ToInt32(oa * 255) & 255) << 24) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
        }

        public void Blend(double x, double y, uint c, double a) => BlendPx(CellX(x, y), CellY(x, y), c, a);

        public void Disc(double cx, double cy, double r, uint c, double a) => Disc(cx, cy, r, (d, g) => c, (d, g) => a);
        public void Disc(double cx, double cy, double r, uint c, Func<double, double, double> a) => Disc(cx, cy, r, (d, g) => c, a);
        public void Disc(double cx, double cy, double r, Func<double, double, uint> c, double a) => Disc(cx, cy, r, c, (d, g) => a);

        public void Disc(double cx, double cy, double r, Func<double, double, uint> c, Func<double, double, double> a)
        {
            if (r < 0.5)
            {
                Blend(cx, cy, c(0, 0), a(0, 0));
                return;
            }
            Each(cx - r, cy - r, cx + r, cy + r, (x, y, sx, sy) =>
            {
                double dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                if (d2 > r * r) return;
                double d = Math.Sqrt(d2) / r, ang = Atan2(dy, dx);
                BlendPx(sx, sy, c(d, ang), a(d, ang));
            });
        }

        public uint Get(double x, double y)
        {
            int sx = CellX(x, y) - ox, sy = CellY(x, y) - oy;
            return sx < 0 || sy < 0 || sx >= w || sy >= h ? 0 : C[sy * w + sx];
        }

        public void Px(double x, double y, uint c) => Put(CellX(x, y), CellY(x, y), c, 1);

        public void Dot(double x, double y, uint c, double a = 1)
        {
            if (a > 0) Put(CellX(x, y), CellY(x, y), c, a);
        }

        public void Light(double x, double y, uint c, double k = 1) => Add(CellX(x, y), CellY(x, y), c, k);

        public void Each(double x0, double y0, double x1, double y1, Action<double, double, int, int> fn)
        {
            double ax = double.PositiveInfinity, bx = double.NegativeInfinity, ay = double.PositiveInfinity, by = double.NegativeInfinity;
            for (int k = 0; k < 4; k++)
            {
                double x = (k & 1) != 0 ? x1 : x0, y = (k & 2) != 0 ? y1 : y0, sx = ToX(x, y), sy = ToY(x, y);
                if (sx < ax) ax = sx;
                if (sx > bx) bx = sx;
                if (sy < ay) ay = sy;
                if (sy > by) by = sy;
            }
            // bounds stay doubles as in JS: a NaN box visits nothing
            double sx0 = Math.Max(Math.Floor(ax), ox), sx1 = Math.Min(Math.Ceiling(bx), ox + w - 1);
            double sy0 = Math.Max(Math.Floor(ay), oy), sy1 = Math.Min(Math.Ceiling(by), oy + h - 1);
            for (double sy = sy0; sy <= sy1; sy++)
            for (double sx = sx0; sx <= sx1; sx++)
            {
                double cx = sx + 0.5, cy = sy + 0.5;
                fn(FromX(cx, cy), FromY(cx, cy), (int)sx, (int)sy);
            }
        }

        public void Shade(double x0, double y0, double x1, double y1, ShadeFn fn)
        {
            Out o = shadeOut;
            Each(x0, y0, x1, y1, (lx, ly, sx, sy) =>
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

        public void Line(double x0, double y0, double x1, double y1, uint c, double a = 1) => Line(x0, y0, x1, y1, c, null, a, null);
        public void Line(double x0, double y0, double x1, double y1, uint c, Func<double, double> a) => Line(x0, y0, x1, y1, c, null, 1, a);
        public void Line(double x0, double y0, double x1, double y1, Func<double, uint> c, double a = 1) => Line(x0, y0, x1, y1, 0, c, a, null);
        public void Line(double x0, double y0, double x1, double y1, Func<double, uint> c, Func<double, double> a) => Line(x0, y0, x1, y1, 0, c, 1, a);

        void Line(double x0, double y0, double x1, double y1, uint c, Func<double, uint> fc, double a, Func<double, double> fa)
        {
            int sx = CellX(x0, y0), sy = CellY(x0, y0);
            int ex = CellX(x1, y1), ey = CellY(x1, y1);
            int dx = Math.Abs(ex - sx), dy = -Math.Abs(ey - sy), stepx = sx < ex ? 1 : -1, stepy = sy < ey ? 1 : -1;
            int n = Math.Max(dx, -dy);
            if (n == 0) n = 1;
            int err = dx + dy, i = 0;
            for (;;)
            {
                double u = (double)i++ / n;
                Put(sx, sy, fc != null ? fc(u) : c, fa != null ? fa(u) : a);
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

        public void Polyline(double[][] pts, uint c, double a = 1) => Polyline(pts, c, null, a, null);
        public void Polyline(double[][] pts, uint c, Func<double, double> a) => Polyline(pts, c, null, 1, a);
        public void Polyline(double[][] pts, Func<double, uint> c, double a = 1) => Polyline(pts, 0, c, a, null);
        public void Polyline(double[][] pts, Func<double, uint> c, Func<double, double> a) => Polyline(pts, 0, c, 1, a);

        void Polyline(double[][] pts, uint c, Func<double, uint> fc, double a, Func<double, double> fa)
        {
            for (int i = 1; i < pts.Length; i++) Line(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], c, fc, a, fa);
        }

        public void Beam(double x0, double y0, double x1, double y1, double width, uint c, double a = 1) => Beam(x0, y0, x1, y1, width, (u, v) => c, (u, v) => a);
        public void Beam(double x0, double y0, double x1, double y1, double width, uint c, Func<double, double, double> a) => Beam(x0, y0, x1, y1, width, (u, v) => c, a);
        public void Beam(double x0, double y0, double x1, double y1, double width, Func<double, double, uint> c, double a = 1) => Beam(x0, y0, x1, y1, width, c, (u, v) => a);

        public void Beam(double x0, double y0, double x1, double y1, double width, Func<double, double, uint> c, Func<double, double, double> a)
        {
            double r = width / 2, dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
            if (l2 == 0) l2 = 1e-9;
            Shade(Math.Min(x0, x1) - r, Math.Min(y0, y1) - r, Math.Max(x0, x1) + r, Math.Max(y0, y1) + r, (x, y, o) =>
            {
                double u = Clamp(((x - x0) * dx + (y - y0) * dy) / l2), px = x0 + dx * u - x, py = y0 + dy * u - y;
                double d2 = px * px + py * py;
                if (d2 > r * r) return 0;
                double v = Math.Sqrt(d2) / r;
                o.c = c(u, v);
                o.a = a(u, v);
                return 0;
            });
        }

        public void Circle(double cx, double cy, double r, uint c, double a = 1) => Circle(cx, cy, r, (d, g) => c, (d, g) => a);
        public void Circle(double cx, double cy, double r, uint c, Func<double, double, double> a) => Circle(cx, cy, r, (d, g) => c, a);
        public void Circle(double cx, double cy, double r, Func<double, double, uint> c, double a = 1) => Circle(cx, cy, r, c, (d, g) => a);

        public void Circle(double cx, double cy, double r, Func<double, double, uint> c, Func<double, double, double> a)
        {
            if (r < 0.6)
            {
                Dot(cx, cy, c(0, 0), a(0, 0));
                return;
            }
            Shade(cx - r, cy - r, cx + r, cy + r, (x, y, o) =>
            {
                double dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                if (d2 > r * r) return 0;
                double d = Math.Sqrt(d2) / r, ang = Atan2(dy, dx);
                o.c = c(d, ang);
                o.a = a(d, ang);
                return 0;
            });
        }

        public void Ellipse(double cx, double cy, double rx, double ry, uint c, double a = 1) => Ellipse(cx, cy, rx, ry, (d, g) => c, (d, g) => a);
        public void Ellipse(double cx, double cy, double rx, double ry, uint c, Func<double, double, double> a) => Ellipse(cx, cy, rx, ry, (d, g) => c, a);
        public void Ellipse(double cx, double cy, double rx, double ry, Func<double, double, uint> c, double a = 1) => Ellipse(cx, cy, rx, ry, c, (d, g) => a);

        public void Ellipse(double cx, double cy, double rx, double ry, Func<double, double, uint> c, Func<double, double, double> a)
        {
            if (rx <= 0 || ry <= 0) return;
            Shade(cx - rx, cy - ry, cx + rx, cy + ry, (x, y, o) =>
            {
                double dx = (x - cx) / rx, dy = (y - cy) / ry, d2 = dx * dx + dy * dy;
                if (d2 > 1) return 0;
                double d = Math.Sqrt(d2), ang = Atan2(dy, dx);
                o.c = c(d, ang);
                o.a = a(d, ang);
                return 0;
            });
        }

        public void Ring(double cx, double cy, double r, double wd, uint c, double a = 1) => Ring(cx, cy, r, wd, (v, g) => c, (v, g) => a);
        public void Ring(double cx, double cy, double r, double wd, uint c, Func<double, double, double> a) => Ring(cx, cy, r, wd, (v, g) => c, a);
        public void Ring(double cx, double cy, double r, double wd, Func<double, double, uint> c, double a = 1) => Ring(cx, cy, r, wd, c, (v, g) => a);

        public void Ring(double cx, double cy, double r, double wd, Func<double, double, uint> c, Func<double, double, double> a)
        {
            double r0 = Math.Max(0, r - wd);
            Shade(cx - r, cy - r, cx + r, cy + r, (x, y, o) =>
            {
                double dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                if (d2 > r * r || d2 < r0 * r0) return 0;
                double span = r - r0; // (r - r0 || 1): 0 and NaN fall back to 1
                double v = wd > 0 ? (Math.Sqrt(d2) - r0) / (span != 0 && !double.IsNaN(span) ? span : 1) : 1, ang = Atan2(dy, dx);
                o.c = c(v, ang);
                o.a = a(v, ang);
                return 0;
            });
        }

        public void Arc(double cx, double cy, double r, double wd, double a0, double a1, uint c, double a = 1) => Arc(cx, cy, r, u => wd, a0, a1, (u, v) => c, (u, v) => a);
        public void Arc(double cx, double cy, double r, double wd, double a0, double a1, uint c, Func<double, double, double> a) => Arc(cx, cy, r, u => wd, a0, a1, (u, v) => c, a);
        public void Arc(double cx, double cy, double r, double wd, double a0, double a1, Func<double, double, uint> c, double a = 1) => Arc(cx, cy, r, u => wd, a0, a1, c, (u, v) => a);
        public void Arc(double cx, double cy, double r, double wd, double a0, double a1, Func<double, double, uint> c, Func<double, double, double> a) => Arc(cx, cy, r, u => wd, a0, a1, c, a);
        public void Arc(double cx, double cy, double r, Func<double, double> wd, double a0, double a1, uint c, double a = 1) => Arc(cx, cy, r, wd, a0, a1, (u, v) => c, (u, v) => a);
        public void Arc(double cx, double cy, double r, Func<double, double> wd, double a0, double a1, uint c, Func<double, double, double> a) => Arc(cx, cy, r, wd, a0, a1, (u, v) => c, a);
        public void Arc(double cx, double cy, double r, Func<double, double> wd, double a0, double a1, Func<double, double, uint> c, double a = 1) => Arc(cx, cy, r, wd, a0, a1, c, (u, v) => a);

        public void Arc(double cx, double cy, double r, Func<double, double> wd, double a0, double a1, Func<double, double, uint> c, Func<double, double, double> a)
        {
            double span = a1 - a0;
            Shade(cx - r, cy - r, cx + r, cy + r, (x, y, o) =>
            {
                double dx = x - cx, dy = y - cy, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > r) return 0;
                double ang = Atan2(dy, dx) - a0;
                ang -= Math.Floor(ang / (Math.PI * 2)) * Math.PI * 2;
                if (ang > span) return 0;
                double u = ang / span, ww = wd(u);
                if (ww <= 0 || d < r - ww) return 0;
                double v = (d - (r - ww)) / ww;
                o.c = c(u, v);
                o.a = a(u, v);
                return 0;
            });
        }

        public void Rect(double x, double y, double rw, double rh, uint c, double a = 1)
        {
            if (sn == 0)
            {
                double x0 = Round(x), y0 = Round(y), x1 = Round(x + rw), y1 = Round(y + rh);
                for (double yy = y0; yy < y1; yy++)
                for (double xx = x0; xx < x1; xx++)
                    Put(CellX(xx, yy), CellY(xx, yy), c, a);
                return;
            }
            Shade(x, y, x + rw, y + rh, (lx, ly, o) =>
            {
                if (lx < x || ly < y || lx >= x + rw || ly >= y + rh) return 0;
                o.c = c;
                o.a = a;
                return 0;
            });
        }

        public void Poly(double[][] pts, uint c, double a = 1) => Poly(pts, (x, y) => c, (x, y) => a);
        public void Poly(double[][] pts, uint c, Func<double, double, double> a) => Poly(pts, (x, y) => c, a);
        public void Poly(double[][] pts, Func<double, double, uint> c, double a = 1) => Poly(pts, c, (x, y) => a);

        public void Poly(double[][] pts, Func<double, double, uint> c, Func<double, double, double> a)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (double[] pt in pts)
            {
                if (pt[0] < x0) x0 = pt[0];
                if (pt[0] > x1) x1 = pt[0];
                if (pt[1] < y0) y0 = pt[1];
                if (pt[1] > y1) y1 = pt[1];
            }
            int n = pts.Length;
            Shade(x0, y0, x1, y1, (x, y, o) =>
            {
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                    if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
                }
                if (!inside) return 0;
                o.c = c(x, y);
                o.a = a(x, y);
                return 0;
            });
        }

        public void Tri(double x0, double y0, double x1, double y1, double x2, double y2, uint c, double a = 1) =>
            Poly(new[] { new[] { x0, y0 }, new[] { x1, y1 }, new[] { x2, y2 } }, c, a);
        public void Tri(double x0, double y0, double x1, double y1, double x2, double y2, uint c, Func<double, double, double> a) =>
            Poly(new[] { new[] { x0, y0 }, new[] { x1, y1 }, new[] { x2, y2 } }, c, a);
        public void Tri(double x0, double y0, double x1, double y1, double x2, double y2, Func<double, double, uint> c, double a = 1) =>
            Poly(new[] { new[] { x0, y0 }, new[] { x1, y1 }, new[] { x2, y2 } }, c, a);
        public void Tri(double x0, double y0, double x1, double y1, double x2, double y2, Func<double, double, uint> c, Func<double, double, double> a) =>
            Poly(new[] { new[] { x0, y0 }, new[] { x1, y1 }, new[] { x2, y2 } }, c, a);

        public void Glow(double x, double y, double r, uint c, double k = 0.5)
        {
            if (r < 1 || k <= 0.004) return;
            double cx = ToX(x, y), cy = ToY(x, y);
            double x0 = Math.Floor(cx - r), x1 = Math.Ceiling(cx + r), y0 = Math.Floor(cy - r), y1 = Math.Ceiling(cy + r);
            for (double sy = y0; sy <= y1; sy++)
            {
                double dy = (sy + 0.5 - cy) / r;
                for (double sx = x0; sx <= x1; sx++)
                {
                    double dx = (sx + 0.5 - cx) / r, d = dx * dx + dy * dy;
                    if (d >= 1) continue;
                    double f = 1 - Math.Sqrt(d);
                    Add(ScreenInt(sx), ScreenInt(sy), c, f * f * k);
                }
            }
        }

        public void GlowLine(double x0, double y0, double x1, double y1, double r, uint c, double k = 0.5)
        {
            if (r < 1 || k <= 0.004) return;
            double ax = ToX(x0, y0), ay = ToY(x0, y0), bx = ToX(x1, y1), by = ToY(x1, y1);
            double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            if (l2 == 0) l2 = 1e-9;
            double X0 = Math.Floor(Math.Min(ax, bx) - r), X1 = Math.Ceiling(Math.Max(ax, bx) + r);
            double Y0 = Math.Floor(Math.Min(ay, by) - r), Y1 = Math.Ceiling(Math.Max(ay, by) + r);
            for (double sy = Y0; sy <= Y1; sy++)
            for (double sx = X0; sx <= X1; sx++)
            {
                double px = sx + 0.5, py = sy + 0.5, u = Clamp(((px - ax) * dx + (py - ay) * dy) / l2);
                double ex = ax + dx * u - px, ey = ay + dy * u - py, d = Math.Sqrt(ex * ex + ey * ey) / r;
                if (d >= 1) continue;
                double f = 1 - d;
                Add(ScreenInt(sx), ScreenInt(sy), c, f * f * k);
            }
        }

        public void GlowRing(double x, double y, double ringR, double r, uint c, double k = 0.5)
        {
            if (r < 1 || k <= 0.004) return;
            double cx = ToX(x, y), cy = ToY(x, y), e = ringR + r;
            for (double sy = Math.Floor(cy - e); sy <= Math.Ceiling(cy + e); sy++)
            for (double sx = Math.Floor(cx - e); sx <= Math.Ceiling(cx + e); sx++)
            {
                double d = Math.Abs(Hypot(sx + 0.5 - cx, sy + 0.5 - cy) - ringR) / r;
                if (d >= 1) continue;
                double f = 1 - d;
                Add(ScreenInt(sx), ScreenInt(sy), c, f * f * k);
            }
        }

        public void MaskFill(Mask m, double x, double y, uint c, double a = 1) => MaskFill(m, x, y, (mx, my) => c, (mx, my) => a);
        public void MaskFill(Mask m, double x, double y, uint c, Func<int, int, double> a) => MaskFill(m, x, y, (mx, my) => c, a);
        public void MaskFill(Mask m, double x, double y, Func<int, int, uint> c, double a = 1) => MaskFill(m, x, y, c, (mx, my) => a);

        public void MaskFill(Mask m, double x, double y, Func<int, int, uint> c, Func<int, int, double> a)
        {
            for (int my = 0; my < m.H; my++)
            for (int mx = 0; mx < m.W; mx++)
                if (m.Data[my * m.W + mx] != 0) Dot(x + mx, y + my, c(mx, my), a(mx, my));
        }

        public void Outline(Mask m, double x, double y, uint c, double a = 1) => Outline(m, x, y, (mx, my) => c, (mx, my) => a);
        public void Outline(Mask m, double x, double y, uint c, Func<int, int, double> a) => Outline(m, x, y, (mx, my) => c, a);
        public void Outline(Mask m, double x, double y, Func<int, int, uint> c, double a = 1) => Outline(m, x, y, c, (mx, my) => a);

        public void Outline(Mask m, double x, double y, Func<int, int, uint> c, Func<int, int, double> a)
        {
            bool At(int mx, int my) => mx >= 0 && my >= 0 && mx < m.W && my < m.H && m.Data[my * m.W + mx] != 0;
            for (int my = -1; my <= m.H; my++)
            for (int mx = -1; mx <= m.W; mx++)
                if (!At(mx, my) && (At(mx - 1, my) || At(mx + 1, my) || At(mx, my - 1) || At(mx, my + 1)))
                    Dot(x + mx, y + my, c(mx, my), a(mx, my));
        }
    }
}
