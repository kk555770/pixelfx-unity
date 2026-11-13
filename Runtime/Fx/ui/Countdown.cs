using System;
using System.Globalization;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class Countdown : Effect
    {
        const double DURATION = 3.1;

        public static readonly Module Module = new Module("countdown", "pixelfx/fx/ui/countdown", 152, 136, 76, 68, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0a1030", "#142a66", "#1f4fae", "#2f86e6", "#5cc4ff", "#a8e8ff", "#e6fbff", "#ffffff" },
                ["go"] = new[] { "#0b2410", "#16501c", "#22882a", "#3cc23a", "#7ee84c", "#c4ff8a", "#f0ffd6", "#ffffff" },
                ["from"] = 3,
                ["word"] = "GO!",
                ["seed"] = 5,
            },
            (o, e) => new Countdown(o, e))
        {
            Title = "Countdown",
            Desc = "3, 2, 1 slam into a timer ring that drains like a burning fuse, throwing sparks; then GO! bursts out as the ring blows into a shockwave with rays.",
            Tags = new[] { "ui", "countdown", "start", "timer", "text", "one-shot" },
            Bg = "dark",
        };

        const double STEP = 0.62, R = 30, TOP = -Math.PI / 2;

        // the timer: a dark socket, ticks round it, and the lit arc from the burning head round to the top
        static double Timer(Painter p, uint[] C, double x, double y, double f, bool flash, double t)
        {
            p.Ring(x, y, R + 4, 8, (v, g) => v < 0.13 || v > 0.87 ? C[0] : C[1]);
            // a fine dotted dial inside the track, turning
            for (int j = 0; j < 40; j++)
            {
                double an = ((double)j / 40) * TAU + t * 0.6;
                p.Dot(x + Px.Cos(an) * (R - 6), y + Px.Sin(an) * (R - 6), j % 5 != 0 ? C[2] : C[4], 1);
            }
            double head = TOP + TAU * (1 - f);
            for (int i = 0; i < 12; i++)
            {
                double an = TOP + (i * TAU) / 12;
                bool lit = an >= head - 1e-6 || f >= 0.999, near = Math.Abs(an - head) < 0.2 && f < 0.999;
                double ca = Px.Cos(an), sa = Px.Sin(an);
                p.Line(x + ca * (R + 6), y + sa * (R + 6), x + ca * (R + 8), y + sa * (R + 8), flash || near ? WHITE : lit ? C[5] : C[2]);
            }
            if (f <= 0.002) return head;
            p.Arc(x, y, R + 2, 5, head, TOP + TAU, (u, v) =>
            {
                double d = u * TAU * f;
                if (flash || d < 0.1) return WHITE;
                if (d < 0.32) return v > 0.5 ? WHITE : C[6];
                return v > 0.75 ? C[6] : v > 0.4 ? C[5] : v > 0.15 ? C[4] : C[3];
            });
            // light runs round the lit arc, hottest at the head
            for (double an = head + 0.25; an < TOP + TAU; an += 0.55) p.Glow(x + Px.Cos(an) * (R - 0.5), y + Px.Sin(an) * (R - 0.5), 9, C[4], 0.12);
            double hx = x + Px.Cos(head) * (R - 0.5), hy = y + Px.Sin(head) * (R - 0.5);
            p.Glow(hx, hy, 14, C[5], 0.5);
            if (!flash) Sim.Sparkle(p, hx, hy, 2 + 1.5 * (0.5 + 0.5 * Px.Sin(t * 40)), C[6], C[5], 0.4);
            return head;
        }

        sealed class Ray
        {
            public double a, L;
        }

        readonly uint[] C, G;
        readonly Rng Rg;
        readonly int n;
        readonly Particles ps, gs;
        readonly Lights lights;
        readonly double GO, OUT;
        const double END = DURATION - 0.05;
        readonly Word[] digits;
        readonly Word word;
        readonly Ray[] rays;
        readonly double seed;
        double t;
        double beat = -1;
        bool went, popped;

        Countdown(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            G = Fit(o.Ramp("go"), 8);
            Rg = new Rng(o.Seed);
            seed = o.Num("seed");
            n = (int)Clamp(Round(o.Num("from")), 1, 3);
            ps = new Particles(Rg, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            gs = new Particles(Rg, sparks: new[] { G[7], G[6], G[5], G[3] }, emitter: e);
            lights = new Lights(e);
            GO = n * STEP;
            OUT = GO + 0.78;
            digits = new Word[n];
            for (int i = 0; i < n; i++) digits[i] = Spell((n - i).ToString(CultureInfo.InvariantCulture));
            word = Spell(Convert.ToString(o["word"], CultureInfo.InvariantCulture));
            rays = new Ray[18];
            for (int i = 0; i < 18; i++)
            {
                var r = new Ray { a = ((double)i / 18) * TAU + Rg.Range(-0.06, 0.06) };
                r.L = i % 2 != 0 ? Rg.Range(10, 16) : Rg.Range(22, 30);
                rays[i] = r;
            }
        }

        static double SizeAt(double u, double b, double big) => b + big * Exp(-u * 26) * Px.Cos(u * 38);
        static double Reach(double v, double d) => v <= d * 2.2 ? 9 : -Px.Log(1 - (d * 2.2) / v) / 2.2;

        public override void Update(double dt)
        {
            t += dt;
            double i = Math.Floor(t / STEP);
            // each number slams in: the ring refills white and throws a ring of sparks
            if (t < GO && i != beat)
            {
                beat = i;
                lights.Add(0, 0, 54, C[5], 0.6, 0.35);
                for (int j = 0; j < 14; j++)
                {
                    double an = ((double)j / 14) * TAU + Rg.Range(-0.1, 0.1), sp = Rg.Range(90, 200);
                    ps.Spawn(P.Spark, Px.Cos(an) * (R + 2), Px.Sin(an) * (R + 2), Px.Cos(an) * sp, Px.Sin(an) * sp, Math.Min(Rg.Range(0.12, 0.3), Reach(sp, 26)));
                }
            }
            // the burning head of the arc spits sparks as it runs
            if (t < GO)
            {
                double k = (t % STEP) / STEP, f = 1 - Seg(k, 0.06, 0.97), head = TOP + TAU * (1 - f);
                if (f > 0.01 && f < 0.99 && Rg.Chance(0.7))
                {
                    double hx = Px.Cos(head) * R, hy = Px.Sin(head) * R, tx = -Px.Sin(head), ty = Px.Cos(head), outK = Rg.Range(-0.4, 1), sp = Rg.Range(60, 150);
                    ps.Spawn(P.Spark, hx, hy, (-tx * 0.8 + Px.Cos(head) * outK) * sp, (-ty * 0.8 + Px.Sin(head) * outK) * sp - 20, Rg.Range(0.12, 0.28));
                    // and leaves cinders drifting off the burnt-out track behind it
                    if (Rg.Chance(0.15)) ps.Spawn(P.Ember, hx, hy, Px.Cos(head) * Rg.Range(5, 20), Px.Sin(head) * Rg.Range(5, 20), Rg.Range(0.4, 0.8), C[5], 1);
                }
            }
            // GO!: the ring blows out, sparks and stars everywhere
            if (!went && t >= GO)
            {
                went = true;
                lights.Add(0, 0, 70, WHITE, 0.9, 0.18);
                lights.Add(0, 0, 64, G[5], 0.7, 0.8);
                for (int j = 0; j < 36; j++)
                {
                    double an = Rg.Range(0, TAU), sp = Rg.Range(120, 300);
                    gs.Spawn(P.Spark, Px.Cos(an) * R, Px.Sin(an) * R, Px.Cos(an) * sp, Px.Sin(an) * sp - 40, Math.Min(Rg.Range(0.18, 0.36), Reach(sp, 24)));
                }
                for (int j = 0; j < 18; j++)
                {
                    double an = Rg.Range(0, TAU), sp = Rg.Range(30, 80), r = Rg.Range(10, 28);
                    gs.Spawn(P.Star, Px.Cos(an) * r, Px.Sin(an) * r, Px.Cos(an) * sp, Px.Sin(an) * sp - 10, Rg.Range(0.5, 1), Rg.Pick(new[] { G[7], G[6], G[5] }), Rg.Chance(0.4) ? Rg.Range(1.5, 2.5) : 0.6);
                }
            }
            // it twinkles while it holds
            if (t > GO + 0.2 && t < OUT - 0.1 && Rg.Chance(0.12))
            {
                double an = Rg.Range(0, TAU), r = Rg.Range(30, 46);
                gs.Spawn(P.Star, Px.Cos(an) * r, Px.Sin(an) * r * 0.8, 0, -Rg.Range(4, 12), Rg.Range(0.3, 0.6), Rg.Pick(new[] { G[7], G[6] }), Rg.Chance(0.3) ? 1.6 : 0.6);
            }
            if (!popped && t >= OUT + 0.05)
            {
                popped = true;
                lights.Add(0, 0, 40, G[6], 0.6, 0.25);
                for (int j = 0; j < 14; j++)
                {
                    double an = ((double)j / 14) * TAU + Rg.Range(-0.15, 0.15), sp = Rg.Range(60, 130);
                    gs.Spawn(P.Star, Px.Cos(an) * 12, Px.Sin(an) * 8, Px.Cos(an) * sp, Px.Sin(an) * sp * 0.8 - 10, Math.Min(Rg.Range(0.3, 0.45), END - t), Rg.Pick(new[] { G[7], G[6], G[5] }), j % 3 != 0 ? 0.6 : Rg.Range(1.3, 2));
                }
            }
            ps.Update(dt);
            gs.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            if (t < GO)
            {
                int i = (int)Math.Floor(t / STEP);
                double u = t - i * STEP, k = u / STEP;
                var (sx, sy) = Shake(t, 3, i * STEP, 0.22, seed + i);
                double f = 1 - Seg(k, 0.06, 0.97);
                bool flash = u < 0.07;
                // a shock ring off each slam
                double rk = u / 0.3;
                if (rk < 1) p.Ring(sx, sy, R + 6 + 22 * Ease.OutCubic(rk), 1.5 * (1 - rk) + 1, rk < 0.3 ? WHITE : C[5], 1.3 * (1 - rk));
                Timer(p, C, sx, sy, f, flash, t);
                // the slam's white-hot frame fills the ring
                if (u < 0.035) p.Circle(sx, sy, R - 3, WHITE);
                else if (u < 0.07) p.Ring(sx, sy, R - 3, 3, C[6]);
                // the number: huge and white for a frame, then a stiff spring down to size
                double s = SizeAt(u, 5, 2);
                Word d = digits[i];
                double W = d.W * s, H = 7 * s, x0 = sx - W / 2, y0 = sy - H / 2;
                double fl = u < 0.035 ? 1 : u < 0.07 ? 0.4 : 0;
                var items = d.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = x0 + L.X * s, Y = y0, Sx = s, Sy = s, Flash = fl }).ToList();
                DrawWord(p, items, new Style { Fill = new[] { C[4], C[5], C[6], C[7] }, Edge = WHITE, Under = C[3], Outline = C[0], Ow = 2, Shadow = Scale(C[0], 0.5) });
                p.Glow(sx, sy, 30, C[4], 0.22 + (u < 0.12 ? 0.6 * (1 - u / 0.12) : 0));
                p.Glow(sx, sy, R + 8, C[3], 0.2 * (1 - k) + 0.08);
            }
            else
            {
                double u = t - GO;
                var (sx, sy) = Shake(t, 4, GO, 0.3, seed + 9);
                // the ring blows out into a shockwave
                double bk = u / 0.4;
                if (bk < 1)
                {
                    double r = R + 4 + 30 * Ease.OutCubic(bk), w = Math.Max(1, 7 * (1 - bk));
                    p.Ring(0, 0, r, w, (v, g) => bk < 0.2 || v > 0.6 ? WHITE : G[6], 1.4 * (1 - bk));
                    p.GlowRing(0, 0, r - w / 2, 8, G[5], 0.5 * (1 - bk));
                }
                double ik = (u - 0.05) / 0.3;
                if (ik > 0 && ik < 1) p.Ring(0, 0, R + 22 * Ease.OutCubic(ik), 1, G[5], 1.2 * (1 - ik));
                // rays: shoot out from just outside the word, turn slowly, draw back in
                double rk = Ease.OutExpo(Clamp(u / 0.18)) * (1 - Ease.InQuad(Seg(t, OUT - 0.25, OUT + 0.05)));
                foreach (Ray ry in rays)
                {
                    if (rk <= 0.02) break;
                    double an = ry.a + u * 0.35, ca = Px.Cos(an), sa = Px.Sin(an), qa = ca / 40, qb = sa / 23;
                    double r0 = 1 / Math.Sqrt(qa * qa + qb * qb) + 3 * rk, r1 = r0 + ry.L * 0.7 * rk;
                    p.Line(ca * r0, sa * r0, ca * r1, sa * r1, q => q < 0.3 ? WHITE : q < 0.65 ? G[6] : G[5], q => q < 0.6 ? 1.3 : (1.3 * (1 - q)) / 0.4);
                }
                // GO!: slams in bigger than the numbers, throbs while it holds, then pops
                double pu = (t - OUT) / 0.06;
                if (pu < 1)
                {
                    double s = SizeAt(u, 5, 3.5), flash = u < 0.035 ? 1 : u < 0.07 ? 0.4 : 0;
                    double throb = Math.Max(0, Px.Sin((u - 0.25) * 9)) * Smooth(0.25, 0.35, u);
                    if (pu > 0)
                    {
                        s = 5 * (1 + 0.25 * pu);
                        flash = 1;
                    }
                    double W = word.W * s, H = 7 * s, x0 = sx - W / 2, y0 = sy - H / 2;
                    var items = word.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = x0 + L.X * s, Y = y0, Sx = s, Sy = s, Slant = 1, Flash = flash }).ToList();
                    double sh = Seg(u, 0.32, 0.56);
                    double? shine = sh > 0 && sh < 1 ? x0 - 8 + (W + 22) * Ease.InOutSine(sh) : (double?)null;
                    DrawWord(p, items, new Style { Fill = new[] { G[4], G[5], G[6], G[7] }, Edge = WHITE, Under = G[3], Outline = G[0], Ow = 2, Shadow = Scale(G[0], 0.5), Shine = shine, ShineY = y0, Glint = G[6] });
                    p.Glow(sx, sy, 46, G[5], 0.25 + 0.15 * throb + (u < 0.12 ? 0.7 * (1 - u / 0.12) : 0));
                }
                else
                {
                    double k = (t - OUT - 0.06) / 0.22;
                    if (k > 0 && k < 1) p.Ring(0, 0, 8 + 26 * Ease.OutCubic(k), 1, k < 0.4 ? WHITE : G[6], 1.3 * (1 - k));
                }
            }
            ps.Draw(p);
            gs.Draw(p);
            lights.Draw(p);
        }
    }
}
