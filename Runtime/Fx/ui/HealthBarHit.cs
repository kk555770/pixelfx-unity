using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.UiLib;

namespace Pixelfx
{
    public sealed class HealthBarHit : Effect
    {
        const double DURATION = 2.0;

        public static readonly Module Module = new Module("health-bar-hit", "pixelfx/fx/ui/health-bar-hit", 160, 64, 80, 30, DURATION, false, 30,
            new Options
            {
                ["palette"] = new[] { "#1a0508", "#4a0b12", "#8c1420", "#d42a2e", "#ff5a4a", "#ff9a86", "#ffd8cc", "#ffffff" },
                ["from"] = 0.8,
                ["to"] = 0.45,
                ["seed"] = 3,
            },
            (o, e) => new HealthBarHit(o, e))
        {
            Title = "Health bar hit",
            Desc = "A segmented health bar takes a hit: it jolts and dips, the lost chunk flashes white and sheds chips, holds, then drains away behind a sparking edge.",
            Tags = new[] { "ui", "health", "hud", "damage", "bar", "one-shot" },
            Bg = "dark",
        };

        const double HIT = 0.2, DRAIN = 0.62, DRAINED = 1.05, FOLD = 1.62;
        const double BX = -48, BW = 104, BY = -4, BH = 8, CELLS = 10;
        static readonly Plate INK = new Plate { Outline = Hex("#0d0a14"), Rim = Hex("#4a4060"), Top = Hex("#2a2338"), Body = Hex("#1e1929"), Low = Hex("#141019") };

        readonly uint[] C;
        readonly Rng R;
        readonly double from, to;
        readonly Particles ps;
        readonly Bits bits;
        readonly Lights lights;
        readonly uint PALE;
        readonly Dictionary<char, uint> pal, whitePal;
        readonly double seed;
        double t;
        bool hit;

        HealthBarHit(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            R = new Rng(o.Seed);
            seed = o.Num("seed");
            from = Clamp(o.Num("from"));
            to = Clamp(Math.Min(o.Num("to"), from));
            ps = new Particles(R, sparks: new[] { C[7], C[6], C[5], C[3] }, emitter: e);
            bits = new Bits(emitter: e);
            lights = new Lights(e);
            PALE = Mix(C[6], Hex("#ffe9a8"), 0.5);
            pal = HeartPal(C);
            whitePal = new Dictionary<char, uint>(pal) { ['k'] = 0, ['w'] = WHITE, ['h'] = WHITE, ['o'] = WHITE, ['m'] = WHITE, ['d'] = C[6] };
        }

        // the white chunk's right end: holds, then drains down to the new value
        double GhostAt(double t) => t < DRAIN ? from : from - (from - to) * Ease.InOutQuad(Seg(t, DRAIN, DRAINED));

        public override void Update(double dt)
        {
            t += dt;
            if (!hit && t >= HIT)
            {
                hit = true;
                lights.Add(BX + BW * (to + from) / 2, BY + 4, 50, C[5], 0.7, 0.3);
                lights.Add(BX + BW * (to + from) / 2, BY + 4, 24, WHITE, 0.6, 0.08);
                // chips break off the lost chunk
                for (int i = 0; i < 12; i++)
                {
                    double x = BX + BW * R.Range(to, from), an = -Math.PI / 2 + R.Range(-1.2, 1.2), sp = R.Range(50, 130);
                    var b = new Bits.Bit { kind = B.Shard, x = x };
                    b.y = BY + R.Range(1, 7);
                    b.vx = Px.Cos(an) * sp;
                    b.vy = Px.Sin(an) * sp;
                    b.g = 0.45;
                    b.drag = 1.2;
                    b.life = R.Range(0.35, 0.6);
                    b.a = R.Range(0, TAU);
                    b.va = R.Range(-20, 20);
                    b.s = R.Range(1.5, 3);
                    b.c = R.Pick(new[] { C[7], C[6], C[5], C[4] });
                    bits.Add(b);
                }
                for (int i = 0; i < 10; i++)
                {
                    double an = R.Range(0, TAU), sp = R.Range(60, 160);
                    ps.Spawn(P.Spark, BX + BW * R.Range(to, from), BY + 4, Px.Cos(an) * sp, Px.Sin(an) * sp * 0.6 - 30, R.Range(0.12, 0.26));
                }
            }
            // the draining edge sparks as it runs back
            if (t > DRAIN && t < DRAINED && R.Chance(0.5))
            {
                double x = BX + BW * GhostAt(t);
                ps.Spawn(P.Spark, x, BY + R.Range(1, 7), R.Range(-20, 60), -R.Range(20, 70), R.Range(0.1, 0.22));
            }
            bits.Update(dt);
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void Draw(Painter p, double tt)
        {
            var (jx, jy) = Shake(t, 3, HIT, 0.28, seed);
            double dip = t > HIT ? Round(3 * Exp(-(t - HIT) * 10) * Px.Cos((t - HIT) * 24)) : 0;
            double open = Ease.OutBack(Seg(t, 0, 0.14), 2) * (1 - Ease.InBack(Seg(t, FOLD, FOLD + 0.18), 2));
            if (open > 0.04)
            {
                double ox = jx, oy = jy + dip, h = 18 * open, y0 = -9 + (18 - h) / 2;
                // the frame flashes red on the hit
                bool red = t >= HIT && t < HIT + 0.07;
                Plate frame = INK;
                if (red)
                {
                    frame = INK.With();
                    frame.Outline = C[4];
                    frame.Rim = C[6];
                }
                Panel(p, BX - 22 + ox, y0 + oy, BW + 26, h, frame, Math.Min(5, h / 2));
                if (open > 0.75)
                {
                    // the track, then the fill (red, lit on top, dark below), the white chunk, and the cell dividers
                    double val = t < HIT ? from : to, ghost = t < HIT ? from : GhostAt(t);
                    bool white = t >= HIT && t < HIT + 0.07;
                    p.Rect(BX + ox, BY + oy, BW, BH, Hex("#0d0a14"));
                    uint trackTop = Hex("#2a2338"), track = Hex("#191424");
                    p.Shade(BX + ox, BY + oy, BX + BW + ox, BY + BH + oy, (x, y, o) =>
                    {
                        double u = (x - BX - ox) / BW, v = y - BY - oy;
                        if (u < val) return v < 1 ? C[6] : v < 2 ? C[5] : v < 5 ? C[4] : v < 7 ? C[3] : C[2];
                        if (u < ghost) return white ? WHITE : v < 1 ? WHITE : v < 6 ? PALE : C[5];
                        return v < 1 ? trackTop : track;
                    });
                    for (int i = 1; i < CELLS; i++) p.Rect(BX + ox + Round((BW * i) / CELLS), BY + oy, 1, BH, Hex("#0d0a14"));
                    // the new edge of the health glints, and a hot spark rides the draining edge
                    if (t > HIT) p.Rect(BX + ox + Round(BW * val) - 1, BY + oy, 1, BH, C[7]);
                    if (t > DRAIN && t < DRAINED)
                    {
                        double gx = BX + ox + BW * ghost;
                        p.Rect(gx - 1, BY + oy - 1, 2, BH + 2, WHITE);
                        p.Glow(gx, BY + oy + 4, 10, C[5], 0.6);
                    }
                    p.Glow(BX + ox + BW * val * 0.5, BY + oy + 4, BW * 0.4, C[3], 0.12);
                    // the heart: jumps and flashes white on the hit, glints before it
                    double hk = t - HIT, hop = hk > 0 ? Round(-3 * Exp(-hk * 12) * Math.Abs(Px.Cos(hk * 22))) : 0;
                    p.Sprite(HEART, pal, BX - 17 + ox, oy - 6 + hop);
                    if (hk > 0 && hk < 0.07) p.Sprite(HEART, whitePal, BX - 17 + ox, oy - 6 + hop);
                    p.Glow(BX - 11 + ox, oy, 10, C[4], 0.25 + (hk > 0 && hk < 0.2 ? 0.4 : 0));
                    if (t < HIT && (t * 2.5) % 1 < 0.3) Sim.Sparkle(p, BX - 14 + ox, oy - 3, 2, C[7], C[6], 0.3);
                }
            }
            bits.Draw(p);
            ps.Draw(p);
            lights.Draw(p);
        }
    }
}
