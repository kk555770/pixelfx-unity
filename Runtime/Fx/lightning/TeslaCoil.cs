// The arcs (forking bolts re-struck every frame, inked with halo, body, core and light), their flicker and the
// zaps that strike the floor in a spray of sparks follow Sprite Fusion's open-source lightning and energy orb
// (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class TeslaCoil : Effect
    {
        public static readonly Module Module = new Module("tesla-coil", "pixelfx/fx/lightning/tesla-coil", 224, 136, 112, 120, (double)N / FPS, true, 30,
            new Options
            {
                ["palette"] = VOLT,
                ["sparks"] = PLASMA,
                ["copper"] = new[] { "#2a120c", "#5a2814", "#8e4a1e", "#c4772f", "#eaa65a", "#ffd9a0" },
                ["steel"] = new[] { "#0f121c", "#1f2433", "#353d52", "#556078", "#7f8ba3", "#b8c3d6", "#f2f6ff" },
                ["seed"] = 4,
            },
            (o, e) => new TeslaCoil(o, e))
        {
            Title = "Tesla coil",
            Desc = "A copper coil under a steel torus throws branching arcs that lash out in volleys and now and then strike the floor in a spray of sparks, lighting the metal and the ground.",
            Tags = new[] { "lightning", "electric", "tesla", "machine", "prop", "loop" },
            Bg = "ground",
        };

        const int FPS = 30, N = 72;
        const double P = (double)N / FPS, TY = -57, RR = 12, TR = 3.6, SY = 0.45, CTOP = -50, CBOT = -12;
        static readonly uint[] IRON = Ramp("#120f18", "#1e1a27", "#2c2637", "#3f374d", "#5a5068");
        static readonly uint ASH = Hex("#5b5566");
        // an arc's brightness over the frames it lives: struck, flickered, re-struck, gone
        static readonly double[] FL = { 1, 0.7, 0.95, 0.55, 0.85, 0.45, 0.7, 0.35 };
        static readonly double[] SK = { 1, 0.75, 1, 0.6, 0.85, 0.4 };

        sealed class ArcPlan
        {
            public int f0, len, side;
            public double phi, th0, th1, L;
            public bool front;
        }

        sealed class StrikePlan
        {
            public int f0, side;
            public double x, phi;
        }

        // the loop's events, frame by frame: volleys of arcs off the torus, and two zaps to the floor
        static (List<ArcPlan> arcs, List<StrikePlan> strikes) Plan(double seed)
        {
            Rng r = NewRng(seed, 1);
            var arcs = new List<ArcPlan>();
            var strikes = new List<StrikePlan>();
            foreach (int[] vn in new[] { new[] { 2, 3 }, new[] { 20, 3 }, new[] { 30, 1 }, new[] { 38, 3 }, new[] { 56, 3 }, new[] { 66, 1 } })
            {
                int v = vn[0], n = vn[1];
                for (int j = 0; j < n + r.Int(0, 1); j++)
                {
                    int side = r.Chance(0.5) ? 1 : -1;
                    bool front = r.Chance(0.55);
                    double a = r.Range(0.15, 1.1);
                    double phi = side > 0 ? (front ? a : -a) : front ? Math.PI - a : Math.PI + a, th0 = r.Range(-1.15, 0.35);
                    var q = new ArcPlan { f0 = (v + j * r.Int(2, 4)) % N };
                    q.len = r.Int(3, 7);
                    q.side = side;
                    q.phi = phi;
                    q.front = front;
                    q.th0 = th0;
                    q.th1 = th0 + r.Range(-0.6, 0.6);
                    q.L = r.Range(24, 52);
                    arcs.Add(q);
                }
            }
            foreach (int[] fs in new[] { new[] { 14, 1 }, new[] { 47, -1 } })
            {
                int side = fs[1];
                strikes.Add(new StrikePlan { f0 = fs[0], side = side, x = side * r.Range(38, 56), phi = side > 0 ? 0.4 : Math.PI - 0.4 });
            }
            return (arcs, strikes);
        }

        static int AgeOf(int f, int f0) => (((f - f0) % N) + N) % N;
        static double[] Root(double phi, double up = 0) => new[] { Px.Cos(phi) * (RR + TR * 0.6), TY + Px.Sin(phi) * RR * SY - up };

        // the coil: an iron plinth, two thick primary turns, the secondary wound in copper wire, catching arc light
        static void DrawCoil(Painter p, uint[] Cu, double[] lit, uint[] C)
        {
            int n = Cu.Length;
            p.Shade(-14, -8, 14, 0, (x, y, o) =>
            {
                if (x < -13 || x > 13 || y < -7) return 0;
                int edge = x < -12 ? 2 : x > 12 ? 0 : 1;
                o.c = y < -6 ? IRON[4] : y < -5 ? IRON[3] : y > -1 ? IRON[0] : IRON[edge + 1 > 2 ? 2 : edge + (y < -3 ? 1 : 0)];
                return 0;
            });
            foreach (int rx in new[] { -10, 10 })
            {
                p.Px(rx, -4, IRON[4]);
                p.Px(rx, -3, IRON[0]);
            }
            void Turn(double y, bool front)
            {
                for (double a = 0; a < TAU; a += 0.04)
                {
                    if ((Px.Sin(a) >= 0) != front) continue;
                    double x = Px.Cos(a) * 10, yy = y + Px.Sin(a) * 2.2, l = 0.5 - 0.4 * Px.Cos(a) + (front ? 0.25 : -0.2);
                    p.Px(x, yy, Cu[(int)Clamp(Round(l * (n - 1)), 0, n - 1)]);
                    p.Px(x, yy + 1, Cu[(int)Clamp(Round(l * (n - 1)) - 2, 0, n - 1)]);
                }
            }
            Turn(-10.5, false);
            Turn(-13.5, false);
            // the secondary: wire on every other row, lit from the left, a bright band down the front, arc light on the rims
            p.Shade(-6, CTOP, 6, CBOT, (x, y, o) =>
            {
                if (x < -5 || x > 5) return 0;
                double u = (x + 5.5) / 11, nx = u * 2 - 1;
                bool wire = (ToInt32(Math.Floor(y)) & 1) == 0;
                double l = 0.62 - 0.55 * nx - 0.35 * nx * nx + (Math.Abs(nx + 0.35) < 0.12 ? 0.3 : 0);
                if (!wire) l -= 0.32;
                o.c = Cu[(int)Clamp(Round(l * (n - 1)), 0, n - 1)];
                if (wire && ((nx < -0.75 && lit[0] > 0.25) || (nx > 0.75 && lit[1] > 0.25))) o.c = nx < 0 ? (lit[0] > 0.6 ? C[6] : C[4]) : lit[1] > 0.6 ? C[6] : C[4];
                return 0;
            });
            p.Line(-5, CTOP - 1, 5, CTOP - 1, IRON[3]);
            p.Line(-4, CTOP - 2, 4, CTOP - 2, IRON[2]);
            Turn(-10.5, true);
            Turn(-13.5, true);
        }

        sealed class TorusMap
        {
            public int X0, Y0, W, H;
            public float[] z, n;
            public double ca, sa;
        }

        // the torus, solved once: for every pixel, the normal of the nearest point of a ring tube seen from a little above
        static TorusMap TORUS;

        static TorusMap Torus()
        {
            if (TORUS != null) return TORUS;
            double sa = SY, ca = Math.Sqrt(1 - SY * SY);
            int X0 = -(int)Math.Ceiling(RR + TR) - 1, Y0 = (int)Math.Floor(TY - RR * SY - TR) - 2;
            int W = 2 * -X0 + 2, H = (int)Math.Ceiling(TY + RR * SY + TR) + 3 - Y0;
            var z = new float[W * H];
            for (int i = 0; i < z.Length; i++) z[i] = -1e9f;
            var n = new float[W * H * 3];
            for (int i = 0; i < 480; i++)
            {
                double phi = (i / 480.0) * TAU, cp = Px.Cos(phi), sp = Px.Sin(phi);
                for (int j = 0; j < 96; j++)
                {
                    double psi = (j / 96.0) * TAU, cs = Px.Cos(psi), ss = Px.Sin(psi), rr = RR + TR * cs, X = rr * cp, Y = TR * ss, Z = rr * sp;
                    double px = Math.Floor(X) - X0, py = Math.Floor(TY - Y * ca + Z * sa) - Y0, d = Z * ca + Y * sa;
                    if (px < 0 || py < 0 || px >= W || py >= H) continue;
                    int k = (int)py * W + (int)px;
                    if (d > z[k])
                    {
                        z[k] = (float)d;
                        n[k * 3] = (float)(cs * cp);
                        n[k * 3 + 1] = (float)ss;
                        n[k * 3 + 2] = (float)(cs * sp);
                    }
                }
            }
            return TORUS = new TorusMap { X0 = X0, Y0 = Y0, W = W, H = H, z = z, n = n, ca = ca, sa = sa };
        }

        static double[] Norm(double x, double y, double z)
        {
            double l = Px.Hypot(x, y, z);
            return new[] { x / l, y / l, z / l };
        }

        static readonly double[] KEY = Norm(-0.55, 0.75, 0.45);

        // steel shaded by a key light from the upper left, a hard specular, dark inner faces, the arcs' light on the sides
        static void DrawTorus(Painter p, uint[] St, uint[] C, double[] lit)
        {
            TorusMap T = Torus();
            int m = St.Length;
            double[] HALF = Norm(KEY[0], KEY[1] + T.sa, KEY[2] + T.ca);
            bool On(int x, int y) => x >= 0 && y >= 0 && x < T.W && y < T.H && T.z[y * T.W + x] > -1e8;
            for (int y = 0; y < T.H; y++)
                for (int x = 0; x < T.W; x++)
                {
                    int k = y * T.W + x;
                    if (T.z[k] < -1e8) continue;
                    double nx = T.n[k * 3], ny = T.n[k * 3 + 1], nz = T.n[k * 3 + 2];
                    double dif = Math.Max(0, nx * KEY[0] + ny * KEY[1] + nz * KEY[2]), spec = Px.Pow(Math.Max(0, nx * HALF[0] + ny * HALF[1] + nz * HALF[2]), 28);
                    double inner = nx * (x + T.X0 + 0.5) + nz * 0 < 0 && Math.Abs(x + T.X0 + 0.5) < RR ? 0.75 : 1;
                    uint c = spec > 0.55 ? St[m - 1] : St[(int)Clamp(Math.Floor((0.12 + 0.8 * dif * inner + spec) * (m - 1.01)), 0, m - 2)];
                    for (int si = 0; si < 2; si++)
                    {
                        double s = si == 0 ? -1 : 1, l = lit[si];
                        double r = Math.Max(0, s * nx * 0.85 + ny * 0.25 + nz * 0.45) * Math.Min(1, l);
                        if (r > 0.88) c = C[6];
                        else if (r > 0.62) c = Mix(c, C[5], 0.6);
                        else if (r > 0.38) c = Mix(c, C[4], 0.3);
                    }
                    if (!On(x, y + 1) && ny < 0.3) c = St[0];
                    p.Px(x + T.X0, y + T.Y0, c);
                }
        }

        readonly Options o;

        TeslaCoil(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), Cu = Fit(o.Ramp("copper"), 6), St = Fit(o.Ramp("steel"), 7), S = Ramp(o.Ramp("sparks"));
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            var (arcs, strikes) = Plan(o.Seed);
            double[] lit = { 0, 0 };
            var live = arcs.FindAll(a => AgeOf(f, a.f0) < a.len);
            foreach (ArcPlan a in live) lit[a.side > 0 ? 1 : 0] += FL[AgeOf(f, a.f0)] * 0.6;
            foreach (StrikePlan s in strikes)
            {
                int a = AgeOf(f, s.f0);
                if (a < 6) lit[s.side > 0 ? 1 : 0] += 1;
            }
            double act = Math.Min(1, (lit[0] + lit[1]) * 0.5);

            // the floor: a cold pool of light under the coil, scorches and smoke where zaps landed
            p.Glow(0, -10, 58, C[3], 0.14 + 0.25 * act);
            p.Ellipse(0, 0.5, 15, 1.6, IRON[0]);
            foreach (StrikePlan s in strikes)
            {
                double age = (((t - (double)s.f0 / FPS) % P) + P) % P;
                Scorch(p, s.x, 0, 7, Px.Pow(Clamp(1 - age / 1.1), 1.3), 1 - Clamp((age - 1.4) / 0.6));
                for (int j = 0; j < 2; j++)
                {
                    double u = (age - 0.12 - j * 0.18) / 1.2;
                    if (u > 0 && u < 1) Puff(p, s.x + (j != 0 ? 3 : -2) + u * 5 * s.side, -3 - u * 16 - j * 2, 2.6 + j, u, Mix(ASH, C[2], 0.3));
                }
            }

            void DrawArc(ArcPlan a)
            {
                int age = AgeOf(f, a.f0);
                double u = age / (double)Math.Max(1, a.len - 1), th = a.th0 + (a.th1 - a.th0) * Ease.OutQuad(u), ang = a.side > 0 ? th : Math.PI - th;
                double[] r0 = Root(a.phi);
                double x0 = r0[0], y0 = r0[1], L = a.L * (age == 0 ? 0.75 : 1);
                Rng r = NewRng(o.Seed, 21, a.f0, f);
                Stroke(p, Zigzag(r, x0, y0, x0 + Px.Cos(ang) * L, y0 + Px.Sin(ang) * L, 0.32, 0.75), FL[age], C, 0.6, new StrokeOpt { light = 0.4 });
                p.Glow(x0, y0, 10, C[5], 0.5 * FL[age]);
            }
            // arcs off the back of the torus go behind the coil
            foreach (ArcPlan a in live) if (!a.front) DrawArc(a);
            DrawCoil(p, Cu, lit, C);
            DrawTorus(p, St, C, lit);
            // corona: a faint violet fuzz crawling over the torus
            Rng rc = NewRng(o.Seed, 9, f);
            for (int i = 0; i < 6; i++)
            {
                double phi = rc.Range(0, TAU), d = rc.Range(1, 3), x = Px.Cos(phi) * (RR + TR + d), y = TY + Px.Sin(phi) * (RR * SY + TR + d * 0.7);
                p.Px(x, y, rc.Chance(0.3) ? C[5] : C[3]);
                if (rc.Chance(0.4)) p.Px(x + Px.Cos(phi), y + Px.Sin(phi) * 0.6, C[2]);
                p.Glow(x, y, 4, C[3], 0.3);
            }
            // brush discharge: short streamers hiss off the rim every frame
            for (int i = 0; i < 2; i++)
            {
                double phi = rc.Range(0, TAU), x0 = Px.Cos(phi) * (RR + TR * 0.8), y0 = TY + Px.Sin(phi) * (RR * SY + TR * 0.6), a = Atan2(Px.Sin(phi) * 0.6 - 0.5, Px.Cos(phi)) + rc.Range(-0.5, 0.5), l = rc.Range(4, 10);
                Crackle(p, Zigzag(rc, x0, y0, x0 + Px.Cos(a) * l, y0 + Px.Sin(a) * l, 0.4, 0.4), rc.Range(0.35, 0.65), C, 0.3);
            }
            p.Glow(0, TY, 34, C[3], 0.18 + 0.3 * act);
            foreach (ArcPlan a in live) if (a.front) DrawArc(a);

            // zaps to the floor: a full bolt, a flash where it lands, sparks thrown and bouncing
            foreach (StrikePlan s in strikes)
            {
                int age = AgeOf(f, s.f0);
                if (age < 6)
                {
                    double k = SK[age];
                    double[] r0 = Root(s.phi, -2);
                    double x0 = r0[0], y0 = r0[1];
                    Rng r = NewRng(o.Seed, 41, s.f0, f);
                    Stroke(p, Zigzag(r, x0, y0, s.x, 0, 0.22, 0.55), k, C, age == 0 ? 0.95 : 0.8, new StrokeOpt { hero = age == 0 });
                    p.Circle(s.x, -1, 3 * k + 1.5, C[2]);
                    p.Circle(s.x, -1, 3 * k + 0.5, C[5]);
                    p.Circle(s.x, -1, 2 * k, WHITE);
                    p.Glow(s.x, -10, 40, C[4], 0.55 * k);
                    p.Glow(s.x, -3, 12, C[6], 0.5 * k);
                    if (age < 2) p.Glow(0, -50, 120, C[3], 0.25 * k);
                }
                double st = (((t - (double)s.f0 / FPS) % P) + P) % P;
                for (int j = 0; j < 18; j++)
                {
                    Rng r = NewRng(o.Seed, 51, s.f0, j);
                    double life = r.Range(0.3, 0.65);
                    if (st >= life) continue;
                    double a = -Math.PI / 2 + r.Range(-1.3, 1.3), sp = r.Range(50, 190);
                    double[] q = Flight(s.x, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, st, floor: 0);
                    Spark(p, q[0], q[1], q[2], q[3], 1 - st / life, S);
                }
            }
            // sparks spat off the arc tips as they strike
            foreach (ArcPlan a in arcs)
            {
                double age = (((t - (double)a.f0 / FPS) % P) + P) % P, ang = a.side > 0 ? a.th0 : Math.PI - a.th0;
                double[] r0 = Root(a.phi);
                double x0 = r0[0], y0 = r0[1];
                for (int j = 0; j < 3; j++)
                {
                    Rng r = NewRng(o.Seed, 61, a.f0, j);
                    double life = r.Range(0.15, 0.35);
                    if (age >= life) continue;
                    double tx = x0 + Px.Cos(ang) * a.L * 0.75, ty = y0 + Px.Sin(ang) * a.L * 0.75, b = ang + r.Range(-0.9, 0.9), sp = r.Range(50, 150);
                    double[] q = Flight(tx, ty, Px.Cos(b) * sp, Px.Sin(b) * sp, age, floor: 0);
                    Spark(p, q[0], q[1], q[2], q[3], 1 - age / life, S);
                }
            }
        }
    }
}
