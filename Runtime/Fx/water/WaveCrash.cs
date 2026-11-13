// The lip's crescent (a thick arc, white on its outer rim, cooling to deep blue underneath, with light along it)
// follows Sprite Fusion's open-source slash (https://www.spritefusion.com/pixel-art-vfx/slash),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class WaveCrash : Effect
    {
        const double DURATION = 3.2;

        public static readonly Module Module = new Module("wave-crash", "pixelfx/fx/water/wave-crash", 224, 104, 112, 86, DURATION, false, 30,
            new Options { ["palette"] = WATER, ["seed"] = 6 },
            (o, e) => new WaveCrash(o, e))
        {
            Title = "Wave crash",
            Desc = "A swell rolls in and steepens, its lip throws forward and curls over a dark barrel, then crashes into a burst of white foam and spray that surges on and fades to lace.",
            Tags = new[] { "water", "wave", "crash", "surf", "foam", "spray", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.22, G = 820, CRASH = 1.2, LIP0 = 0.62, EDGE = 110;

        // the swell: where its crest is, how tall, how steep its face, as it rolls in, crashes and runs on as whitewater
        static double CrestX(double t) => t < CRASH ? -80 + 72 * Ease.InOutSine(t / CRASH) : -8 + 30 * Ease.OutCubic(Clamp((t - CRASH) / 1.4));
        static double CrestH(double t) => t < CRASH ? 5 + 33 * Ease.InOutSine(Clamp(t / (CRASH * 0.92))) : 38 * (1 - Ease.OutCubic(Clamp((t - CRASH) / 0.45))) + 5 * (1 - Smooth(CRASH, CRASH + 1.6, t));
        static double FaceW(double t) => t < CRASH ? 24 - 16 * Smooth(0.3, CRASH, t) : 8 + 14 * Smooth(CRASH, CRASH + 0.6, t);
        // the lip: thrown forward off the crest and falling, until its tip hits the water at CRASH
        static double LipK(double t) => Clamp((t - LIP0) / (CRASH - LIP0));

        sealed class Puff
        {
            public double x, y, vx, vy, r0, r1, life, max, delay;
        }

        sealed class Plink
        {
            public double x, y, age;
        }

        sealed class Cell
        {
            public double x, y, q;
            public uint c;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double S;
        readonly Particles ps;
        readonly WaterLib.Mist mist = new WaterLib.Mist();
        readonly Lights lights;
        readonly WaterLib.Ripples rings = new WaterLib.Ripples(SY);
        readonly List<Puff> puffs = new List<Puff>();
        readonly List<Plink> plinks = new List<Plink>();
        readonly WaterLib.Drops drops;
        double t;
        bool crashed;

        WaveCrash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            S = o.Seed;
            ps = new Particles(R, emitter: e);
            lights = new Lights(e);
            drops = new WaterLib.Drops(SY, G, onLand: d =>
            {
                rings.Add(d.x, d.z, 0.6, 2 + d.s * 1.8, 0.3 + d.s * 0.1, n: 1, k: 0.9, hot: 0.05);
                plinks.Add(new Plink { x = d.x, y = d.z * SY, age = 0 });
            });
        }

        static double Height(double x, double tt)
        {
            double xc = CrestX(tt), A = CrestH(tt), dx = x - xc, q = dx / (dx < 0 ? 30 : FaceW(tt));
            return A * Exp(-(q * q));
        }

        // a point on the lip at s (0 at the crest, 1 at its tip), and its thickness there
        static (double x, double y, double w) Lip(double s, double tt)
        {
            double k = LipK(tt), xc = CrestX(tt), A = CrestH(tt);
            return (xc + 1 + s * 30 * Px.Pow(k, 0.8), -A - 4 * s * (1 - s) * (1 + 4 * k) + s * s * A * Ease.InQuad(k) * 0.98, (11 - 8.5 * s) * (0.5 + 0.5 * k));
        }

        void AddPuff(double x, double y, double vx, double vy, double r0, double r1, double life, double delay = 0) =>
            puffs.Add(new Puff { x = x, y = y, vx = vx, vy = vy, r0 = r0, r1 = r1, life = life, max = life, delay = delay });

        // the swell's body, column by column
        void Body(Painter p, double fade)
        {
            double xc = CrestX(t), A = CrestH(t);
            for (double X = -EDGE; X < EDGE; X++)
            {
                double x = X + 0.5, h = Height(x, t), edge = 1 - Smooth(EDGE - 24, EDGE, Math.Abs(x));
                if (h < 0.6 || edge <= 0) continue;
                double nq = (x - xc) / 9;
                bool face = x > xc;
                double near = Exp(-(nq * nq)), a = fade * edge * 1.4;
                for (double Y = Math.Floor(-h); Y < 0; Y++)
                {
                    double y = Y + 0.5, u = (y + h) / h, depth = y + h;
                    uint c;
                    if (depth < 1) c = near > 0.5 && A > 14 && Noise(x * 0.6 + t * 9, S) > -0.35 ? WHITE : face ? C[5] : C[6];
                    else if (depth < 2.4 && near > 0.35 && A > 14) c = Noise2(x * 0.5, y * 0.5 + t * 6, S + 1) > -0.1 ? WHITE : C[6];
                    else
                    {
                        // face and back shade differently; blend the two across the crest so there's no seam
                        double win = near * (1 - u) * 1.6, up = Noise2((x * 0.9 - y * 0.35) * 0.8, y * 0.12 + t * 5, S + 2), wind = Noise2(x * 0.18 - t * 2, y * 0.9, S + 3);
                        double fi = up > 0.55 && u < 0.85 ? 6 : win > 0.8 ? 5 : win > 0.45 || up > 0.3 ? 4 : win > 0.2 || up > 0.05 ? 3 : u < 0.6 ? 2 : 1;
                        double bi = u < 0.14 ? 4 : wind > 0.55 ? 4 : wind > 0.25 || u < 0.32 ? 3 : u < 0.7 ? 2 : 1;
                        c = C[(int)Round(bi + (fi - bi) * Smooth(xc - 5, xc + 5, x))];
                    }
                    p.Dot(X, Y, c, a);
                }
            }
        }

        // the lip: a thick arc thrown off the crest, white along its outer edge, tinted through, dark underneath
        void Curl(Painter p)
        {
            double k = LipK(t);
            if (k <= 0 || t >= CRASH) return;
            var index = new Dictionary<double, int>();
            var cells = new List<Cell>();
            for (double s = 0; s <= 1; s += 0.012)
            {
                var (x, y, w) = Lip(s, t);
                var (x2, y2, _) = Lip(Math.Min(1, s + 0.012), t);
                double tx = x2 - x, ty = y2 - y, l = Hypot(tx, ty);
                if (l == 0 || double.IsNaN(l)) l = 1;
                double nx = ty / l, ny = -tx / l;
                for (double q = 0; q <= 1; q += 0.5 / Math.Max(1, w))
                {
                    double px = x + nx * (q - 0.5) * w, py = y + ny * (q - 0.5) * w, key = Math.Floor(py) * 1024 + Math.Floor(px);
                    // q 0 is the outer (upper) rim, 1 the underside
                    uint c = q < 0.18 ? WHITE : q < 0.36 ? (s > 0.8 ? WHITE : C[6]) : q < 0.56 ? C[5] : q < 0.76 ? C[4] : C[3];
                    if (!index.TryGetValue(key, out int at))
                    {
                        index[key] = cells.Count;
                        cells.Add(new Cell { x = px, y = py, c = c, q = q });
                    }
                    else if (q < cells[at].q) cells[at] = new Cell { x = px, y = py, c = c, q = q };
                }
            }
            foreach (Cell c in cells) p.Px(c.x, c.y, c.c);
            for (double s = 0; s <= 1; s += 0.125)
            {
                var (x, y, w) = Lip(s, t);
                p.Glow(x, y, 6 + w, C[5], 0.24 * k);
            }
            // the tip glints as it throws
            var (tx1, ty1, _) = Lip(1, t);
            double g = Px.Sin(Math.PI * Clamp((k - 0.55) / 0.45));
            if (g > 0.05) Sim.Sparkle(p, tx1, ty1, 6 * g, C[6], C[5], 0.5 * g);
            // light caught under the barrel
            var (bx, by, _) = Lip(0.6, t);
            p.Glow(bx - 3, by + 8, 16, C[4], 0.25 * k);
        }

        // the sea it rolls on: a lit line along the surface, dark water under it, glints sliding along
        void Sea(Painter p, double fade)
        {
            for (double X = -EDGE; X < EDGE; X++)
            {
                double x = X + 0.5, edge = 1 - Smooth(EDGE - 30, EDGE, Math.Abs(x)), a = fade * edge;
                if (a <= 0.02) continue;
                double g = Noise(x * 0.3 - t * 3, S + 4);
                p.Dot(X, 0, g > 0.55 ? C[6] : C[4], a * 1.2);
                p.Blend(x, 1.5, C[2], 0.6 * a);
                p.Blend(x, 2.5, C[1], 0.45 * a);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            double k = LipK(t);
            // wind tears spray off the crest; the lip's tip sheds drops as it throws
            if (t < CRASH && CrestH(t) > 14 && R.Chance(0.5))
            {
                double xc = CrestX(t), A = CrestH(t);
                ps.Spawn(P.Flake, xc + R.Range(-4, 3), -A - R.Range(0, 2), -R.Range(20, 60), -R.Range(10, 40), R.Range(0.5, 0.9), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.3));
            }
            if (k > 0.3 && t < CRASH && R.Chance(0.35))
            {
                var (x, y, _) = Lip(1, t);
                drops.Add(new WaterLib.Drop { x = x, z = R.Range(-3, 3), h = -y, vx = R.Range(30, 90), vz = R.Range(-10, 10), vh = R.Range(-20, 40), s = R.Chance(0.3) ? 2 : 1 });
            }
            if (!crashed && t >= CRASH)
            {
                crashed = true;
                double X0 = Lip(1, CRASH - 1e-3).x;
                lights.Add(X0, -20, 56, C[5], 0.8, 0.55);
                lights.Add(X0, -4, 22, WHITE, 0.6, 0.1);
                for (int i = 0; i < 46; i++)
                {
                    double a = R.Range(-0.9, 0.6) * Math.PI, v = R.Range(30, 120);
                    drops.Add(new WaterLib.Drop { x = X0 + R.Range(-6, 6), z = R.Range(-4, 4), h = R.Range(2, 8), vx = Px.Cos(a) * v * 0.85 + 18, vz = Px.Sin(a) * v * 0.5, vh = R.Range(120, 320), s = R.Chance(0.15) ? 3 : R.Chance(0.35) ? 2 : 1, delay = R.Range(0, 0.1) });
                }
                // the spit: a plume shot straight up where the lip lands
                for (int i = 0; i < 14; i++) drops.Add(new WaterLib.Drop { x = X0 + R.Range(-3, 3), z = R.Range(-2, 2), h = R.Range(2, 6), vx = R.Range(-15, 25), vz = R.Range(-8, 8), vh = R.Range(220, 300), s = R.Chance(0.3) ? 2 : 1, delay = R.Range(0.04, 0.16) });
                for (int i = 0; i < 34; i++) AddPuff(X0 + R.Range(-12, 10), -R.Range(2, 12), R.Range(-40, 130), -R.Range(30, 150), R.Range(1, 2), R.Range(1.8, 4.2), R.Range(0.4, 0.95), R.Range(0, 0.14));
                // fine spray hangs in the air over the burst
                for (int i = 0; i < 34; i++)
                {
                    double a = R.Range(-Math.PI * 0.95, -Math.PI * 0.05), v = R.Range(40, 160);
                    ps.Spawn(P.Flake, X0 + R.Range(-8, 8), -R.Range(4, 14), Px.Cos(a) * v + 25, Px.Sin(a) * v, R.Range(0.6, 1.3), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.4), R.Range(0, 0.1));
                }
                for (int i = 0; i < 12; i++) mist.Add(X0 + R.Range(-10, 14), -R.Range(3, 16), R.Range(-20, 70), -R.Range(6, 24), 3, R.Range(9, 15), R.Range(1.1, 1.7), R.Chance(0.5) ? C[6] : WHITE, 0.26, R.Range(0, 0.2));
                for (int i = 0; i < 10; i++) ps.Spawn(P.Star, X0 + R.Range(-20, 24), -R.Range(6, 40), 0, -5, R.Range(0.3, 0.6), C[6], R.Range(1, 2.4), R.Range(0.05, 0.4));
                rings.Add(X0, 0, 6, 40, 1.2, n: 2, gap: 3, k: 1.1, hot: 0.12);
            }
            // the whitewater surges on, boiling up foam at its front, then calms
            if (crashed && t < CRASH + 1.1 && R.Chance(0.45 * (1 - (t - CRASH) / 1.1)))
            {
                double x = CrestX(t) + R.Range(-6, 14);
                AddPuff(x, -R.Range(1, 4), R.Range(10, 40), -R.Range(10, 40), R.Range(0.9, 1.8), R.Range(1.8, 3.6), R.Range(0.4, 0.8));
            }
            if (crashed && t < CRASH + 0.8 && R.Chance(0.12)) mist.Add(CrestX(t) + R.Range(-8, 8), -R.Range(2, 6), R.Range(10, 40), -R.Range(3, 10), 3, R.Range(7, 11), R.Range(0.8, 1.1), C[6], 0.2);
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff f = puffs[i];
                if (f.delay > 0)
                {
                    f.delay -= dt;
                    continue;
                }
                if ((f.life -= dt) <= 0)
                {
                    puffs.RemoveAt(i);
                    continue;
                }
                f.vx -= f.vx * 2.5 * dt;
                f.vy += (160 - f.vy * 2.5) * dt;
                f.x += f.vx * dt;
                f.y = Math.Min(f.y + f.vy * dt, -1);
            }
            for (int i = plinks.Count - 1; i >= 0; i--)
                if ((plinks[i].age += dt) > 0.12) plinks.RemoveAt(i);
            drops.Update(dt);
            rings.Update(dt);
            mist.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        double Fade() => Smooth(0, 0.25, t) * (1 - Smooth(DURATION - 0.7, DURATION - 0.1, t));

        public override void DrawBack(Painter p, double tt)
        {
            double fade = Fade();
            Sea(p, fade);
            rings.Draw(p, C, -1);
            drops.Draw(p, C, -1);
        }

        public override void Draw(Painter p, double tt)
        {
            double fade = Fade();
            Body(p, fade);
            Curl(p);
            rings.Draw(p, C, 1);
            // lace left on the water behind the surge, thinning out
            if (crashed)
            {
                double k = 1 - Smooth(CRASH + 0.4, DURATION - 0.15, t), x0 = -20, x1 = CrestX(t) + 10;
                for (double X = Math.Floor(x0); X < x1; X++)
                {
                    double x = X + 0.5, n = 1 - Math.Abs(Noise2(x * 0.35, t * 0.8, S + 7)) * 4;
                    if (n > 1.15 - k * 1.1) p.Px(X, 0, n > 1.4 - k ? WHITE : C[6]);
                    if (n > 1.4 - k * 0.9) p.Px(X, 1, C[5]);
                }
            }
            foreach (Puff f in puffs)
            {
                if (f.delay > 0) continue;
                double g = 1 - f.life / f.max, r = (f.r0 + (f.r1 - f.r0) * Ease.OutCubic(g)) * (1 - Smooth(0.7, 1, g));
                if (r > 0.6) Billow(p, f.x, f.y, r, C);
            }
            foreach (Plink q in plinks)
            {
                if (q.age < 0.06) p.Px(q.x, q.y, WHITE);
                p.Glow(q.x, q.y, 4, C[5], 0.25 * (1 - q.age / 0.12));
            }
            drops.Draw(p, C, 1);
            // the crash: a white burst where the lip lands
            double fc = (t - CRASH) / 0.08;
            if (fc >= 0 && fc < 1)
            {
                double X0 = Lip(1, CRASH - 1e-3).x;
                p.Circle(X0, -3, 8 * (1 - fc * 0.4), WHITE);
                p.Ellipse(X0, 0, 14, 2.6, (d, a) => d < 0.6 ? WHITE : C[6]);
            }
            ps.Draw(p);
            mist.Draw(p, C[5]);
            lights.Draw(p);
            // light: the crest catches it while it stands, the whitewater after
            if (t < CRASH)
            {
                p.Glow(CrestX(t) + 4, -CrestH(t) + 2, 26, C[4], 0.22 * Smooth(0.2, CRASH, t));
                p.GlowLine(CrestX(t) - 26, -CrestH(t) * 0.45, CrestX(t), -CrestH(t), 10, C[5], 0.14 * Smooth(0.2, CRASH, t));
            }
            else p.Glow(CrestX(t), -10, 34, C[4], 0.26 * (1 - Smooth(CRASH, DURATION - 0.3, t)));
        }
    }
}
