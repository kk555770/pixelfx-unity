// The pale ghost of the body and the motes it sheds follow Sprite Fusion's open-source movement and heal
// (https://www.spritefusion.com/pixel-art-vfx/movement), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class SoulRise : Effect
    {
        static readonly Pose SLUMP = new Pose { ["crouch"] = 8, ["lean"] = 6, ["hy"] = 2, ["fx"] = 7, ["fy"] = -9, ["bx"] = 11, ["by"] = -9, ["ffx"] = 9, ["fbx"] = -8 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Follow = 0,
            Poses = new[]
            {
                new PoseKey(-0.2, Poses.READY),
                new PoseKey(0.3, new Pose { ["turn"] = 1, ["crouch"] = 2, ["hy"] = -1, ["fx"] = -10, ["fy"] = -24, ["bx"] = 11, ["by"] = -24, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(0.7, new Pose { ["turn"] = 0.7, ["crouch"] = 4, ["lean"] = 3, ["hy"] = 1, ["fx"] = 2, ["fy"] = -14, ["bx"] = 8, ["by"] = -14, ["ffx"] = 4, ["fbx"] = -3 }),
                new PoseKey(1.1, SLUMP),
                new PoseKey(2.7, SLUMP),
                new PoseKey(3.2, new Pose(SLUMP) { ["crouch"] = 7 }),
            },
            Dust = new[] { new DustCue { T = 1.1, X = 0, N = 5 } },
        };

        const double DURATION = 3.2, LEAVE = 0.35, TOP = 2.05, GONE = 2.55;
        static readonly uint SHADOW = Hex("#0c0f1e");

        public static readonly Module Module = new Module("soul-rise", "pixelfx/fx/spawn/soul-rise", 96, 140, 48, 126, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#101a33", "#1f3563", "#35609e", "#5c96d4", "#94c8f0", "#d2f0ff", "#ffffff" },
                ["height"] = 50,
                ["seed"] = 27,
            },
            (o, e) => new SoulRise(o, e), act: Act, actor: 2, actorAlpha: (t, o) => 1)
        {
            Title = "Soul rise",
            Desc = "The body's outline flares and dims, then a pale glowing copy of it lifts out, stretching as its legs melt into a rippling wisp, shedding motes until it thins into light.",
            Tags = new[] { "spawn", "death", "soul", "ghost", "spirit", "rise", "light", "one-shot" },
            Bg = "ground",
        };

        readonly uint[] C;
        readonly Rng R;
        readonly double height;
        readonly SpawnLib.Body body;
        readonly SpawnLib.RimCell[] rim;
        readonly int W, Hh;
        readonly double waist;
        readonly sbyte[] toneAt;
        readonly Particles ps;
        readonly Lights lights;
        double t;
        bool left;

        SoulRise(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            R = new Rng(o.Seed);
            height = o.Num("height");
            body = SpawnLib.BodyOf(o.Actor, C);
            rim = SpawnLib.RimOf(body);
            W = body.w;
            Hh = body.h;
            waist = Round(Hh * 0.6);
            // the soul is the body's own picture in pale light: each pixel's brightness picks a step of the ramp
            toneAt = new sbyte[W * Hh];
            for (int i = 0; i < toneAt.Length; i++) toneAt[i] = -1;
            foreach (SpawnLib.Cell b in body.list) toneAt[b.my * W + b.mx] = (sbyte)(b.edge ? 6 : 3 + Math.Min(2, Math.Floor(Luma(b.c) * 3.6)));
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[5], C[4], C[3] }, emitter: e);
            lights = new Lights(e);
        }

        // where the soul is: lifted, stretched taller and thinner, swaying; legs melt into a wisp below the waist
        double Lift() => height * Ease.InOutSine(Clamp((t - LEAVE) / (TOP - LEAVE)));
        double Stretch() => 1 + 0.22 * Ease.InOutSine(Clamp((t - LEAVE) / (TOP - LEAVE)));
        double Sway(double row) => Px.Sin(t * 3 - row * 0.12) * 2.2 * Smooth(LEAVE, LEAVE + 0.5, t);
        double Melt() => Smooth(LEAVE + 0.1, LEAVE + 0.9, t);
        // it thins from the bottom up once it's high
        double Thin() => Smooth(TOP - 0.25, GONE, t);

        // a ring of light across the floor as it tears loose: far half behind the body, near half in front
        void Ring(Painter p, bool near)
        {
            double k = (t - LEAVE) / 0.5;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0, 6 + 26 * Ease.OutCubic(k), 0.18, Math.Max(1, 3 * (1 - k)), 1.15 - k, C, g => (Px.Sin(g) >= 0) == near, k < 0.12);
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!left && t >= LEAVE)
            {
                left = true;
                lights.Add(0, -20, 50, C[4], 0.85, 0.55);
                lights.Add(0, -20, 24, WHITE, 0.8, 0.12);
                for (int i = 0; i < 10; i++)
                {
                    SpawnLib.RimCell c = R.Pick(rim);
                    ps.Spawn(P.Star, c.x, c.y, R.Range(-20, 20), -R.Range(20, 60), R.Range(0.4, 0.8), R.Pick(new[] { WHITE, C[5] }), R.Range(0.8, 1.8));
                }
            }
            // motes come off its edges as it rises, and pour off it as it thins
            if (t > LEAVE && t < GONE)
            {
                double s = Stretch(), y0 = body.y * s - Lift(), th = Thin();
                for (int n = 0; n < (th > 0 ? 3 : 1); n++)
                {
                    if (!R.Chance(th > 0 ? 0.9 : 0.5)) continue;
                    SpawnLib.RimCell c = R.Pick(rim);
                    double row = c.my;
                    if (row / Hh < 1 - th * 1.1 - 0.05 && th > 0) continue;
                    double x = c.x * (1 - 0.12 * (s - 1) * 4) + Sway(row), y = y0 + c.my * s;
                    // nothing outlives the effect, and nothing climbs off the top
                    double life = Math.Min(Math.Min(R.Range(0.5, 1.1), DURATION - 0.1 - t), (y + 120) / 60);
                    if (life > 0.1) ps.Spawn(R.Chance(0.7) ? P.Mote : P.Star, x, y, R.Range(-12, 12), -R.Range(25, 60), life, R.Pick(new[] { WHITE, C[5], C[5], C[4] }), R.Range(0.5, 1));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // a pale pool on the floor while it leaves, and a ring from the moment it tears loose
            double k = Smooth(0, LEAVE, t) * (1 - Smooth(LEAVE + 0.4, TOP, t));
            if (k > 0) p.Glow(0, -1, 26, C[4], 0.3 * k);
            Ring(p, false);
        }

        public override void Draw(Painter p)
        {
            // the body: its outline flares as the soul tears loose, then the body is left dimmed
            if (t < LEAVE + 0.3)
            {
                double k = t < LEAVE ? t / LEAVE : 1 - (t - LEAVE) / 0.3;
                foreach (SpawnLib.RimCell c in rim)
                    if (Rand(c.mx, c.my, Math.Floor(t * 30)) < 0.25 + 0.7 * k)
                        p.Px(c.x, c.y, k > 0.7 ? WHITE : C[5]);
                p.Glow(0, -19, 26, C[4], 0.45 * k);
            }
            double dim = Smooth(LEAVE, LEAVE + 0.6, t) * (1 - Smooth(GONE, DURATION - 0.1, t));
            if (dim > 0)
                foreach (SpawnLib.Cell b in body.list)
                    p.Blend(b.x, b.y, SHADOW, 0.45 * dim);
            Ring(p, true);
            if (t < LEAVE || t > GONE + 0.1)
            {
                ps.Draw(p);
                lights.Draw(p);
                return;
            }
            // the soul, drawn backward from the screen so stretching leaves no holes; see-through, its rim solid
            double s = Stretch(), L = Lift(), m = Melt(), th = Thin(), top = body.y * s - L, sx = 1 - 0.12 * (s - 1) * 4;
            double tail = Round(10 * m), y1 = top + (Hh + tail) * s, fadeIn = Clamp((t - LEAVE) / 0.15);
            for (double y = Math.Floor(top); y <= Math.Ceiling(y1); y++)
            {
                double my = Math.Floor((y + 0.5 - top) / s);
                if (my < 0) continue;
                // rows below the waist pull in toward a wavy spine: legs melting into a wisp
                double k = my >= waist ? (my - waist + 1) / (Hh - waist + tail) : 0, narrow = 1 - k * m * 0.95;
                if (narrow <= 0.05) continue;
                double cx = Sway(my) + Px.Sin(my * 0.7 - t * 8) * 2 * k * m, a = (my >= waist ? 1 - k * 0.45 : 1) * fadeIn;
                for (double x = Math.Floor(cx - W / 2.0 - 4); x <= Math.Ceiling(cx + W / 2.0 + 4); x++)
                {
                    double mx = Math.Floor((x + 0.5 - cx) / (sx * narrow) - body.x);
                    // as it thins the lower rows go first, with a ragged edge
                    if (th > 0 && my / (Hh + tail) > 1 - th * 1.25 + 0.25 * Rand(mx, my, 5) - 0.1) continue;
                    int ti;
                    if (my < Hh)
                    {
                        if (mx < 0 || mx >= W) continue;
                        ti = toneAt[(int)my * W + (int)mx];
                        if (ti < 0) continue;
                    }
                    else
                    {
                        // the wisp below the feet: a thin trail that tapers to nothing
                        double w = (1 - (my - Hh) / Math.Max(1, tail)) * 3.2 * m, d = Math.Abs(x + 0.5 - cx);
                        if (d > w) continue;
                        ti = d > w - 1 ? 5 : 4;
                    }
                    // the first frames it tears loose it is all white
                    if (t < LEAVE + 0.07) p.Px(x, y, ti >= 6 ? C[5] : WHITE);
                    else if (ti >= 5) p.Dot(x, y, ti >= 6 ? WHITE : C[5], Math.Min(1, a * 1.3));
                    else p.Blend(x, y, C[ti], a * 0.7);
                }
            }
            // its light
            double cy = top + Hh * s * 0.45;
            p.Glow(Sway(10), cy, 30, C[4], 0.42 * fadeIn * (1 - th));
            p.Glow(Sway(10), cy - 6, 14, C[5], 0.3 * fadeIn * (1 - th));
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
