// The rocks, grit and dust are adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class HeavyLanding : Effect
    {
        const double Duration = 1.6;

        static readonly Pose FALL = new Pose { ["turn"] = 0.8, ["y"] = -46, ["crouch"] = -1, ["lean"] = -1, ["fx"] = -9, ["fy"] = -42, ["bx"] = 10, ["by"] = -42, ["ffx"] = 6, ["ffy"] = -8, ["fbx"] = -3, ["fby"] = -3, ["toeB"] = true, ["ghost"] = 1 };
        static readonly Pose HERO_ = new Pose { ["y"] = 0, ["crouch"] = 8, ["lean"] = 5, ["fx"] = 13, ["fy"] = -8, ["bx"] = -6, ["by"] = -23, ["ffx"] = 11, ["fbx"] = -10 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(-0.26, FALL),
                new PoseKey(-0.01, new Pose(FALL) { ["y"] = -6 }, "in"),
                new PoseKey(0, HERO_, "snap"),
                new PoseKey(0.55, new Pose(HERO_) { ["crouch"] = 7 }),
                new PoseKey(0.95, new Pose(HERO_) { ["crouch"] = 5, ["lean"] = 3, ["fx"] = 13, ["fy"] = -15, ["bx"] = -5, ["by"] = -20 }),
                new PoseKey(1.3, Poses.READY),
                new PoseKey(1.6, Poses.READY),
            },
            Hits = new[] { new HitCue { T = 0.005, Stop = 0.13, Shake = 0.32, Amp = 3 } },
        };

        public static readonly Module Module = new Module("heavy-landing", "pixelfx/fx/impacts/heavy-landing", 176, 80, 88, 60, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1c1a24", "#2f2a3a", "#4a4258", "#6b6280", "#9088a4", "#bdb6cc", "#e6e1ee" },
                ["stone"] = "#4a3f69",
                ["seed"] = 14.0,
            },
            (o, e) => new HeavyLanding(o, e), act: Act, actor: 2)
        {
            Title = "Heavy landing",
            Desc = "A character lands hard: the floor dents and cracks under their feet, two rings of dust roll out along the floor one after the other, and pebbles hop away.",
            Tags = new[] { "impact", "landing", "ground", "stomp", "dust", "rocks", "actor", "one-shot" },
            Bg = "ground",
        };

        static readonly uint[] Thud = { WHITE, WHITE, WHITE, WHITE };

        sealed class Rock
        {
            public double[] pts;
            public double pvy;
        }

        readonly uint[] C;
        readonly ImpactsLib.Stone st;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles back, front;
        readonly Bits bits;
        readonly Dictionary<Bits.Bit, Rock> rocks = new Dictionary<Bits.Bit, Rock>();
        readonly ImpactsLib.Crack[] cracks;
        readonly uint DUST, LIT;
        readonly double PH;
        double t;

        HeavyLanding(Options o, Emitter e)
        {
            emitter = e;
            C = Fit(o.Ramp("palette"), 7);
            st = new ImpactsLib.Stone(o.Color("stone"));
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            back = new Particles(rng: R, ground: floor, emitter: e);
            front = new Particles(rng: R, ground: floor, emitter: e);
            bits = new Bits(floor, e);
            cracks = ImpactsLib.MakeCracks(R, 6, 8, 20, 0.7, 12, 0.4, 2, 6);
            DUST = Mix(st.dust, C[5], 0.4);
            LIT = Mix(DUST, WHITE, 0.3);
            PH = R.Range(0, TAU);
            Ring(20, 140, 190, 2.2, 3.4, 0.01);
            Ring(18, 70, 110, 3.4, 5.4, 0.12);
            for (int i = 0; i < 12; i++)
            {
                int s = R.Sign();
                bool up = i < 6;
                (i % 2 != 0 ? front : back).Spawn(P.Dust, s * R.Range(2, 10), -R.Range(1, 4), s * R.Range(10, 35) * (up ? 0.6 : 1), -(up ? R.Range(25, 60) : R.Range(4, 12)),
                    R.Range(0.55, 0.9), R.Chance(0.5) ? LIT : DUST, R.Range(3, 5), R.Range(0.02, 0.1));
            }
            for (int i = 0; i < 12; i++)
            {
                int s = R.Sign();
                double v = R.Range(50, 130);
                var b = new Bits.Bit
                {
                    kind = B.Chunk, x = s * R.Range(3, 8), y = -1, vx = s * v, vy = -R.Range(140, 230), g = 1, drag = 0.5, bounce = 0.55,
                    life = R.Range(1, 1.45), a = R.Range(0, TAU), va = s * R.Range(8, 16), s = R.Chance(0.3) ? 2 : 1,
                };
                rocks[b] = new Rock { pts = ImpactsLib.RockShape(R) };
                bits.Add(b);
            }
            for (int i = 0; i < 24; i++)
            {
                int s = R.Sign();
                double a = -Math.PI / 2 + s * R.Range(0.3, 1.2), v = R.Range(60, 170);
                front.Spawn(P.Grit, s * R.Range(2, 7), -1, Px.Cos(a) * v, Px.Sin(a) * v, R.Range(0.6, 1.1), R.Pick(new[] { st.light, st.body, st.lip }), R.Chance(0.3) ? 2 : 1);
            }
        }

        void Ring(int n, double v0, double v1, double size0, double size1, double delay)
        {
            for (int i = 0; i < n; i++)
            {
                double g = (double)i / n * TAU + R.Range(-0.1, 0.1), v = R.Range(v0, v1);
                bool far = Px.Sin(g) < 0;
                (far ? back : front).Spawn(P.Dust, Px.Cos(g) * 4, Px.Sin(g) * 0.7 - 1, Px.Cos(g) * v, Px.Sin(g) * v * 0.16 - R.Range(2, 10), R.Range(0.45, 0.8),
                    R.Chance(0.45) ? LIT : DUST, R.Range(size0, size1), delay + R.Range(0, 0.03));
            }
        }

        static readonly (double t0, double dur, double r1, double w, double k0, bool edge)[] DustRings = { (0.01, 0.45, 58, 4.5, 0.75, true), (0.12, 0.85, 42, 6, 0.8, false) };

        void Rings(Painter p, bool far)
        {
            foreach (var (t0, dur, r1, w, k0, edge) in DustRings)
            {
                double g = (t - t0) / dur;
                if (g <= 0 || g >= 1) continue;
                double r = 6 + r1 * Ease.OutCubic(g);
                ImpactsLib.DustRing(p, 0, 0, r, w * (1 + g), k0 * Px.Pow(1 - g, 1.2) * Math.Min(1, g / 0.06), DUST, sy: 0.2, ph: PH + t0 * 10, brk: g * 0.8, half: far ? -1 : 1);
                if (edge && g < 0.75)
                    Sim.Band(p, 0, 0.5, r + 2, 0.2, Math.Max(1, 2.5 * (1 - g)), 1.1 - g / 0.75, new[] { st.body, st.light, LIT, WHITE }, a => Px.Sin(a) < 0 == far);
            }
        }

        void Dent(Painter p, double k)
        {
            for (int x = -10; x <= 9; x++)
            {
                double q = Math.Abs(x + 0.5) / 10, dep = Round(1.8 * (1 - q * q));
                if (q < 0.82)
                {
                    for (int y = 0; y < dep; y++) p.Dot(x, y, ImpactsLib.SOOT, k);
                    p.Dot(x, dep, st.lip, k);
                }
                else p.Dot(x, -1, q > 0.92 ? st.light : st.lip, k);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Bits.Bit b in bits.List) rocks[b].pvy = b.vy;
            bits.Update(dt);
            foreach (Bits.Bit b in bits.List)
            {
                Rock r = rocks[b];
                if (r.pvy > 80 && b.vy <= 0)
                {
                    front.Spawn(P.Dust, b.x, -0.5, R.Sign() * R.Range(6, 20), -R.Range(2, 10), R.Range(0.25, 0.45), DUST, R.Range(1, 1.8));
                    ImpactsLib.BornWith(front, b);
                    r.pvy = 0;
                }
            }
            back.Update(dt);
            front.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double fade = 1 - Smooth(Duration - 0.5, Duration - 0.1, t);
            Dent(p, fade);
            ImpactsLib.DrawCracks(p, cracks, c => c.len * Ease.OutCubic(Clamp((t - c.t0) / 0.08)), d => 0, Thud, ImpactsLib.SOOT, st.light, fade);
            Rings(p, true);
            back.Draw(p);
        }

        public override void Draw(Painter p)
        {
            Rings(p, false);
            front.Draw(p);
            foreach (Bits.Bit b in bits.List) ImpactsLib.Rock(p, emitter, b, rocks[b].pts, st);
            if (t < 0.035) p.Ellipse(0, 0, 15, 2.5, (d, g) => d < 0.55 ? WHITE : C[6]);
            else if (t < 0.07)
                for (int x = -22; x <= 22; x++)
                    p.Dot(x, 0, Math.Abs(x) < 10 ? WHITE : C[5], 1 - Math.Abs(x) / 24.0);
            if (t < 0.12) p.GlowLine(-20, 0, 20, 0, 8, C[6], 0.45 * (1 - t / 0.12));
        }
    }
}
