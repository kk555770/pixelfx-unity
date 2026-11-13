// Shared by the fire effects (the leading _ keeps the tools from listing it as an effect).
// Flame tongues, licks, flame puffs that cool into smoke, embers and sparks, with their motion in closed form so
// looping effects can draw them from t alone and stay seamless. The tongue, lick, puff, smoke, ember and spark
// recipes are adapted from Sprite Fusion's open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class FireLib
    {
        /// <summary>The effect's noise at a time: NL(tt, rate, seed, off).</summary>
        public delegate double LoopNoise(double tt, double rate, double seed = 0, double off = 0);

        /// <summary>The effect's noise at this frame: N(rate, seed, off).</summary>
        public delegate double FrameNoise(double rate, double seed = 0, double off = 0);

        /// <summary>A looped particle's numbers (every effect uses the fields it needs).</summary>
        public sealed class Q
        {
            public double life, x, y, vx, vy, s, a, b, f, ph, w, m, e, sp, fl, sl, ox, oy, fire, d, v, l, born, R, up, dx, a0, v0, rise, @out, seed;
            public int i, n;
            public bool smokes, big, sm;
        }

        /// <summary>Smooth value noise in [-1, 1] that repeats every `period` cells (a whole number).</summary>
        public static double PNoise(double x, double period, double seed = 0)
        {
            double i = Math.Floor(x), f = x - i, u = f * f * (3 - 2 * f);
            double a = Rand(Wrap(i, period), seed), b = Rand(Wrap(i + 1, period), seed);
            return (a + (b - a) * u) * 2 - 1;
        }

        /// <summary>
        /// Noise for a loop D seconds long: N(tt, rate, seed, off) wobbles about `rate` times a second and is the same at
        /// tt and tt + D. Pass D = 0 for one-shots (plain noise, no repeat).
        /// </summary>
        public static LoopNoise NoiseFor(double D)
        {
            if (D == 0 || double.IsNaN(D)) return (tt, rate, seed, off) => Noise(tt * rate + off, seed);
            return (tt, rate, seed, off) =>
            {
                double P = Math.Max(1, Round(rate * D));
                return PNoise((tt / D) * P + off, P, seed);
            };
        }

        /// <summary>
        /// Seamless particles for a loop D seconds long: count particles, each born at its own phase and reborn every D.
        /// make(r, i) builds particle i from its own rng (the same every frame) and returns it with a .life (which may
        /// be longer than D); draw(q, age, i) is called for every live copy.
        /// </summary>
        public static void Looped(double t, double D, double count, double seed, Func<Rng, int, Q> make, Action<Q, double, int> draw)
        {
            for (int i = 0; i < count; i++)
            {
                Rng r = NewRng(seed, i);
                double born = r.Next() * D;
                Q q = make(r, i);
                for (double age = Wrap(t - born, D); age < q.life; age += D) draw(q, age, i);
            }
        }

        /// <summary>A fire ramp hot-first (H[0] white .. H[8] smouldering) from a palette dark to bright, any length.</summary>
        public static uint[] HotFirst(uint[] F)
        {
            var o = (uint[])F.Clone();
            Array.Reverse(o);
            return o;
        }

        /// <summary>Tongue layers [width share, height share, step of the hot-first ramp]: red rim, orange, yellow, white heart.</summary>
        public static readonly double[][] LAYERS = { new[] { 1, 1, 6.0 }, new[] { 0.76, 0.86, 4 }, new[] { 0.54, 0.68, 2 }, new[] { 0.3, 0.42, 0 } };

        /// <summary>Layers for big tongues: more orange and a smaller white heart, so the light doesn't wash them out.</summary>
        public static readonly double[][] BIG_LAYERS =
        {
            new[] { 1, 1, 6.0 }, new[] { 0.8, 0.9, 5 }, new[] { 0.62, 0.78, 4 }, new[] { 0.44, 0.6, 3 }, new[] { 0.26, 0.4, 1 }, new[] { 0.12, 0.22, 0 },
        };

        /// <summary>The same layers a step cooler, for flames further back: dark red rim, deep orange, gold, yellow.</summary>
        public static readonly double[][] BACK_LAYERS = { new[] { 1, 1, 7.0 }, new[] { 0.76, 0.86, 5 }, new[] { 0.54, 0.68, 3 }, new[] { 0.3, 0.42, 2 } };

        /// <summary>
        /// A flame tongue standing at (x, y), h tall, w its half-width at the base, its tip swept sx px sideways: a red
        /// rim round orange round yellow round a white heart, every row's width and centre jittered by noise so the
        /// edges crackle. N(rate, seed, off) is the effect's noise at this frame; n seeds this tongue. H: hot-first ramp.
        /// jit: how far the edges crackle (px); wob: how far the tip wanders (px).
        /// </summary>
        public static void FlameTongue(Painter p, double x, double y, double h, double w, double sx, FrameNoise N, double n, uint[] H, double[][] layers = null, double jit = 0.5, double wob = 1.1)
        {
            if (h < 1) return;
            foreach (double[] layer in layers ?? LAYERS)
            {
                double lw = layer[0], lh = layer[1];
                double L = h * lh;
                uint c = H[(int)layer[2]];
                for (double r = 0; r <= L; r++)
                {
                    double u = r / Math.Max(1, L);
                    double half = w * lw * Px.Pow(1 - u, 0.85) * Math.Min(1, (r + 1.5) / 3.5) + N(9, n, r * 0.45) * jit;
                    if (half < 0.3) continue;
                    double cx = x + sx * u * u * lh + N(4, n + 7, r * 0.2) * u * wob, Y = Round(y - r);
                    for (double px = Round(cx - half), x1 = Round(cx + half); px <= x1; px++) p.Px(px, Y, c);
                }
            }
        }

        /// <summary>
        /// The same tongue lying along any direction (ux, uy) from its root (x, y): for jets, breath and comet tails.
        /// len long, w half-width at the root, bend: px the tip is swept sideways (to the left of the direction).
        /// </summary>
        public static void FlameBlade(Painter p, double x, double y, double ux, double uy, double len, double w, double bend, FrameNoise N, double n, uint[] H, double[][] layers = null, double jit = 0.5, double wob = 1.1)
        {
            if (len < 1) return;
            double nx = -uy, ny = ux;
            foreach (double[] layer in layers ?? LAYERS)
            {
                double lw = layer[0], lh = layer[1];
                double L = len * lh;
                uint c = H[(int)layer[2]];
                for (double r = 0; r <= L; r += 0.5)
                {
                    double u = r / Math.Max(1, L);
                    double half = w * lw * Px.Pow(1 - u, 0.85) * Math.Min(1, (r + 1.5) / 3.5) + N(9, n, r * 0.45) * jit;
                    if (half < 0.3) continue;
                    double off = bend * u * u * lh + N(4, n + 7, r * 0.2) * u * wob, cx = x + ux * r + nx * off, cy = y + uy * r + ny * off;
                    for (double q = -half; q <= half; q += 0.5) p.Px(cx + nx * q, cy + ny * q, c);
                }
            }
        }

        /// <summary>A solid disc drawn straight in screen space (fast; a disc looks the same mirrored or turned).</summary>
        public static void Solid(Painter p, double x, double y, double r, uint c)
        {
            if (r < 0.6)
            {
                p.Px(x, y, c);
                return;
            }
            double cx = p.ToX(x, y), cy = p.ToY(x, y), r2 = r * r;
            for (double sy = Math.Floor(cy - r); sy <= Math.Ceiling(cy + r); sy++)
            {
                double dy = sy + 0.5 - cy, s2 = r2 - dy * dy;
                if (s2 < 0) continue;
                double s = Math.Sqrt(s2);
                for (double sx = Math.Ceiling(cx - s - 0.5); sx <= Math.Floor(cx + s - 0.5); sx++) p.Put((int)sx, (int)sy, c, 1);
            }
        }

        /// <summary>A translucent disc in screen space: smoke and haze.</summary>
        public static void Soft(Painter p, double x, double y, double r, uint c, double a)
        {
            if (a <= 0.004) return;
            if (r < 0.5)
            {
                p.Blend(x, y, c, a);
                return;
            }
            double cx = p.ToX(x, y), cy = p.ToY(x, y), r2 = r * r;
            for (double sy = Math.Floor(cy - r); sy <= Math.Ceiling(cy + r); sy++)
            {
                double dy = sy + 0.5 - cy, s2 = r2 - dy * dy;
                if (s2 < 0) continue;
                double s = Math.Sqrt(s2);
                for (double sx = Math.Ceiling(cx - s - 0.5); sx <= Math.Floor(cx + s - 0.5); sx++) p.BlendPx((int)sx, (int)sy, c, a);
            }
        }

        /// <summary>
        /// A ball of flame with life left k (1 new, 0 gone) cooling down the hot-first ramp H as it ages: a darker rim,
        /// a lit core up and to the left, a little tongue on top, a hot heart while it's young, and its light.
        /// frac (0..1) nudges how hot it starts. (tx, ty): which way its tongue points (up, unless it's streaming).
        /// </summary>
        public static void FirePuff(Painter p, double x, double y, double k, double size, double frac, uint[] H, double glowK = 0.18, double tx = 0, double ty = -1)
        {
            int n = H.Length;
            double age = 1 - k, heat = Math.Min(0.999, Px.Pow(age, 0.62) + frac * 0.1);
            int idx = (int)Math.Min(n - 2, Math.Floor(heat * (n - 1)));
            double r = size * (0.55 + 0.45 * Math.Min(1, age * 7)) * (1 - age * 0.55);
            uint c = H[idx];
            if (idx >= n - 2)
            {
                Soft(p, x, y, r, c, Math.Min(1, k * 3) * 0.85);
                return;
            }
            Solid(p, x, y, r, H[Math.Min(n - 1, idx + 2)]);
            if (r > 1.6 && ty > -0.99) Solid(p, x + tx * r * 1.5, y + ty * r * 1.5, r * 0.3, H[Math.Min(n - 1, idx + 1)]);
            Solid(p, x - r * 0.16, y - r * 0.22, r * 0.78, c);
            if (r > 1.6) Solid(p, x + tx * r * 0.9, y + ty * r * 0.9, r * 0.42, c);
            if (idx <= 3 && r > 1.8) Solid(p, x - r * 0.28, y - r * 0.34, r * 0.36, H[Math.Max(0, idx - 1)]);
            if (idx < 6) p.Glow(x, y, r * 2.4 + 2, c, glowK * k);
        }

        /// <summary>A lick: a blob of flame torn off a tongue's tip, a red rim round a yellow heart, shrinking as it rises.</summary>
        public static void Lick(Painter p, double x, double y, double k, double s, uint[] H)
        {
            double r = s * (0.4 + 0.6 * k);
            Solid(p, x, y, r + 0.6, k > 0.5 ? H[5] : H[6]);
            if (k > 0.35) Solid(p, x, y - 0.3, r * 0.6, k > 0.7 ? H[2] : H[4]);
        }

        static uint RampAt(uint[] r, double t) => r[(int)Math.Min(r.Length - 1, Math.Max(0, Math.Floor(t * r.Length)))];

        /// <summary>
        /// A puff of smoke with life left k: three soft discs (a body, a denser core sitting low, a lit cap while it's
        /// young), swelling and thinning. col tints it (0 for plain smoke); big for slower, fuller smoke.
        /// </summary>
        public static void SmokePuff(Painter p, double x, double y, double k, double size, uint col = 0, bool big = false, double aMul = 1)
        {
            double age = 1 - k;
            double r = size * (0.65 + Math.Min(1, age * 2.5) * 0.45 + age * (big ? 0.5 : 0.4));
            double a = Math.Min(1, age * 12) * Math.Min(1, k * 2.4) * (big ? 0.7 : 0.58) * aMul;
            uint c = col != 0 ? Mix(col, RampAt(Sim.SMOKE_RAMP, age), age) : RampAt(Sim.SMOKE_RAMP, age * 1.05);
            Soft(p, x, y, r, c, a * 0.45);
            Soft(p, x + r * 0.05, y + r * 0.06, r * 0.74, c, a * 0.62);
            if (age < 0.5) Soft(p, x - r * 0.22, y - r * 0.3, r * 0.5, Mix(c, WHITE, 0.12), a * 0.4 * (1 - age * 2));
        }

        /// <summary>
        /// An ember with life left k: a hot pixel cooling through cols [hot, warm, cool, rim], flickering out near the
        /// end (ph: its flicker phase), with its own light. Big ones (size 2) are a little cross with a darker rim.
        /// </summary>
        public static void Ember(Painter p, double x, double y, double k, double size, uint[] cols, double ph)
        {
            if (k < 0.25 && Px.Sin(ph) < 0) return;
            uint c = k > 0.6 ? cols[0] : k > 0.3 ? cols[1] : cols[2];
            if (size >= 2)
            {
                uint rim = k > 0.45 ? cols[2] : cols[3];
                p.Px(x - 1, y, rim);
                p.Px(x + 1, y, rim);
                p.Px(x, y - 1, rim);
                p.Px(x, y + 1, rim);
                p.Px(x, y, c);
                p.Glow(x, y, 8, c, 0.4 * k);
                return;
            }
            p.Px(x, y, c);
            if (size > 1.4) p.Px(x, y + 1, cols[3]);
            p.Glow(x, y, 3 + size, c, 0.35 * k);
        }

        /// <summary>A spark: a streak stretched along its velocity, a hot head and a cooler tail, and a point of light.</summary>
        public static void Spark(Painter p, double x, double y, double vx, double vy, double k, uint[] cols)
        {
            double sp = Hypot(vx, vy);
            if (sp == 0 || double.IsNaN(sp)) sp = 1;
            double ux = vx / sp, uy = vy / sp, len = Math.Min(9, 1 + sp * 0.022);
            uint head = k > 0.6 ? cols[0] : k > 0.3 ? cols[1] : cols[2];
            p.Line(x - ux * len, y - uy * len, x - ux * len * 0.4, y - uy * len * 0.4, k > 0.5 ? head : cols[cols.Length - 1]);
            p.Line(x - ux * len * 0.45, y - uy * len * 0.45, x, y, head);
            p.Glow(x, y, 3, head, 0.45 * k);
        }

        static (double x, double v) Along(double x, double v, double a, double k, double t)
        {
            if (k <= 0) return (x + v * t + 0.5 * a * t * t, v + a * t);
            double e = Exp(-k * t), vt = a / k;
            return (x + vt * t + ((v - vt) * (1 - e)) / k, vt + (v - vt) * e);
        }

        /// <summary>
        /// Where a body is t seconds after launch from (x, y) at (vx, vy), under acceleration (ax, ay) px/s² and linear
        /// drag kx, ky (1/s): [x, y, vx, vy].
        /// </summary>
        public static (double x, double y, double vx, double vy) Drift(double x, double y, double vx, double vy, double t, double ax, double ay, double kx, double ky)
        {
            var X = Along(x, vx, ax, kx, t);
            var Y = Along(y, vy, ay, ky, t);
            return (X.x, Y.x, X.v, Y.v);
        }

        /// <summary>A spark's flight: falls, drags, and bounces off the floor at y = floor, losing speed each time.</summary>
        public static (double x, double y, double vx, double vy) SparkAt(double x, double y, double vx, double vy, double t, double floor = 0, double g = 427, double k = 2.2)
        {
            var s = (x: x, y: y, vx: vx, vy: vy);
            double left = t;
            for (int b = 0; b < 3; b++)
            {
                var e = Drift(s.x, s.y, s.vx, s.vy, left, 0, g, k, k);
                if (e.y < floor || s.y >= floor) return e;
                double lo = 0, hi = left;
                for (int i = 0; i < 16; i++)
                {
                    double m = (lo + hi) / 2;
                    if (Drift(s.x, s.y, s.vx, s.vy, m, 0, g, k, k).y < floor) lo = m;
                    else hi = m;
                }
                var c = Drift(s.x, s.y, s.vx, s.vy, hi, 0, g, k, k);
                s = (c.x, floor - 0.25, c.vx * 0.7, -Math.Abs(c.vy) * 0.45);
                left -= hi;
            }
            var end = Drift(s.x, s.y, s.vx, s.vy, left, 0, g, k, k);
            return end.y < floor ? end : (end.x, floor - 0.25, end.vx, 0);
        }

        /// <summary>A pool of light on the floor: an ellipse of glow, k at its middle falling to 0 at its rim.</summary>
        public static void FloorLight(Painter p, double x, double y, double rx, double ry, uint c, double k)
        {
            if (k <= 0.004 || rx < 1 || ry < 0.5) return;
            p.Shade(x - rx, y - ry, x + rx, y + ry, (px, py, o) =>
            {
                double dx = (px - x) / rx, dy = (py - y) / ry, d = dx * dx + dy * dy;
                if (d >= 1) return 0;
                double f = 1 - Math.Sqrt(d);
                o.l = c;
                o.k = f * f * k;
                return 0;
            });
        }

        /// <summary>
        /// A core Particles system drawn with its flame puffs recoloured down the hot-first ramp H (the core draws them
        /// in its own fire colours): smoke and back particles, then the flames, then sparks and the rest on top.
        /// </summary>
        public static void SimDraw(Painter p, Particles ps, uint[] H, Layer layer, Emitter e)
        {
            var all = new List<Particles.Particle>(ps.List);
            var rest = new List<Particles.Particle>();
            var fire = new List<Particles.Particle>();
            foreach (Particles.Particle q in all) (q.k == P.Fire ? fire : rest).Add(q);
            void Puffs(bool back)
            {
                foreach (Particles.Particle q in fire)
                    if (q.delay <= 0 && ((q.flag & PF.Back) != 0) == back)
                    {
                        Sim.Shift(p, e, q.bornX, q.bornY);
                        FirePuff(p, q.x, q.y, q.life / q.max, q.size, q.seed % 1, H);
                    }
                p.Shift(0, 0);
            }
            ps.List.Clear();
            ps.List.AddRange(rest);
            if (layer != Layer.Front)
            {
                ps.Draw(p, Layer.Back);
                Puffs(true);
            }
            if (layer != Layer.Back)
            {
                Puffs(false);
                ps.Draw(p, Layer.Front);
            }
            ps.List.Clear();
            ps.List.AddRange(all);
        }

        static readonly uint ROCK_OUTLINE = Hex("#1a1422");

        /// <summary>A stone color and its lit, shaded and dust tones (the stone() the debris effects share).</summary>
        public readonly struct Stone
        {
            public readonly uint light, body, shade, dust;

            public Stone(uint c)
            {
                light = Mix(Scale(c, 1.35), WHITE, 0.08);
                body = c;
                shade = Scale(c, 0.64);
                dust = Mix(Scale(c, 1.2), Rgb(150, 140, 150), 0.3);
            }
        }

        /// <summary>A tumbling rock: an outlined lump lit on one side, fading out at the end of its life.</summary>
        public static void Rock(Painter p, Emitter e, Bits.Bit b, Stone st)
        {
            double r = 0.6 + b.s * 0.75, fade = Math.Min(1, b.life / 0.5), lx = Px.Cos(b.a) * r * 0.4, ly = Px.Sin(b.a) * r * 0.4;
            Sim.Shift(p, e, b.bornX, b.bornY);
            p.Shade(b.x - r - 1, b.y - r - 1, b.x + r + 1, b.y + r + 1, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, d = Hypot(dx * 1.15, dy);
                if (d > r + 0.9) return 0;
                double lit = dx * lx + dy * ly;
                o.c = d > r ? ROCK_OUTLINE : lit < -0.4 ? st.light : lit > 0.6 ? st.shade : st.body;
                o.a = fade;
                return 0;
            });
        }

        static readonly uint SMOKE1 = Hex("#5b5566");

        /// <summary>burn's options (the JS o of burn): what's unset falls back to the JS defaults.</summary>
        public sealed class BurnOpts
        {
            public uint[] H;
            public double D;
            public LoopNoise NL;
            public double S, height, x, y, spread = 6, glow = 1, lean = 0.1, smoke = 12, smokeSize = 3, wind = 3, puffs = 8, licks = 4, embers = 8, glowR = 46;
            public double[] smokeLife = { 1.6, 2.6 };
            public Action<double> mid;
        }

        sealed class Tongue
        {
            public double dx, y, h, w, env, n;
            public bool back;
        }

        /// <summary>
        /// A fire for loops, drawn from t alone. Tongues T: [[dx, dy, height share, half-width, back row?], ...] from the
        /// base (o.x, o.y), back row first then outer to inner; licks torn off their tips, flame puffs that burn out into
        /// smoke, a smoke plume curling off with the draught, embers, and the fire's light. Wide tongues get BIG_LAYERS.
        /// </summary>
        public static void Burn(Painter p, double t, double[][] T, BurnOpts o)
        {
            uint[] H = o.H;
            double D = o.D, S = o.S, HC = o.height, X = o.x, Y = o.y, W = o.spread, G = o.glow;
            LoopNoise NL = o.NL;
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            double Fla(double tt) => 0.74 + 0.16 * NL(tt, 7.3, S + 1) + 0.1 * NL(tt, 19, S + 2);
            double fl = Fla(t);
            uint SMOKE = Mix(SMOKE1, H[6], 0.25), GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25);
            uint[] EMB = { H[1], H[2], H[4], H[6] };
            Rng R = NewRng(S, 77);
            double Count(double rate) => Round(rate * D);
            var TT = new Tongue[T.Length];
            for (int i = 0; i < T.Length; i++)
            {
                double[] q = T[i];
                var tg = new Tongue();
                tg.dx = q[0] + R.Range(-0.3, 0.3);
                tg.y = q[1];
                tg.h = 2 + HC * q[2] * R.Range(0.92, 1.08);
                tg.w = q[3] * R.Range(0.92, 1.08);
                tg.back = q[4] != 0;
                tg.env = q[2];
                tg.n = (S * 7 + i) * 13;
                TT[i] = tg;
            }
            double Lean(double tt) => NL(tt, 0.67, S + 90) * o.lean;
            double Height(int i, double tt)
            {
                double h = TT[i].h, n = TT[i].n, leap = Math.Max(0, NL(tt, 2.6, n + 70) - 0.55) * 1.4;
                return (h + 0.25 * h * NL(tt, 3.3, n) + h * leap) * (0.9 + 0.1 * Fla(tt));
            }
            double Sweep(int i, double tt, double h) => NL(tt, 2.2, TT[i].n + 40) * (1 + h * 0.08) + Lean(tt) * h * 0.9 - Clamp(TT[i].dx * 0.3, -2.5, 2.5);
            (double x, double y, double h) Tip(int i, double tt)
            {
                double h = Height(i, tt);
                return (X + TT[i].dx + Sweep(i, tt, h) * 0.85, Y + TT[i].y - h * 0.88 + 3, h);
            }
            void TongueAt(int i)
            {
                Tongue g = TT[i];
                double h = Height(i, t);
                FlameTongue(p, X + g.dx, Y + g.y, h, g.w, Sweep(i, t, h), N, g.n, H, g.back ? BACK_LAYERS : g.w > 2.2 ? BIG_LAYERS : LAYERS, 0.5, 1.1 + h * 0.04);
            }

            // the plume: each puff keeps the sway the plume had when it left, so the wiggle climbs it
            Looped(t, D, Count(o.smoke), S + 300, (r, j) => new Q
            {
                x = r.Range(-W, W) * 0.7,
                y = -HC * 0.8 - r.Range(0, 6),
                vx = r.Range(-2, 2),
                vy = -r.Range(10, 17),
                life = r.Range(o.smokeLife[0], o.smokeLife[1]),
                s = r.Range(0.7, 1.1) * o.smokeSize,
            }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, o.wind, -20, 1.6, 1);
                SmokePuff(p, X + d.x + NL(t - age, 0.8, S + 60) * (1.5 + 2.5 * age), Y + d.y, 1 - age / q.life, q.s, SMOKE);
            });
            // flame puffs shed from the tips
            var puffs = new List<(double x, double y, double k, double s, double f)>();
            Looped(t, D, TT.Length * Count(o.puffs), S + 200, (r, j) =>
            {
                int i = j % TT.Length;
                double fire = r.Range(0.14, 0.3);
                bool smokes = r.Chance(0.45);
                return new Q
                {
                    i = i,
                    fire = fire,
                    smokes = smokes,
                    sl = r.Range(0.8, 1.4),
                    ox = r.Range(-1, 1),
                    oy = r.Range(0, 4),
                    vx = r.Range(-4, 4),
                    vy = -r.Range(15, 30),
                    s = r.Range(0.8, 1.6) * (0.8 + 0.6 * TT[i].env),
                    f = r.Next(),
                    life = fire + (smokes ? 1.4 : 0),
                };
            }, (q, age, j) =>
            {
                var tp = Tip(q.i, t - age);
                var d = Drift(tp.x + q.ox, tp.y + q.oy, q.vx, q.vy, Math.Min(age, q.fire), 0, -95, 5, 3.2);
                if (age < q.fire)
                {
                    puffs.Add((d.x, d.y, 1 - age / q.fire, q.s, q.f));
                    return;
                }
                if (age - q.fire > q.sl) return;
                var sd = Drift(d.x, d.y, d.vx * 0.6, d.vy * 0.6 - 6, age - q.fire, o.wind, -16, 1.6, 1.2);
                SmokePuff(p, sd.x, sd.y, 1 - (age - q.fire) / q.sl, q.s * 0.7, 0);
            });
            for (int i = 0; i < TT.Length; i++)
                if (TT[i].back) TongueAt(i);
            for (int i = 0; i < TT.Length; i++)
                if (!TT[i].back) TongueAt(i);
            o.mid?.Invoke(fl);
            // licks torn off the tips, rising and shrinking
            Looped(t, D, TT.Length * Count(o.licks), S + 220, (r, j) => new Q
            {
                i = j % TT.Length,
                vx = r.Range(-3, 3),
                vy = -r.Range(12, 22),
                s = r.Range(0.9, 1.6) * (0.8 + 0.5 * TT[j % TT.Length].env),
                life = 0.26,
            }, (q, age, j) =>
            {
                var tp = Tip(q.i, t - age);
                Lick(p, tp.x + q.vx * age, tp.y - 2 + q.vy * age, 1 - age / q.life, q.s, H);
            });
            foreach (var (x, y, k, s, f) in puffs) FirePuff(p, x, y, k, s, f, H);
            // embers carried up by the heat, wandering and cooling
            Looped(t, D, Count(o.embers), S + 500, (r, j) => new Q
            {
                x = r.Range(-W, W),
                y = -r.Range(HC * 0.3, HC * 0.8),
                vx = r.Range(-15, 15),
                vy = -r.Range(25, 60),
                life = r.Range(1.1, 2.4),
                s = r.Chance(0.15) ? 2 : r.Range(1, 1.6),
                a = r.Range(0, 6.3),
                b = r.Range(0, 6.3),
            }, (q, age, j) =>
            {
                var d = Drift(q.x, q.y, q.vx, q.vy, age, 0, -30, 1.6, 1.6);
                Ember(p, X + d.x + 7 * (Px.Sin(age * 2.3 + q.a) - Px.Sin(q.a)), Y + d.y + 2 * (Px.Sin(age * 3 + q.b) - Px.Sin(q.b)), 1 - age / q.life, q.s, EMB, age * 40 + q.a);
            });
            // light: a broad warm wash, lights on the flames, a hot heart at the base
            if (G > 0)
            {
                double R1 = o.glowR, lx = X + Lean(t) * 12;
                p.Glow(lx, Y - HC * 0.35, R1 * 1.5, GLOW, 0.22 * fl * G);
                p.Glow(lx, Y - HC * 0.75, R1, GLOW, 0.34 * fl * G);
                p.Glow(X, Y - HC * 0.2, R1 * 0.32, H[2], 0.2 * fl * G);
                p.Glow(X, Y - 2, 8, H[0], 0.14 * fl * G);
            }
        }
    }
}
