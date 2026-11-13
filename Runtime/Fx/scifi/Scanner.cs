using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Scanner : Effect
    {
        static readonly Pose STIFF = new Pose { ["turn"] = 0.9, ["crouch"] = -1, ["fx"] = -7, ["fy"] = -14, ["bx"] = 8, ["by"] = -14, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose OK_ = new Pose { ["turn"] = 0.9, ["fx"] = -9, ["fy"] = -37, ["bx"] = 8, ["by"] = -15, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, new Pose()),
                new PoseKey(0.3, STIFF, "snap"),
                new PoseKey(1.75, STIFF),
                new PoseKey(1.85, OK_, "snap"),
                new PoseKey(2.5, OK_),
                new PoseKey(2.8, new Pose()),
                new PoseKey(3.2, new Pose()),
            },
        };

        public static readonly Module Module = new Module("scanner", "pixelfx/fx/scifi/scanner", 112, 78, 42, 58, 3.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#03140a", "#06301a", "#0c5a2c", "#16893e", "#2cc256", "#7cf08c", "#d0ffd4", "#ffffff" },
                ["seed"] = 22,
            },
            (o, e) => new Scanner(o, e), act: Act, actor: 2)
        {
            Title = "Scanner",
            Desc = "Brackets snap onto the hero, a scan line sweeps head to toe leaving a cooling outline, and a readout counts up to OK before it all lets go.",
            Tags = new[] { "scifi", "scan", "target", "hud", "tech", "status", "ui", "loop" },
            Bg = "ground",
        };

        const double D = 3.2, LOCK = 0.3, SCAN0 = 0.42, SCAN1 = 1.6, DONE = 1.75, LET = 2.5;

        static double ScanY(double t, Actor A) => Lerp(A.Y - 3, 1, Ease.InOutSine(Clamp((t - SCAN0) / (SCAN1 - SCAN0))));
        static double ScanT(double y, Actor A) => SCAN0 + ((SCAN1 - SCAN0) * Px.Acos(1 - 2 * Clamp((y + 0.5 - (A.Y - 3)) / (4 - A.Y)))) / Math.PI;

        // an L-shaped bracket: two arms from a corner, a hot pixel at the corner
        static void Bracket(Painter p, double x, double y, double sx, double sy, double L, uint c, uint hot, double a)
        {
            p.Line(x, y, x + sx * L, y, c, a);
            p.Line(x, y, x, y + sy * L, c, a);
            p.Line(x + sx, y + sy, x + sx * 3, y + sy, c, a * 0.6);
            p.Dot(x, y, hot, a);
        }

        readonly uint[] C;
        readonly Actor A;
        readonly List<int[]> edge = new List<int[]>();
        readonly Dictionary<int, int[]> rows = new Dictionary<int, int[]>();
        readonly ScifiLib.Ghosts g;

        Scanner(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            A = o.Actor;
            // the body: its outline pixels by row, and each row's extent
            Mask m = A.Mask;
            bool On(int x, int y) => x >= 0 && y >= 0 && x < m.W && y < m.H && m.Data[y * m.W + x] != 0;
            for (int y = -1; y <= m.H; y++)
                for (int x = -1; x <= m.W; x++)
                {
                    if (On(x, y))
                    {
                        int[] r = rows.TryGetValue(A.Y + y, out int[] rr) ? rr : new[] { A.X + x, A.X + x };
                        rows[A.Y + y] = new[] { Math.Min(r[0], A.X + x), Math.Max(r[1], A.X + x) };
                    }
                    else if (On(x - 1, y) || On(x + 1, y) || On(x, y - 1) || On(x, y + 1)) edge.Add(new[] { A.X + x, A.Y + y });
                }
            // data bits flicked off the body's edges as the line crosses them
            g = new ScifiLib.Ghosts(D, x => 0, null, 0, new double[] { -40, -58, 68, 9 });
            double seed = o.Seed;
            for (int y = A.Y; y < 0; y += 3)
            {
                if (!rows.TryGetValue(y, out int[] r)) continue;
                foreach (int[] xs in new[] { new[] { r[0] - 1, -1 }, new[] { r[1] + 1, 1 } })
                {
                    int x = xs[0], s = xs[1];
                    if (Rand(seed, y, s) < 0.6) g.Add(ScanT(y, A), P.Mote, x, y, s * (30 + 50 * Rand(y, s, 3)), -10 + 20 * Rand(y, s, 4), 0.2 + 0.15 * Rand(y, s, 5), Rand(y, s, 6) < 0.4 ? WHITE : C[5], 0.6);
                }
            }
        }

        public override void Draw(Painter p, double t)
        {
            // the box the brackets lock to, and how far out they are: closing in with a snap, letting go at the end
            double bx0 = A.X - 4, bx1 = A.X + A.W + 3, by0 = A.Y - 4, by1 = 3;
            double lockK = Ease.OutBack(Clamp(t / LOCK), 2.2), outK = Ease.InCubic(Clamp((t - LET) / 0.45)), spread = (1 - lockK) * 14 + outK * 12;
            bool gone = t > LET + 0.45 || (t > LET + 0.25 && Math.Floor((t - LET) * 20) % 2 == 1);
            double vis = gone ? 0 : 1;
            bool flash = (t > LOCK && t < LOCK + 0.07) || (t > DONE && t < DONE + 0.05) || (t > DONE + 0.12 && t < DONE + 0.17);
            uint bc = flash ? WHITE : C[5];
            double L = 7;
            if (vis > 0)
                foreach (double[] b in new[] { new[] { bx0 - spread, by0 - spread, 1, 1 }, new[] { bx1 + spread, by0 - spread, -1, 1 }, new[] { bx0 - spread, by1 + spread, 1, -1 }, new[] { bx1 + spread, by1 + spread, -1, -1 } })
                {
                    Bracket(p, b[0], b[1], b[2], b[3], L, bc, WHITE, vis);
                    p.Glow(b[0], b[1], 6, C[4], 0.35 * vis);
                }
            // on lock: a box flashes out from the brackets
            double lk = (t - LOCK) / 0.3;
            if (lk >= 0 && lk < 1)
            {
                double e = 3 + 10 * Ease.OutCubic(lk);
                uint c = lk < 0.25 ? WHITE : lk < 0.5 ? C[6] : lk < 0.75 ? C[5] : C[3];
                p.Line(bx0 - e, by0 - e, bx1 + e, by0 - e, c);
                p.Line(bx0 - e, by1 + e, bx1 + e, by1 + e, c);
                p.Line(bx0 - e, by0 - e, bx0 - e, by1 + e, c);
                p.Line(bx1 + e, by0 - e, bx1 + e, by1 + e, c);
            }
            // the outline, lit row by row as the line passes and cooling behind it
            double pulse = ScifiLib.Since(t, DONE, D) < 0.3 ? 1 - ScifiLib.Since(t, DONE, D) / 0.3 : 0, offY = Lerp(A.Y - 2, 2, Clamp((t - LET) / 0.4));
            if (t > SCAN0)
                foreach (int[] xy in edge)
                {
                    int x = xy[0], y = xy[1];
                    double a = t - ScanT(y, A);
                    if (a < 0 || (t > LET && y < offY)) continue;
                    uint c = pulse > 0.4 ? WHITE : a < 0.07 ? WHITE : a < 0.25 ? C[6] : a < 0.6 ? C[5] : t > LET - 0.3 ? C[3] : C[4];
                    p.Px(x, y, c);
                }
            // the scan line and the slice of light just above it
            if (t > SCAN0 && t < SCAN1)
            {
                int y = (int)Math.Floor(ScanY(t, A));
                for (int k = 1; k <= 5; k++)
                {
                    if (rows.TryGetValue(y - k, out int[] r))
                        for (int x = r[0]; x <= r[1]; x++)
                            if (A.Mask.Data[(y - k - A.Y) * A.W + x - A.X] != 0) p.Blend(x + 0.5, y - k + 0.5, k < 2 ? C[6] : C[5], 0.55 - k * 0.09);
                }
                rows.TryGetValue(y, out int[] ry);
                for (double x = bx0 - 2; x <= bx1 + 2; x++)
                {
                    bool inside = ry != null && x >= ry[0] && x <= ry[1];
                    p.Px(x, y, inside ? WHITE : C[6]);
                    p.Blend(x + 0.5, y - 0.5, C[5], 0.35);
                }
                p.GlowLine(bx0, y, bx1, y, 6, C[5], 0.45);
                if (ry != null)
                    foreach (double x in new[] { ry[0] - 0.5, ry[1] + 1.5 }) Sim.Sparkle(p, x, y + 0.5, 2, C[6], C[5], 0.3);
            }
            // the readout: a leader from the top-right bracket to a panel, a count, a bar filling, then OK
            double lead = Clamp((t - LOCK) / 0.1);
            if (vis > 0 && lead > 0)
            {
                double px = bx1 + spread + 6, py = by0 - spread + 2, k = Clamp((t - SCAN0) / (SCAN1 - SCAN0));
                bool ok = t > DONE;
                p.Line(bx1 + spread, by0 - spread, Lerp(bx1 + spread, px - 1, lead), Lerp(by0 - spread, py + 4, lead), C[3]);
                if (lead < 1)
                {
                    g.At(t).Draw(p);
                    return;
                }
                p.Line(px - 1, py + 4, px + 1, py + 4, C[3]);
                double del = Clamp((t - LET) / 0.25);
                string full = ok ? "OK" : ((int)Math.Floor(k * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
                string label = full.Substring(0, Math.Min(full.Length, Math.Max(0, (int)Math.Ceiling(full.Length * (1 - del)))));
                bool blinkOk = ok && Math.Floor((t - DONE) * 8) % 2 == 0 && t < DONE + 0.5;
                if (label.Length > 0) p.Text(label, px + 2, py, blinkOk ? WHITE : C[6], "mini", 2, shadow: C[1]);
                // the bar: ten cells filling as it goes, emptying from the right as it lets go
                for (int i = 0; i < 10; i++)
                {
                    if (i >= 10 * (1 - del)) break;
                    bool on = (i + 1) / 10.0 <= k + 1e-6 || ok;
                    p.Rect(px + 2 + i * 3, py + 13, 2, 3, on ? (ok ? C[6] : C[5]) : C[2]);
                }
                p.Glow(px + 16, py + 6, 18, C[4], 0.2 * vis);
            }
            g.At(t).Draw(p);
        }
    }
}
