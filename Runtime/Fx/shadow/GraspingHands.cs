using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class GraspingHands : Effect
    {
        public static readonly Module Module = new Module("grasping-hands", "pixelfx/fx/shadow/grasping-hands", 200, 116, 100, 100, 3.1, false, 30,
            new Options { ["palette"] = VOID, ["seed"] = 5 },
            (o, e) => new GraspingHands(o, e))
        {
            Title = "Grasping hands",
            Desc = "Shadow arms burst up out of a dark pool with a splash, clench their hands into claws, strain, and are dragged back under, fingers clawing at the air.",
            Tags = new[] { "shadow", "hands", "arms", "grab", "ground", "dark", "magic", "one-shot" },
            Bg = "ground",
        };

        const double RX = 42, OPEN = 0.32, SHUT = 2.35, LX = -0.6, LY = -0.8;

        sealed class Snap
        {
            public double x, y, t0;
        }

        sealed class Arm
        {
            public double x, y, L, W, lean, bend, curve, curl, t0, td, ph;
            public bool back, up, shut, down;
            public Snap snap;
        }

        sealed class APt
        {
            public double x, y, th, w, u;
        }

        // an arm at t: it rises out of the pool along its own shape, bent at the elbow, swaying; returns its spine
        static List<APt> ArmAt(Arm A, double t)
        {
            double e = Clamp((t - A.t0) / 0.22), d = Clamp((t - A.td) / 0.34);
            if (e <= 0 || d >= 1) return null;
            double rise = Ease.OutBack(e, 1.6) * (1 - Ease.InQuad(d)), hidden = (1 - rise) * A.L;
            double grip = GripOf(A, t), strain = t > A.t0 + 0.5 && t < A.td ? Px.Sin(t * 47 + A.ph) * 0.03 : 0;
            double elbow = A.bend * (0.3 + 0.7 * grip) * (1 - d) + strain, lean = A.lean + 0.06 * Px.Sin(t * 2.2 + A.ph) + d * A.lean * 0.6;
            double th = -Math.PI / 2 + lean, x = A.x - Px.Cos(th) * hidden, y = A.y - Px.Sin(th) * hidden;
            var pts = new List<APt>();
            double n = Math.Ceiling(A.L / 0.5);
            for (int i = 0; i <= n; i++)
            {
                double s = i * 0.5, u = s / A.L;
                pts.Add(new APt { x = x, y = y, th = th, w = A.W * (1 - 0.5 * u) + 0.4, u = u });
                th += 0.5 * (elbow * Exp(-Px.Pow((u - 0.52) / 0.07, 2)) / (0.07 * Math.Sqrt(Math.PI) * A.L) + A.curve / A.L);
                x += Px.Cos(th) * 0.5;
                y += Px.Sin(th) * 0.5;
            }
            return pts;
        }

        // how closed the hand is: opens as it rises, snaps shut into a claw, splays as it's dragged down
        static double GripOf(Arm A, double t)
        {
            double shut = Ease.OutBack(Clamp((t - A.t0 - 0.3) / 0.12), 2.2), splay = Smooth(A.td, A.td + 0.12, t);
            return shut * (1 - splay) - 0.35 * splay;
        }

        // the arm in cross-sections, lit like the tendrils: a near-black body, a bright edge toward the light
        static void DrawArm(Painter p, Arm A, List<APt> pts, uint[] C)
        {
            double surf = A.y;
            foreach (APt q in pts)
            {
                if (q.y > surf) continue;
                double nx = -Px.Sin(q.th), ny = Px.Cos(q.th), ks = nx * LX + ny * LY >= 0 ? 1 : -1;
                for (double v = -q.w; v <= q.w; v += 0.5)
                {
                    double x = q.x + nx * v, y = q.y + ny * v, u = (v / q.w) * ks;
                    if (y <= surf) p.Px(x, y, u > 0.55 ? C[2] : u > -0.45 ? C[1] : C[0]);
                }
            }
            foreach (APt q in pts)
            {
                if (q.y > surf) continue;
                double nx = -Px.Sin(q.th), ny = Px.Cos(q.th), f = nx * LX + ny * LY, ks = f >= 0 ? 1 : -1, l = ks * q.w;
                if (q.y - ny * l <= surf) p.Px(q.x - nx * l, q.y - ny * l, C[3]);
                if (q.y + ny * l <= surf) p.Px(q.x + nx * l, q.y + ny * l, Math.Abs(f) > 0.8 ? C[7] : C[6]);
            }
            APt b = pts.Find(q => q.y <= surf);
            if (b != null) p.Line(b.x - b.w - 1.5, surf, b.x + b.w + 1.5, surf, C[5]);
        }

        // a hand on the arm's end: a palm, four fingers and a thumb, each two bones with a lit edge and a pale claw
        static void DrawHand(Painter p, Arm A, List<APt> pts, uint[] C, double t)
        {
            APt tip = pts[pts.Count - 1];
            if (tip.y > A.y - 2) return;
            double th = tip.th, dx = Px.Cos(th), dy = Px.Sin(th), nx = -dy, ny = dx, g = GripOf(A, t), side = A.curl;
            double cx = tip.x + dx * 2.6, cy = tip.y + dy * 2.6;
            p.Shade(cx - 5, cy - 5, cx + 5, cy + 5, (x, y, o) =>
            {
                double a = (x - cx) * dx + (y - cy) * dy, b = (x - cx) * nx + (y - cy) * ny, d = Px.Pow(a / 3, 2) + Px.Pow(b / 2.7, 2);
                if (d > 1.25) return 0;
                if (d > 1) return b * side < 0 ? C[6] : C[3];
                return b * side < -1 ? C[2] : C[1];
            });
            (double, double) Bone(double x0, double y0, double ang, double len, bool last)
            {
                double x1 = x0 + Px.Cos(ang) * len, y1 = y0 + Px.Sin(ang) * len, ox = Round(-Px.Sin(ang) * side * -1), oy = Round(Px.Cos(ang) * side * -1);
                p.Line(x0, y0, x1, y1, C[1]);
                p.Line(x0 + ox, y0 + oy, x1 + ox, y1 + oy, C[5]);
                if (last)
                {
                    p.Px(x1, y1, WHITE);
                    p.Px(x1 + Px.Cos(ang), y1 + Px.Sin(ang), C[7]);
                }
                return (x1, y1);
            }
            double spread = 1 - 0.65 * Math.Max(0, g), curl = g * 1.2;
            double[] fingers = { -2.2, -0.75, 0.75, 2.2 };
            for (int i = 0; i < fingers.Length; i++)
            {
                double o = fingers[i];
                double bx = cx + dx * 2.6 + nx * o * 0.9, by = cy + dy * 2.6 + ny * o * 0.9, len = i == 0 || i == 3 ? 2.6 : 3.2;
                double a0 = th + o * 0.2 * spread;
                var (fx, fy) = Bone(bx, by, a0 + curl * side * 0.6, len, false);
                Bone(fx, fy, a0 + curl * side * 1.5, len * 0.85, true);
            }
            var (jx, jy) = Bone(cx - nx * side * 2.4, cy - ny * side * 2.4, th - side * (1 - 0.6 * g), 2.4, false);
            Bone(jx, jy, th - side * (0.6 - 1.4 * g), 2, true);
            p.Glow(cx, cy, 8, C[5], 0.18 + 0.3 * Math.Max(0, g));
        }

        readonly uint[] C, DIM;
        readonly Rng R;
        readonly Particles ps;
        readonly Puffs puffs;
        readonly Drops drops;
        readonly List<Ripple> ripples = new List<Ripple>();
        readonly List<Splat> splats = new List<Splat>();
        readonly List<Lamp> lights = new List<Lamp>();
        readonly Arm[] arms;
        double t, acc;

        double Rx(double tt) => RX * Ease.OutCubic(Clamp(tt / OPEN)) * (1 - Ease.InCubic(Clamp((tt - SHUT) / 0.45)));

        GraspingHands(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            DIM = new[] { C[0], C[0], C[1], C[1], C[3], C[4], C[5], C[6] };
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, haze: C[4], emitter: e);
            puffs = new Puffs(R, new[] { C[7], C[6], C[5], C[4], C[3], C[2], C[1], C[1], C[0] }, new[] { Mix(C[3], C[2], 0.55), Mix(C[2], C[1], 0.5) }, Mix(C[4], C[2], 0.5), new[] { C[6], C[5], C[4] }, floor, e);
            drops = new Drops(onLand: (x, y, v, s) =>
            {
                if (Math.Abs(x) < Rx(t) * 0.95) ripples.Add(new Ripple { x = x, t0 = t, life = 0.5, w = 0.6 });
                else splats.Add(new Splat { x = x, t0 = t, s = s });
            });
            // four arms: two at the back, taller and dimmer, two in front; elbows bending toward the middle
            double[][] LAY = { new double[] { -20, -2.5, 1 }, new double[] { 17, -2.5, -1 }, new double[] { -30, 2.5, 1 }, new double[] { 28, 2.5, -1 } };
            int[] up = { 1, 3, 0, 2 }, down = { 2, 0, 3, 1 };
            arms = new Arm[LAY.Length];
            for (int i = 0; i < LAY.Length; i++)
            {
                double x = LAY[i][0], y = LAY[i][1], toward = LAY[i][2];
                arms[i] = new Arm
                {
                    x = x + R.Range(-3, 3),
                    y = y,
                    back = y < 0,
                    L = y < 0 ? R.Range(44, 52) : R.Range(36, 44),
                    W = y < 0 ? R.Range(3.2, 3.8) : R.Range(3.6, 4.2),
                    lean = -toward * R.Range(0.16, 0.3),
                    bend = toward * R.Range(0.35, 0.6),
                    curve = toward * R.Range(0.05, 0.15),
                    curl = toward,
                    t0 = 0.3 + up[i] * 0.06 + R.Range(0, 0.03),
                    td = 1.45 + down[i] * 0.09 + R.Range(0, 0.05),
                    ph = R.Range(0, TAU),
                };
            }
        }

        void Splash(double x, double y, int n, double k)
        {
            for (int i = 0; i < n; i++) drops.Add(x + R.Range(-2, 2), y - 1, R.Range(-70, 70) * k, -R.Range(90, 220) * k, R.Chance(0.4) ? 2 : 1);
            ripples.Add(new Ripple { x = x, t0 = t, life = 0.7, w = 1 });
            lights.Add(new Lamp(x, y - 7, 30, 0, 0.55 * k, 0.25));
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Arm A in arms)
            {
                if (!A.up && t >= A.t0)
                {
                    A.up = true;
                    Splash(A.x, A.y, 12, 1);
                }
                if (!A.shut && t >= A.t0 + 0.36)
                {
                    // the clench: a snap of light at the fist, a puff of dark breath, a few sparks
                    A.shut = true;
                    List<APt> pts = ArmAt(A, t);
                    if (pts != null)
                    {
                        APt q = pts[pts.Count - 1];
                        lights.Add(new Lamp(q.x, q.y, 22, 0, 0.7, 0.2));
                        A.snap = new Snap { x = q.x + Px.Cos(q.th) * 4, y = q.y + Px.Sin(q.th) * 4, t0 = t };
                        for (int i = 0; i < 6; i++)
                        {
                            double a = R.Range(0, TAU), sp = R.Range(60, 130);
                            ps.Spawn(P.Spark, A.snap.x, A.snap.y, Px.Cos(a) * sp, Px.Sin(a) * sp - 20, R.Range(0.12, 0.3));
                        }
                        puffs.Spawn(PK.Soot, A.snap.x, A.snap.y, 0, -12, 0.6, 3.5);
                    }
                }
                if (!A.down && t >= A.td + 0.28)
                {
                    A.down = true;
                    Splash(A.x, A.y, 8, 0.7);
                }
            }
            double r = Rx(t);
            if (r > 8 && t < SHUT - 0.4)
            {
                for (acc += dt * 8; acc > 1; acc--) puffs.Spawn(PK.Soot, R.Range(-0.8, 0.8) * r, -R.Range(1, 4), R.Range(-8, 8), -R.Range(14, 28), R.Range(0.6, 0.85), R.Range(3, 6));
                if (R.Chance(dt * 12)) ps.Spawn(P.Mote, R.Range(-0.8, 0.8) * r, -2, R.Range(-8, 8), -R.Range(30, 70), R.Range(0.5, 0.9), R.Pick(new[] { C[7], C[6], C[5] }), R.Range(0.6, 1));
                if (R.Chance(dt * 6)) ps.Spawn(P.Vapour, R.Range(-0.7, 0.7) * r, -R.Range(2, 8), R.Range(-6, 6), -R.Range(10, 22), R.Range(0.6, 0.9), R.Chance(0.5) ? C[3] : C[4], R.Range(3, 6));
                if (R.Chance(dt * 3)) ripples.Add(new Ripple { x = R.Range(-0.7, 0.7) * r, t0 = t, life = 0.6, w = 0.5 });
            }
            for (int i = ripples.Count - 1; i >= 0; i--)
                if (t - ripples[i].t0 > ripples[i].life) ripples.RemoveAt(i);
            for (int i = splats.Count - 1; i >= 0; i--)
                if (t - splats[i].t0 > 0.6) splats.RemoveAt(i);
            for (int i = lights.Count - 1; i >= 0; i--)
                if (!lights[i].Tick(dt)) lights.RemoveAt(i);
            drops.Update(dt);
            puffs.Update(dt);
            ps.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double r = Rx(t), k = r / RX;
            FloorPool(p, C, r, t, ripples);
            if (k > 0)
            {
                p.Glow(0, -16, 80, C[4], 0.2 * k);
                p.Glow(0, -8, r * 1.7 + 12, C[4], 0.3 * k);
                p.Glow(0, 0, r * 1.15, C[5], 0.16 * k);
            }
            puffs.Draw(p, PK.Soot, new PuffLight(0, 6, k, 70));
            ps.Draw(p, Layer.Back);
            foreach (Arm A in arms)
                if (A.back)
                {
                    List<APt> pts = ArmAt(A, t);
                    if (pts != null)
                    {
                        DrawArm(p, A, pts, DIM);
                        DrawHand(p, A, pts, DIM, t);
                    }
                }
        }

        public override void Draw(Painter p)
        {
            foreach (Arm A in arms)
                if (!A.back)
                {
                    List<APt> pts = ArmAt(A, t);
                    if (pts != null)
                    {
                        DrawArm(p, A, pts, C);
                        DrawHand(p, A, pts, C, t);
                    }
                }
            foreach (Arm A in arms)
                if (A.snap != null && t - A.snap.t0 < 0.22)
                {
                    double u = (t - A.snap.t0) / 0.22;
                    RingFast(p, A.snap.x, A.snap.y, 2 + 5 * Ease.OutCubic(u), 1, u < 0.4 ? C[6] : u < 0.7 ? C[5] : C[3]);
                }
            drops.Draw(p, C[1], C[5]);
            foreach (Splat s in splats)
            {
                double a = 1 - (t - s.t0) / 0.6;
                p.Dot(s.x, -0.5, C[1], a * 1.5);
                p.Dot(s.x + 1, -0.5, C[2], a);
                p.Dot(s.x, -1.5, C[5], a);
            }
            ps.Draw(p, Layer.Front);
            foreach (Lamp l in lights)
            {
                double q = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.6 + 0.4 * q), C[5], l.k * q * q);
            }
        }
    }
}
