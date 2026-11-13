using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class ComboCounter : Effect
    {
        public static readonly Module Module = new Module("combo-counter", "pixelfx/fx/ui/combo-counter", 208, 100, 104, 50, 1.45, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0516", "#3d0a33", "#701560", "#b0228f", "#f53cd1", "#ff86e6", "#ffd2f6", "#ffffff" },
                ["count"] = 5,
                ["label"] = "COMBO",
                ["seed"] = 6,
            },
            (o, e) => new ComboCounter(o, e))
        {
            Title = "Combo counter",
            Desc = "x5 COMBO rockets in on streaming speed lines, smearing, and slams to a stop: a white flash, a ring, sparks; it pulses while a timer bar drains, then whooshes out.",
            Tags = new[] { "ui", "combo", "counter", "combat", "text", "one-shot" },
            Bg = "dark",
        };

        const double HIT = 0.1, BEAT = 0.5, OUT = 1.1;

        // a burst of speed lines across the frame, re-thrown every frame like a comic panel; k fades them
        static void SpeedLines(Painter p, uint[] C, double t, double k, int dir)
        {
            if (k <= 0.02) return;
            double f = Math.Floor(t * 30);
            for (int i = 0; i < 18; i++)
            {
                if (Rand(f, i, 3) > k) continue;
                double y = Round((Rand(f, i, 1) - 0.5) * 60), L = 16 + Rand(f, i, 2) * 50, x = (Rand(f, i, 4) - 0.5) * 190;
                double x0 = Math.Max(-100, x - L / 2), x1 = Math.Min(100, x + L / 2);
                p.Line(x0, y, x1, y, u => (dir > 0 ? u : 1 - u) > 0.75 ? WHITE : (dir > 0 ? u : 1 - u) > 0.35 ? C[6] : C[4], u => (dir > 0 ? u : 1 - u) < 0.25 ? 0.6 : 1);
            }
        }

        readonly uint[] C;
        readonly Rng R;
        readonly uint SHADOW;
        readonly Particles ps;
        readonly Lights lights;
        readonly Word big, lab;
        const double BS = 4, LS = 2, BASE = 12;
        readonly double W, X0;
        readonly double[][] spikes;
        readonly double seed;
        double t;
        bool hit, outDone;

        ComboCounter(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            SHADOW = Scale(C[0], 0.5);
            seed = o.Num("seed");
            ps = new Particles(R, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
            big = Spell("x" + RoundInt(o.Num("count")));
            lab = Spell(o.Str("label"));
            W = big.W * BS + 4 + lab.W * LS;
            X0 = -W / 2;
            spikes = new double[10][];
            for (int i = 0; i < 10; i++)
            {
                double a = ((double)i / 10) * TAU + R.Range(-0.15, 0.15);
                spikes[i] = new[] { a, R.Range(14, 24) };
            }
        }

        // where the words sit: rocketing in from the left, overshooting, settling; whooshing out to the right
        static double XAt(double t)
        {
            t = Math.Max(0, t);
            if (t < HIT) return -32 * Px.Pow(1 - t / HIT, 2);
            if (t < OUT) return 7 * Exp(-(t - HIT) * 18) * Px.Sin((t - HIT) * 34);
            double u = Seg(t, OUT, OUT + 0.2);
            return u < 0.3 ? -4 * Ease.OutQuad(u / 0.3) : -4 + 24 * Ease.InQuad((u - 0.3) / 0.7);
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(0, 0, 70, C[5], 0.7, 0.4);
                lights.Add(-20, 0, 34, WHITE, 0.8, 0.1);
                for (int i = 0; i < 26; i++)
                {
                    double an = R.Range(-1.2, 1.2) + (R.Chance(0.3) ? Math.PI : 0), sp = R.Range(120, 300);
                    ps.Spawn(P.Spark, R.Range(-20, 20), R.Range(-8, 8), Px.Cos(an) * sp, Px.Sin(an) * sp * 0.6 - 20, R.Range(0.12, 0.3));
                }
            }
            // sparkles pop off the count on its beat
            if (t > BEAT && t < OUT && R.Chance(0.1))
            {
                double x = X0 + R.Range(0, big.W * BS), y = BASE - R.Range(0, 30);
                ps.Spawn(P.Star, x, y, R.Range(-10, 10), -R.Range(5, 20), R.Range(0.25, 0.45), R.Pick(new[] { C[7], C[6] }), R.Chance(0.3) ? 1.6 : 0.6);
            }
            if (!outDone && t >= OUT + 0.06)
            {
                outDone = true;
                for (int i = 0; i < 10; i++) ps.Spawn(P.Mote, R.Range(-50, 30), R.Range(-14, 10), R.Range(220, 320), R.Range(-10, 10), R.Range(0.06, 0.14), R.Pick(new[] { C[7], C[6] }), 1);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        List<Item> Layout(double dx, double pulse, double stretch, double flash, double a)
        {
            double bs = BS * (1 + 0.12 * pulse);
            var items = new List<Item>();
            double bx = X0 + dx - (big.W * BS * (stretch - 1)) / 2, by = BASE - 7 * bs;
            foreach (Letter L in big.Letters)
                if (L.G != null) items.Add(new Item { G = L.G, X = bx + L.X * BS * stretch, Y = by, Sx = BS * stretch, Sy = bs, Slant = 1, Flash = flash, A = a });
            double lx = X0 + dx + big.W * BS * stretch + 4;
            foreach (Letter L in lab.Letters)
                if (L.G != null) items.Add(new Item { G = L.G, X = lx + L.X * LS * stretch, Y = BASE - 7 * LS, Sx = LS * stretch, Sy = LS, Slant = 0.5, Flash = flash, A = a });
            return items;
        }

        public override void Draw(Painter p, double tt)
        {
            var (sx, sy) = Shake(t, 3, HIT, 0.25, seed);
            // speed lines stream behind it as it rockets in, and again as it leaves
            SpeedLines(p, C, t, t < HIT ? 1 : t < OUT ? Math.Max(0, 1 - (t - HIT) / 0.35) * 0.9 : Seg(t, OUT + 0.04, OUT + 0.12) * (1 - Seg(t, OUT + 0.18, OUT + 0.3)), t >= OUT ? -1 : 1);
            // the slam: a spiky white star behind the count for two frames, then a ring rolling out
            double hk = t - HIT;
            if (hk >= 0 && hk < 0.07)
            {
                double cx = X0 + (big.W * BS) / 2, k = hk < 0.035 ? 1 : 1.3;
                foreach (double[] sp in spikes)
                {
                    double an = sp[0], L = sp[1];
                    double ca = Px.Cos(an) * 1.4, sa = Px.Sin(an);
                    if (k == 1) p.Beam(cx, 0, cx + ca * L, sa * L, 3, (u, v) => u < 0.5 ? WHITE : C[6]);
                    else p.Line(cx + ca * L * 0.5, sa * L * 0.5, cx + ca * L * k, sa * L * k, C[5]);
                }
            }
            double rk = (t - HIT) / 0.32;
            if (rk > 0 && rk < 1)
            {
                double r = 20 + 34 * Ease.OutCubic(rk);
                p.Ellipse(sx, sy, r * 1.5, r * 0.75, rk < 0.3 ? WHITE : C[5], (d, g) => d > 1 - 1.5 / (r * 0.75) ? 1.25 - rk : 0);
            }
            ps.Draw(p);
            // the words: smeared while they fly, white on the slam, a pulse on the beat, a drained timer underneath
            double x = XAt(t);
            bool moving = t < HIT || t > OUT + 0.06;
            double stretch = moving ? (t < HIT ? 1.22 : 1.12) : 1;
            double q = (t - BEAT) / 0.05;
            double pulse = Exp(-(q * q)), flash = (t >= HIT && t < HIT + 0.07) || pulse > 0.6 ? 1 : 0;
            double a = t > OUT ? 1 - Seg(t, OUT + 0.08, OUT + 0.2) : t < 0.035 ? 0.5 : 1;
            if (moving && a > 0)
            {
                // two dimmer afterimages trailing the words
                foreach (var (lag, c) in new[] { (0.03, C[2]), (0.015, C[3]) })
                {
                    var items = Layout(XAt(t - (t < HIT ? lag : -lag)) + sx, pulse, stretch, flash, a);
                    DrawWord(p, items.Select(it => { Item n = it.With(); n.Flash = 0; return n; }).ToList(), new Style { Fill = new[] { c }, Edge = c, Under = c, Outline = 0, Shadow = 0 });
                }
            }
            if (a > 0)
            {
                double sh = Seg(t, 0.62, 0.86);
                var items = Layout(x + sx, pulse, stretch, flash, a);
                DrawWord(p, items.Select(it => { Item n = it.With(); n.Y = it.Y + sy; return n; }).ToList(),
                    new Style { Fill = new[] { C[4], C[5], C[6], C[7] }, Edge = WHITE, Under = C[3], Outline = C[0], Ow = 2, Shadow = SHADOW, Shine = sh > 0 && sh < 1 ? X0 - 10 + (W + 24) * sh : (double?)null, ShineY = BASE - 28, Glint = C[6] });
                // the combo timer: a thin bar under the words, draining until it goes
                double bk = Seg(t, HIT + 0.1, HIT + 0.2) * (t > OUT ? 0 : 1);
                if (bk > 0)
                {
                    double bw = W * bk, left = 1 - Seg(t, HIT + 0.2, OUT), bx = X0 + x + sx, by = BASE + 5 + sy;
                    p.Rect(bx - 1, by - 1, bw + 2, 4, C[0]);
                    p.Rect(bx, by, bw, 2, C[1]);
                    if (left > 0)
                    {
                        p.Rect(bx, by, bw * left, 2, C[5]);
                        p.Rect(bx, by, bw * left, 1, C[6]);
                        p.Px(bx + bw * left - 1, by, WHITE);
                    }
                }
            }
            p.Glow(sx - 20, 0, 60, C[4], 0.2 * a + (t >= HIT && t < HIT + 0.15 ? 0.5 * (1 - (t - HIT) / 0.15) : 0) + 0.25 * pulse);
            double gk = (t - 0.86) / 0.22;
            if (gk > 0 && gk < 1) Sim.Sparkle(p, X0 + W - 2, BASE - 14, 6 * Px.Sin(gk * Math.PI), C[7], C[5], 0.5);
            lights.Draw(p);
        }
    }
}
