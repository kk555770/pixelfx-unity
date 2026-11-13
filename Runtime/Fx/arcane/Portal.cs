// The dithered light ramp and the streaked, knotted disc texture are adapted from Sprite Fusion's open-source
// black hole (https://www.spritefusion.com/pixel-art-vfx/black-hole), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class Portal : Effect
    {
        const double D = 2.4;

        public static readonly Module Module = new Module("portal", "pixelfx/fx/arcane/portal", 152, 116, 76, 106, D, true, 30,
            new Options { ["palette"] = PAL["arcane"], ["width"] = 17, ["height"] = 29, ["seed"] = 4 },
            (o, e) => new Portal(o))
        {
            Title = "Portal",
            Desc = "An upright rift: fibrous spiral arms wind down into the dark, a lit rim wobbles, motes and streaks are pulled in, the floor glows.",
            Tags = new[] { "arcane", "portal", "rift", "teleport", "magic", "ground", "loop" },
            Bg = "ground",
        };

        static double WrapPi(double a) => a - TAU * Math.Floor((a + Math.PI) / TAU);

        sealed class State
        {
            public uint[] C;
            public double rx, ry, ph, S, CY;
            public double[] spots;

            // the rim's radius (1 = the oval) at angle th: slow noise rolling round it, plus a three-lobed breath
            public double Rim(double th) => 1 + 0.05 * TileNoise((th / TAU) * 10 + ph * 10, ph * 4, 10, 4, S) + 0.018 * Px.Sin(th * 3 - ph * TAU * 2);

            // where a point at angle th and radius rho (1 = the rim) sits; outside, the field widens so motes come from round about
            public (double x, double y) At(double th, double rho)
            {
                double @out = Math.Max(0, rho - 1), k = Math.Min(1, rho);
                return (Px.Cos(th) * (k * rx + @out * 21), Math.Min(-1, CY + Px.Sin(th) * (k * ry + @out * 16)));
            }

            // two hot spots chase round the rim with the spin, and a key light sits at the top left
            public double Hot(double th)
            {
                double w0 = WrapPi(th - spots[0]), w1 = WrapPi(th - spots[1]);
                return Math.Max(Exp(-(w0 * w0) / 0.12), 0.85 * Exp(-(w1 * w1) / 0.08)) + 0.35 * Math.Max(0, Px.Cos(th + 2.3));
            }
        }

        static State Setup(double t, Options o)
        {
            var s = new State { C = Fit(o.Ramp("palette"), 8), rx = o.Num("width"), ry = o.Num("height"), ph = t / D, S = o.Seed };
            s.CY = -(s.ry + 5);
            s.spots = new[] { s.ph * TAU * 2 - 0.6, s.ph * TAU * 2 + Math.PI - 0.6 };
            return s;
        }

        // the rift itself: the dark well, the fibrous arms winding down into it, the lit rim
        static void Rift(Painter p, State s)
        {
            uint[] C = s.C;
            double rx = s.rx, ry = s.ry, ph = s.ph, CY = s.CY;
            p.Shade(-rx - 4, CY - ry - 4, rx + 4, CY + ry + 4, (x, y, @out) =>
            {
                double u = x / rx, v = (y - CY) / ry, r = Hypot(u, v);
                if (r == 0 || double.IsNaN(r)) r = 1e-6;
                double th = Atan2(v, u), R0 = s.Rim(th);
                double g = Hypot(u / rx, v / ry) / r, e = (r - R0) / g, h = s.Hot(th);
                if (e > 2.5) return 0;
                // outside the rim: a coloured line where it's lit, a dark one elsewhere, a faint halo past it
                if (e > 1.5)
                {
                    if (h > 0.6)
                    {
                        @out.c = C[3];
                        @out.a = 0.5;
                    }
                    return 0;
                }
                if (e > 0.5)
                {
                    @out.c = h > 0.5 ? C[4] : C[2];
                    return 0;
                }
                if (e > -0.5)
                {
                    @out.c = h > 0.42 ? WHITE : C[6];
                    @out.k = 0.25 + 0.4 * h;
                    return 0;
                }
                // inside, in ramp steps: clean depth bands falling away to a dark well; three thin bright arms and three
                // faint ones on a log spiral, flowing inward a turn a loop, with knots of light running down them; the light
                // that spills in round the rim
                double d = r / R0, z = -Px.Log(Math.Max(0.02, d)), phi = 3 * th + 6.5 * z - TAU * 3 * ph;
                double c1 = 0.5 + 0.5 * Px.Cos(phi), c2 = 0.5 + 0.5 * Px.Cos(phi + Math.PI + 0.6 * z), lit = Px.Pow(d, 1.5), sink = Smooth(0.2, 0.62, d);
                double knot = Px.Pow(Math.Max(0, Px.Cos(4 * z - phi / 3 + TAU * 5 * ph)), 6);
                double st = Smooth(0.4, 0.7, d) + 0.6 * Smooth(0.8, 0.95, d);
                double crest = Px.Pow(c1, 16) * (0.6 + 5.6 * lit) * (1 + 0.5 * knot) * sink;
                st += crest + Px.Pow(c1, 5) * 1.1 * lit;
                st += Px.Pow(c2, 18) * (0.3 + 1.8 * lit) * sink;
                // rings of the tunnel shrinking away down the well, two a loop
                st += Px.Pow(0.5 + 0.5 * Px.Cos(TAU * (z * 1.5 - ph * 2)), 20) * 1.3 * sink * (1 - 0.5 * c1);
                // fine strands between the arms, a step brighter than the dark they run through
                st += Px.Pow(0.5 + 0.5 * Px.Cos(3 * phi + 2 * z + 1.3), 10) * 1.1 * lit * sink;
                st += 2.2 * Px.Pow(Smooth(0.8, 1, d), 2) * (0.75 + 0.45 * h);
                @out.c = Tone(C, st / 7, x, y, 0, 0.65);
                if (crest > 2.2) @out.k = 0.12 + 0.05 * crest;
                return 0;
            });
        }

        // specks of light deep in the well, swept round with the arms and sinking towards the far end
        static void Well(Painter p, State s)
        {
            uint[] C = s.C;
            double rx = s.rx, ry = s.ry, ph = s.ph, CY = s.CY, t = ph * D;
            Cycle(t, D, 18, s.S * 7 + 5, g => g.Range(1.1, 1.8), (age, life, r, _) =>
            {
                double q = age / life, th = r.Range(0, TAU) + q * 2.6, rho = 0.92 * (1 - Ease.InQuad(q) * 0.9), R0 = s.Rim(th);
                double x = Px.Cos(th) * rho * R0 * rx, y = CY + Px.Sin(th) * rho * R0 * ry, tw = Px.Sin(ph * TAU * r.Int(14, 22) + r.Range(0, TAU));
                double a = Math.Min(1, q * 6) * (rho > 0.25 ? 1 : rho / 0.25);
                if (tw > -0.3) p.Dot(x, y, rho > 0.6 && tw > 0.6 ? WHITE : rho > 0.45 ? C[6] : C[5], a);
            });
        }

        // motes riding round just outside the rim, sinking in as they go
        static void Orbit(Painter p, State s)
        {
            uint[] C = s.C;
            double rx = s.rx, ry = s.ry, ph = s.ph, S = s.S, CY = s.CY;
            for (int i = 0; i < 7; i++)
            {
                double k = (ph * 2 + i / 7.0) % 1, th = ((i * 2.39 + S) % TAU) + ph * TAU * 2 + k * 2.5, rho = 1.2 + 0.35 * ((i * 0.618) % 1) - 0.3 * Ease.InQuad(k);
                double x = Px.Cos(th) * rho * rx, y = CY + Px.Sin(th) * rho * ry, a = Math.Min(1, Px.Sin(k * Math.PI) * 2.2);
                Mote(p, x, y, -Px.Sin(th) * rx * 10, Px.Cos(th) * ry * 10, i % 3 != 0 ? C[6] : WHITE, 0.6, a, 0.3);
            }
        }

        // motes and streaks pulled in from round about, spinning faster as they fall; inside they sink into the dark
        static void Infall(Painter p, State s)
        {
            uint[] C = s.C;
            double ph = s.ph, S = s.S, t = ph * D;
            Cycle(t, D, 60, S * 7 + 1, g => g.Range(0.3, 0.5), (age, life, r, _) =>
            {
                double a0 = r.Range(0, TAU), d0 = r.Range(1.8, 3), spin = r.Range(0.8, 1.8) * (r.Chance(0.8) ? 1 : 0.35), size = r.Range(0.8, 1.5), hue = r.Next();
                (double x, double y) Pos(double qq) => s.At(a0 + spin * qq * qq, d0 * (1 - Px.Pow(qq, 1.4)));
                double q = age / life;
                var (x, y) = Pos(q);
                var (x2, y2) = Pos(Math.Min(1, q + 0.015));
                double vx = (x2 - x) / (0.015 * life), vy = (y2 - y) / (0.015 * life);
                double rho = d0 * (1 - Px.Pow(q, 1.4)), R0 = s.Rim(a0 + spin * q * q), fade = Math.Min(1, q * 5);
                if (rho > R0 + 0.06) Mote(p, x, y, vx, vy, hue < 0.25 ? WHITE : hue < 0.55 ? C[6] : hue < 0.8 ? C[5] : C[4], hue < 0.8 ? size : size * 1.4, fade, hue < 0.8 ? 0.4 : 0.15);
                else if (rho > 0.3) p.Dot(x, y, rho > 0.65 ? C[6] : C[5], rho);
            });
            Cycle(t, D, 12, S * 7 + 2, g => g.Range(0.36, 0.5), (age, life, r, _) =>
            {
                double a0 = r.Range(0, TAU), d0 = r.Range(2.3, 3), spin = r.Range(0.2, 0.5);
                double qc = Math.Sqrt(1 - 1.05 / d0), q = age / life;
                (double x, double y) Pos(double qq) => s.At(a0 + spin * qq * qq, d0 * (1 - qq * qq));
                if (q < qc)
                {
                    var (x, y) = Pos(q);
                    var (x2, y2) = Pos(q + 0.015);
                    Streak(p, x, y, (x2 - x) / (0.015 * life), (y2 - y) / (0.015 * life), C[6], Math.Min(1, q * 5));
                }
                else
                {
                    // it hits the rim: a pinprick of white
                    double k = 1 - (q - qc) / (1 - qc);
                    var (x, y) = Pos(qc);
                    if (k > 0.55) p.Px(x, y, WHITE);
                    p.Glow(x, y, 7, C[6], 0.6 * k * k);
                }
            });
        }

        // grit and sparks skating in along the floor, lifting into the foot of the rift
        static void Skaters(Painter p, State s)
        {
            uint[] C = s.C;
            double ph = s.ph, S = s.S, CY = s.CY, ry = s.ry, t = ph * D, foot = CY + ry;
            Cycle(t, D, 14, S * 7 + 3, g => g.Range(0.7, 1.1), (age, life, r, _) =>
            {
                int side = r.Sign();
                double x0 = side * r.Range(22, 50), y0 = -1 - r.Range(0, 1.5), q = age / life, e = Ease.InCubic(q);
                double x = x0 * (1 - e), y = y0 + (foot + 2) * Smooth(0.55, 1, q);
                double xb = x0 * (1 - Ease.InCubic(Math.Max(0, q - 0.04))), yb = y0 + (foot + 2) * Smooth(0.55, 1, q - 0.04);
                bool big = r.Chance(0.3);
                Mote(p, x, y, (x - xb) * 25, (y - yb) * 25, big ? C[6] : C[5], big ? 0.9 : 0.6, Math.Min(1, q * 4) * (1 - Smooth(0.85, 1, q)), 0.3);
            });
        }

        static void Floor(Painter p, State s)
        {
            uint[] C = s.C;
            double pulse = 0.85 + 0.15 * Px.Sin(s.ph * TAU * 2);
            for (int x = -36; x <= 36; x++)
            {
                double u = Math.Abs(x + 0.5) / 36, a = (u < 0.55 ? 1 : 1 - (u - 0.55) / 0.45) * (0.8 + 0.2 * pulse);
                p.Dot(x, 0, u < 0.2 ? WHITE : u < 0.5 ? C[6] : C[5], a);
                if (u < 0.3) p.Dot(x, 1, u < 0.15 ? C[5] : C[4], u < 0.15 ? 1 : 0.5);
            }
        }

        readonly Options o;

        Portal(Options o) => this.o = o;

        public override void DrawBack(Painter p, double t) => Floor(p, Setup(t, o));

        public override void Draw(Painter p, double t)
        {
            State s = Setup(t, o);
            uint[] C = s.C;
            double CY = s.CY, rx = s.rx, ry = s.ry, ph = s.ph, pulse = 0.85 + 0.15 * Px.Sin(ph * TAU * 2);
            Rift(p, s);
            Well(p, s);
            Orbit(p, s);
            Infall(p, s);
            Skaters(p, s);
            // light comes off the rim, not the void: a big soft pool, a halo hugging the rim, hot spots that shine, the floor lit
            p.Glow(0, CY, ry * 5, C[4], 0.17 * pulse);
            p.Glow(0, CY, ry * 2.6, C[4], 0.1 * pulse);
            for (int i = 0; i < 16; i++)
            {
                double th = (i / 16.0) * TAU, R0 = s.Rim(th) * 1.08;
                p.Glow(Px.Cos(th) * R0 * rx, CY + Px.Sin(th) * R0 * ry, 15, C[4], 0.15 * pulse);
            }
            foreach (double th in s.spots) p.Glow(Px.Cos(th) * s.Rim(th) * rx, CY + Px.Sin(th) * s.Rim(th) * ry, 16, C[6], 0.42);
            for (int i = -3; i <= 3; i++) p.Glow(i * 7, -1, 16, C[5], 0.12 * pulse);
        }
    }
}
