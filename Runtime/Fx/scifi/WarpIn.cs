// The column (bands of the ramp across it, strands, pulses scrolling along it, a guide thread before it lands,
// rings riding it) is adapted from Sprite Fusion's open-source holy light
// (https://www.spritefusion.com/pixel-art-vfx/holy-light), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class WarpIn : Effect
    {
        static readonly Pose IN_ = new Pose { ["crouch"] = 7, ["lean"] = 3, ["fx"] = 9, ["fy"] = -12, ["bx"] = 12, ["by"] = -12, ["ffx"] = 8, ["fbx"] = -6 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(0, IN_),
                new PoseKey(1.82, IN_),
                new PoseKey(2.05, new Pose { ["crouch"] = 3, ["fx"] = 8, ["fy"] = -19, ["bx"] = 8, ["by"] = -18, ["ffx"] = 4, ["fbx"] = -3 }),
                new PoseKey(2.5, Poses.READY),
                new PoseKey(3, Poses.READY),
            },
        };

        // the beats: the guide thread, the slam, the tube opens, the scan, the cooling, the beam narrows, it snaps shut
        const double SLAM = 0.34, OPEN = 0.52, SCAN0 = 0.62, SCAN1 = 1.4, COOL = 0.42, DONE = SCAN1 + COOL, CLOSE = 1.9, SNAP = 2.16;
        const double TOP = -128;

        public static readonly Module Module = new Module("warp-in", "pixelfx/fx/scifi/warp-in", 192, 168, 96, 128, 3, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0b1030", "#152a63", "#1d4f9e", "#2582d2", "#36b9ee", "#8ae8ff", "#d4fbff", "#ffffff" },
                ["width"] = 15,
                ["seed"] = 3,
            },
            (o, e) => new WarpIn(o, e), act: Act, actor: 2,
            actorAlpha: (t, o) => t >= DONE ? 1 : 0)
        {
            Title = "Warp in",
            Desc = "A beam slams down, rings run down it and a scanline paints the hero in from head to toe, white-hot pixels cooling to their own colors; the beam pinches shut in sparks.",
            Tags = new[] { "scifi", "teleport", "warp", "beam", "spawn", "arrive", "one-shot" },
            Bg = "ground",
        };

        // when the scanline crosses row y (top to bottom, easing in and out), for a body from y0 to the floor
        static double ScanY(double t, double y0) => Lerp(y0 - 2, 1, Ease.InOutSine(Clamp((t - SCAN0) / (SCAN1 - SCAN0))));
        static double ScanT(double y, double y0) => SCAN0 + ((SCAN1 - SCAN0) * Px.Acos(1 - 2 * Clamp((y + 0.5 - (y0 - 2)) / (3 - y0)))) / Math.PI;

        // the beam's half width over time: overshoots as it lands, hums, narrows to a thread
        static double WidthAt(double t, double W)
        {
            if (t < SLAM) return 0;
            if (t < CLOSE) return W * (1 + 0.45 * Exp(-(t - SLAM) * 14) + 0.03 * Px.Sin(t * 31));
            return Lerp(W, 0.7, Ease.OutCubic(Clamp((t - CLOSE) / (SNAP - CLOSE))));
        }

        sealed class BodyPx
        {
            public int x, y;
            public uint c;
            public bool edge;
            public double ts, j;
        }

        readonly uint[] C;
        readonly double W, seed;
        readonly Rng R;
        readonly Actor A;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<BodyPx> body = new List<BodyPx>();
        readonly Dictionary<int, int[]> rows = new Dictionary<int, int[]>();
        readonly List<double> rings = new List<double>();
        double t, nextRing = SLAM + 0.08, lastRow;
        bool slammed, snapped;

        WarpIn(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            W = o.Num("width");
            seed = o.Seed;
            R = new Rng(o.Seed);
            A = o.Actor;
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[3] }, haze: C[5], emitter: e);
            lights = new Lights(e);
            // the body: each filled pixel, its own color, whether it's on the outline, and each row's extent
            bool On(int x, int y) => x >= 0 && y >= 0 && x < A.W && y < A.H && A.Mask.Data[y * A.W + x] != 0;
            for (int my = 0; my < A.H; my++)
                for (int mx = 0; mx < A.W; mx++)
                {
                    int i = my * A.W + mx;
                    if (A.Mask.Data[i] == 0) continue;
                    int x = A.X + mx, y = A.Y + my;
                    bool edge = !On(mx - 1, my) || !On(mx + 1, my) || !On(mx, my - 1) || !On(mx, my + 1);
                    uint px = A.Pixels[i];
                    body.Add(new BodyPx { x = x, y = y, c = px != 0 ? px : C[2], edge = edge, ts = ScanT(y, A.Y), j = Rand(seed, mx, my) });
                    int[] r = rows.TryGetValue(y, out int[] rr) ? rr : new[] { x, x };
                    rows[y] = new[] { Math.Min(r[0], x), Math.Max(r[1], x) };
                }
            lastRow = A.Y - 3;
        }

        void Column(Painter p)
        {
            double w = WidthAt(t, W);
            if (w <= 0.05 || t > SNAP + 0.14) return;
            // it falls from the sky as it lands, and snaps back up into it at the end
            double bottom = t - SLAM < 0.06 ? Lerp(TOP, 0, Ease.InCubic(Clamp((t - SLAM) / 0.06) * 0.5 + 0.5)) : t > SNAP ? Lerp(0, TOP, Ease.OutExpo((t - SNAP) / 0.14)) : 0;
            // the core hollows out from the middle into a tube of light, and the tube's walls close into a thread
            bool hero = t - SLAM < 0.07;
            double hollow = 0.78 * Smooth(OPEN - 0.16, OPEN, t), thin = 1 - Clamp((w - 1.5) / (W - 1.5));
            for (int y = (int)Math.Floor(TOP); y < bottom; y++)
            {
                double pulse = ((((y - TOP) * 0.028 - t * 2.6) % 1) + 1) % 1 < 0.09 ? 1 : 0;
                for (double x = Math.Floor(-w * 1.35); x <= Math.Ceiling(w * 1.35); x++)
                {
                    double q = Math.Abs(x + 0.5) / w;
                    if (q > 1.35) continue;
                    if (hero)
                    {
                        p.Dot(x, y, q < 0.55 ? WHITE : q < 0.8 ? C[6] : q < 1 ? C[5] : C[4], q < 1 ? 1 : (1.35 - q) / 0.35);
                        continue;
                    }
                    if (w < 2.4)
                    {
                        p.Dot(x, y, q < 0.7 ? WHITE : C[6], q < 1 ? 1 : (1.35 - q) / 0.35);
                        continue;
                    }
                    // strands: thin bright threads drifting inside the beam
                    double strand = 0;
                    for (int s = 0; s < 5; s++)
                        if (Math.Abs((x + 0.5) / w - (Rand(seed, s) - 0.5) * 1.3 - 0.06 * Px.Sin(t * (1.5 + s) + s * 2)) < 0.05) strand = 1;
                    if (q < hollow) p.Blend(x + 0.5, y + 0.5, strand != 0 || pulse != 0 ? C[6] : C[5], Clamp(0.12 + 0.3 * q * q + 0.3 * strand + 0.25 * pulse + 0.7 * thin));
                    else if (q < 0.78)
                    {
                        double lvl = Math.Max(0, (q < 0.22 ? 0 : q < 0.45 ? 1 : 2) - pulse - strand);
                        p.Px(x, y, lvl == 0 ? WHITE : C[7 - (int)lvl]);
                    }
                    else if (q < 0.96) p.Px(x, y, q > 0.86 && q < 0.93 ? WHITE : C[6]);
                    else p.Dot(x, y, q < 1.1 ? C[5] : C[4], (1.35 - q) / 0.39);
                }
                // light rides the walls, not the middle, so the body being built inside stays readable
                if ((y & 7) == 0)
                {
                    double r = Math.Min(w + 8, y - TOP + 2);
                    if (hollow < 0.4) p.Glow(0, y, Math.Min(w * 2.6 + 6, y - TOP + 2), C[4], hero ? 0.55 : 0.32);
                    else
                        foreach (int s in new[] { -1, 1 }) p.Glow(s * (w + 3), y, r, C[4], 0.24);
                }
            }
        }

        // a ring riding down the beam: the near half in front, the far half behind
        void Ring(Painter p, bool near)
        {
            double w = WidthAt(t, W);
            foreach (double t0 in rings)
            {
                double k = (t - t0) / 0.5;
                if (k < 0 || k >= 1) continue;
                double y = Lerp(TOP + 6, -1, Ease.InQuad(k)), rx = w * 1.18 + 1;
                uint c = k < 0.25 ? WHITE : C[6];
                Sim.Band(p, 0, y, rx, 2.6 / rx, 1.5, 1, new[] { C[3], C[5], c, WHITE }, g => Px.Sin(g) >= 0 == near);
                if (near) p.Glow(0, y, rx + 6, C[5], 0.25);
            }
        }

        // the floor where the beam stands: a pool of light, ripples where rings land, the slam's shockwave
        void Floor(Painter p, bool near)
        {
            double on = t < SLAM ? Smooth(0, SLAM, t) * 0.5 : 1 - Smooth(SNAP, SNAP + 0.5, t);
            if (!near && on > 0.01)
            {
                double rx = W * 1.9 + (t < SLAM ? 0 : 2 * Px.Sin(t * 6)), ry = 4;
                for (int y = -4; y <= 4; y++)
                    for (double x = Math.Floor(-rx); x <= Math.Ceiling(rx); x++)
                    {
                        double d = Hypot((x + 0.5) / rx, (y + 0.5) / ry);
                        if (d > 1) continue;
                        p.Dot(x, y, d < 0.35 ? C[7] : d < 0.6 ? C[6] : d < 0.85 ? C[5] : C[4], on * Clamp((1.05 - d) * 2.4));
                    }
                foreach (double x in new[] { -rx * 0.6, 0, rx * 0.6 }) p.Glow(x, -1, rx * 1.3, C[4], 0.3 * on);
            }
            Func<double, bool> keep = g => Px.Sin(g) >= 0 == near;
            foreach (double t0 in rings)
            {
                double k = (t - t0 - 0.5) / 0.35;
                if (k >= 0 && k < 1) Sim.Band(p, 0, 0, W * (1.2 + 1.3 * Ease.OutCubic(k)), 0.16, 1.5, 1 - k, new[] { C[3], C[4], C[5], C[6] }, keep);
            }
            foreach (double[] ds in new[] { new[] { 0, 1.0 }, new[] { 0.08, 0.65 } })
            {
                double d = ds[0], s = ds[1], k = (t - SLAM - d) / 0.55;
                if (k >= 0 && k < 1) Sim.Band(p, 0, 0.5, 92 * s * Ease.OutCubic(k), 0.16, Math.Max(1, 5 * (1 - k)), 1.15 - k, new[] { C[3], C[5], C[6], WHITE }, keep, k < 0.25);
            }
            double kk = (t - SNAP) / 0.45;
            if (kk >= 0 && kk < 1) Sim.Band(p, 0, 0, 10 + 26 * Ease.OutCubic(kk), 0.16, 2 * (1 - kk) + 0.5, 1.1 - kk, new[] { C[4], C[5], C[6], WHITE }, keep, kk < 0.3);
        }

        // the hero, built out of light: the blueprint first, then each row as the scanline crosses it, white-hot
        // and cooling through the beam's color to its own
        void Hero(Painter p)
        {
            if (t >= DONE || t < SCAN0 - 0.2) return;
            double blink = Math.Floor(t * 24);
            foreach (BodyPx b in body)
            {
                double a = t - b.ts;
                if (a < 0)
                {
                    if (b.edge && Rand(b.x, b.y, blink) < 0.6) p.Dot(b.x, b.y, C[1], 0.8 * Smooth(SCAN0 - 0.25, SCAN0 - 0.05, t));
                    continue;
                }
                double k = a / COOL;
                // the newest rows jitter sideways a pixel, as if still arriving
                double dx = a < 0.06 && b.j < 0.3 ? (b.j < 0.15 ? -1 : 1) : 0;
                p.Px(b.x + dx, b.y, k < 0.18 ? WHITE : k < 0.45 ? Mix(b.c, C[6], 0.72) : k < 0.75 ? Mix(b.c, C[5], 0.38) : b.c);
                if (k < 0.5) p.Light(b.x + dx, b.y, C[5], 0.22 * (1 - k / 0.5));
            }
        }

        // the scanline: a white rule across the beam, hottest where it crosses the body, sparking at its edges
        void Scanline(Painter p)
        {
            if (t < SCAN0 || t > SCAN1) return;
            int y = (int)Math.Floor(ScanY(t, A.Y));
            double w = WidthAt(t, W) * 1.5;
            rows.TryGetValue(y, out int[] r);
            for (double x = Math.Floor(-w); x <= Math.Ceiling(w); x++)
            {
                bool inside = r != null && x >= r[0] && x <= r[1];
                p.Dot(x, y, inside ? WHITE : C[6], inside ? 1 : 1 - Math.Abs(x + 0.5) / (w + 1));
            }
            p.GlowLine(-w, y, w, y, 5, C[5], 0.5);
            if (r != null)
                foreach (int x in r) Sim.Sparkle(p, x + 0.5, y, 2, C[6], C[5], 0.3);
        }

        public override void Update(double dt)
        {
            t += dt;
            // gathering: energy streaks in toward the spot, vapour slides along the floor
            if (t < SLAM - 0.04)
            {
                if (R.Chance(0.8))
                {
                    double a = R.Range(-Math.PI * 0.95, -Math.PI * 0.05), D = R.Range(30, 64), T = R.Range(0.18, 0.28), v = (D * 3) / (Exp(3 * T) - 1);
                    double x = Px.Cos(a) * D, y = Px.Sin(a) * D * 0.8 - 2, l = Hypot(x, y + 2);
                    if (l == 0 || double.IsNaN(l)) l = 1;
                    ps.Spawn(P.Streak, x, y, (-x / l) * v, (-(y + 2) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
                }
                if (R.Chance(0.12))
                {
                    double s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(30, 56), -R.Range(0, 2), -s * R.Range(40, 70), -R.Range(0, 4), R.Range(0.3, 0.45), C[5], R.Range(2, 3));
                }
            }
            if (t >= SLAM && !slammed)
            {
                slammed = true;
                lights.Add(0, -40, 120, C[4], 1, 0.55);
                lights.Add(0, -10, 44, WHITE, 0.9, 0.14);
                for (int i = 0; i < 34; i++)
                {
                    double a = -Math.PI / 2 + R.Sign() * R.Range(0.35, 1.45), sp = R.Range(110, 270);
                    ps.Spawn(P.Spark, R.Range(-W, W) * 0.6, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.75));
                }
                for (int i = 0; i < 18; i++)
                {
                    double s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(4, 18), -R.Range(0, 4), s * R.Range(40, 150), -R.Range(2, 18), R.Range(0.5, 0.95), R.Chance(0.6) ? C[6] : C[5], R.Range(3, 6));
                }
                for (int i = 0; i < 10; i++) ps.Spawn(P.Star, R.Range(-W, W) * 1.3, -R.Range(2, 40), R.Range(-20, 20), -R.Range(10, 40), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2.2));
            }
            if (slammed && t < CLOSE)
            {
                // rings ride down, light falls down the beam, glints wink at its walls
                if (t >= nextRing)
                {
                    rings.Add(nextRing);
                    nextRing += 0.17;
                }
                double w = WidthAt(t, W);
                if (R.Chance(0.5)) ps.Spawn(P.Streak, R.Range(-0.8, 0.8) * w, TOP + R.Range(9, 36), 0, R.Range(60, 110), R.Range(0.3, 0.45), R.Chance(0.5) ? WHITE : C[6]);
                if (R.Chance(0.18)) ps.Spawn(P.Star, R.Sign() * w * R.Range(0.8, 1.3), -R.Range(4, 70), 0, -R.Range(5, 20), R.Range(0.3, 0.6), C[6], R.Range(0.6, 1.8));
                if (R.Chance(0.1))
                {
                    double s = R.Sign();
                    ps.Spawn(P.Vapour, s * R.Range(8, 16), -R.Range(0, 3), s * R.Range(20, 50), -R.Range(2, 8), R.Range(0.5, 0.8), C[5], R.Range(2.5, 4));
                }
            }
            // the scanline spits light off the edges of the body as it crosses
            if (t > SCAN0 && t < SCAN1)
            {
                double y = Math.Floor(ScanY(t, A.Y));
                if (y != lastRow)
                {
                    lastRow = y;
                    if (rows.TryGetValue((int)y, out int[] r) && R.Chance(0.7))
                        foreach (int[] xs in new[] { new[] { r[0], -1 }, new[] { r[1] + 1, 1 } })
                            if (R.Chance(0.6)) ps.Spawn(P.Mote, xs[0], y, xs[1] * R.Range(40, 90), R.Range(-20, 10), R.Range(0.15, 0.3), R.Chance(0.5) ? WHITE : C[6], 0.6);
                }
            }
            if (t >= DONE && t - dt < DONE)
            {
                // set: a glint or two on the new body
                for (int i = 0; i < 6; i++)
                {
                    BodyPx b = body[(int)Math.Floor(R.Next() * body.Count)];
                    ps.Spawn(P.Star, b.x + 0.5, b.y + 0.5, R.Range(-6, 6), -R.Range(6, 20), R.Range(0.4, 0.8), R.Chance(0.5) ? WHITE : C[6], R.Range(1, 2));
                }
            }
            if (t >= SNAP && !snapped)
            {
                snapped = true;
                lights.Add(0, -1, 32, C[4], 0.5, 0.25);
                lights.Add(0, -1, 10, WHITE, 0.5, 0.08);
                for (int i = 0; i < 22; i++)
                {
                    double a = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(90, 260);
                    ps.Spawn(P.Spark, R.Range(-2, 2), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.6));
                }
                for (int i = 0; i < 12; i++) ps.Spawn(P.Star, R.Range(-16, 16), -R.Range(0, 46), R.Range(-10, 10), -R.Range(8, 26), R.Range(0.5, 0.9), R.Chance(0.5) ? WHITE : C[6], R.Range(0.6, 1.8));
                // where the beam stood, motes keep rising for a while
                for (int i = 0; i < 18; i++) ps.Spawn(R.Chance(0.6) ? P.Star : P.Mote, R.Range(-6, 6), -R.Range(10, 100), R.Range(-6, 6), -R.Range(14, 40), R.Range(0.5, 0.75), R.Chance(0.4) ? WHITE : R.Chance(0.5) ? C[6] : C[5], R.Range(0.5, 1.4), R.Range(0, 0.25));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Floor(p, false);
            Ring(p, false);
            Column(p);
            // the guide thread: a dotted line pricking down to the spot, brightening
            if (t < SLAM)
            {
                double a = Smooth(0, SLAM * 0.8, t) * 0.8, head = Lerp(TOP, 0, Ease.OutCubic(Clamp(t / (SLAM * 0.6))));
                for (int y = (int)TOP; y < head; y++) p.Dot(0, y, (y & 1) == 0 ? C[6] : C[5], a * (0.35 + 0.65 * ((y - TOP) / -TOP)));
                p.Glow(0, head, 10, C[5], 0.5 * a);
                // a target on the floor, closing in
                double rx = Lerp(34, 12, Ease.OutCubic(t / SLAM)), k = Smooth(0, 0.15, t);
                for (int i = 0; i < 48; i++)
                {
                    double g = (i / 48.0) * TAU + t * 3;
                    if ((i / 4) % 2 != 0) continue;
                    p.Dot(Px.Cos(g) * rx, Px.Sin(g) * rx * 0.16, i % 4 == 0 ? WHITE : C[5], k);
                }
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            Hero(p);
            Scanline(p);
            Ring(p, true);
            Floor(p, true);
            // the set body's outline flares as it lands, then fades
            if (t >= DONE && t < DONE + 0.3) p.Outline(A.Mask, A.X, A.Y, C[6], 1 - (t - DONE) / 0.3);
            if (t > SLAM && t < SLAM + 0.16) Sim.Sparkle(p, 0, -2, 12 * (1 - (t - SLAM) / 0.16), C[6], C[5], 0.6);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
