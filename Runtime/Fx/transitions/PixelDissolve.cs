// The glints and the twinkling pixel dust follow Sprite Fusion's open-source sparkle
// (https://www.spritefusion.com/pixel-art-vfx/sparkle), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class PixelDissolve : Effect
    {
        public static readonly Module Module = Screen("pixel-dissolve", "Pixel dissolve",
            "Clumps of blocks wink in, each drawn in a white outline that fills black and cools, with glints; then they wink out, outlines collapsing, the last small ones sparkling away.",
            new[] { "dissolve", "pixel", "blocks", "glint" }, 2.2,
            new Options { ["palette"] = PAL["neon"], ["seed"] = 1 },
            (o, e) => new PixelDissolve(o));

        // P in the JS original: how long a block takes to wink in or out
        const double PW = 0.17, IN0 = 0.04, IN1 = 0.8, OUT0 = 1.12, OUT1 = 1.85, CELL = 20;
        static readonly int[][] Plus = { new[] { 0, 0 }, new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        sealed class Block
        {
            public double x, y, s, tin, tout, gx, gy, spin, L;
            public bool glint;
        }

        // the blocks: 20 px cells, some whole, some in quarters, some in sixteenths. Each winks in and out at its own
        // time read off a clumpy field, so they gather in growing patches: small ones scatter first, big ones fill in
        static readonly Dictionary<double, List<Block>> cache = new Dictionary<double, List<Block>>();

        static List<Block> Layout(double seed)
        {
            if (cache.TryGetValue(seed, out List<Block> hit)) return hit;
            var blocks = new List<Block>();
            for (int cy = 0; cy < H / CELL; cy++)
                for (int cx = 0; cx < W / CELL; cx++)
                {
                    double h = Rand(cx, cy, seed);
                    int n = h < 0.32 ? 1 : h < 0.74 ? 2 : 4;
                    double s = CELL / n;
                    for (int j = 0; j < n; j++)
                        for (int i = 0; i < n; i++) blocks.Add(new Block { x = cx * CELL + i * s, y = cy * CELL + j * s, s = s });
                }
            float[] Rank(Func<Block, double> key)
            {
                double[] v = blocks.Select(key).ToArray();
                var o = new float[v.Length];
                int k = 0;
                foreach (int i in Enumerable.Range(0, v.Length).OrderBy(i => v[i])) o[i] = (float)((double)k++ / (v.Length - 1));
                return o;
            }
            double At(Block b, double sd) => Fbm((b.x + b.s / 2) / 64, (b.y + b.s / 2) / 64, sd, 3);
            float[] uin = Rank(b => At(b, seed) + 0.22 * Rand(b.x, b.y, seed + 1) + (b.s == 20 ? 0.12 : b.s == 5 ? -0.08 : 0));
            float[] uout = Rank(b => At(b, seed + 7) + 0.22 * Rand(b.x, b.y, seed + 2) + (b.s == 20 ? -0.14 : b.s == 5 ? 0.1 : 0));
            for (int i = 0; i < blocks.Count; i++)
            {
                Block b = blocks[i];
                b.tin = IN0 + uin[i] * (IN1 - IN0);
                b.tout = OUT0 + uout[i] * (OUT1 - OUT0);
                b.glint = Rand(b.x, b.y, seed + 3) < (b.s >= 10 ? 0.32 : 0.1);
                b.gx = b.x + (Rand(b.x, b.y, seed + 4) < 0.5 ? 0 : b.s);
                b.gy = b.y + (Rand(b.x, b.y, seed + 5) < 0.5 ? 0 : b.s);
                b.spin = (Rand(b.x, b.y, seed + 6) - 0.5) * 3;
                // now and then a big block throws a hero glint
                b.L = b.s == 20 && Rand(b.x, b.y, seed + 8) < 0.45 ? 9 + 6 * Rand(b.x, b.y, seed + 9) : 2.5 + b.s * 0.32;
            }
            cache[seed] = blocks;
            return blocks;
        }

        // a mote of pixel dust: a lone pixel, or now and then a little cross
        static void Mote(Painter p, double x, double y, uint c, bool cross)
        {
            p.Px(x, y, c);
            if (cross)
                foreach (int[] d in Cross) p.Px(x + d[0], y + d[1], c);
        }

        // a square block, filled, with a 1 px rim
        static void Square(Painter p, double x, double y, double s, uint fill, uint rim = 0)
        {
            double X = Round(x), Y = Round(y), S = Math.Max(1, Round(s));
            if (fill != 0) Rect(p, X, Y, S, S, fill);
            if (rim != 0 && S > 2)
            {
                Rect(p, X, Y, S, 1, rim);
                Rect(p, X, Y + S - 1, S, 1, rim);
                Rect(p, X, Y, 1, S, rim);
                Rect(p, X + S - 1, Y, 1, S, rim);
            }
        }

        readonly Options o;

        PixelDissolve(Options o) => this.o = o;

        public override void Draw(Painter p0, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            uint ink = o.Color("color");
            Painter p = ClipTo(p0);
            List<Block> blocks = Layout(o.Seed);
            var glints = new List<(Block b, double u, int dir)>();
            double frame = Math.Floor(t * 15);
            for (int n = 0; n < blocks.Count; n++)
            {
                Block b = blocks[n];
                double ki = (t - b.tin) / PW, ko = (t - b.tout) / PW, x = b.x, y = b.y, s = b.s, cx = x + s / 2, cy = y + s / 2;
                if (ki < 0 || ko >= 1.6) continue;
                if (ko < 0)
                {
                    if (ki >= 1) Square(p0, x, y, s, ink);
                    else
                    {
                        // winking in: a white outline swells to the block, it fills with black, its rim cools down the ramp
                        double g = ki < 0.35 ? 0.35 + 0.65 * Ease.OutBack(ki / 0.35) : 1, h = s * g;
                        uint rim = ki < 0.45 ? WHITE : ki < 0.6 ? C[6] : ki < 0.75 ? C[5] : ki < 0.9 ? C[4] : C[3];
                        Square(p, cx - h / 2, cy - h / 2, h, ki > 0.2 ? ink : 0, rim);
                        if (ki < 0.3 && s >= 10)
                            foreach (int[] d in Plus) p.Px(cx - 0.5 + d[0], cy - 0.5 + d[1], d[0] != 0 || d[1] != 0 ? C[6] : WHITE);
                        p.Glow(cx, cy, s * 0.8 + 4, C[5], 0.45 * (1 - ki));
                    }
                    if (b.glint && ki > 0.15 && ki < 1.6) glints.Add((b, (ki - 0.15) / 1.45, 1));
                    // pixel dust flickers round the fresh block
                    if (ki < 1.4 && Rand(frame, n, 9) < 0.35) Mote(p, x + Rand(frame, n, 10) * (s + 6) - 3, y + Rand(frame, n, 11) * (s + 6) - 3, Rand(frame, n, 12) < 0.5 ? WHITE : C[6], Rand(frame, n, 17) < 0.25);
                }
                else
                {
                    if (ko < 0.35)
                    {
                        // winking out: its rim lights up...
                        double q = ko / 0.35;
                        Square(p0, x, y, s, ink, q < 0.4 ? C[4] : q < 0.75 ? C[5] : C[6]);
                        p.Glow(cx, cy, s * 0.8 + 3, C[4], 0.35 * q);
                    }
                    else if (ko < 0.75)
                    {
                        // ...the black drops away and the white outline collapses into its heart
                        double q = (ko - 0.35) / 0.4, h = s * (1 - Ease.InQuad(q) * 0.85);
                        Square(p, cx - h / 2, cy - h / 2, h, 0, q < 0.5 ? WHITE : C[6]);
                        p.Glow(cx, cy, s * 0.8 + 4, C[5], 0.45 * (1 - q * 0.5));
                    }
                    else if (ko < 1)
                    {
                        double q = (ko - 0.75) / 0.25;
                        foreach (int[] d in Plus)
                            if (!(d[0] != 0 || d[1] != 0) || q < 0.5) p.Px(cx - 0.5 + d[0], cy - 0.5 + d[1], d[0] != 0 || d[1] != 0 ? C[6] : WHITE);
                        p.Glow(cx, cy, s * 0.5 + 3, C[5], 0.35 * (1 - q));
                    }
                    if (b.glint && ko > 0.3) glints.Add((b, (ko - 0.3) / 1.3, -1));
                    if (ko > 0.3 && ko < 1.5 && Rand(frame, n, 13) < 0.3) Mote(p, x + Rand(frame, n, 14) * (s + 6) - 3, y + Rand(frame, n, 15) * (s + 6) - 3, Rand(frame, n, 16) < 0.5 ? WHITE : C[6], Rand(frame, n, 18) < 0.25);
                }
            }
            // glints on top: a spinning star on a corner of the block, swelling and fading
            foreach (var (b, u, dir) in glints)
            {
                if (u <= 0 || u >= 1) continue;
                double L = b.L * Px.Pow(Px.Sin(Math.PI * u), 0.7);
                Glint(p, b.gx, b.gy, L, b.spin * u * dir + 0.4, C[5], C[4]);
                if (b.L > 8) p.Glow(b.gx, b.gy, 18 + L, C[5], 0.25 * Px.Sin(Math.PI * u));
            }
        }
    }
}
