using System;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class HealAoe : Effect
    {
        public static readonly Module Module = new Module("heal-aoe", "pixelfx/fx/holy/heal-aoe", 256, 136, 128, 104, 2.9, false, 30,
            new Options { ["palette"] = GREEN, ["reach"] = 104, ["seed"] = 6 },
            (o, e) => new HealAoe(o, e))
        {
            Title = "Healing ring",
            Desc = "A seed of green light sinks into the floor and a soft ring rolls out from it; the floor glows where it passes and plus signs sprout and float up.",
            Tags = new[] { "holy", "heal", "aoe", "ring", "plus", "ground", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.2, SEED = 0.3, ROLL = 1.0;

        sealed class Site
        {
            public double x, z, t0, rise, life, sway, L, thick;
            public bool popped;
        }

        sealed class Rider
        {
            public double a, w;
        }

        readonly uint[] C;
        readonly double REACH;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Site[] sites;
        readonly Rider[] riders;
        double t;
        bool released;

        double Front(double tt) => tt < SEED ? 0 : REACH * Ease.OutCubic(Seg(tt, SEED, SEED + ROLL));
        double Passed(double d) => SEED + ROLL * (1 - Px.Cbrt(1 - Math.Min(0.999, d / REACH)));

        HealAoe(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            REACH = o.Num("reach");
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            // where plus signs sprout: scattered over the floor, each waiting for the ring to reach it
            sites = new Site[26];
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(0, TAU), d = REACH * Math.Sqrt(R.Range(0.04, 0.92));
                bool big = i % 5 < 2;
                sites[i] = new Site
                {
                    x = Px.Cos(a) * d, z = Px.Sin(a) * d, t0 = Passed(d) + R.Range(0, 0.06), rise = R.Range(18, 34), life = R.Range(0.9, 1.3), sway = R.Range(0, TAU),
                    L = big ? 3 + (i % 5 == 0 ? 1 : 0) : i % 5 == 2 ? 1 : 2, thick = big ? 3 : 1,
                };
            }
            // the rolling ring carries little lights round with it
            riders = new Rider[14];
            for (int i = 0; i < 14; i++) riders[i] = new Rider { a = (i / 14.0) * TAU + R.Range(-0.2, 0.2), w = R.Range(0.6, 1.1) * (i % 2 != 0 ? 1 : -1) };
        }

        void Wash(Painter p)
        {
            // the floor glows behind the front and fades after it
            double fr = Front(t);
            if (fr < 2 || t > 2.6) return;
            p.Shade(-fr - 1, -fr * SY - 1, fr + 1, fr * SY + 1, (x, y, o) =>
            {
                double d = Hypot(x, y / SY);
                if (d > fr) return 0;
                double age = t - Passed(d), k = Exp(-age / 0.35) * Smooth(fr, fr - 6, d) * (1 - Smooth(1.4, 1.9, t));
                if (k < 0.04) return 0;
                o.l = C[4];
                o.k = 0.32 * k;
                return 0;
            });
        }

        void Ring(Painter p, bool back)
        {
            if (t < SEED) return;
            double u = Seg(t, SEED, SEED + ROLL + 0.25), fr = Front(t), k = u < 0.7 ? 1 : (1 - u) / 0.3;
            if (k <= 0) return;
            Func<double, bool> keep = back ? (Func<double, bool>)(g => Px.Sin(g) < 0) : g => Px.Sin(g) >= 0;
            Sim.Band(p, 0, 0, fr, SY, Math.Max(2, 7 * (1 - u * 0.7)), k, Slice(C, 2), keep, t < SEED + 0.08);
            Oval(p, 0, 0, fr + 1, (fr + 1) * SY, g => Px.Sin(g) > 0.3 ? WHITE : C[6], k * (back ? 0.7 : 1), keep);
            // a crest of light blades flickers up along the front as it rolls
            for (int j = 0, n = (int)Math.Ceiling(fr * 0.9); j < n; j++)
            {
                double g = ((double)j / n) * TAU;
                bool near = Px.Sin(g) >= 0;
                if (near == back || Rand(j, Math.Floor(t * 20)) > 0.45) continue;
                double x = Px.Cos(g) * fr, y = Px.Sin(g) * fr * SY, h = (1 + Rand(j, 7) * 4) * k * (near ? 1 : 0.6);
                for (int i = 1; i <= h; i++) p.Dot(x, y - i, i > h - 1 ? WHITE : C[6], k * (1.2 - i / (h + 1)));
            }
            // lights riding round the ring as it rolls
            foreach (Rider r in riders)
            {
                double g = r.a + r.w * (t - SEED) * 2.2;
                bool near = Px.Sin(g) >= 0;
                if (near == back) continue;
                double x = Px.Cos(g) * fr, y = Px.Sin(g) * fr * SY;
                if (near) Sim.Sparkle(p, x, y - 1, 2 + (Px.Sin(t * 9 + r.a * 5) > 0.3 ? 1 : 0), WHITE, C[6], 0.35 * k);
                else p.Dot(x, y, C[6], k);
            }
            if (!back)
                for (int i = 0; i < 8; i++)
                {
                    double g = (i / 8.0) * TAU;
                    p.Glow(Px.Cos(g) * fr, Px.Sin(g) * fr * SY - 2, 16, C[4], 0.22 * k);
                }
        }

        static readonly int[][] DIAG = { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };

        void Sprouts(Painter p, bool back)
        {
            foreach (Site s in sites)
            {
                double a = t - s.t0;
                if (a < 0 || a > s.life || (s.z < 0) != back) continue;
                double u = a / s.life, h = s.rise * Ease.OutCubic(u), x = s.x + Px.Sin(a * 4 + s.sway) * 1.5 * u, y = s.z * SY - 2 - h;
                double pop = Ease.OutBack(Clamp(a / 0.12), 2.6), end = s.life - 0.15, L = Math.Max(1, Round(s.L * pop - (back ? 1 : 0)));
                if (a < 0.04) Sim.Sparkle(p, x, y, 3, WHITE, C[6], 0.5);
                else if (a < end) Plus(p, x, y, L, back && L < 3 ? 1 : s.thick, back ? Slice(C, 0, 7) : C, back ? 0.8 : 1, !back && Px.Sin(a * 12 + s.sway * 3) > 0.8);
                else
                {
                    double v = (a - end) / 0.15;
                    foreach (int[] d in DIAG) p.Dot(x + d[0] * (2 + v * 5), y + d[1] * (2 + v * 5) - v * 2, v < 0.4 ? WHITE : C[6], 1 - v);
                }
                // a thread of light still ties it to the floor while it rises
                if (u < 0.5)
                    for (double j = 1; j < h * (1 - u * 2); j += 1.5)
                        p.Dot(s.x, s.z * SY - 2 - h + j + 3, C[5], (1 - j / h) * (1 - u * 2) * (back ? 0.5 : 0.8));
                p.Glow(x, y, 7 + L * 2, C[5], (back ? 0.2 : 0.34) * (a < end ? 1 : 1 - (a - end) / 0.15));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the seed gathers: motes slide in over the floor to a point that brightens
            if (t < SEED - 0.04 && R.Chance(0.7))
            {
                double a = R.Range(0, TAU), D = R.Range(24, 48), x = Px.Cos(a) * D, y = Px.Sin(a) * D * SY - R.Range(1, 8);
                double T = R.Range(0.16, 0.24), l = Hypot(x, y + 3);
                if (l == 0 || double.IsNaN(l)) l = 1;
                double v = (l * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, x, y, (-x / l) * v, ((-3 - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!released && t >= SEED)
            {
                released = true;
                lights.Add(0, -16, 76, C[5], 0.85, 0.55);
                lights.Add(0, -6, 28, WHITE, 0.8, 0.12);
                for (int i = 0; i < 10; i++) ps.Spawn(P.Mote, R.Range(-2, 2), -R.Range(2, 10), R.Range(-25, 25), -R.Range(80, 170), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1));
                for (int i = 0; i < 16; i++)
                {
                    double a = R.Range(Math.PI * 1.05, Math.PI * 1.95), sp = R.Range(40, 120);
                    ps.Spawn(P.Star, 0, -3, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 2));
                }
            }
            foreach (Site s in sites)
                if (!s.popped && t >= s.t0)
                {
                    s.popped = true;
                    // a splash of dew where each one breaks the floor
                    for (int i = 0; i < 3; i++) ps.Spawn(P.Star, s.x + R.Range(-3, 3), s.z * SY - 1, R.Range(-10, 10), -R.Range(15, 40), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.2), 0, s.z < 0 ? PF.Back : PF.None);
                }
            // dew rising off the glowing floor behind the ring
            if (t > SEED + 0.1 && t < 1.9 && R.Chance(0.35))
            {
                double fr = Front(t), a = R.Range(0, TAU), d = fr * Math.Sqrt(R.Next());
                ps.Spawn(P.Ember, Px.Cos(a) * d, Px.Sin(a) * d * SY - 1, R.Range(-5, 5), -R.Range(10, 30), R.Range(0.5, 1), C[5], 1, 0, Px.Sin(a) < 0 ? PF.Back : PF.None);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            Wash(p);
            Ring(p, true);
            Sprouts(p, true);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            Ring(p, false);
            // the seed: a point of light pulsing on the floor, then a flare as it lets go
            if (t < SEED)
            {
                double k = t / SEED;
                Sim.Sparkle(p, 0, -3, 2 + 4 * k * k, WHITE, C[6], 0.3 + 0.4 * k);
                p.Glow(0, -3, 10 + 16 * k, C[4], 0.5 * k);
            }
            else if (t < SEED + 0.2) Sim.Flare(p, 0, -3, 22 * Px.Pow(1 - (t - SEED) / 0.2, 1.5), 1 - (t - SEED) / 0.2, Slice(C, 3));
            // as it lets go, a jet of light leaps out of the floor, lets go of it and dissolves upward
            double g = Seg(t, SEED, SEED + 0.5);
            if (g > 0 && g < 1)
            {
                double top = -4 - 46 * Ease.OutCubic(Math.Min(1, g * 2.2)) - 10 * g, bot = -2 - 50 * Ease.InQuad(Seg(g, 0.35, 1)), w = 1 + 2.4 * (1 - g);
                if (bot - top > 2)
                {
                    p.Beam(0, bot, 0, top, w * 2, (u, v) => v < 0.45 ? WHITE : u > 0.8 ? C[5] : C[6]);
                    p.GlowLine(0, bot, 0, top, 12, C[5], 0.55 * (1 - g));
                    Sim.Sparkle(p, 0, top, 4 * (1 - g) + 1, WHITE, C[6], 0.5 * (1 - g));
                }
            }
            Sprouts(p, false);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
