using System;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class HeartLost : Effect
    {
        const double DURATION = 1.5;

        public static readonly Module Module = new Module("heart-lost", "pixelfx/fx/ui/heart-lost", 96, 88, 48, 34, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0508", "#4a0b12", "#8c1420", "#d42a2e", "#ff5a4a", "#ff9a86", "#ffd8cc", "#ffffff" },
                ["seed"] = 5,
            },
            (o, e) => new HeartLost(o, e))
        {
            Title = "Heart lost",
            Desc = "A HUD heart is hit: it flashes white and jolts, a crack runs down it, it splits into two halves that tumble away, leaving an empty socket that fades.",
            Tags = new[] { "ui", "health", "heart", "hud", "damage", "one-shot" },
            Bg = "dark",
        };

        const double HIT = 0.14, CRACK = 0.24, SPLIT = 0.5;
        // the heart's shape, 19 x 16, centred on (0, 0)
        static readonly string[] MASK =
        {
            "....xxxx...xxxx....",
            "..xxxxxxx.xxxxxxx..",
            ".xxxxxxxxxxxxxxxxx.",
            "xxxxxxxxxxxxxxxxxxx",
            "xxxxxxxxxxxxxxxxxxx",
            "xxxxxxxxxxxxxxxxxxx",
            "xxxxxxxxxxxxxxxxxxx",
            ".xxxxxxxxxxxxxxxxx.",
            "..xxxxxxxxxxxxxxx..",
            "...xxxxxxxxxxxxx...",
            "....xxxxxxxxxxx....",
            ".....xxxxxxxxx.....",
            "......xxxxxxx......",
            ".......xxxxx.......",
            "........xxx........",
            ".........x.........",
        };
        const int MW = 19, MH = 16;
        const double OX = -9.5, OY = -8;

        static bool InMask(double i, double j) => i >= 0 && j >= 0 && i < MW && j < MH && MASK[(int)j][(int)i] == 'x';

        // the crack: a zigzag from the notch to the tip, as a column per row
        static readonly int[] CRACK_X = { 9, 9, 10, 9, 8, 8, 9, 10, 10, 9, 8, 9, 9, 10, 9, 9 };

        // the heart's own color at mask pixel (i, j): a lit left lobe with a white glint, shade down the right
        static uint ShadeAt(uint[] C, double i, double j)
        {
            double u = (i - 9) / 9.5, v = (j - 7) / 8;
            if ((i == 3 || i == 4) && (j == 2 || j == 3) && !(i == 4 && j == 3)) return WHITE;
            if (i >= 2 && i <= 5 && j >= 2 && j <= 5 && u + v < -0.75) return C[5];
            double l = -(u * 0.55 + v * 0.65);
            return l > 0.25 ? C[4] : l > -0.3 ? C[3] : l > -0.6 ? C[2] : C[1];
        }

        // draw part of the heart (side: -1 left of the crack, 1 right, 0 whole) turned by a about its pivot
        static void DrawPart(Painter p, uint[] C, int side, double x, double y, double a, bool white, double crackK, double alpha)
        {
            double ca = Px.Cos(a), sa = Px.Sin(a), px0 = side < 0 ? -4.5 : side > 0 ? 4.5 : 0, py0 = 0;
            bool Own(double i, double j) => InMask(i, j) && (side == 0 || (side < 0 ? i < CRACK_X[(int)j] : i >= CRACK_X[(int)j]));
            p.Shade(x - 13, y - 13, x + 13, y + 13, (sx, sy, o) =>
            {
                // back into the heart's own pixels
                double dx = sx - x, dy = sy - y, lx = dx * ca + dy * sa + px0, ly = -dx * sa + dy * ca + py0;
                double i = Math.Floor(lx - OX), j = Math.Floor(ly - OY);
                if (!Own(i, j)) return 0;
                o.a = alpha;
                bool edge = !Own(i - 1, j) || !Own(i + 1, j) || !Own(i, j - 1) || !Own(i, j + 1);
                if (white) return edge ? C[6] : WHITE;
                if (edge) return C[0];
                // the crack opens down the middle, dark with a lit lip
                if (side == 0 && j / MH < crackK)
                {
                    if (i == CRACK_X[(int)j]) return C[0];
                    if (i == CRACK_X[(int)j] - 1) return C[6];
                }
                return ShadeAt(C, i, j);
            });
        }

        sealed class Half
        {
            public int s;
            public double x, y, vx, vy, a, va;
        }

        readonly uint[] C;
        readonly Rng R;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly uint[] SOCKET = { Hex("#0d0a14"), Hex("#2a2338"), Hex("#3c3350") };
        readonly Half[] halves;
        readonly double seed;
        const double END = DURATION - 0.05;
        double t;
        bool hit, split, gone;

        HeartLost(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            seed = o.Num("seed");
            ps = new Particles(R, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            bits = new Bits(emitter: e);
            lights = new Lights(e);
            halves = new Half[2];
            int k = 0;
            foreach (int s in new[] { -1, 1 })
            {
                var h = new Half { s = s, x = s * 4.5, y = 0 };
                h.vx = s * R.Range(55, 70);
                h.vy = -R.Range(60, 80);
                h.a = 0;
                h.va = s * R.Range(2.2, 3.2);
                halves[k++] = h;
            }
        }

        public override void Update(double dt)
        {
            t += dt;
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(0, 0, 40, C[5], 0.6, 0.3);
                for (int i = 0; i < 8; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(80, 170);
                    ps.Spawn(P.Spark, Px.Cos(an) * 9, Px.Sin(an) * 7, Px.Cos(an) * sp, Px.Sin(an) * sp - 30, R.Range(0.1, 0.22));
                }
            }
            if (!split && t >= SPLIT)
            {
                split = true;
                lights.Add(0, 0, 44, WHITE, 0.6, 0.12);
                lights.Add(0, 0, 50, C[5], 0.5, 0.4);
                // the break throws shards from the crack and a puff of red dust
                for (int i = 0; i < 12; i++)
                {
                    int j = R.Int(1, 14);
                    double an = -Math.PI / 2 + R.Range(-1.4, 1.4), sp = R.Range(40, 100);
                    var b = new Bits.Bit { kind = B.Shard, x = CRACK_X[j] + OX, y = j + OY, vx = Px.Cos(an) * sp, vy = Px.Sin(an) * sp, g = 0.5, drag = 1 };
                    b.life = R.Range(0.35, 0.55);
                    b.a = R.Range(0, TAU);
                    b.va = R.Range(-20, 20);
                    b.s = R.Range(1.5, 3);
                    b.c = R.Pick(new[] { C[6], C[5], C[4], C[3] });
                    bits.Add(b);
                }
                for (int i = 0; i < 5; i++) ps.Spawn(P.Dust, R.Range(-2, 2), R.Range(-6, 6), R.Range(-25, 25), R.Range(-20, 5), R.Range(0.35, 0.6), C[2], R.Range(2, 3.5));
                for (int i = 0; i < 6; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(30, 70);
                    ps.Spawn(P.Star, Px.Cos(an) * 4, Px.Sin(an) * 4, Px.Cos(an) * sp, Px.Sin(an) * sp, R.Range(0.25, 0.45), R.Pick(new[] { C[7], C[6] }), i % 2 != 0 ? 0.6 : 1.4);
                }
            }
            if (split && !gone && t >= 0.95)
            {
                gone = true;
                foreach (Half h in halves)
                    for (int i = 0; i < 5; i++)
                    {
                        double an = R.Range(0, TAU), sp = R.Range(20, 50);
                        ps.Spawn(P.Star, h.x + Px.Cos(an) * 4, h.y + Px.Sin(an) * 4, Px.Cos(an) * sp, Px.Sin(an) * sp - 10, Math.Min(R.Range(0.2, 0.35), END - t), R.Pick(new[] { C[7], C[6], C[5] }), i != 0 ? 0.6 : 1.3);
                    }
            }
            if (split)
                foreach (Half h in halves)
                {
                    h.vy += 520 * dt;
                    h.vx -= h.vx * 0.6 * dt;
                    h.x += h.vx * dt;
                    h.y += h.vy * dt;
                    h.a += h.va * dt;
                    // a shard or two of red trails off each half as it tumbles
                    if (t < 0.92 && R.Chance(0.05)) ps.Spawn(P.Ember, h.x, h.y, R.Range(-10, 10), R.Range(-10, 5), Math.Min(R.Range(0.3, 0.5), END - t), C[4], 1);
                }
            bits.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            var (jx, jy) = Shake(t, 3, HIT, 0.25, seed);
            double tremble = t > CRACK && t < SPLIT ? Round(Px.Sin(t * 90)) : 0;
            // the empty socket it leaves behind, which fades away at the end
            double sk = Seg(t, SPLIT - 0.05, SPLIT + 0.1) * (1 - Seg(t, 1.15, 1.4));
            if (sk > 0)
                p.Shade(-12, -10, 12, 10, (sx, sy, o) =>
                {
                    double i = Math.Floor(sx - OX), j = Math.Floor(sy - OY);
                    if (!InMask(i, j)) return 0;
                    o.a = sk;
                    bool edge = !InMask(i - 1, j) || !InMask(i + 1, j) || !InMask(i, j - 1) || !InMask(i, j + 1);
                    return edge ? SOCKET[2] : j < 3 ? SOCKET[1] : SOCKET[0];
                });
            if (t < SPLIT)
            {
                // whole: it beats before the hit, flashes white and jolts on it, then trembles as the crack runs down
                double beat = t < HIT ? 1 + 0.08 * Math.Max(0, Px.Sin(t * 22)) : 1;
                bool white = t >= HIT && t < HIT + 0.07;
                DrawPart(p, C, 0, jx + tremble, jy, 0, white, Seg(t, CRACK, SPLIT - 0.06), 1);
                p.Glow(0, 0, 18 * beat, C[4], 0.3 + (white ? 0.5 : 0));
                if (t < HIT) Sim.Sparkle(p, -6, -5, 2.5 * Math.Max(0, Px.Sin(t * 22)), C[7], C[6], 0.3);
            }
            else
            {
                // the halves tumble away, flashing white on the break, and fade
                double fade = 1 - Seg(t, 0.9, 1.0);
                bool white = t < SPLIT + 0.05;
                if (fade > 0)
                    foreach (Half h in halves)
                        DrawPart(p, C, h.s, h.x, h.y, h.a, white, 0, fade);
                double rk = (t - SPLIT) / 0.3;
                if (rk < 1) p.Ring(0, 0, 8 + 16 * Ease.OutCubic(rk), Math.Max(1, 2 * (1 - rk)), rk < 0.3 ? WHITE : C[5], 1.2 - rk);
            }
            bits.Draw(p);
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
