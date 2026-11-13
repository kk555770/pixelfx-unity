using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class Smite : Effect
    {
        public static readonly Module Module = new Module("smite", "pixelfx/fx/holy/smite", 224, 176, 112, 148, 2.8, false, 30,
            new Options { ["palette"] = GOLD, ["stone"] = "#5a5066", ["seed"] = 9 },
            (o, e) => new Smite(o, e))
        {
            Title = "Smite",
            Desc = "A glint marks the spot, then a bolt of light drops like a hammer: a white flash, a shockwave over the floor, stone chips flying and cracks that glow and cool.",
            Tags = new[] { "holy", "smite", "strike", "impact", "light", "ground", "one-shot" },
            Bg = "ground",
        };

        const double TOP = -148, SY = 0.38, MARK = 0.42, HIT = 0.54, LEN = 44;
        static readonly uint OUTLINE = Hex("#1a1422");

        // a chip of stone: lit side, shaded side, outline
        static void Chip(Painter p, Bits.Bit b, uint[] st)
        {
            double r = 0.6 + b.s * 0.7, fade = Math.Min(1, b.life / 0.5), lx = Px.Cos(b.a) * r * 0.4, ly = Px.Sin(b.a) * r * 0.4;
            p.Shade(b.x - r - 1, b.y - r - 1, b.x + r + 1, b.y + r + 1, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, d = Hypot(dx * 1.2, dy);
                if (d > r + 0.9) return 0;
                double lit = dx * lx + dy * ly;
                o.c = d > r ? OUTLINE : lit < -0.3 ? st[3] : lit > 0.5 ? st[1] : st[2];
                o.a = fade;
                return 0;
            });
        }

        sealed class Crack
        {
            public double[][] pts, fork;
            public double L, cool;
        }

        readonly uint[] C, st;
        readonly uint dust;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits chips;
        readonly Lights lights;
        readonly Crack[] cracks;
        double t;
        bool hit;

        Smite(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            uint s0 = o.Color("stone");
            st = new[] { Scale(s0, 0.5), Scale(s0, 0.75), s0, Mix(Scale(s0, 1.4), WHITE, 0.1) };
            dust = Mix(s0, Hex("#8a8094"), 0.4);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[3] }, emitter: e);
            chips = new Bits(x => 0, e);
            lights = new Lights(e);
            // cracks across the floor, mostly out to the sides and towards us, each its own length
            cracks = new Crack[7];
            for (int i = 0; i < 7; i++)
            {
                double a = 0.12 + (i / 6.0) * (Math.PI - 0.24) + R.Range(-0.12, 0.12), L = R.Range(30, 54) * (Math.Abs(Px.Sin(a)) > 0.7 ? 0.6 : 1);
                double[][] raw = Shapes.Bolt(R, 0, 0, Px.Cos(a) * L, Px.Sin(a) * L, 0.28, 4);
                var pts = new double[raw.Length][];
                for (int j = 0; j < raw.Length; j++) pts[j] = new[] { raw[j][0], raw[j][1] * SY };
                double[][] fork = null;
                if (pts.Length > 6)
                {
                    double[] m = pts[(int)Math.Floor(pts.Length * 0.5)];
                    double x1 = m[0] + Px.Cos(a + R.Sign() * 0.7) * L * 0.35;
                    double y1 = m[1] + Px.Sin(a + R.Sign() * 0.7) * L * 0.35 * SY;
                    fork = Shapes.Bolt(R, m[0], m[1], x1, y1, 0.3, 3);
                }
                cracks[i] = new Crack { pts = pts, fork = fork, L = L, cool = R.Range(1.6, 2.1) };
            }
        }

        // the hammer of light: a heavy blunt bolt that falls faster and faster
        static double TipY(double tt) => Lerp(TOP - 6, 0, Ease.InCubic(Seg(tt, MARK, HIT)));

        static readonly double[][] SPEED = { new[] { -5, 0.6 }, new[] { -2, 0.9 }, new[] { 2, 0.8 }, new[] { 5, 0.5 } };

        void Hammer(Painter p)
        {
            if (t < MARK || t >= HIT + 0.06) return;
            double y1 = t >= HIT ? 0 : TipY(t), y0 = Math.Max(TOP + 5, y1 - LEN * (t >= HIT ? 1 - (t - HIT) / 0.06 : 1));
            bool white = t >= HIT;
            if (y1 < TOP + 12) return;
            // speed lines above it
            foreach (double[] s in SPEED)
            {
                double dx = s[0], k = s[1];
                if (y0 > TOP + 2) p.Line(dx, Math.Max(TOP, y0 - 70), dx, y0, u => u > 0.7 ? C[6] : C[4], u => u * k);
            }
            if (y1 - 3 > y0) p.Beam(0, y0, 0, y1 - 3, 9, (u, v) => white || v < 0.35 ? WHITE : v < 0.7 ? C[6] : C[5]);
            // the head: wider, white hot
            p.Beam(0, y1 - 6, 0, y1 - 1, 13, (u, v) => v < 0.6 || white ? WHITE : C[6]);
            p.GlowLine(0, y0 + 10, 0, y1, 16, C[5], 0.6);
            p.Glow(0, y1 - 4, Math.Min(22, y1 - TOP - 6), WHITE, 0.5);
        }

        void DrawCracks(Painter p)
        {
            if (t < HIT) return;
            double grow = Ease.OutCubic(Seg(t, HIT, HIT + 0.1));
            foreach (Crack c in cracks)
            {
                foreach (double[][] path in c.fork != null ? new[] { c.pts, c.fork } : new[] { c.pts })
                {
                    double n = Math.Max(1, Round((path.Length - 1) * grow));
                    for (int i = 0; i < n; i++)
                    {
                        double x0 = path[i][0], y0 = path[i][1], x1 = path[i + 1][0], y1 = path[i + 1][1], d = Hypot(x0, y0 / SY) / c.L, heat = Clamp(1 - (t - HIT) / c.cool - d * 0.5);
                        if (heat <= 0) continue;
                        uint col = heat > 0.85 ? WHITE : heat > 0.65 ? C[6] : heat > 0.45 ? C[5] : heat > 0.3 ? C[4] : heat > 0.18 ? C[3] : C[2];
                        p.Line(x0, y0, x1, y1, col, heat < 0.12 ? heat / 0.12 : 1);
                        // wide and deep near the heart, a hairline at the tips
                        if (d < 0.45 && path == c.pts) p.Line(x0, y0 + 1, x1, y1 + 1, heat > 0.5 ? C[4] : C[2], heat < 0.12 ? heat / 0.12 : 1);
                        if (i % 2 == 0) p.Glow(x0, y0, 10, C[4], 0.45 * heat);
                    }
                }
            }
            // the crater glows at the heart and cools
            double h = Clamp(1 - (t - HIT) / 1.6);
            if (h > 0)
            {
                Oval(p, 0, 0, 5, 1.6, h > 0.6 ? WHITE : h > 0.3 ? C[5] : C[3], Math.Min(1, h * 3));
                p.Glow(0, 0, 18, C[4], 0.45 * h);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < MARK && R.Chance(0.25)) ps.Spawn(P.Star, R.Range(-10, 10), -R.Range(0, 18), 0, -R.Range(4, 12), R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : C[6], R.Range(0.5, 1.2));
            // sparks shed off the sides of the falling hammer
            if (t > MARK && t < HIT && TipY(t) > TOP + 50 && R.Chance(0.6)) ps.Spawn(P.Mote, R.Sign() * R.Range(4, 7), TipY(t) - R.Range(4, 30), R.Range(-20, 20), -R.Range(10, 40), R.Range(0.15, 0.3), C[6], 0.7);
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(0, -24, 92, C[5], 1, 0.6);
                lights.Add(0, -8, 40, WHITE, 0.9, 0.12);
                for (int i = 0; i < 60; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.25, 1.25), sp = R.Range(110, 280);
                    ps.Spawn(P.Spark, R.Range(-3, 3), -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.75));
                }
                for (int i = 0; i < 16; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1, 1), sp = R.Range(60, 160);
                    ps.Spawn(P.Star, R.Range(-4, 4), -4, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.4, 0.9), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 2.4));
                }
                for (int i = 0; i < 14; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(90, 230);
                    chips.Add(new Bits.Bit { kind = B.Chunk, x = R.Range(-5, 5), y = -2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 1, drag = 0.4, bounce = 0.35, life = R.Range(1.2, 1.8), a = R.Range(0, TAU), va = R.Range(-14, 14), s = R.Int(1, 3) });
                }
                uint[] grit = Slice(st, 1);
                for (int i = 0; i < 34; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1, 1), sp = R.Range(60, 220);
                    ps.Spawn(P.Grit, R.Range(-4, 4), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.9, 1.5), R.Pick(grit), R.Chance(0.4) ? 2 : 1);
                }
                // dust rolls out along the floor, a little smoke lifts off the crater
                for (int i = 0; i < 9; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(2, 10), -R.Range(1, 4), s * R.Range(50, 130), -R.Range(2, 12), R.Range(0.7, 1.2), Mix(dust, C[4], 0.18), R.Range(3.5, 6), R.Range(0, 0.06), PF.Big);
                }
                for (int i = 0; i < 4; i++) ps.Spawn(P.Smoke, R.Range(-4, 4), -R.Range(2, 8), R.Range(-8, 8), -R.Range(20, 45), R.Range(1.2, 1.8), Mix(dust, C[5], 0.25), R.Range(4, 6), R.Range(0.05, 0.3));
            }
            // the cracks spit embers while they're hot
            if (t > HIT && t < HIT + 1.3 && R.Chance(0.5))
            {
                Crack c = R.Pick(cracks);
                double[] q = R.Pick(c.pts);
                if (R.Chance(0.75)) ps.Spawn(P.Ember, q[0], q[1] - 1, R.Range(-8, 8), -R.Range(15, 35), R.Range(0.5, 0.9), C[5]);
                else ps.Spawn(P.Star, q[0], q[1] - 2, 0, -R.Range(4, 12), R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : C[6], R.Range(0.8, 1.8));
            }
            chips.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            DrawCracks(p);
            ps.Draw(p, Layer.Back);
            double k = Seg(t, HIT, HIT + 0.42);
            if (t >= HIT && k < 1) Sim.Band(p, 0, 0, 6 + 84 * Ease.OutCubic(k), SY * 0.8, Math.Max(1, 5 * (1 - k)), 1.15 - k, Slice(C, 3), g => Px.Sin(g) < 0, k < 0.12);
        }

        public override void Draw(Painter p)
        {
            // the mark: a glint on the floor and a ring closing on it
            if (t < HIT)
            {
                double u = Seg(t, 0, MARK), k = Smooth(0, 0.08, t), spin = t * 3, rr = Lerp(26, 8, Ease.InOutCubic(u));
                Oval(p, 0, 0, rr, rr * SY, g => Px.Sin(g * 4 + spin) > 0 ? WHITE : C[5], k * (0.65 + 0.35 * Px.Sin(t * 40)));
                Oval(p, 0, 1, rr, rr * SY, C[3], k * 0.6, g => Px.Sin(g) > 0);
                double L = (3 + 8 * Ease.OutBack(Clamp(t / 0.2), 2)) * (t > MARK ? 1 + 0.6 * Seg(t, MARK, HIT) : 1);
                for (int i = 0; i < 4; i++)
                {
                    double a = spin * 0.3 + (i * Math.PI) / 2, len = i % 2 != 0 ? L * 0.6 : L;
                    p.Line(0, -2, Px.Cos(a) * len, -2 + Px.Sin(a) * len, v => v < 0.4 ? WHITE : C[5], v => 1.2 - v);
                }
                p.Px(0, -2, WHITE);
                p.Glow(0, -2, 14 + 10 * u, C[5], 0.5 * k);
                p.GlowLine(0, TOP + 6, 0, -2, 5, C[4], 0.12 * u);
            }
            Hammer(p);
            // the bolt's afterimage: a thread of light from the sky to the mark, thinning out
            double ai = Seg(t, HIT + 0.04, HIT + 0.55);
            if (t > HIT + 0.04 && ai < 1)
            {
                double w = 1 + 4 * Px.Pow(1 - ai, 2);
                p.Beam(0, TOP + 4, 0, -4, w, (u, v) => v < 0.5 ? WHITE : C[5], (u, v) => (0.3 + 0.7 * u) * (1 - ai * 0.5));
                p.GlowLine(0, TOP + 16, 0, -6, 10, C[5], 0.4 * (1 - ai));
            }
            double kb = Seg(t, HIT, HIT + 0.42);
            if (t >= HIT && kb < 1) Sim.Band(p, 0, 0, 6 + 84 * Ease.OutCubic(kb), SY * 0.8, Math.Max(1, 5 * (1 - kb)), 1.15 - kb, Slice(C, 3), g => Px.Sin(g) >= 0, kb < 0.12);
            // the air shock: a dome of light thrown up over the hit
            double d = Seg(t, HIT, HIT + 0.28);
            if (t >= HIT && d < 1)
            {
                double r = 8 + 40 * Ease.OutCubic(d);
                p.Arc(0, 0, r, Math.Max(1, 3 * (1 - d)), Math.PI, TAU, (u, v) => d < 0.2 || v > 0.6 ? WHITE : C[5], 1.1 - d);
                for (double g = Math.PI * 1.1; g < TAU - 0.2; g += 0.5) p.Glow(Px.Cos(g) * r, Px.Sin(g) * r, 8, C[5], 0.3 * (1 - d));
            }
            // the white hero frame
            if (t >= HIT && t < HIT + 0.07)
            {
                p.Circle(0, -6, 16, WHITE);
                p.Circle(0, -6, 19, C[6], 0.6);
            }
            foreach (Bits.Bit b in chips.List) Chip(p, b, st);
            ps.Draw(p, Layer.Front);
            if (t >= HIT && t < HIT + 0.25) Sim.Sparkle(p, 0, -8, 16 * (1 - (t - HIT) / 0.25), WHITE, C[5], 0.7);
            lights.Draw(p);
        }
    }
}
