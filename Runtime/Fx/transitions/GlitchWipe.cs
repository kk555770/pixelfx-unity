// The specks of white, cyan and magenta a body breaks into follow Sprite Fusion's open-source blink
// (https://www.spritefusion.com/pixel-art-vfx/movement), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class GlitchWipe : Effect
    {
        public static readonly Module Module = Screen("glitch-wipe", "Glitch wipe",
            "Torn bands of black stutter across the screen, freezing and jerking back, their ragged edges split into magenta and cyan ghosts spitting data slivers; then they tear on through.",
            new[] { "glitch", "digital", "rgb", "bands", "wipe" }, 1.8,
            new Options { ["palette"] = PAL["cyber"], ["split"] = "#ff2d6f", ["seed"] = 1 },
            (o, e) => new GlitchWipe(o));

        const double TICK = 1.0 / 18, IN0 = 0.03, IN1 = 0.78, OUT0 = 0.98, OUT1 = 1.66;

        sealed class Band
        {
            public int i, y, h, dir;
            public double off, t0, t1, o0, o1;
        }

        // the bands: torn strips of different thickness. Neighbours move together in chunks (their timing follows a
        // smooth noise down the screen), mostly left to right, now and then one from the other side
        static readonly Dictionary<double, List<Band>> cache = new Dictionary<double, List<Band>>();

        static List<Band> Bands(double seed)
        {
            if (cache.TryGetValue(seed, out List<Band> hit)) return hit;
            var o = new List<Band>();
            for (int y = 0, i = 0; y < H; i++)
            {
                double r = Rand(i, seed);
                int h = (int)Math.Min(H - y, r < 0.4 ? 5 + Math.Floor(Rand(i, seed + 1) * 6) : r < 0.85 ? 10 + Math.Floor(Rand(i, seed + 1) * 10) : 20 + Math.Floor(Rand(i, seed + 1) * 12));
                double a = Clamp(0.5 + 0.55 * Noise(y / 40.0, seed + 9) + (Rand(i, seed + 2) - 0.5) * 0.25), b = Clamp(0.5 + 0.55 * Noise(y / 40.0, seed + 19) + (Rand(i, seed + 3) - 0.5) * 0.25);
                double span = 0.32 + Rand(i, seed + 6) * 0.12;
                o.Add(new Band
                {
                    i = i,
                    y = y,
                    h = h,
                    dir = Rand(i, seed + 4) < 0.82 ? 1 : -1,
                    off = Rand(i, seed + 5),
                    t0 = IN0 + a * (IN1 - IN0 - span),
                    t1 = IN0 + a * (IN1 - IN0 - span) + span,
                    o0 = OUT0 + b * (OUT1 - OUT0 - span),
                    o1 = OUT0 + b * (OUT1 - OUT0 - span) + span,
                });
                y += h;
            }
            cache[seed] = o;
            return o;
        }

        // how far a band has torn at t: it moves only on ticks, by uneven jumps, sometimes freezing, sometimes
        // jerking back a little, but it always starts at 0 and ends at 1
        static double Tear(Band b, double t, double t0, double t1, double salt)
        {
            if (t <= t0) return 0;
            if (t >= t1) return 1;
            double n = Math.Floor((t + b.off * TICK) / TICK);
            while (n > 0 && Rand(n, b.i, salt) < 0.22) n--; // frozen ticks hold the last position
            double tq = n * TICK - b.off * TICK, u = Clamp((tq - t0) / (t1 - t0));
            return Clamp(Ease.InOutQuad(u) + (Rand(n, b.i, salt + 1) - 0.6) * 0.12 * Px.Sin(Math.PI * u));
        }

        readonly Options o;

        GlitchWipe(Options o) => this.o = o;

        public override void Draw(Painter p0, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8);
            uint ink = o.Color("color"), RED = o.Color("split");
            Painter p = ClipTo(p0);
            List<Band> B = Bands(o.Seed);
            double fr = Math.Floor(t / TICK);
            bool @out = t >= OUT0;
            foreach (Band b in B)
            {
                double k = @out ? Tear(b, t, b.o0, b.o1, 7) : Tear(b, t, b.t0, b.t1, 3);
                if ((!@out && k <= 0) || (@out && k >= 1)) continue;
                bool moving = k > 0 && k < 1;
                double slip = moving && Rand(fr, b.i, 11) < 0.12 ? Round((Rand(fr, b.i, 12) - 0.5) * 18) : 0;
                // the edge advances along +fwd; in, the black is behind it; out, the black is ahead of it
                int fwd = b.dir;
                double e = (b.dir > 0 ? k * W : W - k * W) + slip;
                for (int y = b.y; y < b.y + b.h; y++)
                {
                    // a ragged edge: each row torn a little ahead or behind, re-torn every tick
                    double jag = moving ? Round((Rand(fr, y, 14) - 0.5) * 6 + (Rand(ToInt32(fr) >> 1, b.i, 15) - 0.5) * 6) : 0, ex = Clamp(Round(e + jag), 0, W);
                    bool behind = (fwd > 0) != @out; // is the black on the low-x side of the edge?
                    if (behind)
                    {
                        if (ex > 0) Rect(p0, 0, y, ex, 1, ink);
                    }
                    else if (ex < W) Rect(p0, ex, y, W - ex, 1, ink);
                    if (!moving) continue;
                    // the torn edge: a white line, a magenta ghost on the open side and a cyan ghost in the black
                    double e1 = behind ? ex - 1 : ex, sp = 2 + ((b.i + fr) % 3);
                    int side = behind ? 1 : -1;
                    p.Px(e1, y, WHITE);
                    p.Px(e1 + side * sp, y, RED);
                    p.Px(e1 + side * (sp + 1), y, RED);
                    p.Px(e1 - side * (sp + 1), y, C[5]);
                    p.Px(e1 - side * (sp + 2), y, C[4]);
                    p.Light(e1, y, WHITE, 0.45);
                    p.Light(e1 + side * (sp + 1), y, RED, 0.5);
                    p.Light(e1 + side * (sp + 3), y, RED, 0.22);
                    p.Light(e1 - side * (sp + 1), y, C[5], 0.45);
                    p.Light(e1 - side * (sp + 3), y, C[5], 0.22);
                }
                if (!moving) continue;
                double ex0 = Clamp(Round(e), 0, W);
                int open = (fwd > 0) != @out ? 1 : -1;
                // the edge glows, and a cloud of specks crackles round it, thickest close in
                p.Glow(ex0, b.y + b.h / 2.0, b.h * 0.7 + 7, (b.i & 1) != 0 ? RED : C[5], 0.28);
                for (int s = 0; s < b.h; s++)
                {
                    if (Rand(fr, b.i, 100 + s) > 0.55) continue;
                    double x = ex0 + open * Round(Px.Pow(Rand(fr, b.i, 200 + s), 2) * 22 - 4), y = b.y + Math.Floor(Rand(fr, b.i, 300 + s) * b.h);
                    p.Px(x, y, new[] { WHITE, C[6], C[5], RED }[(int)Math.Floor(Rand(fr, b.i, 400 + s) * 4)]);
                }
                // data slivers spit off the edge: dashes of light out over the picture and glitches back in the black
                for (int s = 0; s < 4; s++)
                {
                    if (Rand(fr, b.i, 20 + s) > 0.5) continue;
                    double len = 2 + Math.Floor(Px.Pow(Rand(fr, b.i, 30 + s), 2) * 60);
                    bool ahead = Rand(fr, b.i, 40 + s) < 0.6;
                    double d = Math.Floor(Px.Pow(Rand(fr, b.i, 50 + s), 1.5) * 70) + 4, y = b.y + Math.Floor(Rand(fr, b.i, 60 + s) * b.h);
                    double x = ahead ? (open > 0 ? ex0 + d : ex0 - d - len) : open > 0 ? ex0 - d - len : ex0 + d;
                    uint c = new[] { WHITE, C[6], C[5], RED }[(int)Math.Floor(Rand(fr, b.i, 70 + s) * 4)];
                    p.Rect(x, y, len, 1, c);
                    for (int q = 0; q < len; q += 3) p.Light(x + q, y, c, 0.35);
                }
                // a corrupted block or two: the outline of a broken tile near the edge
                if (b.h >= 8 && Rand(fr, b.i, 80) < 0.35)
                {
                    double bw = 6 + Math.Floor(Rand(fr, b.i, 81) * 10), bh = 3 + Math.Floor(Rand(fr, b.i, 82) * Math.Min(6, b.h - 3)), x = ex0 + open * Math.Floor(Rand(fr, b.i, 83) * 24 - 4) - (open < 0 ? bw : 0);
                    double y = b.y + Math.Floor(Rand(fr, b.i, 84) * (b.h - bh));
                    uint c = Rand(fr, b.i, 85) < 0.5 ? C[5] : RED;
                    p.Rect(x, y, bw, 1, c);
                    p.Rect(x, y + bh - 1, bw, 1, c);
                    p.Rect(x, y, 1, bh, c);
                    p.Rect(x + bw - 1, y, 1, bh, c);
                    for (int q = 0; q < bw; q += 2) p.Light(x + q, y, c, 0.25);
                }
            }
            // while it's all black, the signal still spits now and then
            if (t > IN1 && t < OUT0 + 0.05)
                for (int s = 0; s < 3; s++)
                {
                    if (Rand(fr, s, 90) > 0.4) continue;
                    double y = Math.Floor(Rand(fr, s, 91) * H), x = Math.Floor(Rand(fr, s, 92) * W), len = 3 + Math.Floor(Px.Pow(Rand(fr, s, 93), 2) * 40);
                    p.Rect(x, y, len, 1, s == 0 ? C[4] : s == 1 ? C[3] : RED);
                }
        }
    }
}
