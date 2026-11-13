// The layered ball, its tapering wake and the zap to the floor with stars where it lands follow Sprite Fusion's
// open-source energy orb and bolt (https://www.spritefusion.com/pixel-art-vfx/energy),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class BallLightning : Effect
    {
        public static readonly Module Module = new Module("ball-lightning", "pixelfx/fx/lightning/ball-lightning", 136, 104, 68, 92, 4, true, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["seed"] = 9 },
            (o, e) => new BallLightning(o, e))
        {
            Title = "Ball lightning",
            Desc = "A crackling ball of plasma drifts on a wandering loop, spitting arcs off its skin and leaving a wake of fading motes, and zaps the floor when it dips low.",
            Tags = new[] { "lightning", "electric", "plasma", "orb", "wisp", "ambient", "loop" },
            Bg = "ground",
        };

        const int FPS = 30, N = 120, ZAP = 62;
        const double P = 4;
        static readonly double[] ZK = { 1, 0.6, 0.9, 0.4 };

        // where the ball is: a closed, wandering path that dips toward the floor once a loop
        static (double x, double y) At(double t)
        {
            double a = (t / P) * TAU;
            return (38 * Px.Sin(a) + 9 * Px.Sin(2 * a + 1), -42 + 13 * Px.Sin(2 * a + 0.4) + 6 * Px.Sin(3 * a + 0.5));
        }

        readonly Options o;

        BallLightning(Options o, Emitter e) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), S = Ramp(o.Ramp("sparks"));
            int f = (int)(Math.Floor(t * FPS + 1e-6) % N);
            Rng r = NewRng(o.Seed, 1, f);
            var (x, y) = At(t);
            // light under the ball on the floor, stronger as it comes low
            p.Glow(x, -2, 30, C[3], 0.12 + 0.3 * Clamp((y + 60) / 40));
            // the wake: where it has been, shrinking and cooling, and motes shed along it, drifting and dying
            for (int i = 14; i >= 1; i--)
            {
                var (wx, wy) = At(t - i * 0.034);
                double u = i / 14.0, rr = 3 * (1 - u) + 0.3;
                if (u > 0.6 && (i + f) % 2 != 0) continue;
                p.Circle(wx, wy, rr + 0.8, u < 0.35 ? C[3] : C[2]);
                p.Circle(wx, wy, rr, u < 0.35 ? C[6] : u < 0.7 ? C[5] : C[4]);
                p.Glow(wx, wy, 7 * (1 - u) + 3, C[4], 0.25 * (1 - u));
            }
            Cycle(t, P, 40, o.Seed + 5, g => g.Range(0.5, 1), (age, life, q, _) =>
            {
                double born = t - age;
                var (bx, by) = At(born);
                double k = 1 - age / life, a = q.Range(0, TAU), sp = q.Range(4, 14);
                double mx = bx + Px.Cos(a) * sp * age + q.Range(-3, 3), my = by + Px.Sin(a) * sp * age + 6 * age + q.Range(-3, 3);
                if (k > 0.25 || f % 2 != 0) p.Px(mx, my, k > 0.6 ? C[6] : k > 0.3 ? C[4] : C[3]);
                p.Glow(mx, my, 3, C[4], 0.3 * k);
            });
            // the ball: a churning skin with hot spots rolling round it, a white heart, corona bristling off it
            double R0 = 5.2 + 0.6 * Px.Sin(t * 9) + 0.4 * r.Range(-1, 1);
            p.Circle(x, y, R0 + 1.3, C[2]);
            p.Circle(x, y, R0, C[4]);
            p.Circle(x - 0.4, y - 0.4, R0 * 0.72, C[5]);
            for (int i = 0; i < 3; i++)
            {
                double a = t * (5 + i * 2.3) + i * 2.1, d = R0 * 0.6;
                p.Circle(x + Px.Cos(a) * d, y + Px.Sin(a) * d * 0.8, 1.2, i != 0 ? C[6] : WHITE);
            }
            p.Circle(x - 0.6, y - 0.6, R0 * 0.32, WHITE);
            for (int i = 0; i < 8; i++)
            {
                if (r.Next() < 0.35) continue;
                double a = (i / 8.0) * TAU + t * 2 + r.Range(-0.2, 0.2), l = r.Range(1.5, 4);
                p.Line(x + Px.Cos(a) * (R0 + 1), y + Px.Sin(a) * (R0 + 1), x + Px.Cos(a) * (R0 + 1 + l), y + Px.Sin(a) * (R0 + 1 + l), i % 2 != 0 ? C[5] : C[4]);
            }
            p.Glow(x, y, 30, C[4], 0.3);
            p.Glow(x, y, 9, C[6], 0.1);
            // arcs spat off its skin, re-struck every frame
            for (int i = 0; i < 2 + (r.Next() < 0.4 ? 1 : 0); i++)
            {
                double a = r.Range(0, TAU), l = r.Range(5, 11), x0 = x + Px.Cos(a) * (R0 + 1), y0 = y + Px.Sin(a) * (R0 + 1), b = a + r.Range(-0.7, 0.7);
                Crackle(p, Zigzag(r, x0, y0, x0 + Px.Cos(b) * l, y0 + Px.Sin(b) * l, 0.4, 0.3), r.Range(0.4, 0.7), C, 0.25);
            }
            // once a loop, low over the floor, it zaps down: a bolt, the floor lit, sparks kicked up
            int za = f - ZAP;
            double zx = At((double)ZAP / FPS).x, gx = zx + NewRng(o.Seed, 3).Range(-10, 10);
            if (za >= 0 && za < 4)
            {
                double k = ZK[za];
                Stroke(p, Zigzag(NewRng(o.Seed, 4, f), x, y + R0, gx, 0, 0.28, 0.4), k, C, 0.7, new StrokeOpt { hero = za == 0 });
                p.Circle(gx, -1, 2.5 * k + 1, C[5]);
                p.Px(gx, -1, WHITE);
                p.Glow(gx, -4, 34, C[4], 0.6 * k);
            }
            double sa = (((t - (double)ZAP / FPS) % P) + P) % P;
            for (int j = 0; j < 14; j++)
            {
                Rng q = NewRng(o.Seed, 7, j);
                double life = q.Range(0.25, 0.55);
                if (sa >= life) continue;
                double a = -Math.PI / 2 + q.Range(-1.3, 1.3), sp = q.Range(50, 170);
                double[] s = Flight(gx, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, sa, floor: 0);
                Spark(p, s[0], s[1], s[2], s[3], 1 - sa / life, S);
            }
        }
    }
}
