using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class CollectBurst : Effect
    {
        const double CY = -30;

        public static readonly Module Module = new Module("collect-burst", "pixelfx/fx/pickups/collect-burst", 112, 108, 56, 94, 1.35, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0516", "#3d0a33", "#701560", "#b0228f", "#f53cd1", "#ff86e6", "#ffd2f6", "#ffffff" },
                ["seed"] = 3.0,
            },
            (o, e) => new CollectBurst(o, e));

        sealed class Star
        {
            public double x, y, a, sp, turn, life, age, size, ph;
            public uint c;
            public readonly List<(double x, double y)> tr = new List<(double x, double y)>();
            public int bornX, bornY;
        }

        sealed class Mote
        {
            public double x, y, vy, life, d, w;
            public int bornX, bornY;
        }

        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        static readonly double[] CrossLength = { 1, 1, 0.6, 0.6 };

        readonly uint[] C;
        readonly Lights lights;
        readonly Emitter emitter;
        readonly Star[] stars = new Star[14];
        readonly Mote[] motes = new Mote[7];
        double t;

        CollectBurst(Options o, Emitter e)
        {
            emitter = e;
            C = Fit(o.Ramp("palette"), 8);
            var R = new Rng(o.Seed);
            lights = new Lights(e);
            lights.Add(0, CY, 36, C[5], 0.85, 0.32);
            lights.Add(0, CY, 14, WHITE, 0.75, 0.08);
            for (int i = 0; i < stars.Length; i++)
            {
                double a = (double)i / 14 * TAU + R.Range(-0.2, 0.2), sp = R.Range(145, 225) * (i % 3 == 0 ? 1.12 : 0.85);
                var s = new Star { x = 0, y = CY, a = a, sp = sp };
                s.turn = (i % 2 != 0 ? 1 : -1) * R.Range(2.5, 5);
                s.life = R.Range(0.75, 1.15);
                s.size = i % 3 == 0 ? 2.6 : R.Range(1.2, 2);
                s.c = i % 4 == 0 ? WHITE : i % 2 != 0 ? C[6] : C[5];
                s.ph = R.Range(0, TAU);
                s.bornX = e.X;
                s.bornY = e.Y;
                stars[i] = s;
            }
            for (int i = 0; i < motes.Length; i++)
            {
                var m = new Mote { x = R.Range(-8, 8) };
                m.y = CY + R.Range(-4, 6);
                m.vy = -R.Range(12, 30);
                m.life = R.Range(0.5, 0.9);
                m.d = R.Range(0.05, 0.25);
                m.w = R.Range(0, TAU);
                m.bornX = e.X;
                m.bornY = e.Y;
                motes[i] = m;
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Star s in stars)
            {
                if (s.age > s.life) continue;
                s.age += dt;
                s.tr.Insert(0, (s.x, s.y));
                if (s.tr.Count > 4) s.tr.RemoveAt(s.tr.Count - 1);
                s.a += s.turn * dt * Math.Max(0, 1 - s.age * 1.5);
                s.sp -= s.sp * 4.3 * dt;
                s.x += Math.Cos(s.a) * s.sp * dt;
                s.y += Math.Sin(s.a) * s.sp * dt + 6 * s.age * dt;
                if (s.y > -2)
                {
                    s.y = -2;
                    s.a = -Math.Abs(s.a);
                    s.sp *= 0.5;
                }
            }
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            if (t < 0.07) p.Circle(0, CY, 5.5 * (1 - t / 0.07) + 1, WHITE);
            if (t < 0.16)
            {
                double k = 1 - t / 0.16, L = 26 * Ease.OutCubic(Math.Min(1, t / 0.03 + 0.4)) * k;
                for (int i = 0; i < Cross.Length; i++)
                {
                    double dx = Cross[i][0], dy = Cross[i][1], l = CrossLength[i];
                    p.Line(0, CY, dx * L * l, CY + dy * L * l, u => u < 0.3 ? WHITE : u < 0.65 ? C[6] : C[5], u => (u < 0.5 ? 1 : (1 - u) * 2) * (0.5 + k));
                }
                p.Glow(0, CY, 20 * k + 4, C[5], 0.6 * k);
            }
            double u1 = t / 0.32;
            if (u1 < 1)
            {
                double r = 3 + 23 * Ease.OutExpo(u1);
                p.Ring(0, CY, r, 1, u1 < 0.3 ? WHITE : C[6], 1.3 - u1 * 1.2);
                p.GlowRing(0, CY, r, 4, C[5], 0.4 * (1 - u1));
            }
            double u2 = (t - 0.04) / 0.5;
            if (u2 > 0 && u2 < 1)
            {
                double r = 2 + 16 * Ease.OutCubic(u2);
                p.Ring(0, CY, r, Math.Max(1, 2.6 * (1 - u2)), C[5], 1 - u2);
            }
            foreach (Star s in stars)
            {
                if (s.age > s.life) continue;
                emitter.Shift(p, s.bornX, s.bornY);
                double k = 1 - s.age / s.life, tw = 0.65 + 0.35 * Math.Sin(s.age * 26 + s.ph);
                for (int i = 1; i < s.tr.Count; i++) p.Dot(s.tr[i].x, s.tr[i].y, i < 2 ? C[5] : C[4], (1 - i / 4.0) * k * 1.4);
                if (k < 0.3 && Math.Sin(s.age * 40 + s.ph) < 0) continue;
                PickupsLib.Glint(p, s.x, s.y, Round(s.size * tw * Math.Min(1, k * 2.5)), s.c, 0.45 * k);
            }
            foreach (Mote m in motes)
            {
                double a = t - m.d;
                if (a < 0 || a > m.life) continue;
                emitter.Shift(p, m.bornX, m.bornY);
                double k = 1 - a / m.life, x = m.x + Math.Sin(a * 6 + m.w) * 1.5, y = m.y + m.vy * a;
                p.Dot(x, y, k > 0.5 ? C[6] : C[5], k * 1.5);
                p.Glow(x + 0.5, y + 0.5, 3, C[5], 0.3 * k);
            }
            p.Shift(0, 0);
            lights.Draw(p);
        }
    }
}
