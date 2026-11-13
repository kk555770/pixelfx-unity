// The pop's star and slivers follow Sprite Fusion's open-source impact, and the sparks his slash
// (https://www.spritefusion.com/pixel-art-vfx), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class WhipCrack : Effect
    {
        static readonly Pose STANCE = new Pose(Poses.READY) { ["weapon"] = "handle", ["x"] = -7, ["sa"] = -0.3, ["fx"] = 13, ["fy"] = -21, ["bx"] = 7, ["by"] = -18 };
        static readonly Pose COCK = new Pose(STANCE) { ["crouch"] = 4, ["lean"] = -3, ["fx"] = -6, ["fy"] = -38, ["sa"] = -2.2, ["bx"] = 9, ["by"] = -23, ["armsFront"] = true, ["ffx"] = 9, ["fbx"] = -7 };
        static readonly Pose CRACK_ = new Pose(STANCE) { ["crouch"] = 4, ["lean"] = 5, ["fx"] = 18, ["fy"] = -21, ["sa"] = 0, ["bx"] = -8, ["by"] = -23, ["ffx"] = 12, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 94 },
            Poses = new[]
            {
                new PoseKey(-0.46, STANCE),
                new PoseKey(-0.22, COCK),
                new PoseKey(0.02, new Pose(COCK) { ["lean"] = -4 }),
                new PoseKey(0.06, new Pose(CRACK_) { ["fx"] = 15, ["fy"] = -34, ["sa"] = -0.7, ["lean"] = 2, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.12, CRACK_, "snap"),
                new PoseKey(0.21, CRACK_),
                new PoseKey(0.26, new Pose(CRACK_) { ["fx"] = 15, ["fy"] = -27, ["sa"] = -0.3, ["lean"] = 2, ["crouch"] = 3 }, "snap"),
                new PoseKey(0.44, new Pose(CRACK_) { ["fx"] = 17, ["fy"] = -22, ["sa"] = 0.1, ["lean"] = 3, ["crouch"] = 3 }),
                new PoseKey(0.92, STANCE),
            },
            Hits = new[] { new HitCue { T = 0.21, Stop = 0.1, Shake = 0.14, Amp = 2, Push = 2.4, Straw = 14, Y = -28 } },
            Dust = new[] { new DustCue { T = 0.06, X = 2, N = 4, Dir = 1 } },
        };

        public static readonly Module Module = new Module("whip-crack", "pixelfx/fx/melee/whip-crack", 192, 80, 30, 68, 0.85, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a1a10", "#5c3a1e", "#9c6a34", "#d8a45a", "#ffd890", "#fff4d6", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["length"] = 76,
                ["seed"] = 3,
            },
            (o, e) => new WhipCrack(o, e), act: Act, actor: 2)
        {
            Title = "Whip crack",
            Desc = "A whip rolls out to the right as a loop running down its length, faster and faster, until the tip flips over and cracks: a white pop, a ring of air, sparks and a puff of smoke.",
            Tags = new[] { "melee", "whip", "crack", "lash", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        const double ROLL0 = 0.02, ROLL = 0.19, CRACK = ROLL0 + ROLL; // the loop runs out; the tip cracks when it gets there
        const double RECOIL = 0.16;
        const double HX = 11, HY = -21;

        sealed class SliverSpec
        {
            public double a, r, v, arc;
        }

        sealed class TipPoint
        {
            public double[] p;
            public double t;
        }

        readonly uint[] C;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Lights lights;
        readonly double L;
        readonly double[] TIP;
        readonly Spike[] spikes;
        readonly SliverSpec[] slivers;
        readonly List<TipPoint> tipTrail = new List<TipPoint>();
        double t;
        bool cracked;

        WhipCrack(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            Rg = new Rng(o.Seed);
            ps = new Particles(Rg, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            L = o.Num("length");
            TIP = new[] { HX + L, HY - 1 };
            double[] sa = { 0, 0.8, 1.6, 2.4, 3.14, 3.9, 4.7, 5.5 };
            spikes = new Spike[sa.Length];
            for (int i = 0; i < sa.Length; i++)
            {
                double a = sa[i] + Rg.Range(-0.15, 0.15), len = (i == 0 ? 13 : i % 2 != 0 ? 6 : 9) + Rg.Range(0, 3), w = 1.4 + Rg.Range(0, 0.5);
                spikes[i] = new Spike(a, len, w);
            }
            double[] la = { -1.2, -0.3, 0.5, 1.3 };
            slivers = new SliverSpec[la.Length];
            for (int i = 0; i < la.Length; i++) slivers[i] = new SliverSpec { a = la[i] + Rg.Range(-0.2, 0.2), r = Rg.Range(4, 6), v = Rg.Range(80, 120), arc = Rg.Range(0.4, 0.7) };
        }

        // how far down the whip the loop has run: it starts a third of the way out, cocked, and accelerates
        double Lam(double t) => L * (0.34 + 0.66 * Ease.InQuad(Clamp((t - ROLL0) / ROLL)));

        // the whip as points, handle to tip: straight out to the loop, round the loop, folded back under it
        List<double[]> Shape(double t)
        {
            double l = Lam(t), r = 6.5 - 3.5 * (l / L);
            var pts = new List<double[]>();
            double recoil = Smooth(CRACK + 0.04, CRACK + 0.04 + RECOIL, t), len = L * (1 - recoil * 0.85);
            for (double s = 0; s <= len; s += 1)
            {
                double x, y;
                if (s <= l)
                {
                    x = HX + s;
                    y = HY - Px.Sin((Math.PI * s) / Math.Max(l, 1)) * 2.5 * (1 - l / L);
                }
                else if (s <= l + Math.PI * r)
                {
                    double th = (s - l) / r;
                    x = HX + l + Px.Sin(th) * r;
                    y = HY + r - Px.Cos(th) * r;
                }
                else
                {
                    x = HX + l - (s - l - Math.PI * r);
                    y = HY + 2 * r;
                }
                // after the crack it sags as it's pulled back in
                if (recoil > 0) y += Px.Sin((Math.PI * s) / L) * 6 * recoil;
                pts.Add(new[] { x, y });
            }
            return pts;
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t > CRACK - 0.05 && t < CRACK + 0.01)
            {
                List<double[]> pts = Shape(t);
                tipTrail.Add(new TipPoint { p = pts[pts.Count - 1], t = t });
            }
            for (int i = tipTrail.Count - 1; i >= 0; i--)
                if (t - tipTrail[i].t > 0.06) tipTrail.RemoveAt(i);
            if (!cracked && t >= CRACK)
            {
                cracked = true;
                lights.Add(TIP[0], TIP[1], 46, C[4], 0.6, 0.3);
                lights.Add(TIP[0], TIP[1], 14, WHITE, 0.8, 0.07);
                for (int i = 0; i < 16; i++)
                {
                    double a = Rg.Range(-1.3, 1.2), sp = Rg.Range(130, 300);
                    ps.Spawn(P.Spark, TIP[0], TIP[1], Px.Cos(a) * sp, Px.Sin(a) * sp - 40, Rg.Range(0.12, 0.36));
                }
                for (int i = 0; i < 3; i++) ps.Spawn(P.Smoke, TIP[0] + Rg.Range(-2, 3), TIP[1] + Rg.Range(-2, 2), Rg.Range(10, 40), -Rg.Range(6, 18), Rg.Range(0.5, 0.8), C[2], Rg.Range(2.5, 4), Rg.Range(0.02, 0.06));
                for (int i = 0; i < 4; i++) ps.Spawn(P.Star, TIP[0] + Rg.Range(-8, 10), TIP[1] + Rg.Range(-8, 8), 0, -3, Rg.Range(0.2, 0.4), Rg.Chance(0.5) ? C[5] : WHITE, Rg.Range(0.8, 1.8), Rg.Range(0.05, 0.2));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            double end = CRACK + 0.04 + RECOIL;
            if (t < end)
            {
                double fade = 1 - Smooth(end - 0.06, end, t);
                // afterimages of the whip a beat and two beats back: the loop's motion
                if (t < CRACK)
                {
                    double[] lags = { 0.03, 0.015 }, alphas = { 0.5, 0.7 };
                    uint[] cols = { C[1], C[2] };
                    for (int j = 0; j < 2; j++)
                    {
                        List<double[]> old = Shape(Math.Max(0, t - lags[j]));
                        for (int i = 1; i < old.Count; i += 2) p.Dot(old[i][0], old[i][1], cols[j], alphas[j]);
                    }
                }
                // the whip: thicker and lit near the handle, a single line to the tip
                List<double[]> pts = Shape(t);
                int n = pts.Count;
                double l = Lam(t), r = 6.5 - 3.5 * (l / L);
                for (int i = 1; i < n; i++)
                {
                    double x0 = pts[i - 1][0], y0 = pts[i - 1][1], x1 = pts[i][0], y1 = pts[i][1], u = (double)i / n, s = i;
                    // the loop's leading half is moving fastest: it runs hot
                    bool inLoop = t < CRACK && s > l && s < l + Math.PI * r, lead = inLoop && Math.Abs(s - l - (Math.PI * r) / 2) < r * 0.9;
                    p.Line(x0, y0, x1, y1, lead ? WHITE : inLoop ? C[5] : u < 0.5 ? C[4] : C[3], fade);
                    if (u < 0.4) p.Line(x0, y0 + 1, x1, y1 + 1, C[2], fade);
                }
                // the running loop is the fastest part: it shines
                if (t < CRACK)
                {
                    double l2 = Lam(t), r2 = 6.5 - 3.5 * (l2 / L);
                    p.Glow(HX + l2 + r2, HY + r2, 12, C[4], 0.4);
                }
            }
            // the tip's path just before the crack: a white-hot streak
            for (int i = 1; i < tipTrail.Count; i++)
            {
                double k = 1 - (t - tipTrail[i].t) / 0.06;
                p.Line(tipTrail[i - 1].p[0], tipTrail[i - 1].p[1], tipTrail[i].p[0], tipTrail[i].p[1], k > 0.6 ? WHITE : C[5], k);
            }
            // the crack: a white pop, a ring of air, slivers
            double q = t - CRACK;
            if (q >= 0 && q < 0.035) Star(p, TIP[0], TIP[1], spikes, 1, 1, WHITE, C[5], C[6], C[4]);
            else if (q >= 0.035 && q < 0.07) Star(p, TIP[0], TIP[1], spikes, 1.3, 0.5, C[6], C[5], C[5], C[3]);
            if (q >= 0.02 && q < 0.2)
            {
                double k = (q - 0.02) / 0.18, r = 4 + 16 * Ease.OutCubic(k), th = Math.Max(1, 2.2 * (1 - k));
                uint col = k < 0.25 ? WHITE : k < 0.5 ? C[5] : k < 0.75 ? C[4] : C[3];
                for (double a = 0; a < TAU; a += 0.6 / r)
                    for (double o = 0; o < th; o += 0.5) p.Dot(TIP[0] + Px.Cos(a) * (r - o), TIP[1] + Px.Sin(a) * (r - o) * 0.8, col, 1.15 - k);
                p.Glow(TIP[0], TIP[1], r + 6, C[4], 0.25 * (1 - k));
            }
            if (q >= 0.04 && q < 0.16)
            {
                double k = (q - 0.04) / 0.12;
                foreach (SliverSpec s in slivers) Sliver(p, TIP[0], TIP[1], s.a, s.r + s.v * (q - 0.04), s.arc * (1 - 0.4 * k), k < 0.4 ? C[6] : C[5], 1 - k);
            }
            if (q >= 0 && q < 0.15) p.Glow(TIP[0], TIP[1], 26, C[5], 0.7 * Exp(-q / 0.04));
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
