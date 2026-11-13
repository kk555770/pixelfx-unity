// The wave (spikes punching up one after another, each bigger, with overshoot, a white frame, a burst of debris
// and glints, a line running along the floor ahead of them) is adapted from Sprite Fusion's open-source ice
// spikes (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Thorns : Effect
    {
        const double DURATION = 2.4;

        public static readonly Module Module = new Module("thorns", "pixelfx/fx/nature/thorns", 232, 92, 48, 78, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#160c0e", "#2e1418", "#4e2224", "#743a30", "#9a5a3e", "#c08458", "#e2b484", "#fbe8c8" },
                ["glow"] = new[] { "#123818", "#2f7a2a", "#6cb83a", "#c8f070" },
                ["soil"] = new[] { "#120c0c", "#251917", "#3b2a24", "#553d31", "#715440", "#8f6e52", "#b8966e", "#e6d0a8" },
                ["count"] = 6,
                ["seed"] = 8,
            },
            (o, e) => new Thorns(o, e))
        {
            Title = "Thorns",
            Desc = "A seam races along the floor and hooked thorns punch up out of it one after another, each bigger than the last, in bursts of dirt and green light; they hold, then sink.",
            Tags = new[] { "nature", "thorns", "plant", "spikes", "ground", "attack", "one-shot" },
            Bg = "ground",
        };

        const double RISE = 0.11, SINK = 0.36;

        sealed class Spike
        {
            public double x, h, w, lean, hook, at, sink;
            public int seed;
            public bool up, down;
        }

        sealed class Chip
        {
            public double x, y, vx, vy, a, va, s, life;
            public double[][] pts;
        }

        // a hooked thorn: a hardwood horn tapering from its foot, its point hooked forward; lit down its left edge with a
        // bright ridge, shaded on the right, grain running up it, a pale point, a barb off its back, a dark rim
        static void Thorn(Painter p, Spike s, double h, uint[] C, bool white, double bas)
        {
            if (h < 1.5) return;
            double x = s.x, w = s.w, hook = s.hook, lean = s.lean;
            double Cx(double u) => x + lean * u + hook * Px.Pow(u, 2.4);
            double Hw(double u) => (w / 2) * Px.Pow(1 - u, 0.9) + 0.25;
            bool Inside(double px, double py)
            {
                double u = (bas - (py + 0.5)) / h;
                if (u < 0 || u > 1 || py + 0.5 > 0) return false;
                double c = Cx(u), fx = px + 0.5;
                return Math.Abs(fx - c) <= Hw(u);
            }
            double X0 = Math.Floor(x - w - 6), X1 = Math.Ceiling(x + w / 2 + Math.Abs(lean) + Math.Abs(hook) + 2);
            for (double py = Math.Floor(bas - h) - 1; py <= Math.Min(0, Math.Ceiling(bas)); py++)
            for (double px = X0; px <= X1; px++)
            {
                if (!Inside(px, py))
                {
                    if (py <= 0 && (Inside(px - 1, py) || Inside(px + 1, py) || Inside(px, py + 1) || Inside(px, py - 1))) p.Dot(px, py, C[0], 1);
                    continue;
                }
                if (white)
                {
                    p.Px(px, py, WHITE);
                    continue;
                }
                double u = (bas - (py + 0.5)) / h, c = Cx(u), r = Hw(u), q = (px + 0.5 - (c - r)) / (2 * r);
                int i = q < 0 ? 4 : q < 0.16 ? 6 : q < 0.36 ? 5 : q < 0.46 ? 7 : q < 0.7 ? 4 : q < 0.86 ? 3 : 2;
                if (u > 0.86) i = q < 0.6 ? 7 : 5;
                if (i < 7 && i > 2 && Rand(px, Math.Floor((py + 0.5 - bas) / 3), s.seed) < 0.12) i -= 1;
                p.Px(px, py, C[i]);
            }
            // a barb off its back, raking up and away
            if (!white && h > 14)
            {
                double u0 = 0.34, u1 = 0.46, y0 = bas - u0 * h, y1 = bas - u1 * h, e0 = Cx(u0) - Hw(u0), e1 = Cx(u1) - Hw(u1), L = w * 0.45;
                if (y0 <= 0)
                {
                    p.Tri(e0 + 0.5, y0, e1 + 0.5, y1, e1 - L, y1 - L * 0.7, C[3]);
                    p.Line(e1, y1, e1 - L, y1 - L * 0.7, C[6]);
                    p.Px(e1 - L, y1 - L * 0.7, C[7]);
                    p.Line(e0, y0 + 1, e1 - L + 1, y1 - L * 0.7 + 1, C[0]);
                }
            }
        }

        static readonly double[][] COLLAR = { new[] { -1, -2.5, 1 }, new[] { 1, -0.55, 0.9 }, new[] { -1, -1.9, 0.7 } };

        readonly uint[] C, G, S;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Chip> chips = new List<Chip>();
        readonly Spike[] list;
        readonly double reach;
        double t;

        Thorns(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            G = Fit(o.Ramp("glow"), 4);
            S = Fit(o.Ramp("soil"), 8);
            R = new Rng(o.Seed);
            int n = (int)Math.Max(2, o.Num("count"));
            ps = new Particles(R, ground: x => 0, sparks: new[] { G[3], G[2], G[1], G[0] }, emitter: e);
            lights = new Lights(e);
            list = new Spike[n];
            for (int i = 0; i < n; i++)
            {
                double k = (double)i / (n - 1), h = 12 + 36 * k + R.Range(-1.5, 1.5);
                var s = new Spike { h = h };
                s.x = 10 + i * (104.0 / (n - 1)) + R.Range(-3, 3);
                s.w = 6 + 9 * k;
                s.lean = R.Range(-1, 1.5) + k * 2;
                s.hook = h * R.Range(0.14, 0.22);
                s.at = 0.12 + i * 0.075;
                s.sink = 1.45 + i * 0.06;
                s.seed = R.Int(1, 999);
                list[i] = s;
            }
            reach = list[n - 1].x + 8;
        }

        void AddChips(double x, double sp, double nn)
        {
            for (int i = 0; i < nn; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.1, 1.1), v = R.Range(60, 210) * sp;
                var c = new Chip { x = x + R.Range(-3, 3), y = -1, vx = Px.Cos(a) * v, vy = Px.Sin(a) * v };
                c.a = R.Range(0, TAU);
                c.va = R.Range(-15, 15);
                c.s = R.Range(1.3, 2.6) * Math.Sqrt(sp);
                c.pts = NatureLib.StoneShape(R);
                c.life = Math.Min(R.Range(0.7, 1.1), DURATION - 0.05 - t);
                chips.Add(c);
            }
        }

        double Grown(Spike s) => t < s.at ? 0 : Ease.OutBack(Clamp((t - s.at) / RISE), 2.4);
        double Sunk(Spike s) => Smooth(s.sink, s.sink + SINK, t);

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            foreach (Spike s in list)
            {
                double k = s.h / 48;
                if (!s.up && t >= s.at)
                {
                    s.up = true;
                    lights.Add(s.x, -s.h * 0.5, 18 + 18 * k, WHITE, 0.45, 0.08);
                    lights.Add(s.x, -s.h * 0.4, 26 + 26 * k, G[2], 0.45, 0.4);
                    AddChips(s.x, 0.6 + 0.6 * k, 3 + Round(5 * k));
                    for (int i = 0; i < 10 + 18 * k; i++)
                    {
                        double a = -Math.PI / 2 + R.Range(-1, 1), v = R.Range(60, 230) * (0.6 + 0.6 * k);
                        ps.Spawn(P.Grit, s.x + R.Range(-s.w / 3, s.w / 3), -1, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.6, 1.1), R.Pick(new[] { S[5], S[4], S[3] }), R.Chance(0.3) ? 2 : 1);
                    }
                    for (int i = 0; i < 3 + 5 * k; i++) ps.Spawn(P.Smoke, s.x + R.Range(-4, 4), -R.Range(1, 4), R.Sign() * R.Range(20, 80), -R.Range(5, 25), R.Range(0.7, 1.2), S[5], R.Range(3, 5.5) * (0.6 + 0.5 * k), 0, PF.None);
                    for (int i = 0; i < 2 + 3 * k; i++) ps.Spawn(P.Star, s.x + s.hook * 0.8 + R.Range(-3, 3), -s.h * R.Range(0.5, 1), R.Range(-10, 10), -R.Range(5, 20), R.Range(0.3, 0.6), G[3], R.Range(0.8, 2), R.Range(0, 0.12));
                    for (int i = 0; i < 4; i++) ps.Spawn(P.Mote, s.x + R.Range(-3, 3), -R.Range(2, s.h * 0.6), R.Range(-20, 20), -R.Range(30, 70), R.Range(0.4, 0.8), G[3], 0.7);
                }
                if (!s.down && t >= s.sink)
                {
                    s.down = true;
                    AddChips(s.x, 0.35, 2 + Round(2 * k));
                    for (int i = 0; i < 3 + 3 * k; i++) ps.Spawn(P.Dust, s.x + R.Range(-s.w / 2, s.w / 2), -R.Range(0, 3), R.Sign() * R.Range(15, 50), -R.Range(2, 10), Math.Min(R.Range(0.4, 0.7), DURATION - 0.05 - t), S[5], R.Range(2, 4));
                }
                // while they stand, glints run up their lit edges
                if (s.up && !s.down && t > s.at + 0.2 && R.Chance(0.05 * f)) ps.Spawn(P.Star, s.x + s.lean * 0.5 - s.w * 0.15, -s.h * R.Range(0.3, 0.8), 0, -3, R.Range(0.25, 0.45), C[7], R.Range(0.8, 1.8));
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
                    if (c.vy > 120)
                    {
                        c.y = 0;
                        c.vy *= -0.3;
                        c.vx *= 0.6;
                        c.va *= 0.5;
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
            // the seam racing ahead along the floor: a dark split with a green glow in it, closing up again at the end
            double run = Ease.OutCubic(Clamp((t - 0.04) / 0.5)) * reach, fade = 1 - Smooth(1.9, 2.3, t);
            for (int x = -2; x <= run; x++)
            {
                double hot = Clamp(1 - (run - x) / 18) * (1 - Smooth(0.6, 1.2, t)), h = Rand(x, 31);
                p.Dot(x, 0, hot > 0.4 ? G[3] : hot > 0.1 ? G[2] : S[0], fade);
                if (h < 0.5) p.Dot(x, 1, S[1], fade * 0.8);
                if (h < 0.15) p.Dot(x, -1, S[4], fade);
                if (hot > 0.3 && (x & 3) == 0) p.Glow(x, 0, 6, G[2], 0.3 * hot);
            }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            foreach (Spike s in list)
            {
                double g = Grown(s), k = Sunk(s);
                if (g <= 0 || k >= 1) continue;
                Thorn(p, s, s.h * g, C, t - s.at < 0.035, k * s.h * 1.05);
                if (t - s.at < 0.3) p.Glow(s.x, -s.h * 0.5 * g, s.h * 0.6 + 6, G[2], 0.35 * (1 - (t - s.at) / 0.3));
                // a collar of leaves round its foot, unfurling as it comes up and folding away as it sinks
                double lk = Clamp((t - s.at - 0.04) / 0.18) * (1 - k), lw = s.w * 0.5;
                if (lk > 0.05)
                    foreach (double[] cl in COLLAR)
                    {
                        double sd = cl[0], a0 = cl[1], sz = cl[2];
                        double la = a0 + sd * 0.25 * (1 - lk) + Px.Sin(t * 4 + s.x) * 0.06, L = (5 + s.w * 0.45) * sz * lk;
                        NatureLib.DrawLeaf(p, s.x + sd * lw + Px.Cos(la) * L * 0.5, -1 + Px.Sin(la) * L * 0.5, L, 1.4 + s.w * 0.1 * sz, la, 0.35, new[] { G[0], G[1], G[2], G[3] });
                    }
                p.Glow(s.x, -2, s.w + 6, G[2], 0.18 * lk);
            }
            foreach (Chip c in chips) NatureLib.DrawRock(p, c.x, c.y, c.s, c.a, c.pts, S, Math.Min(1, c.life / 0.25));
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
