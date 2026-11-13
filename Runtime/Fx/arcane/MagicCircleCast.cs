// The column of light (banded across, strands wandering in it, pulses and halos climbing it, stars thrown off,
// rings shocking out across the floor) is adapted from Sprite Fusion's open-source holy light
// (https://www.spritefusion.com/pixel-art-vfx/holy-light), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class MagicCircleCast : Effect
    {
        // the circle on the floor (plane px; the floor squashes depth by SY), the column, the beats
        const double R = 40, R2 = 35, RI = 24, RR = 30, SY = 0.3, TOP = 192, W = 11;
        const int NRUNE = 8;
        static readonly double[] PEN = { 0.04, 0.42 }, PEN2 = { 0.1, 0.48 }, INNER = { 0.3, 0.6 }, STAR = { 0.52, 0.88 }, RUNES = { 0.9, 1.3 };
        const double FLASH = 1.52, BREAK = 2.42, GONE = 3.45, END = 3.9;

        public static readonly Module Module = new Module("magic-circle-cast", "pixelfx/fx/arcane/magic-circle-cast", 200, 220, 100, 192, END, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 6 },
            (o, e) => new MagicCircleCast(o, e))
        {
            Title = "Magic circle cast",
            Desc = "A circle draws itself on the floor, runes ignite one by one, it flashes, and a column of light fires up and breaks apart into glints.",
            Tags = new[] { "arcane", "magic", "circle", "rune", "cast", "column", "summon", "ground", "one-shot" },
            Bg = "ground",
        };

        static double WrapA(double a) => ((a % TAU) + TAU) % TAU;
        static double FromFront(double g) => Math.Abs(Atan2(Px.Sin(g - Math.PI / 2), Px.Cos(g - Math.PI / 2)));
        // when the pens pass angle g: the outer rings are drawn from the front round both sides to the back, the inner
        // ring from the back all the way round
        static double OuterAt(double g, double[] w) => w[0] + (FromFront(g) / Math.PI) * (w[1] - w[0]);
        static double InnerAt(double g) => INNER[0] + (WrapA(g + Math.PI / 2) / TAU) * (INNER[1] - INNER[0]);
        static double RuneAngle(int k) => -Math.PI / 2 + ((k + 0.5) * TAU) / NRUNE;
        static double RuneLit(int k) => RUNES[0] + (k * (RUNES[1] - RUNES[0])) / (NRUNE - 1);
        static double RuneOut(int k) => BREAK + 0.2 + k * 0.08;

        // how bright the drawn circle burns: settled while it's drawn, swelling as the runes light, white at the flash
        static double Level(double t)
        {
            if (t < RUNES[0]) return 0.55;
            if (t < FLASH) return 0.55 + 0.4 * Smooth(RUNES[0], FLASH, t);
            if (t < FLASH + 0.07) return 1.2;
            if (t < BREAK) return 0.92;
            return 0.92 * (1 - Smooth(BREAK, GONE, t));
        }

        sealed class Strand
        {
            public double o, w, f;
        }

        sealed class Edge
        {
            public double[] p0, p1;
            public double t0, dur;
        }

        readonly uint[] C, COL;
        readonly double S;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly Strand[] strands;
        readonly List<Edge> edges = new List<Edge>();
        readonly bool[] @out = new bool[NRUNE];
        double t;
        bool flashed, broke;

        MagicCircleCast(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            S = o.Seed;
            Rg = new Rng(S);
            ps = new Particles(Rg, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            COL = new[] { WHITE, C[6], C[5], C[4], C[3] };
            strands = new Strand[9];
            for (int i = 0; i < strands.Length; i++)
            {
                var s = new Strand { o = Rg.Range(-0.85, 0.85) };
                s.w = Rg.Range(0.04, 0.1);
                s.f = Rg.Range(1, 3);
                strands[i] = s;
            }
            // the hexagram: two triangles inside the inner ring, each drawn edge by edge
            double[][] Tri(double a0)
            {
                var pts = new double[3][];
                for (int k = 0; k < 3; k++)
                {
                    double a = a0 + (k * TAU) / 3;
                    pts[k] = new[] { Px.Cos(a) * RI, Px.Sin(a) * RI };
                }
                return pts;
            }
            double[][] A = Tri(-Math.PI / 2), Bt = Tri(Math.PI / 2);
            for (int k = 0; k < 3; k++)
                foreach (double[][] T in new[] { A, Bt })
                    edges.Add(new Edge { p0 = T[k], p1 = T[(k + 1) % 3], t0 = STAR[0] + (k * (STAR[1] - STAR[0])) / 3, dur = (STAR[1] - STAR[0]) / 3 });
        }

        uint LineCol(double lvl, double age) => age < 0.05 ? WHITE : age < 0.16 ? C[6] : lvl > 1 ? WHITE : lvl > 0.8 ? C[6] : lvl > 0.45 ? C[5] : lvl > 0.28 ? C[4] : C[3];
        static double LineA(double lvl) => Clamp(lvl / 0.3);

        // a straight line on the floor plane, drawn by a pen from p0 to p1 over [t0, t0 + dur]; only the far (v < 0) or near part
        void PlaneLine(Painter p, double[] p0, double[] p1, double t0, double dur, double lvl, bool far)
        {
            double s = Clamp((t - t0) / dur);
            if (s <= 0) return;
            double u0 = 0, u1 = s;
            double v0 = p0[1], v1 = p1[1];
            if ((v0 < 0) != (v1 < 0))
            {
                double uc = v0 / (v0 - v1);
                if ((v0 < 0) == far) u1 = Math.Min(u1, uc);
                else u0 = uc;
            }
            else if ((v0 < 0) != far) return;
            if (u1 <= u0) return;
            double xa = p0[0] + (p1[0] - p0[0]) * u0, ya = (v0 + (v1 - v0) * u0) * SY;
            double xb = p0[0] + (p1[0] - p0[0]) * u1, yb = (v0 + (v1 - v0) * u1) * SY;
            p.Line(xa, ya, xb, yb, q => LineCol(lvl, t - (t0 + (u0 + (u1 - u0) * q) * dur)), LineA(lvl));
        }

        void Circle(Painter p, bool far)
        {
            if (t >= GONE) return;
            double lvl = Level(t), a = LineA(lvl);
            Func<double, bool> keep = far ? (Func<double, bool>)(g => Px.Sin(g) < 0) : g => Px.Sin(g) >= 0;
            void Ring(double r, Func<double, double> when, Func<double, bool> chase) =>
                FloorRing(p, 0, 0, r, SY, g => chase != null && chase(g) ? WHITE : LineCol(lvl, t - when(g)), g => t < when(g) ? 0 : a, keep);
            // from the moment the runes start to light, two sparks of light chase round the outer ring, faster and faster
            Func<double, bool> chaser = null;
            if (t > RUNES[0] && t < BREAK)
            {
                double run = (t - RUNES[0]) * 5 + (t - RUNES[0]) * (t - RUNES[0]) * 4;
                chaser = g =>
                {
                    foreach (double o in new[] { 0, Math.PI })
                        if (Math.Abs(Atan2(Px.Sin(g - run - o), Px.Cos(g - run - o))) < 0.22) return true;
                    return false;
                };
            }
            Ring(R, g => OuterAt(g, PEN), chaser);
            Ring(R2, g => OuterAt(g, PEN2), null);
            Ring(RI, InnerAt, null);
            // ticks across the band between the two outer rings
            for (int j = 0; j < 32; j++)
            {
                double g = ((j + 0.5) * TAU) / 32;
                if (Px.Sin(g) < 0 != far) continue;
                double when = OuterAt(g, PEN2) + 0.03;
                if (t < when) continue;
                p.Line(Px.Cos(g) * R2, Px.Sin(g) * R2 * SY, Px.Cos(g) * R, Px.Sin(g) * R * SY, LineCol(lvl, t - when), a * (j % 2 != 0 ? 0.6 : 1));
            }
            foreach (Edge e in edges) PlaneLine(p, e.p0, e.p1, e.t0, e.dur, lvl, far);
        }

        // the runes stand on the circle; dim when the inner pen passes, lit one by one, out one by one
        void Runes(Painter p, bool far)
        {
            for (int k = 0; k < NRUNE; k++)
            {
                double g = RuneAngle(k);
                if (Px.Sin(g) < 0 != far) continue;
                double x = Px.Cos(g) * RR, y0 = Px.Sin(g) * RR * SY, lit = t - RuneLit(k), gone = Clamp((t - RuneOut(k)) / 0.25);
                if (t < InnerAt(g) || gone >= 1) continue;
                if (lit < 0)
                {
                    Glyph(p, k, x, y0 - 1, C[3]);
                    continue;
                }
                double rise = 3 * Ease.OutBack(Clamp(lit / 0.2), 2.4), bob = lit > 0.2 ? Px.Sin(t * 5 + k) * 0.5 : 0, y = y0 - 1 - rise + bob;
                bool hot = lit < 0.07 || (t > FLASH && t < FLASH + 0.07);
                double lvl = Level(t);
                Glyph(p, k, x, y, hot ? WHITE : lvl > 0.8 ? WHITE : C[6], 1 - gone, hot ? C[6] : C[4]);
                if (lit < 0.1) Sim.Sparkle(p, x, y - 2, 4 * (1 - lit / 0.1) + 1, C[6], C[5], 0.5);
                p.Glow(x, y - 2, 8, C[5], (0.3 + 0.3 * lvl) * (1 - gone));
                p.Glow(x, y0, 6, C[4], 0.2 * (1 - gone));
            }
        }

        // the column of light: the thread that climbs first, then the beam that fires up, holds and breaks apart
        void Column(Painter p)
        {
            if (t < FLASH - 0.22 || t > BREAK + 0.6) return;
            if (t < FLASH)
            {
                double a0 = Smooth(FLASH - 0.22, FLASH - 0.04, t), top0 = -(TOP - 10) * Ease.OutCubic(a0);
                for (double y = -1; y >= top0; y--) p.Dot(0, y, y > top0 + 6 ? C[6] : WHITE, a0 * (0.35 + 0.65 * (1 + y / TOP)));
                p.Glow(0, top0, 9, C[6], 0.5 * a0);
                p.Glow(0, -6, 24, C[5], 0.4 * a0);
                return;
            }
            double u = t - FLASH, brk = Clamp((t - BREAK) / 0.45), top = -TOP * Ease.OutExpo(Clamp(u / 0.09));
            double w = t < BREAK ? W * (1 + 0.38 * Exp(-u * 9)) : W * (1 - Ease.OutCubic(brk) * 0.85);
            double fade = 1 - Smooth(BREAK + 0.25, BREAK + 0.55, t);
            bool white = u < 0.067;
            if (fade <= 0) return;
            for (double y = 3; y >= Math.Ceiling(top); y--)
            {
                double h = -y, ww = w * (1 + 0.5 * Exp(-Math.Max(0, h) / 6));
                int pulse = Fract(h * 0.022 - u * 2.6) < 0.07 ? 1 : 0;
                for (double x = Math.Floor(-ww * 1.3); x <= Math.Ceiling(ww * 1.3); x++)
                {
                    double q = Math.Abs(x + 0.5) / ww;
                    // as it breaks, holes open and grow and drift upward until only shards are left
                    if (brk > 0 && Noise2(x * 0.32, h * 0.11 - (t - BREAK) * 6, S) * 0.5 + 0.5 < brk * 1.25 - 0.1) continue;
                    int lv = q < 0.2 ? 0 : q < 0.42 ? 1 : q < 0.66 ? 2 : q < 0.88 ? 3 : 4;
                    foreach (Strand s in strands)
                        if (Math.Abs((x + 0.5) / ww - s.o - 0.05 * Px.Sin(t * s.f + s.o * 9)) < s.w) lv = Math.Max(0, lv - 1);
                    lv = white ? 0 : Math.Max(0, lv - pulse);
                    double a = (q < 0.88 ? 1 : (1.3 - q) / 0.42) * fade;
                    if (a <= Bayer(x, y) * (q < 0.66 ? 0.2 : 1)) continue;
                    p.Px(x, y, COL[lv]);
                }
                if ((ToInt32(h) & 7) == 0 && h < TOP - ww * 2.4 - 8) p.Glow(0, y, ww * 2.4 + 6, C[5], 0.3 * fade);
            }
            // halos climbing it
            for (int i = 0; i < 6; i++)
            {
                double k = (t - (FLASH + 0.08 + i * 0.16)) / 0.8;
                if (k < 0 || k > 1) continue;
                double rx = w * 1.4 * (1 + k * 0.35), ry = rx * 0.3, yc = -3 - Ease.OutQuad(k) * 120;
                int n = (int)Math.Ceiling(rx * 7);
                for (int j = 0; j < n; j++)
                {
                    double g = ((double)j / n) * TAU;
                    p.Dot(Px.Cos(g) * rx, yc + Px.Sin(g) * ry, k < 0.3 ? WHITE : C[6], (1 - k) * fade * (Px.Sin(g) < 0 ? 0.6 : 1));
                }
            }
            // white-hot where it meets the floor
            p.Glow(0, -4, 34, C[6], 0.45 * fade);
            p.Glow(0, -60, 70, C[4], 0.25 * fade);
        }

        static readonly double[][] SHOCKS = { new[] { 0, R * 0.6, R * 2, 6, 0.5 }, new[] { 0.08, R * 0.4, R * 1.4, 3, 0.4 } };

        void Shock(Painter p, bool far)
        {
            foreach (double[] sh in SHOCKS)
            {
                double d = sh[0], r0 = sh[1], r1 = sh[2], th = sh[3], dur = sh[4];
                double k = (t - FLASH - d) / dur;
                if (k < 0 || k >= 1) continue;
                Sim.Band(p, 0, 0, r0 + (r1 - r0) * Ease.OutCubic(k), SY, Math.Max(1, th * (1 - k)), 1.15 - k, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) < 0 == far, k < 0.2);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // the pens shed glitter as they write
            if (t > PEN[0] && t < STAR[1] && Rg.Chance(0.5))
            {
                double x, y;
                if (t < PEN[1])
                {
                    double g = Math.PI / 2 + Rg.Sign() * ((t - PEN[0]) / (PEN[1] - PEN[0])) * Math.PI;
                    x = Px.Cos(g) * R;
                    y = Px.Sin(g) * R * SY;
                }
                else if (t < INNER[1])
                {
                    double g = -Math.PI / 2 + ((t - INNER[0]) / (INNER[1] - INNER[0])) * TAU;
                    x = Px.Cos(g) * RI;
                    y = Px.Sin(g) * RI * SY;
                }
                else
                {
                    Edge[] live = edges.FindAll(ed => t >= ed.t0 && t < ed.t0 + ed.dur).ToArray();
                    int pick = (int)Math.Floor(Rg.Next() * live.Length);
                    Edge e = pick < live.Length ? live[pick] : edges[0];
                    double s = Clamp((t - e.t0) / e.dur);
                    x = e.p0[0] + (e.p1[0] - e.p0[0]) * s;
                    y = (e.p0[1] + (e.p1[1] - e.p0[1]) * s) * SY;
                }
                ps.Spawn(P.Ember, x, y - 1, Rg.Range(-12, 12), Rg.Range(-30, -8), Rg.Range(0.25, 0.55), C[6], 1);
            }
            // gathering: light rushes in along the floor to the heart of the circle
            if (t > RUNES[1] - 0.1 && t < FLASH - 0.05 && Rg.Chance(0.7))
            {
                double g = Rg.Range(0, TAU), D = Rg.Range(R * 0.7, R * 1.1), T = Rg.Range(0.16, 0.24), v = (D * 3) / (Exp(3 * T) - 1);
                double x = Px.Cos(g) * D, y = Px.Sin(g) * D * SY - 1, l = Hypot(x, y + 2);
                if (l == 0 || double.IsNaN(l)) l = 1;
                ps.Spawn(P.Streak, x, y, (-x / l) * v, ((-2 - y) / l) * v, T, Rg.Chance(0.5) ? C[6] : C[5]);
            }
            if (!flashed && t >= FLASH)
            {
                flashed = true;
                lights.Add(0, -56, 104, C[5], 0.9, 0.6);
                lights.Add(0, -5, 36, WHITE, 0.8, 0.14);
                for (int i = 0; i < 36; i++)
                {
                    int s = Rg.Sign();
                    double sp = Rg.Range(80, 175);
                    ps.Spawn(P.Spark, s * Rg.Range(0, R * 0.5), -1, s * sp, -Rg.Range(30, 180), Rg.Range(0.25, 0.7));
                }
                for (int i = 0; i < 26; i++)
                    ps.Spawn(P.Star, Rg.Range(-R, R), -Rg.Range(1, 6), Rg.Range(-20, 20), -Rg.Range(40, 130), Rg.Range(0.5, 1.1), Rg.Pick(new[] { WHITE, C[6], C[5] }), Rg.Range(0.8, 2.3));
                // and a spray of twinkles flung out low across the floor
                for (int i = 0; i < 34; i++)
                {
                    int s = Rg.Sign();
                    ps.Spawn(P.Star, s * Rg.Range(0, 14), -Rg.Range(1, 4), s * Rg.Range(50, 140), -Rg.Range(10, 90), Rg.Range(0.4, 0.95), Rg.Pick(new[] { WHITE, C[6], C[6], C[5] }), Rg.Range(0.7, 2.2));
                }
            }
            // glints pop along the burning circle
            if (t > RUNES[0] && t < BREAK + 0.2 && Rg.Chance(0.12))
            {
                double g = Rg.Range(0, TAU), r = Rg.Pick(new[] { R, R2, RI });
                ps.Spawn(P.Star, Px.Cos(g) * r, Px.Sin(g) * r * SY - 0.5, 0, -Rg.Range(2, 8), Rg.Range(0.25, 0.45), Rg.Pick(new[] { WHITE, C[6] }), Rg.Range(0.8, 2));
            }
            // the column throws off stars, and light streams up inside it
            if (t > FLASH && t < BREAK)
            {
                if (Rg.Chance(0.5)) ps.Spawn(P.Star, Rg.Range(-1, 1) * W * 1.5, -Rg.Range(0, 120), Rg.Range(-10, 10), -Rg.Range(30, 85), Rg.Range(0.5, 1.1), Rg.Pick(new[] { WHITE, C[6], C[5] }), Rg.Range(0.7, 2));
                if (Rg.Chance(0.35)) ps.Spawn(P.Mote, Rg.Range(-1, 1) * W * 0.8, -Rg.Range(0, 50), 0, -Rg.Range(160, 300), Rg.Range(0.2, 0.35), Rg.Chance(0.5) ? WHITE : C[6], 0.8);
            }
            // it breaks apart into glints that twinkle as they drift up
            if (!broke && t >= BREAK)
            {
                broke = true;
                for (int i = 0; i < 46; i++)
                    ps.Spawn(P.Star, Rg.Range(-1, 1) * W * 0.9, -Rg.Range(2, TOP * 0.8), Rg.Range(-12, 12), -Rg.Range(8, 40), Rg.Range(0.5, 1.15), Rg.Pick(new[] { WHITE, WHITE, C[6], C[5] }), Rg.Range(0.6, 2.4));
            }
            if (t > BREAK && t < BREAK + 0.4 && Rg.Chance(0.8))
            {
                ps.Spawn(P.Star, Rg.Range(-1, 1) * W * 0.7, -Rg.Range(2, TOP * 0.78), Rg.Range(-8, 8), -Rg.Range(10, 50), Rg.Range(0.4, 0.9), Rg.Pick(new[] { WHITE, C[6], C[5] }), Rg.Range(0.6, 1.8));
                if (Rg.Chance(0.4)) ps.Spawn(P.Mote, Rg.Range(-1, 1) * W * 0.5, -Rg.Range(2, TOP * 0.7), 0, -Rg.Range(60, 140), Rg.Range(0.2, 0.4), C[6], 0.7);
            }
            // the runes go out one by one, each in a little puff of glints
            for (int k = 0; k < NRUNE; k++)
                if (!@out[k] && t >= RuneOut(k))
                {
                    @out[k] = true;
                    double g = RuneAngle(k), x = Px.Cos(g) * RR, y = Px.Sin(g) * RR * SY - 5;
                    for (int i = 0; i < 4; i++) ps.Spawn(P.Star, x + Rg.Range(-2, 2), y + Rg.Range(-2, 2), Rg.Range(-14, 14), -Rg.Range(10, 40), Rg.Range(0.3, 0.55), Rg.Pick(new[] { WHITE, C[6] }), Rg.Range(0.6, 1.6));
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double lvl = Level(t);
            if (t < GONE)
            {
                double k = lvl * Smooth(PEN[0], PEN[1] + 0.2, t);
                p.Glow(0, 0, R * 1.3, C[4], 0.28 * k);
                foreach (int s in new[] { -1, 1 }) p.Glow(s * R * 0.6, 1, R * 0.7, C[5], 0.12 * k);
            }
            Shock(p, true);
            Circle(p, true);
            Runes(p, true);
            Column(p);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            Shock(p, false);
            Circle(p, false);
            Runes(p, false);
            // the pens: white points running round, writing light
            void Pen(double x, double y)
            {
                Sim.Sparkle(p, x, y, 2, C[6], C[5], 0.5);
                p.Glow(x, y, 7, C[6], 0.45);
            }
            if (t > PEN[0] && t < PEN[1])
                foreach (int s in new[] { -1, 1 })
                {
                    double g = Math.PI / 2 + s * ((t - PEN[0]) / (PEN[1] - PEN[0])) * Math.PI;
                    Pen(Px.Cos(g) * R, Px.Sin(g) * R * SY);
                }
            if (t > INNER[0] && t < INNER[1])
            {
                double g = -Math.PI / 2 + ((t - INNER[0]) / (INNER[1] - INNER[0])) * TAU;
                Pen(Px.Cos(g) * RI, Px.Sin(g) * RI * SY);
            }
            foreach (Edge e in edges)
                if (t > e.t0 && t < e.t0 + e.dur)
                {
                    double s = (t - e.t0) / e.dur;
                    Pen(e.p0[0] + (e.p1[0] - e.p0[0]) * s, (e.p0[1] + (e.p1[1] - e.p0[1]) * s) * SY);
                }
            // the flash
            if (t >= FLASH && t < FLASH + 0.24)
            {
                double a = (t - FLASH) / 0.24;
                Sim.Flare(p, 0, -8, 34 * Px.Pow(1 - a, 1.5), 1 - a, C);
                p.Glow(0, -26, 56, WHITE, 0.5 * (1 - a));
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
