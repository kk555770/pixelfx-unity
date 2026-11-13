using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class HitFlash : Effect
    {
        static readonly Pose BLOWN = new Pose { ["x"] = -5, ["crouch"] = 4, ["lean"] = -5, ["hx"] = -1, ["fx"] = 8, ["fy"] = -33, ["bx"] = -11, ["by"] = -27, ["ffx"] = 9, ["ffy"] = -7, ["fbx"] = -6 };

        public static readonly Act Act = new Act
        {
            OnBody = true,
            Poses = new[]
            {
                new PoseKey(-0.26, new Pose(Poses.READY) { ["x"] = 0 }),
                new PoseKey(0, BLOWN, "snap"),
                new PoseKey(0.16, new Pose(BLOWN) { ["x"] = -8, ["crouch"] = 5 }),
                new PoseKey(0.42, new Pose(BLOWN) { ["x"] = -8, ["crouch"] = 5, ["lean"] = -2, ["fx"] = 9, ["fy"] = -22, ["bx"] = 6, ["by"] = -20, ["ffy"] = -4 }),
                new PoseKey(0.86, new Pose(Poses.READY) { ["x"] = 0 }),
            },
            Hits = new[] { new HitCue { T = 0, Stop = 0.07, Shake = 0.14, Amp = 2 } },
        };

        public static readonly Module Module = new Module("hit-flash", "pixelfx/fx/impacts/hit-flash", 80, 64, 40, 54, 0.5, false, 30,
            new Options
            {
                ["palette"] = new[] { "#14040a", "#3a0a14", "#6a101c", "#a01824", "#d42a30", "#ff5a50", "#ff9a8a" },
                ["lines"] = 7,
                ["side"] = 1,
                ["seed"] = 17,
            },
            (o, e) => new HitFlash(o, e), act: Act, actor: 2)
        {
            Title = "Hit flash",
            Desc = "The character takes damage: a white frame, then their own colors flushed red and blinking white, cooling off, while comic hit lines jab in at them.",
            Tags = new[] { "impact", "hit", "damage", "hurt", "flash", "blink", "actor", "comic", "one-shot" },
            Bg = "ground",
        };

        // the beats, by frame at 30 fps: white, white, red, red, white rim, red cooling off
        static readonly (double end, string beat)[] BEATS = { (0.025, "white"), (0.058, "white2"), (0.125, "red"), (0.158, "rim"), (0.36, "cool") };

        sealed class HitLine
        {
            public double a, d, len, t0;
        }

        readonly uint[] C;
        readonly Actor A;
        readonly Mask M;
        readonly uint[] PX, reds, tints;
        readonly double cx, cy, hx, hy;
        readonly Lights lights;
        readonly HitLine[] lines;
        double t;

        HitFlash(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            var R = new Rng(o.Seed);
            A = o.Actor;
            M = A.Mask;
            PX = A.Pixels;
            double S = o.Num("side");
            cx = A.Chest[0];
            cy = A.Chest[1];
            hx = cx + S * 12; // where the blow lands: the character's front, chest high
            hy = cy - 5;
            lights = new Lights(e);
            lights.Add(hx, hy, 34, WHITE, 0.6, 0.1);
            lights.Add(cx, cy, 30, C[4], 0.5, 0.35);
            // the hurt colors: each pixel's own light and shade carried into the red ramp
            reds = new uint[PX.Length];
            tints = new uint[PX.Length];
            for (int i = 0; i < PX.Length; i++)
            {
                if (PX[i] == 0) continue;
                double l = Luma(PX[i] | 0xff000000u);
                reds[i] = C[(int)Clamp(Round(2 + l * 5), 2, 6)];
                tints[i] = Mix(PX[i] | 0xff000000u, C[4], 0.55);
            }
            // hit lines all round the top and sides, jabbing in at the body
            double n = o.Num("lines");
            lines = new HitLine[(int)n];
            for (int i = 0; i < lines.Length; i++)
            {
                double a = -Math.PI - 0.3 + (i + R.Range(0.25, 0.75)) / n * (Math.PI + 0.6);
                lines[i] = new HitLine { a = a, d = R.Range(15, 19), len = R.Range(6, 9), t0 = R.Range(0, 0.04) };
            }
        }

        string Beat()
        {
            foreach (var (end, b) in BEATS)
                if (t < end)
                    return b;
            return null;
        }

        void Body(Painter p, Func<int, uint> color) => p.MaskFill(M, A.X, A.Y, (mx, my) => color(my * M.W + mx));

        public override void Update(double dt)
        {
            t += dt;
            lights.Update(dt);
        }

        public override void Draw(Painter p)
        {
            string b = Beat();
            if (b == "white" || b == "white2")
            {
                Body(p, i => WHITE);
                p.Outline(M, A.X, A.Y, b == "white" ? C[6] : C[5]);
            }
            else if (b == "red")
            {
                Body(p, i => reds[i]);
                p.Outline(M, A.X, A.Y, C[5]);
            }
            else if (b == "rim") p.Outline(M, A.X, A.Y, WHITE);
            else if (b == "cool")
            {
                double k = 1 - (t - 0.158) / (0.36 - 0.158);
                Body(p, i => Mix(PX[i] | 0xff000000u, tints[i], k));
                if (k > 0.5) p.Outline(M, A.X, A.Y, C[4], (k - 0.5) * 2);
            }
            // the blow: a small hot burst on the side it came from
            if (t < 0.07)
            {
                double big = t < 0.035 ? 1 : 1.3;
                int k = t < 0.035 ? 0 : 1;
                for (int i = 0; i < 8; i++)
                {
                    double a = (double)i / 8 * TAU + 0.2, L = (i % 2 != 0 ? 4 : 9) * big;
                    p.Line(hx + Px.Cos(a) * L * 0.45 * k, hy + Px.Sin(a) * L * 0.45 * k, hx + Px.Cos(a) * L, hy + Px.Sin(a) * L, u => u < 0.45 && k == 0 ? WHITE : u < 0.75 ? C[6] : C[4]);
                }
                if (k == 0)
                    foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
                        p.Px(hx + dx, hy + dy, WHITE);
                p.Glow(hx, hy, 14, C[6], 0.5 * (1 - t / 0.07));
            }
            // comic hit lines jab in toward the body, then are gone
            foreach (HitLine l in lines)
            {
                double u = (t - l.t0) / 0.16;
                if (u < 0 || u >= 1) continue;
                double d0 = l.d - 6 * Ease.OutCubic(u), L = l.len * (1 - u), ca = Px.Cos(l.a), sa = Px.Sin(l.a) * 1.15;
                if (L < 1) continue;
                // a wedge, its point at the body and its fat end out, white with a red edge
                double w = (2.8 - 1.6 * u) / 2, nx = -sa * w, ny = ca * w, ex = cx + ca * (d0 + L), ey = cy + sa * (d0 + L), px0 = cx + ca * d0, py0 = cy + sa * d0;
                p.Poly(new[] { new[] { px0 - ca, py0 - sa }, new[] { ex + nx * 1.5 + ca, ey + ny * 1.5 + sa }, new[] { ex - nx * 1.5 + ca, ey - ny * 1.5 + sa } }, C[3]);
                p.Poly(new[] { new[] { px0, py0 }, new[] { ex + nx, ey + ny }, new[] { ex - nx, ey - ny } }, u < 0.5 ? WHITE : C[6]);
            }
            double kk = Exp(-t / 0.08);
            if (kk > 0.02) p.Glow(cx, cy, 26, b == "white" || b == "white2" ? WHITE : C[5], 0.35 * kk);
            lights.Draw(p);
        }
    }
}
