// The flutter (a swing that slows at its ends, a tilt that follows it, a paper flip that shows the back) adapts
// Sprite Fusion's open-source falling leaves (https://www.spritefusion.com/pixel-art-vfx/falling-leaves),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Confetti : Effect
    {
        const double DURATION = 3.4;

        public static readonly Module Module = new Module("confetti", "pixelfx/fx/ui/confetti", 200, 140, 100, 126, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#ff4a5a", "#ff9a2a", "#ffe03c", "#5ad84a", "#3cc8ff", "#7a6cff", "#f53cd1" },
                ["seed"] = 11,
            },
            (o, e) => new Confetti(o, e))
        {
            Title = "Confetti",
            Desc = "Two party poppers bang one after the other: paper bits and curly streamers burst out, stall, then tumble and flutter down, flipping, and settle on the floor.",
            Tags = new[] { "ui", "celebration", "confetti", "party", "reward", "one-shot" },
            Bg = "ground",
        };

        sealed class Popper
        {
            public double x, y, a, t;
        }

        static readonly Popper[] POPPERS =
        {
            new Popper { x = -74, y = -1, a = -Math.PI / 2 + 0.62, t = 0.1 },
            new Popper { x = 74, y = -1, a = -Math.PI / 2 - 0.62, t = 0.26 },
        };
        static readonly uint INK = Hex("#141019");

        // a party popper: a striped cone pointing along a, kicked back k px when it fires
        static void DrawPopper(Painter p, double x, double y, double a, double k, uint[] C, double alpha)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a), bx = x - ca * k, by = y - sa * k;
            p.Shade(bx - 12, by - 12, bx + 12, by + 12, (px, py, o) =>
            {
                double dx = px - bx, dy = py - by, u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                // u along the cone (0 at its tip on the floor, 11 at its mouth), v across
                if (u < 0 || u > 11) return 0;
                double hw = 0.8 + u * 0.32;
                if (Math.Abs(v) > hw + 0.6) return 0;
                o.a = alpha;
                if (Math.Abs(v) > hw - 0.4 || u > 10.2 || u < 0.8) return INK;
                bool stripe = Math.Floor((u + v * 0.6) / 2.2) % 2 == 0;
                return stripe ? (v < 0 ? C[2] : Scale(C[2], 0.75)) : v < 0 ? WHITE : Hex("#c8c0d8");
            });
        }

        sealed class Bit
        {
            public double x, y, vx, vy, w, h, ang, spin, flip, fs, ph, amp, fw, x0, rest, fall;
            public uint c;
            public int mode;
        }

        sealed class Ribbon
        {
            public double[][] pts;
            public double vx, vy, ph, amp, fw, x0, curl, fall;
            public uint c;
            public int mode;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Bit> bits = new List<Bit>();
        readonly List<Ribbon> ribbons = new List<Ribbon>();
        readonly bool[] fired = { false, false };
        const double END = DURATION - 0.05;
        double t;

        Confetti(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: Ramp("#ffffff", "#fff3a6", "#ffc23d", "#ff8a2a"), emitter: e);
            lights = new Lights(e);
        }

        static double Kick(double t, Popper pp) => t < pp.t ? 0 : 3 * Exp(-(t - pp.t) * 14);

        void Fire(Popper pp)
        {
            double mx = pp.x + Px.Cos(pp.a) * 11, my = pp.y + Px.Sin(pp.a) * 11;
            lights.Add(mx, my, 34, WHITE, 0.6, 0.12);
            lights.Add(mx, my, 50, C[2], 0.4, 0.4);
            for (int i = 0; i < 6; i++) ps.Spawn(P.Smoke, mx, my, Px.Cos(pp.a) * R.Range(20, 60) + R.Range(-10, 10), Px.Sin(pp.a) * R.Range(20, 60), R.Range(0.6, 1), Hex("#5d5660"), R.Range(2.5, 4));
            for (int i = 0; i < 12; i++)
            {
                double an = pp.a + R.Range(-0.5, 0.5), sp = R.Range(120, 260);
                ps.Spawn(P.Spark, mx, my, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.1, 0.25));
            }
            // paper: thrown hard up the cone, the air soon stalls it
            for (int i = 0; i < 46; i++)
            {
                double an = pp.a + R.Range(-0.38, 0.38), sp = R.Range(200, 380);
                var b = new Bit { x = mx, y = my, vx = Px.Cos(an) * sp, vy = Px.Sin(an) * sp };
                b.c = R.Pick(C);
                b.w = R.Range(2, 3);
                b.h = R.Range(3, 4.6);
                b.ang = R.Range(0, TAU);
                b.spin = R.Range(-14, 14);
                b.flip = R.Range(0, TAU);
                b.fs = R.Range(5, 13);
                b.ph = R.Range(0, TAU);
                b.amp = R.Range(5, 12);
                b.fw = R.Range(2, 3.4);
                b.x0 = 0;
                b.mode = 0;
                b.rest = 0;
                b.fall = R.Range(26, 40);
                bits.Add(b);
            }
            // streamers: a head that flies like the paper, a tail that follows it and curls
            for (int i = 0; i < 4; i++)
            {
                double an = pp.a + R.Range(-0.25, 0.25), sp = R.Range(220, 330);
                int n = 11;
                var pts = new double[n][];
                for (int j = 0; j < n; j++) pts[j] = new[] { mx, my };
                var r = new Ribbon { pts = pts, vx = Px.Cos(an) * sp, vy = Px.Sin(an) * sp };
                r.c = R.Pick(C);
                r.ph = R.Range(0, TAU);
                r.amp = R.Range(6, 12);
                r.fw = R.Range(1.5, 2.4);
                r.mode = 0;
                r.x0 = 0;
                r.curl = R.Range(0.7, 1.1);
                r.fall = R.Range(22, 32);
                ribbons.Add(r);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            for (int i = 0; i < POPPERS.Length; i++)
                if (!fired[i] && t >= POPPERS[i].t)
                {
                    fired[i] = true;
                    Fire(POPPERS[i]);
                }
            foreach (Bit b in bits)
            {
                if (b.mode == 2)
                {
                    b.rest += dt;
                    continue;
                }
                b.flip += b.fs * dt;
                if (b.mode == 0)
                {
                    // the burst: heavy drag, a little gravity, tumbling fast; once slow it starts to flutter
                    b.vx -= b.vx * 3.4 * dt;
                    b.vy -= b.vy * 3.4 * dt - 140 * dt;
                    b.x += b.vx * dt;
                    b.y += b.vy * dt;
                    b.ang += b.spin * dt;
                    if (Hypot(b.vx, b.vy) < 40)
                    {
                        b.mode = 1;
                        b.x0 = b.x - Px.Sin(b.ph) * b.amp;
                    }
                }
                else
                {
                    b.ph += b.fw * dt;
                    b.x0 += b.vx * dt;
                    b.vx -= b.vx * 2 * dt;
                    b.x = b.x0 + Px.Sin(b.ph) * b.amp;
                    b.y += b.fall * (0.4 + 0.9 * Math.Abs(Px.Cos(b.ph))) * dt;
                    b.ang = Px.Cos(b.ph) * 0.9;
                    if (b.y >= -1)
                    {
                        b.y = -0.5;
                        b.mode = 2;
                        b.ang = R.Range(-0.3, 0.3);
                        b.flip = 0;
                    }
                }
            }
            foreach (Ribbon r in ribbons)
            {
                double[] h = r.pts[0];
                if (r.mode == 0)
                {
                    r.vx -= r.vx * 3 * dt;
                    r.vy -= r.vy * 3 * dt - 120 * dt;
                    h[0] += r.vx * dt;
                    h[1] += r.vy * dt;
                    if (Hypot(r.vx, r.vy) < 35)
                    {
                        r.mode = 1;
                        r.x0 = h[0] - Px.Sin(r.ph) * r.amp;
                    }
                }
                else if (h[1] < -1)
                {
                    r.ph += r.fw * dt;
                    r.x0 += r.vx * dt;
                    r.vx -= r.vx * 2 * dt;
                    h[0] = r.x0 + Px.Sin(r.ph) * r.amp;
                    h[1] += r.fall * (0.5 + 0.7 * Math.Abs(Px.Cos(r.ph))) * dt;
                }
                // the tail follows the head link by link and hangs a little
                for (int i = 1; i < r.pts.Length; i++)
                {
                    double[] a = r.pts[i - 1], b = r.pts[i];
                    b[1] = Math.Min(-0.5, b[1] + 20 * dt);
                    double dx = b[0] - a[0], dy = b[1] - a[1], d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    if (d > 2.6)
                    {
                        b[0] = a[0] + (dx / d) * 2.6;
                        b[1] = a[1] + (dy / d) * 2.6;
                    }
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt) => ps.Draw(p, Layer.Back);

        public override void Draw(Painter p, double tt)
        {
            double fade = 1 - Seg(t, END - 0.45, END - 0.05);
            foreach (Popper pp in POPPERS)
            {
                double k = Kick(t, pp);
                DrawPopper(p, pp.x, pp.y, pp.a, k, C, 1 - Seg(t, 2.5, 2.9));
                double ft = t - pp.t;
                if (ft > 0 && ft < 0.12) Sim.Sparkle(p, pp.x + Px.Cos(pp.a) * 13, pp.y + Px.Sin(pp.a) * 13, 7 * (1 - ft / 0.12), WHITE, C[2], 0.7);
            }
            // streamers: a two-tone ribbon that curls along its length
            foreach (Ribbon r in ribbons)
            {
                var pts = new double[r.pts.Length][];
                for (int i = 0; i < r.pts.Length; i++) pts[i] = new[] { r.pts[i][0] + Px.Sin(i * r.curl + t * 9 + r.ph) * (i != 0 ? 1.6 : 0), r.pts[i][1] };
                for (int i = 1; i < pts.Length; i++)
                {
                    double x0 = pts[i - 1][0], y0 = pts[i - 1][1], x1 = pts[i][0], y1 = pts[i][1];
                    bool front = Px.Sin(i * r.curl + t * 9 + r.ph) > 0;
                    p.Line(x0, y0, x1, y1, front ? r.c : Scale(r.c, 0.6), fade);
                    p.Line(x0 + 1, y0, x1 + 1, y1, front ? Mix(r.c, WHITE, 0.35) : Scale(r.c, 0.45), fade);
                }
            }
            // paper bits: a little card turned by its tilt, its width by its flip; the back side shows darker
            foreach (Bit b in bits)
            {
                if (b.y > 1) continue;
                double fw = b.mode == 2 ? 1 : Px.Cos(b.flip), w = Math.Max(0.5, Math.Abs(fw) * b.w), ca = Px.Cos(b.ang), sa = Px.Sin(b.ang);
                uint c = fw >= 0 ? b.c : Scale(b.c, 0.62);
                double a = fade;
                if (b.mode == 2)
                {
                    p.Dot(b.x, b.y, Scale(b.c, 0.8), a);
                    p.Dot(b.x + 1, b.y, Scale(b.c, 0.6), a);
                    continue;
                }
                p.Shade(b.x - 4, b.y - 4, b.x + 4, b.y + 4, (x, y, o) =>
                {
                    double dx = x - b.x, dy = y - b.y, u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                    if (Math.Abs(u) > w / 2 + 0.2 || Math.Abs(v) > b.h / 2) return 0;
                    o.a = a;
                    return Math.Abs(fw) > 0.92 && v < -b.h / 2 + 1 ? WHITE : c;
                });
                // it catches the light as it turns face-on
                if (Math.Abs(fw) > 0.97) p.Glow(b.x, b.y, 5, Mix(b.c, WHITE, 0.5), 0.35 * fade);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
