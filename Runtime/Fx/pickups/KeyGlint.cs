// The hover, floor shadow and star glints follow Sprite Fusion's open-source pickup and sparkle
// (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class KeyGlint : Effect
    {
        const double DURATION = 2, HOVER = 19, MID = 8, TIP = 17.5;
        const double G0 = 0.18, G1 = 0.5; // the glint's run, as loop phase

        public static readonly Module Module = new Module("key-glint", "pixelfx/fx/pickups/key-glint", 72, 60, 36, 54, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0e1018", "#222838", "#3c4660", "#5e6d8e", "#8c9cbc", "#bccae2", "#e6eeff", "#ffffff" },
                ["seed"] = 7,
            },
            (o, e) => new KeyGlint(o, e))
        {
            Title = "Key glint",
            Desc = "An ornate silver key hovering and rocking; a glint runs from its bow down the shaft to the teeth and bursts off the tip as falling sparks.",
            Tags = new[] { "pickup", "key", "unlock", "item", "loop" },
            Bg = "ground",
        };

        readonly ScratchLayer L = new ScratchLayer(48, 40);
        readonly Options o;

        KeyGlint(Options o, Emitter e) => this.o = o;

        // where the key hangs: bobbing and rocking, bow up to the left, teeth down to the right
        static (double y, double a) Hang(double ph) => (-HOVER - 1.6 * Px.Sin(ph * TAU), 0.42 + 0.11 * Px.Sin(ph * TAU + 0.9));

        static (double x, double y) ToWorld(double u, double v, double cx, double cy, double a) =>
            (cx + (u - MID) * Px.Cos(a) - v * Px.Sin(a), cy + (u - MID) * Px.Sin(a) + v * Px.Cos(a));

        // the key in its own frame: u along the shaft (bow at 0, tip at 17.5), v across it
        static int Part(double u, double v)
        {
            double rb = Hypot(u, v);
            if (rb <= 4.6 && rb >= 2.1) return 1; // the bow's ring
            if (u >= 4.3 && u <= 6 && Math.Abs(v) <= 1.9) return 2; // the collar
            if (u >= 6 && u <= TIP && Math.Abs(v) <= 1) return 3; // the shaft
            if (u >= 12.6 && u <= TIP - 0.3 && v > 1 && v <= 4.6 && !(u > 14.3 && u < 15.4 && v > 2.7)) return 4; // the bit
            return 0;
        }

        void Key(Painter p, uint[] C, double cx, double cy, double a, double gu)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a);
            L.Begin(Math.Floor(cx) - 24, Math.Floor(cy) - 20);
            L.Fill(Math.Floor(cx) - 24, Math.Floor(cy) - 20, Math.Floor(cx) + 23, Math.Floor(cy) + 19, (px, py) =>
            {
                double dx = px - cx, dy = py - cy, u = dx * ca + dy * sa + MID, v = -dx * sa + dy * ca;
                int k = Part(u, v);
                if (k == 0) return 0;
                // light from the top left of the screen: work out which way this bit of metal faces
                double ny;
                if (k == 1)
                {
                    double rb = Hypot(u, v), outer = rb > 3.4 ? 1 : -1, wx = u / rb * outer, wy = v / rb * outer;
                    ny = -((wx * ca - wy * sa) * -0.6 + (wx * sa + wy * ca) * -0.8);
                }
                else ny = v < -0.35 ? 0.8 : v > 0.35 && k != 4 ? -0.6 : k == 4 ? (v > 3.6 ? -0.5 : 0.1) : 0.2;
                int c = ny > 0.55 ? 6 : ny > 0.15 ? 4 : ny > -0.3 ? 3 : 2;
                if (k == 2) c = Math.Min(7, c + 1);
                // the glint: a slanted band of white running down the key
                double g = Math.Abs(u + v * 0.45 - gu);
                if (g < 0.8) return WHITE;
                if (g < 2) c = Math.Min(7, c + 2);
                return C[c];
            });
            L.Outline(C[0]).Draw(p);
        }

        public override void DrawBack(Painter p, double t)
        {
            var (y, _) = Hang(t / DURATION);
            BlobShadow(p, 0, 10, 0.8 - (-y - HOVER + 1.6) * 0.06);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double T = DURATION, ph = t / T, seed = o.Seed;
            var (y, a) = Hang(ph);
            double q = (ph - G0) / (G1 - G0), gu = q > 0 && q < 1 ? -5 + (TIP + 7) * Ease.InOutSine(q) : -100;
            // cool light round it and all along it
            var (bx, by) = ToWorld(0, 0, 0, y, a);
            var (ex, ey) = ToWorld(TIP, 0, 0, y, a);
            p.Glow(0, y, 24, C[4], 0.3);
            p.GlowLine(bx, by, ex, ey, 7, C[5], 0.16);
            Key(p, C, 0, y, a, gu);
            // a few idle twinkles round it, one after another
            for (int i = 0; i < 4; i++)
            {
                double life = 0.4, age = ((t - (i + 0.6) / 4 * T) % T + T) % T;
                if (age > life) continue;
                double an = Rand(i, seed, 8) * TAU, d = 9 + Rand(i, seed, 9) * 6, k = Px.Sin(age / life * Math.PI);
                Glint(p, Px.Cos(an) * d, y + Px.Sin(an) * d * 0.8 - age * 6, Round(1.8 * k), i % 2 != 0 ? C[6] : WHITE, 0.35 * k);
            }
            // light rides with the glint
            if (gu > -6)
            {
                var (gx, gy) = ToWorld(Clamp(gu, 0, TIP), 0, 0, y, a);
                p.Glow(gx, gy, 10, C[6], 0.45 * Px.Sin(q * Math.PI));
            }
            // as it leaves the tip: a star, and sparks that arc away and fall
            var (tx, ty) = ToWorld(TIP, 0, 0, y, a);
            double at = (ph - G1 + 0.02) * T;
            if (at > 0 && at < 0.3) Glint(p, tx, ty, 4.5 * (1 - at / 0.3), C[6], 0.6 * (1 - at / 0.3));
            for (int i = 0; i < 9; i++)
            {
                double life = 0.32 + Rand(i, seed, 4) * 0.3, age = at - Rand(i, seed, 5) * 0.05;
                if (age < 0 || age > life) continue;
                double sp = 25 + Rand(i, seed, 1) * 45, an = a + (Rand(i, seed, 2) - 0.5) * 2.2;
                var (x, yy) = Fly(tx, ty, Px.Cos(an) * sp, Px.Sin(an) * sp - 25, age, 380, 2.5);
                var (vx, vy) = FlyV(Px.Cos(an) * sp, Px.Sin(an) * sp - 25, age, 380, 2.5);
                if (yy > -0.5) continue; // it has reached the floor
                double s = Hypot(vx, vy);
                if (s == 0 || double.IsNaN(s)) s = 1;
                double L2 = Math.Min(4, 1 + s * 0.03), k = 1 - age / life;
                p.Line(x - vx / s * L2, yy - vy / s * L2, x, yy, u => u > 0.6 ? WHITE : C[5], k * 1.6);
                p.Glow(x, yy, 3, C[5], 0.4 * k);
            }
        }
    }
}
