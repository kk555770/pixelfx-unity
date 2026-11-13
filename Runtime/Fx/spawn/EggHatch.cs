// The cracks that open in stages and the burst into tumbling pieces follow Sprite Fusion's open-source ice
// shatter (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class EggHatch : Effect
    {
        const double DURATION = 3.1;

        public static readonly Module Module = new Module("egg-hatch", "pixelfx/fx/spawn/egg-hatch", 120, 96, 60, 84, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1e1826", "#4a3e52", "#857684", "#c4b4ac", "#ece0d2", "#fffaf0" },
                ["glow"] = new[] { "#ff8a2a", "#ffd25a", "#fff3c0" },
                ["speckle"] = "#9a6a52",
                ["seed"] = 31,
            },
            (o, e) => new EggHatch(o, e))
        {
            Title = "Egg hatch",
            Desc = "A speckled egg rocks three times, each rock cracking it further with warm light leaking out, then pops: light pours out of the opening and lit shell halves tumble away.",
            Tags = new[] { "spawn", "egg", "hatch", "birth", "crack", "shell", "light", "ground", "one-shot" },
            Bg = "ground",
        };

        static readonly double[][] WOBBLES = { new[] { 0.3, 0.11 }, new[] { 0.85, 0.19 }, new[] { 1.4, 0.28 } };
        const double POP = 1.98, ZY = 11;
        const int EW = 16, EH = 20;
        static readonly double[] LIGHT = { -0.72, 0.5, 0.48 }; // from the upper left, a little toward us

        sealed class EggPx
        {
            public int x, y, i;
            public double u, v, half;
        }

        // the egg in its own pixels: x across from its middle, y up from its base; wider low than high
        static readonly EggPx[] EGG = MakeEgg();
        // index into EGG by cell, -1 where there's no shell
        static readonly int[] Grid = MakeGrid();

        static EggPx[] MakeEgg()
        {
            var list = new List<EggPx>();
            for (int y = 0; y < EH; y++)
                for (int x = -EW / 2; x < EW / 2; x++)
                {
                    double v = (y + 0.5) / EH, u = (x + 0.5) / (EW / 2.0), half = Math.Sqrt(Math.Max(0, 1 - Px.Pow(2 * v - 1, 2))) * (1.08 - 0.22 * v);
                    if (Math.Abs(u) <= half) list.Add(new EggPx { x = x, y = y, u = u, v = v, half = half, i = list.Count });
                }
            return list.ToArray();
        }

        static int[] MakeGrid()
        {
            var g = new int[EW * EH];
            for (int i = 0; i < g.Length; i++) g[i] = -1;
            foreach (EggPx e in EGG) g[e.y * EW + e.x + EW / 2] = e.i;
            return g;
        }

        static int IndexAt(double x, double y)
        {
            if (x < -EW / 2 || x >= EW / 2 || y < 0 || y >= EH) return -1;
            return Grid[(int)y * EW + (int)x + EW / 2];
        }

        static bool Has(double x, double y) => IndexAt(x, y) >= 0;

        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        sealed class Piece
        {
            public int id;
            public double x, y, vx, vy, a, va, r, life, max, born;
            public Dictionary<(int, int), (uint c, bool edge)> map;
            public bool rest;
        }

        readonly uint[] C, G;
        readonly uint SPK;
        readonly Rng R;
        readonly uint[] ec;
        readonly double[] elit;
        // the crack stage at each pixel of shell, -1 where there's none
        readonly int[] crack;
        readonly double[] zig;
        readonly double[][] near;
        readonly Particles ps;
        readonly Lights lights;
        readonly Bits bits;
        readonly List<Piece> pieces = new List<Piece>();
        readonly bool[] done = new bool[3];
        const double CRUMBLE = DURATION - 0.55;
        double t, cupA, cupV;
        bool popped, crumbled;

        EggHatch(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 6);
            G = Fit(o.Ramp("glow"), 3);
            SPK = o.Color("speckle");
            R = new Rng(o.Seed);
            // shading: a round shell lit from the upper left, a core shadow, a rim of bounce light on the dark side
            ec = new uint[EGG.Length];
            elit = new double[EGG.Length];
            foreach (EggPx p in EGG)
            {
                double nx = Clamp(p.u / Math.Max(0.05, p.half), -1, 1), ny = (2 * p.v - 1) * 0.9, nz = Math.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
                double d = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2];
                bool edge = !Has(p.x - 1, p.y) || !Has(p.x + 1, p.y) || !Has(p.x, p.y - 1) || !Has(p.x, p.y + 1);
                uint c = d > 0.9 ? WHITE : d > 0.62 ? C[5] : d > 0.32 ? C[4] : d > 0.02 ? C[3] : d > -0.3 ? C[2] : C[1];
                if (edge && nx > 0.25 && ny < 0.55) c = C[3]; // rim light on the shadow side
                else if (Rand(p.x, p.y, 3) < 0.08 && p.v > 0.12 && !edge) c = d > 0.2 ? SPK : Mix(SPK, C[1], 0.5);
                ec[p.i] = c;
                elit[p.i] = d;
            }
            // cracks: three stages, the last a zigzag right round the egg where the top breaks off
            crack = new int[EGG.Length];
            for (int i = 0; i < crack.Length; i++) crack[i] = -1;
            var order = new List<int>(); // the cracked pixels in the order they first cracked
            void Set(int i, int s)
            {
                if (crack[i] < 0) order.Add(i);
                crack[i] = s;
            }
            // a crack: a jagged line, zigging either side of its heading, every step joined pixel to pixel
            void Walk(double x, double y, double a, int len, int s)
            {
                double px = Round(x), py = Round(y);
                for (int i = 0; i < len; i++)
                {
                    double side = (i % 2 != 0 ? 1 : -1) * R.Range(0.6, 1.3);
                    a += R.Range(-0.25, 0.25);
                    x += Px.Cos(a) * 1.8 - Px.Sin(a) * side * 0.6;
                    y += Px.Sin(a) * 1.8 + Px.Cos(a) * side * 0.6;
                    double nx = Round(x), ny = Round(y);
                    for (double j = 1, n = Math.Max(Math.Abs(nx - px), Math.Abs(ny - py)); j <= n; j++)
                    {
                        int k = IndexAt(Round(px + ((nx - px) * j) / n), Round(py + ((ny - py) * j) / n));
                        if (k < 0) return;
                        if (crack[k] < 0) Set(k, s);
                    }
                    px = nx;
                    py = ny;
                }
            }
            Walk(2, 15, -2.2, 3, 0);
            Walk(2, 15, 0.3, 2, 0);
            Walk(-2, 13, -0.6, 3, 1);
            Walk(-2, 13, 2.7, 3, 1);
            Walk(3, 11, 1.9, 2, 1);
            Walk(2, 15, 1.5, 2, 1);
            zig = new double[EW];
            for (int x = -EW / 2; x < EW / 2; x++)
            {
                double y = ZY + ((x + 8) % 3 == 0 ? 1 : (x + 8) % 3 == 1 ? 0 : -1);
                zig[x + EW / 2] = y;
                int k = IndexAt(x, y);
                if (k >= 0) Set(k, 2);
            }
            // how close each pixel of shell is to an open crack, per stage: light shows through the shell round them
            near = new double[3][];
            for (int s = 0; s < 3; s++)
            {
                var pts = new List<EggPx>();
                foreach (int i in order)
                    if (crack[i] <= s)
                        pts.Add(EGG[i]);
                var m = new double[EGG.Length];
                foreach (EggPx p in EGG)
                {
                    double best = 9;
                    foreach (EggPx c in pts) best = Math.Min(best, Hypot(c.x - p.x, c.y - p.y));
                    m[p.i] = best;
                }
                near[s] = m;
            }
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, G[2], G[1], G[0] }, emitter: e);
            lights = new Lights(e);
            bits = new Bits(x => 0, e);
        }

        double ZigAt(double x) => zig[(int)Clamp(x + EW / 2, 0, zig.Length - 1)];

        static int Stage(double t) => t > WOBBLES[2][0] + 0.05 ? 3 : t > WOBBLES[1][0] + 0.05 ? 2 : t > WOBBLES[0][0] + 0.05 ? 1 : 0;

        // the rock: a damped swing about the base, each one harder; a shiver just before it pops
        static double Angle(double t)
        {
            double a = 0;
            foreach (double[] w in WOBBLES)
            {
                double u = t - w[0];
                if (u > 0 && u < 0.55) a += w[1] * Px.Sin(u * 22) * Exp(-u * 5);
            }
            if (t > POP - 0.14 && t < POP) a += 0.05 * Px.Sin(t * 120);
            return a;
        }

        // the light inside: grows with each crack, flashes at the pop, then dies down in the empty cup
        static double Inner(double t) => t < POP ? Stage(t) / 3.0 + 0.25 * Math.Max(0, Px.Sin(t * 9)) * (Stage(t) / 3.0) : 1.6 * Exp(-(t - POP) * 2.6);

        // break part of the shell into pieces round a few seeds; each keeps its lit outside and a hot broken edge
        void BreakUp(Func<int, int, bool> keep, int n, Func<double, (double, double)> v, Func<double> life)
        {
            var seeds = new double[n][];
            for (int i = 0; i < n; i++)
            {
                double sx = R.Range(-EW / 2 + 1, EW / 2 - 1), sy = R.Range(0, EH);
                seeds[i] = new[] { sx, sy };
            }
            var cells = new Dictionary<int, List<EggPx>>();
            var keys = new List<int>();
            foreach (EggPx e in EGG)
            {
                if (!keep(e.x, e.y)) continue;
                int best = 0;
                double bd = 1e9;
                for (int i = 0; i < n; i++)
                {
                    double d = Hypot(e.x - seeds[i][0], e.y - seeds[i][1]);
                    if (d < bd)
                    {
                        bd = d;
                        best = i;
                    }
                }
                if (!cells.ContainsKey(best))
                {
                    cells[best] = new List<EggPx>();
                    keys.Add(best);
                }
                cells[best].Add(e);
            }
            foreach (int key in keys)
            {
                List<EggPx> px = cells[key];
                double cx = 0, cy = 0;
                foreach (EggPx e in px)
                {
                    cx += e.x + 0.5;
                    cy += e.y + 0.5;
                }
                cx /= px.Count;
                cy /= px.Count;
                var map = new Dictionary<(int, int), (uint c, bool edge)>();
                var mine = new HashSet<int>();
                foreach (EggPx e in px) mine.Add(e.i);
                foreach (EggPx e in px)
                {
                    bool edge = false;
                    foreach (int[] d in Cross)
                    {
                        int k = IndexAt(e.x + d[0], e.y + d[1]);
                        if (k >= 0 && !mine.Contains(k))
                        {
                            edge = true;
                            break;
                        }
                    }
                    // lit by the flash: no piece flies off in shadow
                    uint c = ec[e.i] == C[1] || ec[e.i] == C[2] ? C[3] : ec[e.i];
                    map[(RoundInt(e.x + 0.5 - cx), RoundInt(-(e.y + 0.5 - cy)))] = (c, edge);
                }
                double r = double.NegativeInfinity;
                foreach (EggPx e in px) r = Math.Max(r, Hypot(e.x + 0.5 - cx, e.y + 0.5 - cy));
                r += 1;
                var (vx, vy) = v(cx);
                double l = life();
                pieces.Add(new Piece { id = pieces.Count + 1, x = cx, y = -cy, vx = vx, vy = vy, a = 0, va = R.Range(-10, 10), r = r, map = map, life = l, max = l, rest = false, born = t });
            }
        }

        void Pop()
        {
            popped = true;
            BreakUp((x, y) => y >= ZigAt(x), 6, cx => (cx * R.Range(8, 13) + R.Range(-14, 14), -R.Range(140, 210)), () => R.Range(0.95, 1.15));
            uint[] shard = { C[5], C[5], C[4], G[2] };
            for (int i = 0; i < 14; i++)
            {
                double an = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(70, 170);
                bits.Add(new Bits.Bit
                {
                    kind = B.Shard, x = R.Range(-5, 5), y = -ZY - R.Range(0, 4), vx = Px.Cos(an) * sp, vy = Px.Sin(an) * sp, g = 0.9, drag = 0.6, bounce = 0.3,
                    life = R.Range(0.55, 0.85), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1.2, 2.4), c = R.Pick(shard),
                });
            }
            uint[] star = { WHITE, G[2], G[1] };
            for (int i = 0; i < 12; i++)
            {
                double an = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(50, 110);
                ps.Spawn(P.Star, R.Range(-3, 3), -ZY - 2, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.4, 0.9), R.Pick(star), R.Range(1, 2.2));
            }
            for (int i = 0; i < 10; i++)
            {
                double an = -Math.PI / 2 + R.Range(-1, 1), sp = R.Range(100, 200);
                ps.Spawn(P.Spark, R.Range(-3, 3), -ZY - 1, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.2, 0.45));
            }
            // a puff of pale dust at the base, gone in a few frames
            for (int i = 0; i < 4; i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Dust, s * R.Range(5, 8), -1, s * R.Range(25, 50), -R.Range(2, 6), R.Range(0.15, 0.25), C[5], R.Range(1, 1.8));
            }
            lights.Add(0, -ZY - 10, 40, G[1], 0.9, 0.55);
            lights.Add(0, -ZY - 2, 18, WHITE, 0.9, 0.12);
            cupV = R.Sign() * 3;
        }

        public override void Update(double dt)
        {
            t += dt;
            // each rock opens more crack: a tick of light, a chip or two of shell, a puff at the base
            uint[] star = { WHITE, G[2] };
            for (int i = 0; i < WOBBLES.Length; i++)
            {
                if (done[i] || t < WOBBLES[i][0] + 0.05) continue;
                done[i] = true;
                lights.Add(i == 1 ? -3 : 2, -14 + i, 8 + i * 2, G[1], 0.25 + i * 0.08, 0.18);
                for (int j = 0; j < 2 + i * 2; j++) ps.Spawn(P.Star, R.Range(-5, 5), -R.Range(8, 17), R.Range(-20, 20), -R.Range(10, 30), R.Range(0.25, 0.5), R.Pick(star), R.Range(0.6, 1.4));
                for (int j = 0; j < i + 1; j++)
                    bits.Add(new Bits.Bit
                    {
                        kind = B.Shard, x = R.Range(-4, 4), y = -R.Range(10, 16), vx = R.Range(-50, 50), vy = -R.Range(40, 90), g = 0.9, drag = 0.5, bounce = 0.3,
                        life = R.Range(0.5, 0.8), a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1, 1.6), c = C[4],
                    });
                ps.Spawn(P.Dust, R.Sign() * R.Range(5, 7), -0.5, R.Sign() * R.Range(8, 18), -R.Range(1, 3), R.Range(0.15, 0.25), C[5], R.Range(0.8, 1.4));
            }
            if (!popped && t >= POP) Pop();
            // light keeps welling up out of the empty cup for a moment: something came out of there
            if (popped && t < POP + 0.6 && R.Chance(0.35))
                ps.Spawn(P.Mote, R.Range(-4, 4), -ZY - 1, R.Range(-8, 8), -R.Range(40, 80), Math.Min(R.Range(0.4, 0.8), DURATION - 0.1 - t), R.Pick(new[] { G[2], G[1], WHITE }), 0.7);
            if (popped && !crumbled && t >= CRUMBLE)
            {
                crumbled = true;
                BreakUp((x, y) => y < ZigAt(x), 4, cx => (cx * R.Range(4, 8), -R.Range(40, 80)), () => R.Range(0.38, 0.48));
                for (int i = 0; i < 3; i++) ps.Spawn(P.Dust, R.Range(-6, 6), -1, R.Sign() * R.Range(10, 25), -R.Range(2, 6), R.Range(0.15, 0.25), C[5], R.Range(1, 1.6));
            }
            if (popped)
            {
                cupV += (-cupA * 90 - cupV * 4) * dt;
                cupA += cupV * dt;
            }
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece q = pieces[i];
                if ((q.life -= dt) <= 0)
                {
                    pieces.RemoveAt(i);
                    continue;
                }
                if (q.rest) continue;
                q.vy += 900 * dt;
                q.vx -= q.vx * 0.6 * dt;
                q.x += q.vx * dt;
                q.y += q.vy * dt;
                q.a += q.va * dt;
                double g = -q.r * 0.45;
                if (q.y > g && q.vy > 0)
                {
                    q.y = g;
                    if (q.vy > 80)
                    {
                        q.vy *= -0.35;
                        q.vx *= 0.6;
                        q.va *= 0.5;
                    }
                    else
                    {
                        q.rest = true;
                        q.vx = q.vy = 0;
                    }
                }
            }
            bits.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // a soft contact shadow under the egg, shifting as it rocks
            if (!crumbled)
            {
                double sx = popped ? 0 : -Angle(t) * 8, k = popped ? 1 - Smooth(POP, CRUMBLE, t) * 0.4 : 1;
                for (int y = -1; y <= 1; y++)
                    for (int x = -10; x <= 10; x++)
                    {
                        double d = Px.Pow((x + 0.5 - sx) / 9.5, 2) + Px.Pow((y + 0.5) / 1.8, 2);
                        if (d < 1) p.Blend(x, y, C[0], 0.5 * k * (1 - d));
                    }
            }
            // warm light on the floor as it leaks out
            double g = Inner(t);
            if (g > 0.05) p.Glow(0, -3, 22, G[0], 0.18 * Math.Min(1.2, g));
            ps.Draw(p, Layer.Back);
        }

        int CrackAt(double x, double y)
        {
            int k = IndexAt(x, y);
            return k < 0 ? -1 : crack[k];
        }

        public override void Draw(Painter p)
        {
            int st = Stage(t);
            double a = popped ? cupA * 0.03 : Angle(t), ca = Px.Cos(a), sa = Px.Sin(a), g = Inner(t);
            if (!crumbled)
                p.Shade(-12, -EH - 4, 12, 1, (x, y, o) =>
                {
                    double ux = Math.Floor(x * ca + y * sa), uy = Math.Floor(-(-x * sa + y * ca));
                    int ei = IndexAt(ux, uy);
                    bool inside = ei >= 0 && !(popped && uy >= ZigAt(ux));
                    if (!inside)
                    {
                        // a dark outline holds the egg's shape
                        bool n = false;
                        foreach (int[] d in Cross)
                            if (Has(ux + d[0], uy + d[1]) && !(popped && uy + d[1] >= ZigAt(ux + d[0])))
                            {
                                n = true;
                                break;
                            }
                        if (n && !(popped && uy >= ZigAt(ux) && uy <= ZigAt(ux) + 1)) o.c = C[0];
                        return 0;
                    }
                    int cs = crack[ei];
                    if (popped)
                    {
                        // the cup: its broken rim burns with the light inside, cooling as it fades
                        o.c = uy >= ZigAt(ux) - 1 ? (g > 0.9 ? WHITE : g > 0.4 ? G[2] : g > 0.12 ? G[1] : C[4]) : ec[ei];
                        if (uy >= ZigAt(ux) - 1)
                        {
                            o.k = 0.25 * g;
                            o.l = G[1];
                        }
                        return 0;
                    }
                    if (cs >= 0 && cs < st)
                    {
                        // an open crack: deep orange light in the gap, flashing white the moment it opens
                        bool fresh = t - WOBBLES[cs][0] < 0.17;
                        o.c = fresh ? WHITE : Rand(ux, uy, ToInt32(t * 20)) < 0.25 + 0.1 * st ? G[1] : G[0];
                        o.k = 0.14 + 0.06 * st;
                        o.l = G[1];
                        return 0;
                    }
                    // the lip of each crack falls into shadow below it, so the gap reads as a gap
                    int below = CrackAt(ux, uy + 1), right = CrackAt(ux + 1, uy);
                    if ((below >= 0 && below < st) || (right >= 0 && right < st))
                    {
                        o.c = elit[ei] > 0.3 ? C[2] : C[1];
                        return 0;
                    }
                    // light shows through the shell in a halo round the cracks, and the whole egg warms a little
                    double dn = st > 0 ? near[st - 1][ei] : 9, k = Math.Min(1, g * 1.5);
                    uint e = ec[ei], b = st > 0 && e != SPK ? Mix(e, G[0], 0.05 * st * k) : e;
                    o.c = dn < 1.6 ? Mix(b, G[1], (st >= 2 ? 0.32 : 0.2) * k) : dn < 2.7 && st >= 2 ? Mix(b, G[1], 0.14 * k) : b;
                    return 0;
                });
            // the pop: the opening floods white, and light rays fan up out of it
            double q = t - POP;
            if (popped && q < 1.2 && !crumbled && g > 0.22)
            {
                double oy = -ZY + 0.5, rx = 7;
                for (int y = -2; y <= 2; y++)
                    for (int x = -(int)rx; x <= rx; x++)
                    {
                        double d = Hypot((x + 0.5) / rx, (y + 0.5) / 2.2);
                        if (d > 1) continue;
                        if (g < 0.6 && d > 0.75) continue;
                        p.Px(x, oy + y, g > 1 ? (d < 0.7 ? WHITE : G[2]) : g > 0.6 ? (d < 0.5 ? G[2] : G[1]) : d < 0.45 ? G[1] : G[0]);
                    }
                p.Glow(0, oy, 16, G[1], 0.6 * g);
                if (q < 0.65)
                {
                    double rk = q / 0.65, L = 36 * Ease.OutCubic(Clamp(q / 0.1)) * (1 - rk * 0.6);
                    for (int i = 0; i < 7; i++)
                    {
                        double an = -Math.PI / 2 + (i - 3) * 0.24 + 0.05 * Px.Sin(t * 6 + i), len = L * (i % 2 != 0 ? 0.65 : 1);
                        int w = i % 2 != 0 ? 1 : 2;
                        double x1 = Px.Cos(an) * len, y1 = oy + Px.Sin(an) * len;
                        for (int j = 0; j < w; j++) p.Line(j * 0.8 - 0.4, oy - 1, x1 + j * 0.8 - 0.4, y1, u => u < 0.3 ? WHITE : u < 0.65 ? G[2] : G[1], u => (1 - u * u) * (1.1 - rk));
                        p.GlowLine(0, oy, x1, y1, 5, G[1], 0.35 * (1 - rk));
                    }
                }
                if (q < 0.1) p.Glow(0, oy - 4, 30, WHITE, 0.6 * (1 - q / 0.1));
                // the light that lingers over the cup
                p.GlowLine(0, oy, 0, oy - 26, 8, G[1], 0.25 * g);
            }
            // the inside glow through the shell before it pops
            if (!popped && g > 0) p.Glow(0, -EH * 0.55, 7 + 2 * st, G[1], 0.05 * g);
            // pieces of shell: their lit outsides, their broken edges hot at first
            foreach (Piece s in pieces)
            {
                double c2 = Px.Cos(s.a), s2 = Px.Sin(s.a), f = Math.Min(1, s.life / 0.4);
                int r = (int)Math.Ceiling(s.r);
                bool hot = t - s.born < 0.35;
                for (int py = -r; py <= r; py++)
                    for (int px = -r; px <= r; px++)
                    {
                        int ux = RoundInt(px * c2 + py * s2), uy = RoundInt(-px * s2 + py * c2);
                        if (s.map.TryGetValue((ux, uy), out var m) && Rand(ux, uy, s.id) < f)
                            p.Px(Math.Floor(s.x) + px, Math.Floor(s.y) + py, m.edge ? (hot ? G[2] : C[4]) : m.c);
                    }
                if (hot) p.Glow(s.x, s.y, 6, G[1], 0.3);
            }
            bits.Draw(p);
            if (popped && q < 0.9) Sim.Sparkle(p, -3, -ZY - 6, Round(4 * (1 - q / 0.9)), WHITE, G[1], 0.4);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
