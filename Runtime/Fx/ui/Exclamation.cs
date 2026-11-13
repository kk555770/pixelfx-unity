using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Exclamation : Effect
    {
        const double DURATION = 1.4;

        static readonly Pose STARTLED = new Pose { ["turn"] = 1, ["y"] = -5, ["crouch"] = -1, ["fx"] = -14, ["fy"] = -34, ["bx"] = 15, ["by"] = -34, ["ffx"] = -3, ["ffy"] = -6, ["fbx"] = 4, ["fby"] = -6 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(-0.08, Poses.READY),
                new PoseKey(0, STARTLED, "snap"),
                new PoseKey(0.14, new Pose(STARTLED) { ["y"] = 0, ["crouch"] = 4, ["ffy"] = -4, ["fby"] = -4, ["fy"] = -26, ["by"] = -26 }, "in"),
                new PoseKey(0.3, new Pose { ["turn"] = 1, ["fx"] = -10, ["fy"] = -23, ["bx"] = 11, ["by"] = -23, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(1.1, new Pose { ["turn"] = 1, ["fx"] = -10, ["fy"] = -23, ["bx"] = 11, ["by"] = -23, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(1.4, Poses.READY),
            },
            Dust = new[] { new DustCue { T = 0.14, X = 0, N = 4 } },
        };

        public static readonly Module Module = new Module("exclamation", "pixelfx/fx/ui/exclamation", 84, 104, 42, 92, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1405", "#6a2e06", "#b85a0a", "#f08c14", "#ffc23a", "#ffe580", "#fff6c8", "#ffffff" },
                ["seed"] = 3,
            },
            (o, e) => new Exclamation(o, e), act: Act, actor: 2)
        {
            Title = "Exclamation",
            Desc = "A bold ! springs up off the hero's head, stretching as it shoots up and squashing as it lands; surprise ticks flick out, it wobbles, then pops away.",
            Tags = new[] { "ui", "alert", "surprise", "emote", "character", "one-shot" },
            Bg = "ground",
        };

        const double HEAD = -40, REST = -45, POP = 1.1;

        // the mark: a bar that tapers to its foot and a round dot, with a dark outline, lit down its left side,
        // turned by a about its foot
        static void Bang(Painter p, uint[] C, double x, double y, double sx, double sy, double a, bool white)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a);
            double Hw(double V) => 2.3 + (-V - 10) * 0.09;
            bool InBar(double U, double V) => V > -24 && V < -9.5 && Math.Abs(U) < Hw(V);
            bool InDot(double U, double V) => Hypot(U, V + 3.6) < 3.3;
            bool Inside(double U, double V) => InBar(U, V) || InDot(U, V);
            p.Shade(x - 16, y - 34, x + 16, y + 6, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, u = (dx * ca + dy * sa) / sx, v = (-dx * sa + dy * ca) / sy, ex = 1 / sx, ey = 1 / sy;
                if (!Inside(u, v)) return Inside(u - ex, v) || Inside(u + ex, v) || Inside(u, v - ey) || Inside(u, v + ey) ? C[0] : 0;
                if (white) return WHITE;
                if (InDot(u, v)) return Hypot(u + 1.2, v + 4.8) < 1.2 ? WHITE : u + v + 3.6 < -0.5 ? C[6] : u + v + 3.6 < 1.5 ? C[5] : C[4];
                double w = Hw(v), q = (u + w) / (2 * w);
                if (q < 0.28) return v < -20 ? WHITE : C[7];
                if (q > 0.74) return C[4];
                return v < -18 ? C[6] : v < -13 ? C[5] : C[5];
            });
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        // the mark's foot: shot up off the head with a spring, so it overshoots and settles
        double my = HEAD, mvy = -420, msq = 0;
        bool landed;
        double t;
        bool popped, sprung;
        const double END = DURATION - 0.05;

        Exclamation(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < POP)
            {
                // a stiff spring pulls it to its rest above the head
                mvy += (-(my - REST) * 900 - mvy * 14) * dt;
                my += mvy * dt;
                if (!landed && mvy > 0)
                {
                    landed = true;
                    msq = 1;
                }
            }
            msq = Math.Max(0, msq - dt / 0.12);
            // it leaves the head with a little burst of light
            if (!sprung && t >= 0.04)
            {
                sprung = true;
                lights.Add(0, HEAD - 4, 26, C[5], 0.6, 0.25);
                for (int i = 0; i < 8; i++)
                {
                    double an = -Math.PI / 2 + R.Range(-1.3, 1.3), sp = R.Range(40, 90);
                    ps.Spawn(P.Star, R.Range(-4, 4), HEAD - 2, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.2, 0.4), R.Pick(new[] { C[7], C[6], C[5] }), i % 3 != 0 ? 0.6 : 1.3);
                }
            }
            if (!popped && t >= POP + 0.05)
            {
                popped = true;
                lights.Add(0, REST - 13, 28, C[5], 0.5, 0.2);
                for (int i = 0; i < 7; i++)
                {
                    double an = ((double)i / 7) * TAU + R.Range(-0.2, 0.2), sp = R.Range(30, 60);
                    ps.Spawn(P.Star, Px.Cos(an) * 5, REST - 13 + Px.Sin(an) * 9, Px.Cos(an) * sp, Px.Sin(an) * sp, Math.Min(R.Range(0.2, 0.35), END - t), R.Pick(new[] { C[7], C[6], C[5] }), i % 3 != 0 ? 0.6 : 1.4);
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            double k = t;
            // surprise ticks: three flick out each side on the spring, and again on a second beat
            foreach (var (t0, len) in new[] { (0.04, 6.0), (0.48, 4.5) })
            {
                double u = (k - t0) / 0.24;
                if (u <= 0 || u >= 1) continue;
                foreach (int s in new[] { -1, 1 })
                    for (int i = 0; i < 3; i++)
                    {
                        double an = -Math.PI / 2 + s * (0.55 + i * 0.42), r0 = 14 + 7 * Ease.OutCubic(u), L = len * Px.Sin(u * Math.PI) * (i == 1 ? 1 : 0.75);
                        double cx = 0, cy = REST - 13, ca = Px.Cos(an), sa = Px.Sin(an);
                        if (L < 0.8) continue;
                        p.Line(cx + ca * r0, cy + sa * r0, cx + ca * (r0 + L), cy + sa * (r0 + L), q => q < 0.5 ? WHITE : C[5]);
                    }
            }
            // the mark: stretched while it shoots up, squashed as it lands, wobbling, white on the pop
            double pu = (k - POP) / 0.06;
            if (pu < 1)
            {
                double speed = Math.Min(1, Math.Abs(mvy) / 420), up = mvy < 0 ? speed : 0;
                double sx = 1 - 0.3 * up + 0.3 * msq, sy = 1 + 0.45 * up - 0.28 * msq;
                if (k < 0.05)
                {
                    sx *= k / 0.05;
                    sy *= k / 0.05;
                }
                if (pu > 0) sx = sy = 1 + 0.3 * pu;
                double wob = 0.28 * Exp(-k * 4) * Px.Sin(k * 26);
                bool white = k < 0.06 || pu > 0;
                if (sx > 0.1) Bang(p, C, 0, my, sx, sy, wob, white);
                p.Glow(0, my - 13, 20, C[5], 0.35 + (white ? 0.3 : 0));
                if (k > 0.6 && k < 0.85) Sim.Sparkle(p, -3, my - 22, 3.5 * Px.Sin(((k - 0.6) / 0.25) * Math.PI), C[7], C[6], 0.4);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
