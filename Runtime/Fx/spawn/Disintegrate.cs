// The flickering outline, the white silhouette flash and the body's own pixels flying off as bits of light are
// adapted from Sprite Fusion's open-source movement (https://www.spritefusion.com/pixel-art-vfx/movement),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Disintegrate : Effect
    {
        static readonly Pose AGONY = new Pose { ["turn"] = 1.15, ["crouch"] = 0, ["hy"] = -1, ["fx"] = -14, ["fy"] = -36, ["bx"] = 15, ["by"] = -36, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.2, Poses.READY),
                new PoseKey(0.05, new Pose { ["turn"] = 1, ["crouch"] = 3, ["fx"] = -9, ["fy"] = -16, ["bx"] = 10, ["by"] = -16, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(0.3, AGONY),
                new PoseKey(3.4, AGONY),
            },
        };

        const double FLASH = 0.42, WHITE_T = 0.07, P0 = FLASH + WHITE_T, PEEL = 1.2, CORE_Y = -20;

        public static readonly Module Module = new Module("disintegrate", "pixelfx/fx/spawn/disintegrate", 176, 100, 48, 86, 3.4, false, 30,
            new Options
            {
                ["palette"] = new[] { "#120a26", "#2a1658", "#3d2f9c", "#3f68d4", "#3fa8ec", "#86e0ff", "#d6f8ff", "#ffffff" },
                ["accent"] = new[] { "#7a3ae4", "#b682ff", "#ecd2ff" },
                ["wind"] = 1,
                ["seed"] = 5,
            },
            (o, e) => new Disintegrate(o, e), act: Act, actor: 2, actorAlpha: (t, o) => t < FLASH - 0.1 ? 1 : 0)
        {
            Title = "Disintegrate",
            Desc = "The character crackles with light, flashes white, then peels away from the head down: each pixel flares white-hot and streams off on the wind as a cooling mote of light.",
            Tags = new[] { "spawn", "death", "disintegrate", "dissolve", "pixels", "energy", "light", "one-shot" },
            Bg = "ground",
        };

        sealed class Mote
        {
            public double x, y, vx, vy, life, max, lift, seed;
            public uint c;
            public int kind;
            public uint[] cool;
            public bool own, rest;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly double W;
        readonly uint[][] COOL;
        readonly SpawnLib.Body body;
        readonly SpawnLib.RimCell[] rim;
        readonly double top, H;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Mote> motes = new List<Mote>();
        double t;
        bool flashed;

        Disintegrate(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            uint[] A = Fit(o.Ramp("accent"), 3);
            R = new Rng(o.Seed);
            W = o.Num("wind");
            // what a mote cools through after its white flare: the main ramp, or the accent for variety
            COOL = new[] { new[] { C[6], C[5], C[5], C[4] }, new[] { A[2], A[1], A[1], A[0] }, new[] { WHITE, C[6], C[5], C[5] } };
            body = SpawnLib.BodyOf(o.Actor, C);
            rim = SpawnLib.RimOf(body);
            top = body.y;
            H = body.h;
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            lights = new Lights(e);
            // when each pixel lets go: row by row from the crown, a ragged front, the rim a beat before the middle
            foreach (SpawnLib.Cell b in body.list)
            {
                double u = (b.y - top) / H;
                b.key = u + 0.14 * (Rand(b.mx, b.my, 3) - 0.5) + 0.05 * Noise(b.mx * 0.45, 7) - (b.edge ? 0.03 : 0);
                b.gone = false;
            }
        }

        static double Front(double t) => -0.06 + 1.14 * Ease.InOutSine(Clamp((t - P0) / PEEL));

        // the wind: a steady push with gusts, and a slow swirl so neighbouring motes travel in sheets
        static double Gust(double t) => 0.7 + 0.5 * (Noise(t * 1.6, 11) * 0.5 + 0.5);

        void LetGo(SpawnLib.Cell b)
        {
            b.gone = true;
            bool lo = b.y > -9 && R.Chance(0.22);
            int kind = lo ? 2 : R.Chance(0.42) ? 1 : R.Chance(0.07) ? 3 : 0;
            if (kind == 3)
            {
                ps.Spawn(P.Star, b.x, b.y, R.Range(-6, 14) * W, R.Range(-26, -8), R.Range(0.6, 1.1), R.Chance(0.5) ? C[6] : WHITE, R.Range(1, 2.2));
                return;
            }
            double life = kind == 2 ? R.Range(0.7, 1.2) : kind == 1 ? R.Range(0.25, 0.5) : R.Range(0.9, 1.7);
            // a pop off the body, outward from its middle and up, before the wind takes it
            double o = (b.x + 0.5) * R.Range(2, 6);
            var m = new Mote
            {
                x = b.x + 0.5, y = b.y + 0.5, vx = (o + R.Range(-6, 16)) * (kind == 2 ? 0.4 : 1), vy = kind == 2 ? R.Range(-30, -8) : R.Range(-46, -8),
                life = life, max = life, c = b.c, kind = kind, lift = R.Range(0.4, 1.4),
            };
            m.cool = COOL[R.Chance(0.3) ? 1 : R.Chance(0.3) ? 2 : 0];
            m.own = R.Chance(0.2);
            m.rest = false;
            m.seed = R.Range(0, 100);
            motes.Add(m);
        }

        public override void Update(double dt)
        {
            t += dt;
            // the charge: light gathers into the body in streaks
            if (t < FLASH - 0.05 && R.Chance(0.55))
            {
                double a = R.Range(0, TAU), D = R.Range(22, 44), T = R.Range(0.16, 0.26), v = (D * 3) / (Exp(3 * T) - 1);
                double x = Px.Cos(a) * D, y = Math.Min(CORE_Y + Px.Sin(a) * D * 0.9, -3), l = Hypot(x, y - CORE_Y);
                if (l == 0 || double.IsNaN(l)) l = 1;
                ps.Spawn(P.Streak, x, y, (-x / l) * v, ((CORE_Y - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!flashed && t >= FLASH)
            {
                flashed = true;
                lights.Add(0, CORE_Y, 64, C[4], 0.85, 0.6);
                lights.Add(0, CORE_Y, 30, WHITE, 0.8, 0.14);
                uint[] pick = { WHITE, C[6], C[5] };
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(50, 150);
                    ps.Spawn(P.Star, R.Range(-6, 6), CORE_Y + R.Range(-10, 10), Px.Cos(a) * sp, Px.Sin(a) * sp * 0.7 - 25, R.Range(0.35, 0.8), R.Pick(pick), R.Range(0.6, 1.8));
                }
            }
            // the peel front eats the body
            if (t >= P0)
            {
                double f = Front(t);
                foreach (SpawnLib.Cell b in body.list)
                    if (!b.gone && b.key < f)
                        LetGo(b);
            }
            // the wind drops once the body is gone, and the last motes hang in the air
            double g = Gust(t) * (1 - 0.55 * Smooth(P0 + PEEL, P0 + PEEL + 1.2, t));
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                if ((m.life -= dt) <= 0)
                {
                    motes.RemoveAt(i);
                    continue;
                }
                if (m.kind == 2)
                {
                    // heavier ash: drifts a little, falls, settles on the floor and glows out
                    if (m.rest) continue;
                    m.vx += (W * 30 * g - m.vx * 1.2) * dt;
                    m.vy += 260 * dt - m.vy * 1.2 * dt;
                    m.x += m.vx * dt;
                    double ny = m.y + m.vy * dt;
                    if (m.vy > 0 && ny >= -0.5)
                    {
                        if (m.vy > 50)
                        {
                            m.vy *= -0.35;
                            m.vx *= 0.6;
                            m.y = -0.6;
                        }
                        else
                        {
                            m.rest = true;
                            m.y = -0.5;
                        }
                    }
                    else m.y = ny;
                    continue;
                }
                double age = m.max - m.life, k = Math.Min(1, age / 0.15);
                double fx = W * (100 * g + 30 * Noise2(m.x * 0.07, m.y * 0.07 - t * 0.8, 3)) * k;
                double fy = (-30 * m.lift + 42 * Noise2(m.x * 0.06 + 40, m.y * 0.06 - t * 1.1, 5)) * k;
                double drag = m.kind == 1 ? 3 : 1.5;
                m.vx += (fx - m.vx * drag) * dt;
                m.vy += (fy - m.vy * drag) * dt;
                m.x += m.vx * dt;
                m.y += m.vy * dt;
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the floor ring from the flash, far half
            double k = (t - FLASH) / 0.5;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0, 6 + 30 * Ease.OutCubic(k), 0.18, Math.Max(1, 3.5 * (1 - k)), 1.15 - k, C, g => Px.Sin(g) < 0, k < 0.12);
            // a cold pool on the floor while the body burns away
            double pool = Smooth(0.1, FLASH, t) * (1 - Smooth(P0 + PEEL * 0.8, P0 + PEEL + 0.6, t));
            if (pool > 0) p.Glow(0, -1, 30, C[4], 0.22 * pool);
        }

        public override void Draw(Painter p)
        {
            double f = Front(t);
            // charge: a crackling outline that thickens, the rim of the body catching light
            if (t < FLASH)
            {
                double k = t / FLASH, fr = Math.Floor(t * 30);
                foreach (SpawnLib.RimCell c in rim)
                {
                    double h = Rand(c.mx * 61 + c.my, fr, 7);
                    if (h < 0.12 + 0.62 * k * k) p.Px(c.x, c.y, k > 0.72 && Rand(c.mx, c.my, fr) < 0.5 ? WHITE : h < 0.2 ? C[6] : C[5]);
                }
                p.Glow(0, CORE_Y, 26, C[4], 0.42 * k);
                p.Glow(0, CORE_Y, 12, C[6], 0.25 * k * k);
            }
            // the body: drawn by us from the shudder on, flashing white, then charged, glowing hot at the front
            if (t >= FLASH - 0.1)
            {
                double shake = t < FLASH ? (Math.Floor(t * 60) % 2 != 0 ? 1 : -1) * 0.6 : 0;
                bool white = t >= FLASH && t < P0;
                foreach (SpawnLib.Cell b in body.list)
                {
                    if (b.gone) continue;
                    uint c;
                    if (white) c = b.edge ? C[6] : WHITE;
                    else if (t < FLASH) c = b.edge ? Mix(b.c, C[6], 0.7) : Mix(b.c, C[5], 0.25);
                    else
                    {
                        double d = b.key - f;
                        bool above = !body.On(b.mx, b.my - 1) || body.Cell(b.mx, b.my - 1).gone;
                        c = above && d < 0.06 ? WHITE : d < 0.03 ? Mix(b.c, C[6], 0.7) : d < 0.1 ? Mix(b.c, C[5], 0.4) : b.edge ? Mix(b.c, C[5], 0.5) : Mix(b.c, C[4], 0.16);
                    }
                    p.Px(b.x + shake, b.y, c);
                }
                if (white)
                {
                    p.Glow(0, CORE_Y, 34, C[5], 0.7);
                    Sim.Sparkle(p, 0, CORE_Y - 3, 9, C[6], C[4], 0.5);
                }
            }
            // light rides the peel front down the body
            if (t >= P0 && f < 1.05)
            {
                double fy = top + Clamp(f) * H, k = 1 - Smooth(0.85, 1.05, f);
                p.GlowLine(-10, fy, 10, fy, 8, C[5], 0.32 * k);
                p.Glow(0, fy, 22, C[4], 0.2 * k);
            }
            // the floor ring, near half
            double kr = (t - FLASH) / 0.5;
            if (kr > 0 && kr < 1) Sim.Band(p, 0, 0, 6 + 30 * Ease.OutCubic(kr), 0.18, Math.Max(1, 3.5 * (1 - kr)), 1.15 - kr, C, g => Px.Sin(g) >= 0, kr < 0.12);
            // the motes: flare white, cool down the ramp, leave a short tail, dither out
            foreach (Mote m in motes)
            {
                double age = m.max - m.life, k2 = m.life / m.max, a = k2 < 0.35 ? k2 / 0.35 : 1;
                uint c;
                if (age < 0.034) c = Mix(m.c, WHITE, 0.55);
                else if (age < 0.06) c = WHITE;
                else if (age < 0.15) c = m.cool[0];
                else
                {
                    double u = (age - 0.15) / Math.Max(0.05, m.max - 0.15);
                    uint cc = m.cool[1 + (int)Math.Min(2, Math.Floor(u * 3))];
                    c = m.own && u < 0.5 ? Mix(m.c, cc, 0.55) : cc;
                    if (m.kind == 0 && Px.Sin(t * 21 + m.seed) > 0.8) c = m.cool[0];
                }
                if (m.kind == 2)
                {
                    p.Dot(m.x, m.y, c, a);
                    p.Glow(m.x, m.y, 3, C[5], 0.3 * k2);
                    continue;
                }
                double sp = Hypot(m.vx, m.vy), sd = sp == 0 || double.IsNaN(sp) ? 1 : sp, ux = m.vx / sd, uy = m.vy / sd;
                if (sp > 30 && m.kind == 0) p.Dot(m.x - ux * 1.2, m.y - uy * 1.2, Scale(c, 0.6), a * 0.85);
                if (sp > 85 && m.kind == 0) p.Dot(m.x - ux * 2.4, m.y - uy * 2.4, Scale(c, 0.4), a * 0.6);
                p.Dot(m.x, m.y, c, a);
                if (k2 > 0.15) p.Glow(m.x, m.y, 2.5, c, (m.kind == 1 ? 0.16 : 0.11) * k2);
            }
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
