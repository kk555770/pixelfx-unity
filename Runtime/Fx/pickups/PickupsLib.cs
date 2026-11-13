// Helpers for the pickups: a scratch layer to build outlined sprites in, floor shadows, twinkles and bobs.
// The rebuilt pickups use techniques from Sprite Fusion's open-source pickup and sparkle, MIT, Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public static class PickupsLib
    {
        /// <summary>
        /// The palette as at least n color steps, dark to bright, so effects can index C[0]..C[n-1] whatever ramp a
        /// caller passes: a 7-step ramp repeats its brightest step, a short one is stretched step by step (no blending).
        /// </summary>
        public static uint[] FitAtLeast(string[] palette, int n = 8)
        {
            var r = new List<uint>(Ramp(palette));
            var o = new uint[n];
            if (r.Count == 0)
            {
                for (int i = 0; i < n; i++) o[i] = WHITE;
                return o;
            }
            if (r.Count >= n - 1)
            {
                while (r.Count < n) r.Add(r[r.Count - 1]);
                return r.ToArray();
            }
            for (int i = 0; i < n; i++) o[i] = r[RoundInt((double)(i * (r.Count - 1)) / (n - 1))];
            return o;
        }


        /// <summary>
        /// A scratch layer (the JS Layer): draw a sprite into it with its own painter (same local coordinates as the
        /// effect), give it a dark outline, recolor it, then stamp it onto the frame. Lets a procedural shape get a
        /// hand-made 1 px outline and lets shines sweep across exactly the sprite's pixels.
        /// </summary>
        public sealed class ScratchLayer
        {
            public readonly Frame F;
            public readonly Painter Q;
            readonly uint[] tmp;

            public ScratchLayer(int w = 64, int h = 64)
            {
                F = new Frame(w, h);
                Q = new Painter(F);
                tmp = new uint[w * h];
            }

            /// <summary>Clear it and put its top-left at local (x0, y0). Returns the layer's painter.</summary>
            public Painter Begin(double x0, double y0)
            {
                F.Ox = (int)Math.Floor(x0);
                F.Oy = (int)Math.Floor(y0);
                F.Clear();
                return Q.Reset(F).Place(0, 0);
            }

            int Idx(double x, double y)
            {
                double i = Math.Floor(x) - F.Ox, j = Math.Floor(y) - F.Oy;
                if (!(i >= 0 && j >= 0 && i < F.W && j < F.H)) return -1;
                return (int)(j * F.W + i);
            }

            public uint Get(double x, double y)
            {
                int i = Idx(x, y);
                return i < 0 ? 0 : F.Color[i];
            }

            public void Set(double x, double y, uint c)
            {
                int i = Idx(x, y);
                if (i >= 0) F.Color[i] = c;
            }

            /// <summary>Fill each pixel from fn(x, y) at the pixel's center (x + 0.5, y + 0.5): return a color or 0.</summary>
            public void Fill(double x0, double y0, double x1, double y1, Func<double, double, uint> fn)
            {
                for (double y = Math.Floor(y0); y <= y1; y++)
                for (double x = Math.Floor(x0); x <= x1; x++)
                {
                    uint c = fn(x + 0.5, y + 0.5);
                    if (c != 0) Set(x, y, c);
                }
            }

            /// <summary>A 1 px outline around everything drawn in color c. corners adds diagonals.</summary>
            public ScratchLayer Outline(uint c, bool corners = false) => Outline((x, y, n) => c, corners);

            /// <summary>A 1 px outline around everything drawn: fn(x, y, neighbour color). corners adds diagonals.</summary>
            public ScratchLayer Outline(Func<double, double, uint, uint> c, bool corners = false)
            {
                uint[] C = F.Color, T = tmp;
                int w = F.W, h = F.H;
                Array.Clear(T, 0, T.Length);
                for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int k = j * w + i;
                    if (C[k] != 0) continue;
                    uint n = 0;
                    if (i > 0 && C[k - 1] != 0) n = C[k - 1];
                    else if (i < w - 1 && C[k + 1] != 0) n = C[k + 1];
                    else if (j > 0 && C[k - w] != 0) n = C[k - w];
                    else if (j < h - 1 && C[k + w] != 0) n = C[k + w];
                    if (n == 0 && corners)
                    {
                        if (i > 0 && j > 0 && C[k - w - 1] != 0) n = C[k - w - 1];
                        else if (i < w - 1 && j > 0 && C[k - w + 1] != 0) n = C[k - w + 1];
                        else if (i > 0 && j < h - 1 && C[k + w - 1] != 0) n = C[k + w - 1];
                        else if (i < w - 1 && j < h - 1 && C[k + w + 1] != 0) n = C[k + w + 1];
                    }
                    if (n != 0) T[k] = c(i + F.Ox, j + F.Oy, n);
                }
                for (int k = 0; k < w * h; k++)
                    if (T[k] != 0) C[k] = T[k];
                return this;
            }

            /// <summary>Recolor every drawn pixel: fn(x, y, c) returns the new color (0 keeps it).</summary>
            public ScratchLayer Map(Func<double, double, uint, uint> fn)
            {
                uint[] C = F.Color;
                for (int k = 0; k < C.Length; k++)
                    if (C[k] != 0)
                    {
                        uint c = fn(k % F.W + F.Ox, k / F.W + F.Oy, C[k]);
                        if (c != 0) C[k] = c;
                    }
                return this;
            }

            /// <summary>Stamp the layer onto p, offset by (dx, dy), at alpha a (dithered).</summary>
            public ScratchLayer Draw(Painter p, double dx = 0, double dy = 0, double a = 1) => Draw(p, dx, dy, a, false, 0, 0);

            /// <summary>Stamp it with a clip: only columns clip0 &lt;= x &lt; clip1.</summary>
            public ScratchLayer Draw(Painter p, double dx, double dy, double a, double clip0, double clip1) => Draw(p, dx, dy, a, true, clip0, clip1);

            ScratchLayer Draw(Painter p, double dx, double dy, double a, bool clip, double clip0, double clip1)
            {
                uint[] C = F.Color;
                for (int k = 0; k < C.Length; k++)
                {
                    if (C[k] == 0) continue;
                    double x = k % F.W + F.Ox + dx;
                    if (clip && (x < clip0 || x >= clip1)) continue;
                    p.Dot(x, k / F.W + F.Oy + dy, C[k], a);
                }
                return this;
            }
        }

        static readonly uint SHADOW = Hex("#100e17");

        /// <summary>
        /// A floor shadow centered on x, half-width rx: a solid dark band on the floor line (y = 0) and a narrower one
        /// under it, with dithered ends. Shrink rx as the thing above rises.
        /// </summary>
        public static void Shadow(Painter p, double x, double rx, double k = 1)
        {
            if (rx < 1) return;
            for (int row = 0; row < 2; row++)
            {
                double w = rx - row * 2;
                for (double i = Math.Floor(x - w); i < Math.Ceiling(x + w); i++)
                {
                    double e = Math.Abs(i + 0.5 - x) / w;
                    if (e <= 1) p.Dot(i, row, SHADOW, row != 0 ? k : k * (e < 0.8 ? 1 : 0.5));
                }
            }
        }

        /// <summary>A four-point twinkle of arm length s (whole pixels): white core, bright inner arms, colored tips.</summary>
        public static void Twinkle(Painter p, double x, double y, double s, uint c) => Twinkle(p, x, y, s, c, c, WHITE);

        public static void Twinkle(Painter p, double x, double y, double s, uint c, uint mid, uint core = WHITE)
        {
            s = Round(s);
            if (s <= 0) return;
            for (int d = 1; d <= s - 1; d++)
            {
                uint k = d < s / 2 ? mid : c;
                p.Px(x + d, y, k);
                p.Px(x - d, y, k);
                p.Px(x, y + d, k);
                p.Px(x, y - d, k);
            }
            if (s >= 4)
            {
                p.Px(x + 1, y + 1, c);
                p.Px(x - 1, y - 1, c);
                p.Px(x + 1, y - 1, c);
                p.Px(x - 1, y + 1, c);
            }
            p.Px(x, y, core);
        }

        /// <summary>A seamless bob: up-and-down offset in px for loop phase ph (0..1), n bobs a loop.</summary>
        public static double Bob(double ph, double amp, double n = 1, double off = 0) => -amp * Px.Sin((ph * n + off) * TAU);

        static readonly double[] PillowLight = { -0.5, -0.6, 0.62 };

        /// <summary>
        /// Fill a puffy shape into layer L from a signed distance sdf(x, y) in px (negative inside). The surface is
        /// flat in the middle and rounds over in a bevel px wide at the edge; color(b, e, x, y) gets the light b
        /// (about -0.3 facing away .. 1 facing the light, 0.62 flat) and the depth e in px from the edge. clip(x, y)
        /// returning true cuts a pixel away without shading the cut (a break, a bite).
        /// </summary>
        public static void Pillow(ScratchLayer L, double x0, double y0, double x1, double y1, Func<double, double, double> sdf, Func<double, double, double, double, uint> color,
            double bevel = 3, double[] light = null, Func<double, double, bool> clip = null)
        {
            light = light ?? PillowLight;
            L.Fill(x0, y0, x1, y1, (x, y) =>
            {
                double d = sdf(x, y);
                if (d > 0 || (clip != null && clip(x, y))) return 0;
                double e = -d;
                double gx = sdf(x + 0.5, y) - sdf(x - 0.5, y), gy = sdf(x, y + 0.5) - sdf(x, y - 0.5);
                double gl = Hypot(gx, gy);
                if (gl == 0 || double.IsNaN(gl)) gl = 1;
                gx /= gl;
                gy /= gl;
                double k = Math.Max(0, 1 - e / bevel) * 1.25, sn = Px.Sin(k), cs = Px.Cos(k);
                return color(gx * sn * light[0] + gy * sn * light[1] + cs * light[2], e, x, y);
            });
        }

        /// <summary>Signed distance (px, roughly) to a heart of half-width w centered at (cx, cy): the classic sextic curve.</summary>
        public static double HeartSdf(double x, double y, double cx, double cy, double w) => HeartSdf(x, y, cx, cy, w, w);

        public static double HeartSdf(double x, double y, double cx, double cy, double w, double h)
        {
            double S = w / 1.14, X = (x - cx) / S, Y = -(y - cy) / (h / 1.14) + 0.12;
            double q = X * X + Y * Y - 1, f = q * q * q - X * X * Y * Y * Y;
            double gx = 6 * X * q * q - 2 * X * Y * Y * Y, gy = 6 * Y * q * q - 3 * X * X * Y * Y;
            return f / Math.Max(Hypot(gx, gy), 0.6) * S;
        }

        static readonly uint INK = Hex("#0b0814");

        /// <summary>
        /// A soft floor shadow at x, half-width rx: dithered darkness on the floor line, fuller in the middle, with a
        /// shorter second row under it. k (0..1) is its strength: lift the thing above and the shadow shrinks and thins.
        /// </summary>
        public static void BlobShadow(Painter p, double x, double rx, double k = 1, double y = 0)
        {
            if (rx < 0.8 || k <= 0) return;
            for (double i = Math.Floor(x - rx); i <= Math.Ceiling(x + rx); i++)
            {
                double u = Math.Abs(i + 0.5 - x) / rx;
                if (u > 1) continue;
                double a = k * (1 - u * u);
                p.Dot(i, y, INK, a * 1.1);
                if (u < 0.6) p.Dot(i, y + 1, INK, a * 0.6);
            }
        }

        /// <summary>A four-point star glint: arms of arm px in c, dimmer tips, a white heart, its own light.</summary>
        public static void Glint(Painter p, double x, double y, double arm, uint c, double k = 0.4) => Glint(p, x, y, arm, c, k, Scale(c, 0.8));

        public static void Glint(Painter p, double x, double y, double arm, uint c, double k, uint tip)
        {
            double n = Round(arm), X = Math.Floor(x), Y = Math.Floor(y);
            if (n < 0) return;
            for (int d = 1; d <= n; d++)
            {
                uint cc = d == n && n > 1 ? tip : c;
                p.Px(X + d, Y, cc);
                p.Px(X - d, Y, cc);
                p.Px(X, Y + d, cc);
                p.Px(X, Y - d, cc);
            }
            p.Px(X, Y, n > 0 ? WHITE : c);
            p.Glow(X + 0.5, Y + 0.5, 3 + n * 2.5, c, k);
        }

        /// <summary>Additive light in an ellipse (a pool of light on the floor): k at the center falling off to the rim.</summary>
        public static void Pool(Painter p, double x, double y, double rx, double ry, uint c, double k)
        {
            if (k <= 0.004 || rx < 1) return;
            p.Shade(x - rx, y - ry, x + rx, y + ry, (px, py, o) =>
            {
                double dx = (px - x) / rx, dy = (py - y) / ry, d = dx * dx + dy * dy;
                if (d >= 1) return 0;
                double f = 1 - Math.Sqrt(d);
                o.l = c;
                o.k = f * f * k;
                return 0;
            });
        }

        /// <summary>
        /// A coin of radius r tumbling: its face turns by spin about an axis at screen angle ax, showing its rim
        /// as it goes edge-on. A lit rim round a face with a shine, built in layer L with a dark outline, lit from the
        /// top left. C: an 8-step gold ramp. snap puts its center on a pixel corner (even sizes) so it holds its shape
        /// while it moves; pass snap false and a pixel-center y for odd heights.
        /// </summary>
        public static void Coin(ScratchLayer L, Painter p, double x, double y, double r, double ax, double spin, uint[] C, bool white = false, double a = 1, bool snap = true)
        {
            double X = snap ? Round(x) : x, Y = snap ? Round(y) : y, cs = Px.Cos(spin), sn = Px.Sin(spin);
            double b = Math.Max(0.5, r * Math.Abs(cs)), th = 1.2 * sn * Sign(cs != 0 && !double.IsNaN(cs) ? cs : 1), ca = Px.Cos(ax), sa = Px.Sin(ax);
            double face = Math.Abs(cs), R = Math.Ceiling(r) + 2;
            L.Begin(Math.Floor(X) - R, Math.Floor(Y) - R);
            L.Fill(Math.Floor(X) - R, Math.Floor(Y) - R, Math.Floor(X) + R, Math.Floor(Y) + R, (px, py) =>
            {
                double dx = px - X, dy = py - Y, u = dx * ca + dy * sa, w = -dx * sa + dy * ca;
                double ef = Hypot(u / r, w / b), er = Hypot(u / r, (w - th) / b);
                if (white && (ef <= 1 || er <= 1)) return WHITE;
                if (ef <= 1)
                {
                    // in the face's own frame: the raised rim, a recessed ring inside it, a raised middle with a shine
                    double lit = -(dx * 0.62 + dy * 0.78) / Math.Max(1, Hypot(dx, dy));
                    int dim = face < 0.45 ? 1 : 0;
                    if (b < 1.1) return lit > 0 ? C[6] : C[4];
                    if (ef > 0.72) return C[(lit > 0.3 ? 6 : lit > -0.35 ? 5 : 3) - dim];
                    if (b > 2.2 && ef > 0.42) return C[(lit > 0.2 ? 4 : 5) - dim];
                    if (face > 0.6 && lit > 0.5) return C[7];
                    return C[5 - dim];
                }
                if (er <= 1) return Math.Abs(w - th) < Math.Abs(w) ? C[2] : C[3];
                return 0;
            });
            L.Outline(C[1]).Draw(p, 0, 0, a);
        }

        static readonly double[][] GemletNormals = { new[] { 0.0, -1 }, new[] { -0.7, -0.7 }, new[] { 0.7, -0.7 }, new[] { -0.6, 0.8 }, new[] { 0.0, 1 }, new[] { 0.6, 0.8 } };

        /// <summary>
        /// A small cut gem of radius r turned to angle ang (radians, on screen): a crown with a table over a pointed
        /// pavilion, its facets lit from the top left, a white glint on the table; G is a ramp dark to bright (6+
        /// steps). Built in L with a dark outline.
        /// </summary>
        public static void Gemlet(ScratchLayer L, Painter p, double x, double y, double r, double ang, uint[] G, bool white = false, double a = 1)
        {
            double X = Round(x), Y = Round(y), ca = Px.Cos(ang), sa = Px.Sin(ang), R = Math.Ceiling(r * 1.4) + 2;
            int n = G.Length;
            double ct = r * 0.75, tw = r * 0.5, pv = r * 1.3;
            L.Begin(X - R, Y - R);
            L.Fill(X - R, Y - R, X + R - 1, Y + R - 1, (px, py) =>
            {
                double dx = px - X, dy = py - Y, u = dx * ca + dy * sa, w = -dx * sa + dy * ca;
                int facet;
                if (w < 0)
                {
                    if (w < -ct || Math.Abs(u) > r - -w / ct * (r - tw)) return 0;
                    facet = w < -ct * 0.45 && Math.Abs(u) < tw * 0.95 ? 0 : u < 0 ? 1 : 2;
                }
                else
                {
                    if (w > pv || Math.Abs(u) > r * (1 - w / pv)) return 0;
                    facet = Math.Abs(u) < 0.5 ? 4 : u < 0 ? 3 : 5;
                }
                if (white) return WHITE;
                // each facet's normal, turned with the gem; the light comes from the top left of the screen
                double[] N = GemletNormals[facet];
                double nx = N[0] * ca - N[1] * sa, ny = N[0] * sa + N[1] * ca, l = -(nx * 0.62 + ny * 0.78);
                if (facet == 0 && l > 0.3 && u < 0) return WHITE;
                return l > 0.6 ? G[n - 1] : l > 0.2 ? G[n - 2] : l > -0.2 ? G[n - 3] : l > -0.6 ? G[n - 4] : G[n - 5];
            });
            L.Outline(G[0]).Draw(p, 0, 0, a);
        }

        /// <summary>A facet plane of a cut gem: outward unit normal n (y up, z towards the viewer), distance d, kind.</summary>
        public sealed class Plane
        {
            public double[] n;
            public double d;
            public int kind;
        }

        /// <summary>A gem as a convex solid: its facet planes, its radius and how far it reaches above and below the girdle.</summary>
        public sealed class Solid
        {
            public Plane[] planes;
            public double r, top, bottom;
        }

        /// <summary>
        /// A round brilliant: a table, crown facets, a thin girdle, pavilion facets down to a point; n facets round.
        /// </summary>
        public static Solid Brilliant(double r = 7.5, double crown = 3.6, double table = 0.52, double pavilion = 8, double girdle = 0.8, int n = 8)
        {
            var planes = new List<Plane> { new Plane { n = new[] { 0.0, 1, 0 }, d = crown + girdle / 2, kind = 0 } };
            double cs = Atan2(crown, r * (1 - table)), ps = Atan2(pavilion, r);
            for (int i = 0; i < n * 2; i++)
            {
                double a = i * Math.PI / n;
                int half = i % 2;
                double c = half != 0 ? Math.Min(1.45, cs + 0.22) : cs, pv = half != 0 ? ps - 0.1 : ps;
                // crown: main facets, and steeper ones between them by the girdle
                planes.Add(new Plane { n = new[] { Px.Sin(c) * Px.Cos(a), Px.Cos(c), Px.Sin(c) * Px.Sin(a) }, d = r * Px.Sin(c) * (half != 0 ? 1.012 : 1) + girdle / 2 * Px.Cos(c), kind = half != 0 ? 2 : 1 });
                // the girdle: a ring of thin upright faces
                planes.Add(new Plane { n = new[] { Px.Cos(a), 0, Px.Sin(a) }, d = r * 1.0, kind = 3 });
                // pavilion: main facets to the point, shallower ones between them by the girdle
                planes.Add(new Plane { n = new[] { Px.Sin(pv) * Px.Cos(a), -Px.Cos(pv), Px.Sin(pv) * Px.Sin(a) }, d = r * Px.Sin(pv) * (half != 0 ? 1.03 : 1) + girdle / 2 * Px.Cos(pv), kind = half != 0 ? 5 : 4 });
            }
            return new Solid { planes = planes.ToArray(), r = r, top = crown + girdle / 2, bottom = pavilion + girdle / 2 };
        }

        /// <summary>
        /// A crystal: an n-sided prism (apothem r, half-height half) with a pointed cap of height top and a shorter
        /// point of height bottom, as facet planes for DrawGem (kinds 6 prism, 7 cap, 8 point).
        /// </summary>
        public static Solid Prism(double r = 4.6, double half = 5.5, double top = 7.5, double bottom = 6, int n = 6)
        {
            var planes = new List<Plane>();
            double ct = Atan2(top, r), cb = Atan2(bottom, r);
            for (int i = 0; i < n; i++)
            {
                double a = i * TAU / n;
                planes.Add(new Plane { n = new[] { Px.Cos(a), 0, Px.Sin(a) }, d = r, kind = 6 });
                planes.Add(new Plane { n = new[] { Px.Sin(ct) * Px.Cos(a), Px.Cos(ct), Px.Sin(ct) * Px.Sin(a) }, d = Px.Sin(ct) * r + Px.Cos(ct) * half, kind = 7 });
                planes.Add(new Plane { n = new[] { Px.Sin(cb) * Px.Cos(a), -Px.Cos(cb), Px.Sin(cb) * Px.Sin(a) }, d = Px.Sin(cb) * r + Px.Cos(cb) * half, kind = 8 });
            }
            return new Solid { planes = planes.ToArray(), r = r / Px.Cos(Math.PI / n), top = half + top, bottom = half + bottom };
        }

        /// <summary>
        /// Draw gem g turned by spin (radians about its upright axis), its center (the girdle) at (x, y), seen from a
        /// little above (tilt), into layer L with a dark outline. Each pixel finds the facet it looks at; crown facets
        /// catch the light from the top left, pavilion facets glow with the light bouncing up through the stone.
        /// C: ramp dark to bright (8). Returns the brightest spot for a glint.
        /// </summary>
        public static (double x, double y, double k) DrawGem(ScratchLayer L, Painter p, Solid g, double x, double y, double spin, uint[] C,
            double tilt = 0.32, bool white = false, double a = 1, bool edges = true, double gain = 1)
        {
            double cs = Px.Cos(spin), sn = Px.Sin(spin), ct = Px.Cos(tilt), st = Px.Sin(tilt);
            var P = new Plane[g.planes.Length];
            for (int i = 0; i < P.Length; i++)
            {
                Plane q = g.planes[i];
                P[i] = new Plane { n = new[] { q.n[0] * cs - q.n[2] * sn, q.n[1], q.n[0] * sn + q.n[2] * cs }, d = q.d, kind = q.kind };
            }
            // light from the top left, a little in front; the eye looks down the tilted z axis
            const double Lx = -0.55, Ly = 0.72, Lz = 0.42;
            double[] D = { 0, -st, -ct }, V = { 0, st, ct };
            double Hl = Px.Hypot(Lx + V[0], Ly + V[1], Lz + V[2]);
            double[] H = { (Lx + V[0]) / Hl, (Ly + V[1]) / Hl, (Lz + V[2]) / Hl };
            double X = Round(x), Y = Round(y), R = Math.Ceiling(g.r) + 2, top = Math.Ceiling(g.top + 2), bot = Math.Ceiling(g.bottom + 2);
            double best = -1, bx = X, by = Y;
            var ids = new Dictionary<double, int>();
            var order = new List<double>();
            L.Begin(X - R, Y - top);
            L.Fill(X - R, Y - top, X + R, Y + bot, (px, py) =>
            {
                double sx = px - X, sy = -(py - Y);
                // the ray through this pixel: origin on the screen plane, straight down the view axis
                double o0 = sx, o1 = sy * ct, o2 = -sy * st;
                double t0 = -1e9, t1 = 1e9;
                int hit = -1;
                for (int qi = 0; qi < P.Length; qi++)
                {
                    Plane q = P[qi];
                    double nd = q.n[0] * D[0] + q.n[1] * D[1] + q.n[2] * D[2], no = q.n[0] * o0 + q.n[1] * o1 + q.n[2] * o2;
                    if (Math.Abs(nd) < 1e-6)
                    {
                        if (no > q.d) return 0;
                        continue;
                    }
                    double tt = (q.d - no) / nd;
                    if (nd < 0)
                    {
                        if (tt > t0)
                        {
                            t0 = tt;
                            hit = qi;
                        }
                    }
                    else if (tt < t1) t1 = tt;
                }
                if (hit < 0 || t0 > t1) return 0;
                if (white) return WHITE;
                double key = (Math.Floor(px) + 500) * 1000 + Math.Floor(py) + 500;
                if (!ids.ContainsKey(key)) order.Add(key);
                ids[key] = hit;
                Plane h = P[hit];
                double[] n = h.n;
                bool pav = h.kind == 4 || h.kind == 5 || h.kind == 8;
                // the pavilion shows light that came in through the crown and bounced back: mirror its normal
                double m0 = n[0], m1 = pav ? -n[1] * 0.6 : n[1], m2 = n[2], ml = Px.Hypot(m0, m1, m2);
                double lam = Math.Max(0, (m0 * Lx + m1 * Ly + m2 * Lz) / ml), spec = Px.Pow(Math.Max(0, (m0 * H[0] + m1 * H[1] + m2 * H[2]) / ml), 18);
                double b = pav ? 0.02 + 0.72 * Px.Pow(lam, 2.4) : h.kind == 3 ? 0.12 + 0.4 * lam : 0.04 + 0.66 * Px.Pow(lam, 1.8);
                // a crystal's upright faces are lit by the light's level direction; its cap catches it from above
                if (h.kind == 6) b = 0.16 + 0.66 * Px.Pow(Math.Max(0, (n[0] * Lx + n[2] * Lz) / Hypot(Lx, Lz)), 1.3);
                else if (h.kind == 7) b = 0.22 + 0.62 * Px.Pow(lam, 1.2);
                // a fracture shows the lit inside of the stone, rough and grainy; chips are small rough facets
                else if (h.kind == 9) b = 0.62 + 0.25 * lam;
                else if (h.kind == 10) b = 0.3 + 0.5 * lam + (Rand(Math.Floor(px - X), Math.Floor(py - Y), 43) - 0.5) * 0.08;
                if (h.kind != 9) b *= gain;
                if (h.kind == 0) b -= 0.04;
                double s = b + spec * 0.6;
                if (s > best)
                {
                    best = s;
                    bx = px;
                    by = py;
                }
                if (spec > 0.8) return pav ? C[6] : WHITE;
                double ci = Math.Max(1, Math.Min(6, Round(1 + s * 5.6)));
                return double.IsNaN(ci) ? 0 : C[(int)ci];
            });
            // the cut: where two facets meet, a pavilion edge sinks a shade and a crown ridge catches a shade more light
            if (!white && edges)
            {
                var outs = new List<(double x, double y, uint c)>();
                foreach (double key in order)
                {
                    int id = ids[key];
                    double kx = Math.Floor(key / 1000), ky = key - kx * 1000, ex = kx - 500, ey = ky - 500;
                    bool hasR = ids.TryGetValue(key + 1000, out int r), hasD = ids.TryGetValue(key + 1, out int d);
                    if ((hasR && r != id) || (hasD && d != id))
                    {
                        uint c = L.Get(ex, ey);
                        int i = Array.IndexOf(C, c);
                        int kind = P[id].kind;
                        bool pav = kind == 4 || kind == 5 || kind == 8;
                        if (i > 0) outs.Add((ex, ey, pav ? C[Math.Max(1, i - 1)] : C[Math.Min(7, i + 1)]));
                    }
                }
                foreach (var (ox, oy, c) in outs) L.Set(ox, oy, c);
            }
            L.Outline(C[0]).Draw(p, 0, 0, a);
            return (bx, by, best);
        }

        /// <summary>
        /// A thick coin turning on its upright axis: radius r, thickness th, turned by theta (0 faces you). Each row
        /// shows the face (squashed by cos theta, a raised rim, a recessed field, an embossed star) and, beside it,
        /// the reeded edge band (th |sin theta| wide). Lit from the top left; the face flashes white as it squares
        /// to the light. Built in L with a dark outline. C: 8-step ramp. Returns the flash strength 0..1.
        /// </summary>
        public static double Coin3d(ScratchLayer L, Painter p, double x, double y, double r, double th, double theta, uint[] C, double a = 1, bool white = false)
        {
            double ct = Px.Cos(theta), st = Px.Sin(theta), sg = ct < 0 ? -1 : 1, act = Math.Abs(ct), ast = Math.Abs(st);
            double xc = th / 2 * st * sg, nfx = st * sg;
            const double Lx = -0.55, Ly = 0.45, Lz = 0.7;
            double Ln = Px.Hypot(Lx, Ly, Lz);
            // the face's normal stays level, so its flash comes when it squares to the light's level direction
            double lam = (nfx * Lx + act * Lz) / Ln, Hx = Lx / Ln, Hz = Lz / Ln + 1, Hn = Hypot(Hx, Hz);
            double based = Math.Max(2, Math.Min(5, Round(2 + lam * 3.2)));
            double X = Round(x), Y = Round(y), B = Math.Ceiling(r + th) + 2;
            double flash = 0;
            L.Begin(X - B, Y - B);
            L.Fill(X - B, Y - B, X + B, Y + B, (px, py) =>
            {
                double sx = px - X, sy = -(py - Y);
                if (Math.Abs(sy) > r) return 0;
                double s = Math.Sqrt(1 - Px.Pow(sy / r, 2)), hw = r * s * act, xr = -(st < 0 ? -1 : 1) * s * r * ct, rw = th / 2 * ast;
                if (white && (Math.Abs(sx - xc) <= hw || Math.Abs(sx - xr) <= rw + 0.05)) return WHITE;
                if (Math.Abs(sx - xc) <= hw && act > 0.08)
                {
                    // on the face: un-squash to the face's own coordinates
                    double fx = (sx - xc) / act, fy = sy, d = Hypot(fx, fy) / r;
                    // a faint curve across the face makes the flash sweep over it as it turns
                    double tx = nfx + fx / r * 0.3 * act, sp = Px.Pow(Math.Max(0, (tx * Hx + act * Hz) / Hn / Hypot(tx, act)), 130);
                    flash = Math.Max(flash, sp);
                    // relief in whole ramp steps so it shows at every angle: a raised rim lit on its upper left, a recessed
                    // field, a raised diamond with lit and shaded faces
                    double lit = (-fx * 0.6 + fy * 0.8) / Math.Max(0.5, Hypot(fx, fy));
                    double k;
                    if (d > 0.76) k = based + (lit > 0.75 ? 2 : lit > 0.3 ? 1 : lit < -0.35 ? -1 : 0);
                    else
                    {
                        double q = (Math.Abs(fx) + Math.Abs(fy)) / (r * 0.52);
                        k = q >= 1 ? based - 1 : q < 0.45 ? based + 1 : fx <= 0 && fy >= 0 ? based + 1 : fx > 0 && fy < 0 ? based - 2 : based;
                    }
                    if (sp > 0.5) return sp > 0.8 ? WHITE : C[(int)Math.Min(7, k + 2)];
                    return C[(int)Math.Max(2, Math.Min(6, k))];
                }
                if (Math.Abs(sx - xr) <= rw + 0.05)
                {
                    // the reeded edge: grooves across the band, lit by the edge's own normal
                    double cphi = xr < 0 ? -s : s, n0 = cphi * ct, n1 = sy / r, n2 = -cphi * st, l = (n0 * Lx + n1 * Ly + n2 * Lz) / Ln;
                    double groove = Math.Floor((Px.Asin(sy / r) + Math.PI) * 9) % 2;
                    bool edge = Math.Abs(sx - xr) > rw - 0.6 && Sign(sx - xr) == Sign(xc - xr);
                    return C[(int)Math.Max(1, Math.Min(6, Round(2.6 + l * 2.6) - groove + (edge ? 1 : 0)))];
                }
                return 0;
            });
            L.Outline(C[1]).Draw(p, 0, 0, a);
            return flash;
        }
    }
}
