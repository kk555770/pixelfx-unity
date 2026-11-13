// The smear that trails the lash (white at its leading edge, cooling down the ramp as it thins), the glint and
// crack burst at the tip, are adapted from Sprite Fusion's open-source slash (https://www.spritefusion.com/pixel-art-vfx/slash),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class VineWhip : Effect
    {
        const double DURATION = 1.4;

        public static readonly Module Module = new Module("vine-whip", "pixelfx/fx/nature/vine-whip", 216, 128, 60, 114, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0c1a10", "#173a1a", "#24582a", "#357a34", "#52a03e", "#86c84e", "#c4ec7a", "#f4ffd0" },
                ["thorn"] = new[] { "#2a1410", "#5e2a1a", "#a04c2e", "#e89a6a" },
                ["soil"] = new[] { "#120c0c", "#251917", "#3b2a24", "#553d31", "#715440", "#8f6e52", "#b8966e", "#e6d0a8" },
                ["seed"] = 3,
            },
            (o, e) => new VineWhip(o, e))
        {
            Title = "Vine whip",
            Desc = "A thorny vine bursts from the floor, rears back coiled, then lashes out right in a whip crack that sprays sap and tears off leaves, before sliding back into the ground.",
            Tags = new[] { "nature", "vine", "plant", "whip", "melee", "directional", "one-shot" },
            Bg = "ground",
        };

        const int N = 24;
        const double LS = 3.3, LASH = 0.36, LAG = 0.0052, TAU_S = 0.055, OMEGA = TAU / 0.21;
        static readonly double CRACK = LASH + LAG * N + (Math.PI - Atan(OMEGA * TAU_S)) / OMEGA;

        // a damped spring's step: 0 before u = 0, swings past 1, settles
        static double Spring(double u) => u <= 0 ? 0 : 1 - Exp(-u / TAU_S) * (Px.Cos(OMEGA * u) + Px.Sin(OMEGA * u) / (OMEGA * TAU_S));

        // the vine's points at time t: each segment's angle blends from standing, to reared back with the tip coiled
        // over, to lashed out right; the lash runs up the vine as a wave, so the tip whips through last
        static double[][] PoseAt(double t)
        {
            double grow = Ease.OutCubic(Clamp(t / 0.14)) * (1 - Ease.InCubic(Clamp((t - 1.0) / 0.3))), rear = Smooth(0.1, LASH, t), relax = Smooth(0.82, 1.0, t);
            var pts = new double[N + 1][];
            pts[0] = new[] { 0, (1 - grow) * (N * LS + 6) };
            for (int i = 0; i < N; i++)
            {
                double s = (double)i / N, stand = -Math.PI / 2 + 0.25 * Px.Sin(s * 3 + t * 7) * s;
                double back = -Math.PI / 2 - 0.3 - 1.65 * Px.Pow(s, 1.3), outA = -1.0 + 0.9 * Px.Pow(s, 0.6);
                double a = Lerp(stand, back, rear);
                a = Lerp(a, outA, Spring(t - LASH - LAG * i));
                a = Lerp(a, stand, relax);
                double x = pts[i][0], y = pts[i][1];
                pts[i + 1] = new[] { x + Px.Cos(a) * LS, y + Px.Sin(a) * LS };
            }
            return pts;
        }

        // a translucent polygon: the soft body of the smear under its streaks
        static void Haze(Painter p, double[][] pts, uint c, double a)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (double[] pt in pts)
            {
                x0 = Math.Min(x0, pt[0]);
                x1 = Math.Max(x1, pt[0]);
                y0 = Math.Min(y0, pt[1]);
                y1 = Math.Max(y1, pt[1]);
            }
            for (double y = Math.Floor(y0); y <= Math.Ceiling(y1); y++)
            for (double x = Math.Floor(x0); x <= Math.Ceiling(x1); x++)
            {
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                    if ((yi > y + 0.5) != (yj > y + 0.5) && x + 0.5 < ((xj - xi) * (y + 0.5 - yi)) / (yj - yi) + xi) inside = !inside;
                }
                if (inside) p.Blend(x, y, c, a);
            }
        }

        static double Width(double s) => 1.2 + 4.8 * Px.Pow(1 - s, 0.7);

        // the vine itself: a thick stroke lit on its upper side with a bright edge, dark beneath, bark notches
        static void Vine(Painter p, double[][] pts, uint[] C)
        {
            for (int i = 0; i < N; i++)
            {
                double ax = pts[i][0], ay = pts[i][1], bx = pts[i + 1][0], by = pts[i + 1][1], dx = bx - ax, dy = by - ay, L = Hypot(dx, dy);
                if (L == 0 || double.IsNaN(L)) L = 1;
                double nx = -dy / L, ny = dx / L;
                double w0 = Width((double)i / N) / 2, w1 = Width((i + 1.0) / N) / 2;
                int up = nx * -0.6 + ny * -0.8 > 0 ? 1 : -1;
                double X0 = Math.Floor(Math.Min(ax, bx) - 4), X1 = Math.Ceiling(Math.Max(ax, bx) + 4), Y0 = Math.Floor(Math.Min(ay, by) - 4), Y1 = Math.Ceiling(Math.Max(ay, by) + 4);
                for (double y = Y0; y <= Y1; y++)
                for (double x = X0; x <= X1; x++)
                {
                    if (y > 0) continue;
                    double px = x + 0.5 - ax, py = y + 0.5 - ay, u = (px * dx + py * dy) / (L * L);
                    if (u < -0.15 || u > 1.15) continue;
                    double d = px * nx + py * ny, w = Lerp(w0, w1, Clamp(u)) + 0.35;
                    if (Math.Abs(d) > w) continue;
                    double side = Sign(d) * up, q = Math.Abs(d) / w, arc = (i + u) * LS;
                    uint c = q > 0.8 ? (side > 0 ? C[5] : C[1]) : side > 0 ? (q > 0.35 ? C[4] : C[3]) : q > 0.4 ? C[2] : C[3];
                    if (w > 1.6 && Math.Abs(((arc + d * 0.8) % 5) - 2.5) < 0.45 && q < 0.8) c = C[2];
                    p.Px(x, y, c);
                }
            }
        }

        static readonly int[] LEAF_AT = { 7, 13, 18 };

        // thorns along it, alternating sides, hooked back toward the root; leaves on short stalks
        static void DrawThorns(Painter p, double[][] pts, uint[] T, uint[] C, double t)
        {
            for (int i = 2; i < N - 1; i += 2)
            {
                double ax = pts[i][0], ay = pts[i][1], bx = pts[i + 1][0], by = pts[i + 1][1], dx = bx - ax, dy = by - ay, L = Hypot(dx, dy);
                if (L == 0 || double.IsNaN(L)) L = 1;
                int side = (i / 2) % 2 != 0 ? 1 : -1;
                double nx = (-dy / L) * side, ny = (dx / L) * side, w = Width((double)i / N) / 2, len = 1.8 + 2.4 * (1 - (double)i / N), tx0 = dx / L, ty0 = dy / L;
                double x0 = ax + nx * (w - 0.5), y0 = ay + ny * (w - 0.5), tx = x0 + nx * len - tx0 * len * 0.7, ty = y0 + ny * len - ty0 * len * 0.7;
                if (y0 > 0) continue;
                // a hooked thorn: a broad foot on the stem, a sharp lit point raking back toward the root
                p.Tri(x0 - tx0 * 1.6, y0 - ty0 * 1.6, x0 + tx0 * 1.4, y0 + ty0 * 1.4, tx, ty, T[1]);
                p.Line(x0 + tx0 * 0.8, y0 + ty0 * 0.8, tx, ty, T[2]);
                p.Px(tx, ty, T[3]);
            }
            foreach (int k in LEAF_AT)
            {
                double ax = pts[k][0], ay = pts[k][1], bx = pts[k + 1][0], by = pts[k + 1][1], a = Atan2(by - ay, bx - ax);
                int side = k % 2 != 0 ? 1 : -1;
                if (ay > -2) continue;
                double la = a + side * 1.1 + Px.Sin(t * 9 + k) * 0.2, lx = ax + Px.Cos(la) * 4, ly = ay + Px.Sin(la) * 4;
                NatureLib.DrawLeaf(p, lx, ly, 7, 2.2, la, 0.3 + Px.Sin(t * 5 + k) * 0.4, new[] { C[1], C[3], C[5], C[6] });
            }
        }

        sealed class Chip
        {
            public double x, y, vx, vy, a, va, s, life;
            public double[][] pts;
        }

        sealed class Leaf
        {
            public double x, y, vx, vy, a, va, fl, vf, life, max;
        }

        sealed class Spot
        {
            public double x, t0, w;
        }

        readonly uint[] C, T, S;
        readonly Rng R;
        readonly Particles ps;
        readonly NatureLib.Drops drops;
        readonly Lights lights;
        readonly List<Chip> chips = new List<Chip>();
        readonly List<Leaf> torn = new List<Leaf>();
        readonly List<Spot> spots = new List<Spot>();
        double t;
        bool burst, cracked, sunk;

        VineWhip(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            T = Fit(o.Ramp("thorn"), 4);
            S = Fit(o.Ramp("soil"), 8);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[4] }, emitter: e);
            drops = new NatureLib.Drops(x => 0);
            lights = new Lights(e);
        }

        void Dirt(int n, double sp)
        {
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.2, 1.2), v = R.Range(60, 200) * sp;
                var c = new Chip { x = R.Range(-3, 3), y = -1, vx = Px.Cos(a) * v, vy = Px.Sin(a) * v };
                c.a = R.Range(0, TAU);
                c.va = R.Range(-15, 15);
                c.s = R.Range(1.4, 2.6);
                c.pts = NatureLib.StoneShape(R);
                c.life = Math.Min(R.Range(0.6, 0.9), DURATION - 0.05 - t);
                chips.Add(c);
            }
            for (int i = 0; i < n * 3; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1, 1), v = R.Range(50, 200) * sp;
                ps.Spawn(P.Grit, R.Range(-3, 3), -1, Px.Cos(a) * v, Px.Sin(a) * v, Math.Min(R.Range(0.5, 0.9), DURATION - 0.05 - t), R.Pick(new[] { S[5], S[4], S[3] }), R.Chance(0.3) ? 2 : 1);
            }
            for (int i = 0; i < n; i++) ps.Spawn(P.Dust, R.Range(-4, 4), -R.Range(0, 3), R.Sign() * R.Range(15, 60), -R.Range(4, 20), Math.Min(R.Range(0.4, 0.8), DURATION - 0.05 - t), S[5], R.Range(2, 4));
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!burst)
            {
                burst = true;
                Dirt(6, 1);
            }
            if (!cracked && t >= CRACK)
            {
                cracked = true;
                double[][] a = PoseAt(t), b = PoseAt(t - 0.01);
                double x = a[N][0], y = a[N][1], vx = (x - b[N][0]) / 0.01, vy = (y - b[N][1]) / 0.01, sp = Hypot(vx, vy);
                if (sp == 0 || double.IsNaN(sp)) sp = 1;
                lights.Add(x, y, 22, WHITE, 0.5, 0.08);
                lights.Add(x, y, 50, C[5], 0.35, 0.4);
                for (int i = 0; i < 18; i++)
                {
                    double ang = Atan2(vy, vx) + R.Range(-0.7, 0.7), v = R.Range(80, 260);
                    drops.Add(x, y, Px.Cos(ang) * v, Px.Sin(ang) * v - R.Range(0, 60), R.Chance(0.3) ? 2 : 1);
                }
                for (int i = 0; i < 4; i++)
                {
                    var l = new Leaf { x = x, y = y };
                    l.vx = (vx / sp) * R.Range(40, 120) + R.Range(-30, 30);
                    l.vy = R.Range(-90, -20);
                    l.a = R.Range(0, TAU);
                    l.va = R.Range(-12, 12);
                    l.fl = R.Range(0, TAU);
                    l.vf = R.Range(8, 16);
                    l.life = R.Range(0.65, 0.8);
                    torn.Add(l);
                }
                foreach (Leaf l in torn) l.max = l.life;
                for (int i = 0; i < 8; i++) ps.Spawn(P.Star, x + R.Range(-6, 6), y + R.Range(-6, 6), R.Range(-20, 20), R.Range(-20, 20), R.Range(0.15, 0.35), C[7], R.Range(0.6, 1.6));
            }
            if (!sunk && t >= 1.08)
            {
                sunk = true;
                Dirt(3, 0.5);
            }
            drops.Update(dt, (d, g) => spots.Add(new Spot { x = d.x, t0 = t, w = d.s * 0.7 + R.Range(0.2, 1) }));
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                Chip c = chips[i];
                if ((c.life -= dt) <= 0) chips.RemoveAt(i);
                c.vy += 950 * dt;
                c.x += c.vx * dt;
                c.y += c.vy * dt;
                c.a += c.va * dt;
                if (c.y > 0)
                {
                    c.y = 0;
                    c.vy *= -0.3;
                    c.vx *= 0.6;
                }
            }
            for (int i = torn.Count - 1; i >= 0; i--)
            {
                Leaf l = torn[i];
                if ((l.life -= dt) <= 0) torn.RemoveAt(i);
                l.vy += 160 * dt;
                l.vx -= l.vx * 2.5 * dt;
                l.vy -= l.vy * 2 * dt;
                l.x += l.vx * dt + Px.Sin(l.fl) * 20 * dt;
                l.y += l.vy * dt;
                l.a += l.va * dt;
                l.fl += l.vf * dt;
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // a hole torn in the floor where it comes up, sap spots where it sprays
            double k = 1 - Smooth(1.15, 1.35, t);
            for (int x = -5; x <= 5; x++) p.Dot(x, 0, Math.Abs(x) < 3 ? S[0] : S[1], k * (1 - Math.Abs(x) / 7.0));
            foreach (Spot s in spots)
            {
                double a = (1 - Smooth(0.25, 0.6, t - s.t0)) * (1 - Smooth(1.2, 1.35, t));
                for (double x = Math.Floor(s.x - s.w); x <= Math.Ceiling(s.x + s.w); x++) p.Dot(x, 0, t - s.t0 < 0.1 ? C[6] : C[4], a);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            double[][] pts = PoseAt(t);
            // the smear: a fan of streaks, each the path one point of the outer vine just swept, the tip's white,
            // the ones further in cooling down the ramp, all fading back along their length
            if (t > LASH + 0.04 && t < CRACK + 0.1)
            {
                double age = Clamp((t - CRACK + 0.02) / 0.12);
                var hist = new double[12][][];
                for (int k = 0; k < 12; k++) hist[k] = PoseAt(t - k * 0.0045);
                for (int k = 0; k < 6; k++) Haze(p, new[] { hist[k][N], hist[k + 1][N], hist[k + 1][N - 12], hist[k][N - 12] }, k < 2 ? C[6] : C[5], 0.4 * (1 - k / 6.0) * (1 - age));
                for (int i = N - 12; i <= N; i += 2)
                {
                    double q = (N - i) / 12.0;
                    uint col = q < 0.1 ? WHITE : q < 0.3 ? C[7] : q < 0.55 ? C[6] : q < 0.8 ? C[5] : C[4];
                    double len = Round(11 * (1 - q * 0.6));
                    for (int k = 0; k < len; k++) p.Line(hist[k][i][0], hist[k][i][1], hist[k + 1][i][0], hist[k + 1][i][1], k < 2 && q < 0.1 ? WHITE : col, (1 - age) * (k < len * 0.5 ? 1 : 2 * (1 - k / len)));
                }
                p.GlowLine(hist[0][N][0], hist[0][N][1], hist[6][N][0], hist[6][N][1], 14, C[5], 0.35 * (1 - age));
            }
            Vine(p, pts, C);
            DrawThorns(p, pts, T, C, t);
            double lk = Smooth(LASH, CRACK, t) * (1 - Smooth(CRACK, CRACK + 0.35, t));
            if (lk > 0)
                for (int i = 6; i <= N; i += 6)
                    p.Glow(pts[i][0], pts[i][1], 12, C[5], 0.18 * lk);
            // the crack: a white burst of air lines and a glint at the tip
            double e = t - CRACK;
            if (e >= 0 && e < 0.12)
            {
                double x = pts[N][0], y = pts[N][1], k = 1 - e / 0.12;
                for (int j = 0; j < 7; j++)
                {
                    double a = (j / 7.0) * TAU + 0.3, r0 = 4 + 10 * (1 - k), r1 = r0 + 5 * k;
                    p.Line(x + Px.Cos(a) * r0, y + Px.Sin(a) * r0, x + Px.Cos(a) * r1, y + Px.Sin(a) * r1, k > 0.5 ? WHITE : C[6]);
                }
                p.Ring(x, y, 3 + 8 * (1 - k), 1, C[7], k);
                Sim.Sparkle(p, x, y, 7 * k, C[7], C[5], 0.4);
            }
            foreach (Chip c in chips) NatureLib.DrawRock(p, c.x, c.y, c.s, c.a, c.pts, S, Math.Min(1, c.life / 0.25));
            foreach (Leaf l in torn) NatureLib.DrawLeaf(p, l.x, l.y, 6, 2, l.a, l.fl, new[] { C[1], C[3], C[5], C[6] }, Math.Min(1, l.life / 0.3));
            drops.Draw(p, C);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
