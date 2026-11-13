using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed partial class Painter
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Frame, Painter> painters = new System.Runtime.CompilerServices.ConditionalWeakTable<Frame, Painter>();

        /// <summary>The painter for a frame (one per frame, reused), reset: the JS painterFor.</summary>
        public static Painter For(Frame f)
        {
            Painter p = painters.GetValue(f, k => new Painter(k));
            p.Layer = Layer.Front;
            return p.Reset(f);
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Painter, Painter> clips = new System.Runtime.CompilerServices.ConditionalWeakTable<Painter, Painter>();

        /// <summary>
        /// This painter, clipped to the local rectangle 0..w, 0..h: every pixel whose centre falls outside it is dropped, so sparks,
        /// smoke and light that fly past an edge are cut there. One per painter, kept in step with it: the Clipped painter and
        /// clipTo of pixelfx's src/fx/transitions/_lib.js.
        /// </summary>
        public Painter ClipTo(double w, double h)
        {
            Painter q = clips.GetValue(this, k => new Painter(k.frame));
            q.Reset(frame);
            q.clipW = w;
            q.clipH = h;
            q.tx = tx;
            q.ty = ty;
            q.fx = fx;
            q.cs = cs;
            q.sn = sn;
            q.Fade = Fade;
            q.LightK = LightK;
            q.Layer = Layer;
            return q;
        }

        /// <summary>Which layer is being drawn right now (Fx sets it before DrawBack and Draw).</summary>
        public Layer Layer = Layer.Front;

        /// <summary>Where the painter is placed, for code that draws other effects into the same frame and must put it back.</summary>
        public readonly struct State
        {
            internal readonly double Tx, Ty, Fx, Cs, Sn, PlacedX, PlacedY, Fade, LightK;

            internal State(double tx, double ty, double fx, double cs, double sn, double placedX, double placedY, double fade, double lightK)
            {
                Tx = tx;
                Ty = ty;
                Fx = fx;
                Cs = cs;
                Sn = sn;
                PlacedX = placedX;
                PlacedY = placedY;
                Fade = fade;
                LightK = lightK;
            }
        }

        public State Save() => new State(tx, ty, fx, cs, sn, placedX, placedY, Fade, LightK);

        public void Restore(State s)
        {
            tx = s.Tx;
            ty = s.Ty;
            fx = s.Fx;
            cs = s.Cs;
            sn = s.Sn;
            placedX = s.PlacedX;
            placedY = s.PlacedY;
            Fade = s.Fade;
            LightK = s.LightK;
        }

        /// <summary>Where the origin is placed in frame space, and whether it is mirrored (-1) or not (1).</summary>
        public double OriginX => tx;
        public double OriginY => ty;
        public double Mirror => fx;

        /// <summary>
        /// A little sprite from strings: rows like "..##..", pal maps characters to colors; "." and " " are empty. Top-left at (x, y).
        /// tint draws every pixel in one color; flip mirrors left-right, flipY turns it upside down.
        /// </summary>
        public void Sprite(string[] rows, IReadOnlyDictionary<char, uint> pal, double x, double y, double scale = 1, double a = 1, bool flip = false, bool flipY = false, uint? tint = null)
        {
            int W = rows.Length > 0 ? rows[0].Length : 0;
            for (int r = 0; r < rows.Length; r++)
                for (int q = 0; q < rows[r].Length; q++)
                {
                    char ch = rows[r][q];
                    if (ch == '.' || ch == ' ') continue;
                    uint c = tint ?? (pal.TryGetValue(ch, out uint v) ? v : 0);
                    if (c == 0) continue;
                    int col = flip ? W - 1 - q : q, row = flipY ? rows.Length - 1 - r : r;
                    if (scale == 1) Dot(x + col, y + row, c, a);
                    else Rect(x + col * scale, y + row * scale, scale, scale, c, a);
                }
        }

        public void Sprite(string[] rows, IReadOnlyDictionary<char, string> pal, double x, double y, double scale = 1, double a = 1, bool flip = false, bool flipY = false, uint? tint = null) =>
            Sprite(rows, Palette(pal), x, y, scale, a, flip, flipY, tint);

        static readonly Dictionary<IReadOnlyDictionary<char, string>, Dictionary<char, uint>> palettes = new Dictionary<IReadOnlyDictionary<char, string>, Dictionary<char, uint>>();

        static Dictionary<char, uint> Palette(IReadOnlyDictionary<char, string> pal)
        {
            lock (palettes)
            {
                if (palettes.TryGetValue(pal, out var done)) return done;
                var o = new Dictionary<char, uint>();
                foreach (var kv in pal) o[kv.Key] = kv.Value == null ? 0 : Hex(kv.Value);
                palettes[pal] = o;
                return o;
            }
        }

        /// <summary>
        /// Pixel text. font: "mini" (3x5) or "big" (5x7); align: "left", "center" or "right"; (x, y) is the top of the text at that side.
        /// outline and shadow draw a ring or a drop under the letters in their color.
        /// </summary>
        public void Text(string str, double x, double y, uint c, string font = "mini", double scale = 1, string align = "left", uint? outline = null, uint? shadow = null, double a = 1) =>
            Text(str, x, y, (gx, gy) => c, font, scale, align, outline, shadow, a);

        public void Text(string str, double x, double y, Func<int, int, uint> c, string font = "mini", double scale = 1, string align = "left", uint? outline = null, uint? shadow = null, double a = 1)
        {
            double k = scale, w = Font.TextWidth(str, font) * k;
            double left = Round(align == "center" ? x - w / 2 : align == "right" ? x - w : x), top = Round(y);
            if (outline is uint ol) Font.RasterText(str, font, (gx, gy) => Rect(left + gx * k - 1, top + gy * k - 1, k + 2, k + 2, ol, a));
            if (shadow is uint sh) Font.RasterText(str, font, (gx, gy) => Rect(left + gx * k + 1, top + gy * k + 1, k, k, sh, a));
            Font.RasterText(str, font, (gx, gy) => Rect(left + gx * k, top + gy * k, k, k, c(gx, gy), a));
        }

        /// <summary>Size of text in pixels.</summary>
        public (double w, double h) Measure(string str, string font = "mini", double scale = 1) => (Font.TextWidth(str, font) * scale, Font.Fonts[font].H * scale);
    }
}
