using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Pixelfx
{
    /// <summary>
    /// The C# half of the parity check, engine-free so a console program can run it too: every frame of an effect, every frame of
    /// an act, and an act's timing written out the way Tools/parity.mjs writes the JS original's.
    /// </summary>
    public static class ParityRun
    {
        public static string Hash(SHA256 sha, byte[] rgba) => string.Concat(sha.ComputeHash(rgba).Take(8).Select(b => b.ToString("x2")));

        /// <summary>Where a module's JS file sits: "fx/impacts/hit-spark" -> "impacts".</summary>
        public static string Category(Module m)
        {
            string[] parts = m.Source.Split('/');
            return parts.Length >= 2 ? parts[parts.Length - 2] : "";
        }

        // The fewest significant digits that read back as v, like JS. Mono's "R" writes 17 digits where 16 are enough.
        static string Shortest(double v)
        {
            for (int digits = 15; ; digits++)
            {
                string s = v.ToString("G" + digits, CultureInfo.InvariantCulture);
                if (digits == 17 || double.Parse(s, CultureInfo.InvariantCulture) == v) return s;
            }
        }

        /// <summary>A number as JS's String(n) writes it.</summary>
        public static string Js(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsInfinity(v)) return v > 0 ? "Infinity" : "-Infinity";
            if (v == 0) return "0";
            string r = Shortest(v);
            int e = r.IndexOfAny(new[] { 'E', 'e' });
            string mant = e < 0 ? r : r.Substring(0, e);
            int exp = e < 0 ? 0 : int.Parse(r.Substring(e + 1), CultureInfo.InvariantCulture);
            bool neg = mant.StartsWith("-");
            if (neg) mant = mant.Substring(1);
            int dot = mant.IndexOf('.');
            string digits = (dot < 0 ? mant : mant.Remove(dot, 1)).TrimStart('0');
            int lead = mant.Length - mant.TrimStart('0', '.').Length;
            // decimal exponent n: the value is 0.digits x 10^n
            int intLen = dot < 0 ? mant.Length : dot;
            int n = intLen + exp;
            if (mant.TrimStart('0').StartsWith(".") || mant.StartsWith("0"))
            {
                string frac = dot < 0 ? "" : mant.Substring(dot + 1);
                int zeros = frac.Length - frac.TrimStart('0').Length;
                n = -zeros + exp;
            }
            digits = digits.TrimEnd('0');
            if (digits.Length == 0) return "0";
            string s;
            int k = digits.Length;
            if (k <= n && n <= 21) s = digits + new string('0', n - k);
            else if (0 < n && n <= 21) s = digits.Substring(0, n) + "." + digits.Substring(n);
            else if (-6 < n && n <= 0) s = "0." + new string('0', -n) + digits;
            else
            {
                int e1 = n - 1;
                s = digits.Substring(0, 1) + (k > 1 ? "." + digits.Substring(1) : "") + "e" + (e1 >= 0 ? "+" : "-") + Math.Abs(e1);
            }
            _ = lead;
            return neg ? "-" + s : s;
        }

        static string Value(object v) => v switch
        {
            null => "-",
            double d => Js(d),
            bool b => b ? "true" : "false",
            string s => s,
            _ => v.ToString(),
        };

        static string Pose(Pose p) => "{" + string.Join(",", p.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => k + "=" + Value(p[k]))) + "}";

        /// <summary>An act's timing (the JS actTiming), as one line of text both sides write alike.</summary>
        public static string Timing(Module m)
        {
            Act.Timing a = Act.TimingOf(m);
            if (a == null) return "none";
            var s = new StringBuilder();
            s.Append("windup=").Append(Js(a.Windup)).Append(";recovery=").Append(Js(a.Recovery)).Append(";onBody=").Append(a.OnBody ? "true" : "false");
            s.Append(";targets=").Append(string.Join(",", a.Targets.Select(Js)));
            s.Append(";hits=").Append(string.Join("|", a.Hits.Select(h =>
                $"t={Js(h.T)},freeze={Js(h.Freeze)},shake={Js(h.Shake)},amp={Js(h.Amp)},push={Js(h.Push)},skin={(h.HasSkin ? h.Skin ?? "null" : "-")},at={(h.At is int at ? Js(at) : "-")}")));
            s.Append(";poses=").Append(string.Join("|", a.Poses.Select(k => $"{Js(k.T)}/{k.How}/{Pose(k.Pose)}")));
            return s.ToString();
        }

        public const int ActPad = 6, ActFps = 30;

        /// <summary>The canvas an act is drawn on and where its feet go.</summary>
        public static (int w, int h, int x, int y) ActCanvas(Module m)
        {
            var b = Act.Bounds(m);
            return (b.x1 - b.x0 + ActPad * 2, b.y1 - b.y0 + ActPad * 2, ActPad - b.x0, ActPad - b.y0);
        }

        /// <summary>
        /// Every frame of an act at 30 fps, on transparency, the rig's pose cache emptied first (the JS side runs each act in a fresh
        /// process). Each frame's text is its hash, then "~sx,sy" while the picture shakes and "*" while it's frozen.
        /// </summary>
        public static IEnumerable<(string text, byte[] rgba)> ActFrames(Module m)
        {
            Rig.ClearCache();
            var (w, h, x, y) = ActCanvas(m);
            var frame = new Frame(w, h);
            var act = new Performance(m, new Options { ["x"] = x, ["y"] = y });
            var rgba = new byte[w * h * 4];
            using SHA256 sha = SHA256.Create();
            int max = Px.RoundInt((m.Loop ? m.Duration * 2 - act.T : 8) * ActFps);
            (string, byte[]) Show()
            {
                frame.Clear();
                act.Draw(frame);
                frame.Composite(rgba);
                var (sx, sy) = act.Shake;
                return (Hash(sha, rgba) + (sx != 0 || sy != 0 ? $"~{sx},{sy}" : "") + (act.Frozen ? "*" : ""), rgba);
            }
            yield return Show();
            for (int i = 0; i < max && !act.Done; i++)
            {
                act.Update(1.0 / ActFps);
                yield return Show();
            }
        }

        /// <summary>Where the C# effect first parts from the JS frames, or null if every frame matches.</summary>
        public static (int frame, byte[] rgba, string why)? EffectMismatch(Module module, int w, int h, IReadOnlyList<string> js, int margin = 0)
        {
            int mw = module.W + margin * 2, mh = module.H + margin * 2;
            if ((mw, mh) != (w, h)) return (-1, null, $"{module.Name} is {mw}x{mh}, the js original is {w}x{h}");
            using SHA256 sha = SHA256.Create();
            int i = 0;
            foreach (byte[] rgba in Canvas.Frames(module, null, margin))
            {
                if (i < js.Count && Hash(sha, rgba) != js[i]) return (i, rgba, $"{module.Name} frame {i} differs from the js original");
                i++;
            }
            return i == js.Count ? ((int, byte[], string)?)null : (-1, null, $"{module.Name} has {i} frames, the js original has {js.Count}");
        }

        /// <summary>Where the C# act first parts from the JS act (timing, size or a frame), or null if it all matches.</summary>
        public static (int frame, byte[] rgba, string why)? ActMismatch(Module module, string timing, int w, int h, IReadOnlyList<string> js)
        {
            string mine = Timing(module);
            if (mine != timing) return (-1, null, $"{module.Name} act timing differs:\n  c#: {mine}\n  js: {timing}");
            var (cw, ch, _, _) = ActCanvas(module);
            if ((cw, ch) != (w, h)) return (-1, null, $"{module.Name} act canvas is {cw}x{ch}, the js original is {w}x{h}");
            int i = 0;
            foreach (var (text, rgba) in ActFrames(module))
            {
                if (i < js.Count && text != js[i]) return (i, (byte[])rgba.Clone(), $"{module.Name} act frame {i} is {text}, the js original is {js[i]}");
                i++;
            }
            return i == js.Count ? ((int, byte[], string)?)null : (-1, null, $"{module.Name} act has {i} frames, the js original has {js.Count}");
        }
    }
}
