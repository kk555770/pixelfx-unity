// The layered flame tongues and the licks, embers and smoke shed off a burning edge follow Sprite Fusion's
// open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class BurnAway : Effect
    {
        const double D = 3;

        public static readonly Module Module = Screen("burn-away", "Burn away",
            "The screen burns in from its edges behind a ragged ember line of licking flames, embers and smoke; then the black smoulders and burns open from the middle.",
            new[] { "burn", "fire", "embers", "smoke", "wipe" }, D,
            new Options { ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" }, ["seed"] = 3 },
            (o, e) => new BurnAway(o, e));

        const double CLOSE = 1.18, COVER = 1.35, SMOULDER = 1.5, POP = 1.8, OPEN = 2.62, SC = 90, HALO = 24, POOL = 48;
        static readonly uint[] ASH = Ramp("#a39cab", "#7b7484", "#5c5664", "#433e4b");
        // a tongue's layers: [width, height, palette step], outermost (red) to the white core
        static readonly double[][] LAYERS = { new double[] { 1, 1, 2 }, new[] { 0.76, 0.86, 4 }, new[] { 0.54, 0.68, 6 }, new[] { 0.3, 0.42, 8 } };
        // what a patch of edge gives off as the front crosses it, per pixel crossed: [flame, ember, smoke, smoke on the
        // hottest stretches, ash, light]. The black burning open sheds fewer embers and less ash.
        static readonly double[] RATE_IN = { 0.006, 0.0014, 0.0006, 0.0024, 0.0012, 0.0004 }, RATE_OUT = { 0.006, 0.0005, 0.0004, 0.0012, 0.0004, 0.0004 };

        // where the fire reaches when: rising from the edges' midpoints (the corners go first), ragged at two scales,
        // and the bottom burns a little faster because fire climbs
        static Field InField(double seed) => new Field((x, y) =>
        {
            double ux = (x - W / 2.0) / (W / 2.0), uy = (y - H / 2.0) / (H / 2.0), m = Px.Pow(Px.Pow(ux, 4) + Px.Pow(uy, 4), 0.25);
            return (1 - m) * SC + 14 * Fbm(x / 44, y / 44, seed, 3) + 2.4 * Fbm(x / 7, y / 7, seed + 9, 2) + 7 * (0.5 - y / H);
        });

        // the hole: from a spot near the middle (and two smaller ones that catch a beat later) out to the corners
        static Field OutField(double seed)
        {
            var R = new Rng(seed * 31 + 5);
            double ox = W / 2.0 + R.Range(-26, 26), oy = H / 2.0 + R.Range(-14, 6);
            var sats = new (double x, double y, double d)[2];
            for (int k = 0; k < 2; k++)
            {
                int s = k == 0 ? -1 : 1;
                double sx = ox + s * R.Range(70, 110), sy = oy + R.Range(-40, 40), sd = R.Range(30, 44);
                sats[k] = (sx, sy, sd);
            }
            var f = new Field((x, y) =>
            {
                double v = Hypot((x - ox) / (W / 2.0), (y - oy) / (H / 2.0)) * SC;
                foreach (var s in sats) v = Math.Min(v, s.d + Hypot((x - s.x) / (W / 2.0), (y - s.y) / (H / 2.0)) * SC * 1.15);
                return v + 13 * Fbm(x / 40, y / 40, seed + 21, 3) + 2.4 * Fbm(x / 7, y / 7, seed + 33, 2);
            });
            f.ox = ox;
            f.oy = oy;
            return f;
        }

        // a flame standing on (x, y): four layers of the ramp, each narrower, shorter and hotter, swaying at the tip
        static void Tongue(Painter p, uint[] F, double x, double y, double h, double w, double sway, double t, double n)
        {
            foreach (double[] layer in LAYERS)
            {
                double lw = layer[0], lh = layer[1], Ht = h * lh;
                uint col = F[(int)layer[2]];
                for (int r = 0; r <= Ht; r++)
                {
                    double u = r / Math.Max(1, Ht), half = w * lw * Px.Pow(1 - u, 0.85) * Math.Min(1, (r + 1.5) / 3.5) + Noise(t * 9 + r * 0.45, n) * 0.5;
                    if (half < 0.3) continue;
                    double cx = x + sway * u * u * lh + Noise(t * 4 + r * 0.2, n + 7) * u * 1.1;
                    for (double px = Round(cx - half); px <= Round(cx + half); px++) p.Px(px, Round(y - r), col);
                }
            }
        }

        readonly uint[] F;
        readonly uint ink, CHAR, SCORCH, WARM, SMOKE, LIT, DIM;
        readonly Rng R;
        readonly double seed;
        readonly Field A, B;
        readonly float[] heat = new float[W * H], fleck = new float[W * H], jit = new float[W * H], vein = new float[W * H];
        readonly byte[] ph = new byte[W * H];
        readonly float[] FL = new float[64], HV = new float[64];
        readonly byte[] TW = new byte[64];
        readonly Particles ps, ash;
        readonly Lights lights;
        readonly List<int> line = new List<int>(); // the white-hot line's pixels this frame: where flames stand
        double t;
        int pin, pout;
        bool closed, popped;

        BurnAway(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 9);
            ink = o.Color("color");
            R = new Rng(o.Seed);
            seed = o.Seed * 13;
            CHAR = Mix(ink, F[0], 0.45);
            SCORCH = Mix(ink, Hex("#3a2216"), 0.6);
            WARM = Mix(F[4], F[5], 0.4);
            SMOKE = Mix(Hex("#8a7276"), F[2], 0.2);
            A = InField(seed);
            B = OutField(seed + 1);
            // per pixel: how hot the ember line burns there (two smooth fields), and the odd fleck that glows on
            // (ph: a smooth phase 0..63 that the flicker tables are read with; jit: a ragged char rim)
            // (vein: near 0 along a web of thin curving cracks, where the char smoulders before it burns open)
            for (int i = 0; i < W * H; i++)
            {
                int x = i % W, y = i / W;
                heat[i] = (float)(0.5 + 0.5 * Noise2(x / 13.0, y / 13.0, seed + 41));
                ph[i] = (byte)Math.Min(63, Math.Floor((0.5 + 0.5 * Noise2(x / 23.0, y / 23.0, seed + 43)) * 64));
                fleck[i] = (float)(Rand(x, y, seed + 45) < 0.04 ? 9 + 22 * Rand(x, y, seed + 47) : 0);
                jit[i] = (float)((Rand(x, y, seed + 49) - 0.5) * 1.5);
                double rx = x * 0.8 + y * 0.6, ry = y * 0.8 - x * 0.6; // off the pixel grid, so cracks don't run square
                vein[i] = (float)Math.Min(Math.Abs(Noise2(rx / 11, ry / 9, seed + 61)), Math.Abs(Noise2(ry / 5.5, rx / 5, seed + 63)) * 1.5);
            }
            ps = new Particles(rng: R, sparks: new[] { F[7], F[6], F[4], F[2] }, emitter: e);
            ash = new Particles(rng: R, sparks: ASH, emitter: e);
            lights = new Lights(e);
            LIT = Mix(Hex("#8a7276"), F[3], 0.25);
            DIM = Sim.SMOKE_RAMP[3];
        }

        double KIn(double t) => A.lo - 8 + (A.hi - A.lo + 8) * Px.Pow(Math.Max(0, t) / CLOSE, 1.25);

        double KOut(double t) =>
            t < POP ? B.lo - 24 + 24 * Ease.InQuad(Clamp((t - SMOULDER) / (POP - SMOULDER))) : B.lo + (B.hi - B.lo + 16) * Ease.OutQuad(Clamp((t - POP) / (OPEN - POP)));

        // what a patch of edge gives off as the front crosses it: licks of flame, embers, ash, now and then a cloud
        // of smoke (a run of puffs from one spot, so it rolls up as a wisp, not a dot) and a flicker of light
        void Shed(int i, bool inA)
        {
            // what burns in is gone before the black burns open, so the uncovered picture starts clean
            double[] rate = inA ? RATE_IN : RATE_OUT;
            double fl = rate[0], em = rate[1], sm = rate[2], sh = rate[3], @as = rate[4], li = rate[5];
            double x = (i % W) + 0.5, y = (i / W) + 0.5, end = inA ? POP + 0.1 : D - 0.05;
            double r = R.Next();
            if ((r -= fl) < 0)
            {
                double life = LifeLeft(R.Range(0.18, 0.4), t, end);
                if (life > 0.08) ps.Spawn(P.Fire, x, y - 3, R.Range(-8, 8), -R.Range(25, 50), life, 0, R.Range(0.8, 1.7));
            }
            else if ((r -= em) < 0)
            {
                double life = LifeLeft(inA ? R.Range(0.6, 1.3) : R.Range(0.5, 1), t, end);
                if (life > 0.2) ps.Spawn(P.Ember, x, y - 2, R.Range(-30, 30), -R.Range(40, 100), life, 0, R.Chance(0.25) ? 2 : 1);
            }
            else if ((r -= sm + sh * Px.Pow(heat[i], 2)) < 0)
            {
                // smoke lives long only where the front chases it upward; elsewhere the front leaves it, so it's a wisp
                int n = R.Int(4, 6);
                double vx = R.Range(-6, 6);
                bool up = (inA ? A : B).ny[i] < -0.25;
                for (int j = 0; j < n; j++)
                {
                    double life = LifeLeft(up ? R.Range(0.8, 1.3) : R.Range(0.35, 0.65), t + j * 0.05, end);
                    if (life > 0.4) ps.Spawn(P.Smoke, x + R.Range(-3.5, 3.5), y - 4 - j * 1.2, vx + R.Range(-3, 3), -R.Range(18, 34), life, SMOKE, R.Range(2.8, 4.8), j * 0.05);
                }
            }
            else if ((r -= @as) < 0)
            {
                double life = LifeLeft(R.Range(0.9, 1.6), t, end);
                if (life > 0.3) ash.Spawn(P.Ember, x, y - 2, R.Range(-18, 18), -R.Range(25, 60), life);
            }
            else if ((r -= li) < 0) lights.Add(x, y - 4, R.Range(20, 32), WARM, R.Range(0.14, 0.26), R.Range(0.18, 0.4));
        }

        // smoke shows where firelight catches it: lit warm near the burning line, dark over the picture, all but lost
        // in the black away from the fire (the sim's smoke puff, three soft discs, shaded by that light)
        void DrawSmoke(Painter p, Painter p0)
        {
            bool inPhase = t < COVER, open = t >= SMOULDER && t < OPEN + 0.25;
            double k = inPhase ? KIn(t) : KOut(t);
            float[] f = inPhase ? A.f : B.f;
            foreach (Particles.Particle q in ps.List)
            {
                if (q.k != P.Smoke || q.delay > 0) continue;
                double kk = q.life / q.max, age = 1 - kk, r = q.size * (0.65 + Math.Min(1, age * 2.5) * 0.45 + age * 0.4);
                int i = (int)Clamp(Math.Floor(q.y), 0, H - 1) * W + (int)Clamp(Math.Floor(q.x), 0, W - 1);
                double e = inPhase || open ? (inPhase ? k - f[i] : f[i] - k) : 99, lit = Clamp(1 - Math.Abs(e) / 30);
                double a = Math.Min(1, age * 12) * Math.Min(1, kk * 2.4) * 0.58 * (e > 0 ? 0.05 + 0.95 * lit : inPhase ? 1 : 0.2 + 0.8 * lit);
                if (a < 0.04) continue;
                // a puff wholly on screen needs no clipping
                uint c = Mix(Mix(DIM, LIT, lit), Sim.SMOKE_RAMP[2], age * 0.5);
                Painter d = q.x - r > 0 && q.y - r > 0 && q.x + r < W - 1 && q.y + r < H - 1 ? p0 : p;
                d.Disc(q.x, q.y, r, c, a * 0.45);
                d.Disc(q.x + r * 0.05, q.y + r * 0.06, r * 0.74, c, a * 0.62);
                if (age < 0.5) d.Disc(q.x - r * 0.22, q.y - r * 0.3, r * 0.5, Mix(c, WHITE, 0.15), a * 0.4 * (1 - age * 2));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the front eats inward: everything it passes this step gives off flame, embers, ash and smoke
            if (t < COVER)
            {
                double k = KIn(t);
                while (pin < A.order.Length && A.f[A.order[pin]] <= k) Shed(A.order[pin++], true);
                if (!closed && t >= CLOSE)
                {
                    // the last of the picture goes up in a puff
                    closed = true;
                    int i = A.order[A.order.Length - 1], x = i % W, y = i / W;
                    lights.Add(x, y, 64, WARM, 0.5, 0.4);
                    for (int j = 0; j < 12; j++) ps.Spawn(P.Ember, x + R.Range(-6, 6), y + R.Range(-3, 3), R.Range(-50, 50), -R.Range(40, 120), R.Range(0.5, 0.75), 0, R.Chance(0.3) ? 2 : 1);
                    for (int j = 0; j < 5; j++) ps.Spawn(P.Smoke, x + R.Range(-8, 8), y + R.Range(-4, 2), R.Range(-10, 10), -R.Range(12, 28), R.Range(1.1, 1.6), SMOKE, R.Range(3.5, 5.5), R.Range(0, 0.15));
                }
            }
            // the black smoulders in the middle, then burns open
            if (t >= SMOULDER && t < POP && R.Chance(0.1))
            {
                int i = B.order[R.Int(0, 400)];
                double x = (i % W) + 0.5, y = (i / W) + 0.5;
                if (R.Chance(0.6)) ps.Spawn(P.Smoke, x, y, R.Range(-4, 4), -R.Range(8, 16), LifeLeft(R.Range(0.9, 1.4), t, D - 0.05), SMOKE, R.Range(2, 3.5));
                else ps.Spawn(P.Ember, x, y, R.Range(-12, 12), -R.Range(15, 40), R.Range(0.4, 0.8));
            }
            if (!popped && t >= POP)
            {
                popped = true;
                lights.Add(B.ox, B.oy, 90, WARM, 0.6, 0.45);
                lights.Add(B.ox, B.oy, 34, F[7], 0.7, 0.12);
                for (int j = 0; j < 12; j++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(50, 150);
                    ps.Spawn(P.Ember, B.ox + Px.Cos(a) * 3, B.oy + Px.Sin(a) * 2, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.7 - 40, R.Range(0.4, 0.9), 0, R.Chance(0.3) ? 2 : 1);
                }
                for (int j = 0; j < 10; j++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(120, 260);
                    ps.Spawn(P.Spark, B.ox, B.oy, Px.Cos(a) * sp, Px.Sin(a) * sp - 50, R.Range(0.15, 0.35));
                }
            }
            if (t >= POP && t < OPEN + 0.2)
            {
                double k = KOut(t);
                while (pout < B.order.Length && B.f[B.order[pout]] <= k) Shed(B.order[pout++], false);
            }
            ps.Update(dt);
            ash.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p0)
        {
            Painter p = ClipTo(p0);
            line.Clear();
            if (t >= COVER && t < SMOULDER) Rect(p0, 0, 0, W, H, ink);
            else if (t < OPEN + 0.25)
            {
                bool inPhase = t < COVER;
                double k = inPhase ? KIn(t) : KOut(t);
                Field Fd = inPhase ? A : B;
                float[] f = Fd.f, g = Fd.g;
                double fade = inPhase ? 1 - Smooth(COVER - 0.15, COVER, t) : 1;
                for (int j = 0; j < 64; j++)
                {
                    double q = j / 63.0;
                    FL[j] = (float)(0.75 + 0.25 * Px.Sin(t * 7 + q * 9));
                    HV[j] = (float)(0.3 * Px.Sin(t * 5.5 + q * 11));
                    TW[j] = (byte)(Px.Sin(t * 13 + q * 40) > -0.5 ? 1 : 0);
                }
                // only the pixels near the burning line need working out (a slice of the field's order): the cover
                // behind it is plain ink, the picture far ahead of it untouched. Straight to the frame: all on screen.
                var px = new Pixels(p0);
                int[] ord = Fd.order;
                int N = ord.Length;
                // before the black burns open it smoulders: a web of cracks in the hole's own shape glows, spreads and
                // heats from dark red, then the hole bursts through it and it cools
                double su = Clamp((t - SMOULDER) / (POP - SMOULDER)), SR = 5 + 17 * su;
                double sk = inPhase || t < SMOULDER ? 0 : t < POP ? 0.3 + 0.7 * su : 1 - (t - POP) / 0.3;
                void ShadeAt(int i)
                {
                    // e: how far into the cover this pixel is in field units (smooth, for light); s: in px from the
                    // burning line (sharp near it, for the line itself)
                    double e = inPhase ? k - f[i] : f[i] - k, s = e * g[i];
                    px.At(i);
                    if (e < 0)
                    {
                        // the picture just ahead of the fire: scorched, and lit by it
                        if (inPhase && s > -2.5) px.Blend(SCORCH, 0.5 * (1 + s / 2.5));
                        double u0 = Math.Max(0, 1 + e / HALO), v0 = 1 + e / POOL;
                        px.Add(WARM, (0.3 * u0 * u0 + 0.12 * v0 * v0) * FL[ph[i]]);
                        return;
                    }
                    double hv = Clamp((double)heat[i] + HV[ph[i]]), th = 0.6 + 0.6 * hv, u = s / th;
                    if (u < 6.4)
                    {
                        // the ember line: white-hot at the edge, cooling down the ramp into char
                        if (u < 0.9) line.Add(i);
                        px.Put(u < 0.9 ? (hv > 0.6 ? F[8] : F[7]) : u < 1.7 ? F[6] : u < 2.4 ? F[5] : u < 3.1 ? F[4] : u < 3.8 ? F[3] : u < 4.6 ? F[2] : u < 5.5 ? F[1] : F[0]);
                        if (u < 4.6) px.Add(WARM, 0.45 * (1 - u / 4.6) * (0.7 + 0.3 * hv));
                        return;
                    }
                    if (sk > 0 && f[i] < B.lo + SR)
                    {
                        double hm = (1 - (f[i] - B.lo) / SR) * sk * (0.7 + 0.6 * hv);
                        if (vein[i] < 0.04 + 0.08 * hm)
                        {
                            px.Put(hm > 0.95 ? F[6] : hm > 0.75 ? F[5] : hm > 0.55 ? F[4] : hm > 0.38 ? F[3] : hm > 0.22 ? F[2] : F[1]);
                            px.Add(WARM, 0.4 * hm);
                            return;
                        }
                        px.Add(WARM, 0.2 * hm * hm);
                    }
                    // char, with the odd fleck still glowing in it
                    double L = fleck[i] * (0.8 + 0.4 * hv);
                    if (L > 0 && s < L)
                    {
                        double v = (s - 6.4 * th) / Math.Max(1, L - 6.4 * th);
                        if (v < 1 && TW[(ph[i] * 5) & 63] != 0 && !(fade < 1 && fade <= jit[i] / 1.5 + 0.5))
                        {
                            px.Put(v < 0.3 ? F[4] : v < 0.55 ? F[3] : v < 0.8 ? F[2] : F[1]);
                            px.Add(WARM, 0.3 * (1 - v));
                            return;
                        }
                    }
                    if (e < 22) px.Add(WARM, 0.2 * Px.Pow(1 - e / 22, 2));
                    px.Put(u - 6.4 < 1.6 * th + jit[i] ? CHAR : ink);
                }
                // [c0, c1): deep in the cover; [a0, a1): near the line
                int lo = Fd.Below(k - (inPhase ? 33 : POOL)), hi = Fd.Below(Math.Max(k + (inPhase ? POOL : 33), sk > 0 ? B.lo + SR : -1e9));
                int c0 = inPhase ? 0 : hi, c1 = inPhase ? lo : N, a0 = lo, a1 = hi;
                for (int j = c0; j < c1; j++)
                {
                    int i = ord[j];
                    if (fleck[i] != 0) ShadeAt(i);
                    else
                    {
                        px.At(i);
                        px.Put(ink);
                    }
                }
                for (int j = a0; j < a1; j++) ShadeAt(ord[j]);
                // flames stand on the white-hot line: tall and many where the char lies below it and they rise clear,
                // short and few where they'd only lick up over the char
                foreach (int i in line)
                {
                    int x = i % W, y = i / W;
                    double dn = inPhase ? -(double)Fd.ny[i] : Fd.ny[i], h0 = Rand(x, y >> 3, seed + 51);
                    if (h0 > 0.12 + 0.6 * Math.Max(0, dn)) continue;
                    int n = (x * 7 + (y >> 4) * 131) % 977;
                    double hv = heat[i];
                    double leap = 1 + Math.Max(0, Noise(t * 2.6 + n * 3.1, n + 70) - 0.5) * 1.8;
                    double h = (5 + 7 * Rand(x, 1, seed + 53)) * (0.6 + 0.4 * Noise(t * 3.3 + n * 1.7, n)) * (0.6 + 0.6 * hv) * leap * fade * (dn > 0 ? 0.65 + 0.5 * dn : 0.5);
                    if (h >= 1.5) Tongue(p, F, x + 0.5, y + 1, h, (1.1 + 0.7 * hv) * (0.8 + h / 16), Noise(t * 2.2 + n, n + 40) * 1.5, t, n);
                }
            }
            DrawSmoke(p, p0);
            ash.Draw(p, Layer.Front);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
