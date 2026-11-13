// The gathering motes, star particles and burst flare follow Sprite Fusion's open-source energy bolt
// (https://www.spritefusion.com/pixel-art-vfx/energy), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class MagicMissile : Effect
    {
        public static readonly Module Module = new Module("magic-missile", "pixelfx/fx/projectiles/magic-missile", 208, 112, 24, 104, 1.9, false, 30,
            new Options
            {
                ["palette"] = new[] { "#140a26", "#2b1252", "#481c88", "#6b2cc0", "#9a48ec", "#c684ff", "#ecc8ff", "#ffffff" },
                ["height"] = 26,
                ["reach"] = 112,
                ["seed"] = 6,
            },
            (o, e) => new MagicMissile(o, e))
        {
            Title = "Magic missile",
            Desc = "Motes gather into an arcane orb at a hand `height` px above the floor (the origin); it weaves to its target trailing a twisting ribbon and bursts into a star of light.",
            Tags = new[] { "projectile", "magic", "arcane", "missile", "spell", "directional", "one-shot" },
            Bg = "ground",
        };

        const double CHARGE = 0.2, FLY = 0.62;

        sealed class Mark
        {
            public double x, y, t;
        }

        readonly uint[] C;
        readonly double H, X1;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly List<Mark> trail = new List<Mark>();
        double t;
        bool burst;

        // the path: out of the hand, weaving up and down twice, homing in on the target at the same height
        (double x, double y) Pos(double u) => (X1 * Ease.InOutSine(u) + Px.Sin(u * Math.PI) * 6, -H + Px.Sin(u * TAU * 1.5) * 9 * Px.Sin(u * Math.PI));

        MagicMissile(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            H = o.Num("height");
            X1 = o.Num("reach");
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < CHARGE)
            {
                // motes spiral in to the hand
                if (R.Chance(0.8))
                {
                    double a = R.Range(0, TAU), d = R.Range(12, 26), sp = d / 0.14;
                    ps.Spawn(P.Mote, Px.Cos(a) * d, -H + Px.Sin(a) * d, -Px.Cos(a) * sp - Px.Sin(a) * sp * 0.5, -Px.Sin(a) * sp + Px.Cos(a) * sp * 0.5, 0.14, R.Pick(new[] { C[5], C[6], WHITE }), 0.8);
                }
            }
            else if (t < CHARGE + FLY)
            {
                double u = (t - CHARGE) / FLY;
                var (x, y) = Pos(u);
                trail.Add(new Mark { x = x, y = y, t = t });
                if (R.Chance(0.6)) ps.Spawn(P.Mote, x, y, R.Range(-50, 50), R.Range(-50, 50), R.Range(0.2, 0.4), R.Pick(new[] { C[4], C[5], C[6] }), 0.7);
                if (R.Chance(0.25)) ps.Spawn(P.Star, x + R.Range(-3, 3), y + R.Range(-3, 3), R.Range(-10, 10), R.Range(-10, 10), R.Range(0.25, 0.5), C[6], R.Range(0.6, 1.6));
            }
            else if (!burst)
            {
                burst = true;
                double y = -H;
                lights.Add(X1, y, 52, C[4], 0.95, 0.55);
                lights.Add(X1, y, 22, WHITE, 0.8, 0.12);
                for (int i = 0; i < 26; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(60, 150) * (1 - 0.65 * Math.Max(0, Px.Sin(a)));
                    ps.Spawn(i % 3 != 0 ? P.Mote : P.Star, X1, y, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.25, 0.45), R.Pick(new[] { C[5], C[6], WHITE }), R.Range(0.8, 1.8));
                }
                for (int i = 0; i < 18; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(110, 240) * (1 - 0.5 * Math.Max(0, Px.Sin(a)));
                    ps.Spawn(P.Spark, X1, y, Px.Cos(a) * sp, Px.Sin(a) * sp - 30, R.Range(0.12, 0.3));
                }
                for (int i = 0; i < 14; i++)
                {
                    double a = R.Range(0, TAU), d = R.Range(4, 22);
                    ps.Spawn(P.Star, X1 + Px.Cos(a) * d, y + Px.Sin(a) * d * 0.8, 0, -R.Range(2, 8), R.Range(0.5, 1.1), R.Pick(new[] { C[6], WHITE, C[5] }), R.Range(0.8, 2.6), R.Range(0.05, 0.45));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            // the ribbon: a strip that twists as it trails the orb, its bright face and its dark back taking turns,
            // fading from the tail
            double now = t;
            var keep = trail.FindAll(q => now - q.t < 0.32);
            for (int i = 1; i < keep.Count; i++)
            {
                Mark a = keep[i - 1], b = keep[i];
                double dx = b.x - a.x, dy = b.y - a.y, l = Hypot(dx, dy);
                if (l == 0 || double.IsNaN(l)) l = 1;
                double nx = -dy / l, ny = dx / l;
                for (double f = 0; f < 1; f += 0.5 / l)
                {
                    double tt = a.t + (b.t - a.t) * f, k = 1 - (now - tt) / 0.32, tw = Px.Sin(tt * 26), w = 3.2 * k * Math.Abs(tw) + 0.4;
                    bool face = tw > 0;
                    double x = a.x + dx * f, y = a.y + dy * f;
                    for (double s = -w; s <= w; s += 0.5)
                    {
                        bool edge = Math.Abs(s) > w - 0.8;
                        uint c = face ? (edge ? C[6] : k > 0.6 ? C[5] : C[4]) : edge ? C[4] : C[2];
                        p.Dot(x + nx * s, y + ny * s, c, k > 0.35 ? 1 : k / 0.35);
                    }
                }
                if (i % 3 == 0) p.Glow(b.x, b.y, 6, C[4], 0.25 * (1 - (now - b.t) / 0.32));
            }
            ps.Draw(p);
            // the orb: gathering at the hand, then flying, its heart white, a ring of light turning round it
            if (t < CHARGE + FLY)
            {
                double k = t < CHARGE ? t / CHARGE : 1;
                var (x, y) = t < CHARGE ? (0.0, -H) : Pos((t - CHARGE) / FLY);
                double r = 1 + 3 * Ease.OutBack(k);
                p.Circle(x, y, r + 1.4, C[3]);
                p.Circle(x, y, r + 0.4, C[5]);
                p.Circle(x - 0.4, y - 0.4, r * 0.55, WHITE);
                for (int i = 0; i < 3; i++)
                {
                    double a = t * 14 + (i * TAU) / 3;
                    p.Px(x + Px.Cos(a) * (r + 3), y + Px.Sin(a) * (r + 3) * 0.6, i != 0 ? C[6] : WHITE);
                }
                p.Glow(x, y, 20, C[4], 0.65 * k);
                p.Glow(x, y, 7, C[6], 0.5 * k);
            }
            // the burst: a star flare and a ring that open out and fade
            double q = t - CHARGE - FLY;
            if (q >= 0 && q < 0.5)
            {
                double e = q / 0.5;
                if (e < 0.5) Sim.Flare(p, X1, -H, 26 * Px.Pow(1 - e * 2, 1.2) + 2, 1 - e * 2, C);
                if (e < 0.12) p.Circle(X1, -H, 6 * (1 - e / 0.12) + 1, WHITE);
                p.Ring(X1, -H, 4 + 20 * Ease.OutCubic(e), Math.Max(1, 2.5 * (1 - e)), e < 0.3 ? C[6] : C[5], Math.Min(1, (1 - e) * 1.5));
                if (e < 0.4) Sim.Sparkle(p, X1, -H, 7 * (1 - e / 0.4), C[6], C[4], 0.6 * (1 - e / 0.4));
            }
            lights.Draw(p);
        }
    }
}
