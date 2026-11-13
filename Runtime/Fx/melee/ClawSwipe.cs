// The shaded smear, its light and the dotted trace follow Sprite Fusion's open-source slash
// (https://www.spritefusion.com/pixel-art-vfx/slash), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.MeleeLib;

namespace Pixelfx
{
    public sealed class ClawSwipe : Effect
    {
        static readonly Pose READY_ = new Pose(Poses.READY) { ["weapon"] = "claw", ["offWeapon"] = "claw", ["sa"] = -0.4, ["sb"] = -0.9, ["crouch"] = 5, ["lean"] = 3, ["fx"] = 14, ["fy"] = -21, ["bx"] = 8, ["by"] = -25, ["ffx"] = 9, ["fbx"] = -8 };
        static readonly Pose HIGH = new Pose(READY_) { ["x"] = -6, ["crouch"] = 4, ["lean"] = -3, ["fx"] = 3, ["fy"] = -41, ["sa"] = -1.4, ["bx"] = 7, ["by"] = -18, ["sb"] = -0.3, ["ffx"] = 10 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { 58 },
            Poses = new[]
            {
                new PoseKey(-0.46, new Pose(READY_) { ["x"] = -6 }),
                new PoseKey(-0.2, HIGH),
                new PoseKey(-0.05, new Pose(HIGH) { ["crouch"] = 5 }),
                new PoseKey(0, new Pose(READY_) { ["x"] = 0, ["crouch"] = 3, ["lean"] = 4, ["fx"] = 19, ["fy"] = -36, ["sa"] = -0.9, ["bx"] = -7, ["by"] = -24, ["ghost"] = 1 }, "snap"),
                new PoseKey(0.05, new Pose(READY_) { ["x"] = 2, ["crouch"] = 5, ["lean"] = 6, ["fx"] = 23, ["fy"] = -24, ["sa"] = 0.5, ["bx"] = -7, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true }, "snap"),
                new PoseKey(0.1, new Pose(READY_) { ["x"] = 3, ["crouch"] = 7, ["lean"] = 7, ["fx"] = 22, ["fy"] = -12, ["sa"] = 1.2, ["bx"] = -7, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true }, "snap"),
                new PoseKey(0.42, new Pose(READY_) { ["x"] = 3, ["crouch"] = 6, ["lean"] = 5, ["fx"] = 21, ["fy"] = -13, ["sa"] = 1.1, ["bx"] = -6, ["by"] = -22, ["ffx"] = 13, ["fbx"] = -10 }),
                new PoseKey(0.9, new Pose(READY_) { ["x"] = -6 }),
            },
            Hits = new[]
            {
                new HitCue { T = 0.04, Stop = 0.03, Push = 0.9, Straw = 5, Y = -30, Flash = 0.03 },
                new HitCue { T = 0.08, Stop = 0.03, Push = 0.9, Straw = 5, Y = -26, Flash = 0.03 },
                new HitCue { T = 0.12, Stop = 0.08, Shake = 0.14, Amp = 2, Push = 1.4, Straw = 9, Y = -18 },
            },
            Dust = new[] { new DustCue { T = 0.1, X = 12, N = 6, Dir = 1 } },
        };

        public static readonly Module Module = new Module("claw-swipe", "pixelfx/fx/melee/claw-swipe", 128, 96, 36, 84, 0.9, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0808", "#5c0f12", "#a51d1d", "#e2401f", "#ff8a2a", "#ffd166", "#fff3c4", "#ffffff" },
                ["sparks"] = new[] { "#fff3c4", "#ffd166", "#ff8a2a", "#c42a1a" },
                ["seed"] = 8,
            },
            (o, e) => new ClawSwipe(o, e), act: Act, actor: 2)
        {
            Title = "Claw swipe",
            Desc = "Three claw marks rake down one after another, white-hot, cooling through orange to red, then close up from both ends and leave a dotted scar, with hot flecks and drips.",
            Tags = new[] { "melee", "claw", "swipe", "beast", "scratch", "combat", "directional", "one-shot" },
            Bg = "ground",
        };

        const double RAKE = 0.05; // each mark tears open
        const double STAGGER = 0.04;
        const double CLOSE0 = 0.16, CLOSE = 0.3; // then closes up from both ends

        sealed class Mark
        {
            public Swing s;
            public double t0, W;
            public bool open;
            public Travel end;
        }

        readonly uint[] C;
        readonly int n;
        readonly Rng Rg;
        readonly Particles ps;
        readonly Bits drips;
        readonly Lights lights;
        readonly Mark[] marks;
        double t;

        ClawSwipe(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            n = C.Length;
            Rg = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(Rg, ground: floor, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            drips = new Bits(floor, e);
            lights = new Lights(e);
            // three marks raking down and back, each a little lower and further out than the last
            marks = new Mark[3];
            for (int i = 0; i < 3; i++)
            {
                Swing s = Through(31 + i * 8, -23 + i * 2, 1.84, 1, 40, 0.84 - i * 0.05, 0.5);
                marks[i] = new Mark { s = s, t0 = i * STAGGER, W = 4.2 - i * 0.4, open = false, end = TravelAt(s, s.span) };
            }
        }

        double HeatAt(Mark m) => Clamp(1 - (t - m.t0) / 0.42);

        public override void Update(double dt)
        {
            t += dt;
            foreach (Mark m in marks)
            {
                if (!m.open && t >= m.t0 + RAKE * 0.7)
                {
                    m.open = true;
                    lights.Add(m.end.x, m.end.y - 6, 34, C[4], 0.45, 0.3);
                    // hot flecks thrown on along the rake, and drips running off the wound
                    for (int i = 0; i < 7; i++)
                    {
                        double a = Atan2(m.end.dy, m.end.dx) + Rg.Range(-0.5, 0.5), sp = Rg.Range(110, 260);
                        ps.Spawn(P.Spark, m.end.x, m.end.y, Px.Cos(a) * sp, Px.Sin(a) * sp, Rg.Range(0.12, 0.32));
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        var (x, y) = OnSwing(m.s, m.s.R, m.s.span * Rg.Range(0.15, 0.9));
                        ps.Spawn(P.Mote, x, y, Rg.Range(-60, -10), Rg.Range(-30, 30), Rg.Range(0.2, 0.4), Rg.Chance(0.5) ? C[5] : C[4], 0.5);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        var (x, y) = OnSwing(m.s, m.s.R - 1, m.s.span * Rg.Range(0.35, 0.75));
                        drips.Add(new Bits.Bit { kind = B.Drip, x = x, y = y, vx = Rg.Range(-8, 8), vy = Rg.Range(10, 40), g = 0.55, life = 1.2, c = C[3] });
                    }
                }
            }
            drips.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            foreach (Mark m in marks)
            {
                double u = t - m.t0;
                if (u < 0) continue;
                double S = m.s.span, head = S * Ease.OutExpo(Clamp(u / RAKE)), c = Smooth(CLOSE0, CLOSE0 + CLOSE, u), lo = (S / 2) * c, hi = Math.Min(head, S - (S / 2) * c);
                double heat = HeatAt(m);
                if (hi - lo > 0.02)
                {
                    // a lens: sharp at both ends, thickest in the middle; white-hot at the core, cooling outward and with time
                    Smear(p, m.s, lo, hi, m.W, (uu, r, o, _) =>
                    {
                        double v = uu / S, w = m.W * Px.Pow(Px.Sin(Math.PI * v), 1.15) * (1 - 0.55 * c), off = Math.Abs(r - (m.s.R - m.W / 2)) / (w / 2 + 0.01);
                        if (w < 0.5 || off > 1) return;
                        double ci = Round((1 - heat) * 5.2 + off * 1.6 + (head < S ? (S - uu) / S : 0) * 0.5);
                        o.c = C[n - 1 - (int)Clamp(ci, 0, n - 2)];
                    }, 0, 2);
                    GlowAlong(p, m.s, m.s.R - m.W / 2, lo, hi, 11, C[4], 0.42 * heat * heat, 0, 0.2);
                }
                // where it has closed, a dotted red scar lingers and fades
                double k = 0.8 * (1 - Clamp((u - CLOSE0 - CLOSE * 0.5) / 0.4));
                if (c > 0.05 && k > 0)
                {
                    Trace(p, m.s, m.s.R - m.W / 2, 0.04 * S, lo, C[2], k);
                    Trace(p, m.s, m.s.R - m.W / 2, S - lo, 0.96 * S, C[2], k);
                }
                // the claw tip glints as it tears in
                double g = u - RAKE * 0.4;
                if (g > 0 && g < 0.16) Sim.Sparkle(p, m.end.x, m.end.y, 5 * Ease.OutBack(Math.Min(1, g / 0.04)) * (1 - g / 0.16), C[6], C[4], 0.4);
            }
            drips.Draw(p);
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
