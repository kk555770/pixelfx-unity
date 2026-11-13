using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Poof : Effect
    {
        const double CY = -17;

        public static readonly Module Module = new Module("poof", "pixelfx/fx/spawn/poof", 112, 92, 56, 80, 1.6, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a2440", "#5e5878", "#9a94b4", "#cdc8e0", "#f2f0fa", "#ffffff" },
                ["stars"] = new[] { "#ffd84a", "#fff3a6", "#ffffff" },
                ["seed"] = 21.0,
            },
            (o, e) => new Poof(o, e));

        sealed class Ball
        {
            public double x, y, vx, vy, r0, r1, life, max, delay;
            public int bornX, bornY;
        }

        sealed class SpeedLine
        {
            public double a, r1, d;
        }

        enum BallLayer
        {
            Shade,
            Body,
            Cap,
        }

        static readonly BallLayer[] BallLayers = { BallLayer.Shade, BallLayer.Body, BallLayer.Cap };

        readonly uint[] C;
        readonly Particles ps;
        readonly Lights lights;
        readonly Emitter emitter;
        readonly List<Ball> balls = new List<Ball>();
        readonly List<SpeedLine> lines = new List<SpeedLine>();
        double t;

        Poof(Options o, Emitter e)
        {
            emitter = e;
            C = Fit(o.Ramp("palette"), 6);
            uint[] S = Fit(o.Ramp("stars"), 3);
            var R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, S[1], S[0], C[2] }, emitter: e);
            lights = new Lights(e);
            const int ring = 9;
            for (int i = 0; i < ring; i++)
            {
                double a = (double)i / ring * TAU + R.Range(-0.2, 0.2), sp = R.Range(95, 140), r = R.Range(5, 7);
                var b = new Ball { x = Math.Cos(a) * 3, y = CY + Math.Sin(a) * 3, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp * 0.8 - 12, r0 = r * 0.5, r1 = r };
                b.life = R.Range(0.7, 1.05);
                b.delay = R.Range(0, 0.03);
                balls.Add(b);
            }
            for (int i = 0; i < 5; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(20, 60);
                var b = new Ball { x = Math.Cos(a) * 2, y = CY + Math.Sin(a) * 2, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp - 16, r0 = 4 };
                b.r1 = R.Range(6.5, 8.5);
                b.life = R.Range(0.85, 1.2);
                b.delay = R.Range(0.02, 0.06);
                balls.Add(b);
            }
            for (int i = 0; i < 7; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(170, 240), r = R.Range(2.2, 3.6);
                var b = new Ball { x = Math.Cos(a) * 4, y = CY + Math.Sin(a) * 4, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp * 0.75 - 10, r0 = r * 0.6, r1 = r };
                b.life = R.Range(0.32, 0.5);
                b.delay = R.Range(0.01, 0.04);
                balls.Add(b);
            }
            foreach (Ball b in balls)
            {
                b.max = b.life;
                b.bornX = e.X;
                b.bornY = e.Y;
            }
            for (int i = 0; i < 12; i++)
            {
                double a = i / 12.0 * TAU + R.Range(-0.12, 0.12) + 0.13, sa = Math.Sin(a);
                var l = new SpeedLine { a = a, r1 = Math.Min(R.Range(24, 34), sa > 0.1 ? (-2 - CY) / sa : 99) };
                l.d = R.Range(0, 0.04);
                lines.Add(l);
            }
            uint[] big = { S[2], S[1], S[1], S[0] };
            for (int i = 0; i < 7; i++)
            {
                double a = R.Range(-Math.PI, 0.3), sp = R.Range(70, 125);
                double life = R.Range(0.55, 0.95);
                uint col = R.Pick(big);
                double size = R.Range(1.3, 2.4), delay = R.Range(0.02, 0.1);
                ps.Spawn(P.Star, Math.Cos(a) * 6, CY + Math.Sin(a) * 6, Math.Cos(a) * sp, Math.Sin(a) * sp - 30, life, col, size, delay);
            }
            uint[] small = { WHITE, S[1] };
            for (int i = 0; i < 10; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(40, 110);
                double life = R.Range(0.3, 0.6);
                uint col = R.Pick(small);
                ps.Spawn(P.Star, Math.Cos(a) * 5, CY + Math.Sin(a) * 5, Math.Cos(a) * sp, Math.Sin(a) * sp - 20, life, col, 0.6, R.Range(0, 0.08));
            }
            for (int i = 0; i < 6; i++)
            {
                int s = R.Sign();
                double x = s * R.Range(1, 6), vx = s * R.Range(25, 70), vy = -R.Range(2, 10), life = R.Range(0.4, 0.7);
                double size = R.Range(1.6, 3), delay = R.Range(0.02, 0.08);
                ps.Spawn(P.Dust, x, -1, vx, vy, life, C[2], size, delay);
            }
            lights.Add(0, CY, 46, C[4], 0.8, 0.4);
            lights.Add(0, CY, 22, WHITE, 0.8, 0.1);
        }

        static double Radius(Ball b)
        {
            double age = b.max - b.life, u = age / b.max;
            double grow = b.r0 + (b.r1 - b.r0) * Ease.OutBack(Clamp(age / 0.22), 2), breathe = 1 + 0.06 * Math.Sin(age * 14 + b.r1);
            return grow * breathe * (u < 0.62 ? 1 : 1 - Ease.InBack(Clamp((u - 0.62) / 0.38), 1.4));
        }

        void DrawBall(Painter p, Ball b, BallLayer layer)
        {
            double age = b.max - b.life, r = Radius(b), a = Math.Min(1, age * 20);
            bool lit = age < 0.12;
            if (r < 0.6) return;
            double hy = (b.y - CY + 6) / 14;
            bool lo = hy > 0.4, hi = hy < -0.3;
            emitter.Shift(p, b.bornX, b.bornY);
            if (layer == BallLayer.Shade) p.Disc(b.x + r * 0.16, b.y + r * 0.2, r, lit ? C[3] : lo ? C[1] : C[2], 0.8 * a);
            else if (layer == BallLayer.Body) p.Disc(b.x - r * 0.06, b.y - r * 0.08, r * 0.86, lit ? C[4] : lo ? C[2] : C[3], 0.88 * a);
            else if (!lo || lit)
            {
                p.Disc(b.x - r * 0.28, b.y - r * 0.34, r * 0.42, lit || hi ? WHITE : C[4], 0.85 * a);
                if (age < 0.25) p.Disc(b.x - r * 0.3, b.y - r * 0.38, r * 0.2, WHITE, 0.9 * a * (1 - age / 0.25));
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball b = balls[i];
                if (b.delay > 0)
                {
                    b.delay -= dt;
                    continue;
                }
                if ((b.life -= dt) <= 0)
                {
                    if (b.r1 > 4)
                    {
                        ps.Spawn(P.Star, b.x, b.y, 0, -8, 0.22, WHITE, 0.6);
                        ps.BornAt(b.bornX, b.bornY);
                    }
                    balls.RemoveAt(i);
                    continue;
                }
                b.vx -= b.vx * 7 * dt;
                b.vy -= b.vy * 7 * dt + 30 * dt;
                b.x += b.vx * dt;
                b.y = Math.Min(b.y + b.vy * dt, -Radius(b) * 0.7);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double s = 1 - Smooth(0.1, 0.9, t);
            if (s > 0) p.Glow(0, -1, 26, C[4], 0.25 * s);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            foreach (BallLayer layer in BallLayers)
                foreach (Ball b in balls)
                    if (b.delay <= 0)
                        DrawBall(p, b, layer);
            p.Shift(0, 0);
            if (t < 0.07)
            {
                double k = t / 0.07;
                p.Circle(0, CY, 9 + 3 * k, C[4]);
                p.Circle(0, CY, 7 + 3 * k, WHITE);
                Sim.Flare(p, 0, CY, 20 * (1 - k * 0.5), 1 - k, C);
            }
            double rk = (t - 0.03) / 0.2;
            if (rk > 0 && rk < 1) p.Ring(0, CY, 8 + 18 * Ease.OutCubic(rk), Math.Max(1, 2.5 * (1 - rk)), rk < 0.3 ? WHITE : C[4], 1.1 - rk);
            foreach (SpeedLine l in lines)
            {
                double u = (t - l.d) / 0.2;
                if (u <= 0 || u >= 1) continue;
                double head = 8 + (l.r1 - 8) * Ease.OutExpo(Clamp(u / 0.6)), tail = 8 + (l.r1 - 8) * Ease.InCubic(u), ca = Math.Cos(l.a), sa = Math.Sin(l.a);
                p.Line(ca * tail, CY + sa * tail, ca * head, CY + sa * head, v => v > 0.6 ? WHITE : C[4]);
                p.GlowLine(ca * tail, CY + sa * tail, ca * head, CY + sa * head, 3, C[4], 0.25);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
