// The healing light (a soft column of glow, a lit ring on the floor, motes and bevelled crosses drifting up and
// glinting) is adapted from Sprite Fusion's open-source heal (https://www.spritefusion.com/pixel-art-vfx/heal),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class HealingBloom : Effect
    {
        const double DURATION = 3.3;

        public static readonly Module Module = new Module("healing-bloom", "pixelfx/fx/nature/healing-bloom", 124, 120, 62, 108, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0c1a10", "#173a1a", "#24582a", "#357a34", "#52a03e", "#86c84e", "#c4ec7a", "#f4ffd0" },
                ["petal"] = new[] { "#4a1a3a", "#a8406e", "#ec8ab0", "#ffd8e8" },
                ["heart"] = new[] { "#a8641a", "#f0b42e", "#ffe680", "#fffbe0" },
                ["seed"] = 4,
            },
            (o, e) => new HealingBloom(o, e))
        {
            Title = "Healing bloom",
            Desc = "A sprout pushes up and unfurls, a bud swells and opens round a glowing golden heart in a flare of healing light; its petals lift away one by one as motes.",
            Tags = new[] { "nature", "heal", "flower", "plant", "buff", "ground", "one-shot" },
            Bg = "ground",
        };

        const double STEM = 0.42, BUD = 0.75, OPEN = 1.05, LIFT = 1.7, WILT = 2.4, H = 36;
        const int NPET = 6;

        // a bevelled cross of light
        static void Cross(Painter p, double x, double y, int L, uint[] C, double a, bool glint)
        {
            double X = Round(x), Y = Round(y);
            bool Ins(int dx, int dy) => (dx == 0 && Math.Abs(dy) <= L) || (dy == 0 && Math.Abs(dx) <= L);
            for (int dy = -L - 1; dy <= L + 1; dy++)
            for (int dx = -L - 1; dx <= L + 1; dx++)
            {
                if (Ins(dx, dy)) p.Dot(X + dx, Y + dy, glint && dx == 0 && dy == 0 ? WHITE : dy < 0 || dx < 0 ? C[6] : C[5], a);
                else if (L > 1 && (Ins(dx - 1, dy) || Ins(dx + 1, dy) || Ins(dx, dy - 1) || Ins(dx, dy + 1))) p.Dot(X + dx, Y + dy, C[2], a * 0.8);
            }
            p.Glow(x, y, 5 + L * 2, C[6], 0.35 * a);
        }

        sealed class Petal
        {
            public double az, off, at;
            public bool gone;
        }

        sealed class Glyph
        {
            public double x, y, t0, life, seed;
            public int L;
        }

        sealed class Flier
        {
            public double x, y, vx, vy, a, va, fl, vf, life, max;
        }

        static readonly double[][] LEAVES = { new[] { 4.0, -1, 0.1 }, new[] { 7.0, 1, 0.22 } };

        readonly uint[] C, Pt, Ht;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Flier> flying = new List<Flier>();
        readonly List<Glyph> glyphs = new List<Glyph>();
        readonly Petal[] petals;
        double t;
        bool popped, sprouted;

        HealingBloom(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            Pt = Fit(o.Ramp("petal"), 4);
            Ht = Fit(o.Ramp("heart"), 4);
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { Ht[3], Ht[2], Ht[1], Ht[0] }, emitter: e);
            lights = new Lights(e);
            petals = new Petal[NPET];
            for (int i = 0; i < NPET; i++) petals[i] = new Petal { az = ((double)i / NPET) * TAU + 0.3, off = (i * 0.7) % 1, gone = false, at = LIFT + ((i * 0.37) % 1) * 0.55 };
        }

        double Grow() => Ease.OutCubic(Clamp(t / STEM)) * (1 - Ease.InCubic(Clamp((t - WILT) / 0.6)));
        double Sway() => Px.Sin(t * 2.6) * 1.4 * Smooth(0.2, 0.6, t);

        // the stem's points, base to tip: a gentle S that sways
        double[][] Stem()
        {
            double g = Grow();
            var pts = new double[13][];
            for (int i = 0; i <= 12; i++)
            {
                double s = i / 12.0;
                pts[i] = new[] { Px.Sin(s * 3.2) * 2.2 * s + Sway() * s * s, (1 - g) * H * 1.1 - s * H };
            }
            return pts;
        }

        double Open() => Ease.OutBack(Clamp((t - BUD) / (OPEN - BUD)), 1.4);

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            double[] top = Stem()[12];
            if (!sprouted)
            {
                sprouted = true;
                for (int i = 0; i < 8; i++) ps.Spawn(P.Grit, R.Range(-3, 3), -1, R.Range(-40, 40), -R.Range(40, 100), R.Range(0.5, 0.8), R.Pick(new[] { C[2], C[3] }), 1);
                for (int i = 0; i < 3; i++) ps.Spawn(P.Dust, R.Range(-3, 3), -1, R.Sign() * R.Range(10, 30), -R.Range(4, 10), R.Range(0.4, 0.6), C[3], R.Range(1.5, 2.5));
            }
            // the bud draws light in as it swells
            if (t > STEM && t < BUD && R.Chance(0.3 * f))
            {
                double a = R.Range(0, TAU), d = R.Range(14, 26), x = top[0] + Px.Cos(a) * d, y = top[1] - 3 + Px.Sin(a) * d * 0.8;
                ps.Spawn(P.Streak, x, y, (top[0] - x) * 3.4, (top[1] - 3 - y) * 3.4, R.Range(0.18, 0.26), R.Chance(0.5) ? Ht[2] : C[6]);
            }
            if (!popped && t >= OPEN - 0.12)
            {
                popped = true;
                lights.Add(top[0], top[1] - 3, 22, WHITE, 0.45, 0.08);
                lights.Add(top[0], top[1] - 3, 70, Ht[2], 0.5, 0.8);
                for (int i = 0; i < 16; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(40, 120);
                    ps.Spawn(P.Mote, top[0], top[1] - 3, Px.Cos(a) * sp, Px.Sin(a) * sp - 20, R.Range(0.4, 0.8), R.Chance(0.5) ? Ht[3] : C[7], 0.7);
                }
                for (int i = 0; i < 5; i++) ps.Spawn(P.Star, top[0] + R.Range(-10, 10), top[1] - R.Range(0, 14), 0, -6, R.Range(0.3, 0.6), Ht[3], R.Range(1, 2.4), R.Range(0, 0.3));
            }
            // while it blooms: crosses and motes of light drift up round it
            if (t > OPEN && t < WILT && R.Chance(0.13 * f))
            {
                double x = R.Range(-22, 22), y = -R.Range(2, 36), life = Math.Min(R.Range(0.7, 1.1), DURATION - 0.05 - t);
                int L = R.Chance(0.35) ? 3 : 2;
                double seed = R.Next() * 9;
                glyphs.Add(new Glyph { x = x, y = y, t0 = t, life = life, L = L, seed = seed });
            }
            if (t > OPEN - 0.1 && t < WILT && R.Chance(0.35 * f)) ps.Spawn(P.Ember, R.Range(-20, 20), -R.Range(0, 8), R.Range(-6, 6), -R.Range(25, 55), Math.Min(R.Range(0.6, 1.1), DURATION - 0.05 - t), R.Chance(0.6) ? C[6] : Ht[2], R.Chance(0.3) ? 2 : 1);
            for (int i = glyphs.Count - 1; i >= 0; i--)
                if (t - glyphs[i].t0 > glyphs[i].life)
                    glyphs.RemoveAt(i);
            // petals let go one by one and lift away, turning, until they twinkle out
            foreach (Petal q in petals)
                if (!q.gone && t >= q.at)
                {
                    q.gone = true;
                    double e = Math.PI / 2 - 0.15, dx = Px.Sin(e) * Px.Cos(q.az), L = 10;
                    var fl = new Flier { x = top[0] + dx * L * 0.6, y = top[1] - 3 - Px.Cos(e) * L * 0.6 };
                    fl.vx = dx * 30 + R.Range(-10, 10);
                    fl.vy = -R.Range(25, 45);
                    fl.a = Atan2(-Px.Cos(e), dx);
                    fl.va = R.Range(-5, 5);
                    fl.fl = 0;
                    fl.vf = R.Range(5, 9);
                    fl.life = Math.Min(R.Range(0.75, 1.0), DURATION - 0.45 - t);
                    fl.max = fl.life;
                    flying.Add(fl);
                }
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                Flier q = flying[i];
                if ((q.life -= dt) <= 0)
                {
                    ps.Spawn(P.Star, q.x, q.y, 0, -8, Math.Min(0.35, DURATION - 0.05 - t), Pt[3], 1.8);
                    flying.RemoveAt(i);
                    continue;
                }
                q.vx -= q.vx * 1.5 * dt;
                q.vy -= q.vy * 0.8 * dt + 10 * dt;
                q.x += q.vx * dt + Px.Sin(q.fl) * 12 * dt;
                q.y += q.vy * dt;
                q.a += q.va * dt;
                q.fl += q.vf * dt;
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double bloom = Smooth(OPEN - 0.15, OPEN + 0.2, t) * (1 - Smooth(WILT - 0.2, WILT + 0.5, t));
            double[] top = Stem()[12];
            // a column of soft healing light, and a lit ring on the floor that blooms out as it opens
            for (int h = 0; h < 70; h += 7) p.Glow(0, -h, 22, C[5], 0.12 * bloom * (1 - h / 80.0));
            double k = Clamp((t - OPEN + 0.12) / 0.6);
            if (k > 0 && k < 1) Sim.Band(p, 0, 0.5, 6 + 30 * Ease.OutCubic(k), 0.18, Math.Max(1, 3 * (1 - k)), 1.1 - k, new[] { C[3], C[5], Ht[2], Ht[3] }, g => Px.Sin(g) < 0);
            double fw = 6 + 16 * Smooth(0, 0.8, t);
            for (double x = Math.Floor(-fw); x <= fw; x++)
            {
                double u = Math.Abs(x) / fw, a = Math.Sqrt(1 - u) * Smooth(0, 0.4, t) * (1 - Smooth(WILT, WILT + 0.6, t));
                p.Dot(x, 0, u < 0.3 ? C[6] : u < 0.65 ? C[5] : C[4], a);
                if ((x % 5) == 0) p.Glow(x, 0, 8, C[5], 0.2 * a * (1 - u));
            }
            // the far petals sit behind the heart
            if (t > BUD - 0.05)
                foreach (Petal q in petals)
                    if (Px.Sin(q.az) < 0 && !q.gone)
                        DrawPetal(p, q, top);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            double[][] pts = Stem();
            double[] top = pts[12];
            double g = Grow();
            if (g > 0.02)
            {
                // the stem: a 2 px shoot, lit down its left side
                for (int i = 1; i < pts.Length; i++)
                {
                    double x0 = pts[i - 1][0], y0 = pts[i - 1][1], x1 = pts[i][0], y1 = pts[i][1];
                    if (y1 > 0) continue;
                    p.Line(x0, y0, x1, y1, C[5]);
                    p.Line(x0 + 1, y0, x1 + 1, y1, C[3]);
                }
                // leaves unfurl off it
                foreach (double[] lf in LEAVES)
                {
                    int i = (int)lf[0];
                    double sd = lf[1], d = lf[2];
                    double lk = Ease.OutBack(Clamp((t - d - 0.12) / 0.3), 1.6) * (1 - Smooth(WILT, WILT + 0.5, t) * 0.4), x = pts[i][0], y = pts[i][1];
                    if (lk <= 0 || y > -1) continue;
                    double la = sd < 0 ? -2.6 + 0.4 * (1 - lk) : -0.55 - 0.4 * (1 - lk);
                    NatureLib.DrawLeaf(p, x + Px.Cos(la) * 6 * lk, y + Px.Sin(la) * 6 * lk, 12 * lk, 3.4, la, 0.25, new[] { C[1], C[3], C[5], C[6] });
                }
                // the bud, then the heart once it opens
                if (t > STEM - 0.1 && t < OPEN - 0.1)
                {
                    double b = Clamp((t - STEM + 0.1) / (BUD - STEM)), w = 2 + 2.5 * b, h = 3.5 + 5 * b;
                    p.Ellipse(top[0], top[1] - h * 0.6, w, h, (d, a) => Px.Cos(a) < -0.2 ? Pt[2] : d > 0.7 ? Pt[0] : Pt[1]);
                    p.Glow(top[0], top[1] - h * 0.6, 8 + 8 * b, Ht[2], 0.25 * b);
                }
            }
            foreach (Petal q in petals)
                if (Px.Sin(q.az) >= 0 && !q.gone && t > BUD - 0.05)
                    DrawPetal(p, q, top);
            if (t > OPEN - 0.12 && t < WILT + 0.3)
            {
                double pulse = 0.85 + 0.15 * Px.Sin(t * 9), k = 1 - Smooth(WILT - 0.3, WILT + 0.3, t), cy = top[1] - 4;
                p.Circle(top[0], cy, 3.3 * k, Ht[1]);
                p.Circle(top[0] - 0.5, cy - 0.5, 2.2 * k, Ht[2]);
                p.Px(top[0] - 1, cy - 1, Ht[3]);
                p.Glow(top[0], cy, 16 * pulse, Ht[2], 0.5 * k);
                p.Glow(top[0], cy, 34, C[5], 0.2 * k);
            }
            double e = t - OPEN + 0.12;
            if (e > 0 && e < 0.16) Sim.Sparkle(p, top[0], top[1] - 3, 10 * (1 - e / 0.16), Ht[3], Ht[2], 0.6);
            foreach (Flier q in flying) NatureLib.DrawLeaf(p, q.x, q.y, 6, 2.2, q.a, q.fl, new[] { Pt[0], Pt[1], Pt[2], Pt[3] }, Math.Min(1, q.life / 0.3));
            foreach (Glyph q in glyphs)
            {
                double k = (t - q.t0) / q.life;
                Cross(p, q.x + Px.Sin(k * 5 + q.seed) * 2, q.y - k * 24, q.L, C, Px.Sin(Math.PI * k), Px.Sin(k * 14 + q.seed) > 0.8);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }

        // a petal: from closed round the bud to splayed open round the heart, foreshortened by where it points
        void DrawPetal(Painter p, Petal q, double[] top)
        {
            double op = Open(), e = Lerp(0.15, Math.PI / 2 - 0.2, op), dx = Px.Sin(e) * Px.Cos(q.az), dz = Px.Sin(e) * Px.Sin(q.az), dy = -Px.Cos(e);
            double L = Lerp(8, 12, op), len = L * Hypot(dx, dy), ang = Atan2(dy, dx), cy = top[1] - 3;
            bool back = dz < 0;
            if (len < 1.5) return;
            NatureLib.DrawLeaf(p, top[0] + dx * L * 0.5, cy + dy * L * 0.5, len + 1, 3 + (1 - Math.Abs(dz)) * 0.8, ang, back ? 0.9 : 0.2, back ? new[] { Pt[0], Pt[0], Pt[1], Pt[2] } : new[] { Pt[0], Pt[1], Pt[2], Pt[3] });
        }
    }
}
