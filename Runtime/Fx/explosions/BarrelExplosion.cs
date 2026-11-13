// The blast (fire puffs that cool into smoke, sparks, embers, burning brands that land and keep burning, grit, scorch,
// rings) is adapted from Sprite Fusion's open-source explosion (https://www.spritefusion.com/pixel-art-vfx/explosion),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class BarrelExplosion : Effect
    {
        const double Duration = 5.2;
        const int W = 300, H = 240, OX = 150, OY = 204;

        public static readonly Module Module = new Module("barrel-explosion", "pixelfx/fx/explosions/barrel-explosion", W, H, OX, OY, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["barrel"] = new[] { "#1c0a0e", "#4a1218", "#7c1a1e", "#b02a26", "#d8483a", "#f08a70" },
                ["seed"] = 8,
            },
            (o, e) => new BarrelExplosion(o, e))
        {
            Title = "Barrel explosion",
            Desc = "An explosive barrel shudders, its lid bulging and spitting flame, then blows: a fireball column, staves and the lid flying, burning fuel splashed over the floor.",
            Tags = new[] { "explosion", "barrel", "fire", "smoke", "debris", "ground", "one-shot" },
            Bg = "ground",
        };

        const double BLOW = 0.8, BW = 8, BH = 22;
        const double X0 = -OX + 3, X1 = W - OX - 3, Y0 = -OY + 3, Y1 = H - OY - 3;
        static readonly uint OIL = Hex("#221a22"), SMUT = Hex("#1f1a24"), DUSTC = Hex("#7d7280"), FLOOR0 = Hex("#4a3f69"), FLOOR1 = Hex("#6e6390");
        static readonly uint BAND_LIT = Hex("#e8d8b0"), BAND_MID = Hex("#b8a888"), BAND_DARK = Hex("#6e6050");
        static readonly uint[] GRIT = { Hex("#5a4f6b"), Hex("#3e3650"), Hex("#776c88") };

        sealed class Lid
        {
            public double x, y = -BH, vx, vy, a, va;
            public bool on, rest;
        }

        sealed class Spill
        {
            public double x, w;
        }

        readonly uint[] F, FR, HR, C;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Blazes blazes;
        readonly Lights lights;
        readonly Bits staves;
        readonly Dictionary<Bits.Bit, double> heat = new Dictionary<Bits.Bit, double>();
        readonly Lid lid = new Lid();
        // the fuel splashed over the floor: a puddle and the spots that keep burning in it
        readonly List<Spill> pools = new List<Spill>();
        double t;
        bool blown;

        BarrelExplosion(Options o, Emitter e)
        {
            emitter = e;
            F = Fit(o.Ramp("palette"), 8);
            FR = FireRamp(F);
            HR = HeatRamp(FR);
            C = Fit(o.Ramp("barrel"), 6);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(rng: R, ground: floor, sparks: new[] { F[6], F[5], F[3], F[2] }, onLand: (x, y) => blazes.Add(x, y - 0.25, R.Range(1.2, 2.2), R.Range(0.6, 1)), emitter: e);
            blazes = new Blazes(ps, e);
            lights = new Lights(e);
            staves = new Bits(floor, e);
        }

        void Blow()
        {
            blown = true;
            double y = -BH * 0.5;
            lights.Add(0, y - 20, 90, Mix(F[3], F[6], 0.45), 0.85, 0.9);
            lights.Add(0, y, 44, WHITE, 0.7, 0.09);
            // the fireball: bursting out, then a column of fire driven up out of the drum
            for (int i = 0; i < 5; i++) ps.Spawn(P.Fire, R.Range(-3, 3), y + R.Range(-3, 3), R.Range(-20, 20), R.Range(-40, -10), R.Range(0.18, 0.28), 0, R.Range(9, 12));
            for (int i = 0; i < 30; i++)
            {
                double a = R.Range(-Math.PI - 0.15, 0.15), d = R.Range(0, 8), sp = R.Range(50, 190);
                ps.Spawn(P.Fire, Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.8 - 40, R.Range(0.55, 1.05), 0, R.Range(5, 10), 0, R.Chance(0.5) ? PF.Smokes | PF.Big : PF.None);
            }
            for (int i = 0; i < 12; i++) ps.Spawn(P.Fire, R.Range(-6, 6), y - R.Range(0, 10), R.Range(-14, 14), -R.Range(170, 300), R.Range(0.7, 1.15), 0, R.Range(6, 10), R.Range(0, 0.12), R.Chance(0.7) ? PF.Smokes | PF.Big : PF.None);
            // oily black smoke
            for (int i = 0; i < 12; i++)
            {
                double a = R.Range(-Math.PI, 0), d = R.Range(4, 16);
                ps.Spawn(P.Smoke, Px.Cos(a) * d, y + Px.Sin(a) * d, Px.Cos(a) * 30, -R.Range(30, 90), R.Range(1.6, 2.6), SMUT, R.Range(8, 13), R.Range(0.1, 0.4), PF.Big);
            }
            for (int i = 0; i < 90; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(160, 400);
                ps.Spawn(P.Spark, 0, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 70, R.Range(0.2, 0.6));
            }
            for (int i = 0; i < 40; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(30, 140);
                ps.Spawn(P.Ember, Px.Cos(a) * 8, y + Px.Sin(a) * 8, Px.Cos(a) * sp, Px.Sin(a) * sp - 70, R.Range(1.2, 2.6), F[3]);
            }
            // burning fuel flung out in arcs: it lands and keeps burning
            for (int i = 0; i < 14; i++)
            {
                double a = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(120, 300);
                ps.Spawn(P.Brand, R.Range(-4, 4), y, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.8, 1.4));
            }
            // the staves: the drum's plates torn off and thrown out, tumbling
            for (int i = 0; i < 9; i++)
            {
                double side = i % 2 != 0 ? 1 : -1, a = -Math.PI / 2 + side * R.Range(0.25, 1.15), sp = R.Range(170, 330);
                var b = new Bits.Bit
                {
                    kind = B.Chunk, x = side * R.Range(2, 6), y = -R.Range(4, BH - 4), vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp, g = 1, drag = 0.5, bounce = 0.35,
                    life = R.Range(3, 3.8), a = R.Range(0, TAU), va = R.Range(-16, 16), s = R.Range(9, 13),
                };
                heat[b] = R.Range(0.4, 0.9);
                staves.Add(b);
            }
            // the lid, flung high and spinning
            lid.on = true;
            lid.x = 0;
            lid.y = -BH;
            lid.vx = R.Range(-40, 40);
            lid.vy = -R.Range(430, 480);
            lid.a = 0;
            lid.va = R.Sign() * R.Range(10, 14);
            // the fuel on the floor, and the floor's own dust and grit
            for (int i = 0; i < 7; i++)
            {
                double x = R.Range(-34, 34);
                pools.Add(new Spill { x = x, w = R.Range(5, 10) });
                blazes.Add(x, -0.25, R.Range(2.2, 3), R.Range(0.8, 1.3));
            }
            for (int i = 0; i < 12; i++)
            {
                int s = R.Sign();
                ps.Spawn(P.Smoke, s * R.Range(0, 10), -R.Range(1, 4), s * R.Range(80, 220), -R.Range(2, 12), R.Range(1.1, 1.8), DUSTC, R.Range(6, 9), R.Range(0, 0.05), PF.Big);
            }
            for (int i = 0; i < 60; i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.7, sp = R.Range(120, 330) * (1 - 0.4 * Math.Abs(u));
                ps.Spawn(P.Grit, u * 6, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.1, 1.9), R.Pick(GRIT), R.Chance(0.4) ? 2 : 1);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!blown)
            {
                // the drum strains: flame and smoke spitting from the seam under the lid, quicker as it goes
                double k = t / BLOW;
                if (R.Chance((6 + 30 * k * k) * dt))
                {
                    int s = R.Sign();
                    ps.Spawn(P.Fire, s * R.Range(4, 7), -BH + 1, s * R.Range(20, 60), -R.Range(20, 60), R.Range(0.15, 0.3), 0, R.Range(1.5, 3));
                    ps.Spawn(P.Spark, s * 6, -BH + 1, s * R.Range(40, 140), -R.Range(20, 120), R.Range(0.1, 0.25));
                }
                if (R.Chance(10 * dt)) ps.Spawn(P.Smoke, R.Range(-5, 5), -BH, R.Range(-8, 8), -R.Range(15, 35), R.Range(0.8, 1.3), 0, R.Range(2.5, 4));
                if (t >= BLOW) Blow();
            }
            if (lid.on && !lid.rest)
            {
                lid.vy += 950 * dt;
                lid.vx -= lid.vx * 0.4 * dt;
                lid.x += lid.vx * dt;
                lid.y += lid.vy * dt;
                lid.a += lid.va * dt;
                if (lid.y >= -1 && lid.vy > 0)
                {
                    // a clank: sparks and dust where it lands, then it bounces flat and settles
                    for (int i = 0; i < 6; i++) ps.Spawn(P.Spark, lid.x, -1, R.Range(-90, 90), -R.Range(40, 140), R.Range(0.1, 0.25));
                    for (int i = 0; i < 5; i++) ps.Spawn(P.Dust, lid.x + R.Range(-4, 4), -0.5, R.Sign() * R.Range(15, 60), -R.Range(4, 18), R.Range(0.35, 0.7), DUSTC, R.Range(1.8, 3));
                    lid.y = -1;
                    if (lid.vy > 160)
                    {
                        lid.vy *= -0.3;
                        lid.vx *= 0.5;
                        lid.va *= 0.5;
                    }
                    else
                    {
                        lid.rest = true;
                        lid.a = Math.PI / 2;
                    }
                }
            }
            staves.Update(dt);
            for (int i = staves.List.Count - 1; i >= 0; i--)
            {
                Bits.Bit b = staves.List[i];
                if (b.x < X0 + 8 || b.x > X1 - 8) staves.List.RemoveAt(i);
                else if (heat[b] > 0) heat[b] = Math.Max(0, heat[b] - dt * 0.35);
            }
            blazes.Update(dt);
            ps.Update(dt);
            Cull(ps, X0, Y0, X1, Y1);
            FadeEdges(ps, X0, X1, 24, Y1);
            lights.Update(dt);
        }

        void Shock(Painter p, double u, bool far)
        {
            double k = u / 0.45;
            if (k < 1) Sim.Band(p, 0, 0.5, 100 * Ease.OutCubic(k), 0.16, Math.Max(1, 4.5 * (1 - k)), 1.15 - k, new[] { FLOOR0, FLOOR1, F[3], F[6] }, g => far ? Px.Sin(g) < 0 : Px.Sin(g) >= 0);
        }

        public override void DrawBack(Painter p)
        {
            if (blown)
            {
                double u = t - BLOW, @out = 1 - Smooth(Duration - BLOW - 1, Duration - BLOW - 0.2, u);
                Scorch(p, HR, u, 30, 2.2, @out, 3);
                // the spilled fuel: a dark slick on the floor, lit by its own fire, shrinking as it burns off
                double left = 1 - Smooth(1.6, 3.4, u);
                if (left > 0)
                    foreach (Spill q in pools)
                        for (double x = Math.Floor(-q.w * left); x <= Math.Ceiling(q.w * left); x++)
                        {
                            double e = Math.Abs(x) / (q.w * left + 0.01);
                            p.Dot(q.x + x, 1, Rand(Math.Floor(q.x + x), Math.Floor(t * 12)) < 0.25 ? F[4] : OIL, @out * (e < 0.8 ? 1 : 0.6));
                            if (e < 0.6) p.Dot(q.x + x, 2, OIL, @out * 0.8);
                        }
                Shock(p, u, true);
            }
            DrawPs(p, ps, Layer.Back, FR, emitter);
            blazes.DrawBack(p);
        }

        public override void Draw(Painter p)
        {
            if (!blown)
            {
                // the barrel, shaking harder, its lid bulging
                double k = t / BLOW, jx = Px.Sin(t * 70) * k * k * 1.4, jy = Math.Abs(Px.Sin(t * 53)) * k * k * 0.8;
                DrawBarrel(p, C, jx, -jy, k);
                p.Glow(0, -BH, 10 + 8 * k, F[4], 0.25 + 0.35 * k * k * (0.6 + 0.4 * Px.Sin(t * 40)));
            }
            else Shock(p, t - BLOW, false);
            DrawPs(p, ps, Layer.Front, FR, emitter);
            foreach (Bits.Bit b in staves.List) Stave(p, b, C, FR, heat[b]);
            if (lid.on) DrawLid(p, C, lid, Math.Min(1, (Duration - t) / 0.4));
            if (blown)
            {
                double u = t - BLOW;
                if (u < 0.08)
                {
                    double k2 = 1 - u / 0.08, r = 18 * (0.85 + 0.15 * k2), y = -BH * 0.5;
                    p.Circle(0, y, r + 1.4, F[2]);
                    p.Circle(0, y, r, k2 > 0.45 ? F[6] : F[5]);
                    p.Circle(0, y, r * 0.6, k2 > 0.45 ? WHITE : F[6]);
                }
                if (u < 0.22) Sim.Flare(p, 0, -BH * 0.5, 54 * Px.Pow(1 - u / 0.22, 1.4), 1 - u / 0.22, F);
                // the burning fuel lights the floor
                Pool(p, 0, 1, 90, 14, F[3], 0.5 * Math.Max(0, 1 - u / 0.7) + 0.22 * (1 - Smooth(1.6, 3.4, u)));
            }
            lights.Draw(p);
        }

        // a steel drum standing on the floor at x: red, two ribs, a lit left side, a dark right side, an outline, a pale
        // band; the lid bulges with k
        static void DrawBarrel(Painter p, uint[] C, double x, double y, double k)
        {
            double bulge = Round(k * k * 2);
            p.Shade(x - BW - 1, y - BH - 3, x + BW + 1, y + 0.5, (px, py, o) =>
            {
                double dx = (px - x) / BW, v = (y - py) / BH; // dx -1..1 across, v 0 at the floor .. 1 at the top
                if (Math.Abs(dx) > 1.1 || v < 0 || v > 1.05) return 0;
                bool edge = Math.Abs(dx) > 0.92 || v > 0.97 || v < 0.04;
                if (edge) return OUTLINE;
                bool rib = Math.Abs(v - 0.33) < 0.035 || Math.Abs(v - 0.67) < 0.035, ribLit = Math.Abs(v - 0.31) < 0.03 || Math.Abs(v - 0.65) < 0.03;
                bool band = v > 0.44 && v < 0.56;
                double lit = -dx; // light from the left
                int i = lit > 0.55 ? 5 : lit > 0.15 ? 4 : lit > -0.35 ? 3 : lit > -0.75 ? 2 : 1;
                if (band) return lit > 0.3 ? BAND_LIT : lit > -0.4 ? BAND_MID : BAND_DARK;
                if (rib) i = Math.Max(1, i - 2);
                else if (ribLit) i = Math.Min(5, i + 1);
                return C[i];
            });
            // the lid: a dark ellipse with a lit rim, pushed up as the drum swells
            p.Ellipse(x, y - BH - bulge * 0.5, BW - 0.5, 2.2 + bulge * 0.4, (d, a) => d > 0.7 ? (Px.Sin(a) < 0 ? C[4] : C[2]) : C[1]);
        }

        // a stave: a curved plate torn from the drum, its painted face and its dark inside showing as it turns; hot edges glow
        void Stave(Painter p, Bits.Bit b, uint[] C, uint[] FR, double heat)
        {
            double fade = Math.Min(1, b.life / 0.5), ca = Px.Cos(b.a), sa = Px.Sin(b.a), L = b.s;
            bool face = Px.Cos(b.a * 1.7) > 0;
            Sim.Shift(p, emitter, b.bornX, b.bornY);
            for (double d = -L / 2; d <= L / 2; d += 0.5)
            {
                double bow = (1 - Px.Pow(2 * d / L, 2)) * 1.2, x = b.x + ca * d - sa * bow, y = b.y + sa * d + ca * bow;
                bool hot = heat > 0.3 && Math.Abs(d) > L / 2 - 1;
                p.Dot(x + sa, y - ca, OUTLINE, fade);
                p.Dot(x, y, hot ? FR[heat > 0.6 ? 3 : 5] : face ? C[4] : C[1], fade);
                p.Dot(x - sa, y + ca, face ? C[2] : OUTLINE, fade);
            }
            if (heat > 0.3) p.Glow(b.x, b.y, 6, FR[4], 0.25 * heat * fade);
            p.Shift(0, 0);
        }

        // the lid: a disc turning as it flies (an ellipse whose width follows its spin), lying flat once it lands
        static void DrawLid(Painter p, uint[] C, Lid lid, double a)
        {
            double w = Math.Max(0.8, Math.Abs(Px.Cos(lid.a)) * 7.5), h = Math.Max(1.2, Math.Abs(Px.Sin(lid.a)) * 7.5);
            double rx = lid.rest ? 7.5 : w, ry = lid.rest ? 1.6 : h;
            p.Ellipse(lid.x, lid.y - (lid.rest ? 0.5 : 0), rx + 0.8, ry + 0.8, OUTLINE, a);
            p.Ellipse(lid.x, lid.y - (lid.rest ? 0.5 : 0), rx, ry, (d, an) => d > 0.65 ? (Px.Sin(an) < 0 ? C[4] : C[2]) : C[3], a);
        }
    }
}
