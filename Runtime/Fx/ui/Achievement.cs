using System;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class Achievement : Effect
    {
        const double DURATION = 2.4;

        public static readonly Module Module = new Module("achievement", "pixelfx/fx/ui/achievement", 220, 72, 110, 36, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1405", "#5c3008", "#9a5a0e", "#d48e18", "#f5b92c", "#ffde5e", "#fff4bc", "#ffffff" },
                ["ribbon"] = new[] { "#0c1430", "#1f3a7a", "#2f66c4", "#5aa0f0", "#a6d4ff" },
                ["title"] = "ACHIEVEMENT",
                ["text"] = "UNLOCKED",
                ["seed"] = 9,
            },
            (o, e) => new Achievement(o, e))
        {
            Title = "Achievement",
            Desc = "A toast slides in on speed lines and settles; a gold medal pops onto it in a burst of sparkles, a shine sweeps the whole toast, then it slides away.",
            Tags = new[] { "ui", "achievement", "toast", "medal", "reward", "text", "one-shot" },
            Bg = "dark",
        };

        const double PW = 130, PH = 34, PX = -PW / 2, PY = -PH / 2, IN = 0.2, MEDAL = 0.26, SHINE = 0.95, OUT = 1.92;
        const double MX = PX + 17, MY = 2;
        static readonly Plate INK = new Plate { Outline = Hex("#0d0a14"), Rim = Hex("#5a4f74"), Top = Hex("#2a2338"), Body = Hex("#211b2d"), Low = Hex("#15111c") };

        // the medal: ribbon straps behind it, a scalloped-free gold disc with a lit rim and a raised star
        static void Medal(Painter p, uint[] G, uint[] B, double x, double y, double s, bool white, double? shine)
        {
            double r = 10 * s;
            foreach (int sd in new[] { -1, 1 })
            {
                // a strap from the medal up to the toast's top edge
                p.Poly(new[] { new[] { x + sd * 2 * s, y - r + 2 }, new[] { x + sd * 7 * s, y - r + 2 }, new[] { x + sd * 9 * s, PY + 1 }, new[] { x + sd * 4 * s, PY + 1 } },
                    (px, py) => Math.Abs(px - x) < 4.5 * s ? B[2] : B[3]);
                p.Line(x + sd * 2 * s, y - r + 2, x + sd * 4 * s, PY + 1, B[0]);
                p.Line(x + sd * 7 * s, y - r + 2, x + sd * 9 * s, PY + 1, B[0]);
            }
            double[][] st = Shapes.Star(x, y + 0.5, 2.8 * s, 6.5 * s, 5);
            bool InStar(double px, double py)
            {
                bool inside = false;
                for (int i = 0, j = st.Length - 1; i < st.Length; j = i++)
                {
                    double xi = st[i][0], yi = st[i][1], xj = st[j][0], yj = st[j][1];
                    if (yi > py != yj > py && px < ((xj - xi) * (py - yi)) / (yj - yi) + xi) inside = !inside;
                }
                return inside;
            }
            p.Shade(x - r - 1, y - r - 1, x + r + 1, y + r + 1, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, d = Hypot(dx, dy);
                if (d > r) return 0;
                if (white) return r - d < 1 ? G[6] : WHITE;
                if (r - d < 1) return G[0];
                double l = -(dx * 0.6 + dy * 0.8) / r;
                if (shine is double sh && Math.Abs(dx - dy * 0.6 - sh) < 1.2) return WHITE;
                if (r - d < 2.5) return l > 0.2 ? G[6] : l > -0.3 ? G[4] : G[3];
                // the raised star: a dark edge, a lit upper-left face, a shaded lower-right one
                if (InStar(px, py)) return !InStar(px + 1, py) || !InStar(px - 1, py) || !InStar(px, py + 1) || !InStar(px, py - 1) ? G[2] : dx + dy < 0 ? G[7] : G[6];
                return l > 0.4 ? G[5] : l > -0.2 ? G[4] : G[3];
            });
        }

        readonly uint[] G, B;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Word title, sub;
        const double TX = PX + 34;
        const double END = DURATION - 0.05;
        double t;
        bool popped;

        Achievement(Options o, Emitter e)
        {
            G = Fit(o.Ramp("palette"), 8);
            B = Fit(o.Ramp("ribbon"), 5);
            R = new Rng(o.Seed);
            ps = new Particles(R, sparks: new[] { G[7], G[6], G[5], G[3] }, emitter: e);
            lights = new Lights(e);
            title = Spell(o.Str("title"), "mini");
            sub = Spell(o.Str("text"), "mini");
        }

        // the toast slides in from the right, overshoots and settles; at the end it eases back and slides off
        static double XAt(double t) => t < IN ? 30 * Px.Pow(1 - t / IN, 3)
            : t < OUT ? -3 * Exp(-(t - IN) * 12) * Px.Sin((t - IN) * 30)
            : Seg(t, OUT, OUT + 0.06) < 1 ? -3 * Seg(t, OUT, OUT + 0.06)
            : -3 + 33 * Ease.InQuad(Seg(t, OUT + 0.06, OUT + 0.24));

        public override void Update(double dt)
        {
            t += dt;
            if (!popped && t >= MEDAL + 0.06)
            {
                popped = true;
                lights.Add(MX, MY, 36, G[5], 0.7, 0.4);
                for (int i = 0; i < 12; i++)
                {
                    double an = ((double)i / 12) * TAU + R.Range(-0.2, 0.2), sp = R.Range(40, 90);
                    ps.Spawn(P.Star, MX + Px.Cos(an) * 9, MY + Px.Sin(an) * 9, Px.Cos(an) * sp, Px.Sin(an) * sp * 0.6, R.Range(0.25, 0.45), R.Pick(new[] { G[7], G[6], G[5] }), i % 3 != 0 ? 0.6 : R.Range(1.2, 1.8));
                }
            }
            if (t > 0.5 && t < OUT && R.Chance(0.05)) ps.Spawn(P.Star, MX + R.Range(-11, 11), MY + R.Range(-11, 9), 0, -R.Range(4, 10), Math.Min(R.Range(0.3, 0.5), END - t), G[6], R.Chance(0.3) ? 1.4 : 0.6);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            double x = XAt(t);
            bool outK = t > OUT + 0.06;
            double a = t < 0.05 ? 0.5 : outK ? 1 - Seg(t, OUT + 0.12, OUT + 0.24) : 1;
            if (a <= 0) return;
            // speed lines trail it while it moves
            bool moving = t < IN * 0.8 || outK;
            if (moving)
                for (int i = 0; i < 8; i++)
                {
                    double f = Math.Floor(t * 30);
                    double y = PY + 3 + Rand(f, i, 1) * (PH - 6), L = 10 + Rand(f, i, 2) * 26, x0 = x + PW / 2 + 2 + Rand(i, 3) * 6;
                    p.Line(x0, y, Math.Min(106, x0 + L), y, u => u < 0.3 ? WHITE : u < 0.6 ? G[6] : G[4], u => a * (1.1 - u));
                }
            // the plate and its gold edge; a shine sweeps the whole toast
            double sh = Seg(t, SHINE, SHINE + 0.36);
            double? shine = sh > 0 && sh < 1 ? PX - 12 + (PW + 30) * Ease.InOutSine(sh) : (double?)null;
            Panel(p, PX + x, PY, PW, PH, INK, 4, a, shine == null ? (double?)null : shine.Value + x);
            p.Line(PX + x + 4, PY + 1, PX + x + PW - 5, PY + 1, G[4], a);
            p.Px(PX + x + 3, PY + 1, G[2]);
            // the words: a gold title, a white line under it
            var ti = title.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = TX + x + L.X * 2, Y = PY + 5, Sx = 2, Sy = 2, A = a }).ToList();
            DrawWord(p, ti, new Style { Fill = new[] { G[4], G[5], G[6] }, Edge = G[7], Under = G[3], Outline = INK.Outline, Shadow = 0, Shine = shine == null ? (double?)null : shine.Value + x, ShineY = PY, Glint = G[7] });
            var si = sub.Letters.Where(L => L.G != null).Select(L => new Item { G = L.G, X = TX + x + L.X * 2, Y = PY + 19, Sx = 2, Sy = 2, A = a }).ToList();
            DrawWord(p, si, new Style { Fill = new[] { Hex("#9a94b0"), Hex("#d8d4e6"), WHITE }, Edge = WHITE, Under = Hex("#6e6886"), Outline = INK.Outline, Shadow = 0, Shine = shine == null ? (double?)null : shine.Value + x, ShineY = PY });
            // the medal pops onto it: big and white for a frame, then springs to size
            double mk = t - MEDAL;
            if (mk > 0)
            {
                double s = mk < 0.035 ? 1.5 : 1 + 0.35 * Exp(-mk * 16) * Px.Cos(mk * 28);
                Medal(p, G, B, MX + x, MY, s, mk < 0.07, shine == null ? (double?)null : shine.Value - MX + 4);
                p.Glow(MX + x, MY, 20, G[5], 0.3 * a + (mk < 0.15 ? 0.5 * (1 - mk / 0.15) : 0));
                double g = ((t - 1.5) % 0.9) / 0.9;
                if (t > 1.5 && g < 0.25) Sim.Sparkle(p, MX + x - 4, MY - 4, 4 * Px.Sin((g / 0.25) * Math.PI), G[7], G[6], 0.5);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
