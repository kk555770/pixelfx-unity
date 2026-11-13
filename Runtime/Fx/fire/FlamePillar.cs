// The flame-puff, smoke, spark, debris and floor-ring recipe is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), and the column's levels, pulses and strands from its
// holy light (https://www.spritefusion.com/pixel-art-vfx/holy-light), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class FlamePillar : Effect
    {
        const double DURATION = 5;

        public static readonly Module Module = new Module("flame-pillar", "pixelfx/fx/fire/flame-pillar", 184, 256, 92, 232, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["stone"] = "#4a3f69",
                ["height"] = 140,
                ["seed"] = 5,
            },
            (o, e) => new FlamePillar(o, e))
        {
            Title = "Flame pillar",
            Desc = "The floor cracks and glows, a column of fire erupts white-hot and roars, spewing embers and shedding flame, then collapses into rolling smoke.",
            Tags = new[] { "fire", "pillar", "eruption", "spell", "ground", "one-shot" },
            Bg = "ground",
        };

        const double ERUPT = 0.55, RISE = 0.12, FALL = 1.95, GONE = 2.45;
        static readonly uint SOOT = Hex("#0d0a12"), WARM = Rgb(255, 176, 96);

        sealed class Crack
        {
            public List<double[]> pts;
            public double t0, grow, w;
        }

        sealed class Strand
        {
            public double o, w, f;
        }

        readonly uint[] H;
        readonly double HMAX, S;
        readonly Stone st;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Emitter emitter;
        readonly uint GLOW;
        readonly List<Crack> cracks = new List<Crack>();
        readonly Strand[] strands;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t;
        bool erupted, fell;

        FlamePillar(Options o, Emitter e)
        {
            uint[] F = Fit(o.Ramp("palette"), 9);
            H = HotFirst(F);
            HMAX = o.Num("height");
            st = new Stone(o.Color("stone"));
            R = new Rng(o.Seed);
            emitter = e;
            ps = new Particles(R, ground: x => 0, sparks: new[] { H[1], H[2], H[4], H[5] }, emitter: e);
            bits = new Bits(x => 0, e);
            lights = new Lights(e);
            GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25);
            S = o.Seed * 13;
            // the cracks: jagged lines racing out from the middle across the floor (squashed for perspective), with branches
            for (int i = 0; i < 9; i++)
            {
                double a = (i / 9.0) * TAU + R.Range(-0.25, 0.25);
                List<double[]> pts = AddCrack(R.Range(-1, 1), R.Range(-0.3, 0.3), a, R.Range(20, 40), R.Range(0, 0.16), 1);
                int j = 3 + R.Int(0, pts.Count - 6);
                if (pts.Count > 8) AddCrack(pts[j][0], pts[j][1], a + R.Sign() * R.Range(0.5, 1), R.Range(6, 14), 0.18 + R.Range(0, 0.15), 0);
            }
            // strands of hotter flame running up the column
            strands = new Strand[7];
            for (int i = 0; i < 7; i++) strands[i] = new Strand { o = (Rand(i, S, 1) - 0.5) * 1.4, w = 0.05 + Rand(i, S, 2) * 0.06, f = 1 + Rand(i, S, 3) * 2 };
        }

        List<double[]> AddCrack(double x, double y, double a, double len, double t0, double w)
        {
            var pts = new List<double[]> { new[] { x, y } };
            for (double d = 0; d < len; d += 2)
            {
                a += R.Range(-0.55, 0.55);
                x += Px.Cos(a) * 2;
                y += Px.Sin(a) * 0.76;
                pts.Add(new[] { x, y });
            }
            cracks.Add(new Crack { pts = pts, t0 = t0, grow = R.Range(0.22, 0.36), w = w });
            return pts;
        }

        double Height(double tt) => tt < ERUPT ? 0 : tt < ERUPT + RISE ? HMAX * Ease.OutCubic((tt - ERUPT) / RISE) : tt < FALL ? HMAX * (1 + 0.05 * Noise(tt * 3, S)) : HMAX * (1 - Ease.InQuad(Clamp((tt - FALL) / (GONE - FALL))));

        double Width(double tt) => tt < ERUPT ? 0 : tt < FALL ? 12 * (1 + 0.45 * Exp(-(tt - ERUPT) * 7)) * (0.94 + 0.06 * Noise(tt * 9, S + 1)) : 12 * (1 - Smooth(FALL, GONE, tt)) + 1.5 * (1 - Smooth(GONE - 0.1, GONE, tt));

        double Heat(double tt) => Smooth(0, ERUPT, tt) * 0.75 + 0.25 * Smooth(ERUPT - 0.05, ERUPT + 0.1, tt) - Smooth(GONE - 0.3, DURATION - 0.6, tt) * 1.05;

        void Column(Painter p)
        {
            double h = Height(t), w0 = Width(t);
            if (h < 1 || w0 < 0.3) return;
            // a crown of tongues where the column breaks into the air, drawn first so the column flows up into them
            if (t < FALL + 0.25)
            {
                double k = Clamp(w0 / 12);
                FrameNoise NZ = (rate, seed, off) => Noise(t * rate + off, seed);
                for (int i = 0; i < 5; i++)
                {
                    double n = (S + i) * 17, dx = (i - 2) * w0 * 0.4, th = (12 + 12 * (0.5 + 0.5 * Noise(t * 3.3 + i, n))) * k * (i == 2 ? 1.2 : i % 4 == 0 ? 0.6 : 0.9);
                    FlameTongue(p, dx + Noise(t * 2, n + 2) * 1.5, -h + 12, th, 3.4 * k * (i % 4 == 0 ? 0.7 : 1), -dx * 0.3 + Noise(t * 2.2, n + 1) * 3, NZ, n, H, LAYERS);
                }
            }
            uint[] lvlC = { H[0], H[1], H[2], H[3], H[4], H[5], H[6], H[7] };
            for (double y = 0; y >= -h; y--)
            {
                double v = -y / h, cx = Noise(-y * 0.04 + t * 1.6, S + 2) * 1.6 * v;
                // licks bulge out of the sides and run up them; a wider splash at the foot; the top thins into tongues
                double foot = 1 + 0.7 * Px.Pow(Math.Max(0, 1 - -y / 12), 2), top = 1 - Smooth(0.82, 1, v) * 0.7;
                double wl = w0 * foot * top * (1 + 0.65 * Px.Pow(Math.Max(0, Noise(y * 0.2 + t * 8, S + 3)), 1.5)), wr = w0 * foot * top * (1 + 0.65 * Px.Pow(Math.Max(0, Noise(y * 0.2 + t * 8, S + 4)), 1.5));
                int pulse = ((-y * 0.032 + t * 3.6) % 1) < 0.09 ? 1 : 0;
                for (double x = Math.Floor(cx - wl * 1.15); x <= Math.Ceiling(cx + wr * 1.15); x++)
                {
                    double dx = x + 0.5 - cx, q = Math.Abs(dx) / (dx < 0 ? wl : wr);
                    if (q > 1.15) continue;
                    int lvl = q < 0.12 ? 0 : q < 0.26 ? 1 : q < 0.42 ? 2 : q < 0.56 ? 3 : q < 0.7 ? 4 : q < 0.84 ? 5 : q < 0.96 ? 6 : 7;
                    foreach (Strand s in strands)
                        if (Math.Abs(dx / w0 - s.o - 0.06 * Px.Sin(t * s.f * 3 + s.o * 9)) < s.w) lvl = Math.Max(0, lvl - 1);
                    lvl = Math.Max(0, lvl - pulse);
                    if (lvl == 7 && Rand(x, y, Math.Floor(t * 20)) < 0.5) continue;
                    p.Px(x, y, lvlC[lvl]);
                }
                if (y % 12 == 0) p.Glow(cx, y, w0 * 2.4 + 8, GLOW, 0.28 * Clamp(w0 / 12));
            }
            p.Glow(0, -h * 0.45, 92, GLOW, 0.3 * Clamp(w0 / 12));
        }

        void CracksDraw(Painter p)
        {
            double k = Heat(t);
            if (k <= -0.05) return;
            foreach (Crack c in cracks)
            {
                int n = (int)Math.Floor(c.pts.Count * Clamp((t - c.t0) / c.grow));
                for (int i = 1; i < n; i++)
                {
                    double u = (double)i / c.pts.Count, hot = k * (1 - 0.45 * u), x0 = c.pts[i - 1][0], y0 = c.pts[i - 1][1], x1 = c.pts[i][0], y1 = c.pts[i][1];
                    if (hot < 0)
                    {
                        // cooled: soot that fades away
                        double a = 1 + hot * 3;
                        if (a > 0) p.Line(x0, y0, x1, y1, SOOT, a);
                        continue;
                    }
                    uint col = hot > 0.85 ? H[1] : hot > 0.65 ? H[2] : hot > 0.45 ? H[4] : hot > 0.25 ? H[5] : hot > 0.1 ? H[6] : H[7];
                    // the crack: a hot seam with a darker glowing lip, wider near the middle
                    if (c.w != 0 && i < c.pts.Count * 0.5 && hot > 0.2)
                    {
                        p.Line(x0, y0 + 1, x1, y1 + 1, hot > 0.6 ? H[5] : H[6]);
                        p.Line(x0, y0 - 1, x1, y1 - 1, H[7]);
                    }
                    p.Line(x0, y0, x1, y1, col);
                    if (i % 2 == 0) p.Glow(x1, y1, 5 + 7 * hot, GLOW, 0.3 * hot);
                }
            }
            // the floor glows from beneath while it's hot
            if (k > 0) FloorLight(p, 0, 0.5, 44, 8, GLOW, 0.35 * k);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the charge: the crust in the middle glows, little flames lick out of it, heat leaks up the cracks
            if (t < ERUPT && R.Chance(0.6 * dt * 60 * Smooth(0.15, ERUPT, t))) ps.Spawn(P.Fire, R.Range(-5, 5), -1, R.Range(-6, 6), -R.Range(20, 45), R.Range(0.15, 0.3), 0, R.Range(1, 2.2));
            if (t < ERUPT && R.Chance(0.5 * dt * 60 * Smooth(0, ERUPT, t)))
            {
                Crack c = cracks[(int)Math.Floor(R.Next() * cracks.Count)];
                int i = R.Int(0, (int)Math.Floor(c.pts.Count * Clamp((t - c.t0) / c.grow)) - 1);
                if (i >= 0)
                {
                    double x = c.pts[i][0], y = c.pts[i][1];
                    if (R.Chance(0.5)) ps.Spawn(P.Fire, x, y - 1, R.Range(-5, 5), -R.Range(15, 35), R.Range(0.15, 0.3), 0, R.Range(0.8, 1.6));
                    else ps.Spawn(P.Ember, x, y - 1, R.Range(-15, 15), -R.Range(30, 70), R.Range(0.5, 1), H[3], 1);
                }
            }
            if (!erupted && t >= ERUPT)
            {
                erupted = true;
                lights.Add(0, -40, 90, WARM, 0.7, 0.6);
                lights.Add(0, -8, 44, WHITE, 0.7, 0.1);
                for (int i = 0; i < 70; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(100, 260);
                    ps.Spawn(P.Spark, R.Range(-4, 4), -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.7));
                }
                for (int i = 0; i < 18; i++)
                    bits.Add(new Bits.Bit { kind = B.Chunk, x = R.Range(-6, 6), y = -2, vx = R.Range(-110, 110), vy = -R.Range(150, 330), g = 1, drag = 0.4, bounce = 0.3, life = R.Range(1.6, 2.4), a = R.Range(0, TAU), va = R.Range(-16, 16), s = R.Int(1, 3) });
                for (int i = 0; i < 60; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-0.9, 0.9), sp = R.Range(80, 260);
                    ps.Spawn(P.Grit, R.Range(-5, 5), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.8), R.Pick(new[] { st.light, st.body, st.body, st.shade }), R.Chance(0.4) ? 2 : 1);
                }
                for (int i = 0; i < 14; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Smoke, s * R.Range(2, 10), -R.Range(1, 4), s * R.Range(40, 110), -R.Range(2, 10), R.Range(1, 1.8), st.dust, R.Range(4, 7), R.Range(0, 0.06), PF.Big);
                }
            }
            // the roar: flame shed off the sides, a billowing cap, embers spewed high
            double h = Height(t), w = Width(t);
            if (t > ERUPT && t < GONE && h > 4)
            {
                if (R.Chance(0.9 * dt * 60))
                {
                    int s = R.Sign();
                    double y = -R.Range(4, h * 0.9);
                    ps.Spawn(P.Fire, s * w * R.Range(0.7, 1.1), y, s * R.Range(15, 40), -R.Range(30, 70), R.Range(0.25, 0.5), 0, R.Range(1.6, 3.2), 0, R.Chance(0.35) ? PF.Smokes : PF.None);
                }
                if (R.Chance(0.7 * dt * 60))
                    ps.Spawn(P.Fire, R.Range(-w, w), -h + R.Range(0, 6), R.Range(-20, 20), -R.Range(30, 80), R.Range(0.35, 0.7), 0, R.Range(3, 5.5), 0,
                        R.Chance(0.6) ? PF.Smokes | (t < FALL - 0.3 ? PF.Big : PF.None) : PF.None);
                if (t < FALL && R.Chance(0.5 * dt * 60)) ps.Spawn(P.Ember, R.Range(-w, w) * 0.6, -h + 4, R.Range(-60, 60), -R.Range(50, 110), R.Range(0.7, 1.3), H[3], R.Chance(0.2) ? 2 : 1);
            }
            // the collapse: the falling top breaks into puffs that roll off as smoke
            if (t >= FALL && !fell) fell = true;
            if (fell && t < GONE - 0.1 && R.Chance(1.6 * dt * 60)) ps.Spawn(P.Fire, R.Range(-w - 2, w + 2), -h - R.Range(0, 8), R.Range(-30, 30), -R.Range(10, 50), R.Range(0.4, 0.75), 0, R.Range(3.5, 6.5), 0, PF.Smokes);
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
                if (b.rest && dusted.Add(b))
                    for (int i = 0; i < 3; i++)
                    {
                        ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 35), -R.Range(2, 18), R.Range(0.3, 0.6), st.dust, R.Range(1.3, 2.6));
                        ps.BornAt(b.bornX, b.bornY);
                    }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            CracksDraw(p);
            SimDraw(p, ps, H, Layer.Back, emitter);
            double k = (t - ERUPT) / 0.45;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0.5, 84 * Ease.OutCubic(k), 0.18, Math.Max(1, 4 * (1 - k)), 1.15 - k, new[] { st.body, st.light, H[4], H[1] }, g => Px.Sin(g) < 0);
        }

        public override void Draw(Painter p, double tt)
        {
            Column(p);
            foreach (Bits.Bit b in bits.List) Rock(p, emitter, b, st);
            p.Shift(0, 0);
            SimDraw(p, ps, H, Layer.Front, emitter);
            double k = (t - ERUPT) / 0.45;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0.5, 84 * Ease.OutCubic(k), 0.18, Math.Max(1, 4 * (1 - k)), 1.15 - k, new[] { st.body, st.light, H[4], H[1] }, g => Px.Sin(g) >= 0, k < 0.15);
            // the eruption's white flash at the foot
            double f = (t - ERUPT) / 0.08;
            if (f > 0 && f < 1)
            {
                p.Ellipse(0, -2, 16 * (1 - f * 0.3), 6, H[2]);
                p.Ellipse(0, -3, 11 * (1 - f * 0.3), 4, f < 0.5 ? WHITE : H[1]);
            }
            lights.Draw(p);
        }
    }
}
