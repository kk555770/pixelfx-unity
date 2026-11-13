// The arrival (bits converging, a line of light that opens into the silhouette, a ring on the floor, a burst of
// stars) is adapted from Sprite Fusion's open-source blink (https://www.spritefusion.com/pixel-art-vfx/movement),
// MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.ArcaneLib;

namespace Pixelfx
{
    public sealed class BlinkIn : Effect
    {
        const double LINE = 0.26, GROW = 0.1, OPEN = 0.12, ARRIVE = LINE + GROW + OPEN;

        static readonly Pose LANDED = new Pose { ["crouch"] = 7, ["lean"] = 3, ["fx"] = 9, ["fy"] = -12, ["bx"] = 12, ["by"] = -12, ["ffx"] = 8, ["fbx"] = -6 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(0, LANDED),
                new PoseKey(0.48, LANDED),
                new PoseKey(0.56, new Pose(LANDED) { ["crouch"] = 6 }, "snap"),
                new PoseKey(0.95, Poses.READY),
                new PoseKey(1.4, Poses.READY),
            },
            Hits = new[] { new HitCue { T = 0.48, Stop = 0.03, Shake = 0.1, Amp = 1 } },
            Dust = new[] { new DustCue { T = 0.48, X = 0, N = 7 } },
        };

        /// <summary>The stand-in character appears once the silhouette has opened.</summary>
        public static readonly Module Module = new Module("blink-in", "pixelfx/fx/arcane/blink-in", 136, 96, 68, 80, 1.4, false, 30,
            new Options { ["palette"] = PAL["arcane"], ["seed"] = 12 },
            (o, e) => new BlinkIn(o, e), act: Act, actor: 2, actorAlpha: (t, o) => t < ARRIVE ? 0 : 1)
        {
            Title = "Blink in",
            Desc = "Glittering bits spiral in to a point of light that opens into a line, then into a white silhouette that cools into the caster.",
            Tags = new[] { "arcane", "blink", "teleport", "appear", "movement", "magic", "one-shot" },
            Bg = "ground",
        };

        sealed class Bit
        {
            public double tx, ty, ox, oy, t0, t1, spin;
            public uint c;
        }

        readonly uint[] C, COLS;
        readonly Rng R;
        readonly Actor A;
        readonly byte[] sh;
        readonly Particles ps;
        readonly Lights lights;
        readonly double mx, my;
        readonly Bit[] bits;
        double t;
        bool arrived;

        BlinkIn(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            A = o.Actor;
            sh = Shades(A);
            ps = new Particles(R, ground: x => 0, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
            lights = new Lights(e);
            mx = A.X + A.W / 2.0;
            my = A.Y + A.H * 0.55;
            COLS = new[] { WHITE, C[6], C[6], C[5], C[4] };
            // the bits: each spirals in from its own spot to a point on the line, arriving as the line forms
            bits = new Bit[230];
            for (int i = 0; i < bits.Length; i++)
            {
                double a = R.Range(0, TAU), d = R.Range(16, 46), ty = A.Y + R.Range(3, A.H - 2);
                double oy = Px.Sin(a) * d * 0.8 - 6;
                if (ty + oy > -2) oy = -Math.Abs(oy) - 2; // none start under the floor
                var b = new Bit { tx = mx + R.Range(-1, 1), ty = ty, ox = Px.Cos(a) * d, oy = oy };
                b.t0 = R.Range(0, 0.1);
                b.t1 = LINE + R.Range(-0.02, GROW);
                b.spin = R.Range(0.8, 1.8) * R.Sign();
                b.c = R.Pick(COLS);
                bits[i] = b;
            }
        }

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < A.W && y < A.H && A.Mask.Data[y * A.W + x] != 0;

        static (double x, double y, double k) At(Bit b, double tt)
        {
            double k = Clamp((tt - b.t0) / (b.t1 - b.t0)), e = Ease.OutCubic(k), r = 1 - e, sp = b.spin * r * 1.2;
            double ox = b.ox * Px.Cos(sp) - b.oy * Px.Sin(sp), oy = b.ox * Px.Sin(sp) + b.oy * Px.Cos(sp);
            return (b.tx + ox * r, Math.Min(-1, b.ty + oy * r), k);
        }

        public override void Update(double dt)
        {
            t += dt;
            if (t < LINE && R.Chance(0.6))
            {
                double a = R.Range(0, TAU), D = R.Range(22, 44), T = R.Range(0.12, 0.18), v = (D * 3) / (Exp(3 * T) - 1);
                double x = mx + Px.Cos(a) * D, y = Math.Min(-2, my + Px.Sin(a) * D), l = Hypot(x - mx, y - my);
                if (l == 0 || double.IsNaN(l)) l = 1;
                ps.Spawn(P.Streak, x, y, ((mx - x) / l) * v, ((my - y) / l) * v, T, R.Chance(0.5) ? C[6] : C[5]);
            }
            if (!arrived && t >= ARRIVE)
            {
                arrived = true;
                lights.Add(mx, my - 6, 56, C[5], 0.9, 0.35);
                lights.Add(mx, my, 24, WHITE, 0.8, 0.1);
                for (int i = 0; i < 30; i++)
                {
                    double a = R.Range(0, TAU), sp = R.Range(35, 110);
                    ps.Spawn(R.Chance(0.6) ? P.Star : P.Mote, mx, my, Px.Cos(a) * sp, Px.Sin(a) * sp * 0.7 - 30, R.Range(0.4, 0.85), R.Pick(COLS), R.Range(0.6, 1.6));
                }
                for (int i = 0; i < 12; i++)
                {
                    int s = R.Sign();
                    ps.Spawn(P.Spark, mx + s * R.Range(2, 8), -1, s * R.Range(60, 180), -R.Range(20, 110), R.Range(0.2, 0.45));
                }
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            double k = (t - ARRIVE + 0.03) / 0.5;
            if (k >= 0 && k < 1) Sim.Band(p, 0, 0, 4 + 30 * Ease.OutCubic(k), 0.2, Math.Max(1, 3 * (1 - k)), 1.15 - k, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) < 0, k < 0.25);
            p.Glow(0, -1, 26, C[5], 0.3 * (t < ARRIVE ? t / ARRIVE : 1 - Clamp((t - ARRIVE) / 0.5)));
        }

        public override void Draw(Painter p)
        {
            // the bits rushing in, streaking as they speed up, white as they land
            foreach (Bit b in bits)
            {
                if (t < b.t0 || t > b.t1 + 0.02) continue;
                var (x, y, k) = At(b, t);
                p.Dot(x, y, k > 0.75 ? WHITE : b.c, Math.Min(1, 0.35 + k * 2));
                // a short tail once they're rushing
                if (k > 0.55)
                {
                    var (px, py, _) = At(b, t - 0.015);
                    p.Dot((x + px) / 2, (y + py) / 2, C[5], 0.8);
                }
                if (k > 0.8) p.Glow(x, y, 3, C[6], 0.3);
            }
            if (t >= LINE - 0.04 && t < LINE)
            {
                // a point of light where it gathers
                double q = (t - LINE + 0.04) / 0.04;
                Sim.Sparkle(p, mx, my, 2 + 3 * q, C[6], C[5], 0.6);
            }
            else if (t >= LINE && t < LINE + GROW)
            {
                // it opens into a line, top to bottom
                double q = Ease.OutCubic((t - LINE) / GROW), hh = (A.H / 2.0 + 3) * q;
                for (double y = -hh - 2; y <= hh + 2; y++) p.Dot(mx, my + y, Math.Abs(y) < hh ? WHITE : C[6], Math.Abs(y) < hh ? 1 : 0.7);
                for (double y = -hh * 0.7; y <= hh * 0.7; y++)
                {
                    p.Dot(mx - 1, my + y, C[5], 0.6);
                    p.Dot(mx + 1, my + y, C[5], 0.6);
                }
                p.Glow(mx, my, 30, C[5], 0.9);
                p.Glow(mx, my, 8, WHITE, 0.5);
            }
            else if (t >= LINE + GROW && t < ARRIVE)
            {
                // the line opens sideways into the silhouette, white hot
                double q = Ease.OutCubic((t - LINE - GROW) / OPEN);
                Squeezed(p, A, sh, q, WHITE, C[6]);
                p.Glow(mx, my, 36, C[5], 0.8);
            }
            else if (t >= ARRIVE && t < ARRIVE + 0.42)
            {
                // the caster cools out of the white: its body full of light, an outline crackling round it, both fading
                double k = 1 - (t - ARRIVE) / 0.42;
                int f = (int)Math.Floor(t * 30);
                if (k > 0.86) Squeezed(p, A, sh, 1, WHITE, C[6]);
                for (int y = 0; y < A.H; y++)
                    for (int x = 0; x < A.W; x++)
                        if (Inside(x, y)) p.Light(A.X + x, A.Y + y, C[6], k * k * (sh[y * A.W + x] == 1 ? 0.3 : 0.8));
                        else if ((Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1)) && Rand(x, y, f) < 0.85 * k) p.Px(A.X + x, A.Y + y, k > 0.5 && Rand(x, y, 7) < 0.5 ? WHITE : C[6]);
                p.Glow(mx, my, 34, C[5], 0.6 * k);
            }
            if (t >= ARRIVE && t < ARRIVE + 0.2)
            {
                double q = (t - ARRIVE) / 0.2;
                Sim.Flare(p, mx, my - 4, 18 * Px.Pow(1 - q, 1.5) + 2, 1 - q, C);
            }
            double kb = (t - ARRIVE + 0.03) / 0.5;
            if (kb >= 0 && kb < 1) Sim.Band(p, 0, 0, 4 + 30 * Ease.OutCubic(kb), 0.2, Math.Max(1, 3 * (1 - kb)), 1.15 - kb, new[] { C[3], C[5], C[6], WHITE }, g => Px.Sin(g) >= 0, kb < 0.25);
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
