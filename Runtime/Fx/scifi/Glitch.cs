using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Glitch : Effect
    {
        static readonly Pose TWITCH_A = new Pose { ["turn"] = 0.3, ["lean"] = 3, ["crouch"] = 1, ["fx"] = 13, ["fy"] = -31, ["bx"] = 6, ["by"] = -14, ["hx"] = 1 };
        static readonly Pose TWITCH_B = new Pose { ["turn"] = 1.2, ["lean"] = -3, ["crouch"] = 0, ["fx"] = -9, ["fy"] = -14, ["bx"] = 14, ["by"] = -31, ["hx"] = -1 };
        static readonly Pose FINE = new Pose();

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, FINE),
                new PoseKey(0.3, TWITCH_A, "snap"),
                new PoseKey(0.37, TWITCH_B, "snap"),
                new PoseKey(0.44, TWITCH_A, "snap"),
                new PoseKey(0.53, FINE, "snap"),
                new PoseKey(1.1, TWITCH_B, "snap"),
                new PoseKey(1.23, FINE, "snap"),
                new PoseKey(1.7, TWITCH_A, "snap"),
                new PoseKey(1.8, new Pose(TWITCH_B) { ["crouch"] = 3 }, "snap"),
                new PoseKey(1.9, TWITCH_A, "snap"),
                new PoseKey(2, TWITCH_B, "snap"),
                new PoseKey(2.07, FINE, "snap"),
                new PoseKey(2.4, FINE),
            },
        };

        public static readonly Module Module = new Module("glitch", "pixelfx/fx/scifi/glitch", 88, 64, 44, 54, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#14101f", "#3b1f5c", "#ff2e63", "#ff7aa8", "#2ef2ff", "#b4fbff", "#ffffff" },
                ["seed"] = 7,
            },
            (o, e) => new Glitch(o, e), act: Act, actor: 2,
            // the effect draws the hero itself, clean or broken
            actorAlpha: (t, o) => 0)
        {
            Title = "Glitch",
            Desc = "The hero's signal breaks up in bursts: an RGB split, slices torn sideways, rows smeared out, junk blocks and dead pixels, then snaps back clean.",
            Tags = new[] { "scifi", "glitch", "digital", "corrupt", "hologram", "status", "loop" },
            Bg = "ground",
        };

        const double DURATION = 2.4;
        const int FPS = 30;
        static readonly int NF = RoundInt(DURATION * FPS);
        // the bursts: [first frame, last frame, how hard]
        static readonly double[][] BURSTS = { new double[] { 9, 16, 0.8 }, new double[] { 33, 37, 0.5 }, new double[] { 51, 62, 1 } };

        readonly uint[] C;
        readonly Actor A;
        readonly double S;

        Glitch(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            A = o.Actor;
            S = o.Seed;
        }

        public override void Draw(Painter p, double t)
        {
            uint RED = C[2], CYAN = C[4];
            int f = (int)(Math.Floor(t * FPS + 1e-6) % NF);
            double[] burst = null;
            foreach (double[] b in BURSTS)
                if (f >= b[0] && f <= b[1])
                {
                    burst = b;
                    break;
                }
            double k = burst != null ? burst[2] : 0;
            double r(params double[] n)
            {
                var all = new double[n.Length + 2];
                all[0] = S;
                all[1] = f;
                Array.Copy(n, 0, all, 2, n.Length);
                return Rand(all);
            }
            byte[] mask = A.Mask.Data;
            uint[] pixels = A.Pixels;
            // what this frame's corruption is
            double split = burst != null ? Round(1 + r(1) * 3 * k) * (r(2) < 0.5 ? -1 : 1) : 0;
            var tears = new List<double[]>();
            if (burst != null)
                for (int i = 0; i < 1 + Math.Floor(r(3) * 4 * k); i++)
                    tears.Add(new[] { A.Y + Math.Floor(r(4, i) * A.H), 1 + Math.Floor(r(5, i) * 5), Round((r(6, i) - 0.5) * 16 * k), r(7, i) });
            // a lone row slips now and then even when it's calm
            if (burst == null && r(8) < 0.07) tears.Add(new[] { A.Y + Math.Floor(r(9) * A.H), 1, r(10) < 0.5 ? -2 : 2, 1 });
            bool interlace = burst != null && r(11) < 0.3;
            double roll = burst != null && r(12) < 0.15 ? -2 - Math.Floor(r(13) * 2) : 0;
            bool poster = burst != null && r(14) < 0.15 * k;
            double[] TearAt(double y)
            {
                foreach (double[] tr in tears)
                    if (y >= tr[0] && y < tr[0] + tr[1]) return new[] { tr[2], tr[3] };
                return null;
            }
            // the calm signal: a faint bar rolling down the body
            double bar = A.Y + ((t / DURATION) * 2 * (A.H + 12)) % (A.H + 12) - 6;

            // the split: the red and cyan ghosts first, offset either side
            if (split != 0)
                for (int my = 0; my < A.H; my++)
                    for (int mx = 0; mx < A.W; mx++)
                    {
                        if (mask[my * A.W + mx] == 0) continue;
                        double y = A.Y + my + roll;
                        double[] tr = TearAt(A.Y + my);
                        double dx = tr != null ? tr[0] : 0;
                        p.Px(A.X + mx + dx - split, y, RED);
                        p.Px(A.X + mx + dx + split, y, CYAN);
                    }
            // the body itself, row by row: torn, interlaced, rolled, posterised, tinted where the bar passes
            for (int my = 0; my < A.H; my++)
            {
                double ry = A.Y + my, y = ry + roll;
                if (interlace && (my & 1) != 0) continue;
                double[] tr = TearAt(ry);
                double dx = tr != null ? tr[0] : 0;
                uint tint = tr != null && tr[1] < 0.35 ? (tr[1] < 0.18 ? RED : CYAN) : 0;
                double near = Math.Abs(ry - bar);
                for (int mx = 0; mx < A.W; mx++)
                {
                    uint c = pixels[my * A.W + mx];
                    if (c == 0) continue;
                    uint col = c;
                    if (poster) col = Luma(c) > 0.32 ? C[5] : C[1];
                    else if (tint != 0) col = Mix(c, tint, 0.6);
                    else if (near < 1) col = Mix(c, C[5], 0.35);
                    p.Px(A.X + mx + dx, y, col);
                }
                // a torn row smears its edge pixel out sideways, fading
                if (tr != null && Math.Abs(dx) > 2 && Rand(S, f, ry) < 0.6)
                {
                    bool right = dx > 0;
                    int found = -1;
                    for (int i = 0; i < A.W; i++)
                        if (mask[my * A.W + (right ? A.W - 1 - i : i)] != 0)
                        {
                            found = i;
                            break;
                        }
                    int mx = right ? A.W - 1 - found : found;
                    if (mx >= 0 && mx < A.W)
                    {
                        uint c = pixels[my * A.W + mx];
                        double L = 4 + Math.Floor(Rand(S, f, ry, 1) * 10);
                        for (int i = 1; i <= L; i++) p.Dot(A.X + mx + dx + (right ? i : -i), y, c, 1 - i / (L + 1));
                    }
                }
            }
            // a roll leaves the top slice repeated at the bottom
            if (roll != 0)
                for (int my = 0; my < -roll; my++)
                    for (int mx = 0; mx < A.W; mx++)
                    {
                        uint c = pixels[my * A.W + mx];
                        if (c != 0 && ((mx + my) & 1) != 0) p.Px(A.X + mx, A.Y + A.H + roll + my, Mix(c, C[1], 0.5));
                    }
            // junk blocks round the body
            if (burst != null)
                for (int i = 0; i < 2 + Math.Floor(r(20) * 5 * k); i++)
                {
                    double w = 2 + Math.Floor(r(21, i) * 9), h = 1 + Math.Floor(r(22, i) * 3), x = A.X - 6 + Math.Floor(r(23, i) * (A.W + 12)), y = A.Y + Math.Floor(r(24, i) * A.H);
                    uint c = new[] { WHITE, RED, CYAN, C[5], C[3] }[(int)Math.Floor(r(25, i) * 5)];
                    if (r(26, i) < 0.4)
                    {
                        for (int yy = 0; yy < h; yy++)
                            for (int xx = 0; xx < w; xx++) p.Px(x + xx, y + yy, ((xx + yy) & 1) != 0 ? c : C[0]);
                    }
                    else p.Rect(x, y, w, h, c);
                }
            // dead pixels thrown off by a burst, drifting and blinking out
            foreach (double[] bb in BURSTS)
            {
                double a = bb[0], b = bb[1], kk = bb[2];
                double age = (f - a + NF) % NF;
                if (age > b - a + 8) continue;
                for (int i = 0; i < 10 * kk; i++)
                {
                    double life = 4 + Math.Floor(Rand(S, a, i) * 6), at = Math.Floor(Rand(S, a, i, 1) * (b - a + 1)), q = age - at;
                    if (q < 0 || q > life || (q > life / 2 && (ToInt32(q) & 1) != 0)) continue;
                    double x0 = A.X + Rand(S, a, i, 2) * A.W, y0 = A.Y + Rand(S, a, i, 3) * A.H, side = Rand(S, a, i, 4) < 0.5 ? -1 : 1;
                    p.Px(x0 + side * q * (1 + Rand(S, a, i, 5) * 2), y0 - q * 0.5, new[] { WHITE, RED, CYAN }[i % 3]);
                }
            }
            // light: a cold flicker in the bursts, the split's colors glowing at its edges
            if (burst != null)
            {
                if (f == burst[0]) p.Glow(A.X + A.W / 2.0, A.Y + A.H / 2.0, 26, C[5], 0.4);
                p.Glow(A.X + A.W / 2.0 - split * 4, A.Y + A.H / 2.0, 18, RED, 0.18 * k);
                p.Glow(A.X + A.W / 2.0 + split * 4, A.Y + A.H / 2.0, 18, CYAN, 0.18 * k);
            }
            if (Math.Abs(bar - (A.Y + A.H / 2.0)) < A.H / 2.0 + 3) p.GlowLine(A.X - 2, bar, A.X + A.W + 2, bar, 4, C[4], 0.12);
        }
    }
}
