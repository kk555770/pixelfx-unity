// The orb's shading and its glow follow Sprite Fusion's open-source pickup orb
// (https://www.spritefusion.com/pixel-art-vfx/pickup), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.PickupsLib;

namespace Pixelfx
{
    public sealed class XpOrb : Effect
    {
        const double DURATION = 1.8, A = 16, B = 9, HOVER = 20, TAIL = 0.48;
        static readonly uint INK = Hex("#0a1406");

        public static readonly Module Module = new Module("xp-orb", "pixelfx/fx/pickups/xp-orb", 72, 56, 36, 48, DURATION, true, 30,
            new Options
            {
                ["palette"] = new[] { "#0c1806", "#1e3a0a", "#36620e", "#5c9614", "#8cd024", "#c4f060", "#effcc0", "#ffffff" },
                ["seed"] = 2,
            },
            (o, e) => new XpOrb(o, e))
        {
            Title = "XP orb",
            Desc = "A small glowing orb races a figure-eight, a comet tail tapering behind it along its path, flecks shed from the tail winking out, its light following it over the floor.",
            Tags = new[] { "pickup", "xp", "orb", "experience", "energy", "loop" },
            Bg = "ground",
        };

        readonly Options o;

        XpOrb(Options o, Emitter e) => this.o = o;

        // the figure-eight, as a function of time (one lap a loop), with a little lift and fall
        static (double x, double y) OrbAt(double t)
        {
            double a = t / DURATION * TAU;
            return (A * Px.Sin(a), -HOVER + B * Px.Sin(a) * Px.Cos(a) - 1.2 * Px.Cos(a * 2));
        }

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            var (x, y) = OrbAt(t);
            // its shadow and its light slide over the floor beneath it
            BlobShadow(p, x, 3.5, 0.55);
            Pool(p, x, 0.5, 14, 3, C[5], 0.3);
            // flecks shed from the tail drift down and wink out
            Cycle(t, DURATION, 16, o.Seed, g => g.Range(0.35, 0.7), (age, life, r, i) =>
            {
                var (fx, fy) = OrbAt(t - age);
                double k = 1 - age / life, tw = Px.Sin(age * r.Range(20, 30) + r.Range(0, TAU));
                double x2 = fx + r.Range(-2, 2) + age * r.Range(-6, 6), y2 = fy + r.Range(-2, 2) + age * r.Range(4, 10);
                if (tw * k > -0.2) p.Px(x2, y2, tw > 0.6 && k > 0.5 ? WHITE : k > 0.5 ? C[6] : C[5]);
                p.Glow(x2 + 0.5, y2 + 0.5, 3, C[5], 0.3 * k);
            });
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            var (x, y) = OrbAt(t);
            double beat = 0.5 + 0.5 * Px.Sin(t / DURATION * TAU * 3);
            // the tail: the orb's own path just behind it, tapering and cooling down the ramp
            const int n = 60;
            // an outer ribbon cooling down the ramp, then a bright core line down its middle
            for (int i = n; i >= 1; i--)
            {
                double u = (double)i / n, r = 3.1 * Px.Pow(1 - u, 1.1);
                var (tx, ty) = OrbAt(t - u * TAIL);
                uint c = u < 0.25 ? C[5] : u < 0.55 ? C[4] : C[3];
                if (r > 0.6) p.Circle(tx, ty, r, c, u < 0.7 ? 1 : (1 - u) * 3.3);
                else p.Dot(tx, ty, c, (1 - u) * 3);
                if (i % 6 == 0) p.Glow(tx, ty, 7 * (1 - u) + 2, C[5], 0.24 * (1 - u));
            }
            for (int i = (int)Math.Floor(n * 0.6); i >= 1; i--)
            {
                double u = (double)i / n;
                var (tx, ty) = OrbAt(t - u * TAIL);
                p.Dot(tx, ty, u < 0.2 ? WHITE : C[6], (0.6 - u) * 4);
            }
            // the orb: a dark rim, a bright body, a white heart and a highlight, pulsing with light
            double X = Round(x), Y = Round(y);
            p.Circle(X, Y, 4.4, INK);
            p.Circle(X, Y, 3.6, (d, a) => d > 0.72 ? (Px.Cos(a + 2.4) > 0.3 ? C[5] : C[3]) : d > 0.4 ? (Px.Cos(a + 2.4) > -0.2 ? C[6] : C[5]) : WHITE);
            p.Px(X - 2, Y - 2, WHITE);
            p.Px(X - 1, Y - 2, C[7]);
            p.Glow(x, y, 16 + 3 * beat, C[5], 0.36 + 0.14 * beat);
            // a glint now and then
            double g = t / DURATION * 3 % 1;
            if (g < 0.12) Glint(p, X - 2, Y - 2, 2.4 * Px.Sin(g / 0.12 * Math.PI), WHITE, 0.4);
        }
    }
}
