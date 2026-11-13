// The lit floor line is adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class Resurrection : Effect
    {
        static readonly Pose DOWN = new Pose { ["turn"] = 0.9, ["crouch"] = 8, ["lean"] = 6, ["hy"] = 2, ["fx"] = -4, ["fy"] = -10, ["bx"] = 9, ["by"] = -10, ["ffx"] = 8, ["fbx"] = -7 };
        static readonly Pose RISEN = new Pose { ["turn"] = 1.15, ["crouch"] = -1, ["fx"] = -15, ["fy"] = -33, ["bx"] = 16, ["by"] = -33, ["hy"] = -1, ["ffx"] = -3, ["ffy"] = -5, ["fbx"] = 4, ["fby"] = -5 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0.6,
            Poses = new[]
            {
                new PoseKey(0, DOWN),
                new PoseKey(0.5, DOWN),
                new PoseKey(0.85, new Pose(DOWN) { ["crouch"] = 6, ["lean"] = 4, ["hy"] = 1 }),
                new PoseKey(1.15, new Pose { ["turn"] = 1, ["crouch"] = 2, ["fx"] = -8, ["fy"] = -17, ["bx"] = 9, ["by"] = -17 }),
                new PoseKey(1.5, new Pose(RISEN) { ["y"] = -2 }),
                new PoseKey(1.95, new Pose(RISEN) { ["y"] = -3 }),
                new PoseKey(2.1, new Pose(RISEN) { ["y"] = -2, ["crouch"] = 1, ["fy"] = -37, ["by"] = -37 }),
                new PoseKey(2.3, new Pose(RISEN) { ["y"] = -9, ["fx"] = -15, ["fy"] = -38, ["bx"] = 16, ["by"] = -38 }),
                new PoseKey(2.95, new Pose(RISEN) { ["y"] = -6 }),
                new PoseKey(3.8, new Pose { ["turn"] = 1, ["y"] = 0, ["crouch"] = 2, ["fx"] = -8, ["fy"] = -17, ["bx"] = 9, ["by"] = -17 }),
                new PoseKey(4.3, Poses.READY),
                new PoseKey(4.8, Poses.READY),
            },
            Dust = new[] { new DustCue { T = 3.8, X = 0, N = 5 } },
        };

        public static readonly Module Module = new Module("resurrection", "pixelfx/fx/holy/resurrection", 192, 160, 96, 140, 4.8, false, 30,
            new Options { ["palette"] = GOLD, ["ash"] = ASH, ["seed"] = 5 },
            (o, e) => new Resurrection(o, e), act: Act, actor: 2)
        {
            Title = "Resurrection",
            Desc = "Light pours down on the fallen and life climbs back up the body; wings of light unfold, a halo forms, one great wingbeat, then the wings scatter into feathers.",
            Tags = new[] { "holy", "revive", "resurrect", "wings", "halo", "light", "character", "one-shot" },
            Bg = "ground",
        };

        const double TOP = -140;
        // the story: the light pours, life climbs the body, wings and halo, the beat, the wings come apart
        const double POUR = 0.34, CL0 = 0.5, CL1 = 1.15, W0 = 1.1, H0 = 1.24, H1 = 1.5, B0 = 1.95, B1 = 2.25, B2 = 2.37, B3 = 2.8, SC = 2.95, GONE = 3.85;
        const double HX = 0, HY = -47, HRX = 9.5, HRY = 2.6;

        // the right wing (the left mirrors it), from its shoulder: the arm folded and spread, then each feather
        // [where along the arm 0..1, angle spread, length, width, kind]: 0 primary, 1 secondary, 2 covert
        static readonly double[] SHOULDER = { 4, -20 };
        static readonly double[][] ARM_OPEN = { new double[] { 0, 0 }, new double[] { 7, -9 }, new double[] { 15, -19 }, new double[] { 20, -27 } };
        static readonly double[][] ARM_FOLD = { new double[] { 0, 0 }, new double[] { 2, -2 }, new double[] { 3, -3 }, new double[] { 4, -3 } };
        static readonly double[][] QUILLS =
        {
            new[] { 0.14, 1.35, 11, 2.6, 1 }, new[] { 0.26, 1.12, 14, 2.8, 1 }, new[] { 0.38, 0.88, 17, 3, 1 }, new[] { 0.5, 0.62, 19, 3.1, 1 }, new[] { 0.62, 0.35, 21, 3.2, 1 }, new[] { 0.72, 0.08, 23, 3.3, 1 },
            new[] { 0.8, -0.2, 26, 3.4, 0 }, new[] { 0.85, -0.45, 29, 3.5, 0 }, new[] { 0.9, -0.72, 31, 3.6, 0 }, new[] { 0.95, -1, 30, 3.6, 0 }, new[] { 1, -1.3, 26, 3.4, 0 },
            new[] { 0.22, 1.2, 7, 2.2, 2 }, new[] { 0.34, 0.98, 8, 2.3, 2 }, new[] { 0.46, 0.75, 9, 2.4, 2 }, new[] { 0.58, 0.5, 10, 2.5, 2 }, new[] { 0.7, 0.22, 10, 2.5, 2 }, new[] { 0.81, -0.1, 11, 2.6, 2 }, new[] { 0.92, -0.5, 11, 2.6, 2 },
        };
        static readonly double[] FLEX = { 1, 0.5, 0.3 };

        static double[] Along(double[][] pts, double s)
        {
            double L = 0;
            var seglen = new double[pts.Length - 1];
            for (int i = 1; i < pts.Length; i++) L += seglen[i - 1] = Hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
            double d = s * L;
            for (int i = 0; i < seglen.Length; i++)
            {
                if (d <= seglen[i] || i == seglen.Length - 1)
                {
                    double f = seglen[i] != 0 && !double.IsNaN(seglen[i]) ? Clamp(d / seglen[i]) : 0;
                    return new[] { Lerp(pts[i][0], pts[i + 1][0], f), Lerp(pts[i][1], pts[i + 1][1], f) };
                }
                d -= seglen[i];
            }
            return pts[pts.Length - 1];
        }

        // the beat: a slow raise, a hard fast downstroke, a recovery that overshoots a touch
        static double Beat(double t)
        {
            if (t < B0) return 0;
            if (t < B1) return -0.34 * Ease.InOutSine(Seg(t, B0, B1));
            if (t < B2) return Lerp(-0.34, 0.66, Ease.InQuad(Seg(t, B1, B2)));
            return 0.66 * (1 - Ease.OutBack(Seg(t, B2, B3), 1.3));
        }

        sealed class Ray
        {
            public double o, w, b, f, ph;
        }

        sealed class Mote
        {
            public double q, y, v, amp, ph, f;
        }

        sealed class Quill
        {
            public double s, ang, len, wid, fold, open;
            public int kind;
            public double[] off;
            public bool[] gone;
        }

        sealed class WF
        {
            public int i;
            public Quill q;
            public double x, y, ang, len, wid, a, hot;
        }

        readonly uint[] C, AS, WING;
        readonly Actor A;
        readonly Shape S;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Feathers loose;
        readonly Dictionary<int, List<int>> rows = new Dictionary<int, List<int>>();
        readonly Ray[] rays, cores;
        readonly Mote[] motes;
        readonly Quill[] quills;
        readonly float[] profile = new float[121];
        double t;
        bool poured, flashed, formed, beaten;

        Resurrection(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            AS = Fit(o.Ramp("ash"), 6);
            A = o.Actor;
            S = ShapeOf(A);
            R = new Rng(o.Seed);
            WING = new[] { C[2], C[3], C[4], C[5], C[6], WHITE };
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[3] }, haze: C[4], emitter: e);
            lights = new Lights(e);
            loose = new Feathers(R, 0);
            foreach (int[] q in S.Edge)
            {
                if (!rows.TryGetValue(q[1], out List<int> xs)) rows[q[1]] = xs = new List<int>();
                xs.Add(q[0]);
            }
            // the shaft of light: a few rays of their own width and shimmer
            rays = new Ray[6];
            for (int i = 0; i < 6; i++) rays[i] = new Ray { o = -0.75 + i * 0.3 + R.Range(-0.08, 0.08), w = R.Range(0.1, 0.2), b = R.Range(0.55, 1), f = R.Range(1.5, 3.5), ph = R.Range(0, TAU) };
            cores = rays.OrderByDescending(r => r.b).Take(2).ToArray();
            motes = new Mote[34];
            for (int i = 0; i < 34; i++) motes[i] = new Mote { q = R.Range(-1, 1), y = R.Range(0, 1), v = R.Range(4, 11), amp = R.Range(0.6, 2.4), ph = R.Range(0, TAU), f = R.Range(0.6, 1.4) };
            // each feather opens on its own beat, outer ones last, and comes off at its own time, tips first
            var reach = new double[QUILLS.Length];
            for (int i = 0; i < QUILLS.Length; i++)
            {
                double[] b = Along(ARM_OPEN, QUILLS[i][0]);
                reach[i] = Hypot(b[0] + Px.Cos(QUILLS[i][1]) * QUILLS[i][2], b[1] + Px.Sin(QUILLS[i][1]) * QUILLS[i][2]);
            }
            double far = double.NegativeInfinity;
            foreach (double r in reach) far = Math.Max(far, r);
            quills = new Quill[QUILLS.Length];
            for (int i = 0; i < QUILLS.Length; i++)
            {
                double[] Q = QUILLS[i];
                double s = Q[0];
                int kind = (int)Q[4];
                double Off() => SC + (1 - reach[i] / far) * 0.2 + R.Range(0, 0.04);
                quills[i] = new Quill
                {
                    s = s, ang = Q[1], len = Q[2], wid = Q[3], kind = kind, fold = 1.45 + (i % 6) * 0.04,
                    open = kind == 0 ? 0.06 + (s - 0.8) * 0.45 : kind == 1 ? s * 0.06 : 0.03 + s * 0.05,
                    off = new[] { Off(), Off() }, gone = new[] { false, false },
                };
            }
        }

        double ClimbY(double tt) => Lerp(1, S.Top - 2, Ease.InOutSine(Seg(tt, CL0, CL1)));
        static double ShaftK(double tt) => Smooth(0, 0.12, tt) * (1 - Smooth(3.0, 3.75, tt));
        static double ShaftW(double tt) => Lerp(0.4, 1, Ease.OutCubic(Seg(tt, 0, 0.6))) * Lerp(1, 0.12, Ease.InCubic(Seg(tt, 2.9, 3.6)));
        static double HalfW(double y, double tt) => Lerp(8, 25, (y - TOP) / -TOP) * ShaftW(tt);

        // JS reads past the end of the profile as undefined: NaN from there on
        double ProfileAt(double i) => i >= 0 && i < profile.Length ? profile[(int)i] : double.NaN;

        // the pose of every feather of one wing at time tt: quill base (x, y), angle, length, width, alpha, hot
        (List<WF> output, double[][] arm) Wing(double tt, int side)
        {
            double oa = Ease.OutBack(Seg(tt, W0, W0 + 0.4), 1.6), rot = Beat(tt), om = (Beat(tt) - Beat(tt - 0.03)) / 0.03;
            var arm = new double[ARM_FOLD.Length][];
            for (int i = 0; i < ARM_FOLD.Length; i++) arm[i] = new[] { Lerp(ARM_FOLD[i][0], ARM_OPEN[i][0], oa), Lerp(ARM_FOLD[i][1], ARM_OPEN[i][1], oa) };
            double cr = Px.Cos(rot), sr = Px.Sin(rot);
            double[] Place(double[] q) => new[] { side * (SHOULDER[0] + q[0] * cr - q[1] * sr), SHOULDER[1] + q[0] * sr + q[1] * cr };
            double a0 = Smooth(W0, W0 + 0.1, tt);
            var output = new List<WF>();
            for (int i = 0; i < quills.Length; i++)
            {
                Quill q = quills[i];
                if (tt >= q.off[side > 0 ? 0 : 1] + 0.05) continue;
                double of = Ease.OutBack(Seg(tt, W0 + q.open, W0 + q.open + 0.42), 2);
                double[] b = Place(Along(arm, q.s));
                double ang = Lerp(q.fold, q.ang, of) + rot - Clamp(om * 0.045 * FLEX[q.kind], -0.5, 0.5);
                output.Add(new WF
                {
                    i = i, q = q, x = b[0], y = b[1], ang = side > 0 ? ang : Math.PI - ang, len = q.len * Lerp(0.3, 1, of), wid = q.wid, a = a0,
                    hot = tt < W0 + q.open + 0.07 || tt >= q.off[side > 0 ? 0 : 1] ? 1 : 0,
                });
            }
            var placed = new double[arm.Length][];
            for (int i = 0; i < arm.Length; i++) placed[i] = Place(arm[i]);
            return (output, placed);
        }

        static readonly int[] TRAILS = { 6, 8, 10 };

        void DrawWing(Painter p, int side)
        {
            var (output, arm) = Wing(t, side);
            if (output.Count == 0) return;
            WF[] order = output.OrderByDescending(f => f.q.ang).ToArray();
            // the lowest feathers first, so each one above lays its warm trailing edge over the next; coverts on top
            foreach (WF f in order)
                if (f.q.kind != 2) Feather(p, f.x, f.y, f.ang, f.len, f.wid, WING, f.a, side, f.hot);
            foreach (WF f in order)
                if (f.q.kind == 2) Feather(p, f.x, f.y, f.ang, f.len, f.wid, WING, f.a, side, f.hot);
            // the leading edge: a white bone of light with a gold underside
            double k = (double)output.Count / quills.Length;
            if (k > 0.5)
            {
                p.Polyline(arm.Select(q => new[] { q[0], q[1] + 1 }).ToArray(), C[5], output[0].a);
                p.Polyline(arm, WHITE, output[0].a);
            }
            p.Glow(side * 28, -36, 38, C[4], 0.26 * output[0].a * k);
            double g = ((t - W0 - 0.45) * 1.3) % 1;
            if (k > 0.9 && t > W0 + 0.45 && g < 0.8)
            {
                double[] gp = Along(arm, g / 0.8);
                Sim.Sparkle(p, gp[0], gp[1], 2 + Px.Sin((g / 0.8) * Math.PI) * 2, WHITE, C[6], 0.4);
            }
            p.GlowLine(arm[0][0], arm[0][1], arm[3][0], arm[3][1], 7, C[5], 0.25 * output[0].a * k);
            // the tips of the primaries leave trails of light as they come down
            if (t > B1 + 0.03 && t < B2 + 0.14)
                foreach (int i in TRAILS)
                {
                    var pts = new List<double[]>();
                    for (int j = 0; j < 10; j++)
                    {
                        double tt = Math.Max(B1 + 0.01, t - j * 0.012);
                        WF w = Wing(tt, side).output.FirstOrDefault(f => f.i == i);
                        if (w != null) pts.Add(new[] { w.x + Px.Cos(w.ang) * (w.len - 1), w.y + Px.Sin(w.ang) * (w.len - 1) });
                    }
                    double fade = 1 - Smooth(B2, B2 + 0.14, t);
                    for (int j = 1; j < pts.Count; j++) p.Line(pts[j - 1][0], pts[j - 1][1], pts[j][0], pts[j][1], j < 3 ? WHITE : j < 6 ? C[6] : C[4], fade * (1.1 - j / 10.0));
                }
        }

        void Shaft(Painter p)
        {
            double k = ShaftK(t);
            if (k <= 0) return;
            for (int i = 0; i < profile.Length; i++)
            {
                double q = (i - 60) / 40.0;
                double I = 0;
                foreach (Ray r in rays)
                {
                    double d = (q - r.o) / r.w;
                    I += r.b * Exp(-d * d) * (0.72 + 0.28 * Px.Sin(t * r.f + r.ph));
                }
                profile[i] = (float)((I * 0.6 + 0.35 * Math.Max(0, 1 - q * q)) * Smooth(1.25, 0.7, Math.Abs(q)));
            }
            double front = Lerp(TOP, 1, Ease.InQuad(Seg(t, 0, POUR))), w0 = HalfW(0, t) * 1.3;
            p.Each(-w0 - 1, TOP, w0 + 1, Math.Min(front, 0.5), (x, y, sx, sy) =>
            {
                double w = HalfW(y, t), q = x / w;
                if (q < -1.5 || q > 1.5) return;
                double I = ProfileAt(Round(q * 40) + 60) * k * (0.75 + 0.25 * Smooth(TOP, 0, y)) * Smooth(front, front - 10, y);
                if (I <= 0.01) return;
                p.BlendPx(sx, sy, C[6], I * 0.24);
                p.Add(sx, sy, C[4], I * 0.3);
            });
            // the brightest rays show as fine lines, light running down them
            foreach (Ray r in cores)
            {
                double sh = 0.75 + 0.25 * Px.Sin(t * r.f + r.ph);
                for (double y = TOP; y < Math.Min(front, 0); y++)
                {
                    double pulse = 0.5 + 0.5 * Px.Sin(y * 0.13 - t * 11 + r.ph);
                    p.Dot(r.o * HalfW(y, t), y, pulse > 0.8 ? WHITE : C[6], k * sh * (0.15 + 0.55 * pulse) * Smooth(front, front - 10, y));
                }
            }
            // the head of the pour
            if (t < POUR + 0.05 && front > TOP + 14)
            {
                double fy = Math.Min(front, 0);
                p.Glow(0, fy, 14, C[5], 0.7 * k);
                for (int dy = -6; dy <= 0; dy++) p.Dot(0, fy + dy, dy > -3 ? WHITE : C[6], 1 + dy / 7.0);
            }
            // dust motes lit where they cross a ray
            foreach (Mote m in motes)
            {
                double y = TOP + ((((m.y * -TOP + t * m.v) % -TOP) + -TOP) % -TOP);
                if (y > front) continue;
                double w = HalfW(y, t), x = m.q * w + Px.Sin(t * m.f + m.ph) * m.amp, I = ProfileAt(Round((x / w) * 40) + 60) * k;
                if (I < 0.25) continue;
                p.Dot(x, y, I > 0.75 ? WHITE : I > 0.5 ? C[6] : C[5], Math.Min(1, I * 1.4));
                p.Light(x, y, C[5], I * 0.5);
            }
        }

        void Pool(Painter p, bool back)
        {
            double k = ShaftK(t) * Smooth(POUR - 0.05, POUR + 0.1, t), w = HalfW(0, t) * 1.25;
            if (k <= 0) return;
            if (back)
            {
                p.Glow(0, -2, w * 1.6, C[4], 0.3 * k);
                return;
            }
            // the floor catches the light: a bright line, and a pool fading into the stone in front
            for (int y = 0; y <= 3; y++)
                for (double x = Math.Floor(-w); x <= w; x++)
                {
                    double u = Hypot(x / w, y / 4.0);
                    if (u >= 1) continue;
                    double a = k * (y == 0 ? Math.Sqrt(1 - u) * 1.2 : (1 - u) * 0.9);
                    p.Dot(x, y, u < 0.3 ? (y != 0 ? C[5] : WHITE) : u < 0.6 ? C[5] : u < 0.85 ? C[4] : C[3], a);
                }
        }

        void DrawHalo(Painter p)
        {
            double form = Ease.OutCubic(Seg(t, H0, H1)), k = 1 - Smooth(3.35, 3.9, t);
            if (form <= 0 || k <= 0) return;
            bool white = t >= H1 && t < H1 + 0.06;
            double gl = (t - H1) * 4.2 + Math.PI / 2;
            const int n = 96;
            for (int j = 0; j < n; j++)
            {
                double g = ((double)j / n) * TAU, fromFront = Math.Abs(((g - Math.PI / 2 + Math.PI) % TAU + TAU) % TAU - Math.PI);
                if (fromFront > form * Math.PI + 0.02) continue;
                double x = HX + Px.Cos(g) * HRX * (0.9 + 0.1 * form), y = HY + Px.Sin(g) * HRY;
                bool near = Px.Sin(g) >= 0;
                double dg = Math.Abs(((g - gl + Math.PI) % TAU + TAU) % TAU - Math.PI);
                bool hot = white || dg < 0.22 || fromFront > form * Math.PI - 0.3;
                if (near)
                {
                    p.Dot(x, y, hot ? WHITE : C[6], k);
                    p.Dot(x, y + 1, hot ? C[6] : C[4], k);
                }
                else p.Dot(x, y, hot ? C[6] : C[5], k * 0.9);
            }
            p.Glow(HX, HY, 20, C[4], 0.4 * k * form);
            p.Glow(HX, HY + 1, 9, WHITE, 0.2 * k * form);
            double gx = HX + Px.Cos(gl) * HRX, gy = HY + Px.Sin(gl) * HRY;
            if (t > H1 && Px.Sin(gl) > 0.2) Sim.Sparkle(p, gx, gy + 0.5, 2 + 2 * Px.Sin(gl), C[6], C[4], 0.35 * k);
            if (t >= H1 && t < H1 + 0.3) Sim.Sparkle(p, HX, HY + HRY + 0.5, 7 * (1 - (t - H1) / 0.3), WHITE, C[5], 0.6);
        }

        void Body(Painter p)
        {
            if (t >= CL1 + 0.6) return;
            double Ly = ClimbY(t);
            if (t < CL1)
            {
                Recolor(p, A, (c, x, y) =>
                {
                    double d = y - Ly;
                    if (d < -0.5) return ToneOf(AS, c);
                    if (d < 0.5) return WHITE;
                    if (d < 1.5) return C[6];
                    double kk = Exp(-(d - 1.5) / 4) * 0.7;
                    return kk > 0.08 ? Mix(c, C[5], kk) : 0;
                });
                // the outline catches the light where the glow has been
                foreach (int[] q in S.Out)
                {
                    double d = q[1] - Ly;
                    if (d < -0.5 || d > 12) continue;
                    p.Dot(q[0], q[1], d < 1 ? WHITE : d < 5 ? C[5] : C[4], d < 1 ? 1 : 1.1 - d / 12);
                }
                if (t > CL0)
                {
                    p.Glow(0, Ly, 26, C[4], 0.5);
                    p.Glow(0, Ly, 8, WHITE, 0.3);
                    p.Glow(0, -18, 36, C[5], 0.16 * Seg(t, CL0, CL1));
                }
                return;
            }
            // the hero frame: the body flashes white, then cools through gold back to itself
            double u = t - CL1;
            Recolor(p, A, (c, x, y) => u < 0.06 ? WHITE : u < 0.45 ? Mix(c, C[5], 0.55 * Px.Pow(1 - u / 0.45, 2)) : 0);
            if (u < 0.25)
                foreach (int[] q in S.Out)
                    p.Dot(q[0], q[1], u < 0.08 ? WHITE : C[5], 1 - u / 0.25);
        }

        static readonly int[] SIDES = { -1, 1 };

        public override void Update(double dt)
        {
            t += dt;
            // light streaks pour down the shaft, faster as they fall
            if (t > 0.05 && t < 2.9 && R.Chance(0.16))
            {
                double q = R.Range(-0.9, 0.9), y = TOP + R.Range(8, 24), v0 = R.Range(50, 90), life = Px.Log(1 + (3 * (-y - 2)) / v0) / 3;
                ps.Spawn(P.Streak, q * HalfW(y, t), y, q * 2, v0, life, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (t > 0.3 && t < 2.8 && R.Chance(0.05)) ps.Spawn(P.Star, R.Range(-1, 1) * HalfW(-30, t), -R.Range(4, 90), R.Range(-3, 3), R.Range(-4, 6), R.Range(0.4, 0.8), R.Chance(0.5) ? C[6] : WHITE, R.Range(0.6, 1.8));
            if (!poured && t >= POUR)
            {
                poured = true;
                lights.Add(0, -12, 42, C[5], 0.9, 0.4);
                lights.Add(0, -6, 16, WHITE, 0.6, 0.1);
                for (int i = 0; i < 14; i++) ps.Spawn(P.Star, R.Range(-18, 18), -R.Range(0, 6), R.Range(-20, 20), -R.Range(10, 50), R.Range(0.5, 1.1), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 2.2));
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(60, 150);
                    ps.Spawn(P.Spark, R.Range(-3, 3), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.5));
                }
            }
            // life climbs the body: bits of light peel off the rim at the front
            if (t > CL0 && t < CL1)
            {
                double Ly = Round(ClimbY(t));
                foreach (double y in new[] { Ly, Ly + 1 })
                {
                    List<int> xs = null;
                    if (y == Math.Floor(y) && Math.Abs(y) < int.MaxValue) rows.TryGetValue((int)y, out xs);
                    if (xs == null || !R.Chance(0.35)) continue;
                    int x = R.Pick(xs.ToArray());
                    int s = Sign(x + 0.5);
                    ps.Spawn(R.Chance(0.6) ? P.Mote : P.Star, x + s, y, s * R.Range(8, 30), -R.Range(15, 45), R.Range(0.35, 0.7), R.Chance(0.5) ? C[6] : C[5], R.Range(0.5, 1.1));
                }
            }
            if (!flashed && t >= CL1)
            {
                flashed = true;
                lights.Add(0, -24, 64, C[5], 0.7, 0.5);
                lights.Add(0, -30, 28, WHITE, 0.7, 0.1);
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(30, 110);
                    ps.Spawn(P.Star, Px.Cos(a) * 6, -24 + Px.Sin(a) * 12, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.7 - 20, R.Range(0.4, 0.9), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 2));
                }
            }
            // motes spiral in to make the halo
            if (t > H0 - 0.2 && t < H0 + 0.1 && R.Chance(0.6))
            {
                double g = R.Range(0, TAU), D = R.Range(14, 24), x = HX + Px.Cos(g) * D, y = HY + Px.Sin(g) * D * 0.5, tx = HX + Px.Cos(g + 0.6) * HRX, ty = HY + Px.Sin(g + 0.6) * HRY;
                double T = R.Range(0.16, 0.24), l = Hypot(tx - x, ty - y);
                if (l == 0 || double.IsNaN(l)) l = 1;
                double v = (l * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, x, y, ((tx - x) / l) * v, ((ty - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!formed && t >= H1)
            {
                formed = true;
                lights.Add(HX, HY, 30, C[6], 0.8, 0.3);
            }
            // the wings burst open with a scatter of light
            if (t > W0 && t < W0 + 0.35 && R.Chance(0.5))
                foreach (int side in SIDES)
                {
                    WF f = Pick(R, Wing(t, side).output);
                    if (f != null) ps.Spawn(P.Star, f.x + Px.Cos(f.ang) * f.len, f.y + Px.Sin(f.ang) * f.len, side * R.Range(5, 25), -R.Range(5, 25), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.6));
                }
            // while the wings hold, light twinkles on the tips and motes lift off them
            if (t > W0 + 0.45 && t < SC)
                foreach (int side in SIDES)
                {
                    if (R.Chance(0.045))
                    {
                        WF f = Pick(R, Wing(t, side).output.Where(w => w.q.kind == 0).ToList());
                        if (f != null) ps.Spawn(P.Star, f.x + Px.Cos(f.ang) * (f.len - 2), f.y + Px.Sin(f.ang) * (f.len - 2), 0, -R.Range(2, 8), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.4));
                    }
                    if (R.Chance(0.03))
                    {
                        WF f = Pick(R, Wing(t, side).output);
                        if (f != null) ps.Spawn(P.Ember, f.x + Px.Cos(f.ang) * f.len * 0.7, f.y + Px.Sin(f.ang) * f.len * 0.7, R.Range(-8, 8), -R.Range(8, 25), R.Range(0.6, 1.1), C[5]);
                    }
                }
            // the downstroke: a gust along the floor, light flung off the tips, a few feathers shaken loose
            if (!beaten && t >= B2 - 0.03)
            {
                beaten = true;
                foreach (int side in SIDES)
                {
                    lights.Add(side * 30, -34, 46, C[5], 0.6, 0.4);
                    // the gust skims out along the floor as streaks of lit dust
                    for (int i = 0; i < 7; i++) ps.Spawn(P.Mote, side * R.Range(8, 22), -R.Range(1, 4), side * R.Range(90, 160), -R.Range(0, 8), R.Range(0.25, 0.45), R.Chance(0.5) ? C[6] : C[5], R.Range(0.6, 1));
                    foreach (WF f in Wing(t, side).output)
                        if (f.q.kind == 0)
                        {
                            double tx = f.x + Px.Cos(f.ang) * f.len, ty = f.y + Px.Sin(f.ang) * f.len;
                            ps.Spawn(P.Spark, tx, ty, side * R.Range(30, 90), R.Range(60, 160), R.Range(0.3, 0.6));
                        }
                    for (int i = 0; i < 3; i++)
                    {
                        WF f = Pick(R, Wing(t, side).output.Where(w => w.q.kind == 1).ToList());
                        if (f != null)
                            loose.Add(new Feathers.Spec
                            {
                                x = f.x + Px.Cos(f.ang) * f.len * 0.6, y = f.y + Px.Sin(f.ang) * f.len * 0.6, vx = side * R.Range(20, 70), vy = R.Range(10, 50), a = f.ang,
                                len = R.Range(9, 12), wid = R.Range(2.2, 2.8), life = R.Range(1.2, 1.6), side = side,
                            });
                    }
                }
            }
            // the wings come apart, tips first: each feather flashes and breaks into small ones and motes
            foreach (int side in SIDES)
            {
                int si = side > 0 ? 0 : 1;
                foreach (Quill q in quills)
                {
                    if (q.gone[si] || t < q.off[si]) continue;
                    q.gone[si] = true;
                    WF f = Wing(q.off[si] - 0.001, side).output.FirstOrDefault(w => w.q == q);
                    if (f == null) continue;
                    double ca = Px.Cos(f.ang), sa = Px.Sin(f.ang);
                    int n = q.kind == 2 ? 0 : 1;
                    for (int j = 0; j < n; j++)
                    {
                        double u = 0.55, x = f.x + ca * f.len * u, y = f.y + sa * f.len * u, ox = x - side * 4, oy = y + 20, l = Hypot(ox, oy);
                        if (l == 0 || double.IsNaN(l)) l = 1;
                        double sp = R.Range(60, 150);
                        loose.Add(new Feathers.Spec
                        {
                            x = x, y = y, vx = (ox / l) * sp + R.Range(-15, 15), vy = (oy / l) * sp * 0.5 - R.Range(20, 50), a = f.ang + R.Range(-0.4, 0.4),
                            len = R.Range(9, 12), wid = R.Range(2.2, 2.8), life = R.Range(1, 1.45), side = side, back = R.Chance(0.35),
                        });
                    }
                    for (int j = 0; j < 2; j++) ps.Spawn(P.Ember, f.x + ca * f.len * R.Range(0.3, 1), f.y + sa * f.len * R.Range(0.3, 1), R.Range(-15, 15), -R.Range(10, 40), R.Range(0.6, 1.1), C[5]);
                    if (R.Chance(0.6)) ps.Spawn(P.Star, f.x + ca * f.len, f.y + sa * f.len, 0, -R.Range(5, 15), R.Range(0.3, 0.6), WHITE, R.Range(1, 2));
                }
            }
            // the halo melts into rising motes
            if (t > 3.35 && t < 3.75 && R.Chance(0.4))
            {
                double g = R.Range(0, TAU);
                ps.Spawn(P.Ember, HX + Px.Cos(g) * HRX, HY + Px.Sin(g) * HRY, R.Range(-6, 6), -R.Range(10, 25), R.Range(0.5, 0.9), C[5]);
            }
            ps.Update(dt);
            lights.Update(dt);
            loose.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            Shaft(p);
            Pool(p, true);
            if (t > POUR && t < POUR + 0.4) Sim.Band(p, 0, 0, 4 + 32 * Ease.OutCubic(Seg(t, POUR, POUR + 0.4)), 0.18, 2.5 * (1 - Seg(t, POUR, POUR + 0.4)) + 0.5, 1.1 - Seg(t, POUR, POUR + 0.4), Slice(C, 3), FAR, t < POUR + 0.06);
            foreach (int side in SIDES) DrawWing(p, side);
            loose.Draw(p, WING, "back");
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            Pool(p, false);
            if (t > POUR && t < POUR + 0.4) Sim.Band(p, 0, 0, 4 + 32 * Ease.OutCubic(Seg(t, POUR, POUR + 0.4)), 0.18, 2.5 * (1 - Seg(t, POUR, POUR + 0.4)) + 0.5, 1.1 - Seg(t, POUR, POUR + 0.4), Slice(C, 3), NEAR, t < POUR + 0.06);
            Body(p);
            // the downstroke sends a ripple of light out along the floor
            if (t > B2 - 0.03 && t < B2 + 0.45)
            {
                double u = Seg(t, B2 - 0.03, B2 + 0.45), fx = 10 + 70 * Ease.OutCubic(u);
                foreach (int s in SIDES)
                    for (int d = 0; d < 12; d++)
                        p.Dot(s * (fx - d), 0, d < 2 ? WHITE : d < 5 ? C[6] : C[4], (1 - u) * (1 - d / 12.0) * 1.3);
            }
            // the wings light the character from behind
            double wk = Smooth(W0, W0 + 0.2, t) * (1 - Smooth(SC, SC + 0.3, t));
            if (wk > 0)
            {
                foreach (int[] q in S.Out)
                    if (q[1] < -8) p.Light(q[0], q[1], C[5], 0.55 * wk);
                p.Glow(0, -34, 76, C[4], 0.14 * wk);
            }
            DrawHalo(p);
            if (t >= CL1 && t < CL1 + 0.22) Sim.Flare(p, 0, S.Top - 1, 22 * (1 - (t - CL1) / 0.22), 1 - (t - CL1) / 0.22, Slice(C, 3));
            loose.Draw(p, WING, "front");
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
