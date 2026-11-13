// The charge (streaks spiralling in, a core that swells with an accelerating pulse, little arcs spitting off
// it) and the release (flash, rings, a spray of motes and stars) follow Sprite Fusion's open-source energy charge
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.LightningLib;

namespace Pixelfx
{
    public sealed class ChargeUp : Effect
    {
        static readonly Pose HOLD = new Pose(Poses.READY) { ["crouch"] = 3, ["lean"] = -2, ["fx"] = 15, ["fy"] = -21, ["bx"] = 9, ["by"] = -17, ["ffx"] = 9, ["fbx"] = -7 };
        static readonly Pose BURST = new Pose { ["turn"] = 1.15, ["y"] = -2, ["crouch"] = 0, ["lean"] = -2, ["fx"] = -13, ["fy"] = -37, ["bx"] = 14, ["by"] = -36, ["ffx"] = -2, ["ffy"] = -6, ["fbx"] = 3, ["fby"] = -6 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(-0.36, Poses.READY),
                new PoseKey(0, HOLD),
                new PoseKey(0.8, new Pose(HOLD) { ["crouch"] = 4 }),
                new PoseKey(1.4, new Pose(HOLD) { ["crouch"] = 6, ["lean"] = 0 }),
                new PoseKey(1.59, new Pose(HOLD) { ["crouch"] = 7, ["lean"] = 0 }),
                new PoseKey(1.6, BURST, "snap"),
                new PoseKey(1.95, new Pose(BURST) { ["y"] = 0, ["crouch"] = 2, ["fy"] = -30, ["by"] = -30, ["ffy"] = -4, ["fby"] = -4 }),
                new PoseKey(2.7, Poses.READY),
            },
            Hits = new[] { new HitCue { T = 1.6, Stop = 0.14, Shake = 0.3, Amp = 3 } },
            Dust = new[] { new DustCue { T = 1.6, X = 10, N = 6, Dir = 1 }, new DustCue { T = 1.6, X = -10, N = 6, Dir = -1 } },
        };

        public static readonly Module Module = new Module("charge-up", "pixelfx/fx/lightning/charge-up", 272, 184, 124, 148, 2.7, false, 30,
            new Options { ["palette"] = VOLT, ["sparks"] = PLASMA, ["charge"] = 1.6, ["seed"] = 4 },
            (o, e) => new ChargeUp(o, e), act: Act, actor: 2)
        {
            Title = "Charge up",
            Desc = "Sparks streak and arcs whip in to a crackling core at the caster's hand that swells and pulses faster, then it flashes and bursts: bolts every way, a ring, a spray of sparks.",
            Tags = new[] { "lightning", "electric", "charge", "spell", "power-up", "actor", "one-shot" },
            Bg = "ground",
        };

        const double CX = 15, CY = -21;
        static readonly double[] BK = { 1, 0.7, 0.95, 0.5, 0.8, 0.35 };
        static readonly uint ASH = Hex("#5b5566");

        sealed class BoltPlan
        {
            public double x1, y1, t0;
        }

        sealed class Whip
        {
            public double x, y, t;
            public int seed;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double REL;
        readonly Particles ps;
        readonly Lights lights;
        readonly BoltPlan[] bolts;
        readonly List<Whip> arcs = new List<Whip>();
        readonly Options o;
        List<double> beats = new List<double>();
        double t, phase;
        bool released;

        ChargeUp(Options o, Emitter e)
        {
            this.o = o;
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            REL = o.Num("charge");
            ps = new Particles(R, ground: x => 0, sparks: Ramp(o.Ramp("sparks")), emitter: e);
            lights = new Lights(e);
            bolts = new BoltPlan[8];
            for (int i = 0; i < 8; i++)
            {
                double a = (i / 8.0) * TAU + R.Range(-0.25, 0.25), L = R.Range(44, 72), x1 = CX + Px.Cos(a) * L, y1 = Math.Min(0, CY + Px.Sin(a) * L);
                bolts[i] = new BoltPlan { x1 = x1, y1 = y1, t0 = REL + R.Range(0, 0.05) };
            }
        }

        double Level() => Clamp(t / REL);

        public override void Update(double dt)
        {
            t += dt;
            double k = Level(), e = k * k;
            if (t < REL)
            {
                // energy rushes in: long streaks spiralling in from all round, more and more of them
                for (double acc = (60 + 360 * e) * dt; acc > 0; acc--)
                {
                    if (acc < 1 && !R.Chance(acc)) break;
                    double a = R.Range(0, TAU), d = R.Range(30, 72) * (1 - 0.35 * k), life = R.Range(0.16, 0.26), sp = (d / life) * 0.95;
                    ps.Spawn(P.Mote, CX + Px.Cos(a) * d, Math.Min(-2, CY + Px.Sin(a) * d), -Px.Cos(a) * sp - Px.Sin(a) * sp * 0.4, -Px.Sin(a) * sp + Px.Cos(a) * sp * 0.4, life, R.Pick(new[] { C[7], C[6], C[5] }), 1);
                }
                // arcs whip in from the air, more and more of them
                if (k > 0.2 && R.Chance((2 + 16 * e) * dt))
                {
                    double a = R.Range(0, TAU), d = R.Range(20, 40);
                    var w = new Whip { x = CX + Px.Cos(a) * d, y = Math.Min(-3, CY + Px.Sin(a) * d), t = t };
                    w.seed = R.Int(0, 1000000);
                    arcs.Add(w);
                }
                // the pulse quickens
                phase += (1.5 + 11 * e) * dt;
                if (phase >= 1)
                {
                    phase -= 1;
                    beats.Add(t);
                }
                if (R.Chance(4 * e * dt)) ps.Spawn(P.Star, CX + R.Range(-14, 14), CY + R.Range(-14, 10), 0, -4, R.Range(0.2, 0.35), C[6], R.Range(1, 2));
            }
            else if (!released)
            {
                released = true;
                lights.Add(CX, CY, 110, C[4], 0.8, 0.5);
                lights.Add(CX, CY, 28, WHITE, 0.6, 0.08);
                for (int i = 0; i < 70; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(140, 330) * (Px.Sin(a) > 0.4 ? 0.55 : 1);
                    ps.Spawn(Px.Sin(a) < -0.2 && R.Chance(0.35) ? P.Mote : P.Spark, CX, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.55), R.Pick(new[] { C[7], C[6], C[5] }), R.Range(0.8, 1.4));
                }
                for (int i = 0; i < 10; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(60, 160);
                    ps.Spawn(P.Star, CX, CY, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.4, 0.8), R.Chance(0.5) ? C[6] : C[5], R.Range(1.2, 2.4));
                }
                for (int i = 0; i < 4; i++) ps.Spawn(P.Smoke, CX + R.Range(-5, 5), CY + R.Range(-4, 4), R.Range(-12, 12), -R.Range(8, 18), R.Range(0.6, 0.85), Mix(ASH, C[4], 0.3), R.Range(3, 5), R.Range(0.05, 0.15));
            }
            beats = beats.FindAll(b => t - b < 0.22);
            for (int i = arcs.Count - 1; i >= 0; i--) if (t - arcs[i].t > 0.07) arcs.RemoveAt(i);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p, double tt)
        {
            double k = Level();
            if (t < REL) p.Glow(CX * 0.5, -3, 40, C[3], 0.3 * k * k);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p, double tt)
        {
            double k = Level(), e = k * k;
            int f = (int)Math.Floor(t * 30 + 1e-4);
            Actor A = o.Actor;
            if (t < REL)
            {
                double jit = k > 0.85 ? (f % 2 != 0 ? 1 : -1) * ((k - 0.85) / 0.15) : 0, x = CX + jit, y = CY, pr = 0.5 + 0.5 * Px.Sin(phase * TAU);
                // the body catches the core's light along its near edge
                foreach (double[] ed in EdgeOf(A, false))
                {
                    double ex = ed[0], ey = ed[1], d = Hypot(ex + 0.5 - x, ey + 0.5 - y);
                    if (d < 6 + 10 * k && e > 0.08) p.Px(ex, ey, d < 4 + 6 * k && e > 0.4 ? C[6] : C[4]);
                }
                // rings fall inward to the core on every beat
                foreach (double b in beats)
                {
                    double q = (t - b) / 0.22;
                    p.Ring(x, y, 4 + 20 * (1 - Ease.InQuad(q)) * (0.5 + k), 1, q > 0.6 ? C[6] : C[4], 0.4 + 0.6 * q);
                }
                // arcs whipping in to the core
                foreach (Whip a in arcs) Crackle(p, Zigzag(NewRng(a.seed, f), a.x, a.y, x, y, 0.35, 0.4), 0.85, C, 0.4);
                // the core: swelling, layered, with arcs racing round its skin
                double r = 1.2 + 6.5 * e + pr * (0.5 + 1.2 * k);
                p.Circle(x, y, r + 1.3, C[2]);
                p.Circle(x, y, r, C[4]);
                p.Circle(x - r * 0.12, y - r * 0.12, r * 0.7, C[5]);
                p.Circle(x, y, r * 0.38, WHITE);
                if (r > 3)
                    foreach (double s in new[] { 0, Math.PI })
                        for (double a = 0; a < 1.3; a += 0.12)
                        {
                            double ang = t * 10 + s + a, rr = r * (0.62 + 0.1 * Px.Sin(a * 3));
                            p.Dot(x + Px.Cos(ang) * rr, y + Px.Sin(ang) * rr, a < 0.5 ? WHITE : C[6]);
                        }
                if (k > 0.35)
                {
                    Rng q = NewRng(o.Seed, 5, f);
                    double a = q.Range(0, TAU), l = q.Range(3, 6) * (0.5 + k);
                    Crackle(p, Zigzag(q, x + Px.Cos(a) * r, y + Px.Sin(a) * r, x + Px.Cos(a) * (r + l), y + Px.Sin(a) * (r + l), 0.4, 0.3), 0.7, C, 0.3);
                }
                p.Glow(x, y, 12 + 30 * k, C[4], 0.35 + 0.45 * k);
                p.Glow(x, y, 6 + 6 * k, C[6], 0.25 + 0.3 * k);
            }
            else
            {
                double a = t - REL;
                // the release: the caster flashed white, a flare, a white core blown open
                if (a < 0.07) p.MaskFill(A.Mask, A.X, A.Y, a < 0.035 ? WHITE : C[6]);
                if (a < 0.22)
                {
                    double q = a / 0.22;
                    Sim.Flare(p, CX, CY, 46 * Px.Pow(1 - q, 1.4), 1 - q, C);
                    if (q < 0.3) p.Circle(CX, CY, 12 * (1 - q * 2.5) + 1.5, WHITE);
                }
                // bolts every way, struck and flickering
                for (int i = 0; i < bolts.Length; i++)
                {
                    BoltPlan b = bolts[i];
                    int ba = (int)Math.Floor((t - b.t0) * 30 + 1e-4);
                    if (t < b.t0 || ba >= BK.Length) continue;
                    Stroke(p, Zigzag(NewRng(o.Seed, 3, i, ba >> 1), CX, CY, b.x1, b.y1, 0.25, 0.75), BK[ba], C, ba == 0 ? 0.95 : 0.75, new StrokeOpt { hero = ba == 0 });
                    if (b.y1 > -1) p.Glow(b.x1, -3, 18, C[5], 0.5 * BK[ba]);
                }
                // a ring bursting out with a fainter echo inside it
                double qr = a / 0.45;
                if (qr < 1)
                {
                    double r = 8 + 80 * Ease.OutCubic(qr);
                    Func<double, Func<double, double, double>> hide = rr => (v, g) => CY + Px.Sin(g) * rr > -1 ? 0 : 1.1 - qr;
                    p.Ring(CX, CY, r, Math.Max(1, 3 * (1 - qr)), qr < 0.25 ? WHITE : C[5], hide(r));
                    double r2 = 6 + 56 * Ease.OutCubic(Math.Min(1, qr * 1.3));
                    p.Ring(CX, CY, r2, 1, C[4], (v, g) => CY + Px.Sin(g) * r2 > -1 ? 0 : 0.8 - qr);
                    p.Glow(CX, CY - 6, Math.Min(r * 0.9, 46), C[3], 0.42 * (1 - qr) * (1 - qr));
                }
                // the charge bleeds off in a few last crackles at the hand
                if (a > 0.15 && a < 0.5)
                {
                    Rng qq = NewRng(o.Seed, 8, f);
                    double ang = qq.Range(0, TAU), l = qq.Range(5, 10);
                    Crackle(p, Zigzag(qq, CX, CY, CX + Px.Cos(ang) * l, CY + Px.Sin(ang) * l, 0.45, 0.3), 0.8 * (1 - (a - 0.15) / 0.35) + 0.15, C, 0.3);
                }
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
