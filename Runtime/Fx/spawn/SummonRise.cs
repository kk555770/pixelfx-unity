// The rubble, dust, grit, embers and smoke recipe is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class SummonRise : Effect
    {
        const double DURATION = 4.6;

        public static readonly Module Module = new Module("summon-rise", "pixelfx/fx/spawn/summon-rise", 176, 116, 88, 94, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0608", "#3d0c0c", "#7a1a10", "#c0381a", "#f06a1e", "#ffae3a", "#ffe08a", "#ffffff" },
                ["body"] = new[] { "#060409", "#0e0913", "#170f1e", "#211628", "#2d2036" },
                ["horn"] = new[] { "#0c080c", "#201820", "#352a32", "#4e3f48", "#6e5c64" },
                ["stone"] = new[] { "#1a1424", "#2b2639", "#4a4263", "#6c6488", "#9a91b4" },
                ["seed"] = 9,
            },
            (o, e) => new SummonRise(o, e))
        {
            Title = "Summon rise",
            Desc = "A rune circle burns into the floor, the ground bursts, and a horned brute heaves up out of it through dust and rubble, lit from below, until its eyes flare.",
            Tags = new[] { "spawn", "summon", "demon", "rise", "runes", "dust", "rubble", "ground", "one-shot" },
            Bg = "ground",
        };

        static readonly uint SOOT = Hex("#2a1e2a");
        const double IGN = 0.5, RUNES = 0.78, BREAK = 1.05, EYES = 2.3, FADE = 3.4, SY = 0.3, R0 = 36, R1 = 27;

        // the brute, built part by part so overlapping forms keep their own edges. Parts front to back: 1 head,
        // 2 horns, 3 arms, 4 shoulders, 5 chest, 6 waist, 7 legs, 8 tail. Each pixel keeps the frontmost part over it, that
        // part's surface direction (from its own blurred shape), and whether it sits on a crease over a part behind.
        sealed class Beast
        {
            public int w, h;
            public byte[] part;
            public float[] amb, down, rim;
            public byte[] crease;
            public int[][] outline, eyes, tusks;
            public double[][] glints;
        }

        static readonly Beast BEAST = MakeBeast();

        static Beast MakeBeast()
        {
            const int W = 46, H = 46, N = W * H;
            const double C = 22.5;
            var parts = new byte[8][];
            for (int k = 0; k < 8; k++) parts[k] = new byte[N];
            byte[] cur = null;
            void Put(double x, double y)
            {
                x = Round(x);
                y = Round(y);
                if (x >= 0 && y >= 0 && x < W && y < H) cur[(int)y * W + (int)x] = 1;
            }
            void Ell(double cx, double cy, double rx, double ry)
            {
                for (double y = Math.Floor(cy - ry); y <= Math.Ceiling(cy + ry); y++)
                    for (double x = Math.Floor(cx - rx); x <= Math.Ceiling(cx + rx); x++)
                    {
                        double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                        if (dx * dx + dy * dy <= 1) Put(x, y);
                    }
            }
            void Stroke(double[][] pts, double r0, double r1)
            {
                var L = new double[pts.Length];
                double total = 0;
                for (int i = 1; i < pts.Length; i++) total += L[i - 1] = Hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
                double acc = 0;
                for (int i = 1; i < pts.Length; acc += L[i - 1], i++)
                {
                    double n = Math.Ceiling(L[i - 1] * 3) + 1;
                    for (int j = 0; j <= n; j++)
                    {
                        double u = j / n, r = r0 + (r1 - r0) * ((acc + L[i - 1] * u) / total);
                        Ell(pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * u, pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * u, r, r);
                    }
                }
            }
            double[] Pt(double x, double y) => new[] { x, y };
            byte[] HEAD = parts[0], HORNS = parts[1], ARMS = parts[2], SHOULDERS = parts[3], CHEST = parts[4], WAIST = parts[5], LEGS = parts[6], TAIL = parts[7];
            foreach (int s in new[] { -1, 1 })
            {
                double X(double d) => C + s * d;
                cur = HORNS;
                Stroke(new[] { Pt(X(4), 11), Pt(X(8), 8), Pt(X(12), 6.5), Pt(X(15), 4), Pt(X(16), 1.5), Pt(X(15.2), 0) }, 2.3, 0.45);
                cur = SHOULDERS;
                Ell(X(10), 16, 7, 5);
                Ell(X(8.5), 19.5, 5.5, 4.2);
                cur = ARMS;
                Stroke(new[] { Pt(X(14.5), 16.5), Pt(X(17.5), 22), Pt(X(18.5), 28) }, 4, 3.2);
                Stroke(new[] { Pt(X(18.5), 28), Pt(X(19), 33), Pt(X(19.2), 36.5) }, 3.4, 2.8);
                Ell(X(19.2), 37.8, 3, 2.3);
                Stroke(new[] { Pt(X(17.2), 39), Pt(X(16.6), 41.8) }, 0.9, 0.4);
                Stroke(new[] { Pt(X(19.2), 39.5), Pt(X(19.3), 42.6) }, 0.9, 0.4);
                Stroke(new[] { Pt(X(21.2), 38.8), Pt(X(22), 41.4) }, 0.9, 0.4);
                cur = LEGS;
                Stroke(new[] { Pt(X(5.5), 31), Pt(X(8), 37), Pt(X(7.5), 42) }, 4, 3);
                Ell(X(8.5), 44.3, 4.2, 1.7);
            }
            cur = TAIL;
            Stroke(new[] { Pt(C, 30), Pt(C + 0.5, 36), Pt(C + 2.5, 41.5), Pt(C + 7, 44.6), Pt(C + 12.5, 44.2), Pt(C + 14.5, 42.5) }, 2.3, 0.6);
            cur = CHEST;
            Ell(C, 22.5, 8.2, 7.5);
            cur = WAIST;
            Ell(C, 29.5, 6.2, 4.5);
            cur = HEAD;
            Ell(C, 12.5, 5.2, 4.6);
            Ell(C, 16, 4.2, 2.8);
            var part = new byte[N];
            for (int i = 0; i < N; i++)
                for (int k = 0; k < parts.Length; k++)
                    if (parts[k][i] != 0)
                    {
                        part[i] = (byte)(k + 1);
                        break;
                    }
            bool On(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && part[y * W + x] > 0;
            // surface direction per part: the gradient of the part blurred on its own
            float[] Blur(byte[] src)
            {
                var a = new float[N];
                for (int i = 0; i < N; i++) a[i] = src[i];
                var b = new float[N];
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            double s = 0;
                            for (int d = -2; d <= 2; d++) s += x + d >= 0 && x + d < W ? (double)a[y * W + x + d] : 0;
                            b[y * W + x] = (float)(s / 5);
                        }
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            double s = 0;
                            for (int d = -2; d <= 2; d++) s += y + d >= 0 && y + d < H ? (double)b[(y + d) * W + x] : 0;
                            a[y * W + x] = (float)(s / 5);
                        }
                }
                return a;
            }
            float[] amb = new float[N], down = new float[N], rim = new float[N];
            var crease = new byte[N];
            int[][] cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
            for (int k = 0; k < parts.Length; k++)
            {
                float[] B = Blur(parts[k]);
                double At(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? (double)B[y * W + x] : 0;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;
                        if (part[i] != k + 1) continue;
                        double gx = At(x + 1, y) - At(x - 1, y), gy = At(x, y + 1) - At(x, y - 1), g = Hypot(gx, gy);
                        if (g == 0 || double.IsNaN(g)) g = 1e-6;
                        double ox = -gx / g, oy = -gy / g, e = Clamp(g / 0.28);
                        // a dim cool light from above and the left rounds each form; undersides fall into shadow
                        amb[i] = (float)(0.42 + e * (0.4 * Math.Max(0, -oy) + 0.14 * Math.Max(0, -ox) - 0.3 * Math.Max(0, oy) - 0.08 * Math.Max(0, ox)));
                        // less light reaches up into the gap between the legs, so it doesn't read as a lit doorway
                        down[i] = (float)(e * Math.Max(0, oy) * (Math.Abs(x + 0.5 - C) < 7 && y > 27 ? 0.3 : 1));
                        // a crease where this part's edge lies over a part behind it
                        foreach (int[] d in cross)
                        {
                            int j = (y + d[1]) * W + x + d[0];
                            if (On(x + d[0], y + d[1]) && part[j] > k + 1) crease[i] = 1;
                        }
                    }
            }
            // the underlight's rim: the bottom of the whole silhouette, two pixels deep; less in the gap between the legs
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!On(x, y)) continue;
                    double k = !On(x, y + 1) ? 1 : !On(x, y + 2) ? 0.5 : 0;
                    rim[y * W + x] = (float)(Math.Abs(x + 0.5 - C) < 7 && y > 27 ? k * 0.3 : k);
                }
            var outline = new List<int[]>();
            for (int y = -1; y <= H; y++)
                for (int x = -1; x <= W; x++)
                    if (!On(x, y) && (On(x - 1, y) || On(x + 1, y) || On(x, y - 1) || On(x, y + 1)))
                        outline.Add(new[] { x, y });
            var eyes = new List<int[]>();
            var tusks = new List<int[]>();
            foreach (int s in new[] { -1, 1 })
            {
                eyes.Add(new[] { RoundInt(C + s * 2.2 - 0.5), 12 });
                eyes.Add(new[] { RoundInt(C + s * 3.2 - 0.5), 12 });
                tusks.Add(new[] { RoundInt(C + s * 3.4 - 0.5), 16 });
                tusks.Add(new[] { RoundInt(C + s * 3.6 - 0.5), 15 });
            }
            return new Beast
            {
                w = W, h = H, part = part, amb = amb, down = down, rim = rim, crease = crease, outline = outline.ToArray(), eyes = eyes.ToArray(), tusks = tusks.ToArray(),
                glints = new[] { new[] { Round(C - 2.7 - 0.5) - W / 2.0, 12.0 - H }, new[] { Round(C + 2.7 - 0.5) - W / 2.0, 12.0 - H } },
            };
        }

        // rune glyphs: little strokes in (along, across) units, set round the circle between its two rings
        static readonly double[][][][] GLYPHS =
        {
            new[] { new[] { new[] { 0.0, -2 }, new[] { 0.0, 2 } }, new[] { new[] { -1.5, -1 }, new[] { 1.5, 1 } } },
            new[] { new[] { new[] { -1.5, 2 }, new[] { 0.0, -2 }, new[] { 1.5, 2 } }, new[] { new[] { -0.8, 0.4 }, new[] { 0.8, 0.4 } } },
            new[] { new[] { new[] { -1.5, -2 }, new[] { -1.5, 2 }, new[] { 1.5, 2 } }, new[] { new[] { 0.0, -2 }, new[] { 1.5, -0.5 } } },
            new[] { new[] { new[] { 0.0, -2 }, new[] { 0.0, 2 } }, new[] { new[] { 0.0, -2 }, new[] { 1.6, -0.6 }, new[] { 0.0, 0.6 } } },
            new[] { new[] { new[] { -1.5, -2 }, new[] { 1.5, 2 } }, new[] { new[] { 1.5, -2 }, new[] { -1.5, 2 } }, new[] { new[] { -1.5, 0.0 }, new[] { 1.5, 0 } } },
            new[] { new[] { new[] { -1.4, -2 }, new[] { 1.4, -2 }, new[] { -1.4, 2 }, new[] { 1.4, 2 } } },
        };

        sealed class Rune
        {
            public double a;
            public double[][][] g;
        }

        readonly uint[] C, SH, HR, ST, RUB;
        readonly uint DUST;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits rocks;
        readonly Lights lights;
        readonly Rune[] runes;
        readonly List<List<double[]>> cracks = new List<List<double[]>>();
        readonly HashSet<string> done = new HashSet<string>();
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t;

        SummonRise(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            SH = Fit(o.Ramp("body"), 5);
            HR = Fit(o.Ramp("horn"), 5);
            ST = Fit(o.Ramp("stone"), 5);
            DUST = Mix(ST[2], Hex("#7a6c78"), 0.45);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            // rubble: the floor's own stone, its faces lit warm by the fire underneath
            RUB = new[] { ST[0], ST[1], Mix(ST[2], C[2], 0.3), Mix(ST[3], C[4], 0.3), Mix(ST[4], C[5], 0.35) };
            rocks = new Bits(x => 0, e);
            lights = new Lights(e);
            runes = new Rune[12];
            for (int i = 0; i < 12; i++) runes[i] = new Rune { a = (double)i / 12 * TAU + 0.13, g = GLYPHS[(i * 5 + 2) % GLYPHS.Length] };
            // cracks radiating from the middle of the circle, on the floor plane
            for (int i = 0; i < 7; i++)
            {
                double a = (double)i / 7 * TAU + R.Range(-0.25, 0.25), r = 3;
                var pts = new List<double[]> { new[] { 0.0, 0 } };
                double len = R.Range(R1 * 0.6, R1 * 1.05);
                while (r < len)
                {
                    r += R.Range(2, 4);
                    a += R.Range(-0.3, 0.3);
                    pts.Add(new[] { Px.Cos(a) * r, Px.Sin(a) * r * SY });
                }
                cracks.Add(pts);
            }
        }

        // the rise: two heaves with a sag between, a little overshoot at the top
        static double Rise(double t)
        {
            if (t < BREAK) return 0;
            double a = Ease.OutCubic(Clamp((t - BREAK) / 0.4)) * 0.56, sag = 0.04 * Px.Sin(Math.PI * Clamp((t - BREAK - 0.42) / 0.3));
            double b = Ease.OutBack(Clamp((t - BREAK - 0.62) / 0.45), 1.4) * 0.44;
            return a - sag + b;
        }

        // the circle's fire: lights up, flares at the break and when the eyes open, dies in the fade
        static double Heat(double t) => Smooth(0, IGN, t) * (1 - Smooth(FADE, FADE + 0.8, t)) * (1 - 0.25 * Smooth(EYES + 0.2, EYES + 0.9, t)) *
            (0.85 + 0.15 * Noise(t * 9, 3) + 0.5 * Exp(-Px.Pow(t - BREAK, 2) / 0.02) + 0.3 * Exp(-Px.Pow(t - EYES, 2) / 0.01));

        // a life that ends before the effect does
        double Left(double a, double b) => Math.Max(0.05, Math.Min(R.Range(a, b), DURATION - 0.1 - t));

        bool Once(string k, double at)
        {
            if (done.Contains(k) || t < at) return false;
            done.Add(k);
            return true;
        }

        double[][] TopEdge()
        {
            double off = BEAST.h * (1 - Rise(t));
            var pts = new List<double[]>();
            for (int x = 0; x < BEAST.w; x++)
                for (int y = 0; y < BEAST.h; y++)
                    if (BEAST.part[y * BEAST.w + x] != 0)
                    {
                        double ly = y - BEAST.h + off;
                        if (ly < 0) pts.Add(new[] { x - BEAST.w / 2.0, ly });
                        break;
                    }
            return pts.ToArray();
        }

        Bits.Bit Rock(double x, double vx, double vy, double drag, double bounce, double life, double a, double va, double s) =>
            new Bits.Bit { kind = B.Chunk, x = x, y = -2, vx = vx, vy = vy, g = 1, drag = drag, bounce = bounce, life = life, a = a, va = va, s = s, ramp = RUB };

        public override void Update(double dt)
        {
            t += dt;
            double h = Heat(t);
            // the rings smoulder: embers and thin smoke off the lines, sparks at the burning front
            if (t < IGN)
            {
                double ph = (t / IGN) * Math.PI;
                foreach (int s in new[] { -1, 1 })
                {
                    double a = Math.PI / 2 + s * ph;
                    double y = Px.Sin(a) * R0 * SY;
                    if (R.Chance(0.5))
                    {
                        if (y < -0.5) ps.Spawn(P.Spark, Px.Cos(a) * R0, y, R.Range(-40, 40), -R.Range(40, 130), R.Range(0.2, 0.45));
                        else ps.Spawn(P.Ember, Px.Cos(a) * R0, y, R.Range(-20, 20), -R.Range(30, 70), R.Range(0.3, 0.6), C[6], 1.4);
                    }
                }
            }
            if (t > 0.2 && t < FADE + 0.6 && R.Chance(0.2 * h))
            {
                double a = R.Range(0, TAU), r = R.Pick(new[] { R0, R1, R0 });
                ps.Spawn(P.Ember, Px.Cos(a) * r, Px.Sin(a) * r * SY, R.Range(-10, 10), -R.Range(20, 50), Left(0.8, 1.6), C[5], R.Range(1, 1.8));
            }
            if (t > 0.15 && R.Chance(0.4 * Math.Min(1, h)))
            {
                double a = R.Range(0, TAU), r = R.Chance(0.7) ? R0 : R1;
                if (t < IGN ? Math.Abs(((a - Math.PI / 2 + Math.PI * 3) % TAU) - Math.PI) < (t / IGN) * Math.PI : true)
                    ps.Spawn(P.Fire, Px.Cos(a) * r, Px.Sin(a) * r * SY - 0.5, R.Range(-4, 4), -R.Range(6, 16), R.Range(0.18, 0.4), 0, R.Range(0.8, 1.6) * Math.Min(1, h), 0, R.Chance(0.25) ? PF.Smokes : PF.None);
            }
            if (t > 0.3 && t < FADE + 0.4 && R.Chance(0.05 * h))
            {
                double a = R.Range(0, TAU);
                ps.Spawn(P.Smoke, Px.Cos(a) * R0 * 0.9, Px.Sin(a) * R0 * SY - 1, R.Range(-4, 4), -R.Range(12, 24), Left(1.4, 2.2), 0, R.Range(2.2, 3.4));
            }
            if (Once("ign", IGN)) lights.Add(0, -4, 60, C[4], 0.55, 0.5);
            if (Once("runes", RUNES)) lights.Add(0, -4, 50, C[5], 0.5, 0.35);
            // the rumble: grit jumps on the floor, dust stirs
            if (t > RUNES && t < BREAK && R.Chance(0.35)) ps.Spawn(P.Grit, R.Range(-R1, R1), -0.5, R.Range(-20, 20), -R.Range(40, 120), R.Range(0.5, 0.9), R.Pick(new[] { ST[2], ST[3], ST[4] }), 1);
            if (t > RUNES && t < BREAK && R.Chance(0.1)) ps.Spawn(P.Dust, R.Range(-R1, R1), -1, R.Range(-12, 12), -R.Range(4, 12), R.Range(0.4, 0.7), DUST, R.Range(1.5, 2.5));
            // the break: the floor bursts open
            if (Once("break", BREAK))
            {
                lights.Add(0, -18, 64, C[4], 0.9, 0.6);
                lights.Add(0, -8, 26, C[6], 0.8, 0.14);
                for (int i = 0; i < 18; i++)
                {
                    double x = R.Range(-14, 14), vx = R.Range(-75, 75), vy = -R.Range(170, 320), life = R.Range(1.6, 2.4), a = R.Range(0, TAU), va = R.Range(-14, 14), s = R.Range(1.3, 3);
                    rocks.Add(Rock(x, vx, vy, 0.7, 0.32, life, a, va, s));
                }
                uint[] grit = { ST[2], ST[3], ST[3], ST[4] };
                for (int i = 0; i < 60; i++)
                {
                    double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.7, sp = R.Range(80, 230) * (1 - 0.4 * Math.Abs(u));
                    ps.Spawn(P.Grit, u * 10, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.8), R.Pick(grit), R.Chance(0.4) ? 2 : 1);
                }
                for (int i = 0; i < 12; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(2, 14), -R.Range(1, 5), s * R.Range(35, 100), -R.Range(4, 24), R.Range(1.2, 2), DUST, R.Range(3.5, 6), R.Range(0, 0.1), PF.Big);
                }
                for (int i = 0; i < 4; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(10, 18), -R.Range(2, 8), s * R.Range(10, 30), -R.Range(40, 90), R.Range(1.4, 2.2), DUST, R.Range(4, 6.5), R.Range(0, 0.12), PF.Big);
                }
                for (int i = 0; i < 24; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(150, 360);
                    ps.Spawn(P.Spark, R.Range(-6, 6), -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.65));
                }
            }
            // the heave: earth slides off its shoulders, dust rolls out round its feet
            double rv = Rise(t) - Rise(t - dt);
            if (t > BREAK && t < BREAK + 1.2 && rv > 0)
            {
                if (R.Chance(Math.Min(0.9, rv * 140)))
                {
                    double[][] edge = TopEdge();
                    if (edge.Length > 0)
                    {
                        double[] pt = R.Pick(edge);
                        ps.Spawn(P.Grit, pt[0], pt[1], R.Range(-25, 25), -R.Range(10, 60), R.Range(0.8, 1.4), R.Pick(new[] { ST[2], ST[3], ST[4] }), R.Chance(0.3) ? 2 : 1);
                    }
                }
                if (R.Chance(Math.Min(0.8, rv * 70)))
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(10, 22), -R.Range(1, 4), s * R.Range(30, 80), -R.Range(2, 12), R.Range(1, 1.8), DUST, R.Range(3, 5.5), 0, PF.Big);
                }
            }
            if (Once("heave2", BREAK + 0.64))
            {
                lights.Add(0, -10, 56, C[4], 0.6, 0.4);
                for (int i = 0; i < 8; i++)
                {
                    double x = R.Sign() * R.Range(10, 22), vx = R.Sign() * R.Range(25, 70), vy = -R.Range(90, 180), life = R.Range(1.2, 1.8), a = R.Range(0, TAU), va = R.Range(-12, 12), s = R.Range(1.2, 2.4);
                    rocks.Add(Rock(x, vx, vy, 0.7, 0.3, life, a, va, s));
                }
            }
            // the eyes flare
            if (Once("eyes", EYES))
            {
                foreach (double[] gl in BEAST.glints)
                {
                    double ex = gl[0], ey = gl[1];
                    lights.Add(ex, ey, 14, C[6], 0.7, 0.3);
                    for (int i = 0; i < 5; i++) ps.Spawn(P.Spark, ex, ey, R.Range(-90, 90), R.Range(-90, 20), R.Range(0.15, 0.35));
                }
                for (int i = 0; i < 10; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Dust, s * R.Range(8, 20), -1, s * R.Range(30, 80), -R.Range(3, 10), R.Range(0.4, 0.8), DUST, R.Range(1.5, 3));
                }
            }
            if (t > FADE + 0.05 && t < FADE + 0.75 && R.Chance(0.45))
            {
                int x = R.Int(4, BEAST.w - 5), y = R.Int(2, BEAST.h - 4);
                if (BEAST.part[y * BEAST.w + x] != 0)
                {
                    double lx = x - BEAST.w / 2.0, ly = y - BEAST.h;
                    ps.Spawn(P.Smoke, lx, ly, R.Range(-6, 6), -R.Range(10, 26), Left(0.9, 1.5), SOOT, R.Range(2, 3.4));
                    if (R.Chance(0.3)) ps.Spawn(P.Ember, lx, ly, R.Range(-10, 10), -R.Range(15, 40), Left(0.6, 1.2), C[4], 1);
                }
            }
            rocks.Update(dt);
            foreach (Bits.Bit b in rocks.List)
                if (b.rest && !dusted.Contains(b))
                {
                    dusted.Add(b);
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(6, 30), -R.Range(2, 12), R.Range(0.3, 0.6), DUST, R.Range(1.2, 2.4));
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double h = Heat(t);
            if (h <= 0.01) return;
            // the warm pool on the floor
            p.Glow(0, -2, 52, C[4], 0.28 * h);
            p.Glow(0, -22, 72, C[4], 0.1 * h);
            p.Glow(0, -1, 22, C[5], 0.2 * h);
            // the cracks open under it and glow
            double ck = Smooth(RUNES, BREAK, t) * (1 - Smooth(FADE, FADE + 0.7, t));
            if (ck > 0)
                foreach (List<double[]> pts in cracks)
                    for (int i = 1; i < pts.Count; i++)
                    {
                        double u = (double)i / pts.Count;
                        if (u > ck * 1.1) break;
                        double cool = Smooth(BREAK + 0.1, BREAK + 1.1, t), ci = Round(6 - u * 2 - cool * 2.6);
                        p.Line(pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1], C[(int)Clamp(ci, 2, 6)], 1);
                    }
            // the rings: burnt in from the front round to the back, flickering
            const int n = 220;
            double prog = Clamp(t / IGN) * Math.PI;
            for (int j = 0; j < n; j++)
            {
                double a = (double)j / n * TAU, d = Math.Abs(((a - Math.PI / 2 + Math.PI * 3) % TAU) - Math.PI);
                if (d > prog) continue;
                bool fresh = prog - d < 0.25 && t < IGN + 0.1;
                double f = Noise(a * 6 + t * 5, 4) * 0.5 + 0.5, k = h * (0.7 + 0.5 * f);
                double ca = Px.Cos(a), sa = Px.Sin(a);
                p.Dot(ca * R0, sa * R0 * SY, fresh ? WHITE : k > 0.95 ? C[6] : k > 0.6 ? C[5] : k > 0.35 ? C[4] : C[3]);
                p.Dot(ca * (R0 + 2.2), sa * (R0 + 2.2) * SY, k > 0.8 ? C[4] : C[3], 0.9);
                if (t > RUNES - 0.15) p.Dot(ca * R1, sa * R1 * SY, k > 0.9 ? C[5] : k > 0.5 ? C[4] : C[3], Smooth(RUNES - 0.15, RUNES + 0.05, t));
                if (j % 11 == 0) p.Glow(ca * R0, sa * R0 * SY, 9, C[4], 0.22 * k);
            }
            // the runes between the rings
            double rk = Smooth(RUNES - 0.1, RUNES + 0.15, t);
            if (rk > 0)
                foreach (Rune r in runes)
                {
                    double k = h * rk * (0.75 + 0.35 * Noise(t * 4 + r.a * 5, 8)), mid = (R0 + R1) / 2 + 0.5;
                    double ta0 = -Px.Sin(r.a), ta1 = Px.Cos(r.a), na0 = Px.Cos(r.a), na1 = Px.Sin(r.a);
                    foreach (double[][] stroke in r.g)
                        for (int i = 1; i < stroke.Length; i++)
                        {
                            double u0 = stroke[i - 1][0], v0 = stroke[i - 1][1], u1 = stroke[i][0], v1 = stroke[i][1];
                            double x0 = na0 * mid + ta0 * u0 * 1.1 + na0 * v0, y0 = (na1 * mid + ta1 * u0 * 1.1 + na1 * v0) * SY;
                            double x1 = na0 * mid + ta0 * u1 * 1.1 + na0 * v1, y1 = (na1 * mid + ta1 * u1 * 1.1 + na1 * v1) * SY;
                            p.Line(x0, y0, x1, y1, k > 0.9 ? C[6] : k > 0.55 ? C[5] : C[4], Math.Min(1, k * 1.5));
                        }
                }
            // the hole the brute climbs out of: a glowing gash
            double hole = Smooth(BREAK - 0.05, BREAK + 0.15, t) * (1 - Smooth(BREAK + 0.95, BREAK + 1.5, t));
            if (hole > 0)
            {
                double hw = 15 * hole;
                p.Shade(-hw - 1, -1, hw + 1, 4, (x, y, o) =>
                {
                    double d = Hypot(x / hw, (y - 0.5) / 3.2);
                    if (d > 1) return 0;
                    o.c = d > 0.86 ? C[4] : d > 0.62 ? C[1] : C[0];
                    return 0;
                });
                p.Glow(0, 0, 24, C[5], 0.3 * hole * h);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            double r = Rise(t), off = BEAST.h * (1 - r), h = Heat(t), fade = Smooth(FADE + 0.1, FADE + 0.8, t);
            if (r > 0)
            {
                int W = BEAST.w, H = BEAST.h;
                double x0 = -W / 2.0, oy = off - H, k = Math.Min(1, h);
                bool Gone(double x, double y) => fade > 0 && fade > 0.06 + 0.86 * (0.55 * (1 - y / H) + 0.45 * Rand(x, y, 3));
                // a dark edge all round, so the bulk reads against the night
                foreach (int[] q in BEAST.outline)
                    if (q[1] + oy < 0 && !Gone(q[0], Math.Min(H - 1, Math.Max(0, q[1]))))
                        p.Px(x0 + q[0], q[1] + oy, SH[0]);
                for (int y = 0; y < H; y++)
                {
                    double ly = y + oy;
                    if (ly >= 0) break;
                    // the fire below lights the undersides: rims strongest by the floor, broad planes higher up
                    double upRim = Exp(ly / 17) * k, upPlane = Exp(ly / 19) * k;
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x, pt = BEAST.part[i];
                        if (pt == 0 || Gone(x, y)) continue;
                        uint[] ramp = pt == 2 ? HR : SH;
                        double a = BEAST.amb[i];
                        int si = BEAST.crease[i] != 0 ? 0 : a > 0.74 ? 4 : a > 0.56 ? 3 : a > 0.36 ? 2 : 1;
                        if (pt == 2 && si > 0) si = Math.Min(4, si + (y < 3 ? 2 : 1)); // horns are paler, tips palest
                        // rims can run hot; the broad planes only ever warm up to deep red
                        double Lr = BEAST.rim[i] * upRim, Lp = BEAST.down[i] * upPlane * 0.6;
                        uint c = ramp[si];
                        if (Lr > 0.7) c = C[5];
                        else if (Lr > 0.48) c = C[4];
                        else if (Lr > 0.3 || Lp > 0.42) c = C[3];
                        else if (Lr > 0.17 || Lp > 0.26) c = Mix(C[2], ramp[si], 0.3);
                        else if (Lr > 0.08 || Lp > 0.12) c = Mix(ramp[si], C[1], 0.6);
                        p.Px(x0 + x, ly, c);
                    }
                }
                // tusks catch the light; the eye sockets smoulder until the eyes open
                foreach (int[] q in BEAST.tusks)
                    if (q[1] + oy < 0 && !Gone(q[0], q[1]))
                        p.Px(x0 + q[0], q[1] + oy, Mix(HR[4], C[4], 0.35 * k));
                foreach (int[] q in BEAST.eyes)
                    if (q[1] + oy < 0 && !Gone(q[0], q[1]))
                        p.Px(x0 + q[0], q[1] + oy, t > EYES - 0.05 ? (t < EYES + 0.1 ? WHITE : C[6]) : t > BREAK + 0.5 ? C[2] : SH[0]);
                // the eyes: a flare as they open, then a steady burn that outlasts the body
                if (t > EYES - 0.05)
                {
                    double ke = (1 - Smooth(FADE + 0.6, FADE + 1.0, t)) * (0.85 + 0.15 * Px.Sin(t * 13));
                    foreach (double[] gl in BEAST.glints)
                    {
                        double ex = gl[0], ey = gl[1] + off;
                        if (ey >= 0) continue;
                        p.Glow(ex + 0.5, ey, 6, C[5], 0.5 * ke);
                        double g = (t - EYES) / 0.3;
                        if (g > 0 && g < 1)
                        {
                            double L = 9 * Px.Pow(1 - g, 1.5);
                            p.Line(ex - L, ey, ex + L, ey, u => Math.Abs(u - 0.5) < 0.18 ? WHITE : Math.Abs(u - 0.5) < 0.35 ? C[6] : C[5]);
                            if (g < 0.4) p.Line(ex, ey - 2 * (1 - g), ex, ey + 2 * (1 - g), C[6]);
                        }
                        else if (fade > 0.6 && ke > 0.05) p.Px(ex, ey, C[6]);
                    }
                }
                // the light that rakes up its front
                p.Glow(0, -6, 30, C[4], 0.25 * h * r);
            }
            rocks.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
