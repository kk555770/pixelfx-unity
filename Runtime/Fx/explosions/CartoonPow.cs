using System;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class CartoonPow : Effect
    {
        const int W = 184, H = 144, OX = 92, OY = 72;

        public static readonly Module Module = new Module("cartoon-pow", "pixelfx/fx/explosions/cartoon-pow", W, H, OX, OY, 1.05, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0e14", "#6e1a12", "#c8361a", "#f47a24", "#ffc23c", "#fff07a", "#ffffff" },
                ["text"] = "POW!",
                ["seed"] = 5,
            },
            (o, e) => new CartoonPow(o, e))
        {
            Title = "Cartoon POW",
            Desc = "A comic-book hit: a jagged two-tone burst star slams in with a shake and boiling edges, POW! drops in letter by letter, action lines fly, then it all snaps away.",
            Tags = new[] { "cartoon", "comic", "pow", "hit", "impact", "text", "one-shot" },
            Bg = "dark",
        };

        const int N = 14;
        const double IN = 0.05, GO = 0.66, END = 0.8;
        static readonly int[][] AROUND = { new[] { -2, 0 }, new[] { 2, 0 }, new[] { 0, -2 }, new[] { 0, 2 }, new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { 1, 1 } };

        sealed class Spike
        {
            public double r, j;
        }

        sealed class ActionLine
        {
            public double a, r, L, t0;
        }

        readonly uint[] C;
        readonly uint Ink;
        readonly Rng R;
        readonly Particles ps;
        readonly Spike[] spikes;
        readonly ActionLine[] lines;
        readonly string text;
        double t;
        bool popped, burst;

        CartoonPow(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            R = new Rng(o.Seed);
            Ink = C[0];
            text = o["text"] == null ? "POW!" : Convert.ToString(o["text"], System.Globalization.CultureInfo.InvariantCulture);
            ps = new Particles(rng: R, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            // the star's spikes: long and short in turn, each its own length, boiling a little every few frames
            spikes = new Spike[N * 2];
            for (int i = 0; i < N * 2; i++) spikes[i] = new Spike { r = i % 2 != 0 ? R.Range(0.5, 0.6) : R.Range(0.85, 1.05), j = R.Next() * 100 };
            lines = new ActionLine[16];
            for (int i = 0; i < 16; i++) lines[i] = new ActionLine { a = ((i + R.Range(0.2, 0.8)) / 16) * TAU, r = R.Range(0.92, 1.08), L = R.Range(10, 17), t0 = R.Range(0, 0.06) };
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!burst && t >= IN)
            {
                burst = true;
                for (int i = 0; i < 22; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(180, 360);
                    ps.Spawn(P.Spark, Px.Cos(a) * 14, Px.Sin(a) * 10, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.12, 0.3));
                }
            }
            // it snaps away into a scatter of little stars
            if (!popped && t >= GO)
            {
                popped = true;
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(60, 150);
                    ps.Spawn(P.Star, Px.Cos(a) * 26, Px.Sin(a) * 18, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.42), R.Pick(new[] { C[6], C[5], C[4] }), R.Range(1.2, 2.4));
                }
            }
            ps.Update(dt);
            Cull(ps, -OX + 3, -OY + 3, W - OX - 3, H - OY - 3);
        }

        public override void Draw(Painter p)
        {
            double k = Clamp(t / IN);
            // the slam: it punches in past full size, shudders and settles; then it squeezes and snaps away
            double s = t < IN ? 0.25 + 0.95 * Ease.OutCubic(k) : t < GO ? 1 + 0.18 * Exp(-(t - IN) * 18) * Px.Cos((t - IN) * 40) : 1 - Ease.InBack(Clamp((t - GO) / (END - GO)), 2);
            double shake = t < 0.3 ? (1 - t / 0.3) * 2.2 : 0, sx = Round(Px.Sin(t * 97) * shake), sy = Round(Px.Cos(t * 83) * shake);
            if (s > 0.02)
            {
                // the burst star, in one pass: its edge at each angle runs straight from spike tip to notch. An inked edge, a
                // yellow body with halftone dots on its shaded side, an inked orange heart and a yellow core
                double boil = Math.Floor(t * 12), outer = 50 * s, rot = -0.1 + (t > GO ? (t - GO) * 3 : 0);
                int M = N * 2;
                var rad = new double[M];
                for (int i = 0; i < M; i++) rad[i] = spikes[i].r * (1 + (Rand(i, boil, 7) - 0.5) * 0.08);
                p.Shade(sx - outer - 4, sy - outer - 4, sx + outer + 4, sy + outer + 4, (x, y, o) =>
                {
                    double dx = x - sx, dy = (y - sy) / 0.82, d = Hypot(dx, dy);
                    double u = (((Atan2(dy, dx) - rot) / TAU) % 1 + 1) % 1 * M;
                    int i = (int)(Math.Floor(u) % M);
                    double f = u - Math.Floor(u), r = rad[i] + (rad[(i + 1) % M] - rad[i]) * f, e = d / r;
                    if (e > outer + 2.5) return 0;
                    if (e > outer) return Ink;
                    if (e > outer * 0.66 + 2) return dx * 0.6 + dy * 0.8 > outer * 0.25 && (Math.Floor(x) + Math.Floor(y) * 2) % 4 == 0 && Math.Floor(y) % 2 == 0 ? C[4] : C[5];
                    if (e > outer * 0.66) return Ink;
                    return e > outer * 0.44 ? C[3] : C[4];
                });
                p.Glow(sx, sy, outer * 1.3, C[5], 0.3 * Math.Min(1, s));
            }
            // action lines flying out, and a ring of impact
            foreach (ActionLine q in lines)
            {
                double u = (t - IN - q.t0) / 0.32;
                if (u <= 0 || u >= 1) continue;
                double r0 = (46 + 22 * Ease.OutCubic(u)) * q.r, L = q.L * 0.7 * (1 - u), ca = Px.Cos(q.a), sa = Px.Sin(q.a) * 0.82;
                p.Line(ca * r0, sa * r0, ca * (r0 + L), sa * (r0 + L), v => v < 0.5 ? WHITE : C[5]);
            }
            double ru = (t - IN) / 0.24;
            if (ru > 0 && ru < 1) p.Ring(0, 0, 30 + 34 * Ease.OutCubic(ru), Math.Max(1, 2.5 * (1 - ru)), ru < 0.4 ? WHITE : C[5], 1.1 - ru);
            // the word: letters dropping in one after another, inked, with a deep red extrusion, then a squash as it goes
            const double scale = 3;
            string word = text;
            var widths = new double[word.Length];
            double sum = -scale;
            for (int i = 0; i < word.Length; i++)
            {
                widths[i] = p.Measure(word[i].ToString(), "big", scale).w;
                sum = sum + widths[i] + scale;
            }
            double lxAt = -sum / 2;
            for (int i = 0; i < word.Length; i++)
            {
                string ch = word[i].ToString();
                double u = (t - IN - 0.04 - i * 0.05) / 0.12, away = Clamp((t - GO) / (END - GO - 0.04));
                if (u > 0 && away < 1)
                {
                    double drop = (1 - Ease.OutBack(Clamp(u), 2.4)) * -14, y = Round(-11 + drop - i * 1.5 + sy + away * 6);
                    double lx = Round(lxAt + sx), a = 1 - away;
                    // a thick ink edge, a deep red extrusion, a white face going warm toward its foot
                    for (int d = 4; d >= 1; d--) p.Text(ch, lx + d, y + d, d >= 3 ? Ink : C[1], font: "big", scale: scale, a: a, outline: d == 4 ? Ink : (uint?)null);
                    foreach (int[] off in AROUND) p.Text(ch, lx + off[0], y + off[1], Ink, font: "big", scale: scale, a: a);
                    p.Text(ch, lx, y, (gx, gy) => gy < 4 ? C[6] : gy < 6 ? C[5] : C[4], font: "big", scale: scale, a: a);
                }
                lxAt += widths[i] + scale;
            }
            ps.Draw(p);
            if (t > IN && t < IN + 0.12) Sim.Sparkle(p, sx + 34, sy - 22, 6 * (1 - (t - IN) / 0.12), C[6], C[5], 0.6);
        }
    }
}
