// The dust drifting in the light and the light squeezing through follow Sprite Fusion's open-source holy light
// (https://www.spritefusion.com/pixel-art-vfx/holy-light), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.TransitionsLib;

namespace Pixelfx
{
    public sealed class Blinds : Effect
    {
        const double DURATION = 2.0;

        public static readonly Module Module = Screen("blinds", "Blinds",
            "Slats catch the light edge-on, then tilt shut top to bottom, lit rims glinting as warm light squeezes through the closing gaps with dust in it; then they tilt open and fade.",
            new[] { "blinds", "slats", "light", "wipe" }, DURATION,
            new Options { ["palette"] = new[] { "#1a120c", "#33241a", "#5a4030", "#8a6848", "#c09a6a", "#e8cc98", "#fff0cc", "#ffffff" }, ["seed"] = 1 },
            (o, e) => new Blinds(o, e));

        const int N = 12;
        const double SH = (double)H / N, SHUT0 = 0.08, OPEN0 = 1.02, STEP = 0.038, TURN = 0.42, OSTEP = 0.03, OTURN = 0.4, FADE0 = 1.72, FADE1 = 1.94;
        static readonly int[][] Cross = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        // how far slat i has turned: 0 edge-on, π/2 flat and shut (a little past while it settles)
        static double Angle(int i, double t)
        {
            if (t < OPEN0) return (Math.PI / 2) * Ease.OutBack(Clamp((t - SHUT0 - i * STEP) / TURN), 1.6);
            return (Math.PI / 2) * (1 - Ease.InOutCubic(Clamp((t - OPEN0 - i * OSTEP) / OTURN)));
        }

        readonly uint[] C;
        readonly uint ink;
        readonly Rng R;
        readonly Particles ps;
        readonly double end = DURATION - 0.05;
        double t;

        Blinds(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            ink = o.Color("color");
            R = new Rng(o.Seed);
            ps = new Particles(rng: R, sparks: new[] { WHITE, C[6], C[5], C[4] }, emitter: e);
        }

        public override void Update(double dt)
        {
            t += dt;
            // dust drifts in the light coming through
            if (t < FADE0 && R.Chance(0.8))
            {
                int i = R.Int(0, N - 1);
                double th = Angle(i, t);
                if (th > 0.2 && th < 1.45) ps.Spawn(P.Ember, R.Range(0, W), (i + 1) * SH + R.Range(-2, 2), R.Range(-8, 8), -R.Range(2, 8), Math.Min(R.Range(0.5, 1), end - t), 0, 1);
            }
            ps.Update(dt);
        }

        public override void Draw(Painter p0)
        {
            Painter p = ClipTo(p0);
            var open = new byte[H];
            for (int y = 0; y < H; y++) open[y] = 1;
            bool Open(int y) => y >= 0 && y < H && open[y] != 0;
            for (int i = 0; i < N; i++)
            {
                double h = Math.Max(1, (SH + 1) * Math.Abs(Px.Sin(Angle(i, t)))), yc = (i + 0.5) * SH;
                for (double y = Math.Max(0, Math.Ceiling(yc - h / 2 - 0.5)); y <= Math.Min(H - 1, Math.Floor(yc + h / 2 - 0.5)); y++) open[(int)y] = 0;
            }
            // the dust is behind the slats: it only shows (and only shines) in the gaps
            foreach (Particles.Particle q in ps.List)
            {
                double y = Math.Floor(q.y), k = q.life / q.max;
                if (q.delay > 0 || y < 0 || y >= H || open[(int)y] == 0 || q.x < 0 || q.x >= W) continue;
                // some catch the light as little twinkling crosses
                bool tw = (q.seed % 1) < 0.3 && Px.Sin(t * 14 + q.seed) > 0;
                p0.Px(q.x, y, tw || k > 0.5 ? WHITE : C[5]);
                if (tw)
                    foreach (int[] d in Cross)
                        if (Open((int)y + d[1])) p.Px(q.x + d[0], y + d[1], C[6]);
                p0.Light(q.x, y, C[6], (tw ? 0.7 : 0.4) * k);
            }
            double fade = 1 - Clamp((t - FADE0) / (FADE1 - FADE0)), appear = Clamp(t / SHUT0);
            for (int i = 0; i < N; i++)
            {
                double th = Angle(i, t), s = Px.Sin(th), c = Px.Cos(th), yc = (i + 0.5) * SH;
                double h = Math.Max(1, (SH + 1) * Math.Abs(s)), y0 = Math.Max(0, Math.Ceiling(yc - h / 2 - 0.5)), y1 = Math.Min(H - 1, Math.Floor(yc + h / 2 - 0.5));
                bool under = th > Math.PI / 2; // turned past flat: the shadowed underside shows
                // edge-on, the slat is just its lit edge: it brightens in at the start and fades out at the end
                if (h < 2.5)
                {
                    double k = Math.Min(appear, fade);
                    if (k <= 0) continue;
                    double yy = Round(yc - 0.5);
                    Rect(p0, 0, yy, W, 1, k > 0.66 ? C[6] : k > 0.33 ? C[4] : C[2]);
                    if (h >= 1.5 && yy + 1 < H) Rect(p0, 0, yy + 1, W, 1, k > 0.5 ? C[3] : C[1]);
                    for (int x = 0; x < W; x += 2) p0.Light(x, yy, C[5], 0.18 * k);
                    continue;
                }
                // tilted: a lit top rim, a sheen, a face that darkens as it turns flat to us, a shadowed underside
                double lit = under ? 0 : c;
                for (double y = y0; y <= y1; y++)
                {
                    // the face is lit from above: brightest just under the rim, falling away down it
                    double r = y - y0, v = r / Math.Max(1, y1 - y0), f = lit * (1 - v * 0.8);
                    bool last = y == y1;
                    uint col = r == 0 ? (lit > 0.55 ? WHITE : lit > 0.3 ? C[6] : lit > 0.12 ? C[4] : ink)
                        : r == 1 ? (lit > 0.45 ? C[5] : lit > 0.2 ? C[3] : ink)
                            : last ? ink
                                : f > 0.55 ? C[3] : f > 0.35 ? C[2] : f > 0.15 ? C[1] : ink;
                    Rect(p0, 0, y, W, 1, col);
                    if (r == 0 && lit > 0.12)
                        for (int x = 0; x < W; x += 2) p0.Light(x, y, C[5], 0.35 * lit);
                }
                // light squeezes through the gap under the slat, brighter as the gap narrows
                double gap = SH - h;
                if (gap > 0.5 && th > 0.12 && !under)
                {
                    double k = (0.2 + 0.45 * Px.Pow(1 - gap / SH, 1.5)) * Math.Min(1, th / 0.4), gy = y1 + 1;
                    for (double y = gy; y < Math.Min(H, gy + Math.Ceiling(gap)); y++)
                    {
                        double v = (y - gy + 0.5) / gap, kk = k * (1 - Math.Abs(v - 0.5) * 1.2);
                        if (kk > 0.01)
                            for (int x = 0; x < W; x += 1)
                            {
                                p0.Light(x, y, C[6], kk);
                                p0.Light(x, y, C[4], kk * 0.5);
                            }
                    }
                }
                // a glint runs along the lit rim as the slat turns
                double u = t < OPEN0 ? Clamp((t - SHUT0 - i * STEP) / TURN) : Clamp((t - OPEN0 - i * OSTEP) / OTURN);
                if (lit > 0.3 && u > 0.05 && u < 0.95) Glint(p, W * (t < OPEN0 ? u : 1 - u), y0, 6 * Px.Sin(Math.PI * u), u * 3, C[6], C[5]);
            }
        }
    }
}
