// The ground's part (grit thrown up that bounces and settles, stones that tumble and kick up dust where they land,
// dust rolling out along the floor and climbing) is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Quake : Effect
    {
        const double DURATION = 3.1;

        public static readonly Module Module = new Module("quake", "pixelfx/fx/nature/quake", 288, 104, 144, 88, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0e0c14", "#1a1724", "#211d2d", "#2b2639", "#3a3450", "#4a4263", "#6a6088", "#8e86a8" },
                ["glow"] = new[] { "#4a1a0c", "#a8401a", "#ff8a2a", "#ffd27a", "#fff4d0" },
                ["reach"] = 118,
                ["seed"] = 12,
            },
            (o, e) => new Quake(o, e))
        {
            Title = "Quake",
            Desc = "The ground jolts three times: a rift tears out along the floor glowing deep inside, slabs of floor heave up and drop back askew, pebbles hop and dust bursts out of the split.",
            Tags = new[] { "nature", "earth", "quake", "rift", "aoe", "ground", "one-shot" },
            Bg = "ground",
        };

        static readonly double[] JOLTS = { 0.06, 0.5, 0.92 };
        const double CLOSE = 2.2;

        sealed class Slab
        {
            public double x, y, vy, a, va, s;
            public int side;
            public bool rest;
            public double[][] pts;
        }

        sealed class Rock
        {
            public double[][] pts;
            public bool dusted;
        }

        readonly uint[] S, G;
        readonly Rng R;
        readonly double REACH;
        readonly uint DUST, DUST2;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly List<double[]> rift = new List<double[]>();
        readonly List<Slab> slabs = new List<Slab>();
        readonly Dictionary<Bits.Bit, Rock> rocks = new Dictionary<Bits.Bit, Rock>();
        double t;
        int j;

        Quake(Options o, Emitter e)
        {
            S = Fit(o.Ramp("palette"), 8);
            G = Fit(o.Ramp("glow"), 5);
            R = new Rng(o.Seed);
            REACH = o.Num("reach");
            DUST = Mix(S[6], Hex("#8c8494"), 0.4);
            DUST2 = Mix(S[5], Hex("#6c6474"), 0.35);
            ps = new Particles(R, ground: x => 0, sparks: new[] { G[4], G[3], G[2], G[1] }, emitter: e);
            bits = new Bits(x => 0, e);
            lights = new Lights(e);
            // the rift: a jagged line out both ways, its depth into the floor at each step
            for (double x = -REACH; x <= REACH; x += R.Range(2.5, 5)) rift.Add(new[] { x, (double)R.Int(0, 2) });
            for (double x = -REACH + 8; x < REACH - 6; x += R.Range(12, 17))
            {
                double sx = x + R.Range(-2, 2), s = R.Range(4.5, 6);
                int side = R.Sign();
                slabs.Add(new Slab { x = sx, y = 0, vy = 0, a = 0, va = 0, rest = true, s = s, side = side, pts = new[] { new[] { -1, -0.38 }, new[] { 1, -0.42 }, new[] { 1.02, 0.4 }, new[] { -0.98, 0.38 } } });
            }
        }

        double Open() => Ease.OutCubic(Clamp((t - JOLTS[0]) / 0.45)) * REACH * (0.55 + 0.45 * Smooth(JOLTS[1], JOLTS[1] + 0.3, t));
        double Gap(double x) => (1 - Smooth(CLOSE, CLOSE + 0.5, t)) * (1 + 2.6 * (1 - Math.Abs(x) / REACH)) * (0.6 + 0.4 * Smooth(JOLTS[1], JOLTS[2] + 0.2, t));
        double Heat() => Smooth(0, 0.3, t) * (1 - Smooth(1.4, CLOSE + 0.3, t)) * (0.8 + 0.2 * Px.Sin(t * 17));

        double Shake()
        {
            double s = 0;
            for (int i = 0; i < JOLTS.Length; i++)
            {
                double tj = JOLTS[i];
                s = t > tj ? s + Exp(-(t - tj) * 9) * Px.Sin((t - tj) * 80) * (1.6 - i * 0.3) : s;
            }
            return s;
        }

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            while (j < JOLTS.Length && t >= JOLTS[j])
            {
                double k = 1 - j * 0.2, L = Open() + 10;
                j++;
                lights.Add(0, -2, 60, G[2], 0.5 * k, 0.5);
                lights.Add(0, -2, 26, WHITE, 0.4 * k, 0.07);
                // the floor heaves: slabs along the split jump and twist, pebbles and stones hop, dust bursts out
                foreach (Slab s in slabs)
                {
                    double d = Math.Abs(s.x) / REACH;
                    if (Math.Abs(s.x) > L || R.Chance(0.25 + d * 0.3)) continue;
                    s.rest = false;
                    s.vy = -R.Range(70, 170) * k * (1 - d * 0.5);
                    s.va = s.side * R.Range(3, 8);
                }
                for (int i = 0; i < 70 * k; i++)
                {
                    double x = R.Range(-L, L), a = -Math.PI / 2 + R.Range(-0.6, 0.6), sp = R.Range(40, 200) * k;
                    ps.Spawn(P.Grit, x, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.6, 1.2), R.Pick(new[] { S[7], S[6], S[5], S[4] }), R.Chance(0.35) ? 2 : 1);
                }
                for (int i = 0; i < 8 * k; i++)
                {
                    double x = R.Range(-L, L) * 0.8, a = -Math.PI / 2 + R.Range(-0.9, 0.9), sp = R.Range(100, 230) * k;
                    var b = new Bits.Bit { kind = B.Chunk, x = x, y = -2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 1, drag = 0.5, bounce = 0.3 };
                    b.life = Math.Min(R.Range(1.1, 1.6), DURATION - 0.1 - t);
                    b.a = R.Range(0, TAU);
                    b.va = R.Range(-14, 14);
                    b.s = R.Range(1.6, 3.2);
                    var rk = new Rock { pts = NatureLib.StoneShape(R) };
                    bits.Add(b);
                    rocks[b] = rk;
                }
                for (int i = 0; i < 14 * k; i++)
                {
                    double x = R.Range(-L, L) * 0.9;
                    ps.Spawn(P.Smoke, x, -R.Range(1, 4), R.Range(-40, 40), -R.Range(15, 55) * k, R.Range(1, 1.7), R.Chance(0.5) ? DUST : DUST2, R.Range(4, 7) * k, R.Range(0, 0.15), PF.Big);
                }
                for (int i = 0; i < 10 * k; i++)
                {
                    double x = R.Range(-L, L) * 0.7, a = -Math.PI / 2 + R.Range(-1, 1), sp = R.Range(120, 300);
                    ps.Spawn(P.Spark, x, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.12, 0.3));
                }
            }
            // between jolts the split keeps breathing grit and wisps of dust, embers float up out of it
            if (t > JOLTS[0] && t < CLOSE && R.Chance(0.3 * f))
            {
                double x = R.Range(-1, 1) * Open();
                ps.Spawn(P.Grit, x, -1, R.Range(-20, 20), -R.Range(20, 70), R.Range(0.5, 0.8), R.Pick(new[] { S[6], S[5] }), 1);
                if (R.Chance(0.3)) ps.Spawn(P.Ember, x, -1, R.Range(-10, 10), -R.Range(20, 45), Math.Min(R.Range(0.6, 1.2), DURATION - 0.1 - t), G[3], 1);
                if (R.Chance(0.25)) ps.Spawn(P.Dust, x, -1, R.Range(-15, 15), -R.Range(4, 14), R.Range(0.5, 0.8), DUST2, R.Range(2, 3.5));
            }
            foreach (Slab s in slabs)
            {
                if (s.rest) continue;
                s.vy += 950 * dt;
                s.y += s.vy * dt;
                s.a += s.va * dt;
                if (s.y >= 0 && s.vy > 0)
                {
                    s.y = 0;
                    s.a = Clamp(s.a, -0.45, 0.45);
                    if (s.vy > 160)
                    {
                        s.vy *= -0.25;
                        s.va *= -0.4;
                    }
                    else
                    {
                        s.rest = true;
                        for (int i = 0; i < 3; i++) ps.Spawn(P.Dust, s.x + R.Range(-5, 5), -0.5, R.Sign() * R.Range(10, 40), -R.Range(2, 12), R.Range(0.4, 0.7), DUST2, R.Range(1.5, 3));
                    }
                }
            }
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
            {
                Rock rk = rocks[b];
                if (b.rest && !rk.dusted)
                {
                    rk.dusted = true;
                    ps.Spawn(P.Dust, b.x, -0.5, R.Sign() * R.Range(8, 30), -R.Range(2, 10), Math.Min(R.Range(0.3, 0.6), DURATION - 0.1 - t), DUST2, R.Range(1.3, 2.4));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the rift: lit along its upper lip, dark inside with a deep glow, opening out from the middle
            double L = Open(), hk = Heat(), jx = Shake(), fade = 1 - Smooth(CLOSE + 0.3, CLOSE + 0.7, t);
            if (t > JOLTS[0])
                for (int i = 1; i < rift.Count; i++)
                {
                    double x0 = rift[i - 1][0], d0 = rift[i - 1][1], x1 = rift[i][0], d1 = rift[i][1];
                    if (Math.Abs(x0) > L) continue;
                    for (double x = Math.Ceiling(x0); x < x1; x++)
                    {
                        double u = (x - x0) / (x1 - x0), d = Round(d0 + (d1 - d0) * u), w = Gap(x), X = x + jx;
                        if (w < 0.4)
                        {
                            p.Dot(X, d, S[1], fade);
                            continue;
                        }
                        p.Dot(X, d - 1, S[6], fade);
                        double deep = Rand(Math.Floor(x / 3), 41) + (w - 1.5) * 0.35, hot = hk * Clamp(deep - 0.35, 0, 1) * 1.6;
                        for (double y = 0; y < w; y++) p.Dot(X, d + y, y > w / 2 - 1 && hot > 0.25 ? (hot > 0.9 && Rand(x, Math.Floor(t * 12)) < 0.6 ? G[3] : G[2]) : y > w / 2 - 1 && hot > 0.08 ? G[1] : S[0], fade);
                        p.Dot(X, d + Math.Ceiling(w), S[2], fade);
                    }
                    if (i % 3 == 0 && hk > 0) p.Glow(x1 + jx, d1 + 1, 9, G[2], 0.35 * hk * (1 - Math.Abs(x1) / (REACH * 1.2)));
                }
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            // slabs of floor heaved up out of it, tilted, sinking back as it closes
            double jx = Shake(), sink = Smooth(CLOSE, CLOSE + 0.6, t);
            foreach (Slab s in slabs)
                if (!(s.rest && s.a == 0 && s.y == 0) && sink < 1)
                    NatureLib.DrawRock(p, s.x + jx, s.y - 1.5 + sink * 4, s.s, s.a, s.pts, S, 1 - sink);
            foreach (Bits.Bit b in bits.List) NatureLib.DrawRock(p, b.x, b.y, b.s, b.a, rocks[b].pts, S, Math.Min(1, b.life / 0.4));
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
