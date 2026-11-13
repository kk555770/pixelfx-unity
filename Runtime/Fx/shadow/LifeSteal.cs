using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ShadowLib;

namespace Pixelfx
{
    public sealed class LifeSteal : Effect
    {
        static readonly Pose GRASP = new Pose(Poses.READY) { ["crouch"] = 3, ["lean"] = 3, ["fx"] = 17, ["fy"] = -28, ["bx"] = -6, ["by"] = -21, ["ffx"] = 10, ["fbx"] = -9 };
        static readonly Pose FED = new Pose { ["turn"] = 1.15, ["crouch"] = 0, ["lean"] = -3, ["hy"] = -1, ["fx"] = -13, ["fy"] = -37, ["bx"] = 14, ["by"] = -37, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Targets = new double[] { 104 },
            Poses = new[]
            {
                new PoseKey(-0.46, Poses.READY),
                new PoseKey(-0.2, new Pose(Poses.READY) { ["crouch"] = 4, ["lean"] = -2, ["fx"] = 3, ["fy"] = -27, ["bx"] = 8, ["by"] = -20 }),
                new PoseKey(0, GRASP, "snap"),
                new PoseKey(0.4, GRASP),
                new PoseKey(0.7, new Pose(GRASP) { ["fx"] = 13, ["fy"] = -26, ["lean"] = 1 }),
                new PoseKey(0.8, new Pose(FED) { ["crouch"] = 2 }, "snap"),
                new PoseKey(1.22, FED),
                new PoseKey(1.7, new Pose { ["turn"] = 1, ["crouch"] = 2, ["fx"] = -11, ["fy"] = -26, ["bx"] = 12, ["by"] = -26, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(2.2, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = 0.01, Stop = 0.07, Shake = 0.12, Amp = 1, Push = 1.6, Straw = 12, Skin = "red", Y = -24 },
                new HitCue { T = 1.6, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
        };

        public static readonly Module Module = new Module("life-steal", "pixelfx/fx/shadow/life-steal", 184, 100, 36, 92, 2.2, false, 30,
            new Options
            {
                ["palette"] = new[] { "#14040a", "#3a0a14", "#6a101c", "#a01824", "#d42a30", "#ff5a50", "#ff9a8a", "#ffe6de" },
                ["dark"] = "#2a1040",
                ["seed"] = 11,
            },
            (o, e) => new LifeSteal(o, e), act: Act, actor: 2)
        {
            Title = "Life steal",
            Desc = "A target on the right is struck and bleeds red orbs; they hang a moment, then whip home as comet streaks and flood the caster crimson.",
            Tags = new[] { "shadow", "life-steal", "drain", "blood", "heal", "directional", "magic", "one-shot" },
            Bg = "ground",
        };

        // the target point on the right, the caster's heart; when the orbs turn for home
        const double TX = 104, TY = -24, HX = 1, HY = -20;
        const int N = 7;

        sealed class Orb
        {
            public double x, y, vx, vy, home, at, size;
            public readonly List<double[]> trail = new List<double[]>();
            public bool done;
        }

        sealed class Stain
        {
            public double x, t0;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Actor A;
        readonly Particles ps;
        readonly Drops drops;
        readonly List<Stain> stains = new List<Stain>();
        readonly List<Lamp> lights = new List<Lamp>();
        readonly List<Orb> orbs = new List<Orb>();
        double t, flush;
        bool hit;

        LifeSteal(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            A = o.Actor;
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: new[] { WHITE, C[7], C[6], C[5] }, emitter: e);
            drops = new Drops(onLand: (x, y, v, s) => stains.Add(new Stain { x = x, t0 = t }));
            for (int i = 0; i < N; i++)
            {
                double a = -Math.PI / 2 + (i - (N - 1) / 2.0) * 0.4 + R.Range(-0.12, 0.12), sp = R.Range(100, 170);
                orbs.Add(new Orb { x = TX, y = TY, vx = Px.Cos(a) * sp + 10, vy = Px.Sin(a) * sp, home = 0.36 + i * 0.05 + R.Range(0, 0.04), at = 0, size = R.Range(1.6, 2.4) });
            }
        }

        void Light(double x, double y, double r, uint c, double k, double life) => lights.Add(new Lamp(x, y, r, c, k, life));

        public override void Update(double dt)
        {
            t += dt;
            if (!hit)
            {
                hit = true;
                Light(TX, TY, 54, C[5], 1, 0.35);
                Light(TX, TY, 18, WHITE, 1, 0.08);
                for (int i = 0; i < 14; i++) drops.Add(TX + R.Range(-3, 3), TY + R.Range(-3, 3), R.Range(-40, 90), R.Range(-140, -20), R.Chance(0.4) ? 2 : 1);
                for (int i = 0; i < 12; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(80, 170);
                    ps.Spawn(P.Spark, TX, TY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.15, 0.35));
                }
            }
            foreach (Orb b in orbs)
            {
                if (b.done) continue;
                if (t < b.home)
                {
                    // burst out, slowing to a hover
                    b.vx -= b.vx * 5 * dt;
                    b.vy -= b.vy * 5 * dt;
                }
                else
                {
                    // whip home: steer hard at the caster's heart, faster and faster
                    double dx = HX - b.x, dy = HY - b.y, d = Hypot(dx, dy);
                    if (d == 0 || double.IsNaN(d)) d = 1;
                    double pull = 2600 * Math.Min(1, (t - b.home) * 3);
                    b.vx += (dx / d) * pull * dt;
                    b.vy += (dy / d) * pull * dt;
                    b.vx -= b.vx * 2.2 * dt;
                    b.vy -= b.vy * 2.2 * dt;
                    if (d < 6)
                    {
                        b.done = true;
                        b.at = t;
                        flush = Math.Min(1.5, flush + 0.45);
                        Light(HX, HY, 30, C[5], 0.8, 0.25);
                        for (int i = 0; i < 6; i++)
                        {
                            double a = R.Range(0, TAU), sp = R.Range(40, 100);
                            ps.Spawn(P.Spark, HX, HY, Px.Cos(a) * sp, Px.Sin(a) * sp - 20, R.Range(0.12, 0.3));
                        }
                        for (int i = 0; i < 3; i++) ps.Spawn(P.Ember, HX + R.Range(-8, 8), HY + R.Range(-6, 14), R.Range(-10, 10), -R.Range(15, 35), R.Range(0.6, 1.1), C[6]);
                    }
                }
                b.x += b.vx * dt;
                b.y += b.vy * dt;
                b.trail.Insert(0, new[] { b.x, b.y });
                if (b.trail.Count > 26) b.trail.RemoveAt(b.trail.Count - 1);
            }
            flush -= flush * 2.6 * dt;
            for (int i = lights.Count - 1; i >= 0; i--)
                if (!lights[i].Tick(dt)) lights.RemoveAt(i);
            for (int i = stains.Count - 1; i >= 0; i--)
                if (t - stains[i].t0 > 0.8) stains.RemoveAt(i);
            drops.Update(dt);
            ps.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the wound: a dark red bloom where the target was struck, thinning away
            double k = 1 - Smooth(0.1, 0.9, t);
            if (k > 0)
            {
                p.Disc(TX, TY, 6 + t * 10, C[2], 0.5 * k);
                p.Glow(TX, TY, 30, C[4], 0.35 * k);
            }
            foreach (Stain s in stains) p.Dot(s.x, -0.5, C[3], 1 - (t - s.t0) / 0.8);
        }

        public override void Draw(Painter p)
        {
            // the caster flushes crimson as the orbs land: its own colours pushed toward blood-red, a hot outline
            double fk = Math.Min(1, flush) * (1 - Smooth(1.5, 2.05, t));
            if (A != null && fk > 0.03)
            {
                for (int my = 0; my < A.H; my++)
                for (int mx = 0; mx < A.W; mx++)
                {
                    uint c0 = A.Pixels[my * A.W + mx];
                    if (c0 == 0) continue;
                    double l = Luma(c0);
                    p.Blend(A.X + mx, A.Y + my, l > 0.6 ? C[6] : l > 0.4 ? C[5] : l > 0.2 ? C[4] : C[3], 0.45 * fk);
                }
                if (fk > 0.12) p.Outline(A.Mask, A.X, A.Y, fk > 0.6 ? C[6] : fk > 0.3 ? C[5] : C[3]);
                p.Glow(HX, HY, 28, C[5], 0.45 * fk);
            }
            // the hit on the target
            if (t < 0.12) Sim.Sparkle(p, TX, TY, 10 * (1 - t / 0.12), C[7], C[5], 0.6);
            double ru = t / 0.28;
            if (ru < 1) RingFast(p, TX, TY, 4 + 16 * Ease.OutCubic(ru), Math.Max(1, 2 * (1 - ru)), ru < 0.4 ? C[7] : C[5]);
            drops.Draw(p, C[3], C[6]);
            // the orbs: comet tails tapering from a hot head through blood-red to a dark wine thread
            foreach (Orb b in orbs)
            {
                if (b.done && t - b.at > 0.2) continue;
                // the tail is swallowed after the head: it shortens rather than fades
                int n = b.done ? (int)Math.Floor(b.trail.Count * (1 - (t - b.at) / 0.2)) : b.trail.Count;
                for (int i = n - 1; i >= 1; i--)
                {
                    double u = i / 25.0, x0 = b.trail[i - 1][0], y0 = b.trail[i - 1][1], x1 = b.trail[i][0], y1 = b.trail[i][1];
                    p.Line(x1, y1, x0, y0, u < 0.15 ? C[7] : u < 0.35 ? C[6] : u < 0.6 ? C[5] : u < 0.8 ? C[4] : C[2]);
                    if (i < 7) p.Disc(x1, y1, (b.size + 0.4) * (1 - i / 7.0), u < 0.12 ? C[6] : C[5], 0.8);
                    if (i % 6 == 3) p.Glow(x1, y1, 6, C[5], 0.2 * (1 - u));
                }
                if (b.done) continue;
                double pulse = t < b.home ? 0.8 + 0.2 * Px.Sin(t * 30 + b.size * 9) : 1;
                p.Circle(b.x, b.y, b.size + 0.9, C[4]);
                p.Circle(b.x, b.y, b.size * pulse, C[6]);
                p.Px(b.x - 0.5, b.y - 0.5, WHITE);
                p.Glow(b.x, b.y, 9, C[5], 0.5);
            }
            ps.Draw(p);
            foreach (Lamp l in lights)
            {
                double q = l.life / l.max;
                p.Glow(l.x, l.y, l.r * (0.6 + 0.4 * q), l.c, l.k * q * q);
            }
        }
    }
}
