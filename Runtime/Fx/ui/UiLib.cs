// Helpers for the ui effects. Big pixel words with a hand-made finish, a character lit or flashed by the effect,
// HUD plates and a whole-pixel shake.
// TintActor adapts the body flash of Sprite Fusion's open-source power-up
// (https://www.spritefusion.com/pixel-art-vfx/power-up), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class UiLib
    {
        /// <summary>One letter laid out in a word, in font pixels: g is null for a space.</summary>
        internal sealed class Letter
        {
            public char Ch;
            public Font.Glyph G;
            public int X, W, H;
        }

        internal sealed class Word
        {
            public Letter[] Letters;
            public int W, H;
        }

        /// <summary>The letters of str in a pixel font, laid out left to right in font pixels (the JS spell).</summary>
        internal static Word Spell(string str, string font = "big")
        {
            Font F = Font.Fonts[font];
            var letters = new List<Letter>();
            int x = 0;
            foreach (char ch in str)
            {
                Font.Glyph g = F.Glyphs.TryGetValue(ch, out Font.Glyph a) ? a : F.Glyphs.TryGetValue(char.ToUpperInvariant(ch), out Font.Glyph b) ? b : null;
                int w = g != null ? g.W : F.Space;
                letters.Add(new Letter { Ch = ch, G = g, X = x, W = w, H = F.H });
                x += w + F.Gap;
            }
            return new Word { Letters = letters.ToArray(), W = Math.Max(0, x - F.Gap), H = F.H };
        }

        /// <summary>One glyph of a word as drawWord takes it.</summary>
        internal sealed class Item
        {
            public Font.Glyph G;
            public double X, Y, Sx, Sy, A = 1, Flash, Slant;

            public Item With() => (Item)MemberwiseClone();
        }

        /// <summary>A word's finish: fill is bottom..top colors; null fields fall back as the JS does.</summary>
        internal sealed class Style
        {
            public uint[] Fill;
            public uint? Edge, Under, Glint;
            public uint Outline, Shadow;
            public int Ow = 1;
            public double? Shine, ShineY;
        }

        // scratch maps of which letter covers each pixel and where it sits in its font pixel, reused between frames
        [ThreadStatic] static short[] ids;
        [ThreadStatic] static byte[] edges;

        static bool Ink(bool[] r, int i) => i >= 0 && i < r.Length && r[i];

        /// <summary>
        /// Draw letters as one word with a crafted finish at the 1 px level: a gradient down each letter, a lit top
        /// edge, a shaded underside, a 1 px dark outline and a drop shadow, an optional diagonal shine and a white flash.
        /// </summary>
        internal static void DrawWord(Painter p, IList<Item> items, Style st)
        {
            if (items.Count == 0) return;
            if (ids == null)
            {
                ids = new short[4096];
                edges = new byte[4096];
            }
            double bx0 = double.PositiveInfinity, by0 = double.PositiveInfinity, bx1 = double.NegativeInfinity, by1 = double.NegativeInfinity;
            foreach (Item it in items)
            {
                double h = it.G.Rows.Length * it.Sy, w = it.G.W * it.Sx, sl = Math.Abs(it.Slant) * it.G.Rows.Length;
                bx0 = Math.Min(bx0, it.X - sl);
                bx1 = Math.Max(bx1, it.X + w + sl);
                by0 = Math.Min(by0, it.Y);
                by1 = Math.Max(by1, it.Y + h);
            }
            int ow = st.Ow;
            int x0 = (int)Math.Floor(bx0) - 1 - ow, y0 = (int)Math.Floor(by0) - 1 - ow, x1 = (int)Math.Ceiling(bx1) + 2 + ow, y1 = (int)Math.Ceiling(by1) + 3 + ow;
            int W = x1 - x0, H = y1 - y0;
            if (W * H > ids.Length)
            {
                ids = new short[W * H * 2];
                edges = new byte[W * H * 2];
            }
            short[] Ids = ids;
            byte[] Edges = edges;
            Array.Clear(Ids, 0, W * H);
            for (int k = 0; k < items.Count; k++)
            {
                Item it = items[k];
                Font.Glyph g = it.G;
                int gh = g.Rows.Length;
                double h = gh * it.Sy, bot = it.Y + h, sl = it.Slant;
                for (double py = Math.Floor(it.Y); py < Math.Ceiling(bot); py++)
                {
                    double gyd = Math.Floor((py + 0.5 - it.Y) / it.Sy);
                    if (gyd < 0 || gyd >= gh) continue;
                    int gy = (int)gyd;
                    double shift = Round(((bot - py - 0.5) / it.Sy) * sl);
                    double ry = py + 0.5 - it.Y - gy * it.Sy;
                    bool[] row = g.Rows[gy];
                    for (double px = Math.Floor(it.X + shift) - 1; px <= Math.Ceiling(it.X + shift + g.W * it.Sx); px++)
                    {
                        double fx = px + 0.5 - it.X - shift, gxd = Math.Floor(fx / it.Sx);
                        if (gxd < 0 || gxd >= g.W) continue;
                        int gx = (int)gxd;
                        if (!row[gx]) continue;
                        double id = (py - y0) * W + (px - x0);
                        if (id < 0 || id >= W * H) continue;
                        int i = (int)id;
                        Ids[i] = (short)(k + 1);
                        // the bevel follows the font pixels, so italic steps don't break it: lit top, shaded underside, lit left.
                        // Only flat edges get it: a diagonal stroke (a font pixel touching one corner to corner) stays whole.
                        double rx = fx - gx * it.Sx;
                        bool[] up = gy > 0 ? g.Rows[gy - 1] : null, dn = gy < gh - 1 ? g.Rows[gy + 1] : null;
                        bool Open(bool[] r) => r == null || (!Ink(r, gx) && !Ink(r, gx - 1) && !Ink(r, gx + 1));
                        Edges[i] = (byte)((ry < 1 && Open(up) ? 1 : 0) | (ry >= it.Sy - 1 && Open(dn) ? 2 : 0) | (rx < 1 && !(gx > 0 && row[gx - 1]) ? 4 : 0));
                    }
                }
            }
            int At(int x, int y) => x < x0 || y < y0 || x >= x1 || y >= y1 ? 0 : Ids[(y - y0) * W + (x - x0)];
            uint[] F = st.Fill;
            int nF = F.Length;
            uint glint = st.Glint ?? WHITE;
            double sy0 = st.ShineY ?? y0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int id = At(x, y);
                    if (id != 0)
                    {
                        Item it = items[id - 1];
                        double v = (y + 0.5 - it.Y) / (it.G.Rows.Length * it.Sy);
                        int i = (int)Math.Min(nF - 1, Math.Max(0, Math.Floor((1 - v) * nF)));
                        uint c = F[i];
                        int e = Edges[(y - y0) * W + (x - x0)];
                        if ((e & 1) != 0) c = st.Edge ?? F[nF - 1];
                        else if ((e & 2) != 0) c = st.Under ?? F[0];
                        else if ((e & 4) != 0 && i < nF - 1) c = F[i + 1];
                        if (st.Shine is double shine)
                        {
                            double d = Math.Abs(x + 0.5 - (y + 0.5 - sy0) * 0.6 - shine);
                            if (d < 1.1) c = WHITE;
                            else if (d < 2.4) c = glint;
                        }
                        // a flash is white all over, then wipes up off the letter (no dither)
                        if (it.Flash > 0.6 || (it.Flash > 0.25 && v < 0.5)) c = WHITE;
                        p.Dot(x, y, c, it.A);
                        continue;
                    }
                    // the outline hugs every letter; the shadow drops below and right of it
                    int n = 0;
                    for (int dy = -ow; dy <= ow && n == 0; dy++)
                        for (int dx = -ow; dx <= ow && n == 0; dx++)
                            if (ow < 2 || Math.Abs(dx) + Math.Abs(dy) < 4) n = At(x + dx, y + dy);
                    if (n != 0)
                    {
                        p.Dot(x, y, st.Outline, items[n - 1].A);
                        continue;
                    }
                    int s = At(x - ow, y - 1 - ow);
                    if (s == 0) s = At(x + 1 - ow, y - 1 - ow);
                    if (s == 0) s = At(x - ow, y - ow);
                    if (s != 0 && st.Shadow != 0) p.Dot(x, y, st.Shadow, items[s - 1].A * 0.9);
                }
        }

        /// <summary>
        /// Flash a character in light: its own pixels recolored by brightness into a bright ramp ([shadow, middle,
        /// light] colors). k (0..1) steps down without dither: all of it, then its lighter parts, then only highlights;
        /// above 0.8 the lightest go white.
        /// </summary>
        internal static void TintActor(Painter p, Actor A, double k, uint[] ramp)
        {
            if (A == null || k <= 0.12) return;
            Mask m = A.Mask;
            uint[] pix = A.Pixels;
            double cut = k > 0.6 ? 0 : k > 0.3 ? 0.22 : 0.5;
            for (int j = 0; j < m.H; j++)
                for (int i = 0; i < m.W; i++)
                {
                    int q = j * m.W + i;
                    if (m.Data[q] == 0) continue;
                    double l = pix != null ? Luma(pix[q]) : 0.6;
                    if (l < cut) continue;
                    p.Px(A.X + i, A.Y + j, l > 0.5 ? (k > 0.8 ? WHITE : ramp[2]) : l > 0.22 ? ramp[1] : ramp[0]);
                }
        }

        /// <summary>
        /// Light a character: warm additive light over its body (k), stronger towards the top, and light catching the
        /// edge just inside its dark outline (rim 0..1), mixed into its own colors so it stays solid and readable.
        /// </summary>
        internal static void LightActor(Painter p, Actor A, uint c, double k, double rim = 0, double t = 0)
        {
            if (A == null || (k <= 0.01 && rim <= 0.01)) return;
            Mask m = A.Mask;
            uint[] pix = A.Pixels;
            bool Has(int i, int j) => i >= 0 && j >= 0 && i < m.W && j < m.H && m.Data[j * m.W + i] != 0;
            for (int j = 0; j < m.H; j++)
                for (int i = 0; i < m.W; i++)
                {
                    if (m.Data[j * m.W + i] == 0) continue;
                    if (k > 0.01) p.Light(A.X + i, A.Y + j, c, k * (1.1 - (0.5 * j) / m.H));
                    if (rim <= 0.01 || !Has(i - 1, j) || !Has(i + 1, j) || !Has(i, j - 1)) continue;
                    if (Has(i - 2, j) && Has(i + 2, j) && Has(i, j - 2)) continue;
                    double f = rim * (0.75 + 0.35 * Noise(j * 0.6 - t * 8, i * 3));
                    if (f > 0.25)
                    {
                        uint own = pix != null ? pix[j * m.W + i] : 0;
                        p.Px(A.X + i, A.Y + j, Mix(own != 0 ? own : c, c, Math.Min(0.85, f)));
                    }
                }
        }

        /// <summary>A glowing 1 px line just outside a character, flickering: an aura's edge.</summary>
        internal static void HaloActor(Painter p, Actor A, uint c, double k, double t = 0)
        {
            if (A == null || k <= 0.02) return;
            p.Outline(A.Mask, A.X, A.Y, c, (mx, my) => k * (0.75 + 0.45 * Noise(my * 0.45 - t * 7, mx * 3)));
        }

        /// <summary>A UI plate's colors.</summary>
        internal sealed class Plate
        {
            public uint Outline, Rim, Top, Body, Low;

            public Plate With() => (Plate)MemberwiseClone();
        }

        /// <summary>
        /// A UI plate: a rounded rectangle with a dark outline, a lit top rim, a body that darkens towards the bottom
        /// and a shaded bottom edge. r: corner; a: alpha; shine: diagonal x, or null.
        /// </summary>
        internal static void Panel(Painter p, double x, double y, double w, double h, Plate col, double r = 3, double a = 1, double? shine = null)
        {
            if (w < 2 || h < 2) return;
            double X1 = x + w, Y1 = y + h;
            p.Shade(x, y, X1, Y1, (px, py, o) =>
            {
                double cx = Math.Max(Math.Max(x + r - px, 0), px - (X1 - r)), cy = Math.Max(Math.Max(y + r - py, 0), py - (Y1 - r));
                double e = cx > 0 && cy > 0 ? r - Hypot(cx, cy) : Math.Min(Math.Min(Math.Min(px - x, X1 - px), py - y), Y1 - py);
                if (e < 0) return 0;
                o.a = a;
                if (e < 1) return col.Outline;
                if (py - y < 2) return col.Rim;
                if (Y1 - py < 2) return col.Low;
                if (shine is double s)
                {
                    double d = Math.Abs(px - (py - y) * 0.6 - s);
                    if (d < 1.3) return WHITE;
                    if (d < 3) return col.Rim;
                }
                return py - y < h * 0.45 ? col.Top : col.Body;
            });
        }

        /// <summary>
        /// A HUD heart, 13 x 12: k outline, w highlight, h light, o body, m shade, d deep shade. HeartPal(C) maps a
        /// ramp onto it (C[0] outline .. C[4] body, white highlight); drawn with p.Sprite.
        /// </summary>
        internal static readonly string[] HEART =
        {
            "..kkk...kkk..",
            ".khhok.khook.",
            "khwhookoooomk",
            "khwoooooooomk",
            "khoooooooommk",
            "kooooooooomdk",
            ".kooooooomdk.",
            "..kooooomdk..",
            "...kooomdk...",
            "....komdk....",
            ".....kdk.....",
            "......k......",
        };

        internal static Dictionary<char, uint> HeartPal(uint[] C) => new Dictionary<char, uint> { ['k'] = C[0], ['w'] = WHITE, ['h'] = C[5], ['o'] = C[4], ['m'] = C[3], ['d'] = C[2] };

        /// <summary>A whole-pixel shake that dies away: (dx, dy) at time t, amp px, from t0 over len seconds.</summary>
        internal static (double x, double y) Shake(double t, double amp, double t0 = 0, double len = 0.3, double seed = 1, double hz = 32)
        {
            if (t < t0 || t > t0 + len) return (0, 0);
            double k = 1 - (t - t0) / len;
            return (Round(Noise(t * hz, seed) * amp * k * 1.7), Round(Noise(t * hz, seed + 7) * amp * k * 1.3));
        }
    }
}
