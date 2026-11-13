// The earth recipe (grit thrown in a fan, rocks that bounce and kick up dust, a dust column and dust rolling out along
// the floor) is adapted from Sprite Fusion's open-source explosion (https://www.spritefusion.com/pixel-art-vfx/explosion),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class DirtExplosion : Effect
    {
        const double Duration = 3.6;
        const int W = 260, H = 240, OX = 130, OY = 206;

        public static readonly Module Module = new Module("dirt-explosion", "pixelfx/fx/explosions/dirt-explosion", W, H, OX, OY, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1c1610", "#33281c", "#4f3f2c", "#6e5a40", "#927a58", "#b89f78", "#dcc8a2" },
                ["fire"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["seed"] = 3,
            },
            (o, e) => new DirtExplosion(o, e))
        {
            Title = "Dirt explosion",
            Desc = "A buried charge goes off: a flash under the floor, a V of dirt clods and grit thrown high, a brown dust plume that billows and settles, and a crater left behind.",
            Tags = new[] { "explosion", "dirt", "earth", "dust", "debris", "ground", "one-shot" },
            Bg = "ground",
        };

        const double X0 = -OX + 3, X1 = W - OX - 3, Y0 = -OY + 3, Y1 = H - OY - 3;

        sealed class ClodState
        {
            public double pvy;
            public bool thud;
        }

        readonly uint[] E, F;
        readonly Stone clod;
        readonly uint DUST, SOIL, DARK;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Lights lights;
        readonly Bits clods;
        readonly Dictionary<Bits.Bit, ClodState> state = new Dictionary<Bits.Bit, ClodState>();
        double t;

        DirtExplosion(Options o, Emitter e)
        {
            emitter = e;
            E = Fit(o.Ramp("palette"), 7);
            F = Fit(o.Ramp("fire"), 8);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            clod = new Stone(E[5], E[3], E[2]);
            DUST = Mix(E[4], Rgb(150, 140, 150), 0.2);
            SOIL = Mix(E[3], Rgb(90, 80, 90), 0.2);
            DARK = Mix(E[2], Rgb(60, 52, 60), 0.3);
            ps = new Particles(rng: R, ground: floor, sparks: new[] { F[6], F[5], F[3], F[2] }, emitter: e);
            lights = new Lights(e);
            clods = new Bits(floor, e);

            lights.Add(0, -10, 56, F[4], 0.7, 0.22);
            lights.Add(0, -4, 26, WHITE, 0.6, 0.06);
            // the charge: a few sparks out of the hole
            for (int i = 0; i < 14; i++)
            {
                double a = -Math.PI / 2 + R.Range(-0.7, 0.7), sp = R.Range(160, 320);
                ps.Spawn(P.Spark, 0, -2, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.15, 0.35));
            }
            // the fountain: grit in a V, clods thrown high, a few pebbles
            uint[] grits = { E[1], E[2], E[2], E[3], E[3], E[4], E[5] };
            for (int i = 0; i < 300; i++)
            {
                int side = R.Sign();
                double a = -Math.PI / 2 + side * R.Range(0.05, 0.62), sp = R.Range(150, 470) * (1 - 0.3 * Math.Abs(a + Math.PI / 2));
                ps.Spawn(P.Grit, R.Range(-4, 4), -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.2, 2.2), R.Pick(grits), R.Chance(0.45) ? 2 : 1, R.Range(0, 0.06));
            }
            for (int i = 0; i < 30; i++)
            {
                int side = R.Sign();
                double a = -Math.PI / 2 + side * R.Range(0.1, 0.6), sp = R.Range(200, 430);
                var b = new Bits.Bit
                {
                    kind = B.Chunk, x = R.Range(-5, 5), y = -2, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 1, drag = 0.35, bounce = 0.25, life = R.Range(2.2, 3),
                    a = R.Range(0, TAU), va = R.Range(-12, 12), s = i < 12 ? R.Range(2.6, 3.8) : R.Range(1.4, 2.4),
                };
                state[b] = new ClodState();
                clods.Add(b);
            }
            // the dust: a plume punched straight up, two lobes riding the V, and a skirt rolling out along the floor
            for (int i = 0; i < 22; i++)
                ps.Spawn(P.Smoke, R.Range(-7, 7), -R.Range(2, 12), R.Range(-25, 25), -R.Range(70, 180), R.Range(1.8, 2.6), R.Chance(0.6) ? SOIL : DUST, R.Range(9, 14), R.Range(0, 0.14), PF.Big);
            for (int i = 0; i < 14; i++)
            {
                int side = R.Sign();
                double a = -Math.PI / 2 + side * R.Range(0.35, 0.6), sp = R.Range(110, 210);
                ps.Spawn(P.Smoke, side * R.Range(2, 8), -R.Range(2, 8), Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.6, 2.4), R.Chance(0.5) ? SOIL : DUST, R.Range(8, 12), R.Range(0.02, 0.1), PF.Big);
            }
            for (int i = 0; i < 16; i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Smoke, s * R.Range(4, 16), -R.Range(1, 5), s * R.Range(70, 200), -R.Range(2, 12), R.Range(1.4, 2.2), DUST, R.Range(6, 9), R.Range(0.02, 0.1), PF.Big);
            }
            // dark soil thrown up with it, heavier and lower
            for (int i = 0; i < 8; i++) ps.Spawn(P.Smoke, R.Range(-8, 8), -R.Range(2, 8), R.Range(-40, 40), -R.Range(40, 110), R.Range(1.2, 1.8), DARK, R.Range(6, 9), R.Range(0, 0.05));
        }

        public override void Update(double dt)
        {
            t += dt;
            foreach (Bits.Bit b in clods.List) state[b].pvy = b.vy;
            clods.Update(dt);
            // clods thud down: a puff of dust and a spray of crumbs where they land, the big ones break up
            for (int i = clods.List.Count - 1; i >= 0; i--)
            {
                Bits.Bit b = clods.List[i];
                ClodState q = state[b];
                if (b.x < X0 + 4 || b.x > X1 - 4)
                {
                    clods.List.RemoveAt(i);
                    continue;
                }
                bool landed = (q.pvy > 0 && b.vy <= 0) || (b.rest && !q.thud);
                if (!landed || q.pvy < 80)
                {
                    if (b.rest) q.thud = true;
                    continue;
                }
                q.thud = b.rest;
                for (int j = 0; j < 2 + Round(b.s); j++)
                {
                    ps.Spawn(P.Dust, b.x + R.Range(-2, 2), -0.5, R.Sign() * R.Range(15, 55), -R.Range(4, 22), R.Range(0.35, 0.7), DUST, R.Range(1.6, 1.2 + b.s));
                    BornWith(ps, b);
                }
                for (int j = 0; j < Round(b.s * 3); j++)
                {
                    ps.Spawn(P.Grit, b.x, -1, R.Range(-70, 70), -R.Range(40, 140), R.Range(0.8, 1.4), R.Pick(new[] { E[2], E[3], E[4] }), 1);
                    BornWith(ps, b);
                }
                if (b.s > 2.2 && q.pvy > 200) b.s *= 0.6;
            }
            ps.Update(dt);
            Cull(ps, X0, Y0, X1, Y1);
            FadeEdges(ps, X0, X1, 24, Y1);
            lights.Update(dt);
        }

        void Band(Painter p, bool far)
        {
            double k = t / 0.45;
            if (k < 1) Sim.Band(p, 0, 0.5, 90 * Ease.OutCubic(k), 0.16, Math.Max(1, 3 * (1 - k)), 1.1 - k, new[] { E[2], E[4], E[5], E[6] }, g => far ? Px.Sin(g) < 0 : Px.Sin(g) >= 0);
        }

        public override void DrawBack(Painter p)
        {
            Crater(p, E, 0, 28, Ease.OutCubic(Math.Min(1, t / 0.12)), 1 - Smooth(Duration - 0.7, Duration - 0.1, t));
            Band(p, true);
            DrawDirt(p, ps, Layer.Back, emitter);
        }

        public override void Draw(Painter p)
        {
            Band(p, false);
            // the flash out of the ground: a squat dome of white-hot air for a frame or two
            if (t < 0.09) Dome(p, F, 0, 15 * (1 - t * 3), 1 - t / 0.09);
            foreach (Bits.Bit b in clods.List) Rock(p, emitter, b, clod, F);
            DrawDirt(p, ps, Layer.Front, emitter);
            Pool(p, 0, 1, 60, 10, F[3], 0.5 * Math.Max(0, 1 - t / 0.3));
            lights.Draw(p);
        }
    }
}
