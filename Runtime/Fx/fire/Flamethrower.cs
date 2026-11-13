// The flame-puff, smoke and spark recipe is adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.FireLib;

namespace Pixelfx
{
    public sealed class Flamethrower : Effect
    {
        public static readonly Module Module = new Module("flamethrower", "pixelfx/fx/fire/flamethrower", 184, 104, 30, 60, 1.6, true, 30,
            new Options
            {
                ["palette"] = new[] { "#4a1712", "#8a1f17", "#c9281f", "#f2561d", "#ff8a2a", "#ffc23d", "#ffe26b", "#fff3a6", "#ffffff" },
                ["pilot"] = new[] { "#1b2a8a", "#3a62e0", "#a8d4ff" },
                ["reach"] = 112,
                ["speed"] = 280,
                ["seed"] = 3,
            },
            (o, e) => new Flamethrower(o))
        {
            Title = "Flamethrower",
            Desc = "A pressurised jet shooting right: a blue pilot at the nozzle, a tight stream with surges racing down it, a sputter, burning drops falling off, a tip that curls up into smoke.",
            Tags = new[] { "fire", "flamethrower", "stream", "weapon", "directional", "loop" },
            Bg = "dark",
        };

        const double D = 1.6;
        static readonly LoopNoise NL = NoiseFor(D);
        static readonly uint SOOT = Hex("#2a2430");
        // the sputter: the jet coughs once a loop, then surges back
        const double COUGH = 0.55;

        static double Flow(double te)
        {
            double u = ((te % D) + D) % D;
            return 1 - Smooth(COUGH, COUGH + 0.04, u) * (1 - Smooth(COUGH + 0.1, COUGH + 0.16, u)) + 0.25 * Exp(-Px.Pow((u - COUGH - 0.2) / 0.05, 2));
        }

        readonly Options o;

        Flamethrower(Options o) => this.o = o;

        public override void Draw(Painter p, double t)
        {
            uint[] F = Fit(o.Ramp("palette"), 9), H = HotFirst(F), PL = Fit(o.Ramp("pilot"), 3);
            double S = o.Seed, L = o.Num("reach"), V = o.Num("speed");
            FrameNoise N = (rate, seed, off) => NL(t, rate, seed, off);
            uint GLOW = Mix(Mix(H[3], H[4], 0.5), WHITE, 0.25), SMOKE = Mix(SOOT, H[7], 0.3);
            uint[] SPARKS = { H[1], H[2], H[4], H[5] };
            // the stream's centre line: dead straight at the nozzle, sagging and wavering as it runs out
            double Cy(double x) => 5 * Px.Pow(x / L, 2) + N(1.2, S + 9, x * 0.02) * 2.2 * Px.Pow(x / L, 1.5);
            double Width(double x) => (2.1 + 5 * Px.Pow(x / L, 0.7)) * Flow(t - x / V);

            // the curl: where the stream runs out of push, burning fuel rolls up and back on itself and turns to smoke
            var fire = new List<(double u, double x, double y, Q q, double k)>();
            var smoke = new List<(double u, double x, double y, Q q)>();
            Looped(t, D, 80, S + 100, (r, j) => new Q
            {
                a0 = r.Range(-0.3, 0.5), R = r.Range(9, 15), w = r.Range(5, 7.5), up = r.Range(4, 12), dx = r.Range(-6, 2), fl = r.Range(0.38, 0.62), sl = r.Range(0.6, 1.0), s = r.Range(2.4, 4.4), f = r.Next(), life = 1.6,
            }, (q, age, j) =>
            {
                double tb = t - age, k = Flow(tb - L / V);
                if (k < 0.4) return;
                // round a curl: forward, up and back over the top, rising as it goes
                double x0 = L + q.dx, y0 = Cy(L), a = Math.Min(age, q.fl) * q.w + q.a0;
                double x = x0 + q.R * Px.Sin(a), y = y0 - q.R * (1 - Px.Cos(a)) - q.up * age;
                if (age < q.fl)
                {
                    fire.Add((age / q.fl, x, y, q, k));
                    return;
                }
                if (age - q.fl < q.sl) smoke.Add(((age - q.fl) / q.sl, x - (age - q.fl) * 6, y - (age - q.fl) * 16, q));
            });
            foreach (var (u, x, y, q) in smoke) SmokePuff(p, x, y, 1 - u, q.s * 1.2, SMOKE, true, 0.85);
            foreach (var (u, x, y, q, k) in fire.OrderByDescending(f => f.u)) FirePuff(p, x, y, 1 - u, q.s * (0.8 + 1.1 * u) * k, q.f, H, 0.1);

            // flames licking up off the burning fuel, riding along the stream, swept back by the wind of it
            for (int i = 0; i < 13; i++)
            {
                double n = (S * 3 + i) * 19, ph = (i * 0.618 + t / D) % 1, x = 10 + ph * (L - 14), k = Flow(t - x / V);
                if (k < 0.35) continue;
                double h = (2 + 11 * (x / L)) * (0.55 + 0.6 * (0.5 + 0.5 * N(5, n + 1))) * k * Math.Min(1, (1 - ph) * 6);
                FlameTongue(p, x, Cy(x) - Width(x) * 0.6, h, 1.1 + 1.6 * (x / L), -h * 0.55, N, n, H, LAYERS);
            }
            // the stream: a white core in yellow, orange and a red skin, surges racing down it
            double P = Round(D * 3);
            uint[] lvls = { H[0], H[2], H[4], H[6], H[7] };
            for (double x = 0; x <= L; x++)
            {
                double c = Cy(x), w = Width(x) * (1 + 0.15 * PNoise(x * 0.4 - (t / D) * 24, 24, S + 4));
                if (w < 0.4) continue;
                int surge = ((x * 0.035 - (t / D) * P) % 1 + 1) % 1 < 0.12 ? 1 : 0;
                for (double y = Math.Floor(c - w - 1); y <= Math.Ceiling(c + w + 1); y++)
                {
                    double q = Math.Abs(y + 0.5 - c) / w;
                    if (q > 1.15) continue;
                    // a white core near the nozzle cooling to yellow down the stream; ragged red skin
                    int lvl = (q < 0.3 ? 0 : q < 0.55 ? 1 : q < 0.78 ? 2 : q < 1 ? 3 : 4) + (x > L * 0.45 && q < 0.55 ? 1 : 0);
                    lvl = Math.Max(0, Math.Min(4, lvl - surge));
                    if (lvl == 4 && PNoise(x * 0.5 + y * 7.3 - (t / D) * 40, 40, S + 6) < 0) continue;
                    p.Px(x, y, lvls[lvl]);
                }
                // two hot strands twisting through the fuel, racing downstream
                foreach (double ph in new[] { 0, Math.PI })
                {
                    double a = x * 0.22 - (t / D) * Math.PI * 2 * 9 + ph, sy = c + Px.Sin(a) * w * 0.55;
                    if (Px.Cos(a) > -0.1 && w > 1.2) p.Px(x, sy, Px.Cos(a) > 0.55 ? H[0] : H[1]);
                }
            }
            // the pilot at the nozzle: a blue cone of gas where it lights
            double pk = Flow(t);
            for (double x = 0; x < 6; x++)
                for (double y = -2; y <= 2; y++)
                    if (Math.Abs(y) <= 2 - x * 0.3) p.Px(x, y, Math.Abs(y) < 1 ? (x < 2 ? PL[2] : PL[1]) : PL[0]);
            Solid(p, 1, 0, 1.6 * pk, WHITE);

            // burning drops shaken off the stream: they arc down trailing flame and burn out
            Looped(t, D, 10, S + 200, (r, j) => new Q { d = r.Range(20, 95), vx = r.Range(0.3, 0.6), vy = r.Range(-25, 15), life = r.Range(0.35, 0.6) }, (q, age, j) =>
            {
                double tb = t - age;
                if (Flow(tb - q.d / V) < 0.5) return;
                (double x, double y, double vx, double vy) At(double a) => Drift(q.d, Cy(q.d), V * q.vx, q.vy, a, 0, 260, 1.2, 0.5);
                for (int k = 4; k >= 1; k--)
                {
                    double a = age - k * 0.035;
                    if (a < 0) continue;
                    var at = At(a);
                    FirePuff(p, at.x, at.y, 1 - Math.Min(0.99, k * 0.18 + age / q.life * 0.4), 1.8 - k * 0.25, 0.3, H, 0.06);
                }
                var e = At(age);
                double kk = 1 - age / q.life;
                p.Px(e.x, e.y, kk > 0.5 ? H[0] : H[2]);
                p.Px(e.x + 1, e.y, H[3]);
                p.Glow(e.x, e.y, 6, H[3], 0.45 * kk);
            });
            // the cough: when the jet sputters the nozzle spits sparks and a puff of smoke
            Looped(t, D, 12, S + 300, (r, j) => new Q { born = COUGH + r.Range(0, 0.08), a = r.Range(-0.8, 0.8), v = r.Range(120, 240), life = r.Range(0.2, 0.45) }, (q, age, j) =>
            {
                double u = ((t - age) % D + D) % D;
                if (Math.Abs(u - q.born) > 0.06) return;
                var d = Drift(4, 0, Px.Cos(q.a) * q.v, Px.Sin(q.a) * q.v, age, 0, 300, 2.5, 2.5);
                Spark(p, d.x, d.y, d.vx, d.vy, 1 - age / q.life, SPARKS);
            });
            // and spits a few blobs of burning fuel that fall short
            Looped(t, D, 5, S + 350, (r, j) => new Q { born = COUGH + r.Range(0.01, 0.1), v = r.Range(90, 170), vy = r.Range(-30, 10), life = r.Range(0.25, 0.4), s = r.Range(1.6, 2.6) }, (q, age, j) =>
            {
                double u = ((t - age) % D + D) % D;
                if (Math.Abs(u - q.born) > 0.05) return;
                var d = Drift(4, 0, q.v, q.vy, age, 0, 200, 2, 1);
                FirePuff(p, d.x, d.y, 1 - age / q.life, q.s, 0.2, H, 0.1, -0.86, -0.5);
            });
            {
                double u = ((t % D) + D) % D, a = u - COUGH;
                if (a > 0 && a < 0.7) SmokePuff(p, 8 + a * 20, -a * 18, 1 - a / 0.7, 3, SMOKE, false, 0.8);
            }

            // light: the pilot, the stream, the burning tip
            p.Glow(2, 0, 34 * pk, GLOW, 0.8 * pk);
            p.Glow(2, 0, 9, PL[2], 0.45 * pk);
            for (double d = 8; d <= L; d += 8) p.Glow(d, Cy(d), 12 + d * 0.16, GLOW, 0.3 * Flow(t - d / V));
            p.Glow(L, Cy(L) - 10, 52, GLOW, 0.34 * Flow(t - L / V));
        }
    }
}
