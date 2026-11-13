using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class HexSigil : Effect
    {
        public static readonly Module Module = new Module("hex-sigil", "pixelfx/fx/shadow/hex-sigil", 168, 128, 84, 112, 4.2, false, 30,
            new Options { ["palette"] = VOID, ["seed"] = 7 },
            (o, e) => new HexSigil(o, e))
        {
            Title = "Hex sigil",
            Desc = "A burning pen traces a cursed sigil in the air, its glyphs pop in one by one, then it flares white and crumbles into falling embers.",
            Tags = new[] { "shadow", "hex", "sigil", "rune", "curse", "magic", "circle", "one-shot" },
            Bg = "ground",
        };

        // the sigil's centre and radii; the beats: traced, glyphs pop, holds, flares, crumbles
        const double CY = -50, RO = 28, RI = 22, RC = 6, GLYPH = 1.62, FLARE = 2.6, BREAK = 2.74;

        sealed class Pt
        {
            public double x, y, t;
        }

        sealed class Glyph
        {
            public double x, y, t;
            public List<int[][]> segs;
            public bool popped;
        }

        static double[][] Circle(double r, double a0, int n = 0)
        {
            int m = n != 0 ? n : (int)Math.Ceiling(r * TAU);
            var o = new double[m + 1][];
            for (int i = 0; i <= m; i++) o[i] = new[] { Px.Cos(a0 + ((double)i / m) * TAU) * r, CY + Px.Sin(a0 + ((double)i / m) * TAU) * r };
            return o;
        }

        // the strokes, in the order the pen draws them, as points with the moment the pen passes each
        static (List<Pt> pts, double end) Strokes()
        {
            var paths = new List<double[][]> { Circle(RO, -Math.PI / 2) };
            // a seven-pointed star, drawn in one stroke, three points round each time
            var star = new double[8][];
            for (int i = 0; i <= 7; i++)
            {
                double a = -Math.PI / 2 + ((i * 3) % 7) * (TAU / 7);
                star[i] = new[] { Px.Cos(a) * RI, CY + Px.Sin(a) * RI };
            }
            paths.Add(star);
            paths.Add(Circle(RI, -Math.PI / 2));
            paths.Add(Circle(RC, Math.PI / 2));
            paths.Add(new[] { new[] { 0, CY - 3.5 }, new[] { 2, CY }, new[] { 0, CY + 3.5 }, new[] { -2, CY }, new[] { 0, CY - 3.5 } });
            // sample every half pixel, timing the pen along it: the outer circle fast, the star with more weight
            var pts = new List<Pt>();
            double[] speeds = { 420, 700, 550, 380, 300 };
            double t = 0.12;
            for (int k = 0; k < paths.Count; k++)
            {
                double[][] path = paths[k];
                for (int i = 1; i < path.Length; i++)
                {
                    double x0 = path[i - 1][0], y0 = path[i - 1][1], x1 = path[i][0], y1 = path[i][1], L = Hypot(x1 - x0, y1 - y0);
                    for (double d = 0; d < L; d += 0.5) pts.Add(new Pt { x = x0 + ((x1 - x0) * d) / L, y = y0 + ((y1 - y0) * d) / L, t = t + d / speeds[k] });
                    t += L / speeds[k];
                }
                t += 0.04;
            }
            return (pts, t);
        }

        // seven small glyphs of our own: a spine and two or three branches on a 3x3 grid of nodes
        static Glyph[] Glyphs(double seed)
        {
            var G = new Glyph[7];
            for (int i = 0; i < 7; i++)
            {
                Rng r = NewRng(seed, i, 31);
                var segs = new List<int[][]> { new[] { new[] { 1, 0 }, new[] { 1, 2 } } };
                for (int k = 0; k < 2 + (r.Next() < 0.5 ? 1 : 0); k++)
                {
                    int a0 = r.Int(0, 2), a1 = r.Int(0, 2);
                    int b0 = Math.Max(0, Math.Min(2, a0 + r.Int(-1, 1))), b1 = Math.Max(0, Math.Min(2, a1 + r.Int(-1, 1)));
                    if (a0 != b0 || a1 != b1) segs.Add(new[] { new[] { a0, a1 }, new[] { b0, b1 } });
                }
                double ang = -Math.PI / 2 + (i * TAU) / 7 + Math.PI / 7, rr = (RO + RI) / 2;
                G[i] = new Glyph { x = Px.Cos(ang) * rr, y = CY + Px.Sin(ang) * rr, segs = segs, t = GLYPH + i * 0.07 };
            }
            return G;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly List<Pt> pts;
        readonly double end;
        readonly Glyph[] G;
        readonly Particles ps, embers;
        readonly Puffs puffs;
        readonly Bits bits;
        readonly List<Lamp> lights = new List<Lamp>();
        double t;
        int pi;
        bool flared, broke;

        HexSigil(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            (pts, end) = Strokes();
            G = Glyphs(o.Seed);
            ps = new Particles(R, ground: floor, sparks: new[] { WHITE, C[7], C[6], C[5] }, emitter: e);
            embers = new Particles(R, ground: floor, sparks: new[] { C[7], C[6], C[5], C[4] }, emitter: e);
            puffs = new Puffs(R, new[] { WHITE, C[7], C[6], C[5], C[4], C[3], C[2], C[1], C[0] }, new[] { Mix(C[3], C[2], 0.5), Mix(C[2], C[1], 0.5) }, Mix(C[4], C[2], 0.5), new[] { C[6], C[5], C[4] }, floor, e);
            bits = new Bits(floor, e);
        }

        Pt Pen() => pts[Math.Min(pts.Count - 1, pi)];

        public override void Update(double dt)
        {
            t += dt;
            // the pen: it spits sparks and drips burning embers as it writes
            while (pi < pts.Count - 1 && pts[pi + 1].t <= t) pi++;
            if (t > 0.12 && t < end)
            {
                Pt q = Pen();
                if (R.Chance(dt * 70)) ps.Spawn(P.Spark, q.x, q.y, R.Range(-70, 70), R.Range(-100, 20), R.Range(0.12, 0.35));
                if (R.Chance(dt * 10)) embers.Spawn(P.Ember, q.x, q.y, R.Range(-12, 12), R.Range(5, 25), R.Range(0.5, 0.9));
            }
            // power drawn into the sigil while it's written and held: streaks racing in to its heart
            if (t > 0.2 && t < FLARE - 0.05 && R.Chance(dt * (8 + 14 * Smooth(0.2, FLARE, t))))
            {
                double a = R.Range(0, TAU), D0 = R.Range(40, 60), T = R.Range(0.22, 0.32), v = (D0 * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, Px.Cos(a) * D0, CY + Px.Sin(a) * D0, -Px.Cos(a) * v, -Px.Sin(a) * v, T, R.Chance(0.5) ? C[7] : C[6]);
            }
            foreach (Glyph g in G)
                if (!g.popped && t >= g.t)
                {
                    g.popped = true;
                    lights.Add(new Lamp(g.x, g.y, 14, 0, 0.7, 0.2));
                    for (int i = 0; i < 4; i++) ps.Spawn(P.Spark, g.x, g.y, R.Range(-60, 60), R.Range(-70, 20), R.Range(0.1, 0.25));
                }
            if (t >= FLARE && !flared)
            {
                flared = true;
                lights.Add(new Lamp(0, CY, 96, 0, 1.4, 0.45));
                lights.Add(new Lamp(0, CY, 44, 0, 1, 0.14));
                for (int i = 0; i < 24; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(60, 130);
                    ps.Spawn(P.Spark, Px.Cos(a) * 13, CY + Px.Sin(a) * 13, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.2, 0.45));
                }
            }
            if (t >= BREAK && !broke)
            {
                // it crumbles: every stroke breaks into burning fragments that tumble down and bounce
                broke = true;
                // pieces drop nearly straight at first, so the sigil's shape lingers as it falls apart
                for (int i = 0; i < pts.Count - 1; i += 7)
                {
                    Pt q = pts[i], n = pts[i + 1];
                    double a = Atan2(n.y - q.y, n.x - q.x);
                    bits.Add(new Bits.Bit { kind = B.Shard, x = q.x, y = q.y, vx = R.Range(-8, 8), vy = R.Range(-12, 6), g = R.Range(0.22, 0.45), drag = 0.6, bounce = 0.25, life = R.Range(0.8, 1.2), a = a, va = R.Range(-3, 3), s = R.Range(2.5, 3.5), c = R.Pick(new[] { C[7], C[6], C[6], C[5] }) });
                    if (i % 27 == 0) embers.Spawn(P.Ember, q.x, q.y, R.Range(-10, 10), -R.Range(10, 40), R.Range(0.6, 1.1));
                }
                foreach (Glyph g in G) puffs.Spawn(PK.Soot, g.x, g.y, R.Range(-6, 6), -R.Range(8, 18), R.Range(0.7, 1.1), R.Range(3, 5));
                for (int i = 0; i < 8; i++) puffs.Spawn(PK.Soot, R.Range(-20, 20), CY + R.Range(-20, 20), R.Range(-8, 8), -R.Range(6, 16), R.Range(0.8, 1.2), R.Range(3, 6), R.Range(0, 0.15));
            }
            for (int i = lights.Count - 1; i >= 0; i--)
                if (!lights[i].Tick(dt)) lights.RemoveAt(i);
            bits.Update(dt);
            ps.Update(dt);
            embers.Update(dt);
            puffs.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            puffs.Draw(p, PK.Soot, new PuffLight(0, CY, broke ? 0.3 : 0, 50));
            double k = t < BREAK ? Smooth(0.1, 0.6, t) : 1 - Smooth(BREAK, BREAK + 0.6, t);
            if (k > 0) p.Glow(0, CY, 52, C[4], 0.3 * k);
        }

        public override void Draw(Painter p)
        {
            if (!broke)
            {
                // the traced lines: white where the pen just passed, cooling to violet; pulsing as it holds; white in the flare
                double hold = t > end ? 0.5 + 0.5 * Px.Sin((t - end) * 9) : 0;
                bool white = t >= FLARE;
                foreach (Pt q in pts)
                {
                    if (q.t > t) break;
                    double age = t - q.t;
                    p.Px(q.x, q.y, white ? WHITE : age < 0.05 ? WHITE : age < 0.15 ? C[7] : age < 0.35 ? C[6] : hold > 0.75 ? C[6] : C[5]);
                }
                if (white)
                    foreach (Pt q in pts)
                        if (ToInt32(q.x * 2) % 3 == 0) p.Light(q.x, q.y, C[6], 0.5);
                // glyphs: each pops in white and settles
                foreach (Glyph g in G)
                {
                    if (t < g.t) continue;
                    double age = t - g.t;
                    uint col = white || age < 0.06 ? WHITE : age < 0.2 ? C[7] : hold > 0.75 ? C[7] : C[6];
                    foreach (int[][] s in g.segs) p.Line(g.x - 2 + s[0][0] * 2, g.y - 2 + s[0][1] * 2, g.x - 2 + s[1][0] * 2, g.y - 2 + s[1][1] * 2, col);
                    if (age < 0.18) RingFast(p, g.x, g.y, 2 + 4 * (age / 0.18), 1, age < 0.09 ? C[7] : C[5]);
                    p.Glow(g.x, g.y, 6, C[5], 0.25);
                }
                // the pen: a white-hot point with a little cross of light
                if (t > 0.12 && t < end + 0.05)
                {
                    Pt q = Pen();
                    Sim.Sparkle(p, q.x, q.y, 3 + Px.Sin(t * 60), C[7], C[6], 0.6);
                    p.Glow(q.x, q.y, 16, C[6], 0.55);
                }
            }
            // the flare: a burst ring as it goes white
            double fu = (t - FLARE) / 0.35, bu = (t - FLARE) / 0.22;
            if (bu > 0 && bu < 1) Sim.Sparkle(p, 0, CY, 18 * (1 - bu), C[7], C[5], 0.9 * (1 - bu));
            if (fu > 0 && fu < 1) RingFast(p, 0, CY, RO + 3 + 12 * Ease.OutCubic(fu), Math.Max(1, 3 * (1 - fu)), fu < 0.4 ? WHITE : fu < 0.7 ? C[6] : C[4]);
            bits.Draw(p);
            puffs.Draw(p, PK.Flame);
            ps.Draw(p);
            embers.Draw(p);
            foreach (Lamp l in lights)
            {
                double q = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.6 + 0.4 * q), l.k > 1 ? C[5] : C[6], l.k * q * q);
            }
        }
    }
}
