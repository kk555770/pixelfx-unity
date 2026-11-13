using System;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Slowed : Effect
    {
        static readonly Pose S1 = new Pose { ["turn"] = 0.3, ["lean"] = 3, ["crouch"] = 3, ["ffx"] = 11, ["ffy"] = -5, ["fbx"] = -9, ["fby"] = -5, ["toeB"] = true, ["fx"] = -5, ["fy"] = -19, ["bx"] = 11, ["by"] = -26 };
        static readonly Pose S2 = new Pose { ["turn"] = 0.3, ["lean"] = 3, ["crouch"] = 3, ["ffx"] = -9, ["ffy"] = -5, ["toeF"] = true, ["fbx"] = 11, ["fby"] = -5, ["fx"] = 13, ["fy"] = -26, ["bx"] = -4, ["by"] = -19 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[] { new PoseKey(0, S1), new PoseKey(1.2, S2), new PoseKey(2.4, S1) },
        };

        public static readonly Module Module = new Module("slowed", "pixelfx/fx/status/slowed", 64, 76, 32, 68, 2.4, true, 30,
            new Options { ["palette"] = new[] { "#081a24", "#0e3446", "#165a6e", "#2a8a9a", "#52bcc4", "#9ee6e0", "#e2fff8", "#ffffff" }, ["seed"] = 7 },
            (o, e) => new Slowed(o), act: Act, actor: 2)
        {
            Title = "Slowed",
            Desc = "Dragged through time: a glassy clock dial behind the head ticks round in heavy steps, flashing with each, while drops sink in slow motion and spread slow rings.",
            Tags = new[] { "status", "slow", "time", "clock", "debuff", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, TICKS = 4, STEP = D / TICKS, R = 18;

        // the hand's angle: it waits, pulls back a little, snaps a quarter turn, overshoots and settles
        static (double ang, double flash, double ph) Hand(double t)
        {
            double k = Math.Floor(t / STEP), ph = (t - k * STEP) / STEP, bas = -Math.PI / 2 + (k * TAU) / TICKS, q = TAU / TICKS;
            if (ph < 0.62) return (bas, 0, ph);
            if (ph < 0.72) return (bas - 0.1 * Px.Sin(((ph - 0.62) / 0.1) * Math.PI * 0.5), 0, ph);
            double s = (ph - 0.72) / 0.28, snap = Ease.OutBack(Clamp(s / 0.35), 2.2), wob = Exp(-s * 6) * Px.Sin(s * 22) * 0.05;
            return (bas - 0.1 * (1 - Clamp(s / 0.2)) + q * snap + wob, Exp(-s * 5), ph);
        }

        // the flash of the last tick, still fading after the hand has settled
        static double Afterglow(double t)
        {
            double s = (((t - STEP * 0.72) % STEP) + STEP) % STEP;
            return Exp(-s * 4);
        }

        static void Dial(Painter p, uint[] C, double cx, double cy, double t)
        {
            var h = Hand(t);
            double fl = Math.Max(h.flash, Afterglow(t));
            // a glassy face, lit from the top left
            p.Disc(cx, cy, R - 1, C[2], 0.2);
            p.Disc(cx - R * 0.25, cy - R * 0.3, R * 0.5, C[4], 0.08);
            // the rim: a dark edge, a bright band, a thin inner line
            p.Ring(cx, cy, R + 1, 1, C[0]);
            p.Ring(cx, cy, R, 2, (v, ang) => Px.Sin(ang + 0.8) < -0.3 ? (fl > 0.4 ? C[7] : C[6]) : v > 0.5 ? C[5] : C[4]);
            p.Ring(cx, cy, R - 3.5, 1, C[3], 0.6);
            // the minute dots, then the hour marks, the quarters longer and brighter
            for (int i = 0; i < 60; i++)
            {
                if (i % 5 == 0) continue;
                double a = (i / 60.0) * TAU;
                p.Dot(cx + Px.Cos(a) * (R - 2.6), cy + Px.Sin(a) * (R - 2.6), C[4], 0.8);
            }
            for (int i = 0; i < 12; i++)
            {
                double a = (i / 12.0) * TAU;
                bool big = i % 3 == 0;
                double r0 = R - 2.5, r1 = R - (big ? 6 : 4.5), c = Px.Cos(a), s = Px.Sin(a);
                p.Line(cx + c * r0, cy + s * r0, cx + c * r1, cy + s * r1, big ? C[6] : C[5]);
            }
            // the hands: a short one that only shudders, a long heavy one that ticks
            double shud = h.flash * Px.Sin(t * 60) * 0.06, ha = -Math.PI / 2 + 2.2 + shud;
            p.Beam(cx, cy, cx + Px.Cos(ha) * R * 0.5, cy + Px.Sin(ha) * R * 0.5, 2.2, C[5]);
            double tx = cx + Px.Cos(h.ang) * (R - 4), ty = cy + Px.Sin(h.ang) * (R - 4);
            p.Beam(cx, cy, tx, ty, 2.4, (u, v) => u > 0.8 ? C[7] : C[6]);
            p.Circle(cx, cy, 2, C[6]);
            p.Px(cx, cy, C[1]);
            // light: the face glows, the rim flares as each tick lands, a ripple runs out from it
            p.Glow(cx, cy, R + 8, C[4], 0.14 + 0.2 * fl);
            p.GlowRing(cx, cy, R, 3, C[5], 0.12 + 0.35 * fl);
            p.Glow(tx, ty, 5, C[6], 0.3 + 0.3 * h.flash);
            if (h.flash > 0.02)
            {
                double s = 1 - h.flash;
                p.Ring(cx, cy, R + 2 + 10 * s, 1, C[5], h.flash);
                p.GlowRing(cx, cy, R + 2 + 10 * s, 3, C[4], 0.3 * h.flash);
            }
        }

        readonly Options o;

        Slowed(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor a = o.Actor;
            Dial(p, C, 0, a.Y + 3, t);
            // the body lags behind itself: an after-image of its outline, dragged a step back after every tick
            double lag = Afterglow(t);
            if (lag > 0.05) p.Outline(a.Mask, a.X - 1 - Round(2 * lag), a.Y, C[5], 0.55 * lag);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            Actor a = o.Actor;
            StatusShape S = Shape(a);
            double fl = Math.Max(Hand(t).flash, Afterglow(t));
            // a cold film of time over the body, thickening with each tick
            foreach (int[] b in S.Body) p.Blend(a.X + b[0], a.Y + b[1], C[5], 0.08 + 0.12 * fl);
            // drops sinking in slow motion, ghosts trailing them, slow rings where they reach the floor
            Cycle(t, D, 6, o.Seed + 3, r => r.Range(1.6, 2.2), (age, life, r, i) =>
            {
                double side = r.Next() < 0.5 ? -1 : 1, x = side * r.Range(10, 17), y0 = a.Y + r.Range(6, 22), v0 = r.Range(18, 30), k = 1.1;
                double YAt(double g) => y0 + (v0 * (1 - Exp(-k * g))) / k + 4 * g;
                double y = YAt(age), u = age / life;
                if (y < -1)
                {
                    foreach (double[] gh in new[] { new[] { 0.22, 0.35 }, new[] { 0.11, 0.6 } })
                        if (age > gh[0]) p.Dot(x, YAt(age - gh[0]), C[4], gh[1]);
                    p.Px(x, y - 1, C[5]);
                    p.Px(x, y, C[6]);
                    p.Px(x, y + 1, C[5]);
                    p.Px(x - 1, y + 1, C[4]);
                    p.Px(x - 1, y, C[7]);
                    p.Glow(x, y, 4, C[5], 0.3 * (1 - u));
                }
                else
                {
                    // it reached the floor: a slow ring spreads and fades
                    double g = age;
                    while (g > 0 && YAt(g) >= -1) g -= 0.02;
                    double s = Clamp((age - g) / 0.9);
                    if (s < 1)
                    {
                        // two crests running apart along the floor, a faint far edge behind them
                        double rx = 1 + 7 * Math.Sqrt(s);
                        uint c = s < 0.3 ? C[6] : C[5];
                        foreach (int sd in new[] { -1, 1 }) p.Line(x + sd * rx, -0.5, x + sd * (rx - 1.5), -0.5, c, (1 - s) * 1.2);
                        for (double dx = -rx + 2; dx <= rx - 2; dx += 1) p.Dot(x + dx, -1.5, C[4], (1 - s) * 0.5 * (1 - Math.Abs(dx) / rx));
                        p.Glow(x, -1, 4 + rx, C[4], 0.12 * (1 - s));
                    }
                }
            });
        }
    }
}
