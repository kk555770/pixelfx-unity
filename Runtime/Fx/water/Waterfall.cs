// The pool's caustic light (two warped sine nets, light where they cross zero) is adapted from Sprite Fusion's
// open-source bubbles (https://www.spritefusion.com/pixel-art-vfx/bubbles), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Waterfall : Effect
    {
        public static readonly Module Module = new Module("waterfall", "pixelfx/fx/water/waterfall", 144, 136, 72, 118, 2, true, 30,
            new Options { ["palette"] = WATER, ["height"] = 92, ["width"] = 13, ["ledge"] = true, ["seed"] = 5 },
            (o, e) => new Waterfall(o, e))
        {
            Title = "Waterfall",
            Desc = "A river pours from a notch in a rock outcrop: a white curl, a deep sheet that splits into strands, churning foam, spray, drifting mist and rings on a dark pool.",
            Tags = new[] { "water", "waterfall", "cascade", "foam", "mist", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 2, SY = 0.2, G = 760, POOL_RX = 56, POOL_RY = 11;
        static readonly int[][] Cross2 = { new[] { 2, 0 }, new[] { -2, 0 }, new[] { 0, 2 }, new[] { 0, -2 } };

        // half the sheet's width at depth d below the lip: it spreads a little as it falls
        static double HalfW(double W, double d, double H) => W + 3.2 * Px.Pow(Clamp(d / H), 1.6);

        // the falling sheet, in 1 px lanes
        static void Sheet(Painter p, uint[] C, double ph, double H, double W, double S)
        {
            for (double y = -H + 2; y <= -1; y++)
            {
                double d = y + 0.5 + H, v = d / H, s = Math.Sqrt(d + 3), hw0 = HalfW(W, d, H), split = Smooth(0.56, 0.95, v), glass = 1 - Smooth(4, 24, d);
                double wl = TileNoise(2, s * 1.4 - ph * 12, 0, 12, S + 2) * (0.4 + 1.8 * v), wr = TileNoise(11, s * 1.4 - ph * 12, 0, 12, S + 2) * (0.4 + 1.8 * v);
                double fan = split > 0 ? TileNoise(3, s * 0.8 - ph * 4, 0, 4, S + 61) * 1.6 : 0, sd = s * 1.5 - ph * 20, sb = s * 0.45 - ph * 6;
                for (double X = -W - 8; X <= W + 8; X++)
                {
                    double x = X + 0.5, hw = hw0 + (x < 0 ? wl : wr), ax = Math.Abs(x);
                    if (ax > hw) continue;
                    double e = ax / hw;
                    bool edge = false;
                    if (split > 0)
                    {
                        double f = ((((x / (1 + 0.22 * split) + fan + 64) / 5.4) % 1) + 1) % 1;
                        if (f > 1 - 0.46 * split) continue;
                        edge = f < 0.2;
                    }
                    double dash = TileNoise(X * 1.618 + 0.31, sd, 0, 20, S);
                    if (Rand(X, S + 60) < 0.48 && e < 0.86 && !edge)
                    {
                        p.Blend(x, y + 0.5, dash > 0.45 ? C[4] : glass > 0.5 ? C[3] : C[2], dash > 0.45 ? 0.7 : 0.42 + 0.15 * v);
                        continue;
                    }
                    double broad = TileNoise(Math.Floor((x + 40) / 3) * 2.7, sb, 0, 6, S + 1);
                    double b = -0.34 + 0.46 * v + 0.3 * broad - glass * 0.5 + 0.35 * Smooth(0.7, 1, e) * (x < 0 ? 1 : 0.3) - (x > 0 ? 0.12 * e : 0) + (edge ? 0.3 : 0);
                    if (dash > 0.2 - 0.22 * v) b += 0.36 + 0.7 * (dash - 0.2);
                    if (b > -0.2) p.Px(X, y, b > 0.62 ? WHITE : b > 0.34 ? C[6] : b > 0.08 ? C[5] : C[4]);
                    else p.Blend(x, y + 0.5, C[3], 0.8);
                }
            }
            // strands peel off the edges and fall free
            Cycle(ph * D, D, 16, S + 30, 0.4, (age, life, r, _) =>
            {
                double side = r.Sign(), d0 = r.Range(14, H * 0.7), d = d0 + 50 * age + 260 * age * age, k = age / life;
                if (d > H - 6) return;
                double x = side * (HalfW(W, d, H) + 1.5 + age * r.Range(6, 16)), y = -H + d, len = 2 + 6 * k;
                p.Line(x, y - len, x, y, u => u > 0.7 ? C[6] : C[4], 1 - k * 0.5);
            });
        }

        sealed class RockMap
        {
            public sbyte[] id;
            public float[] lit;
        }

        // which boulder is frontmost at each pixel of the outcrop, and how much light its face catches there
        static readonly Dictionary<string, RockMap> ROCKS = new Dictionary<string, RockMap>();

        static RockMap MakeRockMap(double[][] lumps, double x0, double y0, int w, int hgt, double Lx, double Ly, double Lz, double S)
        {
            var id = new sbyte[w * hgt];
            var lit = new float[w * hgt];
            for (int n = 0; n < id.Length; n++) id[n] = -1;
            for (int j = 0; j < hgt; j++)
                for (int i = 0; i < w; i++)
                {
                    double x = x0 + i + 0.5, y = y0 + j + 0.5;
                    int best = -1;
                    double z = -1e9, l = 0;
                    for (int k = 0; k < lumps.Length; k++)
                    {
                        double cx = lumps[k][0], cy = lumps[k][1], rx = lumps[k][2], ry = lumps[k][3];
                        double nx = (x - cx) / rx, ny = (y - cy) / ry, r2 = nx * nx + ny * ny;
                        if (r2 >= 1) continue;
                        double nz = Math.Sqrt(1 - r2), depth = nz * ry + (k == 5 ? -6 : k == 2 ? -3 : k > 2 ? -1 : 0);
                        if (depth > z)
                        {
                            z = depth;
                            best = k;
                            l = nx * Lx + ny * Ly + nz * Lz;
                        }
                    }
                    id[j * w + i] = (sbyte)best;
                    lit[j * w + i] = (float)(l + 0.2 * TileNoise(x * 0.32, y * 0.32, 0, 0, S + 70));
                }
            return new RockMap { id = id, lit = lit };
        }

        static void Outcrop(Painter p, uint[] C, double ph, double H, double W, double S)
        {
            uint[] ST = Rock();
            const double Lx = -0.55, Ly = -0.72, Lz = 0.42;
            // [x, y, rx, ry]: a big boulder left of the notch, a smaller one right, the sill the water runs over, two feet,
            // and a slab behind that joins them, the notch cut into it
            double[][] lumps =
            {
                new[] { -W - 11, -H - 4, 13, 10 }, new[] { W + 8, -H - 2, 9, 7.5 }, new[] { 0, -H + 3.5, W + 3, 4.2 },
                new[] { -W - 21, -H + 4, 7, 5.5 }, new[] { W + 15, -H + 4.5, 5, 4 }, new[] { -2, -H - 5, W + 7, 6.5 },
            };
            double x0 = -W - 29, x1 = W + 21, y0 = -H - 15, y1 = -H + 10;
            int w = (int)(x1 - x0 + 1), hgt = (int)(y1 - y0 + 1);
            string key = ParityKey(W) + "|" + ParityKey(H) + "|" + ParityKey(S);
            if (!ROCKS.TryGetValue(key, out RockMap map)) ROCKS[key] = map = MakeRockMap(lumps, x0, y0, w, hgt, Lx, Ly, Lz, S);
            sbyte[] id = map.id;
            float[] lit = map.lit;
            // a soft shadow under it on whatever is behind
            p.Each(-W - 26, -H, W + 18, -H + 20, (x, y, sx, sy) =>
            {
                double k = 1 - Hypot((x + 4) / (W + 22), (y + H) / 20);
                if (k > 0) p.BlendPx(sx, sy, ST[0], 0.4 * Math.Min(1, k * 3));
            });
            int At(double i, double j) => i < 0 || j < 0 || i >= w || j >= hgt ? -1 : id[(int)j * w + (int)i];
            for (int j = 0; j < hgt; j++)
                for (int i = 0; i < w; i++)
                {
                    int k = id[j * w + i];
                    double x = x0 + i, y = y0 + j;
                    // the river runs over the sill: nothing of the sill shows through it
                    if ((k == 2 || k == 5 || k < 0) && y >= -H - 5 && y < -H + 1 && Math.Abs(x + 0.5) < W + 1.5) continue;
                    if (k < 0)
                    {
                        if (At(i - 1, j) >= 0 || At(i + 1, j) >= 0 || At(i, j - 1) >= 0 || At(i, j + 1) >= 0) p.Px(x, y, ST[0]);
                        continue;
                    }
                    // a crease where one boulder sits in front of another
                    int rr = At(i + 1, j), bb = At(i, j + 1);
                    if ((rr >= 0 && rr != k && rr < k) || (bb >= 0 && bb != k))
                    {
                        p.Px(x, y, ST[1]);
                        continue;
                    }
                    double l = lit[j * w + i];
                    bool wet = Math.Abs(x + 0.5) < W + 6 && y > -H - 9;
                    uint c = l > 0.9 ? ST[5] : l > 0.64 ? ST[4] : l > 0.3 ? ST[3] : l > -0.05 ? ST[2] : ST[1];
                    if (k == 5) c = l > 0.7 ? ST[3] : l > 0.2 ? ST[2] : ST[1];
                    if (At(i, j - 1) < 0 && l > 0.25) c = ST[5];
                    if (wet)
                    {
                        // wet stone: a step darker, a cool gloss running down it where the light catches
                        c = l > 0.9 ? ST[4] : l > 0.64 ? ST[3] : l > 0.3 ? ST[2] : ST[1];
                        if (l > 0.3 && TileNoise(x * 0.9, y * 0.6 - ph * 8, 0, 8, S + 71) > 0.42) c = l > 0.6 ? C[6] : C[4];
                    }
                    p.Px(x, y, c);
                }
            // cracks: dark lines with a lit lip above them
            for (int q = 0; q < 3; q++)
            {
                double[] L = lumps[q % 2];
                double cx = Math.Floor(L[0] + (q == 2 ? -0.5 : 0.15) * L[2]), cy = Math.Floor(L[1] + L[3] * (q == 2 ? 0.1 : -0.25));
                double len = 3 + Math.Floor(Rand(q, S + 74) * 4), dx = Rand(q, S + 75) < 0.5 ? 1 : -1;
                for (double n = 0; n <= len; n++)
                {
                    double x = cx + Round(n * 0.6) * dx, y = cy + n, i = x - x0, j = y - y0;
                    if (At(i, j) < 0 || At(i, j) > 1 || At(i, j + 1) < 0) break;
                    p.Px(x, y, n == len ? ST[1] : ST[0]);
                    if (n == 0) p.Px(x - dx, y - 1, ST[4]);
                }
            }
            // trickles run down the wet faces either side of the notch
            foreach (double sx in new[] { -W - 3, W + 3 })
            {
                for (double y = -H - 2; y < -H + 7; y++)
                {
                    double g = ((y * 0.35 - ph * 6) % 1 + 1) % 1, i = Math.Floor(sx) - x0, j = Math.Floor(y) - y0;
                    if (At(i, j) >= 0) p.Px(sx, y, g < 0.3 ? C[6] : C[3]);
                }
            }
        }

        static string ParityKey(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        // the river sliding out of the notch between the boulders, seen from just above: flow lines sliding to the edge
        static void River(Painter p, uint[] C, double ph, double H, double W, double S)
        {
            double top = -H - 6;
            p.Shade(-W - 3, top, W + 3, -H, (x, y, @out) =>
            {
                double back = top + 1.5 + TileNoise(x * 0.3, 4, 0, 0, S + 62) * 1.5, v = (y - back) / (-H - back), hw = W + 2 - (1 - v) * 3;
                if (y < back || Math.Abs(x) > hw) return 0;
                double n = TileNoise(x * 0.32, (y - top) * 0.7 - ph * 8, 0, 8, S + 3);
                @out.c = n > 0.66 ? C[6] : n > 0.4 ? C[5] : v < 0.5 ? C[3] : C[4];
                return 0;
            });
        }

        // the curl over the edge: glassy blue as it rolls, a hard white line along the bend; its light on the stone
        static void Curl(Painter p, uint[] C, double ph, double H, double W, double S)
        {
            for (double x = -W; x <= W; x++)
            {
                double g = TileNoise(x * 0.45, ph * 4, 0, 4, S + 4);
                p.Px(x, -H - 1, C[4]);
                p.Px(x, -H, WHITE);
                p.Px(x, -H + 1, g > 0.2 ? C[6] : C[5]);
            }
            p.GlowLine(-W, -H, W, -H, 6, C[6], 0.18);
            p.Glow(0, -H, 34, C[5], 0.2);
            // glints run along it
            Cycle(ph * D, D, 4, S + 5, 0.36, (age, life, r, _) =>
            {
                double k = Px.Sin((Math.PI * age) / life), x = r.Range(-W, W);
                if (k > 0.5) foreach (int[] v in Sim.Cross) p.Px(x + v[0], -H + v[1], C[6]);
                p.Glow(x, -H, 6, C[5], 0.4 * k);
            });
        }

        // the pool: dark see-through water, a lit far rim, caustic light swimming in it, the falls' reflection
        static void Pool(Painter p, uint[] C, double ph, double W, double S)
        {
            double RX = POOL_RX, RY = POOL_RY, cy = 2;
            double T = ph * TAU;
            int nc = (int)(2 * RX);
            var c1 = new double[nc];
            var c2 = new double[nc];
            for (double X = -RX; X < RX; X++)
            {
                double x = X + 0.5;
                c1[(int)(X + RX)] = Px.Sin(x * 0.11 - T * 2);
                c2[(int)(X + RX)] = Px.Sin(T + x * 0.05) * 1.4 + x * 0.13;
            }
            for (double Y = Math.Floor(cy - RY); Y < cy + RY; Y++)
            {
                double y = Y + 0.5, dy = (y - cy) / RY, sy = Px.Sin(y * 0.5 + T);
                for (double X = -RX; X < RX; X++)
                {
                    double x = X + 0.5, dx = x / RX, d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    p.Blend(x, y, d < 0.12 ? C[2] : C[1], 0.62 * Math.Min(1, (1 - d) * 4));
                    // caustics: two warped sine nets, light where either crosses zero
                    double cx1 = c1[(int)(X + RX)], cx2 = c2[(int)(X + RX)], u = x * 0.2 + 1.8 * sy, w = y * 0.55 + 1.8 * cx1;
                    double f = (Math.Max(0, 1 - Math.Abs(Px.Sin(u) + Px.Sin(w)) * 2.6) + 0.6 * Math.Max(0, 1 - Math.Abs(Px.Sin(cx2 + y * 0.3)) * 3)) * (1 - d);
                    if (f > 0.5) p.Light(x, y, C[5], 0.18 * f);
                }
            }
            // a few glints sit on the far shore
            for (int i = 0; i < 7; i++)
            {
                double a = Math.PI * (1.12 + 0.76 * ((i + 0.5) / 7)), x = Px.Cos(a) * RX * 0.96, y = cy + Px.Sin(a) * RY * 0.96, g = Px.Sin(ph * TAU * 2 + i * 2.4);
                if (g > 0.2) p.Px(x, y, g > 0.8 ? C[6] : C[4]);
            }
            // the falls mirrored in the water, broken by the chop
            p.Shade(-W - 3, 3, W + 3, 10, (x, y, @out) =>
            {
                double n = TileNoise(x * 0.9, (y - 3) * 0.35 + ph * 6, 0, 6, S + 6), k = 1 - (y - 3) / 7;
                if (Math.Abs(x) > W + 2 - (y - 3) * 0.4 || n < 0.25 + (1 - k) * 0.4) return 0;
                @out.c = n > 0.6 && k > 0.6 ? C[6] : C[5];
                return 0;
            });
        }

        // the foot: a churning white band, billows rolling out of it, spray thrown clear landing with rings
        static void Foam(Painter p, uint[] C, double t, double ph, double W, double S)
        {
            double R = W + 10;
            p.Shade(-R, -13, R, 3, (x, y, @out) =>
            {
                double ax = Math.Abs(x), lump = TileNoise(x * 0.22, ph * 6, 0, 6, S + 7) * 3 + TileNoise(x * 0.55 + 9, ph * 12, 0, 12, S + 8) * 1.5;
                double top = -8 + lump + Px.Pow(ax / R, 2) * 7;
                if (ax > R - Math.Max(0, -y - 1) * 0.6 || y < top) return 0;
                // the churn in lumps: each boil lit on top, shadowed under, darker where it meets the pool
                double n = TileNoise(x * 0.5, (y - top) * 0.5 - ph * 10, 0, 10, S + 9), boil = TileNoise(x * 0.42 + 3, y * 0.42 - ph * 8, 0, 8, S + 12), u = y - top;
                if (y > 1) @out.c = n > 0 ? C[5] : C[4];
                else if (u < 1.2) @out.c = WHITE;
                else if (boil < -0.3) @out.c = u > 4 ? C[4] : C[5];
                else if (boil < -0.05) @out.c = C[6];
                else @out.c = n > -0.2 ? WHITE : C[6];
                return 0;
            });
            Cycle(t, D, 30, S + 10, 0.7, (age, life, r, _) =>
            {
                double k = age / life, side = r.Sign(), x0 = r.Range(-W, W) * 0.9, sp = r.Range(2, 8), up = r.Range(2, 8);
                double x = x0 + side * Ease.OutCubic(k) * sp, y = -r.Range(2, 5) - Ease.OutCubic(k) * up, rad = r.Range(1.4, 3) * Px.Pow(Px.Sin(Math.PI * k), 0.7);
                if (rad > 0.6) Billow(p, x, y, rad, C);
            });
            // spray: beads thrown clear of the churn, landing in the pool with a ring
            Cycle(t, D, 18, S + 11, 1, (age, life, r, _) =>
            {
                double side = r.Sign(), x0 = side * r.Range(2, W + 4), vx = side * r.Range(12, 60), vy = -r.Range(70, 150), z = r.Range(-3, 9), s = r.Next() < 0.3 ? 2 : 1;
                double land = z * SY, tl = (-vy + Math.Sqrt(vy * vy + 2 * G * (land + 5))) / G;
                if (age < tl) Bead(p, x0 + vx * age, -5 + vy * age + 0.5 * G * age * age, vx, vy + G * age, s, C);
                else if (age < tl + 0.35) Ring(p, x0 + vx * tl, land, 1 + (age - tl) * 12, SY, 1 - (age - tl) / 0.35, C, n: 1);
            });
        }

        // mist: soft puffs rolled out of the churn that drift sideways along the pool, swell and thin, with a cool light
        static void DrawMist(Painter p, uint[] C, double t, double S, double n, double seed)
        {
            Cycle(t, D, n, S + seed, g => g.Range(1.8, 2.8), (age, life, r, _) =>
            {
                double k = age / life, side = r.Sign(), drift = Ease.OutCubic(k);
                double x = side * (r.Range(3, 12) + drift * r.Range(24, 48)), y = -r.Range(2, 7) - k * r.Range(3, 12);
                double rad = r.Range(4, 6) + k * r.Range(5, 9), a = r.Range(0.2, 0.3) * Math.Min(1, k / 0.12) * Px.Pow(1 - k, 1.7);
                p.Disc(x, y, rad, C[6], (d, g) => a * (1 - d * d));
                if (a > 0.05) p.Glow(x, y, rad * 1.2, C[4], 0.35 * a);
            });
        }

        readonly Options o;

        Waterfall(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, W = o.Num("width");
            Pool(p, C, ph, W, o.Seed);
            for (int i = 0; i < 3; i++)
            {
                double f = (ph + i / 3.0) % 1;
                Ring(p, 0, 1, W + 6 + f * 38, SY, 0.95 * Px.Pow(1 - f, 1.5), C, n: 1, half: -1, seed: i + 1);
            }
            DrawMist(p, C, t, o.Seed, 7, 20);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            double ph = t / D, H = o.Num("height"), W = o.Num("width"), S = o.Seed, T = ph * TAU;
            for (int i = 0; i < 3; i++)
            {
                double f = (ph + i / 3.0) % 1;
                Ring(p, 0, 1, W + 6 + f * 38, SY, 0.95 * Px.Pow(1 - f, 1.5), C, n: 1, half: 1, seed: i + 1, glint: f < 0.4 ? 1 - f * 2.5 : 0);
            }
            River(p, C, ph, H, W, S);
            if (o.Bool("ledge")) Outcrop(p, C, ph, H, W, S);
            Sheet(p, C, ph, H, W, S);
            Curl(p, C, ph, H, W, S);
            Foam(p, C, t, ph, W, S);
            DrawMist(p, C, t, S, 5, 21);
            // glints wink on the falling water and in the spray
            Cycle(t, D, 12, S + 50, g => g.Range(0.2, 0.4), (age, life, r, _) =>
            {
                double k = Px.Sin((Math.PI * age) / life);
                bool low = r.Next() < 0.45;
                double d = low ? H - r.Range(4, 16) : r.Range(12, H - 16);
                double x = low ? r.Range(-W - 10, W + 10) : r.Range(-0.8, 0.8) * HalfW(W, d, H), y = -H + d - (age / life) * (low ? 6 : -10);
                p.Px(x, y, WHITE);
                if (k > 0.55) foreach (int[] v in Sim.Cross) p.Px(x + v[0], y + v[1], C[6]);
                if (k > 0.85 && r.Next() < 0.5) foreach (int[] v in Cross2) p.Px(x + v[0], y + v[1], C[5]);
                p.Glow(x, y, 5, C[5], 0.35 * k);
            });
            // fine spray hangs in the air off the foot, drifting up and out
            Cycle(t, D, 34, S + 51, g => g.Range(0.9, 1.6), (age, life, r, _) =>
            {
                double k = age / life, side = r.Sign(), x = side * (r.Range(0, W + 6) + k * r.Range(6, 22)) + Px.Sin(age * 5 + r.Range(0, TAU)) * 1.2, y = -r.Range(3, 10) - k * r.Range(10, 34);
                if (k > 0.7 && r.Next() < (k - 0.7) * 3.3) return;
                p.Px(x, y, k < 0.4 && r.Next() < 0.5 ? WHITE : C[6]);
            });
            // light: down the sheet, a pool of it where the water lands, on the water around
            p.GlowLine(0, -H + 6, 0, -8, 26, C[4], 0.12);
            p.Glow(0, -5, 40, C[5], 0.32 + 0.05 * Px.Sin(T * 2));
            p.Glow(0, 4, 50, C[3], 0.14);
        }
    }
}
