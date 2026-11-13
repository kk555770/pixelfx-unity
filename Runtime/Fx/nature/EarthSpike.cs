// The eruption (a spike punching up with overshoot and a white frame, a burst of debris, mist and glints, then
// breaking into tumbling pieces) is adapted from Sprite Fusion's open-source ice spikes, and the grit and
// rolling dust from his explosion (https://www.spritefusion.com/pixel-art-vfx/ice), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class EarthSpike : Effect
    {
        const double DURATION = 3.4;

        public static readonly Module Module = new Module("earth-spike", "pixelfx/fx/nature/earth-spike", 272, 150, 136, 132, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#120c0c", "#251917", "#3b2a24", "#553d31", "#715440", "#8f6e52", "#b8966e", "#e6d0a8" },
                ["glow"] = new[] { "#4a1a0c", "#a8401a", "#ff8a2a", "#ffd27a", "#fff4d0" },
                ["height"] = 62,
                ["seed"] = 5,
            },
            (o, e) => new EarthSpike(o, e))
        {
            Title = "Earth spike",
            Desc = "The floor cracks with light and heaves, a faceted stone spike rams up with two side shards in a burst of chips, grit and dust; rubble rains, then it splits and crumbles.",
            Tags = new[] { "nature", "earth", "stone", "spike", "ground", "attack", "one-shot" },
            Bg = "ground",
        };

        const double UP = 0.34, RISE = 0.09, CRACK = 1.18, SPLIT = 1.5, PART = 0.1, FALL = 0.42, SINK = 0.55;

        sealed class Shard
        {
            public double x, H, w, lean, at, split, cut, top;
            public bool back, main, up, split0;
            public NatureLib.SpikeLook look;
            public List<List<double[]>> cracks;
        }

        sealed class Slab
        {
            public int sd;
            public double x, s, a;
            public double[][] pts;
        }

        sealed class Rock
        {
            public double[][] pts;
            public bool dusted;
        }

        readonly uint[] S, G;
        readonly Rng R;
        readonly double H;
        readonly uint DUST, DUST2;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly Shard[] spikes;
        readonly List<double[]>[] cracks;
        readonly Slab[] slabs;
        readonly Dictionary<Bits.Bit, Rock> rocks = new Dictionary<Bits.Bit, Rock>();
        double t;

        EarthSpike(Options o, Emitter e)
        {
            S = Fit(o.Ramp("palette"), 8);
            G = Fit(o.Ramp("glow"), 5);
            R = new Rng(o.Seed);
            H = o.Num("height");
            DUST = Mix(S[6], Hex("#8c8494"), 0.45);
            DUST2 = Mix(S[5], Hex("#6c6474"), 0.4);
            ps = new Particles(R, ground: x => 0, sparks: new[] { G[4], G[3], G[2], G[1] }, emitter: e);
            bits = new Bits(x => 0, e);
            lights = new Lights(e);
            // the spike and two side shards leaning away from it, each with its own chips, strata and cracks
            spikes = new Shard[3];
            spikes[0] = new Shard { x = -H * 0.17, H = H * 0.52, w = H * 0.22, lean = -H * R.Range(0.13, 0.18), at = UP + 0.045, split = SPLIT - 0.1, back = true };
            spikes[1] = new Shard { x = H * 0.18, H = H * 0.4, w = H * 0.2, lean = H * R.Range(0.11, 0.15), at = UP + 0.085, split = SPLIT + 0.08, back = false };
            spikes[2] = new Shard { x = 0, H = H, w = H * 0.36, lean = R.Range(-2, 2), at = UP, split = SPLIT, back = false, main = true };
            foreach (Shard s in spikes)
            {
                s.look = NatureLib.MakeSpikeLook(R);
                s.cracks = new List<List<double[]>>
                {
                    NatureLib.SpikeCrack(R, R.Range(0.04, 0.16), R.Range(-0.6, -0.1), 1),
                    NatureLib.SpikeCrack(R, R.Range(0.22, 0.4), R.Range(0.1, 0.6), -1),
                    NatureLib.SpikeCrack(R, R.Range(0.42, 0.58), R.Range(-0.4, 0.4), R.Sign()),
                };
                s.up = s.split0 = false;
                s.cut = R.Range(0.24, 0.32);
                s.top = 1;
            }
            cracks = new[]
            {
                NatureLib.FloorCrack(R, -1, R.Range(34, 44)), NatureLib.FloorCrack(R, 1, R.Range(34, 44)),
                NatureLib.FloorCrack(R, -1, R.Range(14, 20)), NatureLib.FloorCrack(R, 1, R.Range(14, 20)),
            };
            slabs = new Slab[2];
            int si = 0;
            foreach (int sd in new[] { -1, 1 })
            {
                double x = sd * R.Range(13, 16);
                var pts = new[] { new[] { -1.2, -0.32 }, new[] { 1.1, -0.42 }, new[] { 1.2, 0.36 }, new[] { -1.1, 0.4 } };
                double s = R.Range(5.5, 7), a = sd * R.Range(0.45, 0.65);
                slabs[si++] = new Slab { sd = sd, x = x, pts = pts, s = s, a = a };
            }
        }

        void AddRock(Bits.Bit b)
        {
            var r = new Rock { pts = NatureLib.StoneShape(R) };
            bits.Add(b);
            rocks[b] = r;
        }

        double Grown(Shard s) => t < s.at ? 0 : Ease.OutBack(Clamp((t - s.at) / RISE), 2.2);
        double Open() => Ease.OutCubic(Clamp((t - 0.02) / 0.24));
        double Heat() => t < UP ? Smooth(0, UP, t) : 1 - Smooth(UP, UP + 0.9, t);
        // after the split the halves part, tip outwards and come down from the top as rubble
        double Gap(Shard s) => Smooth(s.split, s.split + PART, t) * (s.main ? 3 : 2);
        double Tip(Shard s) => Smooth(s.split, s.split + PART + FALL, t) * 0.12 * s.H;
        double TopAt(Shard s) => Lerp(1, s.cut, Ease.InQuad(Clamp((t - s.split - 0.04) / FALL)));

        void Erupt(Shard s)
        {
            double k = s.H / H;
            lights.Add(s.x, -s.H * 0.4, 30 * k + 14, WHITE, 0.5 * k + 0.1, 0.1);
            lights.Add(s.x, -10, 60 * k + 20, G[2], 0.34 * k, 0.5);
            for (int i = 0; i < Round(16 * k) + 3; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.1, 1.1), sp = R.Range(140, 360) * (0.6 + 0.4 * k);
                var b = new Bits.Bit { kind = B.Chunk };
                b.x = s.x + R.Range(-s.w / 3, s.w / 3);
                b.y = -R.Range(1, 6);
                b.vx = Px.Cos(a) * sp;
                b.vy = Px.Sin(a) * sp;
                b.g = 1;
                b.drag = 0.35;
                b.bounce = 0.32;
                b.life = R.Range(1.4, 2.4);
                b.a = R.Range(0, TAU);
                b.va = R.Range(-14, 14);
                b.s = R.Range(1.6, 3.8) * (0.6 + 0.4 * k);
                AddRock(b);
            }
            for (int i = 0; i < Round(55 * k) + 10; i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.9, sp = R.Range(90, 330) * (1 - 0.35 * Math.Abs(u));
                ps.Spawn(P.Grit, s.x + u * s.w * 0.4, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1, 2), R.Pick(new[] { S[6], S[5], S[4], S[4], S[3] }), R.Chance(0.4) ? 2 : 1);
            }
            for (int i = 0; i < Round(9 * k) + 2; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(160, 380);
                ps.Spawn(P.Spark, s.x, -R.Range(2, 10), Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.15, 0.4));
            }
            // dust: a low skirt rolling out both ways, a few puffs hanging round the base
            for (int i = 0; i < Round(10 * k) + 3; i++)
            {
                int sd = R.Sign();
                ps.Spawn(P.Smoke, s.x + sd * R.Range(2, 10), -R.Range(1, 4), sd * R.Range(50, 170) * (0.5 + 0.5 * k), -R.Range(3, 16), R.Range(0.9, 1.6), R.Chance(0.5) ? DUST : DUST2, R.Range(3, 5.5) * (0.6 + 0.4 * k), R.Range(0, 0.05), PF.Big);
            }
            for (int i = 0; i < Round(14 * k) + 4; i++)
            {
                int sd = R.Sign();
                ps.Spawn(P.Dust, s.x + sd * R.Range(0, 8), -R.Range(0, 3), sd * R.Range(40, 160), -R.Range(2, 20), R.Range(0.5, 1), R.Chance(0.5) ? DUST : DUST2, R.Range(1.5, 3.2));
            }
            for (int i = 0; i < Round(3 * k) + 1; i++)
                ps.Spawn(P.Smoke, s.x + R.Range(-6, 6), -R.Range(2, 12), R.Range(-20, 20), -R.Range(15, 45), R.Range(1, 1.6), DUST, R.Range(4, 6) * k + 2, R.Range(0.02, 0.12), PF.Big);
            if (s.main)
            {
                for (int i = 0; i < 3; i++) ps.Spawn(P.Star, s.x + R.Range(-5, 5), -R.Range(10, s.H * 0.9), 0, -3, R.Range(0.25, 0.5), S[7], R.Range(1, 2.2), R.Range(0.1, 0.4));
                // rubble that went up high comes raining back down
                for (int i = 0; i < 14; i++) ps.Spawn(P.Grit, R.Range(-60, 60), -R.Range(110, 125), R.Range(-15, 15), R.Range(10, 60), R.Range(1.2, 1.8), R.Pick(new[] { S[5], S[4], S[3] }), R.Chance(0.35) ? 2 : 1, R.Range(0.25, 0.9));
            }
        }

        // one band of a spike, u0..u1, breaks off both halves into rubble that drops and tumbles
        void Crumble(Shard s, double u0, double u1)
        {
            double g = Gap(s), lean = s.lean, tp = Tip(s);
            foreach (int half in new[] { -1, 1 })
            {
                double u = (u0 + u1) / 2, hw = (s.w / 2) * Px.Pow(1 - u, 0.85), x = s.x + lean * u + half * (g / 2 + hw * 0.45 + tp * u), y = -u * s.H;
                if (hw < 0.8 && half > 0) continue;
                var b = new Bits.Bit { kind = B.Chunk, x = x, y = y };
                b.vx = half * R.Range(8, 40) + lean * 0.6;
                b.vy = -R.Range(0, 50);
                b.g = 1;
                b.drag = 0.3;
                b.bounce = 0.3;
                b.life = Math.Min(R.Range(1.1, 1.6), DURATION - 0.15 - t);
                b.a = R.Range(0, TAU);
                b.va = R.Range(-8, 8);
                b.s = Clamp(hw * 0.62, 1.6, 4.8);
                AddRock(b);
                for (int i = 0; i < 3; i++) ps.Spawn(P.Grit, x + R.Range(-2, 2), y + R.Range(-2, 2), half * R.Range(5, 45), -R.Range(10, 70), R.Range(0.9, 1.6), R.Pick(new[] { S[6], S[5], S[4], S[3] }), R.Chance(0.35) ? 2 : 1);
            }
            if (R.Chance(0.5)) ps.Spawn(P.Dust, s.x + lean * u1 + R.Range(-4, 4), -u1 * s.H, R.Sign() * R.Range(15, 45), R.Range(20, 60), R.Range(0.35, 0.6), DUST, R.Range(2, 3.5));
        }

        void DrawShard(Painter p, Shard s)
        {
            double g = Grown(s);
            if (g <= 0) return;
            if (t < s.split)
            {
                bool white = t - s.at < 0.04 || t > s.split - 0.045;
                double crack = Smooth(CRACK + (s.main ? 0 : 0.05), s.split - 0.03, t);
                double sw = (t - s.at) * 0.9 - 0.15, glint = sw > 0 && sw % 1.2 < 0.55 ? (sw % 1.2) / 0.55 : -1;
                NatureLib.DrawSpike(p, s.x, 0, s.H * g, s.w, s.lean * Math.Min(1, g), s.look, S,
                    new NatureLib.SpikeOpts { white = white, glint = glint, cracks = s.cracks, crack = crack, heat = Smooth(CRACK, s.split, t), glowRamp = G });
                return;
            }
            // split: two halves part along the ridge and tip away, crumbling from the top; the stump sinks
            double top = TopAt(s), gp = Gap(s), tp = Tip(s), sink = Smooth(s.split + FALL, s.split + FALL + SINK, t) * s.H * (s.cut + 0.06);
            if (sink >= s.H * (s.cut + 0.05)) return;
            foreach (int half in new[] { -1, 1 })
                NatureLib.DrawSpike(p, s.x + (half * gp) / 2, sink, s.H, s.w, s.lean + half * tp, s.look, S,
                    new NatureLib.SpikeOpts { half = half, clip = top, floor = 0, cracks = s.cracks, crack = 1, heat = 1 - Smooth(s.split, s.split + 0.45, t), glowRamp = G });
        }

        public override void Update(double dt)
        {
            t += dt;
            double f = Math.Min(1, dt * 120);
            // the floor splits open: grit hops out of the cracks as they run, dust puffs from them
            if (t < UP)
            {
                double L = Open();
                foreach (List<double[]> c in cracks)
                    if (R.Chance(0.35 * f))
                    {
                        double[] q = c[(int)Math.Min(c.Count - 1, Math.Floor(R.Range(0, L) * c.Count))];
                        ps.Spawn(P.Grit, q[0], -1, R.Range(-20, 20), -R.Range(30, 110), R.Range(0.5, 1), R.Pick(new[] { S[5], S[4] }), 1);
                        if (R.Chance(0.25)) ps.Spawn(P.Dust, q[0], -1, R.Range(-15, 15), -R.Range(4, 14), R.Range(0.3, 0.6), DUST2, R.Range(1.5, 3));
                    }
                if (t > 0.12 && R.Chance(0.2 * f)) ps.Spawn(P.Dust, R.Sign() * R.Range(10, 16), -R.Range(1, 4), R.Sign() * R.Range(10, 30), -R.Range(4, 12), R.Range(0.4, 0.7), DUST2, R.Range(2, 3.5));
            }
            foreach (Shard s in spikes)
            {
                if (!s.up && t >= s.at)
                {
                    s.up = true;
                    Erupt(s);
                }
                if (!s.split0 && t >= s.split)
                {
                    s.split0 = true;
                    lights.Add(s.x, -s.H * 0.5, 30, WHITE, 0.3, 0.08);
                    ps.Spawn(P.Star, s.x + s.lean * 0.5, -s.H * 0.55, 0, -2, 0.3, S[7], 2.5);
                }
                // crumbling from the top: each band that goes turns to rubble
                if (t >= s.split)
                {
                    double top = TopAt(s);
                    while (s.top - top > 0.045 || (top <= s.cut + 1e-6 && s.top > s.cut + 1e-6))
                    {
                        double u1 = s.top, u0 = Math.Max(top, s.top - 0.11);
                        Crumble(s, u0, u1);
                        s.top = u0;
                        if (u0 <= s.cut + 1e-6) break;
                    }
                }
                // cracking: grit and dust dribble out of the cracks
                if (t < s.split && t > CRACK && R.Chance(0.12 * f))
                {
                    double u = R.Range(0.1, 0.7);
                    ps.Spawn(P.Grit, s.x + s.lean * u + R.Range(-2, 2), -u * s.H, R.Range(-15, 15), -R.Range(0, 20), R.Range(0.6, 1.1), R.Pick(new[] { S[5], S[4] }), 1);
                    if (R.Chance(0.3)) ps.Spawn(P.Dust, s.x + s.lean * u, -u * s.H, R.Range(-10, 10), -R.Range(2, 8), R.Range(0.4, 0.7), DUST, R.Range(1.5, 2.5));
                }
                // while it stands, pebbles trickle off its flanks and light glints off its edges
                if (s.up && t < s.split && t > s.at + 0.2 && R.Chance(0.05 * f))
                {
                    double u = R.Range(0.2, 0.8);
                    int sd = R.Sign();
                    ps.Spawn(P.Grit, s.x + s.lean * u + sd * (s.w / 2) * (1 - u), -u * s.H, sd * R.Range(10, 30), -R.Range(0, 15), R.Range(0.7, 1.2), R.Pick(new[] { S[6], S[5] }), 1);
                }
                if (s.main && s.up && t < s.split && t > s.at + 0.25 && R.Chance(0.03 * f))
                    ps.Spawn(P.Star, s.x + R.Range(-4, 2), -R.Range(8, s.H * 0.85), 0, -2, R.Range(0.25, 0.45), R.Chance(0.5) ? S[7] : G[3], R.Range(0.8, 2));
                // the stump sinking pushes up a last puff of dust
                if (t > s.split + FALL && t < s.split + FALL + SINK * 0.8 && R.Chance(0.15 * f))
                    ps.Spawn(P.Dust, s.x + R.Range(-s.w / 2, s.w / 2), -R.Range(0, 2), R.Sign() * R.Range(20, 60), -R.Range(0, 5), R.Range(0.4, 0.7), DUST2, R.Range(1.5, 3));
            }
            bits.Update(dt);
            // stones that land hard kick up a little dust
            foreach (Bits.Bit b in bits.List)
            {
                Rock rk = rocks[b];
                if (b.rest && !rk.dusted)
                {
                    rk.dusted = true;
                    for (int i = 0; i < 2; i++) ps.Spawn(P.Dust, b.x + R.Range(-1.5, 1.5), -0.5, R.Sign() * R.Range(8, 35), -R.Range(2, 14), R.Range(0.3, 0.6), DUST2, R.Range(1.3, 2.6));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the cracks in the floor: a glowing seam that cools to a dark scar, then fades
            double L = Open(), hk = Heat(), fade = 1 - Smooth(DURATION - 1.1, DURATION - 0.3, t);
            foreach (List<double[]> c in cracks)
            {
                double m = Math.Max(2, Math.Ceiling(c.Count * L));
                for (int k = 1; k < m; k++)
                {
                    double x0 = c[k - 1][0], y0 = c[k - 1][1], x1 = c[k][0], y1 = c[k][1];
                    p.Line(x0, y0 + 1, x1, y1 + 1, S[0], fade);
                    p.Line(x0, y0, x1, y1, hk > 0.65 ? G[4] : hk > 0.35 ? G[3] : hk > 0.12 ? G[2] : hk > 0.03 ? G[1] : S[1], fade);
                    if (k % 2 == 0) p.Glow(x1, y1, 6, G[2], 0.3 * hk);
                }
            }
            if (hk > 0) p.Glow(0, -3, 26, G[2], 0.35 * hk);
            ps.Draw(p, Layer.Back);
            foreach (Shard s in spikes)
                if (s.back)
                    DrawShard(p, s);
        }

        public override void Draw(Painter p)
        {
            // before it breaks through, the floor heaves into a trembling mound
            if (t < UP + 0.02)
            {
                double k = Smooth(0.06, UP, t), hm = 8 * k, jx = Px.Sin(t * 95) * 0.8 * k;
                if (hm > 0.6)
                    p.Shade(-16, -hm - 1, 16, 1, (x, y, o) =>
                    {
                        double dx = (x - jx) / 15, dy = y / hm, d = dx * dx + dy * dy;
                        if (y > 0.5 || d > 1.18) return 0;
                        o.c = d > 1 ? S[0] : dy < -0.72 ? S[6] : dx < -0.3 ? S[5] : dx > 0.35 ? S[3] : S[4];
                        return 0;
                    });
            }
            // slabs of floor shoved up around the base, sinking back as the spike crumbles
            if (t >= UP)
            {
                double sink = Smooth(SPLIT + FALL, SPLIT + FALL + SINK, t) * 7, a = 1 - Smooth(SPLIT + FALL, SPLIT + FALL + SINK, t);
                foreach (Slab sl in slabs)
                {
                    double g = Ease.OutBack(Clamp((t - UP) / 0.1), 2);
                    if (a > 0) NatureLib.DrawRock(p, sl.x, -1.5 + sink, sl.s * Math.Min(1, g), sl.a * g, sl.pts, S, a, t - UP < 0.04);
                }
            }
            foreach (Shard s in spikes)
                if (!s.back)
                    DrawShard(p, s);
            Shard m0 = spikes[2];
            double leak = Smooth(CRACK, SPLIT, t) * (1 - Smooth(SPLIT, SPLIT + 0.5, t));
            if (leak > 0) p.GlowLine(m0.x, -4, m0.x + m0.lean * 0.6, -m0.H * 0.6, 20, G[2], 0.22 * leak);
            foreach (Bits.Bit b in bits.List) NatureLib.DrawRock(p, b.x, b.y, b.s, b.a, rocks[b].pts, S, Math.Min(1, b.life / 0.4));
            ps.Draw(p, Layer.Front);
            // the hero frame: a flare where the tip breaks through, light running up the spike
            Shard m = spikes[2];
            double e = t - m.at;
            if (e > 0 && e < 0.2)
            {
                double g = Ease.OutBack(Clamp(e / RISE), 2.2);
                Sim.Sparkle(p, m.x + m.lean, -m.H * g, 10 * (1 - e / 0.2), S[7], G[3], 0.5);
                p.GlowLine(m.x, 0, m.x + m.lean, -m.H * g, 10, G[3], 0.3 * (1 - e / 0.2));
            }
            lights.Draw(p);
        }
    }
}
