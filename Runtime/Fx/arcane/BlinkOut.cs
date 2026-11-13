// The blink (a crackling outline as it gathers, a white flash, the silhouette squeezed into a line, a ring on the
// floor, a fading afterimage) is adapted from Sprite Fusion's open-source blink
// (https://www.spritefusion.com/pixel-art-vfx/movement), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class BlinkOut : Effect
    {
        const double OUT = 0.32, SQUEEZE = 0.12, COLLAPSE = 0.08, POP = OUT + SQUEEZE + COLLAPSE;

        static readonly Pose IN = new Pose { ["turn"] = 1, ["crouch"] = 4, ["fx"] = -2, ["fy"] = -25, ["bx"] = 2, ["by"] = -25, ["armsFront"] = true, ["ffx"] = 0, ["fbx"] = 2 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.36, Poses.READY),
                new PoseKey(-0.1, IN),
                new PoseKey(0.2, new Pose(IN) { ["crouch"] = 5 }),
                new PoseKey(0.32, new Pose(IN) { ["crouch"] = 0 }, "snap"),
                new PoseKey(1.6, new Pose(IN) { ["crouch"] = 0 }),
            },
        };

        /// <summary>The stand-in character is hidden from the flash on: the effect draws it from then.</summary>
        public static readonly Module Module = new Module("blink-out", "pixelfx/fx/arcane/blink-out", 136, 96, 68, 80, 1.6, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 11 },
            (o, e) => new BlinkOut(o, e), act: Act, actor: 2, actorAlpha: (t, o) => t < OUT ? 1 : 0)
        {
            Title = "Blink out",
            Desc = "The caster crackles with gathering light, flashes white, squeezes into a line of light that collapses and pops away in sparks.",
            Tags = new[] { "arcane", "blink", "teleport", "vanish", "movement", "magic", "one-shot" },
            Bg = "ground",
        };

        readonly uint[] C, COLS;
        readonly Rng R;
        readonly Actor A;
        readonly byte[] sh;
        readonly Particles ps;
        readonly Lights lights;
        readonly double mx, my;
        double t;
        bool @out, popped;

        BlinkOut(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            A = o.Actor;
            sh = Shades(A);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            mx = A.X + A.W / 2.0;
            my = A.Y + A.H * 0.55;
            COLS = new[] { WHITE, C[6], C[6], C[5], C[4] };
        }

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < A.W && y < A.H && A.Mask.Data[y * A.W + x] != 0;

        public override void Update(double dt)
        {
            t += dt;
            // gathering: streaks rush in to the chest
            if (t < OUT - 0.04 && R.Chance(0.7))
            {
                double a = R.Range(0, TAU), D = R.Range(22, 44), T = R.Range(0.12, 0.18), v = (D * 3) / (Exp(3 * T) - 1);
                double x = mx + Px.Cos(a) * D, y = Math.Min(-2, my + Px.Sin(a) * D), l = Hypot(x - mx, y - my);
                if (l == 0 || double.IsNaN(l)) l = 1;
                ps.Spawn(P.Streak, x, y, ((mx - x) / l) * v, ((my - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!@out && t >= OUT)
            {
                @out = true;
                lights.Add(mx, my - 6, 54, C[5], 0.9, 0.3);
                lights.Add(mx, my, 26, WHITE, 0.8, 0.1);
            }
            if (!popped && t >= POP)
            {
                popped = true;
                lights.Add(mx, my - 6, 58, C[5], 0.9, 0.35);
                lights.Add(mx, my, 24, WHITE, 0.9, 0.08);
                // the line bursts: bits of the character fly off and drift up, sparks and stars scatter
                for (int i = 0; i < 110; i++)
                {
                    double y = A.Y + R.Range(2, A.H - 2);
                    int s = R.Sign();
                    ps.Spawn(P.Ember, mx + R.Range(-1, 1), y, s * R.Range(25, 105), R.Range(-70, 10), R.Range(0.5, 0.95), R.Pick(COLS), R.Chance(0.3) ? 2 : 1);
                }
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(80, 240);
                    ps.Spawn(P.Spark, mx, my, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.2, 0.5));
                }
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(20, 110);
                    ps.Spawn(i % 2 != 0 ? P.Star : P.Mote, mx, my, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.8 - 20, R.Range(0.4, 0.9), R.Pick(new[] { WHITE, C[6], C[5] }), R.Range(0.8, 1.8));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // the afterimage it leaves: a translucent violet ghost that thins away
            if (t > POP && t < POP + 0.6)
            {
                double ka = 0.5 * (1 - (t - POP) / 0.6);
                for (int y = 0; y < A.H; y++)
                    for (int x = 0; x < A.W; x++)
                        if (sh[y * A.W + x] != 0) p.Blend(A.X + x, A.Y + y, sh[y * A.W + x] == 1 ? C[3] : C[4], ka);
            }
            double k = (t - POP) / 0.5;
            if (k >= 0 && k < 1) Sim.Band(p, 0, 0, 4 + 30 * Ease.OutCubic(k), 0.2, Math.Max(1, 3 * (1 - k)), 1.15 - k, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) < 0, k < 0.25);
            if (t < POP + 0.3) p.Glow(0, -1, 26, C[5], 0.3 * (t < OUT ? t / OUT : 1 - Clamp((t - POP) / 0.3)));
        }

        public override void Draw(Painter p)
        {
            int f = (int)Math.Floor(t * 30);
            if (t < OUT)
            {
                // the body brightens and an outline of light crackles round it, thicker as it gathers
                double k = t / OUT;
                for (int y = 0; y < A.H; y++)
                    for (int x = 0; x < A.W; x++)
                    {
                        if (Inside(x, y))
                        {
                            // the body fills with light: its lit pixels most
                            p.Light(A.X + x, A.Y + y, C[6], k * k * (sh[y * A.W + x] == 1 ? 0.3 : 0.75));
                        }
                        else if ((Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1)) && Rand(x, y, f) < 0.25 + 0.65 * k)
                            p.Px(A.X + x, A.Y + y, k > 0.6 && Rand(x, y, 7) < 0.5 ? WHITE : C[6]);
                    }
                p.Glow(mx, my, 30, C[5], 0.5 * k);
            }
            else if (t < OUT + 0.05)
            {
                Squeezed(p, A, sh, 1, WHITE, C[6]);
                p.Glow(mx, my, 40, C[5], 0.8);
            }
            else if (t < OUT + SQUEEZE)
            {
                // squeezed into a line
                double q = Ease.InCubic((t - OUT - 0.05) / (SQUEEZE - 0.05));
                Squeezed(p, A, sh, 1 - q, WHITE, C[6]);
                p.Glow(mx, my, 34, C[5], 0.8);
            }
            else if (t < POP)
            {
                // the line shrinks to a point
                double q = (t - OUT - SQUEEZE) / COLLAPSE, hh = (A.H / 2.0 + 2) * (1 - Ease.InQuad(q));
                for (double y = -hh - 3; y <= hh + 3; y++) p.Dot(mx, my + y, Math.Abs(y) < hh ? WHITE : C[6], Math.Abs(y) < hh ? 1 : 0.7);
                if (hh > 4)
                    for (double y = -hh * 0.7; y <= hh * 0.7; y++)
                    {
                        p.Dot(mx - 1, my + y, C[5], 0.6);
                        p.Dot(mx + 1, my + y, C[5], 0.6);
                    }
                p.Glow(mx, my, 30, C[5], 0.9);
                p.Glow(mx, my, 8, WHITE, 0.5);
            }
            if (t >= POP && t < POP + 0.22)
            {
                double q = (t - POP) / 0.22;
                Sim.Flare(p, mx, my, 22 * Px.Pow(1 - q, 1.5) + 2, 1 - q, C);
                if (q < 0.3) p.Circle(mx, my, 4 * (1 - q / 0.3) + 1, WHITE);
            }
            double kb = (t - POP) / 0.5;
            if (kb >= 0 && kb < 1) Sim.Band(p, 0, 0, 4 + 30 * Ease.OutCubic(kb), 0.2, Math.Max(1, 3 * (1 - kb)), 1.15 - kb, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) >= 0, kb < 0.25);
            if (t > POP && t < POP + 0.3) Sim.Sparkle(p, mx, my - 1, 3, C[6], C[5], 0.3 * (1 - (t - POP) / 0.3));
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
