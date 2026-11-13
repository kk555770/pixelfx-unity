// Shared by the projectile effects: a steady emitter for loops, motion in a projectile's own frame, soft smoke
// puffs, speed-stretched sparks, muzzle-flash lobes, brass casings that bounce, and outlined pixel shapes.
// The smoke puff, spark streak, flash lobe and casing recipes are adapted from Sprite Fusion's open-source
// muzzle-flash and explosion (https://www.spritefusion.com/pixel-art-vfx/muzzle-flash),
// MIT License, Copyright (c) 2026 Hugo Duprez. Files starting with _ are helpers, not effects.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class ProjectilesLib
    {
        /// <summary>
        /// Like Cycle(), but particle i is born exactly at i * period / n: an emitter with a steady beat, so a trail
        /// has no clumps or gaps. Loops seamlessly over period. fn(age, life, r, i), r the same rng every period.
        /// </summary>
        public static void Beat(double t, double period, int n, double seed, Func<Rng, double> life, Action<double, double, Rng, int> fn)
        {
            for (int i = 0; i < n; i++)
            {
                double L = life(NewRng(seed, i, 911));
                for (double age = Wrap(t - (i * period) / n + 1e-7, period); age < L; age += period) fn(age, L, NewRng(seed, i), i);
            }
        }

        public static void Beat(double t, double period, int n, double seed, double life, Action<double, double, Rng, int> fn)
        {
            for (int i = 0; i < n; i++)
            {
                double L = life;
                for (double age = Wrap(t - (i * period) / n + 1e-7, period); age < L; age += period) fn(age, L, NewRng(seed, i), i);
            }
        }

        /// <summary>
        /// Where something is after `age` seconds in the frame of a projectile that flies right at V px/s: launched
        /// from (x, y) at (vx, vy) relative to the still air, slowed toward the air by drag k (1/s), pulled by g.
        /// Returns [x, y, vx, vy] (screen motion).
        /// </summary>
        public static (double x, double y, double vx, double vy) Wake(double x, double y, double vx, double vy, double age, double V, double k, double g = 0)
        {
            double e = Exp(-k * age), f = (1 - e) / k, vt = g / k;
            return (x + vx * f - V * age, y + vt * age + (vy - vt) * f, vx * e - V, vt + (vy - vt) * e);
        }

        /// <summary>A soft smoke puff: a body, a denser core sitting low, a lit cap on top while it's young, light from a fire on one side.</summary>
        public static void PuffDisc(Painter p, double x, double y, double r, double a, uint c, double cap = 1, double warm = 0, uint wc = 0, double wa = 0)
        {
            if (a <= 0.01 || r < 0.5) return;
            p.Disc(x, y, r, c, a * 0.45);
            p.Disc(x + r * 0.05, y + r * 0.06, r * 0.74, c, a * 0.62);
            if (cap > 0.02) p.Disc(x - r * 0.22, y - r * 0.3, r * 0.5, Mix(c, WHITE, 0.14), a * 0.42 * cap);
            if (warm > 0.02) p.Disc(x + Px.Cos(wa) * r * 0.32, y + Px.Sin(wa) * r * 0.32, r * 0.55, wc, a * 0.7 * warm);
        }

        /// <summary>One puff of a cloud: position, radius, density, brightness 0..1, fire light 0..1.</summary>
        public sealed class Puff
        {
            public double x, y, r, a, b, w;
        }

        /// <summary>
        /// Puffs merged into one translucent volume: each puff is lit on top and shaded underneath, densities add up,
        /// and tone and density come out in clean steps. R: smoke ramp, dark to bright; WR: the same smoke lit by fire.
        /// </summary>
        public static void Cloud(Painter p, List<Puff> list, uint[] R, uint[] WR, double dens = 1.5, double max = 0.86, Func<double, double, bool> clip = null, double lit = 1)
        {
            if (list.Count == 0) return;
            double X0 = double.PositiveInfinity, Y0 = double.PositiveInfinity, X1 = double.NegativeInfinity, Y1 = double.NegativeInfinity;
            foreach (Puff q in list)
            {
                X0 = Math.Min(X0, Math.Floor(q.x - q.r));
                X1 = Math.Max(X1, Math.Ceiling(q.x + q.r));
                Y0 = Math.Min(Y0, Math.Floor(q.y - q.r));
                Y1 = Math.Max(Y1, Math.Ceiling(q.y + q.r));
            }
            int W = (int)(X1 - X0 + 3), H = (int)(Y1 - Y0 + 3);
            float[] D = new float[W * H], L = new float[W * H], F = new float[W * H];
            X0--;
            Y0--;
            foreach (Puff q in list)
            {
                double r = q.r, r2 = r * r;
                for (double y = Math.Floor(q.y - r); y <= Math.Ceiling(q.y + r); y++)
                {
                    double dy = y + 0.5 - q.y;
                    for (double x = Math.Floor(q.x - r); x <= Math.Ceiling(q.x + r); x++)
                    {
                        double dx = x + 0.5 - q.x, d2 = (dx * dx + dy * dy) / r2;
                        if (d2 >= 1) continue;
                        // a ball's thickness: flat in the middle, steep at the rim, so every lump keeps its own crown
                        double k = Math.Sqrt(1 - d2) * q.a;
                        int i = (int)((y - Y0) * W + (x - X0));
                        D[i] = (float)(D[i] + k);
                        L[i] = (float)(L[i] + k * q.b);
                        F[i] = (float)(F[i] + k * q.w * (0.5 + 0.5 * Clamp(0.5 + dx / r)));
                    }
                }
            }
            // light the merged surface: where density climbs downward the cloud faces up and catches the light. The
            // gradient comes off a blurred copy, so only lump-sized features catch it, never the crossing rims
            float[] B = Blur(Blur(D, W, H, 1, 2), W, H, W, 2);
            int n = R.Length;
            p.Each(X0 + 1, Y0 + 1, X1 + 1, Y1 + 1, (lx, ly, sx, sy) =>
            {
                double cx = Math.Floor(lx) - X0, cy = Math.Floor(ly) - Y0;
                if (cx < 1 || cy < 1 || cx >= W - 1 || cy >= H - 1) return;
                int i = (int)(cy * W + cx);
                double d = D[i];
                if (d < 0.05 || (clip != null && !clip(lx, ly))) return;
                double a = Math.Min(max, Math.Ceiling((1 - Exp(-d * dens)) * 6) / 6);
                if (a < 0.15) return;
                double gy = (double)B[i + W] - B[i - W], gx = (double)B[i + 1] - B[i - 1];
                double up = Clamp(0.5 + Atan((gy * 0.9 + gx * 0.35) / (B[i] * 0.16 + 0.03)) * 0.42 * lit);
                double w = F[i] / d;
                int j = (int)Clamp(Round((L[i] / d) * (0.3 + 0.7 * up) * (n - 1) + 0.25 + w * 1.5), 0, n - 1);
                p.BlendPx(sx, sy, w > 0.03 ? Mix(R[j], WR[j], Math.Min(0.9, Math.Ceiling(w * 10) / 10)) : R[j], a);
            });
        }

        /// <summary>A smoke puff Smoke simulates; fields left out keep the JS defaults.</summary>
        public sealed class SmokeQ
        {
            public double x, y, vx, vy, r0, r1, life, drag = 2.5, rise = 12, grow = 2, curl, a = 0.6, b = 0.85, warm, delay, age, seed;
        }

        /// <summary>
        /// Simulated smoke for one-shots, drawn with Cloud(): puffs push out, slow (drag), lift (rise), sway, swell from
        /// r0 to r1, thin away and darken as they age; warm fades fast; curl keeps turning a puff's heading.
        /// </summary>
        public sealed class Smoke
        {
            readonly Rng R;
            readonly List<SmokeQ> list = new List<SmokeQ>();
            double t;

            public Smoke(Rng rng) => R = rng;

            public void Add(SmokeQ q)
            {
                q.seed = R.Next() * 100;
                list.Add(q);
            }

            public int Count => list.Count;

            public void Update(double dt)
            {
                t += dt;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    SmokeQ q = list[i];
                    if (q.delay > 0)
                    {
                        q.delay -= dt;
                        continue;
                    }
                    if ((q.age += dt) >= q.life)
                    {
                        list.RemoveAt(i);
                        continue;
                    }
                    if (q.curl != 0 && !double.IsNaN(q.curl))
                    {
                        double c = Px.Cos(q.curl * dt), s = Px.Sin(q.curl * dt), vx = q.vx;
                        q.vx = vx * c - q.vy * s;
                        q.vy = vx * s + q.vy * c;
                    }
                    q.vy -= q.rise * dt;
                    q.vx += Px.Sin(t * 1.3 + q.seed) * 8 * dt;
                    q.vx -= q.vx * q.drag * dt;
                    q.vy -= q.vy * q.drag * dt;
                    q.x += q.vx * dt;
                    q.y += q.vy * dt;
                }
            }

            public List<Puff> Puffs()
            {
                var o = new List<Puff>();
                foreach (SmokeQ q in list)
                {
                    if (q.delay > 0) continue;
                    double k = q.age / q.life;
                    o.Add(new Puff { x = q.x, y = q.y, r = q.r0 + (q.r1 - q.r0) * (1 - Exp(-q.grow * q.age)), a = q.a * Math.Min(1, q.age * 30) * Px.Pow(1 - k, 1.3), b = q.b * (1 - k * 0.5), w = q.warm * Exp(-q.age * 5) });
                }
                return o;
            }
        }

        // a box blur of radius r along one axis of a w x h field (step 1 for rows, w for columns)
        static float[] Blur(float[] src, int w, int h, int step, int r)
        {
            float[] o = new float[src.Length];
            int n = step == 1 ? w : h, lines = step == 1 ? h : w;
            double k = 1.0 / (2 * r + 1);
            for (int l = 0; l < lines; l++)
            {
                int bse = step == 1 ? l * w : l;
                double acc = 0;
                for (int j = -r; j <= r; j++) acc += j >= 0 && j < n ? src[bse + j * step] : 0;
                for (int j = 0; j < n; j++)
                {
                    o[bse + j * step] = (float)(acc * k);
                    int a = j - r, b = j + r + 1;
                    if (a >= 0) acc -= src[bse + a * step];
                    if (b < n) acc += src[bse + b * step];
                }
            }
            return o;
        }

        /// <summary>
        /// A spark: a two-tone streak stretched along its screen velocity, its head cooling down S (hot, warm, cool,
        /// tail colors) as k (life left, 1..0) falls, with a point of light.
        /// </summary>
        public static void Spark(Painter p, double x, double y, double vx, double vy, double k, uint[] S, double glow = 0.45, double stretch = 0.022, double max = 9)
        {
            double sp = Hypot(vx, vy);
            if (sp == 0 || double.IsNaN(sp)) sp = 1;
            double ux = vx / sp, uy = vy / sp, len = Math.Min(max, 1 + sp * stretch);
            uint head = k > 0.6 ? S[0] : k > 0.3 ? S[1] : S[2];
            p.Line(x - ux * len, y - uy * len, x - ux * len * 0.4, y - uy * len * 0.4, k > 0.5 ? head : S[3]);
            p.Line(x - ux * len * 0.45, y - uy * len * 0.45, x, y, head);
            p.Glow(x, y, 3, head, glow * k);
        }

        /// <summary>One lobe of a muzzle flash: offset from the muzzle, angle, length, half-width, and how deep its hot core runs.</summary>
        public sealed class Lobe
        {
            public double off, a, len, wid, core;
        }

        /// <summary>
        /// The lobes of one muzzle flash, drawn from rng r: a fat fireball at the muzzle, a spike forward, and a burst of
        /// rays round it. Frame f 2 is the same shape cooled, shorter and thinner.
        /// </summary>
        public static List<Lobe> FlashShape(Rng r, int f, double size = 1, double[] spike = null, double[] diag = null, double[] cross = null, double[] back = null, double core = 0.7)
        {
            spike = spike ?? new double[] { 12, 20 };
            diag = diag ?? new double[] { 5, 10 };
            cross = cross ?? new double[] { 3, 6 };
            back = back ?? new double[] { 2, 4.5 };
            double k = f == 1 ? 1 : 0.6, w = f == 1 ? 1 : 0.72, S = size;
            var L = new List<Lobe>();
            L.Add(new Lobe { off = 0.5, a = r.Range(-0.08, 0.08), len = r.Range(5.5, 8) * S * k, wid = r.Range(2.1, 2.6) * S * w, core = 0.5 * core });
            L.Add(new Lobe { off = 2, a = r.Range(-0.07, 0.07), len = r.Range(spike[0], spike[1]) * S * k, wid = r.Range(1, 1.3) * S * w, core = 0.35 * core });
            void Ray(double a, double len, double wid, bool on)
            {
                if (on) L.Add(new Lobe { off = r.Range(0.5, 2), a = a, len = len * S * k, wid = wid * S * w, core = 0.25 * core });
            }
            foreach (int s in new[] { -1, 1 })
            {
                Ray(s * r.Range(0.3, 0.8), r.Range(diag[0], diag[1]), r.Range(1.1, 1.5), r.Chance(0.85));
                Ray(s * r.Range(1.15, 1.7), r.Range(cross[0], cross[1]), r.Range(1, 1.3), r.Chance(0.6));
                Ray(s * r.Range(2, 2.7), r.Range(back[0], back[1]), 1, r.Chance(0.7) && f == 1);
            }
            return L;
        }

        static double Profile(Lobe l, double u)
        {
            double k = u / l.len;
            return l.wid * Px.Pow(Math.Max(0, 1 - k), 0.8) * Math.Min(1, 0.45 + k * 3);
        }

        /// <summary>
        /// Draw flash lobes L at (x, y) pointing along angle a: a white (or hot) core, a hot inner flame, a body and
        /// a ragged rim. C: [core, inner, body, rim]. seed varies the ragged edge. Returns the flash's reach.
        /// </summary>
        public static double Flash(Painter p, double x, double y, double a, List<Lobe> L, uint[] C, double seed)
        {
            double c = Px.Cos(a), s = Px.Sin(a);
            double ext = 2;
            foreach (Lobe l in L) ext = Math.Max(ext, l.off + l.len + 2);
            p.Shade(x - ext, y - ext, x + ext, y + ext, (px, py, o) =>
            {
                double dx = px - x, dy = py - y, along = dx * c + dy * s, across = -dx * s + dy * c;
                double m = -9;
                bool hot = false, warm = false;
                foreach (Lobe l in L)
                {
                    double ca = Px.Cos(l.a), sa = Px.Sin(l.a), lu = (along - l.off) * ca + across * sa, lv = -(along - l.off) * sa + across * ca;
                    if (lu < -1.5 || lu > l.len + 1) continue;
                    double r = Profile(l, Math.Max(0, lu)), mm = r - Math.Abs(lv);
                    if (mm > m) m = mm;
                    if (lu < l.len * l.core && Math.Abs(lv) <= r * 0.5) hot = true;
                    if (mm > r * 0.4) warm = true;
                }
                double dm = 1.6 - Hypot(along - 0.5, across);
                if (dm > m) m = dm;
                if (dm > 0.6) hot = true;
                m += (Rand(Math.Floor(px) * 73 + Math.Floor(py) * 151, seed) - 0.5) * 0.5 * Math.Min(1, ext / 14);
                if (m < -0.85) return 0;
                return m < 0 ? C[3] : hot ? C[0] : warm ? C[1] : C[2];
            });
            return ext;
        }

        /// <summary>Where a casing is: position, angle, whether it has come to rest, and seconds since its last bounce (or -1).</summary>
        public struct CasingAt
        {
            public double x, y, a, hit;
            public bool rest;
        }

        /// <summary>
        /// A brass casing thrown from (x, y) at (vx, vy) with spin va, falling under g onto a floor at y = floor:
        /// it bounces (keeping `bounce` of its speed) until it's too slow, then slides to a stop. Closed form.
        /// </summary>
        public static CasingAt Casing(double x, double y, double vx, double vy, double va, double age, double g, double floor, double bounce = 0.42, double a0 = 0)
        {
            double t0 = 0, a = a0, hit = -1;
            for (int n = 0; n < 6; n++)
            {
                // time to reach the floor from (x, y) moving (vx, vy)
                double d = floor - y, T = (-vy + Math.Sqrt(Math.Max(0, vy * vy + 2 * g * d))) / g;
                if (age - t0 < T)
                {
                    double u = age - t0;
                    return new CasingAt { x = x + vx * u, y = y + vy * u + 0.5 * g * u * u, a = a + va * u, rest = false, hit = hit };
                }
                x += vx * T;
                a += va * T;
                t0 += T;
                hit = age - t0;
                double vin = vy + g * T;
                y = floor;
                if (vin * bounce < 45)
                {
                    // too slow to hop again: slide to a stop, settling flat
                    double u = age - t0, k = 7, s = (1 - Exp(-k * u)) / k;
                    return new CasingAt { x = x + vx * 0.6 * s, y = floor, a = Round(a / Math.PI) * Math.PI, rest = true, hit = hit };
                }
                vy = -vin * bounce;
                vx *= 0.55;
                va *= 0.6;
            }
            return new CasingAt { x = x, y = floor, a = Round(a / Math.PI) * Math.PI, rest = true, hit = hit };
        }

        /// <summary>Draw a casing: a 3 px brass slug turned by its angle, lit on one end; a glint when it has just hit.</summary>
        public static void DrawCasing(Painter p, CasingAt k, uint[] B, double fade = 1)
        {
            double ca = Px.Cos(k.a), sa = Px.Sin(k.a), x = k.x, y = k.y - 0.5;
            p.Dot(x - ca, y - sa, B[1], fade);
            p.Dot(x, y, B[2], fade);
            p.Dot(x + ca, y + sa, B[3], fade);
            if (k.hit >= 0 && k.hit < 0.07)
            {
                p.Px(x + ca * 0.5, y - 1, WHITE);
                p.Glow(x, y, 5, B[3], 0.55 * (1 - k.hit / 0.07));
            }
        }

        /// <summary>
        /// A little sprite turned by ang about its pivot (px, py in sprite pixels) placed at (x, y), sampled
        /// nearest-pixel so it stays hard-edged at any angle.
        /// </summary>
        public static void TurnSprite(Painter p, string[] rows, IReadOnlyDictionary<char, uint> pal, double x, double y, double ang, double px, double py, double a = 1)
        {
            int H = rows.Length, W = rows[0].Length;
            double cs = Px.Cos(ang), sn = Px.Sin(ang), R = Hypot(Math.Max(px, W - px), Math.Max(py, H - py)) + 1;
            p.Shade(x - R, y - R, x + R, y + R, (lx, ly, o) =>
            {
                double dx = lx - x, dy = ly - y, i = Math.Floor(dx * cs + dy * sn + px), j = Math.Floor(-dx * sn + dy * cs + py);
                if (i < 0 || j < 0 || i >= W || j >= H) return 0;
                if (!pal.TryGetValue(rows[(int)j][(int)i], out uint c) || c == 0) return 0;
                o.c = c;
                o.a = a;
                return 0;
            });
        }

        /// <summary>
        /// A pixel shape from a builder: build(set) calls set(px, py, ch) for every interior pixel; an outline ('o')
        /// is added round it. Returns rows ready for p.Sprite at (x0, y0).
        /// </summary>
        public static (string[] rows, double x0, double y0) PixelShape(int x0, int y0, int w, int h, Action<Action<int, int, char>> build)
        {
            var g = new char[h][];
            for (int j = 0; j < h; j++)
            {
                g[j] = new char[w];
                for (int i = 0; i < w; i++) g[j][i] = '.';
            }
            build((px, py, ch) =>
            {
                int i = px - x0, j = py - y0;
                if (i >= 0 && j >= 0 && i < w && j < h) g[j][i] = ch;
            });
            bool Solid(int i, int j) => i >= 0 && j >= 0 && i < w && j < h && g[j][i] != '.' && g[j][i] != 'o';
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    if (g[j][i] == '.' && (Solid(i - 1, j) || Solid(i + 1, j) || Solid(i, j - 1) || Solid(i, j + 1))) g[j][i] = 'o';
            var rows = new string[h];
            for (int j = 0; j < h; j++) rows[j] = new string(g[j]);
            return (rows, x0, y0);
        }
    }
}
