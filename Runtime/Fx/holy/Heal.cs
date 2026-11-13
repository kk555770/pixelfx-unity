using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class Heal : Effect
    {
        static readonly Pose HURT = new Pose { ["turn"] = 0.8, ["crouch"] = 5, ["lean"] = 3, ["hy"] = 1, ["fx"] = 1, ["fy"] = -21, ["bx"] = 7, ["by"] = -15, ["ffx"] = 4, ["fbx"] = -3 };
        static readonly Pose UP = new Pose { ["turn"] = 1.15, ["crouch"] = -1, ["fx"] = -13, ["fy"] = -36, ["bx"] = 14, ["by"] = -36, ["hy"] = -1, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.3, HURT),
                new PoseKey(0.16, new Pose(HURT) { ["crouch"] = 6 }),
                new PoseKey(0.5, new Pose(HURT) { ["crouch"] = 3, ["lean"] = 1, ["hy"] = 0 }),
                new PoseKey(0.78, UP),
                new PoseKey(1.2, new Pose(UP) { ["crouch"] = 0, ["fy"] = -30, ["by"] = -30, ["hy"] = 0 }),
                new PoseKey(1.9, Poses.READY),
            },
        };

        public static readonly Module Module = new Module("heal", "pixelfx/fx/holy/heal", 80, 104, 40, 92, 1.9, false, 30,
            new Options { ["palette"] = new[] { "#3d2027", "#7d3f35", "#c97a4a", "#f2b46a", "#ffd994", "#fff0cf", "#fffaf2", "#ffffff" }, ["seed"] = 2 },
            (o, e) => new Heal(o, e), act: Act, actor: 2)
        {
            Title = "Heal",
            Desc = "Petals of light bloom under the feet, a hoop of light rises up the body restoring its colour and leaving sparkles, and motes spiral up and pop into tiny hearts.",
            Tags = new[] { "holy", "heal", "restore", "buff", "petals", "hearts", "character", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.3, PR = 16, LIFT0 = 0.16, LIFT1 = 0.74, TOPY = -43;
        const int NP = 8;
        static readonly string[] HEART = { ".#.#.", "#####", ".###.", "..#.." };

        // a tiny heart of light: cream body, a white glint, a warm rim
        static void Heart(Painter p, double x, double y, uint[] C, double a)
        {
            double X = Round(x) - 2, Y = Round(y) - 2;
            bool At(int i, int j) => j >= 0 && j < 4 && i >= 0 && i < 5 && HEART[j][i] == '#';
            for (int j = -1; j <= 4; j++)
                for (int i = -1; i <= 5; i++)
                {
                    if (At(i, j)) p.Dot(X + i, Y + j, i == 1 && j == 1 ? WHITE : j >= 2 ? C[5] : C[6], a);
                    else if (At(i - 1, j) || At(i + 1, j) || At(i, j - 1) || At(i, j + 1)) p.Dot(X + i, Y + j, C[3], a * 0.9);
                }
            p.Glow(x, y, 6, C[5], 0.4 * a);
        }

        sealed class Petal
        {
            public double a, t0, w;
        }

        sealed class Mote
        {
            public double t0, th0, r0, y0, w, v, life;
            public int kind;
        }

        readonly uint[] C;
        readonly Actor A;
        readonly Shape S;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Dictionary<int, List<int>> rows = new Dictionary<int, List<int>>();
        readonly Petal[] petals;
        readonly List<Mote> motes = new List<Mote>();
        double t;
        bool burst;
        double lastRow = 1;

        Heal(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            A = o.Actor;
            S = ShapeOf(A);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            foreach (int[] q in S.Edge)
            {
                if (!rows.TryGetValue(q[1], out List<int> xs)) rows[q[1]] = xs = new List<int>();
                xs.Add(q[0]);
            }
            // petals, each blooming on its own beat round the ring
            petals = new Petal[NP];
            for (int k = 0; k < NP; k++) petals[k] = new Petal { a = ((double)k / NP) * TAU + 0.2, t0 = 0.02 + ((k * 3) % NP) * 0.035, w = R.Range(4.2, 5.2) };
            // motes riding a spiral up round the body: from the petal tips as they open, and from the hoop when it bursts
            for (int k = 0; k < NP; k++) motes.Add(new Mote { t0 = 0.2 + k * 0.05, th0 = petals[k].a, r0 = PR * 0.9, y0 = -1, w = 4.2 * (k % 2 != 0 ? 1 : -1), v = R.Range(46, 58), life = R.Range(0.75, 0.95), kind = k % 2 });
            for (int k = 0; k < 8; k++) motes.Add(new Mote { t0 = LIFT1 + R.Range(0, 0.06), th0 = (k / 8.0) * TAU, r0 = R.Range(12, 18), y0 = TOPY + R.Range(-2, 4), w = 3.6 * (k % 2 != 0 ? 1 : -1), v = R.Range(14, 34), life = R.Range(0.35, 0.6), kind = (k + 1) % 2 });
        }

        static double HoopY(double tt) => Lerp(0, TOPY, Ease.InOutSine(Seg(tt, LIFT0, LIFT1)));
        static double HoopR(double tt) => Lerp(PR, 12, Seg(tt, LIFT0, LIFT1));

        double Bloom(int k, double tt)
        {
            Petal pe = petals[k];
            return Ease.OutBack(Seg(tt, pe.t0, pe.t0 + 0.3), 1.8) * (1 - Ease.InBack(Seg(tt, 1.25 + k * 0.02, 1.6 + k * 0.02), 1.4));
        }

        // a petal of a lotus of light round the feet: rooted on a ring on the floor, leaning out as it opens,
        // foreshortened as it turns away; a lit rim, a white vein, a translucent warm body, a white tip
        void DrawPetal(Painter p, int k, bool back)
        {
            Petal pe = petals[k];
            double g = pe.a + t * 0.4;
            bool near = Px.Sin(g) >= 0;
            double b = Bloom(k, t);
            if (near == back || b <= 0.02) return;
            double ca = Px.Cos(g), sa = Px.Sin(g), rb = 6, rt = rb + 9 * Math.Min(1.2, b), h = (k % 2 != 0 ? 7 : 9) * b;
            double bx = ca * rb, by = sa * rb * SY, tx = ca * rt, ty = sa * rt * SY - h, L = Hypot(tx - bx, ty - by);
            if (L < 1.5) return;
            double ux = (tx - bx) / L, uy = (ty - by) / L, W = pe.w * Math.Min(1, b) * (0.35 + 0.65 * Math.Abs(sa)), pad = W + 1;
            p.Each(Math.Min(bx, tx) - pad, Math.Min(by, ty) - pad, Math.Max(bx, tx) + pad, Math.Max(by, ty) + pad, (x, y, sx, sy) =>
            {
                double dx = x - bx, dy = y - by, u = (dx * ux + dy * uy) / L;
                if (u < 0 || u > 1) return;
                double hw = W * Px.Pow(Px.Sin(Math.PI * Px.Pow(u, 0.6)), 0.7) + 0.2, v = Math.Abs(-dx * uy + dy * ux) / hw;
                if (v > 1) return;
                if (u > 0.9)
                {
                    p.Put(sx, sy, WHITE, 1);
                    return;
                }
                if (v > 0.7)
                {
                    p.Put(sx, sy, u < 0.3 ? C[3] : u < 0.7 ? C[5] : C[6], 1);
                    return;
                }
                if (v < 0.2 && u > 0.15 && u < 0.85)
                {
                    p.Put(sx, sy, u < 0.55 ? C[5] : C[6], back ? 0.75 : 0.85);
                    return;
                }
                p.BlendPx(sx, sy, u < 0.45 ? C[3] : C[4], back ? 0.6 : 0.45);
            });
            p.Glow(tx, ty, 5, C[5], 0.35 * Math.Min(1, b));
        }

        // the hoop of light rising up the body: far half behind, near half in front, a soft glow round it
        void Hoop(Painter p, bool back)
        {
            if (t < LIFT0 || t > LIFT1 + 0.12) return;
            double y = HoopY(t), r = HoopR(t) * (t > LIFT1 ? 1 - (t - LIFT1) / 0.12 : 1);
            Func<double, bool> keep = back ? (Func<double, bool>)(g => Px.Sin(g) < 0) : g => Px.Sin(g) >= 0;
            Oval(p, 0, y, r, r * SY, g => Px.Sin(g) > 0.5 ? WHITE : C[6], back ? 0.8 : 1, keep);
            if (!back)
            {
                Oval(p, 0, y + 1, r - 0.5, r * SY, C[4], 1, keep);
                for (int i = -2; i <= 2; i++) p.Glow(i * r * 0.45, y, 8, C[5], 0.32);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the hoop leaves sparkles on the body as it passes each row
            if (t > LIFT0 && t < LIFT1)
            {
                double Y = Round(HoopY(t));
                for (double y = lastRow; y > Y; y--)
                {
                    rows.TryGetValue((int)y, out List<int> xs);
                    if (xs == null || !R.Chance(0.45)) continue;
                    ps.Spawn(P.Star, R.Pick(xs.ToArray()) + R.Range(-1, 1) * 2, y, R.Range(-4, 4), -R.Range(4, 12), R.Range(0.3, 0.55), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.6));
                }
                lastRow = Math.Min(lastRow, Y);
            }
            if (!burst && t >= LIFT1)
            {
                burst = true;
                lights.Add(0, TOPY, 26, C[5], 0.55, 0.3);
                lights.Add(0, TOPY, 10, WHITE, 0.45, 0.08);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // a warm pool under the flower
            double pool = Smooth(0, 0.2, t) * (1 - Smooth(1.3, 1.75, t));
            if (pool > 0) p.Glow(0, -2, 30, C[4], 0.3 * pool);
            for (int k = 0; k < NP; k++) DrawPetal(p, k, true);
            Hoop(p, true);
            ps.Draw(p, Layer.Back);
            Riders(p, true);
        }

        public override void Draw(Painter p)
        {
            // drained of colour above the hoop, restored below it with a warm glow at the line
            double drain = Smooth(0, 0.08, t);
            if (t < LIFT1 + 0.35)
            {
                double Y = t < LIFT0 ? 1 : HoopY(t);
                bool done = t >= LIFT1;
                Recolor(p, A, (c, x, y) =>
                {
                    double d = y - Y;
                    if (!done && d < -0.5)
                    {
                        double l = Luma(c) * 0.88;
                        return Mix(c, Rgb(l * 236, l * 226, l * 222), 0.68 * drain);
                    }
                    if (!done && d < 1) return WHITE;
                    if (!done && d < 2.5) return C[6];
                    double k = done ? 0.45 * (1 - Seg(t, LIFT1, LIFT1 + 0.35)) : 0.5 * Exp(-(d - 2.5) / 5);
                    return k > 0.05 ? Mix(c, C[5], k) : 0;
                });
                if (t > LIFT0 && !done) p.Glow(0, Y, 18, C[5], 0.35);
            }
            for (int k = 0; k < NP; k++) DrawPetal(p, k, false);
            Hoop(p, false);
            if (t >= LIFT1 && t < LIFT1 + 0.25) Sim.Sparkle(p, 0, TOPY, 8 * (1 - (t - LIFT1) / 0.25), WHITE, C[5], 0.6);
            Riders(p, false);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }

        // the spiralling motes, each popping into a heart or a sparkle at the end of its ride
        void Riders(Painter p, bool back)
        {
            foreach (Mote m in motes)
            {
                double a = t - m.t0;
                if (a < 0) continue;
                (double x, double y, double z) At(double aa)
                {
                    double th = m.th0 + m.w * aa, r = m.r0 * (1 - 0.45 * (aa / m.life));
                    return (Px.Cos(th) * r, m.y0 - m.v * aa, Px.Sin(th));
                }
                if (a < m.life)
                {
                    var (x, y, z) = At(a);
                    if ((z < 0) != back) continue;
                    for (int j = 0; j < 7; j++)
                    {
                        double aj = a - j * 0.03;
                        if (aj < 0) break;
                        var (tx, ty, tz) = At(aj);
                        if ((tz < 0) != back) continue;
                        p.Dot(tx, ty, j == 0 ? WHITE : j < 3 ? C[6] : C[5], (1 - j / 7.0) * (back ? 0.6 : 1));
                    }
                    p.Glow(x, y, 4, C[5], back ? 0.2 : 0.4);
                    continue;
                }
                // the pop: a quick sparkle, then a heart (or a star) that floats up and fades
                double u = (a - m.life) / 0.36;
                if (u >= 1 || back) continue;
                var (px, py, _) = At(m.life);
                double kk = 1 - Smooth(0.5, 1, u);
                if (u < 0.12) Sim.Sparkle(p, px, py, 3, WHITE, C[6], 0.5);
                else if (m.kind != 0) Heart(p, px, py - u * 6, C, kk);
                else Sim.Sparkle(p, px, py - u * 5, 1 + 2 * kk * (0.7 + 0.3 * Px.Sin(a * 30)), WHITE, C[5], 0.45 * kk);
            }
        }
    }
}
