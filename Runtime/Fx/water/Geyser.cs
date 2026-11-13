// The column (a banded white core, strands running through it, light down its length) and the slam with its
// floor ring follow Sprite Fusion's open-source holy-light (https://www.spritefusion.com/pixel-art-vfx/holy-light),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Geyser : Effect
    {
        public static readonly Module Module = new Module("geyser", "pixelfx/fx/water/geyser", 184, 168, 92, 146, 5, false, 30,
            new Options { ["palette"] = WATER, ["height"] = 100, ["seed"] = 4 },
            (o, e) => new Geyser(o, e))
        {
            Title = "Geyser",
            Desc = "A pool heaves and burbles faster and faster, then blasts a white column of water skyward with a churning foam head; it rains back down in drops and steam, slumps and sputters out.",
            Tags = new[] { "water", "geyser", "eruption", "steam", "column", "nature", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.22, G = 820, ERUPT = 1.15, FALL = 2.6, GONE = 3.3;
        // small spurts while it builds, and the sputters after: [time, height]
        static readonly double[][] SPURTS = { new[] { 0.46, 7 }, new[] { 0.72, 12 }, new[] { 0.92, 9 }, new[] { 1.04, 15 }, new[] { GONE + 0.15, 15 }, new[] { GONE + 0.55, 8 } };
        static readonly double[][] POP_DIRS = { new[] { 1, 0.0 }, new[] { -1, 0.0 }, new[] { 0.7, -0.7 }, new[] { -0.7, -0.7 }, new[] { 0, -1.0 } };

        sealed class Plink
        {
            public double x, y, s, age;
        }

        sealed class Pop
        {
            public double x, y, age, r;
        }

        readonly uint[] C, DIM;
        readonly Rng R;
        readonly double HMAX, S, V0;
        readonly Particles ps;
        readonly WaterLib.Mist mist = new WaterLib.Mist();
        readonly Lights lights;
        readonly WaterLib.Ripples rings = new WaterLib.Ripples(SY);
        readonly List<Plink> plinks = new List<Plink>();
        readonly List<Pop> pops = new List<Pop>();
        readonly WaterLib.Drops drops;
        readonly HashSet<string> done = new HashSet<string>();
        double t;

        Geyser(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            HMAX = o.Num("height");
            S = o.Seed;
            ps = new Particles(R, emitter: e);
            lights = new Lights(e);
            V0 = Math.Sqrt(2 * G * HMAX);
            DIM = new[] { C[0], C[1], C[2], C[3], C[3], C[4], C[5], C[5] };
            drops = new WaterLib.Drops(SY, G, onLand: d =>
            {
                bool inPool = Hypot(d.x, d.z) < PoolR(t);
                rings.Add(d.x, d.z, 0.6, (inPool ? 2.5 : 1.5) + d.s * 1.6, 0.32 + d.s * 0.1, n: 1, k: inPool ? 0.95 : 0.7, hot: 0.05);
                plinks.Add(new Plink { x = d.x, y = d.z * SY, s = d.s, age = 0 });
                if (d.s > 1 && !d.small)
                    for (int i = 0; i < 2; i++)
                    {
                        double a = R.Range(0, TAU), v = R.Range(8, 28);
                        drops.Add(new WaterLib.Drop { x = d.x, z = d.z, h = 0.5, vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(45, 95), s = 1, small = true, life = 1 });
                    }
            });
        }

        static double PoolR(double tt) => (12 + 7 * Smooth(0, 1, tt) + 15 * Ease.OutCubic(Clamp((tt - ERUPT) / 1.2))) * (1 - 0.25 * Smooth(GONE, 4.6, tt));
        static double PoolK(double tt) => Smooth(0, 0.2, tt) * (1 - Smooth(4.0, 4.85, tt));

        // how tall the column stands: a fast blast, surging while it holds, slumping when the pressure goes
        double Top(double tt)
        {
            if (tt < ERUPT || tt > GONE) return 0;
            return HMAX * Ease.OutCubic(Clamp((tt - ERUPT) / 0.3)) * (0.93 + 0.07 * Noise(tt * 2.2, S)) * (1 - Ease.InQuad(Clamp((tt - FALL) / (GONE - FALL))));
        }

        static double Head(double tt) => Smooth(ERUPT + 0.08, ERUPT + 0.3, tt) * (1 - Smooth(FALL + 0.1, FALL + 0.45, tt));

        static double Spurt(double tt)
        {
            double h = 0;
            foreach (double[] sp in SPURTS) h = Math.Max(h, sp[1] * Px.Sin(Math.PI * Clamp((tt - sp[0]) / 0.2)));
            return h;
        }

        bool Once(string key, double at)
        {
            if (done.Contains(key) || t < at) return false;
            done.Add(key);
            return true;
        }

        bool Emit(double rate, double dt) => R.Chance(Math.Min(1, rate * dt));

        double Tau(double h) => (V0 - Math.Sqrt(Math.Max(0, V0 * V0 - 2 * G * Math.Min(h, HMAX * 0.96)))) / G;

        // the column: a white core banded out to blue edges, shadowed on the right
        void Column(Painter p, double H, double thin)
        {
            if (H < 1) return;
            uint[] lv = { WHITE, C[6], C[5], C[4], C[3] };
            p.Shade(-22, -H - 2, 22, 0, (x, y, @out) =>
            {
                double h = -y, v = h / H, sway = Noise(t * 1.6 + h * 0.025, S + 5) * 2.5 * v;
                double rag = Noise2(h * 0.2 - t * 11, x < sway ? 3 : 7, S + 6) * (0.5 + 1.8 * v);
                double w = (3.6 + 7.5 * Px.Pow(v, 1.8)) * thin + rag, q = (x - sway) / Math.Max(1, w), aq = Math.Abs(q);
                if (aq > 1) return 0;
                double X = Math.Floor(x), streak = Noise2(X * 1.618 + 0.3, (Tau(h) - t) * 14, S), body = Noise2(Math.Floor((x + 40) / 2) * 2.3, (Tau(h) - t) * 5, S + 1);
                int lvl = aq < 0.42 ? 0 : aq < 0.7 ? 1 : aq < 0.9 ? 2 : 3;
                if (q > 0.38) lvl++;
                if (streak > 0.28) lvl--;
                if (body < -0.4) lvl++;
                @out.c = lv[(int)Clamp(lvl, 0, 4)];
                return 0;
            });
            p.GlowLine(0, 0, 0, -H, 16 + 8 * thin, C[5], 0.12 * thin);
        }

        // the head: water boiling over at the top and falling back round the column in an umbrella of streaks
        void FoamHead(Painter p, double H, double k, bool back)
        {
            if (k <= 0.01 || H < 4) return;
            if (back)
            {
                // the umbrella: streaks pouring off the rim of the head, running down and thinning out
                for (int i = 0; i < 16; i++)
                {
                    double side = i % 2 != 0 ? 1 : -1, x0 = side * (5 + 9 * Rand(i, S + 20)) * k, len = (14 + 26 * Rand(i, S + 21)) * k, speed = 70 + 50 * Rand(i, S + 22);
                    for (int j = 0; j < 3; j++)
                    {
                        double f = (t * speed / len + j / 3.0 + Rand(i, S + 23)) % 1, y0 = -H + 2 + f * len, x = x0 + side * f * f * 6, L = 1 + 4 * f * (1 - f) * 2;
                        if (f < 0.85) p.Line(x, y0, x + side * 0.6, y0 + L, f < 0.4 ? C[6] : f < 0.7 ? C[5] : C[4]);
                    }
                }
            }
            for (int i = 0; i < 12; i++)
            {
                double a = (i / 12.0) * TAU + t * (i % 2 != 0 ? 1.3 : -1.1), z = Px.Sin(a);
                if (z < 0 != back) continue;
                double rho = (3 + 7 * Rand(i, S)) * k, x = Px.Cos(a) * rho * 1.25 + Noise(t * 3 + i, S + 8) * 2.5;
                double y = -H + z * rho * 0.45 + (rho / 10) * 4 - 4 * (1 - rho / (10 * k)) + Noise(t * 3 + i + 50, S + 9) * 2.5, r = (2 + 2.6 * Rand(i, S + 2) + 0.7 * Px.Sin(t * 7 + i)) * k;
                if (back) Billow(p, x, y, r, DIM);
                else
                {
                    p.Circle(x + r * 0.15, y + r * 0.25, r, C[4]);
                    p.Circle(x, y, r * 0.82, rho > 6 * k ? C[5] : C[6]);
                    p.Circle(x - r * 0.25, y - r * 0.3, r * 0.5, WHITE);
                }
            }
            if (!back) p.Glow(0, -H - 2, 38, C[5], 0.34 * k);
        }

        public override void Update(double dt)
        {
            t += dt;
            double H = Top(t), hk = Head(t), k = t / ERUPT;
            // building: bubbles burst on the heaving mound, faster and faster, steam starts to lift
            if (t < ERUPT && Emit(1.5 + 9 * k * k, dt))
            {
                double a = R.Range(0, TAU), r = R.Range(0, 7), x = Px.Cos(a) * r, z = Px.Sin(a) * r;
                pops.Add(new Pop { x = x, y = z * SY - R.Range(1, 3), age = 0, r = R.Range(1, 2) });
                rings.Add(x, z, 1, R.Range(5, 9), 0.55, n: 1, k: 0.85);
                for (int i = 0; i < R.Int(1, 3); i++) drops.Add(new WaterLib.Drop { x = x, z = z, h = 2, vx = R.Range(-25, 25), vz = R.Range(-15, 15), vh = R.Range(50, 120), s = 1, small = true, life = 1 });
            }
            if (t < ERUPT && Emit(3 + 7 * k, dt)) mist.Add(R.Range(-8, 8), -R.Range(1, 5), R.Range(-10, 10), -R.Range(6, 18), 2, R.Range(5, 9), R.Range(0.9, 1.5), C[6], 0.22);
            for (int si = 0; si < SPURTS.Length; si++)
            {
                double s0 = SPURTS[si][0], hs = SPURTS[si][1];
                if (Once("spurt" + si, s0 + 0.08))
                    for (int i = 0; i < 3 + Round(hs / 4); i++)
                    {
                        double a = R.Range(0, TAU), v = R.Range(10, 40);
                        drops.Add(new WaterLib.Drop { x = 0, z = 0, h = hs * 0.8, vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(20, 90), s = R.Chance(0.3) ? 2 : 1 });
                    }
            }
            if (Once("erupt", ERUPT))
            {
                // the blast: light, a ring thrown across the ground, water flung high, steam rolling out
                lights.Add(0, -26, 64, C[5], 0.85, 0.6);
                lights.Add(0, -4, 28, WHITE, 0.75, 0.1);
                rings.Add(0, 0, 6, 40, 1.2, n: 2, gap: 3, k: 1.2, hot: 0.12, glint: 1);
                for (int i = 0; i < 44; i++)
                {
                    double a = R.Range(0, TAU), v = R.Range(30, 120);
                    drops.Add(new WaterLib.Drop { x = Px.Cos(a) * 3, z = Px.Sin(a) * 3, h = R.Range(2, 10), vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(180, 400), s = R.Chance(0.15) ? 3 : R.Chance(0.35) ? 2 : 1, delay = R.Range(0, 0.12) });
                }
                for (int i = 0; i < 12; i++)
                {
                    double s = i % 2 != 0 ? 1 : -1;
                    mist.Add(s * R.Range(3, 9), -R.Range(1, 6), s * R.Range(30, 80), -R.Range(4, 18), 3, R.Range(9, 14), R.Range(1.2, 1.9), R.Chance(0.5) ? C[6] : WHITE, 0.3, R.Range(0, 0.15));
                }
                for (int i = 0; i < 14; i++) ps.Spawn(P.Star, R.Range(-16, 16), -R.Range(8, 80), R.Range(-10, 10), -6, R.Range(0.3, 0.7), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.6), R.Range(0.05, 0.5));
            }
            // holding: the head sheds drops that rain back down, spray hangs off the column, steam pours off the top
            if (hk > 0)
            {
                if (Emit(60 * hk, dt))
                {
                    double a = R.Range(0, TAU), v = R.Range(10, 60);
                    drops.Add(new WaterLib.Drop { x = R.Range(-9, 9), z = R.Range(-4, 4), h = H + R.Range(-8, 3), vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(-40, 60), s = R.Chance(0.08) ? 3 : R.Chance(0.3) ? 2 : 1 });
                }
                if (Emit(9 * hk, dt)) mist.Add(R.Range(-8, 8), -H + R.Range(-6, 2), R.Range(-34, 34), -R.Range(14, 34), 3, R.Range(8, 13), R.Range(0.9, 1.4), R.Chance(0.6) ? C[6] : WHITE, 0.2);
                if (Emit(5 * hk, dt)) mist.Add(R.Sign() * R.Range(3, 9), -R.Range(1, 6), R.Sign() * R.Range(20, 50), -R.Range(2, 10), 3, R.Range(8, 12), R.Range(1, 1.6), C[6], 0.22);
                if (Emit(40 * hk, dt))
                {
                    double s = R.Sign(), hh = R.Range(8, H * 0.95);
                    ps.Spawn(P.Flake, s * (3 + 6 * Px.Pow(hh / H, 1.8)), -hh, s * R.Range(10, 45), R.Range(-20, 10), R.Range(0.6, 1.2), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.3));
                }
                if (Emit(4 * hk, dt)) rings.Add(R.Range(-2, 2), R.Range(-2, 2), 4, PoolR(t) * R.Range(0.7, 1), 0.9, n: 1, k: 0.8);
                if (Emit(7 * hk, dt)) ps.Spawn(P.Star, R.Range(-14, 14), -R.Range(10, H + 8), R.Range(-6, 6), -4, R.Range(0.25, 0.55), R.Chance(0.4) ? WHITE : C[6], R.Range(0.8, 2.2), 0);
            }
            if (Once("collapse", FALL + 0.12))
            {
                // the head falls apart: a heavy rain of drops all at once
                for (int i = 0; i < 40; i++)
                {
                    double a = R.Range(0, TAU), v = R.Range(15, 80);
                    drops.Add(new WaterLib.Drop { x = R.Range(-6, 6), z = R.Range(-3, 3), h = Top(t) + R.Range(-8, 4), vx = Px.Cos(a) * v, vz = Px.Sin(a) * v, vh = R.Range(-40, 60), s = R.Chance(0.12) ? 3 : R.Chance(0.35) ? 2 : 1, delay = R.Range(0, 0.25) });
                }
                for (int i = 0; i < 6; i++) mist.Add(R.Range(-8, 8), -Top(t) + R.Range(-6, 6), R.Range(-34, 34), -R.Range(10, 26), 4, R.Range(9, 13), R.Range(0.8, 1.2), C[6], 0.18);
            }
            for (int i = pops.Count - 1; i >= 0; i--)
                if ((pops[i].age += dt) > 0.12) pops.RemoveAt(i);
            for (int i = plinks.Count - 1; i >= 0; i--)
                if ((plinks[i].age += dt) > 0.14) plinks.RemoveAt(i);
            drops.Update(dt);
            rings.Update(dt);
            mist.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // the pool: dark see-through water, a lit far rim
            double pr = PoolR(t), pk = PoolK(t);
            if (pk > 0)
                p.Each(-pr, -pr * SY, pr, pr * SY, (x, y, sx, sy) =>
                {
                    double qx = x / pr, qy = y / (pr * SY), d = qx * qx + qy * qy;
                    if (d > 1) return;
                    p.BlendPx(sx, sy, d < 0.15 ? C[2] : C[1], 0.6 * pk * Math.Min(1, (1 - d) * 4));
                    if (y < 0 && d > 0.78 && d < 0.95) p.BlendPx(sx, sy, C[4], 0.7 * pk);
                });
            rings.Draw(p, C, -1);
            double u = (t - ERUPT) / 0.4;
            if (u > 0 && u < 1) Ring(p, 0, 0.5, 6 + 42 * Ease.OutCubic(u), SY, 1.5 * (1 - u), C, n: 3, gap: 1.6 + 2 * u, half: -1, hot: u < 0.3, seed: 9);
            FoamHead(p, Top(t), Head(t), true);
            drops.Draw(p, C, -1);
        }

        public override void Draw(Painter p, double tt)
        {
            double H = Top(t), hk = Head(t), k = Clamp(t / ERUPT), thin = Smooth(ERUPT, ERUPT + 0.12, t) * (1 - 0.55 * Smooth(FALL, GONE, t));
            rings.Draw(p, C, 1);
            double u = (t - ERUPT) / 0.4;
            if (u > 0 && u < 1) Ring(p, 0, 0.5, 6 + 42 * Ease.OutCubic(u), SY, 1.5 * (1 - u), C, n: 3, gap: 1.6 + 2 * u, half: 1, hot: u < 0.3, seed: 9);
            // foam rides the pool round the foot of the column
            double fk = Smooth(ERUPT, ERUPT + 0.3, t) * (1 - Smooth(GONE - 0.3, 4.4, t));
            if (fk > 0) Lace(p, 0, 0, 8 + 10 * fk, (8 + 10 * fk) * SY + 0.5, fk, C, t, S);
            // the heaving mound while it builds, beating faster
            if (t < ERUPT + 0.05)
            {
                double beat = Px.Pow(Math.Max(0, Px.Sin(t * (8 + 18 * k * k))), 3), rd = 4 + 3.5 * k + 1.5 * beat, hd = 1.5 + 3 * k + 3 * beat;
                p.Shade(-rd - 1, -hd - 1, rd + 1, 0.5, (x, y, @out) =>
                {
                    double dx = x / rd, dy = y / hd, d = dx * dx + dy * dy;
                    if (d > 1 || y > 0.5) return 0;
                    // water heaving up: a white rim where it faces the light, a tinted body, shadow low on the right
                    double lit = -dx * 0.7 - dy * 0.7;
                    bool rim = d > 0.68;
                    @out.c = rim ? (lit > 0.3 ? WHITE : lit > -0.15 ? C[6] : C[4]) : lit > 0.55 ? C[6] : lit > 0.12 ? C[5] : lit > -0.3 ? C[4] : C[3];
                    return 0;
                });
                p.Px(-rd * 0.35, -hd * 0.6, WHITE);
                p.Glow(0, -5, 16 + 16 * k + 8 * beat, C[5], 0.06 + 0.12 * k + 0.12 * beat);
            }
            // spurts while it builds and sputters as it dies: a short white jet with a bead on top
            double sp = Spurt(t);
            if (sp > 1 && H < 1)
            {
                p.Shade(-2, -sp - 1, 2, 0, (x, y, @out) =>
                {
                    double q = x / (1 + 0.8 * (1 + y / sp));
                    if (Math.Abs(q) > 1) return 0;
                    @out.c = q < -0.3 ? C[6] : q < 0.35 ? WHITE : C[5];
                    return 0;
                });
                p.Circle(0, -sp, 1.3, C[6]);
                p.Px(0, -sp - 1, WHITE);
                p.GlowLine(0, 0, 0, -sp, 8, C[5], 0.3);
            }
            Column(p, H, thin);
            // the foot: billows boil out round it
            if (hk > 0 || (H > 1 && t < GONE))
                for (int i = 0; i < 7; i++)
                {
                    double a = (i / 7.0) * TAU + t * 2.1, r = 4 + 2 * Px.Sin(t * 5 + i * 1.7), x = Px.Cos(a) * r * 1.3, y = Px.Sin(a) * r * SY - 2;
                    Billow(p, x, y, (1.6 + 1.2 * Rand(i, S + 4)) * Math.Max(hk, thin * 0.6), C);
                }
            FoamHead(p, H, hk, false);
            foreach (Pop q in pops)
            {
                double g = q.age / 0.12;
                foreach (double[] dv in POP_DIRS) p.Dot(q.x + dv[0] * (q.r + 3 * g), q.y + dv[1] * (q.r + 3 * g), C[6], 1 - g);
            }
            foreach (Plink q in plinks)
            {
                double g = q.age / 0.14;
                if (g < 0.5) p.Px(q.x, q.y, WHITE);
                if (q.s > 1) p.Line(q.x, q.y - 1, q.x, q.y - 1 - 3 * (1 - g), g < 0.4 ? WHITE : C[6]);
                p.Glow(q.x, q.y, 4, C[5], 0.25 * (1 - g));
            }
            drops.Draw(p, C, 1);
            // the blast's first two frames: a white burst at the vent
            double f = (t - ERUPT) / 0.07;
            if (f >= 0 && f < 1)
            {
                p.Circle(0, -4, 7 * (1 - f * 0.4), WHITE);
                p.Ellipse(0, 0, 12, 2.6, (d, a) => d < 0.6 ? WHITE : C[6]);
            }
            ps.Draw(p);
            mist.Draw(p, C[5]);
            lights.Draw(p);
            // light: the column and its head, the pool round its foot
            if (H > 1) p.GlowLine(0, 0, 0, -H, 26, C[4], 0.2 * thin);
            p.Glow(0, -2, 46, C[4], 0.24 * Math.Max(hk, 0.3 * k * (t < ERUPT ? 1 : 0)));
            p.Glow(0, 2, 60, C[3], 0.12 * PoolK(t));
        }
    }
}
