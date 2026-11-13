// The flames that cool into smoke, the embers and the smouldering glow are adapted from Sprite Fusion's
// open-source fire (https://www.spritefusion.com/pixel-art-vfx/fire), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class DissolveBurn : Effect
    {
        static readonly Pose WRITHE = new Pose { ["turn"] = 1, ["crouch"] = 2, ["fx"] = -13, ["fy"] = -35, ["bx"] = 13, ["by"] = -26, ["ffx"] = -3, ["fbx"] = 4 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.16, Poses.READY),
                new PoseKey(0.4, new Pose { ["turn"] = 1, ["crouch"] = 2, ["fx"] = -9, ["fy"] = -22, ["bx"] = 10, ["by"] = -22, ["ffx"] = -3, ["fbx"] = 4 }),
                new PoseKey(0.8, WRITHE),
                new PoseKey(1.2, new Pose(WRITHE) { ["lean"] = 2, ["fy"] = -26, ["by"] = -36 }),
                new PoseKey(1.6, new Pose(WRITHE) { ["lean"] = -2, ["fy"] = -36, ["by"] = -26 }),
                new PoseKey(2.1, new Pose(WRITHE) { ["crouch"] = 5, ["lean"] = 2, ["fx"] = -8, ["fy"] = -38, ["bx"] = 9, ["by"] = -38 }),
                new PoseKey(3.6, new Pose(WRITHE) { ["crouch"] = 6, ["lean"] = 2, ["fx"] = -8, ["fy"] = -36, ["bx"] = 9, ["by"] = -36 }),
            },
        };

        const double DURATION = 3.6, IGNITE = 0.15, BURN = 1.9;
        static readonly uint[] CHAR = { Hex("#100908"), Hex("#22130e"), Hex("#3a2016") };

        public static readonly Module Module = new Module("dissolve-burn", "pixelfx/fx/spawn/dissolve-burn", 96, 112, 48, 100, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#2a0e08", "#5a1a0c", "#a8320f", "#e8601a", "#ff9a2e", "#ffd25a", "#fff3c0", "#ffffff" },
                ["ash"] = new[] { "#2a2428", "#4a4248", "#6e6670" },
                ["seed"] = 16,
            },
            (o, e) => new DissolveBurn(o, e), act: Act, actor: 2, actorAlpha: (t, o) => 0)
        {
            Title = "Dissolve burn",
            Desc = "An ember seam lights at the feet and climbs the body: above it the body scorches and smoulders, at it flames lick up, below it there's only falling ash, embers and smoke.",
            Tags = new[] { "spawn", "death", "burn", "fire", "ash", "dissolve", "embers", "one-shot" },
            Bg = "ground",
        };

        sealed class Tongue
        {
            public double x, h, s;
        }

        static readonly double[][] Layers = { new[] { 1, 1, 3.0 }, new[] { 0.7, 0.8, 4.0 }, new[] { 0.45, 0.6, 5.0 }, new[] { 0.25, 0.35, 6.0 } };

        readonly uint[] C, A;
        readonly Rng R;
        readonly SpawnLib.Body body;
        readonly double H;
        readonly Particles ps;
        readonly Lights lights;
        readonly Tongue[] tongues;
        double t;

        DissolveBurn(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            A = Fit(o.Ramp("ash"), 3);
            R = new Rng(o.Seed);
            body = SpawnLib.BodyOf(o.Actor, C);
            H = body.h;
            // when each pixel catches: row by row from the feet, a ragged, licking front; the rim a beat early
            foreach (SpawnLib.Cell b in body.list)
            {
                b.key = (body.h - 1 - b.my) / H + 0.08 * (Rand(b.mx, b.my, 3) - 0.5) + 0.06 * Noise(b.mx * 0.35, 4) - (b.edge ? 0.02 : 0);
                b.gone = false;
            }
            ps = new Particles(R, ground: x => 0, sparks: new[] { C[6], C[5], C[4], C[3] }, emitter: e);
            lights = new Lights(e);
            // tongues of flame along the seam, licking up the body still standing: layered, flickering, swaying
            tongues = new Tongue[6];
            for (int i = 0; i < 6; i++) tongues[i] = new Tongue { x = -9 + i * 3.6 + Rand(i, 1) * 1.5, h = 6 + Rand(i, 2) * 6, s = i * 13 };
        }

        static double Front(double t) => -0.04 + 1.1 * Ease.InOutSine(Clamp((t - IGNITE) / BURN));

        // a life that ends before the effect does
        double Left(double a, double b) => Math.Max(0.05, Math.Min(R.Range(a, b), DURATION - 0.1 - t));

        void DrawTongue(Painter p, double x, double y, double h, double n)
        {
            foreach (double[] layer in Layers)
            {
                double lw = layer[0], lh = layer[1];
                uint c = C[(int)layer[2]];
                double Hh = h * lh;
                for (int r = 0; r <= Hh; r++)
                {
                    double u = r / Math.Max(1, Hh), half = 1.8 * lw * Px.Pow(1 - u, 0.9) + Noise(t * 9 + r * 0.5, n) * 0.4;
                    double cxr = x + Noise(t * 4 + r * 0.25, n + 7) * u * 1.6;
                    if (half < 0.3) continue;
                    for (double px = Round(cxr - half); px <= Round(cxr + half); px++) p.Px(px, Round(y - r), c);
                }
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            double f = Front(t);
            foreach (SpawnLib.Cell b in body.list)
            {
                if (b.gone || b.key >= f) continue;
                b.gone = true;
                // a burnt pixel goes to ash that sifts down, an ember that rises, or a lick of flame
                double r = R.Next();
                if (r < 0.2)
                {
                    // ash: low down it drops and settles, higher up it sifts down slowly (and is gone before the floor)
                    if (b.y > -7) ps.Spawn(P.Grit, b.x, b.y, R.Range(-12, 12), R.Range(-20, 0), Left(0.6, 1.2), R.Pick(A), 1);
                    else ps.Spawn(P.Flake, b.x, b.y, R.Range(-10, 10), R.Range(-8, 4), Left(0.5, Math.Min(1.5, -b.y / 18.0)), R.Pick(A), R.Chance(0.2) ? 1.4 : 1);
                }
                else if (r < 0.32) ps.Spawn(P.Ember, b.x, b.y, R.Range(-12, 12), -R.Range(15, 45), Left(0.6, 1.4), C[5], R.Range(1, 1.6));
                else if (r < 0.46) ps.Spawn(P.Fire, b.x, b.y, R.Range(-6, 6), -R.Range(14, 30), R.Range(0.2, 0.42), 0, R.Range(1.2, 2.2), 0, R.Chance(0.5) ? PF.Smokes : PF.None);
                else if (r < 0.5) ps.Spawn(P.Smoke, b.x, b.y - 2, R.Range(-5, 5), -R.Range(14, 26), Left(1.2, 2), Mix(A[1], C[1], 0.3), R.Range(2, 3.4));
            }
            // the feet catch with a spit of sparks and a puff of light
            if (t >= IGNITE && t - dt < IGNITE)
            {
                lights.Add(0, -4, 34, C[4], 0.8, 0.4);
                for (int i = 0; i < 10; i++) ps.Spawn(P.Spark, R.Range(-8, 8), -1, R.Range(-80, 80), -R.Range(40, 150), R.Range(0.2, 0.5));
            }
            // while it burns: steady embers and smoke off the front
            if (f > 0 && f < 1.02)
            {
                double fy = body.y + (1 - Clamp(f)) * H;
                if (R.Chance(0.34)) ps.Spawn(P.Smoke, R.Range(-7, 7), fy - 5, R.Range(-6, 6), -R.Range(16, 30), Left(1.4, 2.4), Mix(A[1], C[3], 0.35), R.Range(2.6, 4.2));
                if (R.Chance(0.5)) ps.Spawn(P.Fire, R.Range(-9, 9), fy, R.Range(-6, 6), -R.Range(18, 34), R.Range(0.18, 0.36), 0, R.Range(1.2, 2.4), 0, R.Chance(0.35) ? PF.Smokes : PF.None);
                if (R.Chance(0.15)) ps.Spawn(P.Ember, R.Range(-9, 9), fy, R.Range(-15, 15), -R.Range(30, 70), Left(0.8, 1.6), C[5], 1.4);
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            // warm light on the floor while the body burns down to it
            double k = Smooth(0, IGNITE + 0.2, t) * (1 - Smooth(IGNITE + BURN * 0.6, IGNITE + BURN + 0.4, t));
            if (k > 0) p.Glow(0, -1, 30, C[4], 0.3 * k);
            ps.Draw(p, Layer.Back);
        }

        public override void Draw(Painter p)
        {
            double f = Front(t), fr = Math.Floor(t * 30);
            foreach (SpawnLib.Cell b in body.list)
            {
                if (b.gone) continue;
                double d = b.key - f;
                uint c;
                // the seam: white-hot at the very edge, flickering through yellow and orange
                if (d < 0.025) c = Rand(b.mx, b.my, fr) < 0.35 ? WHITE : C[6];
                else if (d < 0.05) c = Rand(b.mx, b.my, fr + 7) < 0.5 ? C[5] : C[4];
                // just above it the body is charred, with live sparks in the char
                else if (d < 0.11) c = Rand(b.mx, b.my, 13) < 0.15 && Rand(b.mx, fr, 2) < 0.6 ? C[3] : CHAR[d < 0.075 ? 0 : 1];
                else if (d < 0.16) c = Mix(b.c, CHAR[2], 0.6);
                // above that the heat browns it
                else if (d < 0.26) c = Mix(b.c, C[2], 0.18);
                else c = b.c;
                p.Px(b.x, b.y, c);
            }
            // light along the seam, and flames licking up from it while there's body left to burn
            if (f > -0.02 && f < 1.05)
            {
                double fy = body.y + (1 - Clamp(f)) * H, k = 1 - Smooth(0.95, 1.05, f);
                foreach (Tongue g in tongues)
                {
                    double row = Round(fy - body.y), mx = Round(g.x - body.x);
                    if (!body.On((int)mx, (int)Math.Min(H - 1, Math.Max(0, row)))) continue;
                    double h = g.h * k * (0.6 + 0.6 * (Noise(t * 2.6 + g.s, g.s) * 0.5 + 0.5)) * Smooth(-0.02, 0.06, f);
                    if (h >= 1) DrawTongue(p, g.x, fy + 1, h, g.s);
                }
                double fl = 0.85 + 0.15 * Noise(t * 11, 9);
                p.GlowLine(-11, fy, 11, fy, 9, C[4], 0.45 * k);
                p.Glow(0, fy - 6, 56, C[4], 0.38 * k * fl);
                p.Glow(0, fy - 2, 18, C[5], 0.28 * k * fl);
            }
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
