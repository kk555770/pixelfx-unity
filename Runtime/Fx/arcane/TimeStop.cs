// The cracks walking out from a point and the burst into tumbling shards, glints and light are adapted from
// Sprite Fusion's open-source ice shatter (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License,
// Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class TimeStop : Effect
    {
        const double CY = -42, RC = 26, OPEN = 0.3, FREEZE = 1.75, CRACK = 2.2, SHATTER = 2.42;
        // the second hand ticks, each wait longer than the last, until time stops
        static readonly double[] TICKS = { 0.36, 0.46, 0.57, 0.7, 0.85, 1.03, 1.24, 1.49 };

        public static readonly Module Module = new Module("time-stop", "pixelfx/fx/arcane/time-stop", 196, 136, 98, 118, 3.6, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 14 },
            (o, e) => new TimeStop(o, e))
        {
            Title = "Time stop",
            Desc = "A clock face springs open, ticks slower and slower, freezes drained of colour, cracks, then shatters into falling shards.",
            Tags = new[] { "arcane", "time", "clock", "freeze", "stop", "shatter", "magic", "one-shot" },
            Bg = "ground",
        };

        // how far the hand has gone: a snap with a little overshoot at each tick
        static double Hand(double t)
        {
            double a = 0;
            foreach (double tk in TICKS)
                if (t > tk) a += Ease.OutBack(Clamp((t - tk) / 0.07), 3);
            return -Math.PI / 2 + a * (TAU / 12);
        }

        // how fast time flows: full speed, slowing to nothing at the freeze
        static double Flow(double t) => t < OPEN ? 1 : Px.Pow(Math.Max(0, 1 - (t - OPEN) / (FREEZE - OPEN)), 1.6);

        // the motes' clock: time itself, slowing down (integrated flow)
        static double ClockTime(double t)
        {
            double s = 0;
            for (double u = 0; u < Math.Min(t, FREEZE); u += 1.0 / 120) s += Flow(u) / 120;
            return s;
        }

        sealed class Orbiter
        {
            public double a, r, w, tilt;
            public uint c;
        }

        readonly uint[] C, G, WHITES;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Orbiter[] motes = new Orbiter[14];
        readonly double ix, iy;
        readonly List<double[][]> cracks = new List<double[][]>();
        double t, tc;
        int ticked;
        bool shattered, frozen;

        TimeStop(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            // the drained ramp: each step's own lightness, cold grey
            G = Array.ConvertAll(C, c =>
            {
                double l = Luma(c) * 255;
                return Rgb(l * 0.92, l * 0.96, Math.Min(255, l * 1.06));
            });
            WHITES = Array.ConvertAll(G, c => WHITE);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            bits = new Bits(ground: x => 0, emitter: e);
            lights = new Lights(e);
            for (int i = 0; i < motes.Length; i++)
            {
                var m = new Orbiter { a = R.Range(0, TAU) };
                m.r = RC + R.Range(5, 14);
                m.w = R.Range(1.4, 2.6) * (i % 2 != 0 ? 1 : -1);
                m.tilt = R.Range(-0.5, 0.5);
                m.c = R.Pick(new[] { WHITE, C[6], C[5] });
                motes[i] = m;
            }
            // cracks: jagged walks out from a point on the face
            ix = R.Range(-6, 6);
            iy = R.Range(-6, 6);
            for (int i = 0; i < 7; i++) Walk(ix, iy, (i / 7.0) * TAU + R.Range(-0.3, 0.3), 16, 0);
        }

        void Walk(double x, double y, double a, double len, int depth)
        {
            var pts = new List<double[]> { new[] { x, y } };
            for (int s = 0; s < len; s++)
            {
                a += R.Range(-0.55, 0.55);
                x += Px.Cos(a) * R.Range(1.6, 2.6);
                y += Px.Sin(a) * R.Range(1.6, 2.6);
                if (Hypot(x, y) > RC - 0.5) break;
                pts.Add(new[] { x, y });
                if (depth < 1 && pts.Count > 3 && R.Chance(0.14)) Walk(x, y, a + R.Sign() * R.Range(0.6, 1.1), len / 2, depth + 1);
            }
            cracks.Add(pts.ToArray());
        }

        // the face, in ramp K: glass, rims, ticks and dots, hands, hub
        void Face(Painter p, uint[] K, double s, double cracked)
        {
            double cy = CY, r = RC * s;
            if (r < 1) return;
            p.Disc(0, cy, r, K[1], (d, g) => 0.3 + 0.35 * d * d);
            p.Ring(0, cy, r + 0.5, 1, K[6]);
            p.Ring(0, cy, r - 1.5, 1, K[4]);
            p.Ring(0, cy, r + 2, 1, K[3], 0.6);
            // minute dots and hour ticks; the quarters long and bright
            for (int i = 0; i < 60; i++)
            {
                double a = (i / 60.0) * TAU;
                if (i % 5 != 0) p.Dot(Px.Cos(a) * (r - 3.5), cy + Px.Sin(a) * (r - 3.5), K[4], 0.8);
                else
                {
                    bool q = i % 15 == 0;
                    double r0 = r - (q ? 8 : 6.5);
                    p.Line(Px.Cos(a) * r0, cy + Px.Sin(a) * r0, Px.Cos(a) * (r - 3), cy + Px.Sin(a) * (r - 3), q ? K[7] : K[6]);
                }
            }
            p.Ring(0, cy, r * 0.3, 1, K[4], 0.8);
            // the hour hand, then the second hand with its tail, then the hub
            double ha = -Math.PI / 2 + (Hand(t) + Math.PI / 2) / 12 + 0.9, sa = Hand(t) + (frozen ? Px.Sin(t * 70) * 0.012 : 0);
            p.Beam(0, cy, Px.Cos(ha) * r * 0.48, cy + Px.Sin(ha) * r * 0.48, 2.6, (u, v) => v < 0.5 ? K[6] : K[4]);
            p.Line(-Px.Cos(sa) * 5, cy - Px.Sin(sa) * 5, Px.Cos(sa) * (r - 4), cy + Px.Sin(sa) * (r - 4), u => u > 0.75 ? K[7] : K[6]);
            p.Line(-Px.Cos(sa) * 5 + 0.5, cy - Px.Sin(sa) * 5, -Px.Cos(sa) * 2, cy - Px.Sin(sa) * 2, K[5]);
            p.Circle(0, cy, 2.2, K[5]);
            p.Circle(0, cy, 1.2, K[7]);
            if (cracked > 0)
                foreach (double[][] pts in cracks)
                    for (int i = 0, n = (int)Math.Floor(pts.Length * cracked); i < n; i++)
                    {
                        double x = pts[i][0], y = pts[i][1];
                        bool fresh = i > n - 3 && cracked < 1;
                        p.Px(x, cy + y, fresh ? WHITE : K[7]);
                        if (!fresh) p.Px(x, cy + y - 1, K[2]);
                    }
        }

        public override void Update(double dt)
        {
            t += dt;
            tc = ClockTime(t);
            // each tick sends a ripple and a spark off the hand's tip
            while (ticked < TICKS.Length && t >= TICKS[ticked])
            {
                ticked++;
                double a = Hand(t + 0.1);
                lights.Add(Px.Cos(a) * (RC - 4), CY + Px.Sin(a) * (RC - 4), 22, C[6], 0.6, 0.12);
                for (int i = 0; i < 3; i++) ps.Spawn(P.Star, Px.Cos(a) * (RC + 1), CY + Px.Sin(a) * (RC + 1), Px.Cos(a) * 30 + R.Range(-15, 15), Px.Sin(a) * 30 + R.Range(-15, 15), R.Range(0.2, 0.4), R.Pick(new[] { WHITE, C[6] }), R.Range(0.7, 1.3));
            }
            if (!frozen && t >= FREEZE)
            {
                frozen = true;
                lights.Add(0, CY, 70, WHITE, 0.7, 0.2);
            }
            if (!shattered && t >= SHATTER)
            {
                shattered = true;
                lights.Add(0, CY, 80, WHITE, 0.8, 0.16);
                lights.Add(0, CY, 100, C[5], 0.6, 0.6);
                // the face bursts into shards and chunks, cold grey; colour sparks back into the world around them
                for (int i = 0; i < 64; i++)
                {
                    double a = R.Range(0, TAU), d = R.Range(0, RC), x = Px.Cos(a) * d, y = CY + Px.Sin(a) * d, sp = R.Range(40, 140);
                    double dx = x - ix, dy = y - CY - iy, l = Hypot(dx, dy);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    bits.Add(new Bits.Bit
                    {
                        kind = B.Shard, x = x, y = y, vx = (dx / l) * sp + R.Range(-25, 25), vy = (dy / l) * sp * 0.7 - R.Range(40, 120), g = 0.9, drag = 0.5, bounce = 0.25,
                        life = R.Range(0.8, 1.15), a = R.Range(0, TAU), va = R.Range(-22, 22), s = R.Range(1.5, 3.6), c = R.Pick(new[] { G[7], G[6], G[6], G[5] }),
                    });
                }
                for (int i = 0; i < 10; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(40, 110);
                    bits.Add(new Bits.Bit
                    {
                        kind = B.Chunk, x = Px.Cos(a) * RC * 0.6, y = CY + Px.Sin(a) * RC * 0.6, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp * 0.6 - R.Range(60, 130), g = 1, drag = 0.4, bounce = 0.3,
                        life = R.Range(0.9, 1.15), a = R.Range(0, TAU), va = R.Range(-14, 14), s = R.Range(1.6, 3), ramp = new[] { G[1], G[3], G[4], G[6], G[7] },
                    });
                }
                for (int i = 0; i < 26; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(30, 110);
                    ps.Spawn(i % 2 != 0 ? P.Star : P.Mote, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.4, 0.8), R.Pick(new[] { WHITE, C[6], C[5] }), R.Range(0.8, 1.8));
                }
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(90, 200);
                    ps.Spawn(P.Spark, 0, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.45));
                }
                // the stopped time it held lifts away as a cold vapour
                for (int i = 0; i < 12; i++)
                {
                    double a = R.Range(0, TAU), d = R.Range(0, RC * 0.8);
                    ps.Spawn(P.Vapour, Px.Cos(a) * d, CY + Px.Sin(a) * d, Px.Cos(a) * R.Range(10, 40), Px.Sin(a) * R.Range(10, 30) - R.Range(5, 20), R.Range(0.6, 1), R.Chance(0.5) ? G[5] : C[4], R.Range(3, 6));
                }
            }
            bits.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the floor lit beneath the clock while it stands
            double k = Smooth(0, OPEN, t) * (1 - Smooth(SHATTER, SHATTER + 0.5, t));
            bool cold = t >= FREEZE;
            if (k > 0) p.Glow(0, -1, 40, cold ? G[5] : C[4], 0.25 * k);
            // the motes behind the face
            if (t < SHATTER)
                foreach (Orbiter m in motes)
                    DrawMote(p, m, true);
        }

        public override void Draw(Painter p)
        {
            bool cold = t >= FREEZE;
            double s = t < OPEN ? Ease.OutBack(t / OPEN, 2.6) : 1;
            if (t < SHATTER)
            {
                // white for a beat as it freezes, then cold grey; white again in the moment it bursts
                bool white = (t >= FREEZE && t < FREEZE + 0.06) || t >= SHATTER - 0.05;
                Face(p, white ? WHITES : cold ? G : C, s, Clamp((t - CRACK) / 0.18));
                p.Glow(0, CY, RC * 1.9, cold ? G[5] : C[4], 0.32 * s);
                p.GlowRing(0, CY, RC, 7, cold ? G[6] : C[5], 0.28 * s);
                foreach (Orbiter m in motes) DrawMote(p, m, false);
            }
            // springing open: a ring thrown out and a flare
            if (t < 0.35)
            {
                double q = t / 0.35;
                p.Ring(0, CY, RC * (0.4 + 1.2 * Ease.OutCubic(q)), Math.Max(1, 3 * (1 - q)), q < 0.3 ? WHITE : C[6], 1.2 - q);
                if (q < 0.5) Sim.Flare(p, 0, CY, 26 * (1 - q * 2) + 2, 1 - q * 2, C);
            }
            // a ripple of time off each tick; ripples run on the clock's own slowing time, so they drag to a halt and
            // hang there, drained, until it all breaks
            if (t < SHATTER)
                foreach (double tk in TICKS)
                {
                    if (t < tk) continue;
                    double q = (tc - ClockTime(tk)) / 0.32;
                    if (q >= 1) continue;
                    p.Ring(0, CY, RC + 3 + 24 * Ease.OutCubic(q), 1, cold ? G[5] : C[5], (cold ? 0.6 : 0.8) * Px.Pow(1 - q, 1.6));
                }
            if (t >= SHATTER && t < SHATTER + 0.2)
            {
                double q = (t - SHATTER) / 0.2;
                Sim.Flare(p, 0, CY, 34 * (1 - q) + 2, 1 - q, C);
                p.Ring(0, CY, RC + 30 * Ease.OutCubic(q), Math.Max(1, 3 * (1 - q)), WHITE, 1.2 - q);
            }
            bits.Draw(p);
            ps.Draw(p);
            lights.Draw(p);
        }

        // a mote circling the clock on a tilted orbit, slowing with time; drained and still once it stops
        void DrawMote(Painter p, Orbiter m, bool back)
        {
            double a = m.a + m.w * tc, z = Px.Sin(a);
            if (z < 0 != back) return;
            double x = Px.Cos(a) * m.r, y = CY + Px.Cos(a) * m.r * m.tilt + z * m.r * 0.3, v = Flow(t) * m.w;
            bool cold = t >= FREEZE;
            uint c = cold ? G[6] : m.c;
            p.Dot(x, y, c, back ? 0.6 : 1);
            // a tail while it still moves
            if (Math.Abs(v) > 0.2)
                for (int k = 1; k <= 3; k++)
                {
                    double b = a - Sign(m.w) * k * 0.05 * Math.Min(1, Math.Abs(v));
                    p.Dot(Px.Cos(b) * m.r, CY + Px.Cos(b) * m.r * m.tilt + Px.Sin(b) * m.r * 0.3, k < 2 ? C[5] : C[4], (back ? 0.5 : 0.9) * (1 - k / 4.0));
                }
            if (!back) p.Glow(x, y, 5, cold ? G[6] : C[6], 0.3);
        }
    }
}
