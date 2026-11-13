// The hold-then-break (it closes round the character, holds with glints, cracks, then bursts into tumbling
// pieces, mist and light) is adapted from Sprite Fusion's open-source ice freeze (https://www.spritefusion.com/pixel-art-vfx/ice),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class RootSnare : Effect
    {
        const double DURATION = 2.7;

        static readonly Pose GRAB = new Pose { ["turn"] = 0.9, ["crouch"] = 3, ["fx"] = -12, ["fy"] = -34, ["bx"] = 12, ["by"] = -28, ["ffx"] = -2, ["fbx"] = 3 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(-0.24, new Pose(Poses.READY) { ["ffx"] = -2, ["fbx"] = 3 }),
                new PoseKey(0.3, new Pose { ["turn"] = 0.9, ["lean"] = -1, ["crouch"] = 2, ["fx"] = -10, ["fy"] = -24, ["bx"] = 11, ["by"] = -24, ["ffx"] = -2, ["fbx"] = 3 }),
                new PoseKey(0.62, GRAB, "snap"),
                new PoseKey(0.86, new Pose(GRAB) { ["lean"] = -3, ["fy"] = -37, ["by"] = -27 }),
                new PoseKey(1.1, new Pose(GRAB) { ["lean"] = 3, ["fy"] = -27, ["by"] = -37 }),
                new PoseKey(1.34, new Pose(GRAB) { ["lean"] = -3, ["fy"] = -37, ["by"] = -27 }),
                new PoseKey(1.58, new Pose(GRAB) { ["lean"] = 3, ["fy"] = -27, ["by"] = -37 }),
                new PoseKey(1.75, new Pose(GRAB) { ["crouch"] = 6 }),
                new PoseKey(1.8, new Pose { ["turn"] = 1.1, ["crouch"] = -1, ["y"] = -3, ["fx"] = -14, ["fy"] = -36, ["bx"] = 15, ["by"] = -36, ["ffx"] = -3, ["ffy"] = -6, ["fbx"] = 4, ["fby"] = -6 }, "snap"),
                new PoseKey(2.05, new Pose(Poses.READY) { ["y"] = 0, ["crouch"] = 5 }),
                new PoseKey(2.7, new Pose(Poses.READY) { ["ffx"] = -2, ["fbx"] = 3 }),
            },
            Hits = new[] { new HitCue { T = 0.62, Stop = 0.05, Shake = 0.1, Amp = 1 }, new HitCue { T = 1.75, Stop = 0.04, Shake = 0.14, Amp = 2 } },
        };

        public static readonly Module Module = new Module("root-snare", "pixelfx/fx/nature/root-snare", 128, 96, 64, 78, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#140c08", "#2a1a10", "#46301e", "#64462a", "#86623a", "#a8824e", "#ccaa70", "#ecd6a0" },
                ["glow"] = new[] { "#123818", "#2f7a2a", "#6cb83a", "#c8f070" },
                ["seed"] = 7,
            },
            (o, e) => new RootSnare(o, e), act: Act, actor: 2)
        {
            Title = "Root snare",
            Desc = "The floor cracks and roots burst up round the character, spiralling round its legs; they cinch tight with a snap, hold, then crack and crumble into bark and dust.",
            Tags = new[] { "nature", "roots", "plant", "snare", "root", "status", "actor", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.34, CINCH = 0.62, BREAK = 1.75;
        const int NP = 48;

        sealed class Root
        {
            public double th0, turns, r0, leg, h, at;
            public int dir;
            public double[] buds;
            public bool burst;
        }

        struct Pt
        {
            public double x, y, z, s;
        }

        sealed class Chip
        {
            public double x, y, vx, vy, a, va, s, life;
            public double[][] pts;
        }

        // one root: from the floor outside the feet it spirals in round the legs and climbs, thinning to its tip
        static Pt[] PathOf(Root r, double cinch)
        {
            var pts = new Pt[NP + 1];
            for (int i = 0; i <= NP; i++)
            {
                double s = (double)i / NP, th = r.th0 + r.dir * TAU * r.turns * s, rad = Lerp(r.r0, r.leg - cinch * 1.6, Ease.OutCubic(Math.Min(1, s * 3.5)));
                pts[i] = new Pt { x = Px.Cos(th) * rad, y = -r.h * Px.Pow(s, 1.05) + Px.Sin(th) * rad * SY, z = Px.Sin(th), s = s };
            }
            return pts;
        }

        // a stroke of bark along the path: lit on its upper side, dark beneath, a ridge of grain, rootlet hairs
        static void DrawRoot(Painter p, Pt[] pts, double g, uint[] C, bool back, double a)
        {
            double n = Math.Floor(NP * g);
            for (int i = 0; i < n; i++)
            {
                Pt A = pts[i], Bp = pts[i + 1];
                bool front = (A.z + Bp.z) / 2 >= 0;
                if (front == back) continue;
                double dx = Bp.x - A.x, dy = Bp.y - A.y, L = Hypot(dx, dy);
                if (L == 0 || double.IsNaN(L)) L = 1;
                double nx = -dy / L, ny = dx / L;
                int up = nx * -0.6 + ny * -0.8 > 0 ? 1 : -1;
                double w = (1.4 + 3.6 * Px.Pow(1 - A.s, 0.8)) / 2 + 0.35;
                int dim = back ? 1 : 0;
                for (double y = Math.Floor(Math.Min(A.y, Bp.y) - w - 1); y <= Math.Ceiling(Math.Max(A.y, Bp.y) + w + 1); y++)
                for (double x = Math.Floor(Math.Min(A.x, Bp.x) - w - 1); x <= Math.Ceiling(Math.Max(A.x, Bp.x) + w + 1); x++)
                {
                    if (y > 0) continue;
                    double px = x + 0.5 - A.x, py = y + 0.5 - A.y, u = (px * dx + py * dy) / (L * L);
                    if (u < -0.2 || u > 1.2) continue;
                    double d = px * nx + py * ny;
                    if (Math.Abs(d) > w) continue;
                    double side = Sign(d) * up, q = Math.Abs(d) / w;
                    int i2 = q > 0.72 ? (side > 0 ? 7 : 1) : side > 0 ? (q > 0.3 ? 6 : 5) : q > 0.35 ? 2 : 3;
                    if (q < 0.25 && Rand(Round(A.s * 40), 3) < 0.4) i2 = 4;
                    p.Dot(x, y, C[Math.Max(1, i2 - dim)], a);
                }
                // rootlet hairs off its underside now and then
                if (i % 7 == 3 && w > 1.2) p.Dot(A.x - nx * up * (w + 1), A.y - ny * up * (w + 1), C[2 - dim], a);
            }
        }

        static readonly double[][] SNAPS = { new[] { -8.0, -6 }, new[] { 8.0, -10 }, new[] { -7.0, -15 } };

        readonly uint[] C, G;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Chip> chips = new List<Chip>();
        readonly Root[] roots;
        readonly List<double[]>[] cracks;
        double t;
        bool cinched, broken;

        RootSnare(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            G = Fit(o.Ramp("glow"), 4);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { G[3], G[2], G[1], G[0] }, emitter: e);
            lights = new Lights(e);
            roots = new Root[5];
            for (int i = 0; i < 5; i++)
            {
                var r = new Root();
                r.th0 = (i / 5.0) * TAU + R.Range(-0.3, 0.3);
                r.dir = i % 2 != 0 ? 1 : -1;
                r.turns = R.Range(1.1, 1.6);
                r.r0 = R.Range(14, 19);
                r.leg = R.Range(8, 10);
                r.h = R.Range(13, 21);
                r.at = 0.16 + i * 0.07 + R.Range(0, 0.04);
                double b0 = R.Range(0.35, 0.55), b1 = R.Range(0.6, 0.85);
                r.buds = new[] { b0, b1 };
                roots[i] = r;
            }
            cracks = new[] { NatureLib.FloorCrack(R, -1, R.Range(18, 26)), NatureLib.FloorCrack(R, 1, R.Range(18, 26)) };
        }

        double Grow(Root r) => Ease.OutCubic(Clamp((t - r.at) / 0.32));
        double Squeeze() => Smooth(CINCH - 0.03, CINCH + 0.05, t) * (1 - 0.4 * Smooth(CINCH + 0.05, CINCH + 0.25, t));

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            if (t < 0.4 && R.Chance(0.3 * f))
            {
                int sd = R.Sign();
                ps.Spawn(P.Grit, sd * R.Range(4, 22), -1, R.Range(-20, 20), -R.Range(30, 90), R.Range(0.5, 0.9), R.Pick(new[] { C[5], C[4] }), 1);
            }
            foreach (Root r in roots)
                if (!r.burst && t >= r.at)
                {
                    r.burst = true;
                    double x = Px.Cos(r.th0) * r.r0;
                    for (int i = 0; i < 4; i++)
                    {
                        var c = new Chip { x = x, y = -1 };
                        c.vx = R.Range(-60, 60);
                        c.vy = -R.Range(60, 160);
                        c.a = R.Range(0, TAU);
                        c.va = R.Range(-15, 15);
                        c.s = R.Range(1.2, 2);
                        c.pts = NatureLib.StoneShape(R);
                        c.life = R.Range(0.6, 0.9);
                        chips.Add(c);
                    }
                    for (int i = 0; i < 3; i++) ps.Spawn(P.Dust, x, -1, R.Range(-25, 25), -R.Range(4, 18), R.Range(0.4, 0.8), C[5], R.Range(2, 3.5));
                    lights.Add(x, -4, 20, G[2], 0.35, 0.3);
                }
            if (!cinched && t >= CINCH)
            {
                cinched = true;
                lights.Add(0, -10, 40, G[3], 0.5, 0.3);
                lights.Add(0, -10, 16, WHITE, 0.4, 0.06);
                for (int i = 0; i < 8; i++) ps.Spawn(P.Dust, R.Range(-12, 12), -R.Range(0, 4), R.Sign() * R.Range(20, 60), -R.Range(4, 14), R.Range(0.5, 0.9), C[5], R.Range(2, 3.5));
                for (int i = 0; i < 6; i++) ps.Spawn(P.Star, R.Range(-9, 9), -R.Range(4, 20), 0, -4, R.Range(0.25, 0.5), G[3], R.Range(0.8, 1.8), R.Range(0, 0.1));
            }
            // while it holds, motes of green rise off the roots
            if (t > CINCH && t < BREAK && R.Chance(0.06 * f)) ps.Spawn(P.Mote, R.Range(-9, 9), -R.Range(3, 18), R.Range(-6, 6), -R.Range(14, 30), R.Range(0.5, 0.9), G[3], 0.6);
            if (!broken && t >= BREAK)
            {
                broken = true;
                lights.Add(0, -10, 36, G[2], 0.4, 0.25);
                // every root crumbles into bark chunks and splinters along its length
                foreach (Root r in roots)
                {
                    Pt[] pts = PathOf(r, 1);
                    for (int i = 2; i < NP; i += 4)
                    {
                        Pt q = pts[i];
                        double w = 1.4 + 3.6 * Px.Pow(1 - q.s, 0.8);
                        int sg = Sign(q.x);
                        var c = new Chip { x = q.x, y = Math.Min(q.y, -1) };
                        c.vx = (sg != 0 ? sg : 1) * R.Range(25, 85) + q.x * 3;
                        c.vy = -R.Range(50, 150);
                        c.a = R.Range(0, TAU);
                        c.va = R.Range(-10, 10);
                        c.s = Clamp(w * 0.75, 1.5, 3);
                        c.pts = NatureLib.StoneShape(R);
                        c.life = Math.Min(R.Range(0.7, 0.95), DURATION - 0.05 - t);
                        chips.Add(c);
                        if (i % 10 == 2) ps.Spawn(P.Grit, q.x, Math.Min(q.y, -1), R.Range(-40, 40), -R.Range(20, 80), Math.Min(R.Range(0.6, 0.9), DURATION - 0.05 - t), R.Pick(new[] { C[4], C[3], C[2] }), R.Chance(0.3) ? 2 : 1);
                    }
                }
                for (int i = 0; i < 10; i++) ps.Spawn(P.Smoke, R.Range(-12, 12), -R.Range(2, 16), R.Range(-25, 25), -R.Range(5, 20), Math.Min(R.Range(0.6, 0.9), DURATION - 0.05 - t), C[5], R.Range(3, 5), 0, PF.None);
            }
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                Chip c = chips[i];
                if ((c.life -= dt) <= 0) chips.RemoveAt(i);
                c.vy += 950 * dt;
                c.x += c.vx * dt;
                c.y += c.vy * dt;
                c.a += c.va * dt;
                if (c.y > 0 && c.vy > 0)
                {
                    if (c.vy > 110)
                    {
                        c.y = 0;
                        c.vy *= -0.3;
                        c.vx *= 0.5;
                    }
                    else
                    {
                        c.y = 0;
                        c.vy = c.vx = c.va = 0;
                    }
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // cracks run out from the feet, a mossy light seeping up from them
            double open = Ease.OutCubic(Clamp(t / 0.25)), hot = Smooth(0, 0.3, t) * (1 - Smooth(0.5, 1.4, t)), fade = 1 - Smooth(DURATION - 0.6, DURATION - 0.15, t);
            foreach (List<double[]> c in cracks)
            {
                double m = Math.Max(2, Math.Ceiling(c.Count * open));
                for (int k = 1; k < m; k++)
                {
                    double x0 = c[k - 1][0], y0 = c[k - 1][1], x1 = c[k][0], y1 = c[k][1];
                    p.Line(x0, y0 + 1, x1, y1 + 1, C[0], fade);
                    p.Line(x0, y0, x1, y1, hot > 0.5 ? G[3] : hot > 0.2 ? G[2] : C[1], fade);
                    if (k % 2 == 0) p.Glow(x1, y1, 6, G[2], 0.3 * hot);
                }
            }
            double hold = Smooth(0.3, CINCH, t) * (1 - Smooth(BREAK, BREAK + 0.3, t)) * (0.8 + 0.2 * Px.Sin(t * 8));
            if (hold > 0)
            {
                p.Glow(0, -12, 30, G[2], 0.3 * hold);
                p.Glow(0, -1, 22, G[3], 0.16 * hold);
            }
            if (t < BREAK)
                foreach (Root r in roots)
                    if (t > r.at)
                        DrawRoot(p, PathOf(r, Squeeze()), Grow(r), C, true, 1);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            if (t < BREAK)
            {
                foreach (Root r in roots)
                {
                    if (t <= r.at) continue;
                    Pt[] pts = PathOf(r, Squeeze());
                    double g = Grow(r);
                    DrawRoot(p, pts, g, C, false, 1);
                    // buds open into small leaves once it has hold of them
                    foreach (double b in r.buds)
                    {
                        Pt q = pts[(int)Math.Floor(b * NP)];
                        double lk = Clamp((t - CINCH - 0.1 - b * 0.2) / 0.25);
                        if (b > g || lk <= 0 || q.z < 0) continue;
                        NatureLib.DrawLeaf(p, q.x + 2, q.y - 2, 5 * lk, 1.6, -0.6 + Px.Sin(t * 5 + b * 9) * 0.2, 0.3, new[] { G[0], G[1], G[2], G[3] });
                    }
                }
                // the snap as they cinch
                double e = t - CINCH;
                if (e > 0 && e < 0.12)
                    foreach (double[] xy in SNAPS)
                        Sim.Sparkle(p, xy[0], xy[1], 5 * (1 - e / 0.12), C[7], G[3], 0.4);
                // the white flash just before they go
                double w = BREAK - t;
                if (w > 0 && w < 0.05)
                    foreach (Root r in roots)
                        foreach (Pt q in PathOf(r, 1))
                            if (q.z >= 0)
                                p.Px(q.x, q.y, WHITE);
            }
            uint[] BARK = { C[0], C[1], C[2], C[2], C[3], C[4], C[5] };
            foreach (Chip c in chips) NatureLib.DrawRock(p, c.x, c.y, c.s, c.a, c.pts, BARK, Math.Min(1, c.life / 0.25));
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
