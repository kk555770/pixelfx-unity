using System;
using System.Globalization;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class CritNumber : Effect
    {
        const double DURATION = 1.5;

        public static readonly Module Module = new Module("crit-number", "pixelfx/fx/ui/crit-number", 152, 120, 76, 60, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0710", "#5e0f16", "#a3191c", "#e0401c", "#ff7b22", "#ffb93b", "#ffe68a", "#ffffff" },
                ["text"] = "128!",
                ["label"] = "CRIT",
                ["seed"] = 3,
            },
            (o, e) => new CritNumber(o, e))
        {
            Title = "Crit number",
            Desc = "CRIT 128! slams in huge and white over a jagged starburst with a hard shake and speed lines, cools to fire, holds with a shine, then pops into sparks.",
            Tags = new[] { "ui", "critical", "damage", "number", "combat", "text", "one-shot" },
            Bg = "dark",
        };

        const double FOLD = 0.92, TAGOUT = 0.98, POP = 1.06, SX = 1.3, NS = 4, NY = 5;

        sealed class Tip
        {
            public double len, dip;
        }

        sealed class SpeedLine
        {
            public double a, r, len;
        }

        // the jagged starburst: a spiky polygon with a dark rim, a bright inner contour, bands that cool from the
        // white-hot middle out, and spokes of light running to every tip. heat 1 is white all through.
        static void Starburst(Painter p, uint[] C, double cx, double cy, double R, double rot, Tip[] tips, double heat, double boil, double a = 1)
        {
            if (R < 2 || a <= 0) return;
            int n = tips.Length;
            var V = new double[n * 2][];
            for (int i = 0; i < n * 2; i++)
            {
                double an = rot + (i * Math.PI) / n, r = R * (i % 2 != 0 ? 0.6 + tips[i >> 1].dip : tips[i >> 1].len * (1 + 0.14 * (Rand(boil, i) - 0.5)));
                V[i] = new[] { Px.Cos(an) * r, Px.Sin(an) * r, an };
            }
            double step = Math.PI / n;
            double Edge(double dx, double dy)
            {
                // where the ray from the middle through (dx, dy) leaves the polygon: the vertices sit at even angles, so
                // the ray's angle names the one edge it can cross
                double ang = Atan2(dy, dx) - rot;
                ang -= Math.Floor(ang / TAU) * TAU;
                int i = (int)Math.Min(V.Length - 1, Math.Floor(ang / step));
                double x0 = V[i][0], y0 = V[i][1], x1 = V[(i + 1) % V.Length][0], y1 = V[(i + 1) % V.Length][1];
                double ex = x1 - x0, ey = y1 - y0, den = dx * ey - dy * ex;
                return Math.Abs(den) < 1e-9 ? Hypot(x0, y0) : (x0 * ey - y0 * ex) / den;
            }
            double M = R * 1.15 + 2;
            uint[] steps = { WHITE, C[6], C[5], C[4], C[3], C[2] };
            p.Shade(cx - M * SX, cy - M, cx + M * SX, cy + M, (x, y, o) =>
            {
                double dx = (x - cx) / SX, dy = y - cy, d = Hypot(dx, dy);
                if (d < 0.01) return WHITE;
                double L = Edge(dx / d, dy / d), gap = (L - d) * (1 + 0.3 * Math.Abs(dx / d)), q = d / L;
                if (gap < 0) return 0;
                o.a = a;
                if (heat > 0.85) return gap < 1 ? C[6] : WHITE;
                // a dark edge, a glowing rim inside it, then a middle that deepens to dark red for the number to stand on
                if (gap < 1) return C[1];
                if (gap < 2.2) return heat > 0.3 ? WHITE : C[6];
                if (gap < 3.4) return heat > 0.3 ? C[6] : C[5];
                if (gap >= 6 && gap < 7) return heat > 0.5 ? WHITE : C[4];
                // spokes: a line of light from the middle out to each tip
                double an = Atan2(dy, dx) - rot, sp = Math.Abs(((an / TAU) * n + 0.5) % 1 - 0.5) * (TAU / n) * d;
                if (sp < 0.6 && q > 0.25) return heat > 0.5 ? WHITE : q > 0.6 ? C[5] : C[4];
                double i = Round((q > 0.72 ? 3 : q > 0.45 ? 4 : 5) - heat * 5);
                return i <= 0 ? WHITE : steps[(int)i];
            });
        }

        // the CRIT tag: a leaning dark plate with a lit top edge
        static void Tag(Painter p, uint[] C, double x, double y, double w, double h, double lean)
        {
            p.Shade(x - 1, y - 1, x + w + lean + 1, y + h + 1, (px, py, o) =>
            {
                double lx = px - x - lean * ((y + h - py) / h);
                if (py < y || py > y + h || lx < 0 || lx > w) return 0;
                if (lx < 1 || lx > w - 1 || py < y + 1 || py > y + h - 1) return C[0];
                return py < y + 2 ? C[4] : py < y + h * 0.55 ? C[3] : C[2];
            });
        }

        // speed lines thrown out round the burst
        static void SpeedLines(Painter p, uint[] C, double t, SpeedLine[] lines)
        {
            double k = t / 0.24;
            if (k <= 0 || k >= 1) return;
            foreach (SpeedLine l in lines)
            {
                double r0 = l.r * (1 + 0.5 * Ease.OutExpo(k)), r1 = r0 + l.len * (1 - k) + 2, ca = Px.Cos(l.a) * 1.2, sa = Px.Sin(l.a) * 0.92;
                p.Line(ca * r0, sa * r0, ca * r1, sa * r1, u => u > 0.5 || k < 0.3 ? WHITE : C[6]);
            }
        }

        static readonly uint[] WHITE_TAG = { WHITE, 0, WHITE, WHITE, WHITE };

        readonly uint[] C;
        readonly Rng R;
        readonly uint SHADOW;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Word num, lab;
        readonly Tip[] tips;
        readonly SpeedLine[] lines;
        const double END = DURATION - 0.05;
        readonly double seed;
        double t;
        bool slammed, stamped, popped, tagGone;

        CritNumber(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            SHADOW = Scale(C[0], 0.5);
            seed = o.Num("seed");
            ps = new Particles(R, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            bits = new Bits(emitter: e);
            lights = new Lights(e);
            num = Spell(Convert.ToString(o["text"], CultureInfo.InvariantCulture));
            lab = Spell(Convert.ToString(o["label"], CultureInfo.InvariantCulture), "mini");
            tips = new Tip[13];
            for (int i = 0; i < 13; i++)
            {
                var tp = new Tip { len = R.Range(0.86, 1.12) };
                tp.dip = R.Range(-0.06, 0.06);
                tips[i] = tp;
            }
            lines = new SpeedLine[18];
            for (int i = 0; i < 18; i++)
            {
                var l = new SpeedLine { a = ((double)i / 18) * TAU + R.Range(-0.1, 0.1) };
                l.r = R.Range(31, 34);
                l.len = R.Range(8, 15);
                lines[i] = l;
            }
        }

        // how long a spark launched at v px/s may live before its drag carries it d px (so none leaves the canvas)
        static double Reach(double v, double d) => v <= d * 2.2 ? 9 : -Px.Log(1 - (d * 2.2) / v) / 2.2;

        void SpawnStar(double x, double y, double vx, double vy, double life, uint c, double size) => ps.Spawn(P.Star, x, y, vx, vy, Math.Min(life, END - t), c, size);

        // the number's size: huge for the first frame, then a stiff spring down to 3 px a pixel
        static double SizeAt(double t) => NS + 3 * Exp(-t * 26) * Px.Cos(t * 38);

        public override void Update(double dt)
        {
            t += dt;
            if (!slammed)
            {
                slammed = true;
                lights.Add(0, 0, 70, WHITE, 0.9, 0.16);
                lights.Add(0, 0, 60, C[4], 0.45, 0.5);
                for (int i = 0; i < 30; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(140, 300);
                    ps.Spawn(P.Spark, Px.Cos(an) * 8, Px.Sin(an) * 6, Px.Cos(an) * sp * 1.15, Px.Sin(an) * sp - 40, Math.Min(R.Range(0.12, 0.32), Reach(sp * 1.15, 40)));
                }
                for (int i = 0; i < 10; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(90, 170);
                    var b = new Bits.Bit { kind = B.Shard, x = Px.Cos(an) * 10, y = Px.Sin(an) * 7, vx = Px.Cos(an) * sp * 1.15, vy = Px.Sin(an) * sp - 60, g = 0.35, drag = 1.6 };
                    b.life = Math.Min(R.Range(0.3, 0.5), END - t);
                    b.a = R.Range(0, TAU);
                    b.va = R.Range(-24, 24);
                    b.s = R.Range(2, 3.5);
                    b.c = R.Pick(new[] { C[7], C[6], C[5] });
                    bits.Add(b);
                }
            }
            // the label stamps down a beat later
            if (!stamped && t >= 0.09)
            {
                stamped = true;
                lights.Add(-4, -20, 30, C[6], 0.5, 0.2);
                for (int i = 0; i < 6; i++) ps.Spawn(P.Star, -4 + R.Range(-16, 16), -20 + R.Range(-4, 4), R.Range(-40, 40), R.Range(-30, 10), R.Range(0.2, 0.4), C[6], R.Range(0.5, 1.4));
            }
            // the tag bursts into sparkles as it goes
            if (!tagGone && t >= TAGOUT)
            {
                tagGone = true;
                for (int i = 0; i < 6; i++) SpawnStar(-10 + R.Range(-16, 16), -14 + R.Range(-5, 5), R.Range(-50, 50), -R.Range(10, 60), R.Range(0.25, 0.4), R.Pick(new[] { C[7], C[6] }), i != 0 ? 0.6 : 1.6);
            }
            // the burst crackles: sparks spit from its tips
            if (t > 0.3 && t < FOLD - 0.1 && R.Chance(0.07))
            {
                double an = R.Range(0, TAU), sp = R.Range(70, 150);
                ps.Spawn(P.Spark, Px.Cos(an) * 30 * SX, Px.Sin(an) * 30, Px.Cos(an) * sp, Px.Sin(an) * sp - 30, Math.Min(R.Range(0.12, 0.26), Reach(sp, 14)));
            }
            // embers lift off the burst while it holds
            if (t > 0.12 && t < FOLD && R.Chance(0.16))
            {
                double an = R.Range(0, TAU), r = R.Range(14, 28);
                ps.Spawn(P.Ember, Px.Cos(an) * r * SX, Px.Sin(an) * r, R.Range(-10, 10), -R.Range(15, 40), Math.Min(R.Range(0.5, 0.9), END - t), C[5], R.Range(1, 2));
            }
            if (!popped && t >= POP + 0.05)
            {
                popped = true;
                lights.Add(0, 0, 44, C[6], 0.7, 0.25);
                for (int i = 0; i < 14; i++)
                {
                    double an = ((double)i / 14) * TAU + R.Range(-0.15, 0.15), sp = R.Range(60, 130);
                    ps.Spawn(P.Star, Px.Cos(an) * 10 * SX, Px.Sin(an) * 7, Px.Cos(an) * sp * SX, Px.Sin(an) * sp - 10, Math.Min(R.Range(0.3, 0.42), END - t), R.Pick(new[] { C[7], C[6], C[5] }), i % 3 != 0 ? 0.6 : R.Range(1.3, 2.2));
                }
                for (int i = 0; i < 8; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(120, 240);
                    ps.Spawn(P.Spark, Px.Cos(an) * 6, Px.Sin(an) * 4, Px.Cos(an) * sp, Px.Sin(an) * sp - 30, Math.Min(Math.Min(R.Range(0.15, 0.3), END - t), Reach(sp, 40)));
                }
            }
            ps.Update(dt);
            bits.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            var (sx, sy) = Shake(t, 4, 0, 0.32, seed);
            var (lx, ly) = Shake(t, 2, 0.09, 0.15, seed + 3);
            // the burst: white on the slam, cooling; it breathes while it holds, then folds away
            double grow = t < 0.04 ? 1.25 : 1 + 0.1 * Exp(-(t - 0.04) * 14), fold = 1 - Ease.InBack(Seg(t, FOLD - 0.12, FOLD), 2.4);
            double heat = t < 0.035 ? 1 : Clamp(1 - (t - 0.035) / 0.12), breathe = 1 + 0.04 * Px.Sin(t * 16);
            // it boils: the spikes re-roll a little every few frames
            if (fold > 0.02) Starburst(p, C, sx, sy, 31 * grow * fold * breathe, -Math.PI / 2 + 0.25 * t, tips, heat, t < 0.2 ? 0 : Math.Floor(t * 10));
            SpeedLines(p, C, t, lines);
            // the ring the slam throws out
            double rk = t / 0.26;
            double rr = 30 + 22 * Ease.OutCubic(rk);
            if (rk > 0 && rk < 1) p.Ellipse(0, 0, rr * SX, rr, rk < 0.35 ? WHITE : C[6], (d, g) => d > 1 - 1.6 / rr ? 1.2 - rk : 0);
            bits.Draw(p);
            ps.Draw(p);
            // the number: slams in huge and white, settles with a spring; a shine runs over it; it pops at the end
            double s = SizeAt(t), sxN = s, syN = s, flash = t < 0.035 ? 1 : t < 0.07 ? 0.5 : 0;
            double pu = (t - POP) / 0.05;
            if (pu >= 1)
            {
                // gone: a ring where it was
                double k = (t - POP - 0.05) / 0.22;
                if (k > 0 && k < 1) p.Ellipse(0, 0, (6 + 22 * Ease.OutCubic(k)) * SX, 6 + 22 * Ease.OutCubic(k), k < 0.4 ? WHITE : C[6], (d, g) => d > 1 - 1.5 / (6 + 22 * Ease.OutCubic(k)) ? 1.3 - k : 0);
            }
            else
            {
                double sq = Seg(t, POP - 0.07, POP);
                if (sq > 0 && pu <= 0)
                {
                    sxN = NS * (1 + 0.14 * sq);
                    syN = NS * (1 - 0.12 * sq);
                }
                if (pu > 0)
                {
                    sxN = syN = NS * (1 + 0.25 * pu);
                    flash = 1;
                }
                double W = num.W * sxN, H = 7 * syN, x0 = sx - W / 2 - 3, y0 = sy + NY - H / 2 - 3 * Smooth(0.15, FOLD, t);
                var items = num.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = x0 + L.X * sxN, Y = y0, Sx = sxN, Sy = syN, Slant = 1, Flash = flash }).ToList();
                double sh = Seg(t, 0.36, 0.6);
                double? shine = sh > 0 && sh < 1 ? x0 - 10 + (W + 26) * Ease.InOutSine(sh) : (double?)null;
                DrawWord(p, items, new Style { Fill = new[] { C[5], C[6], C[6], C[7] }, Edge = WHITE, Under = C[4], Outline = C[0], Ow = 2, Shadow = SHADOW, Shine = shine, ShineY = y0, Glint = C[5] });
                if (shine is double shv && shv > x0 && shv < x0 + W) p.GlowLine(shv, y0, shv + 0.6 * H, y0 + H, 8, C[6], 0.3);
                // the label stamps down on top, a size too big for a frame
                double lk = t - 0.06, lo = Seg(t, TAGOUT - 0.05, TAGOUT);
                if (lk > 0 && t < TAGOUT)
                {
                    double ls = lk < 0.035 ? 3 : lk < 0.07 ? 1.8 : 2 * (1 + 0.15 * lo), LW = lab.W * ls, lh = 5 * ls, lxo = sx + lx - 8 - LW / 2, lyo = y0 - 3 - lh + ly - 2 * lo;
                    if (lo > 0.5) Tag(p, WHITE_TAG, lxo - 4, lyo - 3, LW + 8, lh + 5, 3);
                    else Tag(p, C, lxo - 4, lyo - 3, LW + 8, lh + 5, 3);
                    double lf = lk < 0.035 ? 1 : 0;
                    var lit = lab.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = lxo + 1 + L.X * ls, Y = lyo, Sx = ls, Sy = ls, Slant = 0.5, Flash = lf }).ToList();
                    DrawWord(p, lit, new Style { Fill = new[] { C[6], C[7], C[7] }, Edge = WHITE, Under = C[5], Outline = 0, Shadow = C[1] });
                }
                // a glint on the number's corner after the shine
                double g = (t - 0.6) / 0.24;
                if (g > 0 && g < 1) Sim.Sparkle(p, x0 + W - 2, y0 + 1, 6 * Px.Sin(g * Math.PI), C[6], C[5], 0.6);
            }
            // light: the white flash of the slam, then a hot glow that pulses while it holds
            double hold = Smooth(0, 0.1, t) * (1 - Smooth(FOLD - 0.1, POP + 0.1, t));
            p.Glow(sx, sy, 56, C[4], 0.14 * hold * (1 + 0.2 * Px.Sin(t * 16)));
            if (t < 0.12) p.Glow(0, 0, 64, WHITE, 0.8 * (1 - t / 0.12));
            lights.Draw(p);
        }
    }
}
