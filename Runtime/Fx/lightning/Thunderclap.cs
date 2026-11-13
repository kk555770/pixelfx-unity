// The flash and flare, the bolts and their flicker, the plasma spray, the scorch where bolts land and the dust
// thrown along the floor follow Sprite Fusion's open-source lightning and explosion
// (https://www.spritefusion.com/pixel-art-vfx/lightning), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class Thunderclap : Effect
    {
        static readonly Pose WIDE = new Pose { ["turn"] = 1.15, ["crouch"] = 5, ["lean"] = -1, ["fx"] = -15, ["fy"] = -33, ["bx"] = 15, ["by"] = -33, ["ffx"] = -7, ["fbx"] = 8 };
        static readonly Pose CLAP_ = new Pose { ["turn"] = 1.15, ["y"] = -2, ["crouch"] = 0, ["fx"] = -1, ["fy"] = -45, ["bx"] = 1, ["by"] = -45, ["ffx"] = -3, ["ffy"] = -5, ["fbx"] = 4, ["fby"] = -5 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Targets = new double[] { 104 },
            Poses = new[]
            {
                new PoseKey(-0.46, Poses.READY),
                new PoseKey(-0.18, WIDE),
                new PoseKey(0.08, new Pose(WIDE) { ["crouch"] = 6 }),
                new PoseKey(0.12, CLAP_, "snap"),
                new PoseKey(0.55, new Pose(CLAP_) { ["y"] = 0, ["ffy"] = -4, ["fby"] = -4, ["crouch"] = 1 }),
                new PoseKey(1.05, new Pose { ["turn"] = 1, ["crouch"] = 3, ["fx"] = -10, ["fy"] = -28, ["bx"] = 11, ["by"] = -28, ["ffx"] = -4, ["fbx"] = 5 }),
                new PoseKey(1.7, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = 0.12, Stop = 0.13, Shake = 0.3, Amp = 3, Target = false },
                new HitCue { T = 0.42, Push = 2, Straw = 14, Y = -8, Skin = "shock", Shiver = 0.5 },
                new HitCue { T = 1.2, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
        };

        public static readonly Module Module = new Module("thunderclap", "pixelfx/fx/lightning/thunderclap", 288, 156, 144, 124, 1.9, false, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["reach"] = 104, ["seed"] = 6 },
            (o, e) => new Thunderclap(o, e), act: Act, actor: 2)
        {
            Title = "Thunderclap",
            Desc = "A thunderclap round the caster: a white flash, forked bolts burst out and stab the floor, a heavy shock ring tears across the ground raising dust and grit, sparks streak away.",
            Tags = new[] { "lightning", "electric", "nova", "aoe", "spell", "shockwave", "one-shot" },
            Bg = "ground",
        };

        const double CLAP = 0.12, CY = -22;
        static readonly double[] BK = { 1, 0.7, 0.95, 0.5, 0.8, 0.3 };
        static readonly uint DUST = Hex("#6e6382"), ASH = Hex("#5b5566");
        static readonly uint[] STONE = Ramp("#655b7a", "#4a4263", "#342d45");

        sealed class BoltPlan
        {
            public bool down;
            public double x1, y1, t0;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double REACH;
        readonly Particles ps, hot;
        readonly Lights lights;
        readonly BoltPlan[] bolts;
        readonly bool[] landed;
        readonly Options o;
        double t;
        bool clapped;

        Thunderclap(Options o, Emitter e)
        {
            this.o = o;
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            REACH = o.Num("reach");
            ps = new Particles(R, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            hot = new Particles(R, ground: x => 0, emitter: e);
            lights = new Lights(e);
            // eight bolts: the lower ones stab the floor, the upper ones tear into the air
            bolts = new BoltPlan[8];
            for (int i = 0; i < 8; i++)
            {
                bool down = i % 2 == 0;
                int side = i % 4 < 2 ? -1 : 1;
                double a = down ? (side > 0 ? R.Range(0.35, 0.75) : Math.PI - R.Range(0.35, 0.75)) : side > 0 ? -R.Range(0.15, 1.2) : Math.PI + R.Range(0.15, 1.2);
                double L = down ? -CY / Px.Sin(a) : R.Range(40, 62), x1 = Px.Cos(a) * L, y1 = down ? 0 : CY + Px.Sin(a) * L;
                bolts[i] = new BoltPlan { down = down, x1 = x1, y1 = y1, t0 = CLAP + R.Range(0, 0.06) };
            }
            landed = new bool[8];
        }

        double Front(double tt) => tt < CLAP ? 0 : 8 + (REACH - 8) * Ease.OutCubic(Clamp((tt - CLAP) / 0.5));

        public override void Update(double dt)
        {
            t += dt;
            // the breath before: sparks rush in to the caster's chest
            if (t < CLAP && R.Chance(60 * dt))
            {
                double a = R.Range(0, TAU), d = R.Range(16, 30), T = R.Range(0.07, 0.1), v = (d * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, Px.Cos(a) * d, CY + Px.Sin(a) * d * 0.8, -Px.Cos(a) * v, -Px.Sin(a) * v * 0.8, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (t >= CLAP && !clapped)
            {
                clapped = true;
                lights.Add(0, CY - 10, 100, C[4], 0.65, 0.45);
                lights.Add(0, CY, 26, WHITE, 0.5, 0.07);
                for (int i = 0; i < 64; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(170, 380) * (Px.Sin(a) > 0.4 ? 0.5 : 1);
                    ps.Spawn(P.Spark, Px.Cos(a) * 6, CY + Px.Sin(a) * 6, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.2, 0.55));
                }
                for (int i = 0; i < 8; i++) ps.Spawn(P.Smoke, R.Range(-8, 8), CY + R.Range(-6, 6), R.Range(-25, 25), -R.Range(10, 30), R.Range(0.8, 1.3), Mix(ASH, C[4], 0.3), R.Range(4, 7), R.Range(0.05, 0.2));
            }
            for (int i = 0; i < bolts.Length; i++)
            {
                BoltPlan b = bolts[i];
                if (b.down && !landed[i] && t >= b.t0)
                {
                    landed[i] = true;
                    lights.Add(b.x1, -4, 46, C[4], 0.7, 0.25);
                    for (int j = 0; j < 12; j++)
                    {
                        double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(70, 240);
                        ps.Spawn(P.Spark, b.x1, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.2, 0.5));
                    }
                    for (int j = 0; j < 5; j++) hot.Spawn(P.Spark, b.x1 + R.Range(-2, 2), -1, R.Range(-80, 80), -R.Range(60, 180), R.Range(0.4, 0.8));
                }
            }
            // the ring tears the floor up as it passes: dust rolling out, grit tossed
            double fr = Front(t);
            if (t > CLAP && t < CLAP + 0.5)
                foreach (int s in new[] { -1, 1 })
                {
                    if (R.Chance(70 * dt)) ps.Spawn(P.Smoke, s * fr + R.Range(-3, 3), -R.Range(1, 4), s * R.Range(10, 40), -R.Range(5, 20), R.Range(0.6, 1.1), DUST, R.Range(3, 6) * (1 - 0.4 * (fr / REACH)), 0, PF.Big);
                    if (R.Chance(50 * dt)) ps.Spawn(P.Grit, s * fr, -1, s * R.Range(10, 60), -R.Range(60, 180), R.Range(0.8, 1.3), R.Pick(STONE), R.Chance(0.3) ? 2 : 1);
                }
            ps.Update(dt);
            hot.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            // scorch where the bolts struck, and the far half of the shock ring
            foreach (BoltPlan b in bolts)
                if (b.down && t >= b.t0) Scorch(p, b.x1, 0, 9, Px.Pow(Clamp(1 - (t - b.t0) / 1.1), 1.3), 1 - Smooth(1.4, 1.85, t));
            double u = (t - CLAP) / 0.5;
            if (u > 0 && u < 1) Sim.Band(p, 0, 0.5, Front(t), 0.18, Math.Max(2, 16 * (1 - u)), 1.25 - u, new[] { C[1], C[2], C[4], C[6] }, g => Px.Sin(g) < 0, u < 0.15);
            ps.Draw(p, Layer.Back);
            hot.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            int f = (int)Math.Floor(t * 30 + 1e-4);
            double u = (t - CLAP) / 0.5;
            Actor A = o.Actor;
            // the flash: the caster lit white for a frame, a flare, a core
            double a = t - CLAP;
            if (a >= 0 && a < 0.07) p.MaskFill(A.Mask, A.X, A.Y, a < 0.035 ? WHITE : C[6]);
            if (a >= 0 && a < 0.2)
            {
                double q = a / 0.2;
                Sim.Flare(p, 0, CY, 44 * Px.Pow(1 - q, 1.4), 1 - q, C);
                if (q < 0.35) p.Circle(0, CY, 10 * (1 - q * 2) + 2, WHITE);
                p.Glow(0, CY, 44, C[5], 0.32 * (1 - q));
            }
            // the bolts: struck, flickering, re-struck every other frame
            for (int i = 0; i < bolts.Length; i++)
            {
                BoltPlan b = bolts[i];
                int ba = (int)Math.Floor((t - b.t0) * 30 + 1e-4);
                if (t < b.t0 || ba >= BK.Length) continue;
                Rng r = NewRng(o.Seed, 3, i, ba >> 1);
                Stroke(p, Zigzag(r, 0, CY, b.x1, b.y1, 0.25, b.down ? 0.5 : 0.8), BK[ba], C, ba == 0 ? 1 : 0.8, new StrokeOpt { hero = ba == 0 });
                if (b.down)
                {
                    p.Circle(b.x1, -1, 3 * BK[ba] + 1, C[5]);
                    p.Px(b.x1, -1, WHITE);
                    p.Glow(b.x1, -3, 20, C[5], 0.5 * BK[ba]);
                }
                else if (ba < 2) Sim.Sparkle(p, b.x1, b.y1, 4, C[6], C[4], 0.4);
            }
            // the shock: the near half of the floor ring, and a ring in the air that bursts out from the clap
            if (u > 0 && u < 1)
            {
                Sim.Band(p, 0, 0.5, Front(t), 0.18, Math.Max(2, 16 * (1 - u)), 1.25 - u, new[] { C[1], C[2], C[4], C[6] }, g => Px.Sin(g) >= 0, u < 0.15);
                foreach (int s in new[] { -1, 1 }) p.Glow(s * Front(t), -3, 28, C[4], 0.45 * (1 - u));
                double ua = (t - CLAP) / 0.26;
                double ra = 8 + 60 * Ease.OutCubic(ua);
                if (ua < 1) p.Ring(0, CY, ra, Math.Max(1, 2 * (1 - ua)), ua < 0.25 ? C[5] : ua < 0.6 ? C[4] : C[3], (v, g) => CY + Px.Sin(g) * ra > -1 ? 0 : 0.85 - 0.85 * ua);
            }
            // the hum after: small arcs crackle round the caster as the charge bleeds away
            if (a > 0.2 && a < 0.9)
            {
                Rng r = NewRng(o.Seed, 9, f);
                double k = 1 - (a - 0.2) / 0.7;
                if (r.Next() < 0.8)
                {
                    double x0 = r.Range(-14, 14), y0 = r.Range(-34, -4), b = r.Range(0, TAU), l = r.Range(6, 13);
                    Crackle(p, Zigzag(r, x0, y0, x0 + Px.Cos(b) * l, y0 + Px.Sin(b) * l, 0.4, 0.3), 0.85 * k + 0.1, C, 0.35);
                }
            }
            ps.Draw(p, Layer.Front);
            hot.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
