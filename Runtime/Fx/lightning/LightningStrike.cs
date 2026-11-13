// The stepped leader, the stroke flicker, the cooling channel and the scorch follow Sprite Fusion's open-source
// lightning (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class LightningStrike : Effect
    {
        public static readonly Module Module = new Module("lightning-strike", "pixelfx/fx/lightning/lightning-strike", 240, 264, 120, 228, 3, false, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["seed"] = 5 },
            (o, e) => new LightningStrike(o, e))
        {
            Title = "Lightning strike",
            Desc = "A targeted strike: a sigil charges on the floor, the scene darkens, a thick bolt slams into it, arcs race out along the cracks it splits, the channel crumbles to a scorch.",
            Tags = new[] { "lightning", "electric", "strike", "spell", "target", "impact", "ground", "one-shot" },
            Bg = "ground",
        };

        const double W = 120, TOP = -224, RX = 28, SY = 0.22, REACH = 52, LEAD0 = 0.48, MEET = -30;
        const int STEPS = 9;
        static readonly double[] ST_T = { 0.76, 1.04, 1.26 }, ST_K = { 1, 0.8, 0.55 };
        static readonly double[] FLICK = { 1, 0.72, 0.3, 0.95, 0.5, 0.8, 0.22 };
        static readonly uint NIGHT = Hex("#04030a"), DUST = Hex("#6e6382"), ASH = Hex("#5b5566");
        static readonly uint[] STONE = Ramp("#655b7a", "#4a4263", "#342d45");
        const double S1 = 0.76, LAST_T = 1.26;
        const int NS = 3;

        static double Level(double a)
        {
            if (a < 0) return 0;
            double i = Math.Floor(a * 30 + 1e-4);
            return i >= 0 && i < FLICK.Length ? FLICK[(int)i] : 0;
        }

        sealed class Crack
        {
            public double[][] pts; // [x, y, d]
            public double w;
        }

        // the cracks the strike splits in the floor: Lichtenberg branches lying on the floor's face
        static List<Crack> Cracks(Rng R)
        {
            var o = new List<Crack>();
            void Grow(double x, double y, double a, double len, double w, double d, int depth)
            {
                var pts = new List<double[]> { new[] { x, y, d } };
                for (double s = 0; s < len; s += 1.5)
                {
                    a += R.Range(-0.32, 0.32);
                    x += Px.Cos(a) * 1.5;
                    y = Clamp(y + Px.Sin(a) * 0.45, 0, 5);
                    d += 1.5;
                    pts.Add(new[] { x, y, d });
                    if (depth < 2 && s > 3 && R.Chance(0.09)) Grow(x, y, a + R.Sign() * R.Range(0.5, 1.2), (len - s) * R.Range(0.3, 0.6), w * 0.6, d, depth + 1);
                }
                o.Add(new Crack { pts = pts.ToArray(), w = w });
            }
            for (int i = 0; i < 6; i++) Grow(R.Range(-1, 1), R.Range(0, 1), i % 2 != 0 ? R.Range(-0.5, 0.7) : Math.PI + R.Range(-0.7, 0.5), REACH * R.Range(0.55, 1), 1, 0, 0);
            return o;
        }

        static bool OnSide(double a, bool near) => (Px.Sin(a) >= 0) == near;
        static double From(double a) => Math.Abs((((a - Math.PI / 2) % TAU) + TAU + Math.PI) % TAU - Math.PI) / Math.PI;
        static readonly double[] DAS = { 0, 0.06, 0.12 };

        // the sigil on the floor: a ring of dashes turning slowly, crosshair ticks, an inner ring and a heart; drawn in
        // from the front, charging brighter; half picks the far (behind) or near part
        static void Sigil(Painter p, double t, uint[] C, double k, bool near)
        {
            double reveal = Clamp(t / 0.22), spin = t * 0.8, burnt = t > S1 ? t - S1 : -1;
            if (burnt > 0.45) return;
            double a0 = burnt < 0 ? 1 : burnt < 0.07 ? 1 : 1 - (burnt - 0.07) / 0.38;
            bool hot = burnt >= 0 && burnt < 0.07;
            for (int i = 0; i < 22; i++)
            {
                double a = (i / 22.0) * TAU + spin;
                if (!OnSide(a, near) || From(a) > reveal) continue;
                foreach (double da in DAS)
                {
                    double x = Px.Cos(a + da) * RX, y = Px.Sin(a + da) * RX * SY;
                    p.Dot(x, y, hot ? WHITE : near ? (k > 0.7 ? C[6] : C[5]) : k > 0.7 ? C[4] : C[3], a0);
                    if (near) p.Dot(x, y + 1, hot ? C[6] : C[2], a0);
                }
                if (i % 3 == 0) p.Glow(Px.Cos(a) * RX, Px.Sin(a) * RX * SY, 5, C[4], 0.3 * k * a0);
            }
            for (int j = 0; j < 4; j++)
            {
                double a = spin * -0.5 + (j * TAU) / 4 + Math.PI / 4;
                if (!OnSide(a, near) || reveal < 1) continue;
                p.Line(Px.Cos(a) * (RX - 5), Px.Sin(a) * (RX - 5) * SY, Px.Cos(a) * (RX + 5), Px.Sin(a) * (RX + 5) * SY, hot ? WHITE : near ? C[6] : C[4], a0);
            }
            if (t > 0.3)
                for (double a = 0; a < TAU; a += 0.09)
                {
                    if (!OnSide(a, near)) continue;
                    p.Dot(Px.Cos(a) * 14, Px.Sin(a) * 14 * SY, hot ? WHITE : near ? C[4] : C[2], a0 * Clamp((t - 0.3) / 0.12));
                }
            if (near && burnt < 0)
            {
                double pulse = 0.6 + 0.4 * Px.Sin(t * (10 + 30 * k));
                p.Px(0, 0, k > 0.5 ? WHITE : C[6]);
                p.Px(1, 0, C[5]);
                p.Px(-1, 0, C[5]);
                p.Px(0, -1, C[5]);
                p.Glow(0, -1, 10 + 10 * k, C[5], 0.3 + 0.4 * k * pulse);
            }
        }

        // keep forks on the canvas and above the floor
        static Bolt Keep(Bolt b)
        {
            var o = new Bolt();
            foreach (BoltPath q in b.paths)
            {
                var pts = new List<double[]>();
                foreach (double[] pt in q.pts)
                {
                    if (Math.Abs(pt[0]) > W - 5 || pt[1] > -3 || pt[1] < TOP + 6) break;
                    pts.Add(pt);
                }
                if (q.w >= 1 || pts.Count > 3) o.paths.Add(new BoltPath(q.w >= 1 ? q.pts : pts.ToArray(), q.w));
            }
            return o;
        }

        sealed class Now
        {
            public int i;
            public double a, k;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps, hot;
        readonly Lights lights;
        readonly double X0;
        readonly Bolt main;
        readonly Bolt[] bolts;
        readonly double[][] end;
        readonly List<Crack> tree;
        readonly double[] gone;
        readonly Options o;
        double t;
        int struck = -1;

        LightningStrike(Options o, Emitter e)
        {
            this.o = o;
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            hot = new Particles(R, ground: x => 0, emitter: e);
            lights = new Lights(e);
            X0 = R.Range(8, 18) * (R.Chance(0.5) ? 1 : -1);
            main = Keep(Zigzag(R, X0, TOP, 0, -1, 0.17, 0.5));
            double[][] trunk = main.paths[0].pts;
            Func<double, double, double[]> Drift(double dx) => (x, y) => new[] { x + dx * Px.Sin((Math.PI * y) / TOP), y };
            bolts = new[] { main, Bend(new Bolt(new BoltPath(trunk, 1)), Drift(1.5)), Bend(new Bolt(new BoltPath(trunk, 1)), Drift(3)) };
            end = Chan(NS - 1);
            tree = Cracks(R);
            // the spent channel breaks up from the top down, each piece at its own moment
            gone = new double[end.Length];
            for (int i = 0; i < end.Length; i++) gone[i] = LAST_T + 0.25 + 0.75 * (0.55 * Rand(o.Seed, i, 5) + 0.45 * (1 - end[i][1] / TOP));
        }

        double[][] Chan(int i) => bolts[i].paths[0].pts;

        Now NowAt()
        {
            for (int i = NS - 1; i >= 0; i--)
                if (t >= ST_T[i]) return new Now { i = i, a = t - ST_T[i], k = Level(t - ST_T[i]) * ST_K[i] };
            return null;
        }

        double Charge() => Px.Pow(Clamp(t / S1), 1.5);

        double HeatAt()
        {
            if (struck < 0) return 0;
            double m = double.NegativeInfinity;
            for (int i = 0; i <= struck; i++) m = Math.Max(m, ST_K[i] * Px.Pow(Clamp(1 - (t - ST_T[i]) / 1.6), 1.4));
            return m;
        }

        public override void Update(double dt)
        {
            t += dt;
            for (int i = 0; i < NS; i++)
            {
                if (struck >= i || t < ST_T[i]) continue;
                struck = i;
                double k = ST_K[i];
                lights.Add(0, -50, 120, C[4], 0.55 * k, 0.4);
                lights.Add(0, -3, 24, WHITE, 0.5 * k, 0.1);
                for (int j = 0; j < Round(44 * k); j++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.3, 1.3), sp = R.Range(110, 320) * (0.65 + 0.35 * k);
                    ps.Spawn(P.Spark, R.Range(-1.5, 1.5), -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.55));
                }
                for (int j = 0; j < Round(12 * k); j++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(70, 220);
                    hot.Spawn(P.Spark, R.Range(-2, 2), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.5, 1));
                }
                for (int j = 0; j < Round(5 * k); j++) hot.Spawn(P.Ember, R.Range(-4, 4), -2, R.Range(-25, 25), -R.Range(30, 80), R.Range(0.8, 1.5));
                for (int j = 0; j < Round(18 * k); j++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(50, 190);
                    ps.Spawn(P.Grit, R.Range(-3, 3), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 1.5), R.Pick(STONE), R.Chance(0.35) ? 2 : 1);
                }
                for (int j = 0; j < 8; j++) ps.Spawn(P.Dust, R.Range(-3, 3), -1, R.Sign() * R.Range(30, 110), -R.Range(4, 24), R.Range(0.4, 0.8), DUST, R.Range(1.5, 3));
                for (int j = 0; j < Round(4 * k); j++) ps.Spawn(P.Smoke, R.Range(-8, 8), -4, R.Range(-10, 10), -R.Range(10, 25), R.Range(1.1, 1.6), Mix(ASH, C[2], 0.3), R.Range(3, 6), R.Range(0.05, 0.25));
            }
            if (struck >= 0 && t < 1.7 && R.Chance(6 * dt)) ps.Spawn(P.Smoke, R.Range(-8, 8), -2, R.Range(-4, 4), -R.Range(9, 17), R.Range(1, 1.3), 0, R.Range(2, 3.5));
            for (int i = 0; i < gone.Length - 1; i++)
                if (gone[i] <= t && gone[i] > t - dt && Rand(o.Seed, i, 6) < 0.5)
                {
                    double x = end[i][0], y = end[i][1];
                    ps.Spawn(P.Mote, x, y, R.Range(-8, 8), R.Range(6, 22), R.Range(0.4, 0.8), R.Chance(0.5) ? C[5] : C[4], R.Range(0.5, 0.9));
                }
            ps.Update(dt);
            hot.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // the scene darkens a beat before the strike: a soft veil, deepest round the bolt's path, gone at the flash
            double dark = 0.5 * Smooth(0.46, 0.66, t) * (1 - Smooth(S1 - 0.01, S1 + 0.02, t));
            if (dark > 0.01)
                for (int y = -228; y < 36; y++)
                    for (double x = -W; x < W; x++)
                    {
                        double d = Hypot(x / W, (y + 96) / 132.0), a = dark * (1 - Smooth(0.55, 1, d));
                        if (a > 0.01) p.Blend(x + 0.5, y + 0.5, NIGHT, a);
                    }
            Sigil(p, t, C, Charge(), false);
            // the cracks: arcs race out along them white, they glow with heat, cool to soot and fade
            if (struck >= 0)
            {
                Now s = NowAt();
                double front = REACH * Ease.OutCubic(Clamp((t - S1) / 0.35)), T0 = HeatAt(), fade = 1 - Smooth(2.3, 2.95, t);
                double elec = s.a < 0.14 ? ST_K[s.i] * (1 - s.a / 0.14) : 0;
                foreach (Crack br in tree)
                    foreach (double[] pt in br.pts)
                    {
                        double x = pt[0], y = pt[1], d = pt[2];
                        if (d > front) break;
                        double q = d / REACH, n = Rand(Round(x * 2), Round(y * 2), 9);
                        bool fresh = d > front - 6 && front < REACH;
                        double T = T0 * (1.15 - q) * (br.w < 1 ? 0.8 : 1) - n * 0.25;
                        uint c;
                        if (fresh || (elec > 0.3 && n < 0.85)) c = q < 0.5 ? WHITE : C[6];
                        else
                        {
                            c = Heat(T);
                            if (c == 0) c = SOOT2;
                        }
                        p.Dot(x, y + 1, SOOT, fade);
                        p.Dot(x, y, c, fade);
                        if (br.w >= 1 && d < 9) p.Dot(x, y + 2, SOOT, fade);
                        if ((fresh || elec > 0.3) && (ToInt32(Round(d)) & 3) == 0) p.Glow(x, y, 7, C[5], 0.4);
                        else if (T > 0.2 && (ToInt32(Round(d)) & 3) == 0) p.Glow(x, y, 5, c, 0.2 * T);
                    }
                Scorch(p, 0, 0, 12, T0, fade);
                if (T0 > 0) p.Glow(0, -2, 34, Heat(0.55), 0.3 * T0);
            }
            ps.Draw(p, Layer.Back);
            hot.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            Now s = NowAt();
            int f = (int)Math.Floor(t * 30 + 1e-4);
            double k = Charge();
            Sigil(p, t, C, k, true);
            // static rising off the charging sigil, more and more of it
            if (t > 0.15 && t < S1)
            {
                Rng r = NewRng(o.Seed, 61, f);
                for (int i = 0; i < Round(1 + 4 * k); i++)
                {
                    double a = r.Range(0, TAU), x = Px.Cos(a) * RX, y = Px.Sin(a) * RX * SY;
                    Crackle(p, Zigzag(r, x, y, x + r.Range(-2.5, 2.5), y - r.Range(4, 9) * (0.6 + k), 0.45, 0.3), 0.45 + 0.4 * k, C, 0.3);
                }
                p.Glow(0, -2, 36, C[4], 0.12 + 0.3 * k);
            }
            // the stepped leader feels down from above; a streamer climbs out of the sigil to meet it
            if (t >= LEAD0 && t < S1)
            {
                double u = Clamp((t - LEAD0) / (S1 - LEAD0)), st = Math.Min(STEPS, Math.Floor(u * STEPS) + 1), fresh = 1 - ((u * STEPS) % 1);
                double reach = TOP + (MEET - TOP) * Px.Pow(st / STEPS, 1.2), prev = TOP + (MEET - TOP) * Px.Pow((st - 1) / STEPS, 1.2);
                foreach (BoltPath q in main.paths)
                {
                    bool tk = q.w >= 1;
                    for (int i = 0; i < q.pts.Length - 1; i++)
                    {
                        double ax = q.pts[i][0], ay = q.pts[i][1], bx = q.pts[i + 1][0], by = q.pts[i + 1][1];
                        if (by > reach)
                        {
                            if (ay <= reach && i > 0)
                            {
                                p.Px(ax, ay, WHITE);
                                p.Glow(ax, ay, 7, C[5], 0.5 * fresh + 0.15);
                            }
                            break;
                        }
                        bool young = by > prev;
                        p.Line(ax, ay, bx, by, young ? (fresh > 0.4 ? (tk ? C[6] : C[5]) : C[4]) : tk ? C[3] : C[2]);
                        if (i % 4 == 0 && ay > TOP + 14) p.Glow(ax, ay, young ? 8 : 5, C[4], young ? 0.3 * fresh + 0.1 : 0.12);
                    }
                }
                if (t > 0.62)
                {
                    double h = -MEET * Ease.OutQuad((t - 0.62) / (S1 - 0.62));
                    Rng r = NewRng(o.Seed, 77, f);
                    Bolt b = Zigzag(r, 0, -1, r.Range(-2, 2), -Math.Max(3, h), 0.3, 0.3);
                    double[][] bp = b.paths[0].pts;
                    double[] tip = bp[bp.Length - 1];
                    Crackle(p, b, 0.85, C, 0.45);
                    p.Px(tip[0], tip[1], WHITE);
                    p.Glow(tip[0], tip[1], 9, C[5], 0.5);
                }
            }
            // the strokes, the dart that runs down the channel before each flicker, the channel cooling and crumbling
            int next = -1;
            for (int i = 0; i < NS; i++)
                if (ST_T[i] > t)
                {
                    next = i;
                    break;
                }
            if (next > 0 && t > ST_T[next] - 0.06)
            {
                double[][] tr = Chan(next);
                double u = (t - (ST_T[next] - 0.06)) / 0.06;
                int i1 = (int)Math.Floor(u * tr.Length);
                Trace(p, tr, C[3], 1);
                Trace(p, tr, C[6], 1, i1 - 18, i1);
                for (int i = Math.Max(0, i1 - 18); i < Math.Min(tr.Length, i1); i += 4)
                    if (tr[i][1] > TOP + 14) p.Glow(tr[i][0], tr[i][1], 9, C[4], 0.35);
            }
            else if (s != null && s.k > 0)
            {
                bool hero = s.i == 0 && s.a < 1.0 / 30;
                Stroke(p, bolts[s.i], s.k, C, s.i == 0 ? 1.5 : 1.2, new StrokeOpt { hero = hero, core = s.k > 0.6 ? (hero ? 1.2 : 0.8) : 0, minY = TOP + 14 });
                double r = 4.5 * s.k;
                p.Circle(0, -1, r + 1.5, C[2]);
                p.Circle(0, -1, r + 0.5, C[5]);
                p.Circle(0, -1, r * 0.65, WHITE);
                if (s.a < 0.07) Sim.Sparkle(p, 0, -2, Round(12 * s.k * (1 - s.a / 0.07)), C[6], C[4], 0.25);
                p.Glow(0, -16, 96, C[3], 0.42 * s.k);
                p.Glow(0, -3, 22, C[5], 0.3 * s.k);
                if (hero) p.Glow(X0 * 0.5, -110, 170, C[3], 0.3);
            }
            else if (s != null)
            {
                bool crumble = s.i == NS - 1;
                double cool = Clamp((s.a - 0.23) / 0.45);
                double[][] tr = Chan(s.i);
                for (int i = 0; i < tr.Length - 1; i++)
                {
                    if (crumble && t >= gone[i]) continue;
                    double h = crumble ? 1 - Clamp((t - LAST_T - 0.23) / (gone[i] - LAST_T - 0.23)) : 1 - cool;
                    p.Line(tr[i][0], tr[i][1], tr[i + 1][0], tr[i + 1][1], h > 0.7 ? C[5] : h > 0.4 ? C[4] : h > 0.15 ? C[3] : C[2]);
                    if (i % 6 == 0 && h > 0.3 && tr[i][1] > TOP + 14) p.Glow(tr[i][0], tr[i][1], 6, C[4], 0.15 * h);
                }
            }
            // arcs crawl out along the cracks as they split, and again with every flicker
            if (s != null && s.a < 0.4)
            {
                Rng r = NewRng(o.Seed, 91, f);
                double run = s.i == 0 ? REACH * Ease.OutCubic(Clamp(s.a / 0.35)) : REACH * Clamp(s.a / 0.22);
                foreach (Crack br in tree)
                {
                    if (br.w < 1 || r.Next() < 0.3) continue;
                    int i1 = Array.FindIndex(br.pts, pt => pt[2] > run), j1 = i1 < 0 ? br.pts.Length : i1, j0 = Math.Max(0, j1 - r.Int(7, 13));
                    var pts = new double[j1 - j0][];
                    for (int j = j0; j < j1; j++) pts[j - j0] = new[] { br.pts[j][0] + r.Range(-0.6, 0.6), br.pts[j][1] - 1.5 - r.Range(0, 2.5) };
                    if (pts.Length > 1) Crackle(p, new Bolt(new BoltPath(pts, 1)), 0.9 * (1 - s.a / 0.4) + 0.1, C, 0.35);
                }
            }
            ps.Draw(p, Layer.Front);
            hot.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
