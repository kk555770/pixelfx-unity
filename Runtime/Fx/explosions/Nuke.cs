// The fire puffs, sparks and embers are adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class Nuke : Effect
    {
        const int W = 400, H = 320, OX = 200, OY = 284;

        public static readonly Module Module = new Module("nuke", "pixelfx/fx/explosions/nuke", W, H, OX, OY, 9, false, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["dust"] = "#8a7466",
                ["seed"] = 5,
            },
            (o, e) => new Nuke(o, e))
        {
            Title = "Nuke",
            Desc = "A blinding white frame, then a fireball lifts on a thin stem into a wide rolling cap that glows from inside; a white skirt flashes and a dust surge sweeps the floor.",
            Tags = new[] { "explosion", "nuke", "mushroom", "cloud", "big", "ground", "one-shot" },
            Bg = "ground",
        };

        const double FLASH = 0.05, LIFT = 0.07, SKIRT0 = 0.55, SKIRT1 = 1.75;
        const double X0 = -OX + 3, X1 = W - OX - 3, Y0 = -OY + 3, Y1 = H - OY - 3;
        // the cap's smoke: a lit crescent, two mid tones, a dark underside, the darkest hollow
        static readonly uint[] CAP = Ramp("#a0918c", "#7c6f6e", "#5f5458", "#40373f", "#271f27");

        sealed class Veil
        {
            public double a, s, w, e, dx, dy;
        }

        sealed class Skirt
        {
            public double ph, v, s, w, e, dx, dy;
        }

        readonly uint[] F, FR, DUST;
        readonly uint dust;
        readonly Rng R;
        readonly Emitter emitter;
        readonly Particles ps;
        readonly Lights lights;
        readonly Plume plume;
        readonly Column column;
        readonly Veil[] veil;
        readonly Skirt[] skirt;
        double t;
        bool boomed;

        Nuke(Options o, Emitter e)
        {
            emitter = e;
            F = Fit(o.Ramp("palette"), 8);
            FR = FireRamp(F);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            dust = o.Color("dust");
            DUST = new[] { Mix(dust, WHITE, 0.22), dust, Mix(dust, Hex("#3a3040"), 0.35), Mix(dust, Hex("#2a2330"), 0.6), Mix(dust, Hex("#1c1720"), 0.8) };
            ps = new Particles(rng: R, ground: floor, sparks: new[] { F[6], F[5], F[3], F[2] }, emitter: e);
            lights = new Lights(e);
            // a wide, flat cap rolling like a torus: lobes go up the middle, out over the top and curl under at the rim
            plume = new Plume(R, new Plume.Opts
            {
                x = 0, y = -26, rise = 152, tau = 1.7, drift = 3, tube = new double[] { 14, 26 }, ring = new double[] { 4, 46 }, grow = 1.6, n = 50, size = new[] { 9.5, 14.5 }, swell = 0.45,
                spin = 2.4, spinTau = 2.4, heat = 1.25, cool = 0.42, under = 0.6, tilt = 0.16, squash = 0.48, fade = new[] { 6.0, 8.3 }, spread = 26,
                style = 1, inner = true, hollow = true, fray = new[] { 4, 1.6, 6.4 },
            });
            plume.onFray = (x, y, r, ux, uy) => ps.Spawn(P.Smoke, x, y, ux * R.Range(10, 26), uy * R.Range(4, 12) - R.Range(2, 8), R.Range(1, 1.7), CAP[R.Int(0, 1)], r * R.Range(0.3, 0.45));
            // a thin stem that keeps feeding the cap, then breaks up from the bottom
            column = new Column(R, new Column.Opts { w = 3.5, foot = 22, speed = 125, size = new[] { 5.5, 7 }, n = 84, sway = 1.4, style = 1 });
            // the condensation skirt: a veil arching over the cap and a bell flaring round the stem
            veil = new Veil[72];
            for (int i = 0; i < 72; i++)
                veil[i] = new Veil { a = -Math.PI * ((i + R.Range(0.2, 0.8)) / 72), s = R.Range(6, 10), w = R.Range(0.97, 1.06), e = R.Range(0.6, 1.3), dx = R.Range(-1, 1), dy = R.Range(-1, 0.2) };
            skirt = new Skirt[110];
            for (int i = 0; i < 110; i++)
                skirt[i] = new Skirt { ph = R.Range(0, TAU), v = R.Range(0, 1), s = R.Range(5, 9), w = R.Range(0.9, 1.06), e = R.Range(0.6, 1.3), dx = R.Range(-1, 1), dy = R.Range(-1, 0.4) };
        }

        double Top()
        {
            Plume.Shape S = plume.ShapeNow();
            return S.y + S.tube * 0.3;
        }

        void Boom()
        {
            lights.Add(0, -60, 150, F[4], 0.7, 1.6);
            // the fireball's first fire, sparks and embers
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(-Math.PI, 0), d = R.Range(4, 22), sp = R.Range(40, 160);
                ps.Spawn(P.Fire, Px.Cos(a) * d, -26 + Px.Sin(a) * d, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.7 - 30, R.Range(0.6, 1.1), 0, R.Range(7, 12), 0, R.Chance(0.5) ? PF.Smokes | PF.Big : PF.None);
            }
            for (int i = 0; i < 90; i++)
            {
                double a = R.Range(-Math.PI - 0.3, 0.3), sp = R.Range(200, 420);
                ps.Spawn(P.Spark, Px.Cos(a) * 18, -26 + Px.Sin(a) * 18, Px.Cos(a) * sp, Px.Sin(a) * sp - 60, R.Range(0.3, 0.8));
            }
            for (int i = 0; i < 50; i++)
            {
                double a = R.Range(-Math.PI, 0), sp = R.Range(30, 140);
                ps.Spawn(P.Ember, Px.Cos(a) * 20, -26 + Px.Sin(a) * 20, Px.Cos(a) * sp, Px.Sin(a) * sp - 60, R.Range(1.6, 3.4), F[3]);
            }
            // the base surge: dust thrown out along the floor in every direction, racing to the edges; a slower ring behind
            for (int i = 0; i < 120; i++)
            {
                bool slow = i >= 92;
                double a = R.Range(0, TAU), c = Px.Cos(a), s = Px.Sin(a), sp = slow ? R.Range(110, 200) : R.Range(240, 440);
                ps.Spawn(P.Smoke, c * 14, s * 14 * 0.16 - R.Range(1, 3), c * sp, s * sp * (s > 0 ? 0.06 : 0.14) - R.Range(0, slow ? 6 : 3), R.Range(1.8, 3), dust, slow ? R.Range(8, 11) : R.Range(5, 9), R.Range(0.05, 0.2));
            }
            uint[] grits = { DUST[0], DUST[1], DUST[2] };
            for (int i = 0; i < 40; i++)
            {
                double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.7, sp = R.Range(150, 380) * (1 - 0.4 * Math.Abs(u));
                ps.Spawn(P.Grit, u * 12, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(1.2, 2), R.Pick(grits), R.Chance(0.4) ? 2 : 1);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!boomed && t >= LIFT)
            {
                boomed = true;
                Boom();
            }
            if (t >= LIFT) plume.Update(dt);
            Plume.Shape S = plume.ShapeNow();
            double hot = plume.Hot();
            column.Update(dt, Top(), t > 0.35 && t < 4.9 ? 1 : 0);
            // afterwinds: dust drawn in along the floor and up the stem
            if (t > 0.9 && t < 4.6 && R.Chance(9 * dt))
            {
                int s = R.Sign();
                ps.Spawn(P.Vapour, s * R.Range(50, 160), -R.Range(2, 6), -s * R.Range(20, 45), -R.Range(0, 6), R.Range(1.2, 1.8), DUST[1], R.Range(5, 8));
            }
            if (t > 0.3 && t < 3.4 && R.Chance(14 * hot * dt))
            {
                double a = R.Range(0, TAU);
                ps.Spawn(P.Ember, S.x + Px.Cos(a) * (S.ring + S.tube), S.y + Px.Sin(a) * S.tube * 0.5, Px.Cos(a) * R.Range(30, 80), -R.Range(10, 50), R.Range(1.4, 2.6), F[3]);
            }
            Draft(ps, dt, x: 0, top: S.y + S.tube * 0.3, w: 9, up: 160, pull: 120, low: 14, reach: 170, k: 1 - Smooth(4.4, 5.4, t));
            ps.Update(dt);
            Cull(ps, X0, Y0, X1, Y1);
            FadeEdges(ps, X0, X1, 34, Y1);
            lights.Update(dt);
        }

        void Shock(Painter p, bool far)
        {
            // the ground shock: a ring racing out along the floor ahead of the dust
            double k = (t - LIFT) / 0.8;
            if (k > 0 && k < 1) Sim.Band(p, 0, 0.5, 196 * Ease.OutCubic(k), 0.16, Math.Max(1, 5 * (1 - k)), 1.15 - k, new[] { DUST[2], DUST[0], F[5], F[7] }, g => far ? Px.Sin(g) < 0 : Px.Sin(g) >= 0);
        }

        public override void DrawBack(Painter p)
        {
            if (t < FLASH) return;
            Plume.Shape S = plume.ShapeNow();
            Shock(p, true);
            DrawPs(p, ps, Layer.Back, FR, emitter);
            SkirtDraw(p, t, S, veil, skirt, true);
            plume.Draw(p, FR, CAP);
            // the dark hollow under the cap where the stem goes in, its heart still warm while the inside glows
            double E = plume.Ember(), gone = Smooth(plume.o.fade[0], plume.o.fade[1] - 0.6, t - LIFT);
            if (gone < 1)
            {
                double hy = S.y + S.tube * 0.42, rx = S.ring * 0.7 + 5, ry = S.tube * 0.28 + 2;
                Haze(p, 0, hy, rx, ry, d => d < 0.55 * E ? Mix(CAP[4], FR[d < 0.3 * E ? 5 : 6], 0.75 * E) : CAP[4], d => (d < 0.6 ? 0.8 : d < 0.85 ? 0.5 : 0.22) * (1 - gone));
            }
            column.Draw(p, Top(), FR, DUST, Clamp(1 - t / 1.8), E * 0.8);
            SkirtDraw(p, t, S, veil, skirt, false);
            Pool(p, 0, 1, 140, 16, F[3], 0.3 * plume.Hot());
        }

        public override void Draw(Painter p)
        {
            // the blinding frame: a white-hot core in a wash of white light that fades out before the canvas edges
            if (t < FLASH)
            {
                Wash(p, 0, -40, WHITE, 1.6);
                p.Circle(0, -40, 64, WHITE);
                return;
            }
            Shock(p, false);
            // out of the white: a crisp white-hot sphere, cooling into the fireball
            double f = (t - FLASH) / 0.13;
            if (f < 1)
            {
                double r = 22 + 8 * f;
                p.Circle(0, -26, r + 1.5, F[2]);
                p.Circle(0, -26, r, f < 0.5 ? F[6] : F[5]);
                p.Circle(0, -26, r * 0.62, f < 0.5 ? WHITE : F[6]);
            }
            DrawPs(p, ps, Layer.Front, FR, emitter);
            // the shock in the air: a thin bright shell racing out from the fireball, cut off by the floor and the canvas
            double u = (t - FLASH) / 0.7;
            if (u < 1)
            {
                double r = 30 + 250 * Ease.OutCubic(u), cy = -26;
                bool Out(double a) => cy + Px.Sin(a) * r > 0 || Math.Abs(Px.Cos(a) * r) > OX - 3 || cy + Px.Sin(a) * r < 3 - OY;
                p.Ring(0, cy, r, Math.Max(1, 2.5 * (1 - u)), u < 0.3 ? WHITE : F[6], (v, a) => Out(a) ? 0 : 1.1 - u);
                GlowDome(p, 0, cy, r, 7, F[5], 0.3 * (1 - u), new double[] { 3 - OX, 3 - OY, W - OX - 4, 0 });
            }
            // the light of it: everything washed white, falling away fast, then the fireball's own warm light
            double w = Exp(-(t - FLASH) / 0.13);
            if (w > 0.02) Wash(p, 0, -40, WHITE, 0.9 * w);
            lights.Draw(p);
        }

        // light over most of the canvas, brightest near (cx, cy), falling to nothing just inside the canvas edges so it
        // never shows the canvas as a box
        static void Wash(Painter p, double cx, double cy, uint c, double k)
        {
            double rx = Math.Min(OX + cx, W - OX - cx) - 4, up = OY + cy - 4, down = H - OY - cy - 4;
            p.Shade(-OX, -OY, W - OX - 1, H - OY - 1, (x, y, o) =>
            {
                double d = Hypot((x - cx) / rx, (y - cy) / (y < cy ? up : down));
                o.l = c;
                o.k = k * (1 - Smooth(0, 1, d));
                return 0;
            });
        }

        // the condensation skirt: a white veil that flashes over the cap and a bell that flares round the stem, edged with a
        // crisp white rim, then evaporates into wisps that drift apart and thin; far half behind the cloud, near in front
        static void SkirtDraw(Painter p, double t, Plume.Shape S, Veil[] veil, Skirt[] skirt, bool back)
        {
            double u = (t - SKIRT0) / (SKIRT1 - SKIRT0);
            if (u <= 0 || u >= 1) return;
            double a = Math.Min(1, u / 0.06) * Px.Pow(1 - u, 1.5) * 0.6, capR = (S.ring + S.tube) * 1.1, y0 = S.y + S.tube * 0.3, y1 = S.y * 0.55;
            if (!back)
                foreach (Veil q in veil)
                {
                    double ev = u * u * q.e, R = capR * q.w * (1 + ev * 0.2);
                    p.Disc(Px.Cos(q.a) * R + q.dx * ev * 10, S.y + Px.Sin(q.a) * (S.tube * 1.15 + 6) * q.w + q.dy * ev * 8, q.s * (1 - 0.5 * u), WHITE, a * 0.38);
                }
            foreach (Skirt q in skirt)
            {
                double z = Px.Sin(q.ph);
                if ((z < 0) != back) continue;
                double R = Lerp(capR * 0.82, 13, Px.Pow(q.v, 0.6)) * q.w, ev = u * u * q.e;
                double x = Px.Cos(q.ph) * R * (1 + ev * 0.25) + q.dx * ev * 10, y = Lerp(y0, y1, q.v) + z * R * 0.18 + q.dy * ev * 8;
                p.Disc(x, y, q.s * (1 - 0.55 * u), WHITE, a * (back ? 0.3 : 0.55) * (1 - q.v * 0.3));
            }
            // the bell's crisp lower edge, dithering away as it evaporates
            if (u < 0.7)
            {
                double R = 13 * 1.25, k = 1 - u / 0.7;
                for (int i = 0; i < 64; i++)
                {
                    double g = (i / 64.0) * TAU, z = Px.Sin(g);
                    if ((z < 0) != back) continue;
                    p.Dot(Px.Cos(g) * R * (1 + u * 0.6), y1 + 2 + z * R * 0.22, WHITE, k * (back ? 0.6 : 1));
                }
            }
        }
    }
}
