// The spark and fire-ball recipes are adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.ProjectilesLib;

namespace Pixelfx
{
    public sealed class Rocket : Effect
    {
        public static readonly Module Module = new Module("rocket", "pixelfx/fx/projectiles/rocket", 288, 76, 252, 42, 1.2, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["metal"] = new[] { "#0f0d16", "#25243a", "#3e4058", "#61667f", "#8e97b0", "#c4cce0", "#f2f6ff" },
                ["paint"] = new[] { "#3a0d14", "#7c1a22", "#bd2f2c", "#ec5a45", "#ff9b7e" },
                ["smoke"] = new[] { "#26222e", "#3d3846", "#5a5463", "#7e7887", "#a7a1ad", "#d0cad2", "#f1ecee" },
                ["speed"] = 120,
                ["seed"] = 5,
            },
            (o, e) => new Rocket(o))
        {
            Title = "Rocket",
            Desc = "A steel rocket roars along: a flickering jet with shock diamonds and a white-hot nozzle, a thick billowing smoke trail lit by the flame, sparks shed behind.",
            Tags = new[] { "projectile", "rocket", "missile", "smoke", "fire", "directional", "loop" },
            Bg = "sky",
        };

        const double D = 1.2;
        const int OX = 252;
        static readonly uint OUTLINE = Hex("#100d17");
        const double NOZZLE = -14; // the bell's exit: the jet starts here
        const double J0 = 22; // the jet's mean length

        static readonly Dictionary<int, char> BODY = new Dictionary<int, char> { [-3] = '4', [-2] = '6', [-1] = '5', [0] = '4', [1] = '3', [2] = '2', [3] = '1' };
        static readonly Dictionary<int, char> SEAM = new Dictionary<int, char> { [-3] = '3', [-2] = '4', [-1] = '4', [0] = '3', [1] = '2', [2] = '1', [3] = '1' };
        static readonly Dictionary<int, char> PAINT = new Dictionary<int, char> { [-3] = 'd', [-2] = 'e', [-1] = 'e', [0] = 'd', [1] = 'c', [2] = 'b', [3] = 'a' };

        // the rocket: a steel body lit from above with a specular line, a painted ogive nose and band, swept fins with
        // lit leading edges, a dark bell with a glowing lip
        static readonly (string[] rows, double x0, double y0) SHIP = PixelShape(-15, -9, 30, 19, set =>
        {
            for (int y = -3; y <= 3; y++)
                for (int x = -11; x <= 5; x++) set(x, y, BODY[y]);
            for (int y = -3; y <= 3; y++)
            {
                set(-3, y, SEAM[y]);
                set(-9, y, PAINT[y]);
                set(-8, y, PAINT[y]);
            }
            int[][] nose = { new[] { 6, 3 }, new[] { 7, 3 }, new[] { 8, 2 }, new[] { 9, 2 }, new[] { 10, 1 }, new[] { 11, 1 }, new[] { 12, 0 }, new[] { 13, 0 } };
            foreach (int[] n in nose)
                for (int y = -n[1]; y <= n[1]; y++) set(n[0], y, PAINT[y]);
            set(7, -2, 'w');
            set(8, -2, 'w');
            set(9, -1, 'e');
            set(13, 0, 'e');
            // fins, the top one in the light, the bottom one in shadow; each leading edge catches a highlight
            int[][] fins = { new[] { -13, -8 }, new[] { -13, -9 }, new[] { -13, -10 }, new[] { -13, -11 } };
            for (int i = 0; i < fins.Length; i++)
            {
                int a = fins[i][0], b = fins[i][1];
                for (int x = a; x <= b; x++)
                {
                    set(x, -4 - i, x == b ? '5' : i > 1 ? '4' : '3');
                    set(x, 4 + i, x == b ? '3' : i > 1 ? '1' : '2');
                }
            }
            // the bell: a throat and a flared exit, its lip glowing
            for (int y = -2; y <= 2; y++) set(-12, y, y < 0 ? '3' : '2');
            for (int y = -3; y <= 3; y++) set(-13, y, y < -1 ? '2' : '1');
            set(-14, -3, 'h');
            set(-14, 3, 'h');
            for (int y = -2; y <= 2; y++) set(-14, y, Math.Abs(y) < 2 ? 'x' : 'h');
        });

        // the jet: a cone of flame out of the bell in clean bands (a red rim, orange, a gold core), white-hot at the
        // nozzle, with a chain of shock diamonds down its middle; fr changes every frame so it flickers
        static void Jet(Painter p, uint[] F, double x0, double y0, double J, double wob, double fr)
        {
            var dia = new double[4];
            for (int i = 0; i < 4; i++) dia[i] = 3.6 + i * 4.3 + (Rand(fr, i, 41) - 0.5) * 0.7;
            p.Shade(x0 - J - 2, y0 - 6, x0, y0 + 6, (x, y, ou) =>
            {
                double u = x0 - x;
                if (u < 0) return 0;
                double q = u / J;
                if (q > 1.05) return 0;
                double ady = Math.Abs(y - y0), qq = Math.Min(q, 1), w = (2.6 + 2.4 * Px.Pow(qq, 0.7)) * (1 - qq * qq) * wob;
                double m = w - ady + (Rand(Math.Floor(x) * 7 + Math.Floor(y) * 131, fr) - 0.5) * 0.9 * Math.Min(1, 0.25 + q * 2);
                if (m < -0.5) return 0;
                if (m < 0.5) return F[q < 0.45 ? 3 : 2];
                for (int i = 0; i < dia.Length; i++)
                {
                    if (dia[i] > J * 0.72) break;
                    double d = Math.Abs(u - dia[i]) / (1.9 - i * 0.15) + ady / (1.7 - i * 0.25);
                    if (d < 1) return d < 0.55 && i < 3 ? F[8] : F[7];
                }
                if (u < 1.6 && ady < 1.6) return F[8];
                double lvl = (1 - ady / Math.Max(0.6, w)) * (1 - Px.Pow(qq, 1.5));
                return lvl > 0.62 ? (u < 3 ? F[7] : F[5]) : lvl > 0.38 ? F[5] : lvl > 0.16 ? F[4] : F[3];
            });
        }

        readonly Options o;

        Rocket(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), M = Fit(o.Ramp("metal"), 7), A = Fit(o.Ramp("paint"), 5), SM = Fit(o.Ramp("smoke"), 7);
            double S = o.Seed, V = o.Num("speed");
            int fr = (int)Math.Floor(t * 30 + 1e-6);
            double Bob(double tt) => Round(Px.Sin((tt / D) * TAU) * 0.62);
            double by = Bob(t);
            double J = J0 * (0.84 + 0.32 * Rand(fr, S, 1)), wob = 0.92 + 0.16 * Rand(fr, S, 2), fl = J / J0;
            double left = -OX + 2;
            uint[] SP = { F[7], F[6], F[4], F[3] };

            // smoke: puffs leave the tail of the flame, stream back, swell and rise, lit by the fire while young
            var list = new List<Puff>();
            Beat(t, D, 66, S + 1, r => r.Range(1.1, 1.45), (age, life, r, i) =>
            {
                var (x, y, _, _) = Wake(NOZZLE - J0 * r.Range(0.62, 0.8), Bob(t - age) + r.Range(-1, 1), -r.Range(75, 105), r.Range(-20, 20), age, V, 3, 0);
                double k = age / life, rise = 0.5 * r.Range(6, 16) * age * age, sway = Px.Sin(age * 2.3 + r.Range(0, TAU)) * age * 2;
                double rad = r.Range(2.2, 3) + ((r.Chance(0.25) ? r.Range(14, 19) : r.Range(8, 13)) - 2.6) * (1 - Exp(-2.2 * age));
                if (x - rad < left + 1) return;
                double a = Math.Min(1, age * 16) * Px.Pow(1 - k, 1.25) * 0.68 * Smooth(left, left + 40, x - rad);
                list.Add(new Puff { x = x, y = y - rise + sway, r = rad, a = a, b = Clamp(0.95 - k * 0.75 + r.Range(-0.08, 0.08)), w = Exp(-age * r.Range(3.5, 6)) });
            });
            Cloud(p, list, SM, new[] { Mix(F[0], SM[0], 0.5), Mix(F[1], SM[1], 0.45), Mix(F[2], SM[2], 0.35), F[3], F[4], F[6], F[7] }, dens: 1.3, lit: 1.5);
            // the smoke right behind the flame glows from inside
            p.Glow(NOZZLE - J0 - 6, by, 26, F[4], 0.24 * fl);

            // embers carried off in the smoke, cooling and winking out
            Beat(t, D, 9, S + 4, r => r.Range(0.6, 1.1), (age, life, r, i) =>
            {
                double k = 1 - age / life;
                var (x, y, _, _) = Wake(NOZZLE - J0 * 0.6, Bob(t - age), -r.Range(40, 120), r.Range(-40, 30), age, V, 2.5, -8);
                if (x < left + 2 || (k < 0.3 && Rand(fr, r.Int(0, 999)) < 0.5)) return;
                uint c = k > 0.6 ? F[6] : k > 0.3 ? F[4] : F[2];
                p.Px(x, y, c);
                p.Glow(x, y, 3, c, 0.35 * k);
            });

            // tongues of flame torn off the tip of the jet, cooling and shrinking as they fall behind
            Beat(t, D, 30, S + 3, r => r.Range(0.08, 0.16), (age, life, r, i) =>
            {
                double k = age / life;
                var (x, y, _, _) = Wake(NOZZLE - J0 * r.Range(0.6, 0.9), Bob(t - age) + r.Range(-1.5, 1.5), -r.Range(10, 50), r.Range(-12, 12), age, V, 4, -30);
                int idx = 7 - (int)Math.Floor(Px.Pow(k, 0.7) * 4);
                double rad = r.Range(1.2, 2.2) * (1 - k * 0.6);
                p.Circle(x, y, rad, F[idx - 2]);
                p.Circle(x + rad * 0.25, y - rad * 0.1, rad * 0.65, F[idx]);
                p.Glow(x, y, rad * 2.4 + 2, F[idx], 0.2 * (1 - k));
            });

            // sparks shed behind, streaking back and dropping
            Beat(t, D, 30, S + 2, r => r.Range(0.18, 0.45), (age, life, r, i) =>
            {
                var (x, y, vx, vy) = Wake(NOZZLE - r.Range(2, J0 * 0.7), Bob(t - age) + r.Range(-1.5, 1.5), -r.Range(20, 190), r.Range(-80, 50), age, V, 2.6, 160);
                if (x > left + 10) Spark(p, x, y, vx, vy, 1 - age / life, SP);
            });
            Beat(t, D, 8, S + 6, r => r.Range(0.5, 0.8), (age, life, r, i) =>
            {
                var (x, y, vx, vy) = Wake(NOZZLE - r.Range(4, J0 * 0.8), Bob(t - age) + r.Range(-1, 1), -r.Range(10, 80), r.Range(-60, 10), age, V, 1.8, 170);
                if (x > left + 10) Spark(p, x, y, vx, vy, 1 - age / life, SP, 0.5);
            });

            Jet(p, F, NOZZLE, by + 0.5, J, wob, fr + S * 31);
            p.Sprite(SHIP.rows, new Dictionary<char, uint>
            {
                ['o'] = OUTLINE, ['1'] = M[1], ['2'] = M[2], ['3'] = M[3], ['4'] = M[4], ['5'] = M[5], ['6'] = M[6],
                ['a'] = A[0], ['b'] = A[1], ['c'] = A[2], ['d'] = A[3], ['e'] = A[4], ['w'] = WHITE, ['h'] = F[5], ['x'] = F[8],
            }, SHIP.x0, SHIP.y0 + by);

            // light: a warm pool round the tail that breathes with the jet, the flame's own glow, a white-hot nozzle
            p.Glow(NOZZLE - 10, by, 60, F[4], 0.28 * fl);
            p.GlowLine(NOZZLE - 3, by + 0.5, NOZZLE - J * 0.9, by + 0.5, 11, F[4], 0.2);
            p.Glow(NOZZLE - 1, by + 0.5, 8, F[7], 0.45);
            p.Glow(NOZZLE + 3, by + 5, 12, F[4], 0.22 * fl); // the flame lights the tail from below
        }
    }
}
