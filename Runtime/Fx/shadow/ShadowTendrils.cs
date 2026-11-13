using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class ShadowTendrils : Effect
    {
        public static readonly Module Module = new Module("shadow-tendrils", "pixelfx/fx/shadow/shadow-tendrils", 208, 130, 104, 106, 3.2, false, 30,
            new Options { ["palette"] = VOID, ["seed"] = 3 },
            (o, e) => new ShadowTendrils(o, e))
        {
            Title = "Shadow tendrils",
            Desc = "A dark pool opens on the floor; tentacles with lit rims and hooked tips lash up out of it with a splash, writhe, and sink back as it closes.",
            Tags = new[] { "shadow", "tentacle", "tendril", "ground", "dark", "magic", "one-shot" },
            Bg = "ground",
        };

        const double RX = 40, OPEN = 0.34, SHUT = 2.5, SINK = 0.5, LX = -0.6, LY = -0.8;

        sealed class Whip
        {
            public double t, a;
        }

        sealed class Tent
        {
            public double x, y, L, W, lean, curl, lam, om, ph, A, H, t0, t1;
            public bool back, up, down;
            public Whip whip;
        }

        sealed class SPt
        {
            public double x, y, th, w, u, s;
        }

        // a tentacle's spine at t: it rises out of the pool along its own curve, a wave writhes up it, its lean sways
        // and sometimes whips, and its tip curls into a hook; points every 0.5 px with angle and half-width
        static List<SPt> Spine(Tent T, double t)
        {
            double e = Clamp((t - T.t0) / 0.2), s1 = Clamp((t - T.t1) / SINK);
            if (e <= 0 || s1 >= 1) return null;
            // it lashes up past its length and settles; going, it lifts a little, then slides back under
            double lift = 0.07 * Px.Sin(Math.PI * Clamp(s1 / 0.3)), down = Ease.InOutCubic(Clamp((s1 - 0.12) / 0.88));
            double rise = Ease.OutBack(e, 1.8) * (1 + lift) * (1 - down), hidden = (1 - rise) * T.L;
            double writhe = T.A * (0.25 + 0.75 * Smooth(T.t0 + 0.08, T.t0 + 0.5, t));
            double hook = (0.04 + T.H * Ease.OutBack(Clamp((t - T.t0 - 0.1) / 0.32), 2.5)) * (1 + 0.5 * s1) * (1 + 0.18 * Px.Sin(t * 2.3 + T.ph));
            double lean = T.lean + 0.12 * Px.Sin(t * 1.6 + T.ph);
            if (T.whip != null) lean += T.whip.a * Exp(-Px.Pow((t - T.whip.t) / 0.14, 2));
            double th = -Math.PI / 2 + lean, x = T.x - Px.Cos(th) * hidden, y = T.y - Px.Sin(th) * hidden;
            var pts = new List<SPt>();
            double n = Math.Ceiling(T.L / 0.5);
            for (int i = 0; i <= n; i++)
            {
                double s = i * 0.5, u = s / T.L;
                pts.Add(new SPt { x = x, y = y, th = th, w = T.W * Px.Pow(1 - u, 0.75) + 0.45, u = u, s = s });
                th += 0.5 * (writhe * Px.Sin((s / T.lam) * TAU - T.om * t + T.ph) + T.curl * hook * Smooth(0.58, 1, u));
                x += Px.Cos(th) * 0.5;
                y += Px.Sin(th) * 0.5;
            }
            return pts;
        }

        // a tentacle in cross-sections: a dark body with a lighter half toward the light, then the rims: bright where it
        // faces the light (white where it faces it squarely), violet on the side the pool lights, suckers inside the hook
        static void Tentacle(Painter p, Tent T, List<SPt> pts, uint[] C, double t)
        {
            double surf = T.y;
            foreach (SPt q in pts)
            {
                if (q.y > surf) continue;
                double nx = -Px.Sin(q.th), ny = Px.Cos(q.th), ks = nx * LX + ny * LY >= 0 ? 1 : -1;
                for (double v = -q.w; v <= q.w; v += 0.5)
                {
                    double x = q.x + nx * v, y = q.y + ny * v;
                    double u = (v / q.w) * ks;
                    if (y <= surf) p.Px(x, y, u > 0.55 ? C[2] : u > -0.45 ? C[1] : C[0]);
                }
            }
            // a pulse of light runs up it as it lashes, then slower ones while it writhes
            double pulse = (t - T.t0) * 150, slow = (((t - T.t0) * 55) % (T.L * 1.8)) - 4;
            foreach (SPt q in pts)
            {
                if (q.y > surf) continue;
                double nx = -Px.Sin(q.th), ny = Px.Cos(q.th), f = nx * LX + ny * LY, ks = f >= 0 ? 1 : -1;
                double lit = ks * q.w, x0 = q.x + nx * lit, y0 = q.y + ny * lit, x1 = q.x - nx * lit, y1 = q.y - ny * lit;
                if (y1 <= surf) p.Px(x1, y1, C[3]);
                if (y0 <= surf) p.Px(x0, y0, Math.Abs(f) > 0.8 && q.u > 0.15 ? C[7] : q.u > 0.85 ? C[6] : Math.Abs(f) > 0.3 ? C[6] : C[5]);
                if (q.u > 0.62 && q.u < 0.93 && q.s % 3 < 0.5) p.Px(q.x + nx * T.curl * q.w * 0.45, q.y + ny * T.curl * q.w * 0.45, C[4]);
                double dp = Math.Min(Math.Abs(q.s - pulse), Math.Abs(q.s - slow) * 1.6);
                if (dp < 2.5) p.Px(q.x, q.y, dp < 1 ? C[7] : C[5]);
            }
            // the tip catches the light, and light runs along it
            SPt tip = pts[pts.Count - 1];
            if (tip.y <= surf)
            {
                p.Px(tip.x, tip.y, WHITE);
                p.Glow(tip.x, tip.y, 6, C[6], 0.25);
            }
            for (int i = 0; i < pts.Count; i += 8)
                if (pts[i].y < surf) p.Glow(pts[i].x, pts[i].y, 9, C[4], 0.1);
            SPt pq = pts[(int)Math.Min(pts.Count - 1, Math.Max(0, Round(pulse * 2)))];
            if (pulse < T.L && pq.y < surf) p.Glow(pq.x, pq.y, 10, C[6], 0.45);
            // where it breaks the surface: a lit collar of ink
            SPt b = pts.Find(q => q.y <= surf);
            if (b != null)
            {
                double w = b.w + 1.5;
                p.Line(b.x - w, surf, b.x + w, surf, C[5]);
                p.Px(b.x - w - 1, surf + 0.5, C[3]);
                p.Px(b.x + w + 1, surf + 0.5, C[3]);
            }
        }

        readonly uint[] C, DIM;
        readonly Rng R;
        readonly Particles ps;
        readonly Puffs puffs;
        readonly Drops drops;
        readonly List<Ripple> ripples = new List<Ripple>();
        readonly List<Splat> splats = new List<Splat>();
        readonly List<Lamp> lights = new List<Lamp>();
        readonly Tent[] tents;
        double t, acc;

        double Rx(double tt) => RX * Ease.OutCubic(Clamp(tt / OPEN)) * (1 - Ease.InCubic(Clamp((tt - SHUT) / 0.45)));
        bool InPool(double x, double tt) => Math.Abs(x) < Rx(tt) * 0.95;

        ShadowTendrils(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            DIM = new[] { C[0], C[0], C[1], C[1], C[3], C[4], C[5], C[6] };
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[4], emitter: e);
            puffs = new Puffs(R, new[] { C[7], C[6], C[5], C[4], C[3], C[2], C[1], C[1], C[0] }, new[] { Mix(C[3], C[2], 0.55), Mix(C[2], C[1], 0.5) }, Mix(C[4], C[2], 0.5), new[] { C[6], C[5], C[4] }, floor, e);
            drops = new Drops(onLand: (x, y, v, s) =>
            {
                if (InPool(x, t)) ripples.Add(new Ripple { x = x, t0 = t, life = 0.5, w = 0.6 });
                else splats.Add(new Splat { x = x, t0 = t, s = s });
            });
            // seven tentacles in two rows: the back row taller and dimmer, hooks mostly curling inward
            double[][] rows = { new double[] { -22, -2.5 }, new double[] { -8, -3 }, new double[] { 7, -3 }, new double[] { 21, -2.5 }, new double[] { -28, 2.5 }, new double[] { -1, 3 }, new double[] { 27, 2.5 } };
            int[] order = { 3, 0, 5, 2, 4, 1, 6 }, last = { 1, 5, 0, 6, 3, 2, 4 };
            tents = new Tent[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                double x = rows[i][0], y = rows[i][1];
                bool back = y < 0;
                int sx = x != 0 ? Sign(x) : 1;
                tents[i] = new Tent
                {
                    x = x + R.Range(-3, 3),
                    y = y,
                    back = back,
                    L = back ? R.Range(46, 62) : R.Range(32, 46),
                    W = back ? R.Range(3.2, 4) : R.Range(3.8, 4.6),
                    lean = sx * (0.06 + Math.Abs(x) / 120) + R.Range(-0.06, 0.06),
                    curl = R.Chance(0.75) ? -sx : sx,
                    lam = R.Range(36, 50),
                    om = R.Range(4.5, 7) * R.Sign(),
                    ph = R.Range(0, TAU),
                    A = R.Range(0.06, 0.085),
                    H = R.Range(0.15, 0.32),
                    t0 = 0.3 + order[i] * 0.045 + R.Range(0, 0.03),
                    t1 = 1.5 + last[i] * 0.1 + R.Range(0, 0.05),
                    whip = R.Chance(0.45) ? new Whip { t = R.Range(0.9, 1.5), a = R.Range(0.28, 0.45) * R.Sign() } : null,
                };
            }
        }

        void Splash(double x, double y, int n, double power)
        {
            for (int i = 0; i < n; i++) drops.Add(x + R.Range(-2, 2), y - 1, R.Range(-70, 70) * power, -R.Range(90, 230) * power, R.Chance(0.4) ? 2 : 1);
            ripples.Add(new Ripple { x = x, t0 = t, life = 0.7, w = 1 });
            lights.Add(new Lamp(x, y - 7, 30, 0, 0.6 * power, 0.25));
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Tent T in tents)
            {
                if (!T.up && t >= T.t0)
                {
                    T.up = true;
                    Splash(T.x, T.y, 12, 1);
                    for (int i = 0; i < 4; i++) ps.Spawn(P.Spark, T.x, T.y - 2, R.Range(-80, 80), -R.Range(120, 260), R.Range(0.2, 0.45));
                }
                if (!T.down && t >= T.t1 + SINK * 0.8)
                {
                    T.down = true;
                    Splash(T.x, T.y, 6, 0.6);
                }
                // glints run over the rims while they stand
                if (t > T.t0 + 0.2 && t < T.t1 && R.Chance(dt * 1.6))
                {
                    List<SPt> pts = Spine(T, t);
                    SPt q = pts != null ? pts[(int)Math.Floor(R.Range(0.3, 0.95) * pts.Count)] : null;
                    if (q != null && q.y < T.y) ps.Spawn(P.Star, q.x - Px.Sin(q.th) * q.w, q.y + Px.Cos(q.th) * q.w, 0, -4, R.Range(0.25, 0.5), C[6], R.Range(1, 2.2));
                }
            }
            // the pool breathes out wisps and motes while it's open
            double r = Rx(t);
            if (r > 8 && t < SHUT - 0.4)
            {
                for (acc += dt * 9; acc > 1; acc--) puffs.Spawn(PK.Soot, R.Range(-0.8, 0.8) * r, -R.Range(1, 4), R.Range(-8, 8), -R.Range(14, 30), R.Range(0.6, 0.85), R.Range(3, 6));
                if (R.Chance(dt * 16)) ps.Spawn(P.Mote, R.Range(-0.8, 0.8) * r, -2, R.Range(-8, 8), -R.Range(30, 80), R.Range(0.5, 1), R.Pick(new[] { C[7], C[6], C[6], C[5] }), R.Range(0.6, 1.1));
                if (R.Chance(dt * 5)) ps.Spawn(P.Ember, R.Range(-0.8, 0.8) * r, -3, R.Range(-10, 10), -R.Range(20, 45), R.Range(0.6, 1), C[6]);
                if (R.Chance(dt * 3)) ripples.Add(new Ripple { x = R.Range(-0.7, 0.7) * r, t0 = t, life = 0.6, w = 0.5 });
                // a violet haze hangs over the pool, lit from inside
                if (R.Chance(dt * 7)) ps.Spawn(P.Vapour, R.Range(-0.7, 0.7) * r, -R.Range(2, 8), R.Range(-6, 6), -R.Range(10, 22), R.Range(0.6, 0.9), R.Chance(0.5) ? C[3] : C[4], R.Range(3, 6));
            }
            for (int i = ripples.Count - 1; i >= 0; i--)
                if (t - ripples[i].t0 > ripples[i].life) ripples.RemoveAt(i);
            for (int i = splats.Count - 1; i >= 0; i--)
                if (t - splats[i].t0 > 0.6) splats.RemoveAt(i);
            for (int i = lights.Count - 1; i >= 0; i--)
                if (!lights[i].Tick(dt)) lights.RemoveAt(i);
            drops.Update(dt);
            puffs.Update(dt);
            ps.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double r = Rx(t), k = r / RX;
            FloorPool(p, C, r, t, ripples);
            if (k > 0)
            {
                p.Glow(0, -16, 86, C[4], 0.22 * k);
                p.Glow(0, -8, r * 1.7 + 12, C[4], 0.32 * k * (0.9 + 0.1 * Px.Sin(t * 5)));
                p.Glow(0, 0, r * 1.15, C[5], 0.18 * k);
            }
            puffs.Draw(p, PK.Soot, new PuffLight(0, 6, k, 70));
            ps.Draw(p, Layer.Back);
            foreach (Tent T in tents)
                if (T.back)
                {
                    List<SPt> pts = Spine(T, t);
                    if (pts != null) Tentacle(p, T, pts, DIM, t);
                }
        }

        public override void Draw(Painter p)
        {
            foreach (Tent T in tents)
                if (!T.back)
                {
                    List<SPt> pts = Spine(T, t);
                    if (pts != null) Tentacle(p, T, pts, C, t);
                }
            drops.Draw(p, C[1], C[5]);
            foreach (Splat s in splats)
            {
                double a = 1 - (t - s.t0) / 0.6;
                p.Dot(s.x, -0.5, C[1], a * 1.5);
                p.Dot(s.x + 1, -0.5, C[2], a);
                if (s.s > 1) p.Dot(s.x - 1, -0.5, C[2], a);
                p.Dot(s.x, -1.5, C[5], a);
            }
            ps.Draw(p, Layer.Front);
            foreach (Lamp l in lights)
            {
                double q = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.6 + 0.4 * q), C[5], l.k * q * q);
            }
        }
    }
}
