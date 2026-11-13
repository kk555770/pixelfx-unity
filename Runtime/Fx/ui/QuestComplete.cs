using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class QuestComplete : Effect
    {
        const double DURATION = 2.6;

        public static readonly Module Module = new Module("quest-complete", "pixelfx/fx/ui/quest-complete", 204, 92, 102, 46, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1405", "#5c3008", "#9a5a0e", "#d48e18", "#f5b92c", "#ffde5e", "#fff4bc", "#ffffff" },
                ["ribbon"] = new[] { "#1e0608", "#45101a", "#7a1a26", "#ad2632", "#d8433f", "#f27a5e", "#ffb79a" },
                ["text"] = "QUEST COMPLETE",
                ["seed"] = 8,
            },
            (o, e) => new QuestComplete(o, e))
        {
            Title = "Quest complete",
            Desc = "A stitched ribbon unrolls from the middle, QUEST COMPLETE types on letter by letter, a gold seal stamps down and ticks, stars pop; then it all rolls away.",
            Tags = new[] { "ui", "quest", "banner", "reward", "text", "one-shot" },
            Bg = "dark",
        };

        const double L = 66, BH = 8, OPEN = 0.32, TYPE = 0.2, STEP = 0.036, STAMP = 0.95, SX = 64, SY = 4, POPSEAL = 2.0, ROLL = 2.08;

        // the ribbon: two notched tails behind, dark folds, then the band with a lit top, stitching and rolled ends
        static void Ribbon(Painter p, uint[] Rb, uint[] G, double l, double tails, double t)
        {
            if (l < 1) return;
            if (tails > 0.02)
                foreach (int s in new[] { -1, 1 })
                {
                    double drop = 6 * tails, x0 = s * (l - 8), x1 = s * (l + 16 * tails);
                    p.Shade(Math.Min(x0, x1) - 1, -BH + drop - 1, Math.Max(x0, x1) + 1, BH + drop + 1, (x, y, o) =>
                    {
                        double u = (x - x0) / (x1 - x0), v = y - drop;
                        if (u < 0 || u > 1 || Math.Abs(v) > BH) return 0;
                        // a V notch cut into the far end
                        if (u > 1 - (0.3 * (BH - Math.Abs(v))) / BH) return 0;
                        double e = Math.Min(BH - Math.Abs(v), (1 - u) * (x1 - x0) * s);
                        if (e < 1 || u < 0.02) return Rb[0];
                        return v < -BH + 2 ? Rb[3] : v < 0 ? Rb[2] : Rb[1];
                    });
                    // the fold where the tail tucks under the band
                    p.Tri(s * (l - 8), BH, s * (l - 1), BH, s * (l - 8), BH + drop, Rb[0]);
                }
            p.Shade(-l - 1, -BH - 1, l + 1, BH + 1, (x, y, o) =>
            {
                double ax = Math.Abs(x), v = y;
                if (ax > l || Math.Abs(v) > BH) return 0;
                double e = Math.Min(l - ax, BH - Math.Abs(v));
                if (e < 1) return Rb[0];
                if (v < -BH + 2) return Rb[5];
                if (v > BH - 2) return Rb[1];
                // gold stitching just inside the top and bottom edges
                if ((Math.Abs(v + BH - 2.5) < 0.5 || Math.Abs(v - BH + 3.5) < 0.5) && Math.Floor(x + 0.5) % 3 != 0 && ax < l - 3) return G[3];
                return v < -1 ? Rb[4] : Rb[3];
            });
            // while it unrolls, the rolled-up ends ride its edges
            if (tails < 0.02)
                foreach (int s in new[] { -1, 1 })
                {
                    double x = s * l;
                    p.Rect(Round(x) - 2, -BH - 2, 4, BH * 2 + 4, Rb[0]);
                    p.Rect(Round(x) - 1, -BH - 1, 1, BH * 2 + 2, Rb[4]);
                    p.Rect(Round(x), -BH - 1, 1, BH * 2 + 2, Rb[2]);
                    p.Px(Round(x) - 1, -BH - 1, Rb[6]);
                }
        }

        // a gold seal: a scalloped rim, a lit face with an inner ring, and a check mark drawn on in progress k
        static void Seal(Painter p, uint[] G, double x, double y, double r, double sx, double sy, bool white, double k)
        {
            double M = r * 1.25 + 2;
            p.Shade(x - M * sx, y - M * sy, x + M * sx, y + M * sy, (px, py, o) =>
            {
                double dx = (px - x) / sx, dy = (py - y) / sy, d = Hypot(dx, dy), an = Atan2(dy, dx), edge = r * (1 + 0.09 * Px.Cos(an * 14));
                if (d > edge) return 0;
                if (white) return edge - d < 1 ? G[5] : WHITE;
                if (edge - d < 1) return G[0];
                double l = -(dx * 0.6 + dy * 0.8) / r;
                if (edge - d < 3) return l > 0.3 ? G[6] : l > -0.3 ? G[5] : G[3];
                if (Math.Abs(d - r * 0.66) < 0.5) return l > 0 ? G[3] : G[2];
                return l > 0.45 ? G[7] : l > 0.1 ? G[6] : l > -0.35 ? G[5] : G[4];
            });
            if (white || k <= 0) return;
            // the check: a short stroke down, a long one up, white with a dark edge
            double[][] pts = new[] { new[] { -5.0, 0 }, new[] { -1.5, 3.5 }, new[] { 5.5, -4.5 } }.Select(q => new[] { x + q[0] * sx, y + q[1] * sy }).ToArray();
            double len1 = Hypot(pts[1][0] - pts[0][0], pts[1][1] - pts[0][1]), len2 = Hypot(pts[2][0] - pts[1][0], pts[2][1] - pts[1][1]);
            double done = k * (len1 + len2);
            var segs = new[] { (a: pts[0], b: pts[1], u: Math.Min(1, done / len1)), (a: pts[1], b: pts[2], u: Clamp((done - len1) / len2)) };
            foreach (var (w, c) in new[] { (4.2, G[0]), (2.4, WHITE) })
                foreach (var (a, b, u) in segs)
                    if (u > 0) p.Beam(a[0], a[1], a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, w, c);
        }

        sealed class Sat
        {
            public double x, y, t0, dur, L;
        }

        readonly uint[] G, Rb;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Word word;
        const double TS = 2, TY = -5;
        readonly double TW, TX;
        readonly Letter[] glyphs;
        readonly Sat[] sats;
        readonly double seed;
        const double END = DURATION - 0.05;
        double t;
        bool stamped, popped;

        QuestComplete(Options o, Emitter e)
        {
            G = Fit(o.Ramp("palette"), 8);
            Rb = Fit(o.Ramp("ribbon"), 7);
            R = new Rng(o.Seed);
            seed = o.Num("seed");
            ps = new Particles(R, sparks: new[] { G[7], G[6], G[5], G[3] }, emitter: e);
            lights = new Lights(e);
            word = Spell(Convert.ToString(o["text"], CultureInfo.InvariantCulture), "mini");
            TW = word.W * TS;
            TX = -TW / 2 - 4;
            glyphs = word.Letters.Where(Lt => Lt.G != null).ToArray();
            sats = new Sat[7];
            for (int i = 0; i < 7; i++)
            {
                var s = new Sat { x = ((double)i / 6 - 0.5) * 140 + R.Range(-6, 6) };
                s.y = (i % 2 != 0 ? 1 : -1) * R.Range(15, 22);
                s.t0 = STAMP + 0.12 + i * 0.12 + R.Range(0, 0.04);
                s.dur = R.Range(0.3, 0.45);
                s.L = R.Range(3, 6);
                sats[i] = s;
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!stamped && t >= STAMP)
            {
                stamped = true;
                lights.Add(SX, SY, 40, G[5], 0.7, 0.35);
                lights.Add(SX, SY, 18, WHITE, 0.7, 0.1);
                for (int i = 0; i < 12; i++)
                {
                    double an = ((double)i / 12) * TAU + R.Range(-0.2, 0.2), sp = R.Range(40, 80) * (Px.Cos(an) > 0 ? 0.6 : 1);
                    ps.Spawn(P.Star, SX + Px.Cos(an) * 11, SY + Px.Sin(an) * 11, Px.Cos(an) * sp, Px.Sin(an) * sp * 0.7, R.Range(0.25, 0.45), R.Pick(new[] { G[7], G[6], G[5] }), i % 3 != 0 ? 0.6 : R.Range(1.2, 1.8));
                }
            }
            // gold motes drift up off the banner while it holds
            if (t > STAMP && t < POPSEAL && R.Chance(0.08)) ps.Spawn(P.Star, R.Range(-L, L), R.Range(-BH, BH), R.Range(-5, 5), -R.Range(8, 20), Math.Min(R.Range(0.4, 0.8), END - t), R.Pick(new[] { G[6], G[5] }), 0.6);
            if (!popped && t >= POPSEAL + 0.05)
            {
                popped = true;
                lights.Add(SX, SY, 30, G[6], 0.6, 0.2);
                for (int i = 0; i < 8; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(40, 90);
                    ps.Spawn(P.Star, SX + Px.Cos(an) * 6, SY + Px.Sin(an) * 6, Px.Cos(an) * sp, Px.Sin(an) * sp, Math.Min(R.Range(0.25, 0.4), END - t), R.Pick(new[] { G[7], G[6] }), i % 2 != 0 ? 0.6 : 1.5);
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            var (jx, jy) = Shake(t, 2, STAMP, 0.18, seed);
            // the ribbon unrolls from the middle, and rolls back up at the end
            double l = L * (t < ROLL ? Ease.OutCubic(Seg(t, 0, OPEN)) : 1 - Ease.InCubic(Seg(t, ROLL, ROLL + 0.26)));
            double tails = Seg(t, OPEN - 0.02, OPEN + 0.12) * (t < ROLL ? 1 : 0);
            double sh = Seg(t, 1.2, 1.5);
            double? shine = sh > 0 && sh < 1 ? -L - 10 + (2 * L + 20) * sh : (double?)null;
            Ribbon(p, Rb, G, l, Ease.OutBack(tails, 2), t);
            if (shine is double shv)
                for (double y = -BH + 1; y < BH; y++)
                {
                    double x = shv + (y + BH) * 0.6;
                    if (Math.Abs(x) < l - 2)
                    {
                        p.Px(x, y, Rb[6]);
                        p.Px(x + 1, y, Rb[5]);
                    }
                }
            // the words type on, each letter popping in white, clipped by the ribbon as it rolls away
            var items = new List<Item>();
            for (int i = 0; i < glyphs.Length; i++)
            {
                Letter Lt = glyphs[i];
                double u = t - (TYPE + i * STEP), x = TX + Lt.X * TS + jx;
                if (u < 0 || Math.Abs(x + Lt.W) > l - 3) continue;
                double s = u < 0.035 ? 2.6 : TS;
                items.Add(new Item { G = Lt.G, X = x - ((s - TS) * Lt.W) / 2, Y = TY + jy - ((s - TS) * 5) / 2, Sx = s, Sy = s, Flash = u < 0.07 ? 1 : 0 });
            }
            DrawWord(p, items, new Style { Fill = new[] { G[4], G[5], G[6], G[7] }, Edge = WHITE, Under = G[3], Outline = Rb[0], Shadow = Rb[1] });
            // the seal: drops in big and faint, stamps with a squash and a flash, ticks, pops at the end
            double dk = Seg(t, STAMP - 0.12, STAMP), pk = t - POPSEAL;
            if (dk > 0 && pk < 0.08)
            {
                double fall = 1 - Ease.InQuad(dk), sq = Exp(-(t - STAMP) * 30) * (t >= STAMP ? 1 : 0);
                double sc = 1 + 0.8 * fall, sx = sc * (1 + 0.2 * sq), sy = sc * (1 - 0.2 * sq);
                if (pk > 0) sx = sy = 1 + 0.3 * (pk / 0.08);
                bool white = (t >= STAMP && t < STAMP + 0.05) || pk > 0;
                if (dk < 1) p.Ellipse(SX, SY + 12, 10 * (1 - 0.3 * fall), 2, Rb[0], 0.5 * dk);
                Seal(p, G, SX + jx, SY - 14 * fall + jy, 11, sx, sy, white, Seg(t, STAMP + 0.08, STAMP + 0.24));
                p.Glow(SX, SY, 22, G[5], 0.3 + (white ? 0.4 : 0));
            }
            double rk = (t - STAMP) / 0.3;
            double rr = 12 + 13 * Ease.OutCubic(rk);
            if (rk > 0 && rk < 1) p.Ellipse(SX, SY, rr * 1.15, rr, rk < 0.3 ? WHITE : G[5], (d, g) => d > 1 - 1.4 / rr ? 1.2 - rk : 0);
            // stars pop round the banner one after another
            foreach (Sat s in sats)
            {
                double u = (t - s.t0) / s.dur;
                if (u > 0 && u < 1) Sim.Sparkle(p, s.x, s.y, s.L * Px.Pow(Px.Sin(u * Math.PI), 0.7), G[6], G[5], 0.5);
            }
            ps.Draw(p);
            p.Glow(0, 0, 76, G[4], 0.2 * Seg(t, 0.2, 0.5) * (1 - Seg(t, ROLL, ROLL + 0.2)));
            lights.Draw(p);
        }
    }
}
