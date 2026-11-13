// The crack-and-burst recipe (cracks walking out from a point, a white frame, the ice flung apart as shards
// with ice dust and glints) is adapted from Sprite Fusion's open-source ice
// (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.IceLib;

namespace Pixelfx
{
    public sealed class Freeze : Effect
    {
        const double DURATION = 4;

        static readonly Pose FEAR = new Pose { ["turn"] = 0.9, ["lean"] = -2, ["crouch"] = 2, ["fx"] = -8, ["fy"] = -31, ["bx"] = 9, ["by"] = -31, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose FREE = new Pose { ["turn"] = 1, ["crouch"] = 5, ["fx"] = -12, ["fy"] = -18, ["bx"] = 12, ["by"] = -18, ["ffx"] = -5, ["fbx"] = 6 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.2, Poses.READY),
                new PoseKey(0.05, Poses.READY),
                new PoseKey(0.16, FEAR, "snap"),
                new PoseKey(0.3, new Pose(FEAR) { ["lean"] = -1, ["fx"] = -10, ["fy"] = -34 }),
                new PoseKey(0.45, new Pose(FEAR) { ["lean"] = -3, ["bx"] = 11, ["by"] = -34, ["fx"] = -7, ["fy"] = -29 }),
                new PoseKey(0.62, FEAR),
                new PoseKey(2.52, FEAR),
                new PoseKey(2.54, FREE, "snap"),
                new PoseKey(2.9, new Pose(FREE) { ["crouch"] = 4 }),
                new PoseKey(3.1, new Pose(FREE) { ["crouch"] = 3, ["lean"] = 1 }),
                new PoseKey(3.3, new Pose(FREE) { ["crouch"] = 3, ["lean"] = -1 }),
                new PoseKey(4, Poses.READY),
            },
            Hits = new[] { new HitCue { T = 0.62, Stop = 0.03, Shake = 0.08, Amp = 1 }, new HitCue { T = 2.52, Stop = 0.05, Shake = 0.2, Amp = 2 } },
        };

        public static readonly Module Module = new Module("freeze", "pixelfx/fx/ice/freeze", 176, 128, 88, 108, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["seed"] = 5,
            },
            (o, e) => new Freeze(o, e), act: Act, actor: 2)
        {
            Title = "Freeze",
            Desc = "Frost races in and climbs the character, turning it to ice; a faceted block crystallizes round it pane by pane, glints, cracks and bursts, and the character thaws free.",
            Tags = new[] { "ice", "freeze", "frozen", "status", "crowd-control", "one-shot" },
            Bg = "ground",
        };

        const double SNAP = 0.16; // frost lines race in along the floor to the feet
        const double CLIMB0 = 0.14, CLIMB1 = 0.62; // and climb the character
        const double SEAL = 0.6; // the block crystallizes, pane by pane
        static readonly double[] SWEEPS = { 1.12, 1.82 }; // light slides across the faces while it holds
        const double CRACK = 2.2, CRACK2 = 2.34, FLASH = 2.46, BURST = 2.52, THAW = 0.36;
        const double MELT0 = 2.95, MELT1 = 3.7; // the floor frost goes
        const double REACH = 38;
        static readonly double[] CORE = { 0, -21 };

        // the block: a crown of three crystal points over straight sides, eleven corners round the character and
        // eight inside, cut into thirteen panes. Each pane: its corners, how much light it catches (0 shadow .. 1 lit)
        // and when it crystallizes after SEAL (from the floor up).
        static readonly string[] VNAMES = { "l0", "l1", "l2", "p1", "n1", "p2", "n2", "p3", "r2", "r1", "r0", "i0", "i1", "i2", "i3", "i4", "i5", "i6", "i7" };
        static readonly double[][] VPTS =
        {
            new double[] { -15, 0 }, new double[] { -17, -10 }, new double[] { -17, -31 }, new double[] { -12, -45 }, new double[] { -6, -42 }, new double[] { 0, -52 },
            new double[] { 6, -43 }, new double[] { 12, -47 }, new double[] { 17, -30 }, new double[] { 16, -9 }, new double[] { 14, 0 },
            new double[] { -6, -36 }, new double[] { 6, -35 }, new double[] { -9, -21 }, new double[] { 3, -25 }, new double[] { 9, -13 }, new double[] { -5, -8 }, new double[] { 2, 0 }, new double[] { 0, -40 },
        };
        static double[] V(string name) => VPTS[Array.IndexOf(VNAMES, name)];
        static double[][] Pts(string names) => names.Split(' ').Select(V).ToArray();

        static readonly (string k, double lit, double at)[] FACET_DEFS =
        {
            ("l0 l1 i5 i6", 0.62, 0), ("i5 i4 i6", 0.42, 0.03), ("i4 r2 r1 r0 i6", 0.2, 0.05), ("l1 l2 i2 i5", 0.74, 0.08), ("i2 i3 i4 i5", 0.5, 0.1),
            ("i1 r2 i4 i3", 0.28, 0.13), ("l2 i0 i2", 0.7, 0.15), ("i0 i1 i3 i2", 0.58, 0.18), ("n1 i7 n2 i1 i0", 0.52, 0.2), ("i1 n2 p3 r2", 0.4, 0.23),
            ("l2 p1 n1 i0", 0.88, 0.25), ("p2 n2 i7", 0.66, 0.28), ("n1 p2 i7", 1, 0.31),
        };
        static readonly double[][] OUTLINE = Pts("l0 l1 l2 p1 n1 p2 n2 p3 r2 r1 r0");
        const int X0 = -18, X1 = 18, Y0 = -53, Y1 = 0, BW = X1 - X0;

        sealed class Facet
        {
            public double[][] pts;
            public double lit, at, alpha, d, len, g;
            public int i;
            public (double x, double y, double area) c;
            public uint tint;
        }

        sealed class Edge
        {
            public double[] a, b;
            public List<Facet> fs = new List<Facet>();
            public bool outer, floor;
            public double lit, at;
        }

        sealed class Feather
        {
            public double x, y, dx, dy, L, at;
        }

        static readonly int[][] Steps = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } };
        static readonly int[] Sides = { -1, 1 };

        // frost on the floor: a crust on the floor line, rime ferns standing up from it, a cold bloom under the surface
        static void FloorFrost(Painter p, uint[] C, double t, double outA)
        {
            for (int x = (int)-REACH; x <= REACH; x++)
            {
                double d = Math.Abs(x + 0.5) / REACH, arrive = SNAP * (1 - d), age = t - arrive;
                if (age < 0) continue;
                double a = outA * Clamp((1.12 - d) / 0.2), g = Smooth(0, 0.4, age), h = Rand(x, 31), s = x < 0 ? -1 : 1;
                if (a <= 0) continue;
                p.Dot(x, 0, age < 0.05 || h < 0.3 ? WHITE : C[6], a);
                if (h < 0.55)
                {
                    double L = Round(g * (1 + Rand(x, 7) * 3.4) * (1 - d * 0.55));
                    for (int j = 1; j <= L; j++) p.Dot(x, -j, j == L ? WHITE : C[5], a);
                    if (L >= 2) p.Dot(x + s, -L + 1, C[6], a);
                    if (L >= 4) p.Dot(x - s, -L + 2, C[5], a);
                }
                double D = g * (2 + Rand(x, 11) * 4) * (1 - d * 0.5);
                for (int j = 1; j <= D; j++) p.Dot(x, j, Rand(x * 13 + j, 5) < 0.07 ? WHITE : j < 2 ? C[5] : j < 4 ? C[4] : C[3], a * (1.05 - j / (D + 1)));
            }
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Actor A;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Panes panes;
        readonly int W, H;
        readonly uint[] ice;
        readonly float[] reach;
        readonly int[] order;
        readonly List<Feather> feathers = new List<Feather>();
        readonly int[] fid;
        readonly Facet[] FACETS;
        readonly List<Edge> EDGES = new List<Edge>();
        readonly List<double[][]> cracks, cracks2;
        double t;
        int formed, struck;
        bool sealedUp, burst, white;

        Freeze(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            A = o.Actor;
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            panes = new Panes(floor,
                onLand: (q, x, y, v) =>
                {
                    // a pane that lands breaks into splinters that skip along the floor, with a puff of ice dust and a glint
                    for (int i = 0; i < 3; i++)
                        bits.Add(new Bits.Bit { kind = B.Shard, x = x + R.Range(-2, 2), y = y - 1, vx = q.vx * 0.6 + R.Range(-50, 50), vy = -R.Range(30, 100), g = 0.8, drag = 0.6, bounce = 0.35, life = R.Range(0.4, 0.8), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1.2, 2.4), c = C[R.Int(5, 7)] });
                    ps.Spawn(P.Vapour, x, y - 1, R.Range(-25, 25), -R.Range(2, 10), R.Range(0.4, 0.7), C[6], R.Range(1.4, 2.4));
                    if (R.Chance(0.4)) ps.Spawn(P.Star, x, y - 2, 0, -6, R.Range(0.2, 0.4), WHITE, R.Range(0.8, 1.6));
                },
                emitter: e);

            // ---- the character: each pixel's frozen color and when the frost reaches it. A randomized flood up from the
            // feet, quicker round the outline, so the front creeps up the edges first in ragged fingers.
            W = A.W;
            H = A.H;
            int N = W * H;
            var dist = new float[N];
            for (int i = 0; i < N; i++) dist[i] = float.PositiveInfinity;
            ice = new uint[N];
            var rim = new byte[N];
            var open = new List<int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!On(x, y)) continue;
                    int i = y * W + x;
                    rim[i] = (byte)(!On(x - 1, y) || !On(x + 1, y) || !On(x, y - 1) ? 1 : 0);
                    bool up = !On(x - 1, y) || !On(x, y - 1);
                    ice[i] = Mix(up ? C[Math.Min(7, Array.IndexOf(C, IceOf(A.Pixels[i], C)) + 1)] : IceOf(A.Pixels[i], C), C[6], 0.2);
                    if (y == H - 1 || !On(x, y + 1))
                        if (y >= H - 4)
                        {
                            dist[i] = (float)R.Range(0, 1.5);
                            open.Add(i);
                        }
                }
            var done = new byte[N];
            while (open.Count > 0)
            {
                int bi = 0;
                for (int j = 1; j < open.Count; j++)
                    if (dist[open[j]] < dist[open[bi]])
                        bi = j;
                int i = open[bi];
                open[bi] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                if (done[i] != 0) continue;
                done[i] = 1;
                int x = i % W, y = i / W;
                foreach (int[] st in Steps)
                {
                    int dx = st[0], dy = st[1], nx = x + dx, ny = y + dy;
                    if (!On(nx, ny)) continue;
                    int j = ny * W + nx;
                    double w = (rim[j] != 0 ? 0.55 : 1) * (dy < 0 ? 1 : 1.3) + R.Range(0, 1.5);
                    if ((double)dist[i] + w < dist[j])
                    {
                        dist[j] = (float)((double)dist[i] + w);
                        open.Add(j);
                    }
                }
            }
            double maxD = 0;
            for (int i = 0; i < N; i++)
                if (dist[i] < float.PositiveInfinity)
                    maxD = Math.Max(maxD, dist[i]);
            reach = new float[N];
            var orderList = new List<int>();
            for (int i = 0; i < N; i++)
                if (dist[i] < float.PositiveInfinity)
                {
                    reach[i] = (float)(CLIMB0 + (CLIMB1 - CLIMB0) * Px.Pow(dist[i] / maxD, 0.85));
                    orderList.Add(i);
                }
            order = orderList.OrderBy(i => (double)reach[i]).ToArray();
            // rime feathers: little ticks of frost standing out from the outline once the frost has passed
            foreach (int i in order)
            {
                int x = i % W, y = i / W;
                if (rim[i] == 0 || Rand(i, o.Seed, 3) > 0.42 || y > H - 3) continue;
                int dx = !On(x - 1, y) ? -1 : !On(x + 1, y) ? 1 : Rand(i, 9) < 0.5 ? -1 : 1, dy = !On(x, y - 1) ? -1 : Rand(i, 4) < 0.5 ? -1 : 0;
                feathers.Add(new Feather { x = A.X + x + dx, y = A.Y + y + dy, dx = dx, dy = dy, L = 1 + Math.Floor(Rand(i, 7) * 2.6), at = reach[i] + 0.04 });
            }

            // ---- the block: which pane every pixel belongs to, the panes' looks, the edges they share
            FACETS = FACET_DEFS.Select((f, i) => new Facet { pts = Pts(f.k), lit = f.lit, at = SEAL + f.at, i = i }).ToArray();
            fid = new int[BW * (Y1 - Y0)];
            for (int i = 0; i < fid.Length; i++) fid[i] = -1;
            for (int y = Y0; y < Y1; y++)
                for (int x = X0; x < X1; x++)
                {
                    if (!InBlock(x + 0.5, y + 0.5)) continue;
                    int f = Array.FindIndex(FACETS, F => InPoly(F.pts, x + 0.5, y + 0.5));
                    if (f < 0)
                    {
                        f = 0;
                        for (int k = 0; k < FACETS.Length; k++)
                        {
                            var ck = Centroid(FACETS[k].pts);
                            var cb = Centroid(FACETS[f].pts);
                            f = Hypot(ck.x - x, ck.y - y) < Hypot(cb.x - x, cb.y - y) ? k : f;
                        }
                    }
                    fid[(y - Y0) * BW + (x - X0)] = f;
                }
            foreach (Facet F in FACETS)
            {
                F.c = Centroid(F.pts);
                F.tint = C[3 + RoundInt(F.lit * 3)];
                F.alpha = 0.2 + 0.24 * F.lit;
                F.d = Clamp((F.c.x + 0.55 * F.c.y + 36) / 46);
                double len = 0;
                for (int k = 0; k < F.pts.Length; k++)
                {
                    double[] q = F.pts[k], nq = F.pts[(k + 1) % F.pts.Length];
                    len = len + Hypot(nq[0] - q[0], nq[1] - q[1]);
                }
                F.len = len;
            }
            // every edge once: its ends, the panes it borders, whether it's on the outline and how it faces the light
            foreach (Facet F in FACETS)
                for (int k = 0; k < F.pts.Length; k++)
                {
                    double[] a = F.pts[k], b = F.pts[(k + 1) % F.pts.Length];
                    Edge ed = EDGES.Find(q => (q.a == a && q.b == b) || (q.a == b && q.b == a));
                    if (ed == null) EDGES.Add(ed = new Edge { a = a, b = b });
                    ed.fs.Add(F);
                }
            foreach (Edge ed in EDGES)
            {
                double x0 = ed.a[0], y0 = ed.a[1], x1 = ed.b[0], y1 = ed.b[1];
                var c = ed.fs[0].c;
                double nx = y1 - y0, ny = x0 - x1;
                if (nx * ((x0 + x1) / 2 - c.x) + ny * ((y0 + y1) / 2 - c.y) < 0)
                {
                    nx = -nx;
                    ny = -ny;
                }
                ed.outer = ed.fs.Count == 1;
                ed.lit = (-nx * 0.55 - ny * 0.84) / Hypot(nx, ny);
                ed.floor = y0 == 0 && y1 == 0;
                ed.at = ed.fs.Min(F => F.at);
            }
            cracks = Fractures(R, InBlock, CORE[0], CORE[1], 3, len: 9, a0: -2.2, spread: 3.6);
            cracks2 = Fractures(R, InBlock, CORE[0] + 1, CORE[1] - 2, 6, len: 16, fork: 0.22);
        }

        bool On(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && A.Mask.Data[y * W + x] > 0;
        static bool InBlock(double x, double y) => InPoly(OUTLINE, x, y);
        int At(int x, int y) => x < X0 || x >= X1 || y < Y0 || y >= Y1 ? -1 : fid[(y - Y0) * BW + (x - X0)];
        int Jolt() => t >= CRACK && t < CRACK + 0.06 ? 1 : t >= CRACK2 && t < FLASH ? (Math.Floor(t * 40) % 2 != 0 ? 1 : -1) : 0;

        // the frozen character: each pixel turns as the frost reaches it, white at the front; after the burst the
        // ice runs off it from the top down
        void DrawCharacter(Painter p)
        {
            double thaw = t >= BURST ? Ease.InQuad(Seg(t, BURST, BURST + THAW)) * (H + 2) - 2 : -1;
            foreach (int i in order)
            {
                double age = t - reach[i];
                if (age < 0) break;
                int x = i % W, y = i / W;
                if (y < thaw) continue;
                p.Px(A.X + x, A.Y + y, y < thaw + 1.2 && t >= BURST ? C[7] : age < 0.016 ? WHITE : age < 0.045 ? C[6] : ice[i]);
                if (age < 0.03) p.Light(A.X + x, A.Y + y, C[6], 0.3);
            }
            if (t >= BURST) return;
            foreach (Feather f in feathers)
            {
                double g = Clamp((t - f.at) / 0.08);
                if (g <= 0) continue;
                double L = Round(f.L * g);
                for (int j = 0; j < L; j++) p.Px(f.x + f.dx * j, f.y + f.dy * j, j == L - 1 ? WHITE : C[6]);
            }
        }

        // the block: panes fill in as they crystallize (edges traced in white, a white flash, then glassy), a light
        // sweep that breaks at every facet, cracks, the white frame before it bursts
        void DrawBlock(Painter p)
        {
            if (t >= BURST || t < SEAL - 0.08) return;
            int jx = Jolt();
            bool wh = t >= FLASH;
            // light slides across the block: each facet flashes whole as the light passes it, the lit ones brightest
            foreach (Facet F in FACETS)
            {
                double g = 0;
                foreach (double s0 in SWEEPS) g = Math.Max(g, Px.Sin(Math.PI * Clamp((t - s0 - F.d * 0.32) / 0.13)));
                F.g = g * (0.35 + 0.65 * F.lit);
            }
            for (int y = Y0; y < Y1; y++)
                for (int x = X0; x < X1; x++)
                {
                    int f = At(x, y);
                    if (f < 0) continue;
                    Facet F = FACETS[f];
                    double age = t - F.at;
                    if (age < 0) continue;
                    if (wh)
                    {
                        p.Px(x + jx, y, WHITE);
                        continue;
                    }
                    if (age < 0.03)
                    {
                        p.Blend(x + jx, y, C[7], 0.55);
                        continue;
                    }
                    double fresh = 1 - Clamp((age - 0.03) / 0.1), g = F.g;
                    p.Blend(x + jx, y, fresh > 0.5 || g > 0.5 ? C[6] : F.tint, F.alpha + 0.25 * fresh + 0.15 * g);
                    if (g > 0.05) p.Light(x + jx, y, C[5], 0.42 * g);
                }
            // edges: the outline lit where it faces up and left, cool on the shadow side with a deep rim outside it;
            // inner edges pale; any pane that has just set rings white
            foreach (Edge ed in EDGES)
            {
                if (ed.floor || t < ed.at) continue;
                double x0 = ed.a[0], y0 = ed.a[1], x1 = ed.b[0], y1 = ed.b[1];
                bool fresh = ed.fs.Any(F => t - F.at < 0.1 || F.g > 0.6);
                if (ed.outer && ed.lit < 0 && !wh) p.Line(x0 + jx + 1, y0, x1 + jx + 1, y1, C[1]);
                if (ed.outer) p.GlowLine(x0 + jx, y0, x1 + jx, y1, 5, C[4], 0.1);
                p.Line(x0 + jx, y0, x1 + jx, y1, wh ? C[6] : fresh ? WHITE : ed.outer ? (ed.lit > 0.3 ? WHITE : ed.lit > -0.2 ? C[6] : C[4]) : (y0 + y1) / 2 < -30 ? C[6] : C[5]);
            }
            // a pane about to set: its edges traced in white from its first corner round, a bead of light at the head
            foreach (Facet F in FACETS)
            {
                double age = t - F.at;
                if (age < -0.07 || age >= 0) continue;
                double left = ((age + 0.07) / 0.07) * F.len;
                for (int k = 0; k < F.pts.Length && left > 0; k++)
                {
                    double x0 = F.pts[k][0], y0 = F.pts[k][1], x1 = F.pts[(k + 1) % F.pts.Length][0], y1 = F.pts[(k + 1) % F.pts.Length][1], L = Hypot(x1 - x0, y1 - y0), v = Math.Min(1, left / L);
                    left -= L;
                    p.Line(x0 + jx, y0, x0 + jx + (x1 - x0) * v, y0 + (y1 - y0) * v, WHITE);
                    if (v < 1) p.Glow(x0 + jx + (x1 - x0) * v, y0 + (y1 - y0) * v, 6, C[6], 0.6);
                }
            }
            if (t >= CRACK && !wh) DrawFractures(p, cracks, (t - CRACK) * 150, C, ox: jx);
            if (t >= CRACK2 && !wh) DrawFractures(p, cracks2, (t - CRACK2) * 190, C, ox: jx);
        }

        List<double[][]> Chop(double[][] pts, int depth)
        {
            var c = Centroid(pts);
            if (c.area < 20 || depth > 2) return new List<double[][]> { pts };
            var o = new List<double[][]>();
            foreach (double[][] q in SplitPoly(pts, c.x, c.y, R.Range(0, Math.PI))) o.AddRange(Chop(q, depth + 1));
            return o;
        }

        public override void Update(double dt)
        {
            t += dt;
            // frost lines race in along the floor from both sides, vapour riding them
            if (t < SNAP)
                foreach (int s in Sides)
                {
                    double x = s * REACH * (1 - t / SNAP);
                    if (R.Chance(0.12)) ps.Spawn(P.Vapour, x, -R.Range(0, 2), -s * R.Range(40, 90), -R.Range(0, 5), R.Range(0.3, 0.5), C[6], R.Range(1.4, 2.4));
                    if (R.Chance(0.45)) ps.Spawn(P.Streak, x + s * R.Range(0, 6), -R.Range(1, 4), -s * R.Range(50, 90), 0, R.Range(0.1, 0.16), R.Chance(0.5) ? C[6] : C[5]);
                }
            // stars and motes kicked off the climbing front
            if (t > CLIMB0 && t < CLIMB1 + 0.03)
            {
                int lo = 0;
                while (lo < order.Length && reach[order[lo]] < t - 0.06) lo++;
                int hi = lo;
                while (hi < order.Length && reach[order[hi]] <= t) hi++;
                if (hi > lo && R.Chance(0.45))
                {
                    int i = order[R.Int(lo, hi - 1)];
                    ps.Spawn(P.Star, A.X + (i % W) + 0.5, A.Y + i / W, R.Range(-8, 8), -R.Range(0, 18), R.Range(0.2, 0.42), R.Chance(0.5) ? C[6] : WHITE, R.Range(0.6, 1.9));
                }
                if (hi > lo && R.Chance(0.12))
                {
                    int i = order[R.Int(lo, hi - 1)];
                    ps.Spawn(P.Flake, A.X + (i % W), A.Y + i / W, R.Range(-20, 20), -R.Range(10, 30), R.Range(0.5, 0.9), C[6], R.Range(0.5, 1.2));
                }
            }
            // each pane snaps into place with a chip of shards, a puff and a glint
            while (formed < FACETS.Length && t >= FACETS[formed].at)
            {
                Facet F = FACETS[formed++];
                var c = Centroid(F.pts);
                for (int i = 0; i < 3; i++)
                {
                    double a = Atan2(c.y - CORE[1], c.x - CORE[0]) + R.Range(-0.7, 0.7), sp = R.Range(50, 120);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = c.x, y = c.y, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - 40, g = 0.8, drag = 0.8, bounce = 0.3, life = R.Range(0.35, 0.7), a = R.Range(0, TAU), va = R.Range(-18, 18), s = R.Range(1, 2.2), c = C[R.Int(5, 7)] });
                }
                if (c.y > -14) ps.Spawn(P.Vapour, c.x, -1, Sign(c.x != 0 && !double.IsNaN(c.x) ? c.x : 1) * R.Range(10, 30), -R.Range(2, 8), R.Range(0.5, 0.8), C[6], R.Range(2.2, 3.4));
                ps.Spawn(P.Star, c.x + R.Range(-2, 2), c.y + R.Range(-2, 2), 0, -4, R.Range(0.25, 0.45), WHITE, R.Range(1.2, 2.2));
                lights.Add(c.x, c.y, 16, C[6], 0.4, 0.12);
            }
            if (!sealedUp && formed == FACETS.Length)
            {
                sealedUp = true;
                lights.Add(0, -26, 70, C[5], 0.6, 0.4);
                for (int i = 0; i < 5; i++) ps.Spawn(P.Star, R.Range(-15, 15), -R.Range(6, 44), R.Range(-6, 6), -R.Range(0, 8), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.2));
            }
            // while it holds: glints wink on the corners, cold vapour rolls off the foot, a little snow
            if (sealedUp && t < CRACK2)
            {
                if (R.Chance(0.035))
                {
                    double[] v = R.Pick(VPTS);
                    ps.Spawn(P.Star, v[0] + R.Range(-1, 1), v[1] + R.Range(-1, 1), 0, -2, R.Range(0.3, 0.6), R.Chance(0.6) ? C[6] : WHITE, R.Range(0.8, 2.2));
                }
                if (R.Chance(0.025))
                {
                    int s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(12, 18), -R.Range(1, 3), s * R.Range(8, 24), -R.Range(1, 5), R.Range(0.7, 1.1), C[6], R.Range(1.8, 3));
                }
                if (R.Chance(0.012)) ps.Spawn(P.Flake, R.Range(-30, 30), -R.Range(56, 76), R.Range(-4, 4), R.Range(4, 10), R.Range(1.2, 1.8), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.4));
            }
            // it cracks: a jolt, chips spall off, light leaks from the cracks
            if (struck == 0 && t >= CRACK)
            {
                struck = 1;
                lights.Add(CORE[0], CORE[1], 26, WHITE, 0.4, 0.1);
                for (int i = 0; i < 4; i++)
                    bits.Add(new Bits.Bit { kind = B.Shard, x = R.Range(-14, 14), y = -R.Range(10, 40), vx = R.Range(-40, 40), vy = -R.Range(10, 60), g = 0.9, drag = 0.6, bounce = 0.3, life = R.Range(0.5, 0.9), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1, 2), c = C[R.Int(5, 7)] });
            }
            if (struck == 1 && t >= CRACK2)
            {
                struck = 2;
                lights.Add(CORE[0], CORE[1], 40, C[6], 0.35, 0.16);
                for (int i = 0; i < 3; i++) ps.Spawn(P.Star, R.Range(-12, 12), -R.Range(8, 40), 0, -3, R.Range(0.15, 0.3), WHITE, R.Range(1, 2));
            }
            if (!white && t >= FLASH)
            {
                white = true;
                lights.Add(CORE[0], CORE[1], 60, WHITE, 0.9, 0.1);
            }
            // it bursts: the panes fly off tumbling, shards and ice dust, glints, a flurry, a flash of light
            if (!burst && t >= BURST)
            {
                burst = true;
                lights.Add(CORE[0], CORE[1], 64, WHITE, 0.85, 0.14);
                lights.Add(CORE[0], CORE[1] - 4, 72, C[4], 0.55, 0.6);
                foreach (Facet F in FACETS)
                {
                    foreach (double[][] part in Chop(F.pts, 0))
                    {
                        var m = Centroid(part);
                        double dx = m.x - CORE[0], dy = m.y - CORE[1], d = Hypot(dx, dy);
                        if (d == 0 || double.IsNaN(d)) d = 1;
                        double sp = R.Range(40, 85) * (1.2 - Math.Min(0.5, m.area / 120));
                        panes.Add(new Pane
                        {
                            pts = part.Select(q => new[] { q[0] - m.x, q[1] - m.y }).ToArray(),
                            x = m.x,
                            y = m.y,
                            vx = (dx / d) * sp + R.Range(-15, 15),
                            vy = (dy / d) * sp * 0.6 - R.Range(90, 190),
                            a = 0,
                            va = R.Range(-9, 9),
                            f = R.Range(-0.8, 0.8),
                            vf = R.Sign() * R.Range(5, 13),
                            lit = F.lit,
                            g = 0.85,
                            drag = 0.7,
                            brittle = 1,
                            life = R.Range(1, 1.45),
                        });
                    }
                }
                for (int i = 0; i < 50; i++)
                {
                    int x = 0, y = 0;
                    for (int k = 0; k < 12 && At(x, y) < 0; k++)
                    {
                        x = R.Int(X0, X1 - 1);
                        y = R.Int(Y0, Y1 - 1);
                    }
                    double dx = x - CORE[0], dy = y - CORE[1], d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    double sp = R.Range(50, 130);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = x, y = y, vx = (dx / d) * sp, vy = (dy / d) * sp * 0.7 - R.Range(60, 170), g = 0.85, drag = 0.9, bounce = 0.3, life = R.Range(0.8, 1.4), a = R.Range(0, TAU), va = R.Range(-22, 22), s = R.Range(1.5, 3.2), c = C[R.Int(4, 7)] });
                }
                foreach (Feather f in feathers)
                    if (R.Chance(0.5))
                        bits.Add(new Bits.Bit { kind = B.Shard, x = f.x, y = f.y, vx = f.dx * R.Range(20, 70), vy = -R.Range(20, 90), g = 0.8, drag = 0.8, bounce = 0.3, life = R.Range(0.4, 0.8), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1, 1.8), c = WHITE });
                for (int i = 0; i < 12; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(2, 16), -R.Range(1, 6), s * R.Range(30, 90), -R.Range(4, 16), R.Range(0.6, 1.1), C[6], R.Range(2.2, 4));
                }
                for (int i = 0; i < 8; i++) ps.Spawn(P.Vapour, R.Range(-14, 14), -R.Range(8, 40), R.Range(-30, 30), -R.Range(10, 30), R.Range(0.6, 1), C[6], R.Range(3, 5));
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(-Math.PI, 0), sp = R.Range(40, 130);
                    ps.Spawn(P.Flake, R.Range(-10, 10), CORE[1] + R.Range(-12, 12), Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.5), R.Chance(0.6) ? WHITE : C[6], R.Range(0.5, 1.6));
                }
                for (int i = 0; i < 7; i++) ps.Spawn(P.Star, R.Range(-26, 26), -R.Range(6, 52), R.Range(-12, 12), -R.Range(4, 16), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.4));
            }
            // glints wink on the debris as it settles
            if (burst && t < BURST + 0.9 && R.Chance(0.07))
            {
                Bits.Bit b = R.Pick(bits.List.Count > 0 ? bits.List.ToArray() : new[] { new Bits.Bit { x = 0, y = -10 } });
                ps.Spawn(P.Star, b.x, b.y - 1, 0, -3, R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.6));
            }
            // the last of the ice runs off the character
            if (burst && t < BURST + THAW && R.Chance(0.12))
            {
                double y = -H + Ease.InQuad(Seg(t, BURST, BURST + THAW)) * H;
                bits.Add(new Bits.Bit { kind = B.Drip, x = R.Range(-9, 9), y = y, vy = 10, g = 0.5, life = 0.8, c = C[5] });
            }
            ps.Update(dt);
            bits.Update(dt);
            panes.Update(dt);
            lights.Update(dt);
            EndBy(t, DURATION, ps, bits, panes);
        }

        public override void DrawBack(Painter p)
        {
            double outA = 1 - Smooth(MELT0, MELT1, t);
            if (outA > 0) FloorFrost(p, C, t, outA);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            DrawCharacter(p);
            DrawBlock(p);
            // the cold light: a pool under the ice, a glow in it that brightens as it cracks, the climbing front's light
            double hold = Smooth(CLIMB0, SEAL, t) * (1 - Smooth(BURST, BURST + 0.6, t)), crk = Smooth(CRACK, FLASH, t);
            if (hold > 0)
            {
                p.Glow(0, -2, 34, C[4], 0.22 * hold * (0.85 + 0.15 * Px.Sin(t * 3)));
                p.Glow(CORE[0], CORE[1] - 4, 40, C[3], (0.22 + 0.1 * crk) * hold);
            }
            if (t > CLIMB0 && t < CLIMB1) p.Glow(0, -38 * Ease.OutQuad(Seg(t, CLIMB0, CLIMB1)), 18, C[5], 0.28);
            // the seal: a star at the crown as the last pane sets
            double top = FACETS[FACETS.Length - 1].at, g = t - top;
            if (g > 0 && g < 0.3) Sim.Sparkle(p, V("p2")[0], V("p2")[1], 8 * Ease.OutBack(Math.Min(1, g / 0.06)) * (1 - g / 0.3), C[6], C[4], 0.6);
            if (t >= FLASH && t < BURST + 0.16)
            {
                double a = Clamp((t - FLASH) / 0.22);
                Sim.Flare(p, CORE[0], CORE[1], 34 * Px.Pow(1 - a, 1.4), 1 - a, C);
            }
            panes.Draw(p, C);
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
