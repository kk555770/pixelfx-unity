// The impact flash, cracks racing out from the impact, the white flash before the burst, and the glass
// splinters, glints and cold light of the burst are adapted from Sprite Fusion's open-source ice shatter
// (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class ShatterWipe : Effect
    {
        const double D = 2.8;

        public static readonly Module Module = Screen("shatter-wipe", "Shatter wipe",
            "A dark glass pane drops over the screen and bounces; cracks race out from an impact with a flash, then it bursts into tumbling glass shards with lit edges that fall away.",
            new[] { "shatter", "glass", "break", "shards", "wipe" }, D,
            new Options { ["palette"] = new[] { "#0c1430", "#162c5c", "#1f4f94", "#2f7fcc", "#58b4ec", "#9bdcff", "#d8f4ff", "#ffffff" }, ["seed"] = 2 },
            (o, e) => new ShatterWipe(o, e));

        const double LAND = 0.55, GP = (2.0 * H) / (LAND * LAND), REST = 0.3, CRACK = 1.32, BREAK = 1.62, WAVE = 520;
        const double LX = -0.6, LY = -0.8; // light comes from the top left
        static readonly double[] L3 = { -0.42, -0.56, 0.71 }; // and a little from the front, for the faces

        // the pane's top edge over time: it falls from above the screen, lands, bounces twice and settles
        static readonly (double t0, double v, double T)[] BOUNCES = Bounces();

        static (double t0, double v, double T)[] Bounces()
        {
            var o = new (double, double, double)[3];
            double t0 = LAND, v = GP * LAND * REST;
            for (int k = 0; k < 3; k++)
            {
                o[k] = (t0, v, (2 * v) / GP);
                t0 += (2 * v) / GP;
                v *= REST;
            }
            return o;
        }

        static double PaneY(double t)
        {
            if (t < LAND) return -H + 0.5 * GP * t * t;
            foreach (var b in BOUNCES)
                if (t < b.t0 + b.T) return -(b.v * (t - b.t0) - 0.5 * GP * Px.Pow(t - b.t0, 2));
            return 0;
        }

        sealed class Crack
        {
            public int x, y;
            public double t0;
        }

        sealed class Cell
        {
            public List<double[]> pts, lp;
            public double cx, cy, d, r, tb, vx, vy, a, va, phi, vphi, psi, z, vz, g;
            public bool broke, gone, edgeOn;
        }

        sealed class Pattern
        {
            public double Ix, Iy;
            public List<Crack> cracks;
            public List<Cell> cells;
        }

        // the break: jagged rays out of the impact crossed by rings, every crack shared by the two cells it splits
        static Pattern MakePattern(Rng R)
        {
            double Ix = W * R.Range(0.4, 0.6), Iy = H * R.Range(0.36, 0.54);
            const int NR = 13;
            int[] RING = { 0, 9, 21, 37, 58, 86, 122, 168, 232 };
            var ang = new double[NR];
            for (int i = 0; i < NR; i++) ang[i] = ((i + R.Range(-0.3, 0.3)) / NR) * TAU;
            var speed = new double[NR];
            for (int i = 0; i < NR; i++) speed[i] = R.Range(800, 1300);
            var V = new double[NR][][];
            for (int i = 0; i < NR; i++)
            {
                V[i] = new double[RING.Length][];
                for (int j = 0; j < RING.Length; j++)
                {
                    if (j == 0)
                    {
                        V[i][j] = new[] { Ix, Iy };
                        continue;
                    }
                    double rr = RING[j] * R.Range(0.84, 1.16), aa = ang[i] + R.Range(-0.07, 0.07);
                    V[i][j] = new[] { Ix + Px.Cos(aa) * rr, Iy + Px.Sin(aa) * rr };
                }
            }
            List<double[]> Jag(double[] A, double[] Bp)
            {
                double dx = Bp[0] - A[0], dy = Bp[1] - A[1], L = Hypot(dx, dy);
                if (L == 0 || double.IsNaN(L)) L = 1;
                int n = L > 18 ? 2 : 1;
                var o = new List<double[]> { A };
                for (int k = 1; k <= n; k++)
                {
                    double f = (double)k / (n + 1) + R.Range(-0.08, 0.08), off = R.Range(-1, 1) * Math.Min(3, L * 0.07);
                    o.Add(new[] { A[0] + dx * f - (dy / L) * off, A[1] + dy * f + (dx / L) * off });
                }
                o.Add(Bp);
                return o;
            }
            var rad = new List<double[]>[NR][];
            for (int i = 0; i < NR; i++)
            {
                rad[i] = new List<double[]>[RING.Length - 1];
                for (int j = 0; j < RING.Length - 1; j++) rad[i][j] = Jag(V[i][j], V[i][j + 1]);
            }
            var ring = new List<double[]>[NR][];
            for (int i = 0; i < NR; i++)
            {
                ring[i] = new List<double[]>[RING.Length];
                for (int j = 0; j < RING.Length; j++) ring[i][j] = j != 0 ? Jag(V[i][j], V[(i + 1) % NR][j]) : null;
            }
            // real glass breaks into long wedges: rings near the impact are whole, further out they're broken arcs
            var keep = new bool[NR][];
            for (int i = 0; i < NR; i++)
            {
                keep[i] = new bool[RING.Length];
                for (int j = 0; j < RING.Length; j++) keep[i][j] = j <= 2 || j == RING.Length - 1 || R.Chance(0.78 - 0.07 * j);
            }
            // when a crack gets to each pixel: rays race out at their own speed, rings snap across once both rays pass
            var cracks = new List<Crack>();
            double Reach(int i, double x, double y) => CRACK + Hypot(x - Ix, y - Iy) / speed[i];
            void Trace(List<double[]> pts, Func<double, double, double> when)
            {
                for (int k = 1; k < pts.Count; k++)
                {
                    double ax = pts[k - 1][0], ay = pts[k - 1][1], bx = pts[k][0], by = pts[k][1], n = Math.Ceiling(Hypot(bx - ax, by - ay) * 2);
                    for (int s = 0; s < n; s++)
                    {
                        double x = ax + ((bx - ax) * s) / n, y = ay + ((by - ay) * s) / n;
                        if (x >= 0 && y >= 0 && x < W && y < H) cracks.Add(new Crack { x = (int)Math.Floor(x), y = (int)Math.Floor(y), t0 = when(x, y) });
                    }
                }
            }
            for (int i = 0; i < NR; i++)
                foreach (List<double[]> e in rad[i])
                {
                    int ii = i;
                    Trace(e, (x, y) => Reach(ii, x, y));
                }
            for (int i = 0; i < NR; i++)
                for (int j = 1; j < RING.Length - 1; j++)
                {
                    if (!keep[i][j]) continue;
                    int i2 = (i + 1) % NR;
                    List<double[]> e = ring[i][j];
                    double start = Math.Max(Reach(i, V[i][j][0], V[i][j][1]), Reach(i2, V[i2][j][0], V[i2][j][1])) + R.Range(0.02, 0.07);
                    double ax = e[0][0], ay = e[0][1];
                    Trace(e, (x, y) => start + Hypot(x - ax, y - ay) / 420);
                }
            cracks = cracks.OrderBy(c => c.t0).ToList();
            var cells = new List<Cell>();
            for (int i = 0; i < NR; i++)
                for (int j = 0, jb; j < RING.Length - 1; j = jb + 1)
                {
                    // a wedge from ring j out to the next ring crack that's there
                    for (jb = j; !keep[i][jb + 1]; jb++) { }
                    int i2 = (i + 1) % NR, j0 = j, j1 = jb;
                    List<double[]> Side(int r)
                    {
                        var o = new List<double[]>();
                        for (int k = j0; k < j1 + 1; k++)
                        {
                            List<double[]> e = rad[r][k];
                            o.AddRange(k - j0 != 0 ? e.Skip(1) : e);
                        }
                        return o;
                    }
                    var pts = new List<double[]>();
                    pts.AddRange(Side(i));
                    pts.AddRange(ring[i][jb + 1].Skip(1));
                    List<double[]> other = Side(i2);
                    other.Reverse();
                    pts.AddRange(other.Skip(1));
                    if (j != 0)
                    {
                        List<double[]> back = ring[i][j].ToList();
                        back.Reverse();
                        pts.AddRange(back.Skip(1).Take(back.Count - 2));
                    }
                    else pts.RemoveAt(pts.Count - 1);
                    double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
                    foreach (double[] q in pts)
                    {
                        x0 = Math.Min(x0, q[0]);
                        x1 = Math.Max(x1, q[0]);
                        y0 = Math.Min(y0, q[1]);
                        y1 = Math.Max(y1, q[1]);
                    }
                    if (x1 < 0 || y1 < 0 || x0 >= W || y0 >= H) continue;
                    // a shard of what's on screen: centered on the visible part, so it spins about what you see
                    double cx = (Clamp(x0, 0, W) + Clamp(x1, 0, W)) / 2, cy = (Clamp(y0, 0, H) + Clamp(y1, 0, H)) / 2;
                    double d = Hypot(cx - Ix, cy - Iy);
                    cells.Add(new Cell { pts = pts, lp = pts.ConvertAll(q => new[] { q[0] - cx, q[1] - cy }), cx = cx, cy = cy, d = d, r = Hypot(x1 - x0, y1 - y0) / 2, tb = BREAK + d / WAVE + R.Range(0, 0.04) });
                }
            return new Pattern { Ix = Ix, Iy = Iy, cracks = cracks, cells = cells };
        }

        readonly uint[] C;
        readonly uint ink;
        readonly Rng R;
        readonly Pattern pat;
        readonly double Ix, Iy;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly double end = D - 0.05;
        double t;
        int bounce = -1;
        bool struck, burst;

        ShatterWipe(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            ink = o.Color("color");
            R = new Rng(o.Seed);
            pat = MakePattern(R);
            Ix = pat.Ix;
            Iy = pat.Iy;
            ps = new Particles(rng: R, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[5], emitter: e);
            bits = new Bits(emitter: e);
            lights = new Lights(e);
        }

        // glass splinters thrown from (x, y): little white-tipped streaks that tumble and fall
        void Splinters(double x, double y, int n, double vx, double vy, double spread, double sp0, double sp1)
        {
            for (int i = 0; i < n; i++)
            {
                double a = Atan2(vy, vx) + R.Range(-spread, spread), sp = R.Range(sp0, sp1), life = Math.Min(R.Range(0.6, 1.2), end - t);
                if (life > 0.2)
                    bits.Add(new Bits.Bit { kind = B.Shard, x = x, y = y, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 0.8, drag = 0.6, life = life, a = R.Range(0, TAU), va = R.Range(-20, 20), s = R.Range(1.5, 3.5), c = R.Pick(new[] { C[7], C[6], C[5] }) });
            }
        }

        void Glint(double x, double y) => Glint(x, y, R.Range(1, 2.4));

        void Glint(double x, double y, double s) => ps.Spawn(P.Star, x, y, R.Range(-8, 8), R.Range(-10, 4), Math.Min(R.Range(0.3, 0.6), end - t), R.Chance(0.5) ? WHITE : C[6], s);

        public override void Update(double dt)
        {
            t += dt;
            // each landing: chips spring off the bottom edge, glints, a cold flash along it
            while (bounce + 1 < BOUNCES.Length && t >= BOUNCES[bounce + 1].t0)
            {
                double k = BOUNCES[++bounce].v / BOUNCES[0].v;
                if (k < 0.05) continue;
                for (int i = 0; i < 5; i++) lights.Add(W * (i + 0.5) / 5, H - 2, 70, C[5], 0.5 * k, 0.35);
                for (int i = 0; i < Round(26 * k); i++) Splinters(R.Range(4, W - 4), H - 1, 1, R.Range(-0.4, 0.4), -1, 0.5, 120 * k, 330 * k);
                for (int i = 0; i < Round(9 * k); i++) Glint(R.Range(8, W - 8), H - R.Range(2, 10));
            }
            if (!struck && t >= CRACK)
            {
                // the blow: a white flash at the point of impact, a puff of splinters back off the glass
                struck = true;
                lights.Add(Ix, Iy, 90, C[5], 0.8, 0.35);
                lights.Add(Ix, Iy, 34, WHITE, 0.9, 0.1);
                Splinters(Ix, Iy, 16, 0, -1, Math.PI, 60, 240);
                for (int i = 0; i < 5; i++) ps.Spawn(P.Vapour, Ix, Iy, R.Range(-40, 40), R.Range(-40, 30), R.Range(0.35, 0.6), C[6], R.Range(2, 3.5));
            }
            // while the cracks run, glints chase their tips
            if (t > CRACK && t < BREAK && R.Chance(0.12))
            {
                Crack c = pat.cracks[R.Int(0, pat.cracks.Count - 1)];
                if (c.t0 < t) Glint(c.x, c.y, R.Range(0.6, 1.6));
            }
            if (!burst && t >= BREAK)
            {
                // the burst: a cold flash, a spray of splinters and a breath of glass dust out of the impact
                burst = true;
                lights.Add(Ix, Iy, 170, C[4], 0.55, 0.5);
                lights.Add(Ix, Iy, 60, WHITE, 0.8, 0.14);
                Splinters(Ix, Iy, 46, 0, -1, Math.PI, 90, 340);
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(30, 110);
                    ps.Spawn(P.Vapour, Ix + Px.Cos(a) * 4, Iy + Px.Sin(a) * 4, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.45, 0.8), R.Chance(0.6) ? C[6] : C[5], R.Range(3, 6));
                }
            }
            // the pane bursts outward from the impact: each shard flies off, tumbling, and falls away from the screen
            foreach (Cell s in pat.cells)
            {
                if (!s.broke && t >= s.tb)
                {
                    s.broke = true;
                    double dx = s.cx - Ix, dy = s.cy - Iy, d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    double sp = R.Range(40, 130) * (1.15 - Math.Min(1, s.d / 260));
                    s.vx = (dx / d) * sp + R.Range(-15, 15);
                    s.vy = (dy / d) * sp * 0.6 - R.Range(20, 90);
                    s.a = 0;
                    s.va = R.Range(-2.5, 2.5) * (1 + 20 / (s.r + 10));
                    s.phi = 0;
                    s.vphi = R.Sign() * R.Range(3, 9);
                    s.psi = R.Range(0, TAU);
                    s.z = 0;
                    s.vz = R.Range(0.25, 0.7);
                    s.g = R.Range(0.85, 1.1);
                    s.edgeOn = false;
                    Splinters(s.cx, s.cy, s.r > 24 ? 4 : 2, s.vx, s.vy, 1.2, 60, 220);
                    if (R.Chance(0.15)) Glint(s.cx + R.Range(-4, 4), s.cy + R.Range(-4, 4));
                }
                if (!s.broke || s.gone) continue;
                s.vy += 950 * s.g * dt;
                s.vx -= s.vx * 0.3 * dt;
                s.cx += s.vx * dt;
                s.cy += s.vy * dt;
                s.a += s.va * dt;
                s.phi += s.vphi * dt;
                s.z += s.vz * dt;
                // turning edge-on to us it flashes: a glint where the light catches the glass
                bool on = Math.Abs(Px.Cos(s.phi)) < 0.2;
                if (on && !s.edgeOn && t < end - 0.3) Glint(s.cx, s.cy, R.Range(1.4, 2.6));
                s.edgeOn = on;
                if (s.cy - s.r / (1 + s.z) > H + 2) s.gone = true;
            }
            ps.Update(dt);
            bits.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p0)
        {
            Painter p = ClipTo(p0);
            if (t < BREAK)
            {
                // the pane: dark glass, a lit bevel along its falling edge, faint reflections that fade once it settles
                double yb = PaneY(t) + H, r1 = Math.Min(H, Math.Floor(yb));
                if (r1 > 0)
                {
                    Rect(p0, 0, 0, W, r1, ink);
                    double shine = 1 - Smooth(0.98, 1.22, t), y0 = yb - H;
                    if (shine > 0)
                    {
                        foreach (double[] sh in new[] { new double[] { 0, 16, 0.05 }, new[] { 22, 3, 0.1 }, new double[] { 30, 1, 0.12 } })
                        {
                            double off = sh[0], w = sh[1], a = sh[2];
                            for (int y = 0; y < r1; y++)
                            {
                                double xc = W * 0.18 + off + (H - (y - y0)) * 0.7;
                                for (double x = Math.Max(0, Math.Floor(xc - w / 2)); x < Math.Min(W, Math.Ceiling(xc + w / 2)); x++) p0.Blend(x + 0.5, y + 0.5, C[6], a * shine);
                            }
                        }
                        int st = shine > 0.66 ? 0 : shine > 0.33 ? 1 : 2;
                        foreach (var (dy, c) in new[] { (1, C[6 - st * 2]), (2, C[4 - st]), (3, C[Math.Max(1, 2 - st)]) })
                            if (r1 - dy >= 0 && r1 - dy < H) Rect(p0, 0, r1 - dy, W, 1, c);
                        // light runs along the edge as it falls, and spills onto the picture below it
                        double gx = W * Clamp(t / (LAND + 0.25)) * 1.2 - 20;
                        for (int x = 0; x < W; x++)
                        {
                            double k = Math.Max(0, 1 - Math.Abs(x - gx) / 28);
                            if (k > 0 && r1 - 1 < H)
                            {
                                p0.Px(x, r1 - 1, k > 0.5 ? WHITE : C[6]);
                                p0.Light(x, r1 - 1, C[6], 0.5 * k);
                            }
                            for (int d = 0; d < 14 && r1 + d < H; d++) p0.Light(x, r1 + d, C[5], 0.3 * shine * Px.Pow(1 - d / 14.0, 2) * (0.6 + 0.8 * k));
                        }
                        if (t < LAND + 0.25 && gx > 0 && gx < W && r1 - 1 < H) Sim.Sparkle(p, gx, r1 - 1, 6, C[6], C[4], 0.5);
                    }
                }
                // the cracks race out from the impact: white at the tip, cooling to ice, each with its shadow
                if (t >= CRACK)
                {
                    foreach (Crack c in pat.cracks)
                    {
                        if (c.t0 > t) break;
                        double age = t - c.t0;
                        if (c.x + 1 < W && c.y + 1 < H) p0.Px(c.x + 1, c.y + 1, C[1]);
                        p0.Px(c.x, c.y, age < 0.04 ? WHITE : age < 0.18 ? C[6] : C[5]);
                        if (age < 0.3) p0.Light(c.x, c.y, C[5], 0.55 * (1 - age / 0.3));
                    }
                    double u = (t - CRACK) / 0.22;
                    if (u < 1)
                    {
                        Sim.Flare(p, Ix, Iy, 30 * Px.Pow(1 - u, 1.5), 1 - u, C);
                        p.Glow(Ix, Iy, 50, WHITE, 0.7 * (1 - u));
                    }
                    // a beat before the burst, the cracks flare white
                    double w = (t - (BREAK - 0.07)) / 0.07;
                    if (w > 0)
                        foreach (Crack c in pat.cracks)
                        {
                            p0.Px(c.x, c.y, WHITE);
                            p0.Light(c.x, c.y, C[6], 0.4 * w);
                        }
                }
            }
            else
            {
                // shards still in place first (the pane breaking up outward), then the falling ones, nearest last
                List<Cell> fall = pat.cells.Where(s => s.broke && !s.gone).OrderByDescending(s => s.z).ToList();
                foreach (Cell s in pat.cells)
                    if (!s.broke) DrawShard(p, p0, s, true);
                foreach (Cell s in fall) DrawShard(p, p0, s, false);
            }
            bits.Draw(p);
            ps.Draw(p);
            lights.Draw(p);
        }

        // a shard: dark glass that tumbles about an axis in its plane (squashing as it turns, a bright sliver
        // edge-on), its edges lit where they face the light, its face catching a cool sheen as it turns to it
        void DrawShard(Painter p, Painter p0, Cell s, bool still)
        {
            List<double[]> pts;
            uint face = ink;
            bool sliver = false;
            double gl = 0;
            if (still) pts = s.pts;
            else
            {
                double cp = Px.Cos(s.phi), sp = Px.Sin(s.phi), ci = Px.Cos(s.psi), si = Px.Sin(s.psi), ca = Px.Cos(s.a), sa = Px.Sin(s.a), k = 1 / (1 + s.z);
                pts = s.lp.ConvertAll(q =>
                {
                    double x = q[0], y = q[1], u = x * ci + y * si, v = (-x * si + y * ci) * cp, qx = u * ci - v * si, qy = u * si + v * ci;
                    return new[] { s.cx + (qx * ca - qy * sa) * k, s.cy + (qx * sa + qy * ca) * k };
                });
                // the face's normal, turned by the tumble and the spin, against the light
                double nx0 = si * sp, ny0 = -ci * sp, nz = cp, nx = nx0 * ca - ny0 * sa, ny = nx0 * sa + ny0 * ca;
                double d = Math.Abs(nx * L3[0] + ny * L3[1] + nz * L3[2]);
                gl = Px.Pow(d, 6);
                // turning toward edge-on the glass reflects more (a grazing sheen), and a hairline sliver goes bright
                double graze = Px.Pow(Clamp(1 - Math.Abs(cp) / 0.3), 2);
                face = Mix(Mix(ink, C[2], 0.12 + 0.4 * gl), C[3], 0.55 * graze);
                sliver = Math.Abs(cp) < 0.2;
                if (Math.Abs(cp) * s.r < 1.5) face = C[5];
            }
            FillConvex(p0, pts, face);
            double a2 = 0;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++) a2 += pts[j][0] * pts[i][1] - pts[i][0] * pts[j][1];
            int sg = a2 > 0 ? 1 : -1;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                double ax = pts[j][0], ay = pts[j][1], bx = pts[i][0], by = pts[i][1], dx = bx - ax, dy = by - ay, L = Hypot(dx, dy);
                if (L < 0.5) continue;
                double mx = (dy / L) * sg, my = (-dx / L) * sg, el = mx * LX + my * LY;
                if (still)
                {
                    // a crack in the pane: lit fracture line, its shadow below
                    p.Line(ax, ay, bx, by, el > 0 ? C[5] : C[4]);
                    continue;
                }
                if (el > 0.2 || sliver)
                {
                    p.Line(ax - mx, ay - my, bx - mx, by - my, C[4]);
                    p.Line(ax, ay, bx, by, gl > 0.5 || sliver ? WHITE : C[6]);
                    p.Glow((ax + bx) / 2, (ay + by) / 2, Math.Min(12, 4 + L * 0.3), C[5], 0.12 + 0.3 * gl);
                }
                else p.Line(ax, ay, bx, by, el > -0.3 ? C[4] : C[2]);
            }
        }
    }
}
