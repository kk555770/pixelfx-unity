// The hit recipe (a tracer from the right, a flash and a ripple over a hex shell mapped by longitude and
// latitude, sparks glancing off) is adapted from Sprite Fusion's open-source shield
// (https://www.spritefusion.com/pixel-art-vfx/shield), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class ForceField : Effect
    {
        static readonly Pose HOLD = new Pose { ["turn"] = 1, ["crouch"] = 3, ["lean"] = 1, ["fx"] = -9, ["fy"] = -38, ["bx"] = 10, ["by"] = -38, ["ffx"] = -5, ["fbx"] = 6 };
        static readonly Pose HIT_ = new Pose(HOLD) { ["x"] = -2, ["crouch"] = 5, ["lean"] = -3 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(0, HOLD),
                new PoseKey(0.42, HOLD),
                new PoseKey(0.42, HIT_, "snap"),
                new PoseKey(0.7, new Pose(HOLD) { ["x"] = -1 }),
                new PoseKey(1.1, HOLD),
                new PoseKey(1.5, HOLD),
                new PoseKey(1.5, new Pose(HIT_) { ["crouch"] = 4, ["lean"] = -2 }, "snap"),
                new PoseKey(1.75, HOLD),
                new PoseKey(2.4, HOLD),
            },
            Hits = new[] { new HitCue { T = 0.42, Stop = 0.07, Shake = 0.16, Amp = 2 }, new HitCue { T = 1.5, Stop = 0.03, Shake = 0.08, Amp = 1 } },
        };

        public static readonly Module Module = new Module("force-field", "pixelfx/fx/scifi/force-field", 184, 104, 76, 84, 2.4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0b1030", "#152a63", "#1d4f9e", "#2582d2", "#36b9ee", "#8ae8ff", "#d4fbff", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["radius"] = 44,
                ["seed"] = 5,
            },
            (o, e) => new ForceField(o, e), act: Act, actor: 2)
        {
            Title = "Force field",
            Desc = "A glassy dome of hex plates over the hero: plates glint, tracers from the right slam into it, a ripple and cracks of light race across, sparks glance off.",
            Tags = new[] { "scifi", "shield", "barrier", "dome", "hex", "hit", "defense", "loop" },
            Bg = "ground",
        };

        const double D = 2.4, SIZE_W = 184, ORIGIN_X = 76;
        static readonly double S3 = Math.Sqrt(3);
        const double SA = 0.13;
        static readonly double CA = Math.Sqrt(1 - SA * SA); // we look down on the dome a little
        const double G = 6.3, KY = 1.12; // hex plates per radian of longitude, and their stretch in latitude

        sealed class Shot
        {
            public double t, lon, lat;
            public bool big;
            public double[] N;
        }

        // the shots, once a loop: a heavy one that cracks the shell, a lighter one lower down that it shrugs off
        static readonly Shot[] SHOTS = new[]
        {
            new Shot { t = 0.42, lon = 0.66, lat = 0.62, big = true },
            new Shot { t = 1.5, lon = 0.95, lat = 0.26, big = false },
        }.Select(s =>
        {
            s.N = new[] { Px.Cos(s.lat) * Px.Sin(s.lon), Px.Sin(s.lat), Px.Cos(s.lat) * Px.Cos(s.lon) };
            return s;
        }).ToArray();

        struct Cell
        {
            public double q, r, hx, hy, e, corner;
        }

        // the pointy-top hex (corner radius 1) under grid point (gx, gy): its axial coords and center, how near its
        // edge (1 on it) and how near one of its corners (0 on it)
        static Cell CellAt(double gx, double gy)
        {
            double fq = (S3 / 3) * gx - gy / 3, fr = (2.0 / 3) * gy, fs = -fq - fr;
            double q = Round(fq), r = Round(fr);
            double s = Round(fs), dq = Math.Abs(q - fq), dr = Math.Abs(r - fr), ds = Math.Abs(s - fs);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            double hx = S3 * (q + r / 2), hy = 1.5 * r, dx = gx - hx, dy = gy - hy, ax = Math.Abs(dx);
            double e = Math.Max(Math.Max(ax, Math.Abs(ax / 2 + (dy * S3) / 2)), Math.Abs(ax / 2 - (dy * S3) / 2)) / (S3 / 2);
            double k = Round((Atan2(dy, dx) - Math.PI / 6) / (Math.PI / 3)), ca = Math.PI / 6 + (k * Math.PI) / 3;
            return new Cell { q = q, r = r, hx = hx, hy = hy, e = e, corner = Hypot(dx - Px.Cos(ca), dy - Px.Sin(ca)) };
        }

        // a point on the dome (longitude, latitude) to the screen: [x, y, facing (> 0 on the near side)]
        static double[] Project(double R, double lon, double lat)
        {
            double X = Px.Cos(lat) * Px.Sin(lon), Y = Px.Sin(lat), Z = Px.Cos(lat) * Px.Cos(lon);
            return new[] { R * X, -R * (Y * CA - Z * SA), Y * SA + Z * CA };
        }

        sealed class SP
        {
            public int x, y;
            public double X, Y, Z, lon, lat, q, r, id, e, corner, rim, sil;
            public bool seam, bevel;
        }

        // every pixel of the near and far shell, worked out once: where it sits on the sphere and on the plates, and
        // whether it's a seam (the last pixel of a plate before the next) or a plate's lit top-left edge
        static (List<SP> near, List<SP> far) Shell(double R)
        {
            var near = new List<SP>();
            var far = new List<SP>();
            int N = (int)Math.Ceiling(R) + 1, W = 2 * N + 1;
            var ids = new[] { new Dictionary<int, double>(), new Dictionary<int, double>() };
            for (int y = -N; y <= Math.Ceiling(R * SA) + 1; y++)
                for (int x = -N; x <= N; x++)
                {
                    double u = (x + 0.5) / R, vy = -(y + 0.5) / R, q = u * u + vy * vy;
                    if (q > 1) continue;
                    double vz = Math.Sqrt(1 - q);
                    foreach (int s in new[] { 1, -1 })
                    {
                        double Y = vy * CA + s * vz * SA, Z = -vy * SA + s * vz * CA;
                        if (Y < 0) continue;
                        double lon = Atan2(u, Z), lat = Px.Asin(Math.Min(1, Y));
                        Cell c = CellAt(lon * G, lat * G * KY);
                        double id = c.q * 1000 + c.r;
                        (s > 0 ? near : far).Add(new SP { x = x, y = y, X = u, Y = Y, Z = Z, lon = lon, lat = lat, q = c.q, r = c.r, id = id, e = c.e, corner = c.corner, rim = 1 - vz, sil = (1 - Math.Sqrt(q)) * R });
                        ids[s > 0 ? 0 : 1][(y + N) * W + x + N] = id;
                    }
                }
            var lists = new[] { near, far };
            for (int i = 0; i < 2; i++)
            {
                Dictionary<int, double> m = ids[i];
                double? At(int x, int y) => m.TryGetValue((y + N) * W + x + N, out double v) ? v : (double?)null;
                foreach (SP q in lists[i])
                {
                    double? r = At(q.x + 1, q.y), d = At(q.x, q.y + 1), l = At(q.x - 1, q.y), u = At(q.x, q.y - 1);
                    q.seam = (r != null && r.Value != q.id) || (d != null && d.Value != q.id);
                    q.bevel = !q.seam && ((l != null && l.Value != q.id) || (u != null && u.Value != q.id));
                }
            }
            return (near, far);
        }

        // fractures: jagged forked paths from the hit across the plates, laid out by longitude and latitude so they
        // bend with the dome; each point carries how far along it is, so they can race out from the hit
        static List<double[][]> Cracks(double R, double seed, Shot s)
        {
            var rn = new Rng(seed * 13 + 5);
            var o = new List<double[][]>();
            double[][] ToScreen(double[][] path, double u0, double u1)
            {
                var pts = new List<double[]>();
                for (int i = 0; i < path.Length; i++)
                {
                    double lon = path[i][0], lat = path[i][1];
                    if (lat < 0.03 || lat > 1.52 || Math.Abs(lon) > 1.5) break;
                    double[] pr = Project(R, lon, lat);
                    pts.Add(new[] { pr[0], pr[1], pr[2], u0 + ((u1 - u0) * i) / (path.Length - 1) });
                }
                return pts.ToArray();
            }
            for (int i = 0; i < 5; i++)
            {
                double a = (i / 5.0) * TAU + rn.Range(-0.35, 0.35), L = rn.Range(0.42, 0.8);
                double[][] path = Shapes.Bolt(rn, s.lon, s.lat, s.lon + (Px.Cos(a) * L) / Px.Cos(s.lat), s.lat + Px.Sin(a) * L, 0.2, 4);
                o.Add(ToScreen(path, 0, 1));
                foreach (double[][] f in Shapes.Forks(rn, path, rn.Int(0, 2), L * 0.5, 0.25, 0.9))
                {
                    int j = Array.FindIndex(path, q => q[0] == f[0][0] && q[1] == f[0][1]);
                    double u0 = (double)Math.Max(0, j) / (path.Length - 1);
                    o.Add(ToScreen(f, u0, u0 + 0.5));
                }
            }
            return o;
        }

        static bool Glints(double S, double q, double r) => Rand(S, q, r, 3) < 0.2;

        // when a plate glints: only in the quiet part of the loop, so nothing competes with the big hit
        static double GlintAt(double S, double q, double r)
        {
            double g = Rand(S, q, r) * 1.7;
            return g < 1.4 ? 1 + g : g - 1.4;
        }

        readonly uint[] C, SPK;
        readonly double R, S;
        readonly List<SP> nearShell, farShell;
        readonly List<double[][]> cracks;
        readonly ScifiLib.Ghosts g;

        ForceField(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            SPK = Fit(o.Ramp("sparks"), 4);
            R = o.Num("radius");
            S = o.Seed;
            (nearShell, farShell) = Shell(R);
            cracks = Cracks(R, S, SHOTS[0]);
            g = Ghosts(o);
        }

        // what the shots throw off and the glints on the plates, born once a loop
        ScifiLib.Ghosts Ghosts(Options o)
        {
            double W = SIZE_W - ORIGIN_X;
            var rn = new Rng(o.Seed);
            var gh = new ScifiLib.Ghosts(D, x => 0, Ramp(o.Ramp("sparks")), 0, new[] { -70, -80, W - 6, 14 });
            foreach (Shot s in SHOTS)
            {
                double[] h = Project(R, s.lon, s.lat);
                double hx = h[0], hy = h[1], n = s.big ? 1 : 0.55;
                // glancing off the curve: up and away to the right, the way the shot was going when it was turned
                for (int i = 0; i < 24 * n; i++)
                {
                    double a = (s.big ? -0.9 : -0.55) + rn.Range(-0.7, 0.65), sp = rn.Range(100, 280) * (0.7 + 0.3 * n);
                    gh.Add(s.t + rn.Range(0, 0.02), P.Spark, hx, hy, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, rn.Range(0.25, 0.75));
                }
                for (int i = 0; i < 7 * n; i++)
                {
                    double a = -0.9 + rn.Range(-1.2, 1.2), sp = rn.Range(30, 90);
                    gh.Add(s.t + rn.Range(0, 0.05), P.Mote, hx, hy, Px.Cos(a) * sp, Px.Sin(a) * sp - 15, rn.Range(0.3, 0.6), rn.Pick(new[] { C[5], C[6], C[4] }), 0.7);
                }
                for (int i = 0; i < 3 * n; i++) gh.Add(s.t + 0.04 + i * 0.07, P.Star, hx + rn.Range(-6, 6), hy + rn.Range(-5, 5), 0, -4, rn.Range(0.3, 0.5), C[6], rn.Range(1, 2));
                for (int i = 0; i < 3 * n; i++) gh.Add(s.t + 0.03 + i * 0.04, P.Smoke, hx + 2 + rn.Range(-2, 2), hy - 1, rn.Range(8, 22), rn.Range(-14, -4), rn.Range(0.9, 1.3), Hex("#5b5f78"), rn.Range(2.2, 3.4));
            }
            // a star on each plate as it glints
            for (int r = 0; r < 12; r++)
                for (int q = -14; q <= 14; q++)
                {
                    if (!Glints(o.Seed, q, r)) continue;
                    double lon = (S3 * (q + r / 2.0)) / G, lat = (1.5 * r) / (G * KY);
                    double[] pr = Project(R, lon, lat);
                    double x = pr[0], y = pr[1], f = pr[2];
                    if (f < 0.25 || lat < 0.15 || lat > 1.45 || Math.Abs(lon) > 1.3) continue;
                    gh.Add(GlintAt(o.Seed, q, r) + 0.03, P.Star, x, y, 0, -3, 0.4, C[6], 1.4 + Rand(q, r, 9));
                }
            return gh;
        }

        void DrawShell(Painter p, double t, bool near)
        {
            double ph = t / D;
            double[] ages = SHOTS.Select(s => ScifiLib.Since(t, s.t, D)).ToArray();
            // the scan: a band of light climbing the dome once a loop
            double sk = Clamp((ph - 0.72) / 0.26), scanLat = -0.2 + sk * 2, scanK = Px.Sin(Math.PI * sk);
            foreach (SP q in near ? nearShell : farShell)
            {
                double ff = q.rim * q.rim;
                // the ripples: a front racing out over the sphere from each hit, the plates behind it still humming
                double rp = 0, hot = 0;
                for (int i = 0; i < SHOTS.Length; i++)
                {
                    Shot s = SHOTS[i];
                    double age = ages[i];
                    if (age >= 1) continue;
                    double d = Px.Acos(Clamp(q.X * s.N[0] + q.Y * s.N[1] + q.Z * s.N[2], -1, 1)), f = 0.06 + 3.1 * age, k = (1 - age) * (s.big ? 1 : 0.7);
                    rp = Math.Max(Math.Max(rp, Exp(-Px.Pow((d - f) / 0.15, 2)) * k), d < f ? 0.22 * k * k * (1 - d / f) : 0);
                    double hr = s.big ? 0.3 : 0.17;
                    if (age < 0.55 && d < hr) hot = Math.Max(hot, age < 0.07 && d < hr * 0.75 ? 2 : Px.Pow(1 - age / 0.55, 1.5) * (1.1 - (0.6 * d) / hr));
                }
                // plates glint now and then
                double gl = 0;
                if (Glints(S, q.q, q.r))
                {
                    double a = ScifiLib.Since(t, GlintAt(S, q.q, q.r), D);
                    if (a < 0.45) gl = 1 - a / 0.45;
                }
                double sc = scanK * Exp(-Px.Pow((q.lat - scanLat) / 0.09, 2));
                if (!near)
                {
                    // the far side, seen through the glass: dim seams, a breath of tint
                    if (q.seam) p.Blend(q.x + 0.5, q.y + 0.5, rp > 0.3 ? C[5] : C[3], Clamp(0.18 + 0.25 * ff + 0.6 * rp + 0.3 * sc));
                    else p.Blend(q.x + 0.5, q.y + 0.5, C[2], 0.04 + 0.06 * ff + 0.15 * rp);
                    continue;
                }
                if (q.sil < 1.15)
                {
                    // the rim: a hard bright edge, whiter where the light catches it
                    double lit = -q.X * 0.55 + q.Y * 0.85;
                    p.Px(q.x, q.y, rp > 0.4 || lit > 0.8 ? WHITE : lit > 0.3 ? C[6] : C[5]);
                    p.Light(q.x, q.y, C[4], 0.3);
                    continue;
                }
                if (hot >= 2)
                {
                    // the hero frame: the struck plates go white
                    p.Px(q.x, q.y, q.seam ? C[6] : WHITE);
                    continue;
                }
                if (q.seam)
                {
                    double I = 0.12 + 0.5 * ff + 0.08 * Px.Sin(q.lon * 2.3 + q.lat * 3 - ph * TAU) + (q.corner < 0.32 ? 0.18 : 0) + 0.9 * gl + 0.7 * sc + 1.3 * rp + hot;
                    uint c = gl > 0.6 || rp > 0.55 || hot > 0.5 ? WHITE : I > 0.8 ? C[6] : I > 0.55 ? C[5] : C[4];
                    if (I > 0.36) p.Px(q.x, q.y, c);
                    else p.Blend(q.x + 0.5, q.y + 0.5, c, 0.3 + I);
                    p.Light(q.x, q.y, C[4], 0.04 + 0.08 * I);
                }
                else
                {
                    // the glass: thin face-on, thicker toward the rim, a lit top-left edge on every plate
                    double a = 0.05 + 0.34 * ff * q.rim + 0.1 * Px.Pow(q.e, 4) + (q.bevel ? 0.12 + 0.2 * ff : 0) + 0.35 * gl + 0.1 * sc + 0.3 * rp + 0.6 * hot;
                    uint c = hot > 0.6 ? WHITE : hot > 0.3 || gl > 0.5 ? C[6] : rp > 0.4 || q.bevel ? C[5] : ff > 0.5 ? C[4] : C[3];
                    p.Blend(q.x + 0.5, q.y + 0.5, c, Clamp(a));
                    if (rp > 0.1 || hot > 0 || gl > 0) p.Light(q.x, q.y, C[4], 0.06 * (rp + hot + gl));
                }
            }
        }

        // the dome's foot: a ring on the floor with emitters round it, chasing
        void DrawFoot(Painter p, double t, bool near)
        {
            Func<double, bool> keep = gg => (Px.Sin(gg) >= 0) == near;
            Sim.Band(p, 0, 0, R + 1, SA, 2, 1, new[] { C[1], C[3], C[5], C[6] }, keep);
            for (int i = 0; i < 10; i++)
            {
                double gg = (i / 10.0) * TAU + 0.31;
                if (!keep(gg)) continue;
                double x = Px.Cos(gg) * (R - 0.5), y = Px.Sin(gg) * R * SA, k = 0.5 + 0.5 * Px.Cos(TAU * (2 * (t / D) - i / 10.0));
                p.Px(x - 1, y, C[2]);
                p.Px(x + 1, y, C[2]);
                p.Px(x, y, k > 0.6 ? WHITE : C[5]);
                p.Glow(x, y, 6 + 4 * k, C[4], (near ? 0.25 : 0.15) * (0.4 + k));
            }
        }

        public override void DrawBack(Painter p, double t)
        {
            for (double x = -R; x <= R; x += R / 3) p.Glow(x, -1, 22, C[3], 0.1);
            DrawFoot(p, t, false);
            DrawShell(p, t, false);
        }

        static readonly int[][] OFFS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        public override void Draw(Painter p, double t)
        {
            double W = SIZE_W - ORIGIN_X;
            DrawShell(p, t, true);
            DrawFoot(p, t, true);
            foreach (Shot s in SHOTS)
            {
                double age = ScifiLib.Since(t, s.t, D);
                double[] h = Project(R, s.lon, s.lat);
                double hx = h[0], hy = h[1], n = s.big ? 1 : 0.6;
                // the fractures: race out from the hit white-hot, then cool down the ramp and fade
                if (s.big && age < 0.9)
                {
                    double fade = 1 - Smooth(0.35, 0.9, age), reach = Clamp(age / 0.09);
                    for (int pass = 0; pass <= 1; pass++)
                        foreach (double[][] c in cracks)
                            for (int i = 1; i < c.Length; i++)
                            {
                                double x0 = c[i - 1][0], y0 = c[i - 1][1], f0 = c[i - 1][2], x1 = c[i][0], y1 = c[i][1], f1 = c[i][2], u = c[i][3];
                                if (u > reach || f0 < 0.05 || f1 < 0.05) continue;
                                double k = fade * (1.2 - u * 0.4);
                                if (pass == 0)
                                {
                                    if (age < 0.32)
                                        foreach (int[] of in OFFS) p.Line(x0 + of[0], y0 + of[1], x1 + of[0], y1 + of[1], age < 0.12 ? C[6] : C[5], k * (1 - age / 0.32));
                                    continue;
                                }
                                p.Line(x0, y0, x1, y1, age < 0.12 + u * 0.08 ? WHITE : age < 0.3 + u * 0.1 ? C[6] : C[5], k);
                                if (i % 3 == 0) p.Glow(x1, y1, 6, C[4], 0.35 * fade * (1 - u * 0.5));
                            }
                }
                // the tracer, in from the right: a streak that comes out of the edge, its tail never past where it entered
                double tk = (t - (s.t - 0.1)) / 0.1;
                if (tk >= 0 && tk < 1)
                {
                    double sx = W - 3, sy = hy - 12, L = Hypot(sx - hx, sy - hy);
                    (double, double) At(double dd) => (sx + ((hx - sx) * Math.Max(0, dd)) / L, sy + ((hy - sy) * Math.Max(0, dd)) / L);
                    double hh = L * tk;
                    var (x, y) = At(hh);
                    var (x2, y2) = At(hh - 2);
                    var (x7, y7) = At(hh - 7);
                    var (x16, y16) = At(hh - 16);
                    p.Line(x16, y16, x7, y7, SPK[3]);
                    p.Line(x7, y7, x2, y2, SPK[1]);
                    p.Line(x2, y2, x, y, SPK[0]);
                    p.Px(x, y, WHITE);
                    p.Glow(x, y, 9, SPK[1], 0.55);
                }
                // the impact: a white star, light that flares and dies
                if (age < 0.14) Sim.Sparkle(p, hx, hy, (7 * (1 - age / 0.14) + 1) * n, C[6], C[5], 0.4);
                ScifiLib.Flash(p, t, s.t, hx, hy, 56 * n, C[4], 0.45, 0.45);
                ScifiLib.Flash(p, t, s.t, hx, hy, 12, WHITE, 0.5 * n, 0.08);
                ScifiLib.Flash(p, t, s.t, hx + 3, hy, 22, SPK[1], 0.4 * n, 0.15);
            }
            // the dome's own light, and what glances off
            double a0 = ScifiLib.Since(t, SHOTS[0].t, D);
            p.Glow(0, -R * 0.45, R * 1.3, C[3], 0.11 + 0.1 * Math.Max(0, 1 - a0 * 2));
            g.At(t).Draw(p);
        }
    }
}
