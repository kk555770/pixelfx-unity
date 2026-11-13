// Shared by the status effects: the character's shape, noise that loops, and the toolkit's flame, smoke and ember
// particles drawn from their age alone, so loops stay seamless.
// The flame tongue, flame puff, smoke and ember recipes are
// adapted from Sprite Fusion's open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire),
// MIT License, Copyright (c) 2026 Hugo Duprez.
// The shaded, outlined icons are
// adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Pixelfx.Px;

namespace Pixelfx
{
    /// <summary>
    /// The character's shape, worked out once per mask, in mask space (0, 0 = its top-left): On(x, y), Body ([x, y] of every
    /// pixel), Up (pixels with nothing above them), Side ([x, y, dir] of pixels open to the left (-1) or right (1)), Outline
    /// ([x, y, nx, ny]: empty cells touching it, with the outward normal), Top (each column's first row, -1 if empty) and Rows
    /// ([left, right] of each row).
    /// </summary>
    internal sealed class StatusShape
    {
        public int W, H;
        public byte[] Data;
        public readonly List<int[]> Body = new List<int[]>(), Up = new List<int[]>(), Side = new List<int[]>();
        public readonly List<double[]> Outline = new List<double[]>();
        public readonly List<int> Top = new List<int>();
        public readonly List<int[]> Rows = new List<int[]>();

        public bool On(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Data[y * W + x] > 0;

        // JS indexes the mask with any number: only whole cells inside it are on
        public bool On(double x, double y) => x >= 0 && y >= 0 && x < W && y < H && x == Math.Floor(x) && y == Math.Floor(y) && Data[(int)y * W + (int)x] > 0;

        /// <summary>S.rows[my], or null where JS would read undefined.</summary>
        public int[] Row(double my) => my >= 0 && my < Rows.Count && my == Math.Floor(my) ? Rows[(int)my] : null;
    }

    /// <summary>The character's eyes: their box, their pixels and the skin round them (0 when none was found).</summary>
    internal sealed class StatusEye
    {
        public int X0, X1, Y0, Y1;
        public List<int[]> Px;
        public uint Skin;
    }

    internal static class StatusLib
    {
        static readonly ConditionalWeakTable<Mask, StatusShape> SHAPES = new ConditionalWeakTable<Mask, StatusShape>();

        public static StatusShape Shape(Actor a)
        {
            Mask m = a.Mask;
            if (SHAPES.TryGetValue(m, out StatusShape s)) return s;
            int w = m.W, h = m.H;
            s = new StatusShape { W = w, H = h, Data = m.Data };
            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h && !s.On(x, y)) y++;
                s.Top.Add(y < h ? y : -1);
            }
            for (int y = 0; y < h; y++)
            {
                int l = -1, r = -1;
                for (int x = 0; x < w; x++)
                {
                    if (!s.On(x, y)) continue;
                    if (l < 0) l = x;
                    r = x;
                    s.Body.Add(new[] { x, y });
                    if (!s.On(x, y - 1)) s.Up.Add(new[] { x, y });
                    if (!s.On(x - 1, y)) s.Side.Add(new[] { x, y, -1 });
                    if (!s.On(x + 1, y)) s.Side.Add(new[] { x, y, 1 });
                }
                s.Rows.Add(new[] { l, r });
            }
            for (int y = -1; y <= h; y++)
                for (int x = -1; x <= w; x++)
                {
                    if (s.On(x, y)) continue;
                    int nx = 0, ny = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (s.On(x + dx, y + dy))
                            {
                                nx -= dx;
                                ny -= dy;
                            }
                    if (s.On(x - 1, y) || s.On(x + 1, y) || s.On(x, y - 1) || s.On(x, y + 1))
                    {
                        double l = Hypot(nx, ny);
                        if (l == 0 || double.IsNaN(l)) l = 1;
                        s.Outline.Add(new[] { x, y, nx / l, ny / l });
                    }
                }
            SHAPES.Add(m, s);
            return s;
        }

        /// <summary>The character's own color at mask pixel (x, y), or a mid grey for a mask without pixels (0 outside it).</summary>
        public static uint ColorAt(Actor a, int x, int y)
        {
            int i = y * a.Mask.W + x;
            if (a.Pixels != null) return i >= 0 && i < a.Pixels.Length ? a.Pixels[i] : 0;
            return i >= 0 && i < a.Mask.Data.Length && a.Mask.Data[i] != 0 ? 0xff6b6b6b : 0;
        }

        /// <summary>1D value noise in [-1, 1] whose lattice wraps every P cells.</summary>
        public static double Wnoise(double x, double P, double seed = 0)
        {
            double i = Math.Floor(x), f = x - i, u = f * f * (3 - 2 * f);
            double a = Rand(Wrap(i, P), seed), b = Rand(Wrap(i + 1, P), seed);
            return (a + (b - a) * u) * 2 - 1;
        }

        /// <summary>Noise of time t that repeats every D seconds, about f wiggles a second; off slides along it (ripples).</summary>
        public static double Tnoise(double t, double D, double f, double seed = 0, double off = 0)
        {
            double P = Math.Max(2, Round(f * D));
            return Wnoise((t / D) * P + off, P, seed);
        }

        /// <summary>How far something has moved after age a, launched at speed v under acceleration g with linear drag d.</summary>
        public static double Travel(double v, double g, double d, double a) =>
            d > 0 ? (g / d) * a + ((v - g / d) * (1 - Exp(-d * a))) / d : v * a + 0.5 * g * a * a;

        /// <summary>A tongue's nested layers: [width, height, ramp step (0 brightest)]: a deep rim, orange, yellow, a white heart.</summary>
        public static readonly double[][] LAYERS = { new double[] { 1, 1, 6 }, new[] { 0.76, 0.86, 4 }, new[] { 0.54, 0.68, 2 }, new[] { 0.3, 0.42, 0 } };

        /// <summary>
        /// A flame tongue standing on (x, y): h tall, w half-wide at its root, its tip swept sx px sideways, filled in nested
        /// layers from F (bright to dark, 9 steps). Rows ripple with noise that loops every D seconds (seed n).
        /// </summary>
        public static void Tongue(Painter p, double x, double y, double h, double w, double sx, uint[] F, double t, double D, double n, double[][] layers = null)
        {
            layers ??= LAYERS;
            if (h < 1) return;
            foreach (double[] L in layers)
            {
                double lw = L[0], lh = L[1];
                int ri = (int)L[2];
                if (ri == 0 && h < 4) continue;
                double H = h * lh;
                uint col = F[ri];
                for (double r = 0; r <= H; r++)
                {
                    double u = r / Math.Max(1, H);
                    double half = w * lw * Px.Pow(1 - u, 0.85) * Math.Min(1, (r + 1.5) / 3.5) + Tnoise(t, D, 9, n, r * 0.45) * 0.5;
                    if (half < 0.3) continue;
                    double cx = x + sx * u * u * lh + Tnoise(t, D, 4, n + 7, r * 0.2) * u * 1.1;
                    for (double px = Round(cx - half); px <= Round(cx + half); px++) p.Px(px, Round(y - r), col);
                }
            }
        }

        /// <summary>A ball of flame at age u (0..1) of its life, radius size, cooling down F (bright to dark) as it rises.</summary>
        public static void Flame(Painter p, double x, double y, double size, double u, uint[] F, double heatSeed = 0)
        {
            int n = F.Length;
            double k = 1 - u;
            double heat = Math.Min(0.999, Px.Pow(u, 0.62) + heatSeed * 0.1);
            int idx = (int)Math.Min(n - 2, Math.Floor(heat * (n - 1)));
            double r = size * (0.55 + 0.45 * Math.Min(1, u * 7)) * (1 - u * 0.55);
            uint c = F[idx];
            if (idx >= n - 2)
            {
                p.Disc(x, y, r, c, Math.Min(1, k * 3) * 0.85);
                return;
            }
            p.Circle(x, y, r, F[Math.Min(n - 1, idx + 2)]);
            p.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, c);
            if (r > 1.6) p.Circle(x, y - r * 0.9, r * 0.42, c);
            if (idx <= 3 && r > 1.8) p.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, F[Math.Max(0, idx - 1)]);
            if (idx < 6) p.Glow(x, y, r * 2.4 + 2, c, 0.18 * k);
        }

        static uint RampAt(uint[] r, double t) => r[(int)Math.Min(r.Length - 1, Math.Max(0, Math.Floor(t * r.Length)))];

        /// <summary>
        /// A puff of smoke at age u (0..1): three soft discs (a body, a denser core sitting low, a lit cap while young), swelling
        /// and thinning, tinted col and walking down the smoke ramp. warm (a color) lights its underside.
        /// </summary>
        public static void Smoke(Painter p, double x, double y, double size, double u, uint col, double k = 1, uint warm = 0)
        {
            double r = size * (0.65 + Math.Min(1, u * 2.5) * 0.45 + u * 0.4);
            double a = Math.Min(1, u * 12) * Math.Min(1, (1 - u) * 2.4) * 0.58 * k;
            if (a <= 0.01) return;
            uint c = col != 0 ? Mix(col, RampAt(Sim.SMOKE_RAMP, u), u) : RampAt(Sim.SMOKE_RAMP, u * 1.05);
            p.Disc(x, y, r, c, a * 0.45);
            p.Disc(x + r * 0.05, y + r * 0.06, r * 0.74, c, a * 0.62);
            if (warm != 0 && u < 0.6) p.Disc(x + r * 0.1, y + r * 0.3, r * 0.56, Mix(c, warm, 0.5), a * 0.7 * (1 - u / 0.6));
            if (u < 0.5) p.Disc(x - r * 0.22, y - r * 0.3, r * 0.5, Mix(c, WHITE, 0.12), a * 0.4 * (1 - u * 2));
        }

        /// <summary>An ember at age u: hot and flickering, cooling to a dim red that blinks out. E is [hot, warm, cool].</summary>
        public static void Ember(Painter p, double x, double y, double u, uint[] E, double ph, double k = 1)
        {
            double left = 1 - u;
            if (left < 0.22 && Px.Sin(ph * 40) < 0) return;
            uint c = left > 0.5 ? (Px.Sin(ph * 22) > -0.2 ? E[0] : E[1]) : left > 0.22 ? E[1] : E[2];
            p.Px(x, y, c);
            p.Glow(x, y, 3, c, 0.32 * left * k);
        }

        /// <summary>A flat, translucent bank of haze centred on (x, y), rx by ry, thinning to nothing at its edge.</summary>
        public static void Haze(Painter p, double x, double y, double rx, double ry, uint col, double a)
        {
            if (a <= 0.01 || rx < 0.5 || ry < 0.5) return;
            for (double py = Math.Floor(y - ry); py <= Math.Ceiling(y + ry); py++)
                for (double px = Math.Floor(x - rx); px <= Math.Ceiling(x + rx); px++)
                {
                    double dx = (px + 0.5 - x) / rx, dy = (py + 0.5 - y) / ry, d = dx * dx + dy * dy;
                    if (d < 1) p.Blend(px, py, col, a * (1 - d));
                }
        }

        /// <summary>
        /// A shape from a bitmap (rows of strings, "." empty), centred on (0, 0), scaled up by whole k, mirrored if flip:
        /// returns inside(dx, dy) giving the bitmap's character there ('\0' for none).
        /// </summary>
        public static Func<double, double, char> Bitmap(string[] rows, double k = 1, bool flip = false)
        {
            int h = rows.Length, w = rows[0].Length;
            double ox = Math.Floor((w * k) / 2), oy = Math.Floor((h * k) / 2);
            return (dx, dy) =>
            {
                double bx = Math.Floor((dx + ox) / k);
                double by = Math.Floor((dy + oy) / k);
                if (bx < 0 || by < 0 || bx >= w || by >= h) return '\0';
                if (flip) bx = w - 1 - bx;
                char c = rows[(int)by][(int)bx];
                return c == '.' ? '\0' : c;
            };
        }

        /// <summary>
        /// A little shaded pixel icon at (x, y): every cell where inside(dx, dy) is set, within ext of the centre, filled from
        /// R = [outline, shadow, body, light] (lit on its top and left edges, shaded on the bottom and right, ringed by the
        /// outline) and recoloured where inside returns a character listed in paint. a fades it.
        /// </summary>
        public static void Icon(Painter p, double x, double y, int ext, Func<double, double, char> inside, uint[] R, double a = 1, IReadOnlyDictionary<char, uint> paint = null)
        {
            double X = Round(x), Y = Round(y);
            bool At(int dx, int dy) => inside(dx, dy) != '\0';
            for (int dy = -ext - 1; dy <= ext + 1; dy++)
                for (int dx = -ext - 1; dx <= ext + 1; dx++)
                {
                    char ch = inside(dx, dy);
                    uint c;
                    if (ch != '\0')
                    {
                        if (paint != null && paint.TryGetValue(ch, out uint pc)) c = pc;
                        else
                        {
                            bool top = !At(dx, dy - 1), left = !At(dx - 1, dy), bottom = !At(dx, dy + 1), right = !At(dx + 1, dy);
                            c = top || left ? R[3] : bottom || right ? R[1] : R[2];
                        }
                    }
                    else if (R[0] != 0 && (At(dx - 1, dy) || At(dx + 1, dy) || At(dx, dy - 1) || At(dx, dy + 1))) c = R[0];
                    else continue;
                    p.Dot(X + dx, Y + dy, c, a);
                }
        }

        /// <summary>inside(dx, dy) for a shape given in its own unit space by f(u, v), rotated by ang and scaled by s px.</summary>
        public static Func<double, double, char> Shaped(Func<double, double, bool> f, double s, double ang = 0)
        {
            double c = Px.Cos(ang), n = Px.Sin(ang);
            return (dx, dy) =>
            {
                double x = dx + 0.5, y = dy + 0.5;
                return f((x * c + y * n) / s, (-x * n + y * c) / s) ? '#' : '\0';
            };
        }

        /// <summary>A five-pointed star in unit space (points at radius 1, inner corners at r0).</summary>
        public static Func<double, double, bool> StarShape(double r0 = 0.45) => (u, v) =>
        {
            double a = Atan2(v, u) + Math.PI / 2, d = Hypot(u, v), seg = (2 * Math.PI) / 5;
            double k = 1 - Math.Abs((((a % seg) + seg) % seg) - seg / 2) / (seg / 2); // 0 at a point, 1 between two
            return d <= 1 - (1 - r0) * Px.Pow(k, 0.8);
        };

        /// <summary>A heart in unit space (two lobes and a point, about 2 wide and 2 tall, centred).</summary>
        public static bool HeartShape(double u, double v) =>
            Px.Pow(u + 0.5, 2) + Px.Pow(v + 0.32, 2) <= Px.Pow(0.55, 2) || Px.Pow(u - 0.5, 2) + Px.Pow(v + 0.32, 2) <= Px.Pow(0.55, 2) ||
            (v >= -0.32 && v <= 1 && Math.Abs(u) <= 1.04 * Px.Pow(1 - (v + 0.32) / 1.32, 0.75));

        static readonly ConditionalWeakTable<Mask, List<StatusEye>> EYES = new ConditionalWeakTable<Mask, List<StatusEye>>();

        /// <summary>
        /// The character's eyes, if it has any we can find: small blobs of very dark pixels (with any white beside them) inside
        /// the upper part of the body, surrounded by it, in mask space, left to right; Skin is the colour round them, to paint
        /// them shut with. Empty when nothing looks like eyes.
        /// </summary>
        public static List<StatusEye> Eyes(Actor a)
        {
            if (EYES.TryGetValue(a.Mask, out List<StatusEye> list)) return list;
            // a body that says where its eyes would go: a blank head still needs somewhere to go pink, burn or shut
            if (a.Eyes != null)
            {
                list = new List<StatusEye>();
                foreach (int[] xy in a.Eyes)
                {
                    int x = xy[0], y = xy[1];
                    list.Add(new StatusEye { X0 = x, X1 = x, Y0 = y, Y1 = y + 1, Px = new List<int[]> { new[] { x, y }, new[] { x, y + 1 } }, Skin = ColorAt(a, x, y) });
                }
                EYES.Add(a.Mask, list);
                return list;
            }
            StatusShape S = Shape(a);
            int W = S.W;
            uint Col(int x, int y) => S.On(x, y) ? ColorAt(a, x, y) : 0;
            bool Dark(int x, int y) => S.On(x, y) && y < S.H * 0.4 && Luma(Col(x, y)) < 0.15 && S.On(x - 1, y) && S.On(x + 1, y) && S.On(x, y - 1) && S.On(x, y + 1);
            var seen = new HashSet<int>();
            list = new List<StatusEye>();
            int[][] four = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
            int[][] six = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 }, new[] { 2, 0 }, new[] { -2, 0 } };
            foreach (int[] b in S.Body)
            {
                int x0 = b[0], y0 = b[1];
                if (seen.Contains(y0 * W + x0) || !Dark(x0, y0)) continue;
                var px = new List<int[]>();
                var q = new List<int[]> { new[] { x0, y0 } };
                seen.Add(y0 * W + x0);
                while (q.Count > 0)
                {
                    int[] cur = q[q.Count - 1];
                    q.RemoveAt(q.Count - 1);
                    int x = cur[0], y = cur[1];
                    px.Add(new[] { x, y });
                    foreach (int[] d in four)
                    {
                        int nx = x + d[0], ny = y + d[1], k = ny * W + nx;
                        if (seen.Contains(k) || !(Dark(nx, ny) || (S.On(nx, ny) && Luma(Col(nx, ny)) > 0.95))) continue;
                        seen.Add(k);
                        q.Add(new[] { nx, ny });
                    }
                }
                if (px.Count < 2 || px.Count > 14) continue;
                int ex0 = int.MaxValue, ex1 = int.MinValue, ey0 = int.MaxValue, ey1 = int.MinValue;
                foreach (int[] pp in px)
                {
                    ex0 = Math.Min(ex0, pp[0]);
                    ex1 = Math.Max(ex1, pp[0]);
                    ey0 = Math.Min(ey0, pp[1]);
                    ey1 = Math.Max(ey1, pp[1]);
                }
                var e = new StatusEye { X0 = ex0, X1 = ex1, Y0 = ey0, Y1 = ey1, Px = px };
                // a Map in insertion order: colour -> how often it borders the eye
                var colors = new List<uint>();
                var counts = new Dictionary<uint, int>();
                foreach (int[] pp in px)
                    foreach (int[] d in six)
                    {
                        int x = pp[0], y = pp[1];
                        uint c = Col(x + d[0], y + d[1]);
                        double l = Luma(c);
                        if (c != 0 && l > 0.5 && l < 0.95 && !px.Exists(o => o[0] == x + d[0] && o[1] == y + d[1]))
                        {
                            if (!counts.ContainsKey(c))
                            {
                                counts[c] = 0;
                                colors.Add(c);
                            }
                            counts[c]++;
                        }
                    }
                List<uint> sorted = StableSort(colors, (p, o) => counts[o] - counts[p]);
                e.Skin = sorted.Count > 0 ? sorted[0] : 0;
                if (e.Skin != 0 && e.X1 - e.X0 <= 4 && e.Y1 - e.Y0 <= 5) list.Add(e);
            }
            list = StableSort(list, (p, o) => p.X0 - o.X0);
            if (list.Count > 2) list = new List<StatusEye>();
            EYES.Add(a.Mask, list);
            return list;
        }

        /// <summary>A stable sort by a consistent comparator (JS Array.prototype.sort's result for one), as a new list.</summary>
        public static List<T> StableSort<T>(IEnumerable<T> items, Func<T, T, double> cmp)
        {
            var o = new List<T>(items);
            // binary insertion: stable, and any stable sort gives the same order for a consistent comparator
            for (int i = 1; i < o.Count; i++)
            {
                T pivot = o[i];
                int left = 0, right = i;
                while (left < right)
                {
                    int mid = left + ((right - left) >> 1);
                    if (cmp(pivot, o[mid]) < 0) right = mid;
                    else left = mid + 1;
                }
                for (int k = i; k > left; k--) o[k] = o[k - 1];
                o[left] = pivot;
            }
            return o;
        }

        // ---- V8's Array.prototype.sort (TimSort, third_party/v8/builtins/array-sort.tq), call for call, for comparators
        // that aren't consistent (a shuffle by `() => rng.next() - 0.5`): which pairs it compares, in which order, decides the result.

        sealed class TimSort<T>
        {
            const int MinGallopWins = 7;
            readonly T[] a;
            readonly Func<T, T, double> cmp;
            readonly List<int> runBase = new List<int>(), runLen = new List<int>();
            int minGallop = MinGallopWins;

            public TimSort(T[] a, Func<T, T, double> cmp)
            {
                this.a = a;
                this.cmp = cmp;
            }

            double Compare(T x, T y)
            {
                double r = cmp(x, y);
                return double.IsNaN(r) ? 0 : r;
            }

            static int MinRunLength(int n)
            {
                int r = 0;
                while (n >= 64)
                {
                    r |= n & 1;
                    n >>= 1;
                }
                return n + r;
            }

            public void Sort()
            {
                int length = a.Length;
                if (length < 2) return;
                int remaining = length, low = 0, minRun = MinRunLength(remaining);
                while (remaining != 0)
                {
                    int run = CountAndMakeRun(low, low + remaining);
                    if (run < minRun)
                    {
                        int forced = Math.Min(minRun, remaining);
                        BinaryInsertionSort(low, low + run, low + forced);
                        run = forced;
                    }
                    runBase.Add(low);
                    runLen.Add(run);
                    MergeCollapse();
                    low += run;
                    remaining -= run;
                }
                MergeForceCollapse();
            }

            void BinaryInsertionSort(int low, int startArg, int high)
            {
                int start = low == startArg ? startArg + 1 : startArg;
                for (; start < high; ++start)
                {
                    int left = low, right = start;
                    T pivot = a[start];
                    while (left < right)
                    {
                        int mid = left + ((right - left) >> 1);
                        double order = Compare(pivot, a[mid]);
                        if (order < 0) right = mid;
                        else left = mid + 1;
                    }
                    for (int p = start; p > left; --p) a[p] = a[p - 1];
                    a[left] = pivot;
                }
            }

            int CountAndMakeRun(int lowArg, int high)
            {
                int low = lowArg + 1;
                if (low == high) return 1;
                int runLength = 2;
                T elementLow = a[low], elementLowPre = a[low - 1];
                double order = Compare(elementLow, elementLowPre);
                bool isDescending = order < 0;
                T previous = elementLow;
                for (int idx = low + 1; idx < high; ++idx)
                {
                    T current = a[idx];
                    order = Compare(current, previous);
                    if (isDescending)
                    {
                        if (order >= 0) break;
                    }
                    else if (order < 0) break;
                    previous = current;
                    ++runLength;
                }
                if (isDescending) Array.Reverse(a, lowArg, runLength);
                return runLength;
            }

            bool RunInvariantEstablished(int n)
            {
                if (n < 2) return true;
                return runLen[n - 2] > runLen[n - 1] + runLen[n];
            }

            void MergeCollapse()
            {
                while (runLen.Count > 1)
                {
                    int n = runLen.Count - 2;
                    if (!RunInvariantEstablished(n + 1) || !RunInvariantEstablished(n))
                    {
                        if (runLen[n - 1] < runLen[n + 1]) --n;
                        MergeAt(n);
                    }
                    else if (runLen[n] <= runLen[n + 1]) MergeAt(n);
                    else break;
                }
            }

            void MergeForceCollapse()
            {
                while (runLen.Count > 1)
                {
                    int n = runLen.Count - 2;
                    if (n > 0 && runLen[n - 1] < runLen[n + 1]) --n;
                    MergeAt(n);
                }
            }

            void MergeAt(int i)
            {
                int stackSize = runLen.Count;
                int baseA = runBase[i], lengthA = runLen[i], baseB = runBase[i + 1], lengthB = runLen[i + 1];
                runLen[i] = lengthA + lengthB;
                if (i == stackSize - 3)
                {
                    runBase[i + 1] = runBase[i + 2];
                    runLen[i + 1] = runLen[i + 2];
                }
                runBase.RemoveAt(stackSize - 1);
                runLen.RemoveAt(stackSize - 1);
                int k = GallopRight(a, a[baseB], baseA, lengthA, 0);
                baseA += k;
                lengthA -= k;
                if (lengthA == 0) return;
                lengthB = GallopLeft(a, a[baseA + lengthA - 1], baseB, lengthB, lengthB - 1);
                if (lengthB == 0) return;
                if (lengthA <= lengthB) MergeLow(baseA, lengthA, baseB, lengthB);
                else MergeHigh(baseA, lengthA, baseB, lengthB);
            }

            int GallopLeft(T[] array, T key, int bas, int length, int hint)
            {
                int lastOfs = 0, offset = 1;
                double order = Compare(array[bas + hint], key);
                if (order < 0)
                {
                    int maxOfs = length - hint;
                    while (offset < maxOfs)
                    {
                        order = Compare(array[bas + hint + offset], key);
                        if (order >= 0) break;
                        lastOfs = offset;
                        offset = (offset << 1) + 1;
                        if (offset <= 0) offset = maxOfs;
                    }
                    if (offset > maxOfs) offset = maxOfs;
                    lastOfs += hint;
                    offset += hint;
                }
                else
                {
                    int maxOfs = hint + 1;
                    while (offset < maxOfs)
                    {
                        order = Compare(array[bas + hint - offset], key);
                        if (order < 0) break;
                        lastOfs = offset;
                        offset = (offset << 1) + 1;
                        if (offset <= 0) offset = maxOfs;
                    }
                    if (offset > maxOfs) offset = maxOfs;
                    int tmp = lastOfs;
                    lastOfs = hint - offset;
                    offset = hint - tmp;
                }
                lastOfs++;
                while (lastOfs < offset)
                {
                    int m = lastOfs + ((offset - lastOfs) >> 1);
                    order = Compare(array[bas + m], key);
                    if (order < 0) lastOfs = m + 1;
                    else offset = m;
                }
                return offset;
            }

            int GallopRight(T[] array, T key, int bas, int length, int hint)
            {
                int lastOfs = 0, offset = 1;
                double order = Compare(key, array[bas + hint]);
                if (order < 0)
                {
                    int maxOfs = hint + 1;
                    while (offset < maxOfs)
                    {
                        order = Compare(key, array[bas + hint - offset]);
                        if (order >= 0) break;
                        lastOfs = offset;
                        offset = (offset << 1) + 1;
                        if (offset <= 0) offset = maxOfs;
                    }
                    if (offset > maxOfs) offset = maxOfs;
                    int tmp = lastOfs;
                    lastOfs = hint - offset;
                    offset = hint - tmp;
                }
                else
                {
                    int maxOfs = length - hint;
                    while (offset < maxOfs)
                    {
                        order = Compare(key, array[bas + hint + offset]);
                        if (order < 0) break;
                        lastOfs = offset;
                        offset = (offset << 1) + 1;
                        if (offset <= 0) offset = maxOfs;
                    }
                    if (offset > maxOfs) offset = maxOfs;
                    lastOfs += hint;
                    offset += hint;
                }
                lastOfs++;
                while (lastOfs < offset)
                {
                    int m = lastOfs + ((offset - lastOfs) >> 1);
                    order = Compare(key, array[bas + m]);
                    if (order < 0) offset = m;
                    else lastOfs = m + 1;
                }
                return offset;
            }

            void MergeLow(int baseA, int lengthA, int baseB, int lengthB)
            {
                var temp = new T[lengthA];
                Array.Copy(a, baseA, temp, 0, lengthA);
                int dest = baseA, cursorTemp = 0, cursorB = baseB;
                a[dest++] = a[cursorB++];
                if (--lengthB == 0) goto Succeed;
                if (lengthA == 1) goto CopyB;
                int mg = minGallop;
                for (;;)
                {
                    int winsA = 0, winsB = 0;
                    for (;;)
                    {
                        double order = Compare(a[cursorB], temp[cursorTemp]);
                        if (order < 0)
                        {
                            a[dest++] = a[cursorB++];
                            ++winsB;
                            --lengthB;
                            winsA = 0;
                            if (lengthB == 0) goto Succeed;
                            if (winsB >= mg) break;
                        }
                        else
                        {
                            a[dest++] = temp[cursorTemp++];
                            ++winsA;
                            --lengthA;
                            winsB = 0;
                            if (lengthA == 1) goto CopyB;
                            if (winsA >= mg) break;
                        }
                    }
                    ++mg;
                    bool first = true;
                    while (winsA >= MinGallopWins || winsB >= MinGallopWins || first)
                    {
                        first = false;
                        mg = Math.Max(1, mg - 1);
                        minGallop = mg;
                        winsA = GallopRight(temp, a[cursorB], cursorTemp, lengthA, 0);
                        if (winsA > 0)
                        {
                            Array.Copy(temp, cursorTemp, a, dest, winsA);
                            dest += winsA;
                            cursorTemp += winsA;
                            lengthA -= winsA;
                            if (lengthA == 1) goto CopyB;
                            if (lengthA == 0) goto Succeed;
                        }
                        a[dest++] = a[cursorB++];
                        if (--lengthB == 0) goto Succeed;
                        winsB = GallopLeft(a, temp[cursorTemp], cursorB, lengthB, 0);
                        if (winsB > 0)
                        {
                            Array.Copy(a, cursorB, a, dest, winsB);
                            dest += winsB;
                            cursorB += winsB;
                            lengthB -= winsB;
                            if (lengthB == 0) goto Succeed;
                        }
                        a[dest++] = temp[cursorTemp++];
                        if (--lengthA == 1) goto CopyB;
                    }
                    ++mg;
                    minGallop = mg;
                }
                Succeed:
                if (lengthA > 0) Array.Copy(temp, cursorTemp, a, dest, lengthA);
                return;
                CopyB:
                Array.Copy(a, cursorB, a, dest, lengthB);
                a[dest + lengthB] = temp[cursorTemp];
            }

            void MergeHigh(int baseA, int lengthA, int baseB, int lengthB)
            {
                var temp = new T[lengthB];
                Array.Copy(a, baseB, temp, 0, lengthB);
                int dest = baseB + lengthB - 1, cursorTemp = lengthB - 1, cursorA = baseA + lengthA - 1;
                a[dest--] = a[cursorA--];
                if (--lengthA == 0) goto Succeed;
                if (lengthB == 1) goto CopyA;
                int mg = minGallop;
                for (;;)
                {
                    int winsA = 0, winsB = 0;
                    for (;;)
                    {
                        double order = Compare(temp[cursorTemp], a[cursorA]);
                        if (order < 0)
                        {
                            a[dest--] = a[cursorA--];
                            ++winsA;
                            --lengthA;
                            winsB = 0;
                            if (lengthA == 0) goto Succeed;
                            if (winsA >= mg) break;
                        }
                        else
                        {
                            a[dest--] = temp[cursorTemp--];
                            ++winsB;
                            --lengthB;
                            winsA = 0;
                            if (lengthB == 1) goto CopyA;
                            if (winsB >= mg) break;
                        }
                    }
                    ++mg;
                    bool first = true;
                    while (winsA >= MinGallopWins || winsB >= MinGallopWins || first)
                    {
                        first = false;
                        mg = Math.Max(1, mg - 1);
                        minGallop = mg;
                        int k = GallopRight(a, temp[cursorTemp], baseA, lengthA, lengthA - 1);
                        winsA = lengthA - k;
                        if (winsA > 0)
                        {
                            dest -= winsA;
                            cursorA -= winsA;
                            Array.Copy(a, cursorA + 1, a, dest + 1, winsA);
                            lengthA -= winsA;
                            if (lengthA == 0) goto Succeed;
                        }
                        a[dest--] = temp[cursorTemp--];
                        if (--lengthB == 1) goto CopyA;
                        k = GallopLeft(temp, a[cursorA], 0, lengthB, lengthB - 1);
                        winsB = lengthB - k;
                        if (winsB > 0)
                        {
                            dest -= winsB;
                            cursorTemp -= winsB;
                            Array.Copy(temp, cursorTemp + 1, a, dest + 1, winsB);
                            lengthB -= winsB;
                            if (lengthB == 1) goto CopyA;
                            if (lengthB == 0) goto Succeed;
                        }
                        a[dest--] = a[cursorA--];
                        if (--lengthA == 0) goto Succeed;
                    }
                    ++mg;
                    minGallop = mg;
                }
                Succeed:
                if (lengthB > 0) Array.Copy(temp, 0, a, dest - (lengthB - 1), lengthB);
                return;
                CopyA:
                dest -= lengthA;
                cursorA -= lengthA;
                Array.Copy(a, cursorA + 1, a, dest + 1, lengthA);
                a[dest] = temp[cursorTemp];
            }
        }

        /// <summary>JS array.sort(cmp) exactly as V8 runs it, comparator calls and all, sorting a in place.</summary>
        public static void JsSort<T>(T[] a, Func<T, T, double> cmp) => new TimSort<T>(a, cmp).Sort();
    }
}
