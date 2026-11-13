using System;
using static Pixelfx.Px;
using static Pixelfx.HolyLib;

namespace Pixelfx
{
    public sealed class FeatherBurst : Effect
    {
        public static readonly Module Module = new Module("feather-burst", "pixelfx/fx/holy/feather-burst", 160, 160, 80, 60, 3, false, 30,
            new Options { ["palette"] = new[] { "#4a4560", "#7a7494", "#aaa5c2", "#d6d3e6", "#f4f3fa", "#ffffff" }, ["glow"] = "#fff0c0", ["seed"] = 4 },
            (o, e) => new FeatherBurst(o, e))
        {
            Title = "Feather burst",
            Desc = "A white puff pops and throws feathers that brake hard, then rock from side to side as they sink and fade, with down drifting after them.",
            Tags = new[] { "holy", "feather", "burst", "poof", "angel", "bird", "one-shot" },
            Bg = "dark",
        };

        sealed class Lobe
        {
            public double x, y, r0, r1, life;
        }

        readonly uint[] F;
        readonly uint G;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Feathers loose;
        readonly Lobe[] lobes;
        double t;

        FeatherBurst(Options o, Emitter e)
        {
            F = Fit(o.Ramp("palette"), 6);
            G = o.Color("glow");
            R = new Rng(o.Seed);
            ps = new Particles(R, ground: x => double.PositiveInfinity, sparks: new[] { WHITE, F[4], F[3], F[2] }, emitter: e);
            lights = new Lights(e);
            loose = new Feathers(R, 1e6);
            // the puff: soft lobes thrown out from the pop, swelling and thinning
            lobes = new Lobe[11];
            for (int i = 0; i < 11; i++)
            {
                double a = (i / 11.0) * TAU + R.Range(-0.25, 0.25), d = R.Range(5, 14);
                var l = new Lobe { x = Px.Cos(a) * d, y = Px.Sin(a) * d * 0.8 };
                l.r0 = R.Range(2.5, 4);
                l.r1 = R.Range(6, 9.5);
                l.life = R.Range(0.45, 0.8);
                lobes[i] = l;
            }
            for (int i = 0; i < 20; i++)
            {
                double a = (i / 20.0) * TAU + R.Range(-0.15, 0.15), sp = R.Range(110, 230), up = Px.Sin(a) < 0 ? 0.7 : 0.9;
                var f = new Feathers.Spec { x = Px.Cos(a) * 3, y = Px.Sin(a) * 3, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp * up - 20 };
                f.a = a + R.Range(-0.5, 0.5);
                f.len = R.Range(10, 14);
                f.wid = R.Range(2.6, 3.4);
                f.life = R.Range(1.9, 2.6);
                f.delay = R.Range(0, 0.03);
                f.sink = R.Range(26, 38);
                f.amp = R.Range(8, 13);
                loose.Add(f);
            }
            // small feathers and down, slower
            for (int i = 0; i < 10; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(50, 130);
                var f = new Feathers.Spec { x = 0, y = 0, vx = Px.Cos(a) * sp, vy = Px.Sin(a) * sp - 20 };
                f.a = R.Range(0, TAU);
                f.len = R.Range(6, 8);
                f.wid = R.Range(1.8, 2.2);
                f.life = R.Range(1.6, 2.4);
                f.back = true;
                f.sink = R.Range(18, 28);
                loose.Add(f);
            }
            for (int i = 0; i < 26; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(30, 140);
                ps.Spawn(P.Flake, R.Range(-3, 3), R.Range(-3, 3), Px.Cos(a) * sp, Px.Sin(a) * sp - 15, R.Range(1.4, 2.6), R.Chance(0.6) ? WHITE : F[4], R.Range(0.6, 1.6));
            }
            for (int i = 0; i < 10; i++)
            {
                double a = R.Range(0, TAU), sp = R.Range(60, 160);
                ps.Spawn(P.Star, 0, 0, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.3, 0.6), R.Chance(0.5) ? WHITE : G, R.Range(0.8, 2.2));
            }
            lights.Add(0, 0, 50, G, 0.75, 0.45);
            lights.Add(0, 0, 20, WHITE, 0.7, 0.1);
        }

        void Puff(Painter p)
        {
            foreach (Lobe l in lobes)
            {
                double u = t / l.life;
                if (u >= 1) continue;
                double e = Ease.OutCubic(u), x = l.x * (0.4 + e * 1.3), y = l.y * (0.4 + e * 1.3) - u * 5;
                double r = (l.r0 + (l.r1 - l.r0) * Ease.OutCubic(Math.Min(1, u * 3))) * (1 - Ease.InQuad(u));
                if (r < 0.6) continue;
                p.Circle(x, y, r + 0.8, F[2]);
                p.Circle(x, y, r, (d, a) => d > 0.55 && Px.Sin(a + 0.8) > 0.3 ? F[3] : d < 0.45 && Px.Sin(a + 0.8) < -0.2 ? WHITE : F[4]);
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            // feathers catch the light as they turn
            if (t > 0.3 && t < 2.2 && loose.Count != 0 && R.Chance(0.14))
            {
                Feathers.F q = Pick(R, loose.List);
                if (q.delay <= 0) ps.Spawn(P.Star, q.x + R.Range(-2, 2), q.y + R.Range(-1, 1), 0, 0, R.Range(0.2, 0.4), R.Chance(0.5) ? WHITE : G, R.Range(1, 2.2));
            }
            ps.Update(dt);
            loose.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            loose.Draw(p, F, "back");
            ps.Draw(p, Layer.Back);
            Puff(p);
        }

        public override void Draw(Painter p)
        {
            // the pop: a white flash and a ring of air
            if (t < 0.07) p.Circle(0, 0, 9 * (1 - t / 0.14), WHITE);
            if (t < 0.25)
            {
                double u = t / 0.25;
                p.Ring(0, 0, 6 + 24 * Ease.OutCubic(u), Math.Max(1, 2.5 * (1 - u)), (v, g) => v > 0.5 ? WHITE : Mix(F[4], G, 0.4), 1.1 - u);
            }
            if (t < 0.2) Sim.Sparkle(p, 0, 0, 10 * (1 - t / 0.2), WHITE, G, 0.6);
            // the burst's warmth lingers a moment
            p.Glow(0, 4, 46, G, 0.28 * (1 - Smooth(0, 1.3, t)));
            loose.Draw(p, F, "front");
            ps.Draw(p, Layer.Front);
            lights.Draw(p);
        }
    }
}
