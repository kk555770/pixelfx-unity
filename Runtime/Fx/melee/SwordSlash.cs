// The crescent, echo, trail, glint and spark recipe is adapted from Sprite Fusion's open-source slash
// (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class SwordSlash : Effect
    {
        static readonly Pose GUARD = new Pose(Poses.READY) { ["weapon"] = "sword", ["fx"] = 12, ["fy"] = -21, ["sa"] = -0.85 };
        // the blade goes back past his head, then lags behind the hand on the way through, so the wrist breaks and it whips
        static readonly Pose WIND = new Pose(GUARD) { ["x"] = -8, ["crouch"] = 4, ["lean"] = -4, ["fx"] = -7, ["fy"] = -35, ["sa"] = -2.65, ["bx"] = 9, ["by"] = -25, ["armsFront"] = true, ["ffx"] = 9, ["fbx"] = -8 };
        static readonly Pose CUT = new Pose(GUARD) { ["x"] = 0, ["crouch"] = 2, ["lean"] = 3, ["fx"] = 16, ["fy"] = -37, ["sa"] = -1.45, ["bx"] = -7, ["by"] = -24, ["ffx"] = 11, ["fbx"] = -10, ["ghost"] = 1 };
        static readonly Pose DOWN = new Pose(GUARD) { ["x"] = 2, ["crouch"] = 5, ["lean"] = 5, ["fx"] = 20, ["fy"] = -21, ["sa"] = 0.62, ["bx"] = -9, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 58 },
            Poses = new[]
            {
                new PoseKey(-0.55, new Pose(GUARD) { ["x"] = -8 }),
                new PoseKey(-0.3, WIND),
                new PoseKey(-0.08, new Pose(WIND) { ["crouch"] = 5, ["lean"] = -5 }),
                new PoseKey(0, CUT, "snap"),
                new PoseKey(0.06, DOWN, "snap"),
                new PoseKey(0.3, new Pose(DOWN) { ["x"] = 3, ["crouch"] = 6 }),
                new PoseKey(0.58, new Pose(GUARD) { ["x"] = 1, ["crouch"] = 3, ["fx"] = 15, ["fy"] = -21, ["sa"] = 0.1, ["bx"] = -5, ["by"] = -21, ["ffx"] = 9, ["fbx"] = -7 }),
                new PoseKey(0.86, new Pose(GUARD) { ["x"] = -8 }),
            },
            Hits = new[] { new HitCue { T = 0.06, Stop = 0.14, Shake = 0.18, Amp = 2, Push = 2.8, Straw = 18 } },
            Dust = new[] { new DustCue { T = 0.06, X = 16, N = 7, Dir = 1 }, new DustCue { T = 0.06, X = -11, N = 4, Dir = -1 } },
        };

        public static readonly Module Module = new Module("sword-slash", "pixelfx/fx/melee/sword-slash", 136, 104, 44, 92, 0.75, false, 30,
            new Options
            {
                ["palette"] = new[] { "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#b8c9e8", "#e8f1ff", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["radius"] = 42,
                ["seed"] = 2,
            },
            (o, e) => new SwordSlash(o, e), act: Act, actor: 2)
        {
            Title = "Sword slash",
            Desc = "A tilted crescent whips round in two frames, cools from white to deep blue as it trails off, glints at the tip and throws sparks.",
            Tags = new[] { "melee", "slash", "sword", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        sealed class Swing
        {
            public double cx, cy, R, sy, rot, a0, a1, W, t0, sweep, hold, trail;
            public uint[] ramp;

            public Swing With() => (Swing)MemberwiseClone();
        }

        // a point on the slash's tilted ellipse
        static (double x, double y) OnArc(Swing s, double r, double a)
        {
            double lx = Px.Cos(a) * r, ly = Px.Sin(a) * r * s.sy, c = Px.Cos(s.rot), n = Px.Sin(s.rot);
            return (s.cx + lx * c - ly * n, s.cy + lx * n + ly * c);
        }

        // the smear: thickest just behind the leading edge, tapering to a hair at the tail, white on the outer rim,
        // cooling down the ramp with age and towards the tail; the tail dissolves through the dither
        static void Crescent(Painter p, Swing s, double ta, double L, double W, double age)
        {
            if (Math.Abs(L) < 0.02 || W < 0.3) return;
            uint[] R = s.ramp;
            int n = R.Length;
            double dir = Sign(L), span = Math.Abs(L), c = Px.Cos(s.rot), sn = Px.Sin(s.rot), B = s.R + 2;
            p.Shade(s.cx - B, s.cy - B, s.cx + B, s.cy + B, (x, y, o) =>
            {
                double dx = x - s.cx, dy = y - s.cy, lx = dx * c + dy * sn, ly = (-dx * sn + dy * c) / s.sy, r = Hypot(lx, ly);
                if (r > s.R + 0.5 || r < s.R - W - 1) return 0;
                double u = (dir * (Atan2(ly, lx) - ta)) % TAU;
                if (u < 0) u += TAU;
                if (u > span) return 0;
                double v = u / span, w = W * Px.Pow(Px.Sin(Math.PI * Px.Pow(v, 2.2)), 0.6);
                if (w < 0.35) return 0;
                double q = (s.R - r) / w;
                if (q < 0 || q > 1) return 0;
                // brightest step at the rim while it's young, then darker steps inward, with age and towards the tail
                double ci = q < 0.22 && age < 0.5 ? 0 : Round(q * (n - 1) * 0.75 + age * 2.4 - v * 0.8 + 0.4);
                o.c = R[n - 1 - (int)Clamp(ci, 0, n - 1)];
                o.a = Math.Min(1, v / 0.4);
                return 0;
            });
            for (double u = 0; u <= span; u += 0.3)
            {
                var (x, y) = OnArc(s, s.R - W * 0.4, ta + dir * u);
                p.Glow(x, y, 5 + W, R[n - 3], 0.22 * (1 - age) * (u / span));
            }
        }

        static void DrawSlash(Painter p, Swing s, double t)
        {
            double u = t - s.t0;
            if (u < 0) return;
            double span = s.a1 - s.a0, head = Ease.OutExpo(Clamp(u / s.sweep)), tail = Smooth(s.sweep + s.hold, s.sweep + s.hold + s.trail, u);
            double age = Clamp(u / (s.sweep + s.hold + s.trail));
            if (tail < 1) Crescent(p, s, s.a0 + span * tail, span * (head - tail), s.W * (1 - tail * 0.55), age);
            // a dotted trace of the blade's path lingers where the smear has already gone
            double th = 0.7 * (1 - Clamp((u - s.sweep - s.hold) / (s.trail + 0.2)));
            if (tail > 0.02 && th > 0)
                for (double k = 0; k <= tail; k += 0.6 / (Math.Abs(span) * s.R))
                {
                    var (x, y) = OnArc(s, s.R - 0.5, s.a0 + span * k);
                    p.Dot(x, y, s.ramp[1], th * (0.4 + 0.6 * k));
                }
        }

        readonly uint[] C;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Swing A, E;
        readonly double a0, a1, tipX, tipY, tipDx, tipDy;
        double t;
        bool landed;

        SwordSlash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            double R0 = o.Num("radius");
            a0 = -2.55;
            a1 = 0.95;
            // the main smear, and a thinner echo just inside it a beat behind
            A = new Swing { cx = 6, cy = -26, R = R0, sy = 0.6, rot = -0.38, a0 = a0, a1 = a1, W = 12, t0 = 0, sweep = 0.06, hold = 0.06, trail = 0.24, ramp = C };
            E = A.With();
            E.R = R0 * (33.0 / 42);
            E.W = R0 * (4.5 / 42);
            E.a0 = a0 + (a1 - a0) * 0.1;
            E.a1 = a1 - (a1 - a0) * (0.15 / 3.5);
            E.t0 = 0.017;
            E.hold = 0.03;
            E.trail = 0.2;
            E.ramp = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(C, 6));
            var (x, y) = OnArc(A, A.R - A.W * 0.3, A.a1);
            var (x2, y2) = OnArc(A, A.R - A.W * 0.3, A.a1 + Sign(A.a1 - A.a0) * 0.05);
            double l = Hypot(x2 - x, y2 - y);
            if (l == 0 || double.IsNaN(l)) l = 1;
            tipX = x;
            tipY = y;
            tipDx = (x2 - x) / l;
            tipDy = (y2 - y) / l;
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!landed && t >= A.sweep)
            {
                landed = true;
                // sparks off the edge where it stops, and bright shavings flung off the arc
                for (int i = 0; i < 10; i++)
                {
                    double a = Atan2(tipDy, tipDx) + Rg.Range(-0.5, 0.5), sp = Rg.Range(150, 400);
                    ps.Spawn(P.Spark, tipX, tipY, Px.Cos(a) * sp, Px.Sin(a) * sp, Rg.Range(0.12, 0.35));
                }
                for (int i = 0; i < 16; i++)
                {
                    double a = a0 + (a1 - a0) * Rg.Range(0.3, 1);
                    var (x, y) = OnArc(A, A.R, a);
                    double ox = x - A.cx, oy = y - A.cy, l = Hypot(ox, oy), sp;
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    sp = Rg.Range(30, 100);
                    double life = Rg.Range(0.25, 0.55);
                    ps.Spawn(P.Mote, x, y, (ox / l) * sp, (oy / l) * sp - 10, life, C[4 + Rg.Int(0, 1)], 0.6);
                }
            }
            ps.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            DrawSlash(p, E, t);
            DrawSlash(p, A, t);
            // the tip glints as the edge arrives
            double g = t - A.sweep * 0.6;
            if (g > 0 && g < 0.22) Sim.Sparkle(p, tipX, tipY, 9 * Ease.OutBack(Math.Min(1, g / 0.05)) * (1 - g / 0.22), C[5], C[3], 0.6);
            // light rides the head of the swing
            if (t < 0.3)
            {
                var (x, y) = OnArc(A, A.R - 4, A.a0 + (A.a1 - A.a0) * Ease.OutExpo(Clamp(t / A.sweep)));
                p.Glow(x, y, 40, C[3], 0.5 * (1 - t / 0.3));
            }
            ps.Draw(p);
        }
    }
}
