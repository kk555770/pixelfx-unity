// The bolt's forked shape, its halo-body-core inking and its flicker follow Sprite Fusion's open-source
// lightning (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class StormCloud : Effect
    {
        public static readonly Module Module = new Module("storm-cloud", "pixelfx/fx/lightning/storm-cloud", 168, 176, 84, 164, 3, true, 30,
            new Options
            {
                ["palette"] = VOLT,
                ["sparks"] = PLASMA,
                ["cloud"] = new[] { "#09070f", "#120f1e", "#1b182e", "#252640", "#323758", "#464d78", "#646b9c" },
                ["rain"] = new[] { "#2b3250", "#46527a", "#6d7cab", "#a5b5e0" },
                ["seed"] = 8,
            },
            (o, e) => new StormCloud(o, e))
        {
            Title = "Storm cloud",
            Desc = "A storm cloud boils overhead, billows lit from inside by flickering discharges, ragged scud under its flat dark base; rain slants down and once a loop a bolt cracks to the floor.",
            Tags = new[] { "lightning", "storm", "weather", "cloud", "rain", "ambient", "loop" },
            Bg = "ground",
        };

        const int FPS = 30, N = 90;
        const double P = 3, BASE = -100, TOP = -156, TH = 0.35;
        static readonly double VIS = Math.Sqrt(1 - Math.Sqrt(TH));
        // the billows: [x, y, visible radius, squash]; a tower leaning into a flat anvil sheared off to the right, big
        // billows of all sizes below it, smaller shoulders, a flat run along the base
        static readonly double[][] BILLOWS =
        {
            new double[] { -6, -132, 17 }, new double[] { 18, -127, 14 }, new double[] { -28, -122, 12 }, new double[] { 38, -118, 10 }, new double[] { 0, -142, 12 },
            new[] { 14, -149, 19, 0.38 }, new[] { 40, -148, 14, 0.34 },
            new double[] { -48, -113, 10 }, new double[] { 55, -110, 9 }, new double[] { -17, -115, 13 }, new double[] { 22, -113, 12 },
            new double[] { -42, -104, 11 }, new double[] { -10, -104, 12 }, new double[] { 20, -104, 11 }, new double[] { 48, -103, 9 }, new double[] { -63, -104, 7 }, new double[] { 66, -103, 6 },
        };
        // discharges inside: [frame, billow, strength]; the bolt to the floor at frame BOLT, out of billow BOLT_B
        static readonly double[][] FLASHES =
        {
            new[] { 7, 2, 0.8 }, new[] { 8, 9, 0.55 }, new[] { 29, 1, 0.75 }, new double[] { 31, 10, 1 }, new[] { 33, 3, 0.5 }, new[] { 70, 0, 0.8 }, new[] { 71, 4, 0.55 }, new[] { 72, 5, 0.35 },
        };
        const int BOLT = 52, BOLT_B = 12;
        static readonly double[] BK = { 1, 0.7, 0.3, 0.95, 0.5, 0.75, 0.2 };
        const double W0 = -84, H0 = TOP - 6;
        const int GW = 168, GH = (int)(BASE + 8 - H0);
        static readonly float[] FIELD = new float[GW * GH], GXs = new float[GW * GH], GYs = new float[GW * GH], LIT = new float[GW * GH];

        sealed class Billow
        {
            public double sq, x, y, r;
        }

        // where each billow is this frame: drifting, breathing and boiling up on its own clock, all seamless over the loop
        static Billow[] Billows(double seed, double ph)
        {
            var o = new Billow[BILLOWS.Length];
            for (int i = 0; i < BILLOWS.Length; i++)
            {
                double[] b = BILLOWS[i];
                double x = b[0], y = b[1], v = b[2], sq = b.Length > 3 ? b[3] : 1;
                bool top = y < -112 && sq == 1;
                double boil = 0.82 + 0.3 * Px.Pow(Px.Sin(Math.PI * ((ph + i * 0.37) % 1)), 2);
                o[i] = new Billow
                {
                    sq = sq,
                    x = x + 3.5 * LoopNoise(ph, 0.7, seed + i) + (sq < 1 ? 4 * LoopNoise(ph, 0.5, seed + i + 90) : 0),
                    y = y + 2 * LoopNoise(ph, 0.7, seed + i + 30) - (top ? 4 * (boil - 0.97) : 0),
                    r = (v * (top ? boil : 1 + 0.08 * LoopNoise(ph, 0.6, seed + i + 60))) / VIS,
                };
            }
            return o;
        }

        // the cloud as one field: billows summed so they fuse, clipped flat at the base, its edge ragged with drifting
        // noise; shaded as one mass (a lit top, a cool middle, a dark belly), discharges lighting whole billows from inside
        static void Cloud(Painter p, Billow[] B, double ph, double seed, uint[] K, uint[] C, Dictionary<int, double> flashes)
        {
            Array.Clear(FIELD, 0, FIELD.Length);
            Array.Clear(GXs, 0, GXs.Length);
            Array.Clear(GYs, 0, GYs.Length);
            Array.Clear(LIT, 0, LIT.Length);
            for (int i = 0; i < B.Length; i++)
            {
                Billow b = B[i];
                double k = flashes.TryGetValue(i, out double fk) ? fk : 0, r2 = b.r * b.r, ry = b.r * b.sq;
                for (double y = Math.Max(0, Math.Floor(b.y - ry - H0)); y < Math.Min(GH, Math.Ceiling(b.y + ry - H0)); y++)
                    for (double x = Math.Max(0, Math.Floor(b.x - b.r - W0)); x < Math.Min(GW, Math.Ceiling(b.x + b.r - W0)); x++)
                    {
                        double dx = x + W0 + 0.5 - b.x, dy = (y + H0 + 0.5 - b.y) / b.sq, d2 = (dx * dx + dy * dy) / r2;
                        if (d2 >= 1) continue;
                        int j = (int)(y * GW + x);
                        double u = 1 - d2, c = u * u;
                        FIELD[j] = (float)(FIELD[j] + c);
                        GXs[j] = (float)(GXs[j] + (-4 * u * dx) / r2);
                        GYs[j] = (float)(GYs[j] + (-4 * u * dy) / (r2 * b.sq));
                        if (k != 0 && !double.IsNaN(k)) LIT[j] = (float)(LIT[j] + k * (0.25 + 0.75 * c) * (d2 < 1 ? 1 : 0));
                    }
            }
            uint[] ramp = new uint[K.Length + 4];
            Array.Copy(K, ramp, K.Length);
            ramp[K.Length] = C[3];
            ramp[K.Length + 1] = C[4];
            ramp[K.Length + 2] = C[5];
            ramp[K.Length + 3] = C[6];
            int n = K.Length;
            for (int y = 0; y < GH; y++)
            {
                double cy = y + H0 + 0.5;
                for (int x = 0; x < GW; x++)
                {
                    int j = y * GW + x;
                    if (FIELD[j] < TH - 0.06) continue;
                    double cx = x + W0 + 0.5, bse = BASE + 1.5 * TileNoise(cx * 0.12 + 9 * ph, 3, 9, 0, seed + 5);
                    double F = FIELD[j] * (1 - Smooth(bse - 3, bse, cy)) + 0.05 * TileNoise(cx * 0.21 + 18 * ph, cy * 0.21, 18, 0, seed);
                    if (F < TH) continue;
                    // the field's slope rounds each billow's top; height does the rest: lit above, cool in the middle, dark below
                    double gx = GXs[j], gy = GYs[j], hy = Hypot(gx, gy), gl = hy != 0 && !double.IsNaN(hy) ? hy : 1, lam = -(gx * 0.42 + gy * 0.9) / gl, edge = Math.Min(1, gl * 5) * (F < TH + 0.08 ? 1 : 0.55);
                    double h = 1 - 2 * Clamp((cy - TOP) / (BASE - TOP));
                    double i = 2.7 + 2.2 * h + 1.3 * lam * edge + 0.4 * TileNoise(cx * 0.16 + 12 * ph, cy * 0.16, 12, 0, seed + 9);
                    if (F < TH + 0.05 && lam > 0.3 && h > -0.5) i += 1;
                    if (cy > bse - 6) i -= 1;
                    i = Clamp(Round(i), 0, n - 1);
                    // lit from inside: brightest deep in the billow, breaking up through the cloud's own lumps toward its edges
                    double L = LIT[j] * (0.7 + 0.6 * TileNoise(cx * 0.2 - 14 * ph, cy * 0.2, 14, 0, seed + 3));
                    if (L > 0.12) i = Math.Min(ramp.Length - 1, i + Round(L * 5.5));
                    p.Px(cx, cy, ramp[(int)i]);
                }
            }
        }

        // scud: ragged wisps that form under the base, drift off on the wind and fray away
        static void Scud(Painter p, double ph, double seed, uint[] K)
        {
            Rng r = NewRng(seed, 13);
            for (int w = 0; w < 6; w++)
            {
                double x0 = r.Range(-60, 60), y0 = BASE + r.Range(3, 9), rx = r.Range(10, 20), ry = r.Range(1.3, 2.2), off = r.Next(), u = (ph + off) % 1, a = Px.Pow(Px.Sin(Math.PI * u), 0.8);
                double xc = x0 + 16 * (u - 0.5);
                for (double y = Math.Floor(y0 - ry - 1); y <= y0 + ry + 1; y++)
                    for (double x = Math.Floor(xc - rx - 1); x <= xc + rx + 1; x++)
                    {
                        double dx = (x + 0.5 - xc) / rx, dy = (y + 0.5 - y0) / ry, nz = TileNoise((x + 0.5) * 0.35 + 10 * ph + w * 7, (y + 0.5) * 0.5, 10, 0, seed + 20 + w);
                        double v = (1 - dx * dx - dy * dy) * a + 0.6 * nz - 0.25;
                        if (v > 0) p.Blend(x + 0.5, y + 0.5, v > 0.4 ? K[2] : K[3], Math.Min(0.6, 0.2 + v * 0.7));
                    }
            }
        }

        readonly Options o;

        StormCloud(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), K = Fit(o.Ramp("cloud"), 7), RN = Fit(o.Ramp("rain"), 4), S = Ramp(o.Ramp("sparks"));
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            double ph = (double)f / N;
            Billow[] B = Billows(o.Seed, ph);
            Rng br = NewRng(o.Seed, 5, BOLT);
            int ba = f - BOLT;
            double bx0 = BILLOWS[BOLT_B][0] + br.Range(-4, 4), bx1 = br.Range(-24, 24);
            // what's lighting the cloud this frame: discharges inside billows, and the billow the bolt leaves from
            var flashes = new Dictionary<int, double>();
            var order = new List<int>();
            void Set(int b, double k)
            {
                if (!flashes.ContainsKey(b)) order.Add(b);
                flashes[b] = k;
            }
            foreach (double[] fl in FLASHES) if (fl[0] == f) Set((int)fl[1], fl[2]);
            if (ba >= 0 && ba < BK.Length) Set(BOLT_B, Math.Max(flashes.TryGetValue(BOLT_B, out double bk) ? bk : 0, BK[ba]));
            var lights = new List<double[]>();
            foreach (int b in order) lights.Add(new[] { B[b].x, B[b].y, flashes[b], B[b].r * VIS * (B[b].sq < 1 ? 1.6 : 2.2) });
            double flash = 0;
            foreach (double[] l in lights) flash = Math.Max(flash, l[2]);

            // rain: slanting streaks from the belly, splashing on the floor, lit when the sky flashes
            Cycle(t, P, 150, o.Seed + 11, g => g.Range(0.5, 0.62), (age, life, r, _) =>
            {
                double x0 = r.Range(-62, 62), vy = r.Range(190, 240), vx = -vy * 0.18, tf = (0 - (BASE + 4)) / vy;
                if (age < tf)
                {
                    double x = x0 + vx * age, y = BASE + 4 + vy * age, l = r.Range(4, 6.5);
                    p.Line(x - vx * 0.018 * (l / 4), y - l, x, y, u => u > 0.7 ? (flash > 0.5 ? C[5] : RN[3]) : flash > 0.5 ? C[3] : RN[u > 0.35 ? 2 : 1]);
                }
                else if (age < tf + 0.12)
                {
                    double x = x0 + vx * tf, a = (age - tf) / 0.12;
                    foreach (int s in new[] { -1, 1 }) p.Px(x + s * (1 + a * 2.5), -1 - Px.Sin(a * Math.PI) * 2.5, a < 0.5 ? RN[3] : RN[2]);
                    if (a < 0.4) p.Px(x, 0, RN[3]);
                }
            });
            // the scud hanging under the base, the cloud, then the light of whatever burns inside it
            Scud(p, ph, o.Seed, K);
            Cloud(p, B, ph, o.Seed, K, C, flashes);
            foreach (double[] l in lights) p.Glow(l[0], l[1], l[3], C[4], 0.4 * l[2]);
            p.Glow(0, -122, 80, C[2], 0.05 + 0.22 * flash);
            // a discharge crawls along the belly now and then
            foreach (double[] fl in FLASHES)
                if (fl[0] == f && fl[2] > 0.6)
                {
                    Rng r = NewRng(o.Seed, 7, fl[0]);
                    double x = B[(int)fl[1]].x;
                    Crackle(p, Zigzag(r, x - r.Range(12, 22), BASE - r.Range(2, 6), x + r.Range(12, 22), BASE - r.Range(1, 5), 0.25, 0.5), 0.6, C, 0.4);
                }
            // the bolt: struck once a loop, forked, flickering, the floor lit and sparks thrown where it lands
            if (ba >= 0 && ba < BK.Length)
            {
                double k = BK[ba];
                Stroke(p, Zigzag(NewRng(o.Seed, 9, BOLT), bx0, BASE - 3, bx1, 0, 0.22, 0.7), k, C, ba == 0 ? 1.15 : 1, new StrokeOpt { hero = ba == 0 });
                p.Circle(bx1, -1, 3 * k + 1.5, C[2]);
                p.Circle(bx1, -1, 3 * k + 0.5, C[5]);
                p.Circle(bx1, -1, 2 * k, WHITE);
                p.Glow(bx1, -22, 78, C[3], 0.5 * k);
                p.Glow(bx1, -3, 18, C[5], 0.45 * k);
                p.Glow(bx0, BASE, 84, C[4], 0.3 * k);
            }
            double sa = (((t - (double)BOLT / FPS) % P) + P) % P;
            for (int j = 0; j < 16; j++)
            {
                Rng q = NewRng(o.Seed, 31, j);
                double life = q.Range(0.25, 0.6);
                if (sa >= life) continue;
                double a = -Math.PI / 2 + q.Range(-1.3, 1.3), sp = q.Range(60, 200);
                double[] s = Flight(bx1, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, sa, floor: 0);
                Spark(p, s[0], s[1], s[2], s[3], 1 - sa / life, S);
            }
        }
    }
}
