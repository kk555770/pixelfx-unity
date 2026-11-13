// The beam's slam, strands, rings and the sparks thrown along the floor are adapted from Sprite Fusion's
// open-source holy-light (https://www.spritefusion.com/pixel-art-vfx/holy-light), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class RespawnBeam : Effect
    {
        static readonly Pose SET = new Pose { ["turn"] = 1, ["crouch"] = 4, ["fx"] = -9, ["fy"] = -14, ["bx"] = 10, ["by"] = -14, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(0, SET),
                new PoseKey(1.4, SET),
                new PoseKey(1.72, new Pose { ["turn"] = 1.15, ["crouch"] = -1, ["fx"] = -13, ["fy"] = -36, ["bx"] = 14, ["by"] = -36, ["hy"] = -1, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(2.1, Poses.READY),
                new PoseKey(2.5, Poses.READY),
            },
        };

        const double SLAM = 0.32, WRITE0 = SLAM + 0.08, WRITE = 0.95, DONE = WRITE0 + WRITE, LIFT = DONE + 0.12, DROP = 0.07, TOPY = -136;

        public static readonly Module Module = new Module("respawn-beam", "pixelfx/fx/spawn/respawn-beam", 128, 150, 64, 136, 2.5, false, 30,
            new Options
            {
                ["palette"] = new[] { "#061e1b", "#0c3e36", "#136a5a", "#1f9c80", "#45d1a8", "#a8f5d8", "#ffffff" },
                ["width"] = 15,
                ["seed"] = 12,
            },
            (o, e) => new RespawnBeam(o, e), act: Act, actor: 2, actorAlpha: (t, o) => t < DONE + 0.04 ? 0 : 1)
        {
            Title = "Respawn beam",
            Desc = "A guide line drops, a pillar of light slams onto the spot, and a scan bar climbs from the feet writing the character back in row by row, white-hot lines cooling to its colors.",
            Tags = new[] { "spawn", "respawn", "beam", "teleport", "scanline", "light", "one-shot" },
            Bg = "ground",
        };

        sealed class Strand
        {
            public double o, f, w;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double BW;
        readonly SpawnLib.Body body;
        readonly SpawnLib.RimCell[] rim;
        readonly double top, H;
        readonly Particles ps;
        readonly Lights lights;
        readonly Strand[] strands;
        double t;
        bool slammed, finished;

        RespawnBeam(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            R = new Rng(o.Seed);
            BW = o.Num("width");
            body = SpawnLib.BodyOf(o.Actor, C);
            rim = SpawnLib.RimOf(body);
            top = body.y;
            H = body.h;
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[5], C[4], C[3] }, emitter: e);
            lights = new Lights(e);
            // strands of brighter light running down the beam, each at its own offset and shimmer
            strands = new Strand[6];
            for (int i = 0; i < 6; i++) strands[i] = new Strand { o = Rand(i, 1) * 1.6 - 0.8, f = 1 + Rand(i, 2) * 3, w = Rand(i, 3) };
        }

        // the scan bar: where it is (local y), climbing from the feet to the crown
        double ScanY(double t) => -Ease.InOutSine(Clamp((t - WRITE0) / WRITE)) * (H + 2);

        // when each row is written
        double RowT(int my) => WRITE0 + (WRITE * Px.Acos(1 - 2 * Clamp((H - my) / (H + 2)))) / Math.PI;

        double Width(double t) => t < SLAM - DROP ? 0 : t < SLAM ? BW * 0.6 : t < LIFT ? BW * (1 + 0.4 * Exp(-(t - SLAM) * 10)) * (0.92 + 0.08 * Px.Sin(t * 40)) : BW * (1 - Ease.OutCubic(Clamp((t - LIFT) / 0.3)));

        // the beam lifts away bottom first once the character is written
        static double Bottom(double t) => t < LIFT ? 0 : -150 * Ease.InCubic(Clamp((t - LIFT) / 0.4));

        public override void Update(double dt)
        {
            t += dt;
            // the guide: motes drawn in to the spot along the floor
            if (t < SLAM && R.Chance(0.5))
            {
                int s = R.Sign();
                double D = R.Range(18, 40), T = R.Range(0.15, 0.25), v = (D * 3) / (Exp(3 * T) - 1);
                ps.Spawn(P.Streak, s * D, -R.Range(0, 3), -s * v, 0, T, R.Chance(0.5) ? C[5] : C[4]);
            }
            if (!slammed && t >= SLAM)
            {
                slammed = true;
                lights.Add(0, -28, 64, C[4], 1, 0.5);
                lights.Add(0, -52, 80, C[5], 1.1, 0.4);
                lights.Add(0, -9, 24, WHITE, 0.9, 0.12);
                for (int i = 0; i < 40; i++)
                {
                    int s = R.Sign();
                    double sp = R.Range(60, 135);
                    ps.Spawn(P.Spark, s * R.Range(0, 6), -1, s * sp, -R.Range(20, 140), R.Range(0.25, 0.7));
                }
                for (int i = 0; i < 12; i++) ps.Spawn(P.Star, R.Range(-BW, BW), -R.Range(2, 40), R.Range(-20, 20), -R.Range(20, 70), R.Range(0.5, 1), R.Pick(new[] { WHITE, C[5] }), R.Range(0.8, 1.8));
                for (int i = 0; i < 8; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Dust, s * R.Range(4, 10), -1, s * R.Range(30, 80), -R.Range(3, 10), R.Range(0.4, 0.7), C[2], R.Range(1.5, 3));
                }
            }
            // the scan bar spits sparks sideways as it climbs
            if (t > WRITE0 && t < DONE && R.Chance(0.45))
            {
                double y = ScanY(t);
                int s = R.Sign();
                ps.Spawn(P.Spark, s * R.Range(6, 11), y, s * R.Range(40, 140), R.Range(-60, 10), R.Range(0.15, 0.35));
            }
            // motes rise inside the beam while it stands
            if (t > SLAM && t < LIFT + 0.2 && R.Chance(0.6))
                ps.Spawn(P.Star, R.Range(-1.3, 1.3) * BW, -R.Range(0, 90), R.Range(-6, 6), -R.Range(30, 90), R.Range(0.4, 0.9), R.Pick(new[] { WHITE, C[5], C[5], C[4] }), R.Chance(0.3) ? R.Range(1, 1.8) : 0.6);
            if (!finished && t >= DONE)
            {
                finished = true;
                lights.Add(0, top + H / 2, 40, C[5], 0.7, 0.3);
                for (int i = 0; i < 14; i++)
                {
                    SpawnLib.RimCell c = R.Pick(rim);
                    ps.Spawn(P.Star, c.x, c.y, R.Range(-25, 25), -R.Range(10, 50), R.Range(0.4, 0.9), R.Pick(new[] { WHITE, C[5] }), R.Range(0.8, 1.8));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double w = Width(t), b = Bottom(t);
            // the guide line before the slam, a marker on the floor
            if (t < SLAM)
            {
                double k = Smooth(0, SLAM * 0.7, t) * (1 - Smooth(SLAM - DROP, SLAM, t) * 0.5);
                for (double y = TOPY; y < -1; y += 2) p.Dot(0, y + ToInt32(t * 60) % 2, C[5], k * (0.35 + 0.65 * ((y - TOPY) / -TOPY)));
                Sim.Band(p, 0, 0, 10 - 4 * k, 0.2, 1, k, C, g => Px.Sin(g) < 0);
                p.Glow(0, -2, 16, C[4], 0.4 * k);
            }
            if (w <= 0.2) return;
            // the beam: a see-through column, hard bright edges, shimmering strands; its light hugs the edges so
            // whatever it writes inside stays readable
            double yEnd = t < SLAM ? TOPY + (-1 - TOPY) * Ease.InCubic(Clamp((t - SLAM + DROP) / DROP)) : -1 + b;
            double writing = Smooth(WRITE0 - 0.05, WRITE0 + 0.1, t) * (1 - Smooth(DONE, LIFT, t)), fill = 0.2 - 0.1 * writing;
            for (double y = TOPY; y <= yEnd; y++)
            {
                for (double x = Math.Floor(-w); x <= Math.Ceiling(w); x++)
                {
                    double q = Math.Abs((x + 0.5) / w);
                    if (q > 1) continue;
                    if (q > 0.86)
                    {
                        p.Dot(x, y, q > 0.94 && Rand(x, y, ToInt32(t * 30)) < 0.7 ? WHITE : C[5], 1);
                        continue;
                    }
                    double core = 1 - Smooth(SLAM + 0.06, WRITE0 + 0.08, t);
                    if (q < 0.7 * core)
                    {
                        // the white-hot core, with strands of colour running down it
                        bool hot = q < 0.35 * core;
                        foreach (Strand s in strands)
                            if (Math.Abs((x + 0.5) / w - s.o * 0.6 - 0.05 * Px.Sin(t * s.f * 3 + y * 0.05)) < 0.04)
                                hot = false;
                        p.Px(x, y, hot ? WHITE : q < 0.5 * core ? C[6] : C[5]);
                        continue;
                    }
                    double a = fill * (1.2 - q);
                    uint c = C[4];
                    foreach (Strand s in strands)
                        if (Math.Abs((x + 0.5) / w - s.o - 0.06 * Px.Sin(t * s.f + s.o * 7)) < 0.045 + 0.03 * s.w)
                        {
                            a = 0.55 - 0.25 * writing;
                            c = C[5];
                        }
                    p.Blend(x, y, c, a);
                }
            }
            if (yEnd > TOPY + 24)
                foreach (int s in new[] { -1, 1 })
                    p.GlowLine(s * w, TOPY + 24, s * w, yEnd, 6, C[4], 0.3);
            p.Glow(0, -5, w * 1.5 + 6, C[5], 0.45);
        }

        public override void Draw(Painter p)
        {
            double w = Width(t);
            double sk = (t - SLAM) / 0.45;
            if (sk > 0 && sk < 1)
            {
                double rx = 14 + 18 * Ease.OutCubic(sk), ry = 3 + 1.5 * sk;
                p.Ellipse(0, 0.5, rx, ry, (d, g) => d < 0.4 ? WHITE : d < 0.7 ? C[6] : C[5], (d, g) => (1 - sk) * 1.4 * (1 - d * d));
                p.Glow(0, -2, rx * 1.3, C[5], 0.5 * (1 - sk));
            }
            // the floor: a hot spot and rings at the slam
            for (int i = 0; i < 2; i++)
            {
                double k = (t - SLAM - 0.08 * i) / 0.5;
                if (k > 0 && k < 1) Sim.Band(p, 0, 0, 8 + (i != 0 ? 22 : 34) * Ease.OutCubic(k), 0.2, Math.Max(1, 3 * (1 - k)), 1.15 - k, C, g => true, k < 0.15);
            }
            // the character, written in from the feet: each row lands white, then cools to its own colors
            if (t >= WRITE0 && t < DONE + 0.05)
            {
                double sy = ScanY(t), fr = Math.Floor(t * 30);
                foreach (SpawnLib.Cell b in body.list)
                {
                    double t0 = RowT(b.my), age = t - t0;
                    if (age < 0) continue;
                    double glitch = age < 0.12 && Rand(b.my, fr, 9) < 0.3 ? (Rand(b.my, fr, 4) < 0.5 ? -2 : 2) : 0;
                    uint c = age < 0.05 ? WHITE : age < 0.14 ? C[5] : age < 0.3 ? Mix(b.c, C[5], 0.5 * (1 - (age - 0.14) / 0.16)) : b.c;
                    p.Px(b.x + glitch, b.y, b.edge && age < 0.3 ? C[6] : c);
                }
                // the scan bar itself: a bright line across the beam and a light riding it
                if (t < DONE)
                {
                    p.Line(-w - 2, sy, w + 2, sy, u => Math.Abs(u - 0.5) < 0.35 ? WHITE : C[5]);
                    p.Line(-w, sy + 1, w, sy + 1, C[4], 0.6);
                    p.GlowLine(-w, sy, w, sy, 6, C[5], 0.6);
                }
            }
            // done: a flash round the finished character
            double fk = (t - DONE) / 0.25;
            if (fk > 0 && fk < 1)
            {
                foreach (SpawnLib.RimCell c in rim) p.Dot(c.x, c.y, fk < 0.4 ? WHITE : C[5], 1.2 - fk);
                Sim.Sparkle(p, 6, top + 6, Round(6 * (1 - fk)), WHITE, C[5], 0.5);
            }
            // the beam's front edges over the character while it stands
            if (w > 0.2)
            {
                if (t >= SLAM && t - SLAM < 0.1) p.Glow(0, -30, 56, WHITE, 0.6 * (1 - (t - SLAM) / 0.1));
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
