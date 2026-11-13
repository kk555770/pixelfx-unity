// The flame-puff, smoke and spark recipe and the floor ring are adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), and the tongue from its fire
// (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class FireRing : Effect
    {
        static readonly Pose HIGH = new Pose { ["turn"] = 1.15, ["crouch"] = 0, ["fx"] = -7, ["fy"] = -44, ["bx"] = 8, ["by"] = -44, ["ffx"] = -5, ["fbx"] = 6 };
        static readonly Pose SLAM = new Pose { ["turn"] = 1.15, ["crouch"] = 8, ["lean"] = 3, ["fx"] = -9, ["fy"] = -8, ["bx"] = 11, ["by"] = -8, ["ffx"] = -8, ["fbx"] = 9 };
        static readonly Pose EASE = new Pose { ["turn"] = 1, ["crouch"] = 2, ["fx"] = -8, ["fy"] = -17, ["bx"] = 9, ["by"] = -17, ["ffx"] = -4, ["fbx"] = 5 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 70 },
            Poses = new[]
            {
                new PoseKey(-0.32, Poses.READY),
                new PoseKey(0.02, HIGH),
                new PoseKey(0.27, new Pose(HIGH) { ["y"] = -1 }),
                new PoseKey(0.32, SLAM, "snap"),
                new PoseKey(0.85, new Pose(SLAM) { ["crouch"] = 7 }),
                new PoseKey(1.5, EASE),
                new PoseKey(2.4, Poses.READY),
                new PoseKey(3.9, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = 0.32, Stop = 0.1, Shake = 0.28, Amp = 3, Target = false },
                new HitCue { T = 0.6, Push = 1.2, Straw = 10, Y = -8, Skin = "char" },
                new HitCue { T = 3.5, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = 0.32, X = 10, N = 6, Dir = 1 }, new DustCue { T = 0.32, X = -10, N = 6, Dir = -1 } },
        };

        const double DURATION = 3.9;

        public static readonly Module Module = new Module("fire-ring", "pixelfx/fx/fire/fire-ring", 224, 128, 112, 92, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["reach"] = 92,
                ["seed"] = 6,
            },
            (o, e) => new FireRing(o, e), act: Act, actor: 2)
        {
            Title = "Fire ring",
            Desc = "A white flash at the feet, then flames race outward along the floor in a ring, flaring as the front passes and burning down behind it over a smouldering scorch.",
            Tags = new[] { "fire", "ring", "nova", "aoe", "spell", "ground", "one-shot" },
            Bg = "ground",
        };

        const double SY = 0.2, CHARGE = 0.32, SPREAD = 0.75;
        static readonly uint SOOT = Hex("#0d0a12"), SOOT2 = Hex("#17121d"), WARM = Rgb(255, 176, 96);
        // the dying flames: deep orange and red, no white heart
        static readonly double[][] EMBER_LAYERS = { new[] { 1, 1, 7.0 }, new[] { 0.7, 0.8, 6 }, new[] { 0.4, 0.55, 5 } };

        sealed class Site
        {
            public double x, y, t0, life, h, w, n;
            public bool back;
        }

        readonly uint[] H;
        readonly double REACH, S;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Emitter emitter;
        readonly uint GLOW;
        readonly List<Site> sites;
        double t;
        bool burst;

        double Front(double tt) => REACH * Ease.OutCubic(Clamp((tt - CHARGE) / SPREAD));
        double ReachedAt(double d) => CHARGE + SPREAD * (1 - Px.Cbrt(1 - Math.Min(1, d / REACH)));

        FireRing(Options o, Emitter e)
        {
            uint[] F = Fit(o.Ramp("palette"), 9);
            H = HotFirst(F);
            REACH = o.Num("reach");
            R = new Rng(o.Seed);
            S = o.Seed * 7;
            emitter = e;
            ps = new Particles(R, ground: x => 0, sparks: new[] { H[1], H[2], H[4], H[5] }, emitter: e);
            lights = new Lights(e);
            GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25);
            // the flames: rings of tongues on the floor, each lit as the front reaches it, flaring then burning down
            var list = new List<Site>();
            for (double r = 10; r <= REACH; r += 9)
            {
                double per = TAU * Math.Sqrt((r * r + Px.Pow(r * SY, 2)) / 2), n = Math.Max(5, Round(per / 8.5));
                for (int k = 0; k < n; k++)
                {
                    double a = ((k + R.Range(0.15, 0.85)) / n) * TAU, rr = r + R.Range(-3, 3), q = Math.Min(1, rr / REACH);
                    // near the middle they flash and go out as the front passes; the rim, where it stops, burns on and down
                    var s = new Site();
                    s.x = Px.Cos(a) * rr;
                    s.y = Px.Sin(a) * rr * SY;
                    s.back = Px.Sin(a) < 0;
                    s.t0 = ReachedAt(rr) + R.Range(0, 0.04);
                    s.life = (0.3 + 1.0 * q * q) * R.Range(0.85, 1.2);
                    s.h = R.Range(9, 15) * (0.6 + 0.55 * q);
                    s.w = R.Range(1.8, 2.7) * (0.75 + 0.3 * q);
                    s.n = R.Int(0, 100000);
                    list.Add(s);
                }
            }
            sites = list.OrderBy(s => s.y).ToList();
        }

        void Flames(Painter p, bool back)
        {
            FrameNoise NZ = (rate, seed, off) => Noise(t * rate + off, seed);
            foreach (Site s in sites)
            {
                if (s.back != back) continue;
                double age = t - s.t0;
                if (age < 0 || age > s.life) continue;
                double u = age / s.life, rise = Ease.OutBack(Clamp(age / 0.08), 2), fade = 1 - Px.Pow(Math.Max(0, (u - 0.1) / 0.9), 1.3);
                double h = s.h * rise * fade * (0.85 + 0.15 * Noise(t * 3.3, s.n)) * (1 + 0.5 * Math.Max(0, Noise(t * 2.6, s.n + 70) - 0.55));
                if (h < 1) continue;
                // fresh flames white-hot, then cooling as they burn down; the far ones a step dimmer
                double[][] L = u < 0.3 ? (back ? BACK_LAYERS : LAYERS) : u < 0.65 ? BACK_LAYERS : EMBER_LAYERS;
                FlameTongue(p, s.x, s.y, h, s.w * (0.6 + 0.4 * fade), NZ(2.2, s.n + 40) * 1.5 - s.x * 0.02, NZ, s.n, H, L);
            }
        }

        // the scorch: a translucent stain of soot where the fire has crossed, sparse embers cooling in it
        void Scorch(Painter p)
        {
            double fr = Front(t), end = 1 - Smooth(DURATION - 1.2, DURATION - 0.15, t);
            if (fr < 1 || end <= 0) return;
            for (double y = Math.Floor(-fr * SY); y <= Math.Ceiling(fr * SY); y++)
                for (double x = Math.Floor(-fr); x <= Math.Ceiling(fr); x++)
                {
                    double d = Hypot(x + 0.5, (y + 0.5) / SY);
                    if (d > fr) continue;
                    double since = t - ReachedAt(d), h = Rand(x, y, S), hot = 1 - since / (1.2 + h * 0.9);
                    if (hot > 0 && h < 0.03 + 0.06 * hot)
                    {
                        p.Px(x, y, hot > 0.7 ? H[3] : hot > 0.45 ? H[4] : hot > 0.2 ? H[5] : H[6]);
                        p.Light(x, y, H[4], 0.3 * hot);
                        continue;
                    }
                    p.Blend(x, y, h < 0.5 ? SOOT : SOOT2, Math.Min(1, since * 3) * (0.55 - (d / REACH) * 0.25) * end);
                }
        }

        void Ring(Painter p, bool back)
        {
            double k = (t - CHARGE) / (SPREAD + 0.1);
            if (k < 0 || k >= 1) return;
            Sim.Band(p, 0, 0, Front(t) + 1.5, SY, Math.Max(1.5, 6 * (1 - k)), k < 0.75 ? 1 : (1 - k) / 0.25, new[] { H[6], H[4], H[2], H[1] }, g => Px.Sin(g) < 0 == back, k < 0.12);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the charge: streaks of fire spiral in to the feet, heat building there
            if (t < CHARGE - 0.05 && R.Chance(0.9 * dt * 60))
            {
                double a = R.Range(0, TAU), d = R.Range(26, 54), T = R.Range(0.18, 0.26), v = (d * 3) / (Exp(3 * T) - 1);
                double x = Px.Cos(a) * d, y = Math.Min(-2, -6 + Px.Sin(a) * d * 0.45), l = Hypot(x, y + 4);
                if (l == 0 || double.IsNaN(l)) l = 1;
                ps.Spawn(P.Streak, x, y, (-x / l) * v, ((-4 - y) / l) * v, T, R.Chance(0.5) ? H[2] : H[3]);
            }
            if (t < CHARGE && R.Chance(0.5 * dt * 60 * (t / CHARGE))) ps.Spawn(P.Fire, R.Range(-4, 4), -1, R.Range(-8, 8), -R.Range(20, 40), R.Range(0.12, 0.24), 0, R.Range(1, 2.2));
            if (!burst && t >= CHARGE)
            {
                burst = true;
                lights.Add(0, -18, 64, WARM, 0.85, 0.5);
                lights.Add(0, -6, 30, WHITE, 0.8, 0.08);
                for (int i = 0; i < 40; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(80, 220);
                    ps.Spawn(P.Spark, Px.Cos(a) * 3, -2, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.4 - R.Range(40, 120), R.Range(0.2, 0.5));
                }
            }
            // fire shed off the burning ring: puffs that rise and some cool into smoke, and embers
            if (t > CHARGE && t < CHARGE + SPREAD + 1.2)
                for (int i = 0; i < 3; i++)
                {
                    if (!R.Chance(0.75 * dt * 60)) continue;
                    Site s = sites[(int)Math.Floor(R.Next() * sites.Count)];
                    double age = t - s.t0;
                    if (age < 0 || age > s.life * 0.7) continue;
                    ps.Spawn(P.Fire, s.x + R.Range(-1, 1), s.y - s.h * 0.6, R.Range(-5, 5), -R.Range(20, 40), R.Range(0.2, 0.4), 0, R.Range(1.2, 2.4), 0,
                        R.Chance(0.5) ? PF.Smokes | (s.back ? PF.Back : PF.None) : s.back ? PF.Back : PF.None);
                    if (R.Chance(0.12)) ps.Spawn(P.Ember, s.x, s.y - s.h * 0.5, R.Range(-15, 15), -R.Range(30, 70), R.Range(0.6, 1.3), H[3], 1, 0, s.back ? PF.Back : PF.None);
                }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            Scorch(p);
            Ring(p, true);
            Flames(p, true);
            SimDraw(p, ps, H, Layer.Back, emitter);
            // light on the floor running with the front
            double fr = Front(t), k = t < CHARGE ? 0 : 1 - Smooth(CHARGE + SPREAD, CHARGE + SPREAD + 1.4, t);
            if (k > 0)
                for (int i = 0; i < 16; i++)
                    p.Glow(Px.Cos((i / 16.0) * TAU) * fr, Px.Sin((i / 16.0) * TAU) * fr * SY - 3, 16, GLOW, 0.2 * k);
        }

        public override void Draw(Painter p, double tt)
        {
            Ring(p, false);
            Flames(p, false);
            SimDraw(p, ps, H, Layer.Front, emitter);
            // the charge glowing at the feet, then the white flash as it goes
            if (t < CHARGE) p.Glow(0, -3, 8 + 22 * (t / CHARGE), GLOW, 0.6 * (t / CHARGE));
            if (t >= CHARGE && t < CHARGE + 0.1)
            {
                double kf = 1 - (t - CHARGE) / 0.1;
                p.Ellipse(0, -1, 14 * (0.6 + 0.4 * kf), 4, H[2]);
                p.Ellipse(0, -1, 9 * (0.6 + 0.4 * kf), 2.6, kf > 0.5 ? WHITE : H[1]);
            }
            double k = Smooth(CHARGE - 0.05, CHARGE, t) * (1 - Smooth(CHARGE + SPREAD * 0.5, CHARGE + SPREAD + 1.2, t));
            if (k > 0) p.Glow(0, -6, 92, GLOW, 0.3 * k);
            lights.Draw(p);
        }
    }
}
