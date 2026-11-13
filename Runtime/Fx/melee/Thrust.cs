// The speed lines are adapted from Sprite Fusion's open-source dash and the ray star from his sparkle
// (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Thrust : Effect
    {
        static readonly Pose STANCE = new Pose(Poses.READY) { ["weapon"] = "sword", ["x"] = -14, ["fx"] = 12, ["fy"] = -21, ["sa"] = -0.3 };
        // the hand comes back to the hip with the point forward, then the whole arm goes out along the blade
        static readonly Pose BACK = new Pose(STANCE) { ["crouch"] = 5, ["lean"] = -3, ["fx"] = -3, ["fy"] = -19, ["sa"] = -0.05, ["bx"] = 8, ["by"] = -30, ["armsFront"] = true, ["ffx"] = 10, ["fbx"] = -8 };
        static readonly Pose LUNGE = new Pose(STANCE) { ["crouch"] = 6, ["lean"] = 6, ["fx"] = 23, ["fy"] = -20, ["sa"] = 0, ["bx"] = -8, ["by"] = -24, ["ffx"] = 15, ["fbx"] = -11, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 80 },
            Poses = new[]
            {
                new PoseKey(-0.46, STANCE),
                new PoseKey(-0.24, BACK),
                new PoseKey(-0.05, new Pose(BACK) { ["crouch"] = 6, ["fx"] = -5 }),
                new PoseKey(0, new Pose(LUNGE) { ["ghost"] = 1 }, "snap"),
                new PoseKey(0.07, LUNGE, "snap"),
                new PoseKey(0.3, new Pose(LUNGE) { ["crouch"] = 5, ["lean"] = 5 }),
                new PoseKey(0.7, STANCE),
            },
            Hits = new[] { new HitCue { T = 0.07, Stop = 0.12, Shake = 0.14, Amp = 2, Push = 2.8, Straw = 16, Y = -26 } },
            Dust = new[] { new DustCue { T = 0, X = -24, N = 6, Dir = -1 }, new DustCue { T = 0.07, X = 1, N = 6, Dir = 1 } },
        };

        public static readonly Module Module = new Module("thrust", "pixelfx/fx/melee/thrust", 196, 76, 34, 62, 0.75, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1608", "#6b3c10", "#b8701a", "#f0b42e", "#ffe27a", "#fff6cf", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["reach"] = 66,
                ["seed"] = 4,
            },
            (o, e) => new Thrust(o, e), act: Act, actor: 2)
        {
            Title = "Thrust",
            Desc = "A piercing stab: a bundle of streaks shoots out from the hand to a point, a long four-point star flares at the tip with a pierce ring, sparks and speed lines.",
            Tags = new[] { "melee", "thrust", "stab", "spear", "rapier", "pierce", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        const double OUT = 0.07; // the point arrives
        const double HOLD = 0.06, PULL = 0.2;
        const double HX = 9, HY = -20;

        static readonly double[][] Arms4 = { new double[] { 1, 1 }, new double[] { 1, -1 }, new double[] { -1, 1 }, new double[] { -1, -1 } };

        // a long four-point star: the forward and back arms long, up and down shorter, thick at the heart
        static void LongStar(Painter p, double x, double y, double L, double k, uint[] C)
        {
            if (L < 1) return;
            double[][] arms = { new[] { 1, 0, L }, new[] { -1, 0, L * 0.7 }, new[] { 0, -1, L * 0.42 }, new[] { 0, 1, L * 0.42 } };
            foreach (double[] arm in arms)
            {
                double dx = arm[0], dy = arm[1], len = arm[2];
                for (double d = 1; d <= len; d += 0.5)
                {
                    double u = d / len;
                    uint c = u < 0.3 ? WHITE : u < 0.65 ? C[5] : C[4];
                    p.Dot(x + dx * d, y + dy * d, c, u < 0.7 ? 1 : 1 - (u - 0.7) / 0.35);
                    if (u < 0.25 && len > 6)
                    {
                        p.Dot(x + dx * d - dy, y + dy * d + dx, C[5]);
                        p.Dot(x + dx * d + dy, y + dy * d - dx, C[5]);
                    }
                }
            }
            foreach (double[] dd in Arms4)
                for (double d = 1; d <= Math.Min(3, L * 0.14); d++) p.Px(x + dd[0] * d, y + dd[1] * d, d < 2 ? C[5] : C[4]);
            p.Px(x, y, WHITE);
            p.Glow(x, y, 6 + L * 1.3, C[4], 0.35 * k);
            p.Glow(x, y, 3 + L * 0.4, WHITE, 0.3 * k);
        }

        sealed class Streak
        {
            public double off, x0, t0, w, len;
            public bool hot;
        }

        sealed class SpeedLine
        {
            public double x, y, l, v, t;
        }

        readonly uint[] C;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly double reach, TX, TY;
        readonly Streak[] streaks;
        readonly List<SpeedLine> lines = new List<SpeedLine>();
        double t;
        bool struck;

        Thrust(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            reach = o.Num("reach");
            TX = HX + reach;
            TY = HY;
            // the bundle: streaks fanned at the hand, meeting at the point; the middle one hottest
            double[] offs = { -7, -4, -2, 0, 2, 4, 7 };
            streaks = new Streak[offs.Length];
            for (int i = 0; i < offs.Length; i++)
            {
                double off = offs[i];
                streaks[i] = new Streak { off = off, x0 = HX + 1 + Rg.Range(0, 5) + Math.Abs(off) * 0.6, t0 = Rg.Range(0, 0.02) + Math.Abs(off) * 0.003, w = off == 0 ? 2 : 1, hot = off == 0, len = Rg.Range(0.75, 1) };
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // speed lines race forward beside the bundle while it shoots out and just after
            if (t < OUT + 0.08 && Rg.Chance(0.5))
            {
                double s = Rg.Sign();
                lines.Add(new SpeedLine { x = HX + Rg.Range(0, reach * 0.6), y = HY + s * Rg.Range(7, 17), l = Rg.Range(18, 42), v = Rg.Range(200, 340), t = t });
            }
            for (int i = lines.Count - 1; i >= 0; i--)
                if (t - lines[i].t > 0.24) lines.RemoveAt(i);
            foreach (SpeedLine l in lines) l.x += l.v * dt;
            if (!struck && t >= OUT)
            {
                struck = true;
                lights.Add(TX, TY, 44, C[4], 0.45, 0.3);
                lights.Add(TX, TY, 14, WHITE, 0.6, 0.06);
                for (int i = 0; i < 12; i++)
                {
                    double a = Rg.Range(-0.55, 0.45), sp = Rg.Range(150, 330);
                    ps.Spawn(P.Spark, TX, TY, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, Rg.Range(0.12, 0.35));
                }
                for (int i = 0; i < 10; i++)
                {
                    double a = Rg.Range(-0.9, 0.9) + (Rg.Chance(0.3) ? Math.PI : 0), sp = Rg.Range(40, 120);
                    ps.Spawn(P.Mote, TX + Rg.Range(-6, 2), TY + Rg.Range(-2, 2), Px.Cos(a) * sp, Px.Sin(a) * sp, Rg.Range(0.25, 0.5), Rg.Chance(0.5) ? C[5] : C[4], 0.6);
                }
                for (int i = 0; i < 4; i++) ps.Spawn(P.Star, TX + Rg.Range(-10, 8), TY + Rg.Range(-9, 9), 0, -3, Rg.Range(0.2, 0.4), Rg.Chance(0.5) ? C[5] : WHITE, Rg.Range(1, 2), Rg.Range(0.06, 0.2));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            // the point catches the light before it goes
            if (t < 0.05) Sim.Sparkle(p, HX + 2, HY, 5 * (1 - t / 0.05) + 1, C[5], C[3], 0.4);
            // speed lines: dotted, white at the head, dimming back along their length
            foreach (SpeedLine l in lines)
            {
                double k = (t - l.t) / 0.24;
                for (double d = 0; d < l.l * (1 - k * 0.6); d++) p.Dot(l.x - d, l.y, d < 3 ? WHITE : d < l.l * 0.4 ? C[5] : C[3], (1 - d / l.l) * (1 - k));
            }
            // the bundle: each streak shoots from the hand to the point, holds, then is drawn in to the point
            double pull = Smooth(OUT + HOLD, OUT + HOLD + PULL, t);
            foreach (Streak s in streaks)
            {
                double u = Clamp((t - s.t0) / OUT), head = Ease.OutExpo(u);
                if (u <= 0 || pull >= 1) continue;
                // the outer streaks stop short of the point and trail a little behind, so each reads on its own
                double y0 = HY + s.off, end = s.x0 + (TX - s.x0) * s.len, x1 = s.x0 + (end - s.x0) * head, xa = s.x0 + (end - s.x0) * Math.Max(pull, head * 0.35 * (1 - s.len) * 3);
                Func<double, double> yAt = x => y0 + (TY - y0) * Clamp((x - s.x0) / (TX - s.x0)) * 0.85;
                for (double x = xa; x <= x1; x += 0.5)
                {
                    double v = (x - xa) / Math.Max(1, x1 - xa);
                    uint c = v > 0.88 ? WHITE : s.hot ? (v > 0.35 ? C[6] : C[5]) : v > 0.55 ? C[5] : v > 0.25 ? C[4] : C[3];
                    p.Dot(x, yAt(x), c, v < 0.12 ? v / 0.12 : 1);
                    if (s.w > 1 && v > 0.1) p.Dot(x, yAt(x) + 1, v > 0.6 ? C[5] : C[3], v < 0.2 ? v / 0.2 : 1);
                }
            }
            if (t < OUT + HOLD + PULL) p.GlowLine(HX + (TX - HX) * pull, HY, HX + (TX - HX) * Ease.OutExpo(Clamp(t / OUT)), TY, 6, C[4], 0.22 * (1 - pull));
            // the long star at the point, and a pierce ring shed from it
            double q = t - OUT;
            if (q >= 0 && q < 0.4)
            {
                double g = Ease.OutBack(Clamp(q / 0.04), 2.4) * (1 - Smooth(0.08, 0.4, q)) * (1 + 0.12 * Px.Sin(q * 70));
                LongStar(p, TX, TY, 24 * g, 1 - q / 0.4, C);
            }
            if (q >= 0 && q < 0.2)
            {
                double k = q / 0.2, rx = 2 + 7 * Ease.OutCubic(k), ry = 4 + 12 * Ease.OutCubic(k);
                for (double a = 0; a < TAU; a += 0.05) p.Dot(TX + 4 + 10 * k + Px.Cos(a) * rx, TY + Px.Sin(a) * ry, k < 0.3 ? WHITE : C[5], 1.15 - k);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
