// The creeping front, the branching cracks and the burst of the body into chunks, grit and dust are
// adapted from Sprite Fusion's open-source ice freeze and shatter (https://www.spritefusion.com/pixel-art-vfx/ice),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.StatusLib;

namespace Pixelfx
{
    public sealed class Petrified : Effect
    {
        static readonly Pose REACH_ = new Pose { ["lean"] = 3, ["crouch"] = 2, ["fx"] = 14, ["fy"] = -29, ["bx"] = -4, ["by"] = -22, ["ffx"] = 8, ["fbx"] = -7, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.16, Poses.READY),
                new PoseKey(0.08, new Pose { ["turn"] = 0.9, ["lean"] = -1, ["crouch"] = 2, ["fx"] = -8, ["fy"] = -24, ["bx"] = 9, ["by"] = -24 }, "snap"),
                new PoseKey(0.5, REACH_), new PoseKey(0.95, REACH_), new PoseKey(3.8, REACH_),
            },
            Hits = new[] { new HitCue { T = 2.2, Stop = 0.08, Shake = 0.24, Amp = 2 } },
        };

        const double CREEP = 0.95, CRACK = 1.75, SHATTER = 2.2;

        public static readonly Module Module = new Module("petrified", "pixelfx/fx/status/petrified", 120, 74, 60, 64, 3.8, false, 30,
            new Options { ["palette"] = new[] { "#16141c", "#2a2733", "#433f4d", "#5f5a6a", "#7d7889", "#a29dab", "#cdc8d2", "#f0edf3" }, ["seed"] = 3 },
            (o, e) => new Petrified(o, e), act: Act, actor: 2,
            // the stand-in character is gone once the stone covers it (the statue is drawn in its place)
            actorAlpha: (t, o) => t < CREEP ? 1 : 0)
        {
            Title = "Petrified",
            Desc = "Turned to stone: grey creeps up from the feet behind a crackling edge that spits grit, the statue holds, cracks run through it, and it bursts into tumbling chunks and dust.",
            Tags = new[] { "status", "stone", "petrify", "petrified", "debuff", "death", "one-shot" },
            Bg = "ground",
        };

        // how far up the stone has crept (0 at the feet, past 1 over the head), stuttering as it climbs
        static double Level(double t) => t >= CREEP ? 1.2 : 1.2 * Smooth(0, CREEP, t) + 0.025 * Px.Sin((t / CREEP) * TAU * 3);

        sealed class Stone
        {
            public int x, y;
            public double up;
            public uint c;
        }

        readonly uint[] C;
        readonly Actor a;
        readonly StatusShape S;
        readonly int H;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly double seed;
        readonly List<Stone> stone = new List<Stone>();
        readonly List<double[][]> cracks = new List<double[][]>();
        readonly int ox, oy;
        readonly HashSet<Bits.Bit> dusted = new HashSet<Bits.Bit>();
        double t;
        bool broke, settled;

        double Front(double mx) => Level(t) + 0.06 * TileFbm(mx * 0.4, 0.5, 0, 0, seed, 2);

        Petrified(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            a = o.Actor;
            S = Shape(a);
            H = S.H;
            seed = o.Seed;
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[4] }, emitter: e);
            bits = new Bits(ground: x => 0, emitter: e);
            lights = new Lights(e);
            // each pixel's stone: its own shading walked down a grey ramp, grained, lit on the top left
            foreach (int[] b in S.Body)
            {
                int x = b[0], y = b[1];
                double tone = Clamp((Luma(ColorAt(a, x, y)) - 0.06) / 0.78), mottle = TileFbm(x * 0.32, y * 0.32, 0, 0, o.Seed + 5, 2);
                double i = 1 + Round(tone * 4.6 + mottle * 1.1);
                if (!S.On(x - 1, y) || !S.On(x, y - 1)) i += 1;
                else if (!S.On(x + 1, y) || !S.On(x, y + 1)) i -= 1;
                stone.Add(new Stone { x = x, y = y, up = 1 - (y + 0.5) / H, c = C[(int)Clamp(i, 1, 6)] });
            }
            // cracks: random walks through the statue from a point on its chest
            ox = RoundInt(S.W * 0.55);
            oy = RoundInt(H * 0.42);
            for (int i = 0; i < 7; i++) Walk(ox, oy, (i / 7.0) * TAU + R.Range(-0.3, 0.3), 14);
        }

        void Walk(double x, double y, double ang, double len)
        {
            var pts = new List<double[]> { new[] { x, y } };
            for (int s = 0; s < len; s++)
            {
                ang += R.Range(-0.55, 0.55);
                x += Px.Cos(ang) * R.Range(1.4, 2.4);
                y += Px.Sin(ang) * R.Range(1.4, 2.4);
                if (!S.On(Math.Floor(x), Math.Floor(y))) break;
                pts.Add(new[] { x, y });
                if (pts.Count > 3 && cracks.Count < 16 && R.Chance(0.14)) Walk(x, y, ang + R.Sign() * R.Range(0.6, 1.2), len / 2);
            }
            cracks.Add(pts.ToArray());
        }

        public override void Update(double dt)
        {
            t += dt;
            // the creeping edge spits grit and dust and the odd spark
            if (t < CREEP)
            {
                double rate = 60 * dt;
                for (double n = 0; n < rate; n++)
                {
                    if (!R.Chance(0.5)) continue;
                    int[] b = S.Body[R.Int(0, S.Body.Count - 1)];
                    int mx = b[0], my = b[1];
                    if (Math.Abs(1 - (my + 0.5) / H - Front(mx)) > 0.04) continue;
                    double x = a.X + mx + 0.5, y = a.Y + my, side = mx < S.W / 2.0 ? -1 : 1;
                    ps.Spawn(P.Grit, x, y, side * R.Range(15, 60), -R.Range(30, 90), R.Range(0.6, 1.1), R.Pick(new[] { C[4], C[5], C[6] }), R.Chance(0.3) ? 2 : 1);
                    if (R.Chance(0.3)) ps.Spawn(P.Dust, x + side * 2, y, side * R.Range(10, 30), -R.Range(4, 14), R.Range(0.4, 0.7), C[4], R.Range(1.5, 2.6));
                    if (R.Chance(0.25)) ps.Spawn(P.Mote, x, y, side * R.Range(40, 90), -R.Range(20, 70), R.Range(0.15, 0.3), C[7], 0.5);
                }
            }
            if (!settled && t >= CREEP)
            {
                settled = true;
                lights.Add(0, a.Y + H * 0.5, 34, C[6], 0.35, 0.3);
                for (int i = 0; i < 8; i++) ps.Spawn(P.Dust, R.Range(-8, 8), -R.Range(0, 2), R.Sign() * R.Range(20, 50), -R.Range(2, 10), R.Range(0.6, 1), C[3], R.Range(2, 3.5));
            }
            // while it holds a pebble or two drops off
            if (t > CREEP + 0.2 && t < SHATTER && R.Chance(1.6 * dt))
            {
                int[] b = S.Body[R.Int(0, S.Body.Count - 1)];
                int mx = b[0], my = b[1];
                if (!S.On(mx, my + 1)) ps.Spawn(P.Grit, a.X + mx + 0.5, a.Y + my + 1, R.Range(-8, 8), 0, R.Range(0.8, 1.2), C[3], 1);
            }
            // the cracks pop chips off as they run
            if (t >= CRACK && t < SHATTER && R.Chance(12 * dt))
            {
                double[][] pts = R.Pick(cracks.ToArray());
                double[] xy = pts[(int)Math.Floor(R.Next() * pts.Length)];
                ps.Spawn(P.Grit, a.X + xy[0], a.Y + xy[1], R.Range(-40, 40), -R.Range(30, 80), R.Range(0.5, 0.9), R.Pick(new[] { C[5], C[6] }), 1);
            }
            if (!broke && t >= SHATTER)
            {
                broke = true;
                lights.Add(0, a.Y + H * 0.45, 46, C[6], 0.7, 0.5);
                lights.Add(0, a.Y + H * 0.45, 30, WHITE, 0.7, 0.12);
                // the statue bursts: chunks tumble out, grit sprays, dust rolls along the floor
                double cx = a.X + ox, cy = a.Y + oy;
                for (int i = 0; i < 18; i++)
                {
                    Stone q = stone[R.Int(0, stone.Count - 1)];
                    double x = a.X + q.x + 0.5, y = a.Y + q.y + 0.5, dx = x - cx, dy = y - cy, d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    double sp = R.Range(35, 110);
                    var bit = new Bits.Bit { kind = B.Chunk, x = x, y = y, vx = (dx / d) * sp };
                    bit.vy = (dy / d) * sp * 0.6 - R.Range(60, 160);
                    bit.g = 1;
                    bit.drag = 0.4;
                    bit.bounce = 0.3;
                    bit.life = R.Range(1.1, 1.5);
                    bit.a = R.Range(0, TAU);
                    bit.va = R.Range(-14, 14);
                    bit.s = R.Range(1.3, 2.8);
                    bit.ramp = new[] { C[0], C[2], C[3], C[5], C[6] };
                    bits.Add(bit);
                }
                foreach (Stone q in stone)
                {
                    if (Rand(q.x, q.y, 9) > 0.22) continue;
                    double x = a.X + q.x + 0.5, y = a.Y + q.y + 0.5, dx = x - cx, dy = y - cy, d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    double sp = R.Range(40, 130);
                    ps.Spawn(P.Grit, x, y, (dx / d) * sp, (dy / d) * sp * 0.6 - R.Range(40, 140), R.Range(0.9, 1.4), q.c, R.Chance(0.35) ? 2 : 1);
                }
                // pale flakes of the skin of it, tumbling, and glints in the dust
                for (int i = 0; i < 16; i++)
                {
                    double an = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(50, 120);
                    var bit = new Bits.Bit { kind = B.Shard };
                    bit.x = cx + R.Range(-5, 5);
                    bit.y = cy + R.Range(-8, 8);
                    bit.vx = Px.Cos(an) * sp;
                    bit.vy = Px.Sin(an) * sp;
                    bit.g = 0.8;
                    bit.drag = 0.6;
                    bit.bounce = 0.3;
                    bit.life = R.Range(0.9, 1.4);
                    bit.a = R.Range(0, TAU);
                    bit.va = R.Range(-18, 18);
                    bit.s = R.Range(1.5, 3);
                    bit.c = R.Pick(new[] { C[5], C[6], C[7] });
                    bits.Add(bit);
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Star, cx + R.Range(-12, 12), cy + R.Range(-10, 14), R.Range(-10, 10), -R.Range(5, 20), R.Range(0.4, 0.9), C[6], R.Range(1, 2.2), R.Range(0.05, 0.4));
                for (int i = 0; i < 12; i++)
                {
                    int side = R.Sign();
                    ps.Spawn(P.Smoke, side * R.Range(0, 8), -R.Range(1, 6), side * R.Range(20, 60), -R.Range(4, 16), R.Range(0.9, 1.4), C[4], R.Range(2.4, 4), R.Range(0, 0.08), 0);
                }
                for (int i = 0; i < 6; i++) ps.Spawn(P.Smoke, R.Range(-6, 6), a.Y + H * R.Range(0.3, 0.8), R.Range(-20, 20), -R.Range(10, 30), R.Range(1, 1.5), C[3], R.Range(3, 4.5), R.Range(0, 0.1), 0);
            }
            bits.Update(dt);
            // chunks that hit hard kick up a little dust
            foreach (Bits.Bit b in bits.List)
                if (b.rest && !dusted.Contains(b))
                {
                    dusted.Add(b);
                    ps.Spawn(P.Dust, b.x, -0.5, R.Range(-20, 20), -R.Range(2, 10), R.Range(0.3, 0.6), C[3], R.Range(1.2, 2.2));
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p) => ps.Draw(p, Layer.Back);

        public override void Draw(Painter p)
        {
            if (t < SHATTER)
            {
                // the shudder before it breaks
                double sh = t >= CRACK ? Round(Px.Sin(t * 90) * Math.Min(1, (t - CRACK) / (SHATTER - CRACK)) * 1.2) : 0;
                bool white = t > SHATTER - 0.035;
                foreach (Stone q in stone)
                {
                    double f = Front(q.x), d = f - q.up;
                    if (d < 0) continue;
                    double x = a.X + q.x + sh, y = a.Y + q.y;
                    // the crackling edge: bright, flickering
                    if (d < 0.035 && t < CREEP) p.Px(x, y, Rand(q.x, q.y, Math.Floor(t * 24)) < 0.5 ? C[7] : C[6]);
                    else if (d < 0.07 && t < CREEP) p.Px(x, y, C[5]);
                    else p.Px(x, y, white ? WHITE : q.c);
                }
                if (t < CREEP)
                    for (int mx = 0; mx < S.W; mx += 2)
                    {
                        double my = Math.Floor((1 - Front(mx)) * H);
                        if (S.On(mx, my)) p.Glow(a.X + mx, a.Y + my, 5, C[6], 0.18);
                    }
                // the cracks running through it, fresh tips white
                if (t >= CRACK)
                {
                    double k = Clamp((t - CRACK) / 0.3);
                    foreach (double[][] pts in cracks)
                        for (int i = 0, n = (int)Math.Floor(pts.Length * k); i < n; i++)
                        {
                            double x = pts[i][0], y = pts[i][1], px = Math.Floor(a.X + x) + sh, py = Math.Floor(a.Y + y);
                            bool fresh = i > n - 3;
                            p.Px(px, py, fresh ? WHITE : C[0]);
                            if (!fresh) p.Px(px, py - 1, C[6]);
                        }
                    p.Glow(a.X + ox, a.Y + oy, 12, C[6], 0.25 * k);
                }
            }
            bits.Draw(p);
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
