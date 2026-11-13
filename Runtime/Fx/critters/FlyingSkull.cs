// The fire (nested flame tongues with leaps and flicker, licks, flame puffs cooling to smoke, wandering embers,
// layered warm light) is adapted from Sprite Fusion's open-source fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.CrittersLib;

namespace Pixelfx
{
    public sealed class FlyingSkull : Effect
    {
        public static readonly Module Module = new Module("flying-skull", "pixelfx/fx/critters/flying-skull", 96, 132, 48, 120, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["bone"] = new[] { "#1a1114", "#3e2b2a", "#6b5144", "#9e8467", "#cdb892", "#efe2bf", "#fffaea" },
                ["hover"] = 40,
                ["floor"] = true,
                ["seed"] = 1,
            },
            (o, e) => new FlyingSkull(o))
        {
            Title = "Flying skull",
            Desc = "A cracked bone skull bobs in the air, jaw chattering, eyes burning, a fire roaring off its crown that sheds flame puffs, embers and smoke and throws warm light.",
            Tags = new[] { "critter", "skull", "undead", "fire", "flying", "monster", "loop" },
            Bg = "ground",
        };

        const double D = 2.4;
        static readonly uint SOCKET = Hex("#120a0c"), SMOKE = Hex("#5b5566"), FLOOR_SHADOW = Hex("#0d0a12");

        // where the skull is at loop time t: a slow figure-eight bob, and its velocity (for the flames to trail)
        static (double x, double y, double vx, double vy) At(double t, double hover)
        {
            double a = (t / D) * TAU;
            return (3 * Px.Sin(a), -hover + 3 * Px.Sin(2 * a + 0.5), (3 * TAU * Px.Cos(a)) / D, (6 * TAU * Px.Cos(2 * a + 0.5)) / D);
        }

        // the jaw: two bursts of chatter a loop, a wide gape at the end of the second
        static double Jaw(double t)
        {
            double ch = Math.Max(0, Px.Sin((t / D) * TAU * 22)), b1 = Burst(t, 0.15, 0.75), b2 = Burst(t, 1.3, 1.95);
            double gape = Burst(t, 1.95, 2.3);
            return Round(Math.Max(ch * 2.4 * Math.Max(b1, b2), gape * 3.4));
        }

        static double Burst(double t, double a, double b) => t <= a || t >= b ? 0 : Px.Pow(Px.Sin(Math.PI * ((t - a) / (b - a))), 0.5);
        static double Flick(double t) => 0.74 + 0.16 * Lnoise(t, D, 18, 0, 1) + 0.1 * Lnoise(t, D, 46, 0, 2);

        static double Sq(double x) => x * x;

        // the skull's shape, centered on (0, 0): a cranium, cheekbones narrowing to the upper jaw
        static bool InCranium(double x, double y) => Sq(x / 11) + Sq((y + 6) / 10.5) <= 1;
        static bool InFace(double x, double y) => y >= -3 && y <= 6.5 && Math.Abs(x) <= Lerp(10.2, 7.2, (y + 3) / 9.5);
        static bool InSkull(double x, double y) => InCranium(x, y) || InFace(x, y);
        static double BrowY(double x) => -6.6 + (8.5 - Math.Abs(x)) * 0.3;
        static bool InSocket(double x, double y) => Sq((Math.Abs(x) - 5.1) / 3.3) + Sq((y + 2.9) / 3.3) <= 1 && y > BrowY(x);
        static readonly int[][] NOSE = { new[] { -1, 1 }, new[] { 0, 1 }, new[] { -2, 2 }, new[] { -1, 2 }, new[] { 0, 2 }, new[] { 1, 2 }, new[] { -2, 3 }, new[] { 1, 3 } };
        static readonly int[][] CRACK = { new[] { 4, -16 }, new[] { 4, -15 }, new[] { 5, -14 }, new[] { 5, -13 }, new[] { 4, -12 }, new[] { 5, -11 }, new[] { 6, -10 }, new[] { 6, -9 } };
        static double TopY(double x) => -6 - 10.5 * Math.Sqrt(Math.Max(0, 1 - Sq(x / 11)));

        static void DrawSkull(Grid g, uint[] B, uint[] F, double open, double t)
        {
            uint warm = Mix(B[6], F[6], 0.45), rim = Mix(B[5], F[5], 0.3);
            const double lx = -4, ly = -15;
            bool Lit(double x, double y, double k) => InSkull(lx + (x - lx) / k, ly + (y - ly) / k);
            g.Clear();
            g.Shade(-12, -17, 12, 8, (x, y, o) =>
            {
                if (!InSkull(x, y)) return 0;
                uint c = Lit(x, y, 0.45) ? B[5] : Lit(x, y, 0.74) ? B[4] : B[3];
                // temples pinch in behind the sockets
                if (Math.Abs(x) > 8.2 && y > -7 && y < -1) c = x < 0 ? B[3] : B[2];
                if (!InSkull(x + 2, y + 2.2) || (x > 0 && !InSkull(x + 2.6, y))) c = B[2];
                if (!InSkull(x + 1, y + 1.2) && y > 2) c = B[1];
                // the crown catches the fire
                if (!InSkull(x, y - 1.5)) c = Math.Abs(x) < 7 ? warm : rim;
                else if (!InSkull(x, y - 2.5) && Math.Abs(x) < 6) c = rim;
                // under the brow, the bone drops into shadow
                if (y > BrowY(x) - 1.2 && y < BrowY(x) + 0.4 && Math.Abs(x) > 1.6 && Math.Abs(x) < 8.6) c = x < 0 ? B[4] : B[3];
                // cheekbones
                if (Math.Abs(x) > 6.2 && Math.Abs(x) < 9.2 && y > 0 && y < 2) c = x < 0 ? B[5] : B[3];
                // the upper teeth
                if (y > 6)
                {
                    if (Math.Abs(x) > 6.4) return 0;
                    double cx = Math.Floor(x);
                    bool gap = cx == -6 || cx == -3 || cx == 2 || cx == 5;
                    c = gap ? B[1] : y < 7 ? (x < 0 ? B[5] : B[4]) : cx == -1 || cx == 0 ? B[3] : B[4];
                }
                return c;
            });
            // sockets: dark hollows with an ember glow catching their lower rim
            g.Shade(-9, -7, 9, 1, (x, y, o) =>
            {
                if (!InSocket(x, y)) return 0;
                if (!InSocket(x, y + 1.3)) return Mix(B[2], F[4], 0.55);
                return SOCKET;
            });
            foreach (int[] n in NOSE) g.Set(n[0], n[1], n[1] == 3 ? B[1] : SOCKET);
            g.Set(-1, 3, B[2]);
            g.Set(0, 3, B[2]);
            foreach (int[] c in CRACK)
            {
                g.Set(c[0], c[1], B[1]);
                g.Set(c[0] - 1, c[1], B[5]);
            }
            g.Set(7, -7, B[1]);
            g.Set(8, -6, B[1]);
            // the jaw hangs below: rami up the sides, a row of teeth, the chin
            double jy = 8 + open;
            g.Shade(-8, 4, 8, jy + 6, (x, y, o) =>
            {
                double ax = Math.Abs(x);
                if (ax > 6.6 && ax < 8.1 && y > 4 && y < jy + 2) return x < 0 ? B[3] : B[2];
                if (y < jy || y > jy + 5.5) return 0;
                double v = y - jy, hw = 7.4 - v * 0.45 - (v > 4 ? (v - 4) * 1.2 : 0);
                if (ax > hw) return 0;
                if (v < 1.5)
                {
                    if (ax > 6) return x < 0 ? B[4] : B[3];
                    double cx = Math.Floor(x);
                    bool gap = cx == -5 || cx == -2 || cx == 1 || cx == 4;
                    return gap ? B[1] : v < 1 ? B[5] : B[4];
                }
                return !InJaw(x + 1.6, y + 1.6, jy) ? B[1] : v < 2.5 ? (x < 0 ? B[4] : B[3]) : B[3];
            });
            if (open > 0)
                for (double y = 8; y < jy; y++)
                for (double x = -6; x <= 5; x++)
                    g.Set(x, y, y == jy - 1 ? Mix(SOCKET, F[3], 0.5) : SOCKET);
            g.Outline((x, y, nx, ny) => ny < 0 ? Mix(B[0], F[1], 0.75) : B[0]);
        }

        static bool InJaw(double x, double y, double jy)
        {
            double v = y - jy, hw = 7.4 - v * 0.45 - (v > 4 ? (v - 4) * 1.2 : 0);
            return v >= 0 && v <= 5.5 && Math.Abs(x) <= hw;
        }

        sealed class Flame
        {
            public double x, h, w, n;
            public double? y;
            public bool front;
        }

        // the tongues of fire on the crown: one every 2 px, tallest in the middle, leaping now and then
        static List<Flame> Crown(double seed)
        {
            var output = new List<Flame>();
            for (double x = -10; x <= 10; x += 2) output.Add(new Flame { x = x, h = 13 + 13 * (1 - Sq(x / 11)), w = 2.3 + Rand(x + 20, seed, 3) * 0.8, n = 37 + x * 3 + seed * 11, front = false });
            // and up the temples
            foreach (int s in new[] { -1, 1 }) output.Add(new Flame { x = s * 11, y = -5, h = 10, w = 1.8, n = 61 + s * 13 + seed * 5, front = false });
            for (double x = -5; x <= 5; x += 2.5) output.Add(new Flame { x = x, h = 5 + 3 * Rand(x * 4 + 20, seed, 5), w = 1.6, n = 91 + x * 5 + seed * 7, front = true });
            return output;
        }

        static double Height(Flame f, double t, double k)
        {
            double leap = Math.Max(0, Lnoise(t, D, 6, f.n * 3.1, 70) - 0.55) * 1.6;
            return (f.h + 0.25 * f.h * Lnoise(t, D, 8, f.n * 1.7, 5) + f.h * leap) * (0.9 + 0.1 * k);
        }

        readonly Options o;
        readonly Grid GRID = new Grid(-14, -20, 28, 40);
        readonly Swarm SWARM = new Swarm();

        FlyingSkull(Options o) => this.o = o;

        public override void Draw(Painter p, double time)
        {
            double hover = o.Num("hover");
            double t = Shift(time, D, o.Seed);
            uint[] F = Fit(o.Ramp("palette"), 9), heat = F.Reverse().ToArray(), B = Fit(o.Ramp("bone"), 7);
            var S = At(t, hover);
            double k = Flick(t), lean = Clamp(-S.vx / 60, -0.8, 0.8), stretch = Clamp(S.vy / 90, -0.3, 0.45);
            Flame[] tongues = Crown(o.Seed).OrderByDescending(f => Math.Abs(f.x)).ToArray();
            double X = Round(S.x), Y = Round(S.y);
            double Lick(Flame f) => Lnoise(t, D, 5, f.n, 40) * 2 + lean * Height(f, t, k) * 0.9 - Clamp(f.x * 0.25, -2, 2);
            // the fire behind: tall tongues off the back of the crown
            for (int li = 0; li < 4; li++)
                foreach (Flame f in tongues)
                    if (!f.front)
                        Tongue(p, X + f.x, Y + (f.y ?? TopY(f.x) + 3), Height(f, t, k) * (1 + stretch), f.w * (1 - 0.25 * Math.Abs(lean)), Lick(f), t, D, f.n, heat, li);
            // embers and flame puffs behind it
            DrawParticles(p, t, hover, heat, F, true);
            Grid g = GRID;
            DrawSkull(g, B, F, Jaw(t), t);
            g.Draw(p, 1, X, Y);
            // the burning eyes
            foreach (int s in new[] { -1, 1 })
            {
                double ex = X + s * 5.1 - 0.5, ey = Y - 2.4, fl = 0.75 + 0.25 * Lnoise(t, D, 30, s * 9, 12);
                p.Px(ex, ey, heat[0]);
                p.Px(ex + 1, ey, heat[1]);
                p.Px(ex, ey + 1, heat[2]);
                p.Px(ex + 1, ey + 1, heat[3]);
                if (fl > 0.8)
                {
                    p.Px(ex, ey - 1, heat[3]);
                    p.Px(ex + 1, ey - 1, heat[5]);
                }
                p.Glow(ex + 1, ey + 1, 6, F[5], 0.45 * fl);
                p.Glow(ex + 1, ey + 0.5, 3, F[7], 0.5 * fl);
            }
            // fire in the mouth when it gapes, and a gout of flame breathed out
            double open = Jaw(t);
            if (open > 0) p.Glow(X, Y + 8 + open * 0.5, 4 + open * 2, F[5], 0.25 * open);
            for (int i = 0; i < 9; i++)
            {
                Rng r = NewRng(o.Seed, i, 21);
                double born = 1.98 + i * 0.03, age = Wrap(t - born, D), L = r.Range(0.25, 0.45);
                if (age >= L) continue;
                var S0 = At(born, hover);
                var (x, y) = Fly(S0.x + r.Range(-3, 3), S0.y + 11, r.Range(-22, 22), r.Range(40, 75), age, -70, 3.5);
                FirePuff(p, x, y, 1 - age / L, r.Range(1.1, 2), r.Next() * 100, heat);
            }
            // the front flames, over the crown
            for (int li = 0; li < 4; li++)
                foreach (Flame f in tongues)
                    if (f.front)
                        Tongue(p, X + f.x, Y + TopY(f.x) + 1.5, Height(f, t, k) * (1 + stretch), f.w, Lick(f) * 0.6, t, D, f.n, heat, li);
            DrawParticles(p, t, hover, heat, F, false);
            // light: a big warm pool, a hot core over the crown, the eyes
            double br = 0.92 + 0.08 * Px.Sin((t / D) * TAU * 2);
            p.Glow(X + lean * 4, Y - 20, 74 * br * (0.9 + 0.1 * k), Mix(F[4], F[5], 0.5), 0.48 * k);
            p.Glow(X, Y - 21, 24, F[6], 0.3 * k);
            p.Glow(X, Y - 15, 9, F[8], 0.16 * k);
        }

        // flame puffs off the tongue tips that cool into smoke, licks of flame breaking away, embers wandering up
        void DrawParticles(Painter p, double t, double hover, uint[] heat, uint[] F, bool back)
        {
            Swarm sw = SWARM.Begin(t);
            sw.Sparks = new[] { F[7], F[6], F[4], F[2] };
            if (back)
            {
                // smoke from the tips, born where the skull was
                Cycle(t, D, 15, o.Seed * 7 + 1, r => r.Range(1.7, 2.5), (age, L, r, i) =>
                {
                    var S0 = At(Wrap(t - age, D), hover);
                    double x0 = S0.x + r.Range(-6, 6), y0 = S0.y - 22 - r.Range(0, 8);
                    var (x, y) = Fly(x0, y0, r.Range(-5, 5), -r.Range(16, 26), age, -12, 1.2);
                    sw.Add(P.Smoke, x + Px.Sin(age * 1.3 + r.Next() * 6) * 3 * age, y, 0, 0, age, L, Mix(SMOKE, F[2], 0.3), r.Range(2.8, 4.4), PF.Big);
                });
                sw.Draw(p);
                return;
            }
            // flame puffs: bright, fast, cooling down the ramp as they rise
            Cycle(t, D, 40, o.Seed * 7 + 2, r => r.Range(0.18, 0.36), (age, L, r, i) =>
            {
                var S0 = At(Wrap(t - age, D), hover);
                bool side = r.Chance(0.3);
                double a = side ? r.Sign() * r.Range(0.9, 1.45) : r.Range(-0.8, 0.8);
                double x0 = S0.x + Px.Sin(a) * 10.5, y0 = S0.y - 6 - Px.Cos(a) * 10 - (side ? 0 : r.Range(2, 10));
                var (x, y) = Fly(x0, y0, r.Range(-6, 6) + Px.Sin(a) * 6, -r.Range(18, 34), age, -95, 3.2);
                FirePuff(p, x + Px.Sin(age * 17 + r.Next() * 6) * 0.8, y, 1 - age / L, r.Range(1, 2.2), r.Next() * 100, heat);
            });
            // licks: little blobs of flame breaking off the tips
            Cycle(t, D, 18, o.Seed * 7 + 3, 0.26, (age, L, r, i) =>
            {
                var S0 = At(Wrap(t - age, D), hover);
                double fx = r.Range(-10, 10), x0 = S0.x + fx, y0 = S0.y + TopY(fx) - r.Range(4, 16) * (1 - Math.Abs(fx) / 14);
                double kk = 1 - age / L, x = x0 + r.Range(-3, 3) * age, y = y0 - r.Range(12, 20) * age, rr = r.Range(0.9, 1.7) * (0.4 + 0.6 * kk);
                p.Circle(x, y, rr + 0.6, kk > 0.5 ? heat[5] : heat[6]);
                if (kk > 0.35) p.Circle(x, y - 0.3, rr * 0.6, kk > 0.7 ? heat[2] : heat[4]);
            });
            // embers wander up and cool
            Cycle(t, D, 24, o.Seed * 7 + 4, r => r.Range(1, 2.4), (age, L, r, i) =>
            {
                var S0 = At(Wrap(t - age, D), hover);
                double x0 = S0.x + r.Range(-11, 11), y0 = S0.y - r.Range(6, 24), s = r.Next() * 100;
                var (x, y) = Fly(x0, y0, r.Range(-22, 22), -r.Range(25, 70), age, -20, 1.6);
                sw.Add(P.Ember, x + Px.Sin(age * 2.3 + s) * 4 * Math.Min(1, age), y, 0, 0, age, L, F[4], r.Chance(0.3) ? 2 : 1, PF.None, s);
            });
            // sparks pop out of the fire now and then, fall, and skip along the floor
            if (o.Bool("floor"))
                Cycle(t, D, 5, o.Seed * 7 + 5, r => r.Range(0.9, 1.4), (age, L, r, i) =>
                {
                    var S0 = At(Wrap(t - age, D), hover);
                    Bounced b = Bounce(S0.x + r.Range(-6, 6), S0.y - r.Range(10, 18), r.Range(-28, 28), -r.Range(50, 110), age, 430, -0.5, 0.42);
                    sw.Add(P.Spark, b.x, b.y, b.rest ? 0.1 : b.vx, b.rest ? 0 : b.vy, age, L, 0, 1, PF.None, r.Next() * 100);
                });
            sw.Draw(p);
        }

        public override void DrawBack(Painter p, double time)
        {
            if (!o.Bool("floor")) return;
            double t = Shift(time, D, o.Seed);
            uint[] F = Fit(o.Ramp("palette"), 9);
            var S = At(t, o.Num("hover"));
            double k = Flick(t);
            // its shadow on the floor, and the floor lit warm around it
            Shadow(p, S.x, 0, 9, 1.6, FLOOR_SHADOW, 0.6);
            for (double x = -30; x <= 30; x += 6) p.Glow(S.x + x, -1, 20, Mix(F[4], F[5], 0.5), 0.15 * k * (1 - Math.Abs(x) / 40));
        }
    }
}
