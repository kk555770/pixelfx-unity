using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class GroundSlam : Effect
    {
        const double Duration = 3.2, WAVE = 0.6;

        public static readonly Module Module = new Module("ground-slam", "pixelfx/fx/impacts/ground-slam", 288, 208, 144, 164, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1712", "#5e2c1c", "#a4502a", "#e8893a", "#ffc764", "#fff0b4", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["reach"] = 104.0,
                ["seed"] = 5.0,
            },
            (o, e) => new GroundSlam(o, e));

        sealed class Ember
        {
            public double x, y, life, ph;
        }

        sealed class Rock
        {
            public double[] pts;
            public double pvy;
        }

        readonly uint[] C, HOT;
        readonly ImpactsLib.Stone st;
        readonly double REACH;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Bits bits;
        readonly Dictionary<Bits.Bit, Rock> rocks = new Dictionary<Bits.Bit, Rock>();
        readonly Lights lights;
        readonly uint dustLit;
        readonly ImpactsLib.Crack[] cracks;
        readonly ImpactsLib.Spike[] burst;
        readonly List<Ember> embers = new List<Ember>();
        readonly double[] acc;
        double t;

        GroundSlam(Options o, Emitter e)
        {
            emitter = e;
            C = Fit(o.Ramp("palette"), 7);
            st = new ImpactsLib.Stone(o.Color("stone"));
            REACH = o.Num("reach");
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(rng: R, ground: floor, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            bits = new Bits(floor, e);
            lights = new Lights(e);
            HOT = new[] { WHITE, C[5], C[4], C[3] };
            dustLit = Mix(st.dust, WHITE, 0.2);
            cracks = ImpactsLib.MakeCracks(R, 10, 22, 58, 0.55, 22, 0.7, 2, 9);
            burst = new[]
            {
                new ImpactsLib.Spike(-0.07, R.Range(34, 40), 1.9), new ImpactsLib.Spike(Math.PI + 0.07, R.Range(34, 40), 1.9),
                new ImpactsLib.Spike(-0.42 + R.Range(-0.08, 0.08), R.Range(15, 20), 1.5), new ImpactsLib.Spike(-Math.PI + 0.42 + R.Range(-0.08, 0.08), R.Range(15, 20), 1.5),
                new ImpactsLib.Spike(-Math.PI / 2 + R.Range(-0.15, 0.15), R.Range(18, 24), 1.7),
                new ImpactsLib.Spike(-1.05 + R.Range(-0.1, 0.1), R.Range(10, 14), 1.2), new ImpactsLib.Spike(-Math.PI + 1.05 + R.Range(-0.1, 0.1), R.Range(10, 14), 1.2),
            };
            foreach (ImpactsLib.Crack c in cracks)
            foreach (double[] pt in c.pts)
            {
                double d = pt[2];
                if (d > 3 && d < 32 && R.Chance(0.25)) embers.Add(new Ember { x = pt[0], y = pt[1], life = R.Range(0.8, 2.2), ph = R.Range(0, TAU) });
            }

            lights.Add(0, -12, 96, C[4], 0.6, 0.6);
            lights.Add(0, -4, 50, WHITE, 0.9, 0.14);
            for (int i = 0; i < 56; i++)
            {
                bool low = i < 14;
                int s = R.Sign();
                double a = low ? (s > 0 ? -R.Range(0.04, 0.6) : Math.PI + R.Range(0.04, 0.6)) : -Math.PI / 2 + R.Range(-1.15, 1.15);
                double sp = low ? R.Range(150, 380) : R.Range(200, 500);
                ps.Spawn(P.Spark, R.Range(-4, 4), -1, Math.Cos(a) * sp, Math.Sin(a) * sp, low ? R.Range(0.2, 0.5) : R.Range(0.25, 0.65), WHITE, 1, low ? 0 : R.Range(0, 0.05));
            }
            for (int i = 0; i < 20; i++)
            {
                int s = i < 3 ? 4 : R.Int(1, 3);
                double a = -Math.PI / 2 + R.Range(-0.5, 0.5), sp = s > 3 ? R.Range(110, 190) : R.Range(170, 380) * (s > 2 ? 0.8 : 1);
                var b = new Bits.Bit
                {
                    kind = B.Chunk, x = R.Range(-7, 7), y = -2, vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp, g = 1, drag = 0.4, bounce = 0.38,
                    life = R.Range(1.9, 2.7), a = R.Range(0, TAU), va = R.Range(-14, 14), s = s,
                };
                rocks[b] = new Rock { pts = ImpactsLib.RockShape(R) };
                bits.Add(b);
            }
            for (int i = 0; i < 110; i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.65, sp = R.Range(110, 380) * (1 - 0.35 * Math.Abs(u));
                ps.Spawn(P.Grit, u * 6, -1, Math.Cos(a) * sp, Math.Sin(a) * sp, R.Range(1, 2.2), R.Pick(new[] { st.light, st.body, st.body, st.shade, st.shade, st.lip }), R.Chance(0.35) ? 2 : 1);
            }
            for (int i = 0; i < 28; i++)
            {
                bool core = i < 14;
                int s = R.Sign();
                ps.Spawn(P.Smoke, core ? R.Range(-5, 5) : s * R.Range(4, 12), -R.Range(2, 10), core ? R.Range(-12, 12) : s * R.Range(14, 38), -R.Range(50, 130) * (core ? 1 : R.Range(0.3, 0.6)),
                    R.Range(1.9, 2.7), R.Chance(0.35) ? dustLit : st.dust, core ? R.Range(4.5, 9) : R.Range(4, 7.5), R.Range(0.02, 0.22), PF.Big);
            }
            for (int i = 0; i < 10; i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Smoke, s * R.Range(2, 14), -R.Range(2, 6), s * R.Range(20, 70), -R.Range(4, 20), R.Range(1.4, 2.3), st.dust, R.Range(6, 9), R.Range(0, 0.08), PF.Big);
            }
            for (int i = 0; i < 26; i++)
                ps.Spawn(P.Flake, R.Range(-22, 22), -R.Range(14, 80), R.Range(-12, 12), R.Range(-10, 4), R.Range(1.2, 2), R.Chance(0.5) ? st.lip : dustLit, R.Range(0.5, 1.2), R.Range(0.25, 1));
            acc = new[] { R.Next(), R.Next() };
        }

        double Front(double at) => 7 + (REACH - 7) * Ease.OutCubic(Clamp((at - 0.015) / WAVE));
        static double Heat(double at) => Math.Pow(Clamp(1 - at / 0.8), 1.4);
        static double CrestHeight(double u) => 6 + 28 * Math.Pow(1 - u, 0.75);
        static double CrestWidth(double u) => 1.5 + 5 * Math.Pow(1 - u, 0.9);

        void Crater(Painter p, double hot, double k)
        {
            for (int x = -12; x <= 11; x++)
            {
                double q = Math.Abs(x + 0.5) / 12, dep = Round(2.6 * (1 - q * q));
                if (q < 0.84)
                {
                    for (int y = 0; y < dep; y++) p.Dot(x, y, ImpactsLib.SOOT, k);
                    p.Dot(x, dep, hot > 0.5 ? C[5] : hot > 0.2 ? C[4] : st.lip, k);
                }
                else
                {
                    p.Dot(x, -1, q > 0.93 ? st.light : st.lip, k);
                    if (q < 0.9) p.Dot(x, -2, st.light, k * 0.8);
                }
            }
        }

        void Ring(Painter p, bool back)
        {
            double u = Clamp((t - 0.015) / (WAVE + 0.1));
            if (t < 0.015 || u >= 0.9) return;
            Sim.Band(p, 0, 0.5, Front(t) + 1, 0.16, Math.Max(1, 4 * Math.Pow(1 - u, 0.8)), u < 0.55 ? 1.1 : (0.9 - u) / 0.35,
                u < 0.35 ? new[] { C[3], C[4], C[5], C[6] } : new[] { st.body, st.light, C[4], C[5] }, g => Math.Sin(g) < 0 == back, t < 0.09);
        }

        void Fronts(Painter p)
        {
            double u = Clamp((t - 0.03) / (WAVE + 0.16));
            if (t < 0.03 || u >= 1) return;
            double fr = Front(t), h = CrestHeight(u), w = CrestWidth(u), k = u < 0.7 ? 1 : (1 - u) / 0.3;
            uint[] cols = u < 0.14 ? new[] { WHITE, C[6], C[5] } : u < 0.34 ? new[] { C[6], C[5], C[4] } : u < 0.55 ? new[] { C[5], C[4], st.lip } : new[] { st.lip, st.light, st.dust };
            foreach (int s in new[] { -1, 1 })
            {
                ImpactsLib.FrontCrescent(p, s * fr, s, h, w, cols, k);
                p.Glow(s * fr, -h * 0.3, 8 + h * 0.7, C[4], 0.5 * (1 - u) * k);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            double u = Clamp((t - 0.015) / WAVE);
            if (u > 0 && u < 1)
            {
                double fr = Front(t), h = CrestHeight(u);
                for (int k = 0; k < 2; k++)
                {
                    int s = k != 0 ? 1 : -1;
                    for (acc[k] += dt * 120 * (1 - u * 0.7); acc[k] > 1; acc[k]--)
                    {
                        ps.Spawn(P.Smoke, s * (fr - R.Range(1, 7)), -R.Range(0.5, 3), s * R.Range(15, 70) * (1 - u), -R.Range(2, 12), R.Range(1, 2.1), R.Chance(0.3) ? dustLit : st.dust, R.Range(2.5, 6.5) * (1 - 0.4 * u), 0, PF.Big);
                        if (R.Chance(0.5)) ps.Spawn(P.Dust, s * (fr - R.Range(0, 3)), -R.Range(0.3, 0.9) * h, s * R.Range(-15, 25), -R.Range(8, 35), R.Range(0.3, 0.6), R.Chance(0.5) ? dustLit : st.dust, R.Range(1.2, 2.4));
                        if (R.Chance(0.4)) ps.Spawn(P.Grit, s * fr, -1, s * R.Range(10, 60), -R.Range(40, 140) * (1 - u), R.Range(0.5, 1), R.Pick(new[] { st.light, st.body, st.lip }), 1);
                        if (u < 0.45 && R.Chance(0.2)) ps.Spawn(P.Spark, s * fr, -1, s * R.Range(60, 200), -R.Range(30, 160), R.Range(0.12, 0.3));
                    }
                }
            }
            foreach (Bits.Bit b in bits.List) rocks[b].pvy = b.vy;
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
            {
                Rock r = rocks[b];
                if (r.pvy > 70 && b.vy <= 0)
                {
                    int n = r.pvy > 200 ? 3 : 2;
                    for (int i = 0; i < n; i++)
                    {
                        ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 35), -R.Range(2, 16), R.Range(0.3, 0.65), st.dust, R.Range(1.3, 2.4) * (0.7 + b.s * 0.25));
                        ImpactsLib.BornWith(ps, b);
                    }
                    r.pvy = 0;
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double fade = 1 - Smooth(Duration - 0.9, Duration - 0.15, t), H = Heat(t);
            if (t > 0.02) Crater(p, H, fade);
            ImpactsLib.DrawCracks(p, cracks, c => c.len * Ease.OutCubic(Clamp((t - c.t0) / 0.1)), d => H * Clamp(1.2 - d / 40), HOT, ImpactsLib.SOOT, st.light, fade);
            foreach (Ember e in embers)
            {
                double k = 1 - (t - 0.1) / e.life;
                if (t < 0.1 || k <= 0) continue;
                double f = 0.6 + 0.4 * Math.Sin(t * 23 + e.ph);
                p.Px(e.x, e.y, k * f > 0.55 ? C[5] : k * f > 0.25 ? C[4] : C[3]);
                p.Glow(e.x, e.y, 4, C[4], 0.35 * k * f);
            }
            Ring(p, true);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            Ring(p, false);
            Fronts(p);
            foreach (Bits.Bit b in bits.List) ImpactsLib.Rock(p, emitter, b, rocks[b].pts, st);
            ps.Draw(p, Layer.Front);
            if (t < 0.035)
            {
                p.Ellipse(0, 0, 30, 4.5, (d, g) => d > 0.8 ? C[4] : d > 0.5 ? C[6] : WHITE);
                ImpactsLib.Star(p, 0, -1, burst, 1, 1, new[] { C[4], C[6], WHITE, WHITE });
            }
            else if (t < 0.07) ImpactsLib.Star(p, 0, -1, burst, 1.25, 0.5, new[] { C[3], C[5], C[6], WHITE });
            if (t < 0.14) p.GlowLine(-80, 0, 80, 0, 10, C[5], 0.55 * (1 - t / 0.14));
            double H = Heat(t);
            if (H > 0) p.Glow(0, 1, 46, C[4], 0.35 * H);
            lights.Draw(p);
        }
    }
}
