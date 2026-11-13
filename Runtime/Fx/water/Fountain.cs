// The basin's caustic light (two warped sine nets, light where they cross zero) is adapted from Sprite Fusion's
// open-source bubbles (https://www.spritefusion.com/pixel-art-vfx/bubbles), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;
using static Pixelfx.WaterLib;

namespace Pixelfx
{
    public sealed class Fountain : Effect
    {
        public static readonly Module Module = new Module("fountain", "pixelfx/fx/water/fountain", 120, 104, 60, 96, 2, true, 30,
            new Options { ["palette"] = WATER, ["seed"] = 3 },
            (o, e) => new Fountain(o, e))
        {
            Title = "Fountain",
            Desc = "A stone basin; a jet rises from its spout and blooms into glassy arcs that break into beads, fall back and plink into the water with rings.",
            Tags = new[] { "water", "fountain", "jet", "basin", "prop", "ambient", "loop" },
            Bg = "ground",
        };

        const double D = 2, SY = 0.34, G = 600, RIM = 10, RX = 36, IN = 29.5, JET = 54;
        const int ARCS = 12;
        // the basin stands on the floor: its base ellipse touches y = 0 at the front; heights are measured from that plane
        const double BY = -RX * SY, LEVEL = RIM - 1.2;

        sealed class Arc
        {
            public double a, vr, vh, tl, ca, sa;
        }

        // the arcs: water thrown out of the top of the jet in every direction, each a parabola back down to the basin
        static Arc[] Arcs(double S)
        {
            var list = new Arc[ARCS];
            for (int i = 0; i < ARCS; i++)
            {
                double a = ((i + 0.5) / ARCS) * TAU + Rand(i, S) * 0.25, vr = 38 + 14 * Rand(i, S + 1), vh = 30 + 26 * Rand(i, S + 2);
                double tl = (vh + Math.Sqrt(vh * vh + 2 * G * (JET - LEVEL))) / G;
                list[i] = new Arc { a = a, vr = vr, vh = vh, tl = tl, ca = Px.Cos(a), sa = Px.Sin(a) };
            }
            return list;
        }

        // a point along an arc tau seconds into its flight: screen x, y
        static (double x, double y) Along(Arc q, double tau)
        {
            double r = q.vr * tau, h = JET + q.vh * tau - 0.5 * G * tau * tau;
            return (q.ca * r, BY + q.sa * r * SY - h);
        }

        static bool InEll(double x, double y, double rx, double cy)
        {
            double a = x / rx, b = (y - cy) / (rx * SY);
            return a * a + b * b <= 1;
        }

        // the stone basin: a wide rim lit toward the light with a dark line round both edges, a wall of stone blocks
        // below its near side (lit left, shaded right) standing on the floor
        static void Basin(Painter p, uint[] ST, double half)
        {
            double cy = BY - RIM, ry = RX * SY;
            p.Shade(-RX - 1, cy - ry - 1, RX + 1, 1, (x, y, @out) =>
            {
                if (InEll(x, y, RX, cy))
                {
                    if (InEll(x, y, IN, cy)) return 0;
                    bool near = y > cy;
                    if (near != half > 0) return 0;
                    bool outer = !InEll(x, y - 1, RX, cy) || !InEll(x, y + 1, RX, cy) || !InEll(x - 1, y, RX, cy) || !InEll(x + 1, y, RX, cy);
                    bool inner = InEll(x, y - 1, IN, cy) || InEll(x, y + 1, IN, cy) || InEll(x - 1, y, IN, cy) || InEll(x + 1, y, IN, cy);
                    double a = Atan2((y - cy) / ry, x / RX), lit = -Px.Cos(a) * 0.65 - Px.Sin(a) * 0.45;
                    @out.c = outer ? ST[0] : inner ? (y < cy ? ST[2] : ST[1]) : lit > 0.4 ? ST[5] : lit > -0.05 ? ST[4] : lit > -0.45 ? ST[3] : ST[2];
                    return 0;
                }
                // the wall, under the near half of the rim
                if (half < 0 || Math.Abs(x) >= RX) return 0;
                double dx = x / RX, top = cy + Math.Sqrt(1 - dx * dx) * ry, bse = top + RIM;
                if (y < top || y > bse) return 0;
                double u = Px.Asin(Clamp(dx)) * RX * 0.62, row = y - top, course = row > RIM * 0.5 ? 1 : 0;
                bool joint = (((((u + 40 + course * 4.5) / 9) % 1) + 1) % 1) < 0.1;
                @out.c = y > bse - 1 || Math.Abs(x) > RX - 1 || row < 1 ? ST[0] : joint || Math.Abs(row - RIM * 0.5) < 0.5 ? ST[1] : dx < -0.4 ? ST[4] : dx < 0.3 ? ST[3] : ST[2];
                if (row >= 1 && row < 2 && !joint) @out.c = dx < 0 ? ST[2] : ST[1];
                return 0;
            });
        }

        // the water in the basin: dark and see-through, a lit band at the far side, caustic light swimming in it
        static void Pool(Painter p, uint[] C, double ph)
        {
            double rx = IN, ry = rx * SY, cy = BY - LEVEL, T = ph * TAU;
            for (double Y = Math.Floor(cy - ry); Y < cy + ry; Y++)
                for (double X = -Math.Ceiling(rx); X < rx; X++)
                {
                    double x = X + 0.5, y = Y + 0.5, dx = x / rx, dy = (y - cy) / ry, d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    p.Blend(x, y, dy < -0.55 ? C[3] : C[1], dy < -0.55 ? 0.8 : 0.85);
                    double u = x * 0.25 + 1.6 * Px.Sin(y * 0.6 + T), w = y * 0.7 + 1.6 * Px.Sin(x * 0.14 - T * 2);
                    double f = Math.Max(0, 1 - Math.Abs(Px.Sin(u) + Px.Sin(w)) * 2.6) * (1 - d);
                    if (f > 0.35) p.Light(x, y, C[5], 0.3 * f);
                }
        }

        // one arc: a glassy stream that thins as it falls and breaks into beads at the end; beads slide down it
        static void DrawArc(Painter p, uint[] C, Arc q, double ph, bool back)
        {
            bool has = false;
            double px = 0, py = 0;
            for (double tau = 0; tau <= q.tl; tau += 0.012)
            {
                var (x, y) = Along(q, tau);
                double u = tau / q.tl;
                if (has && u < 0.66) p.Line(px, py, x, y, back ? (u < 0.25 ? C[5] : C[4]) : u < 0.2 ? WHITE : u < 0.45 ? C[6] : C[5]);
                has = true;
                px = x;
                py = y;
            }
            // the beads it breaks into, sliding down at the water's own speed
            uint[] dim = { C[0], C[1], C[2], C[2], C[3], C[4], C[5], C[5] };
            for (int k = 0; k < 6; k++)
            {
                double u = ((ph * 2 + k / 6.0) % 1) * 0.42 + 0.58, tau = u * q.tl;
                var (x, y) = Along(q, tau);
                double vy = (q.vh - G * tau) * -1 + q.sa * q.vr * SY;
                Bead(p, x, y, q.ca * q.vr, vy, back ? 1 : k % 3 == 0 ? 2 : 1, back ? dim : C);
            }
            // and the ones sliding along the stream itself catch the light
            double g = ((ph * 4 + q.a) % 1) * 0.6;
            var (gx, gy) = Along(q, g * q.tl);
            p.Px(gx, gy, WHITE);
            if (!back) p.Glow(gx, gy, 3, C[5], 0.3);
        }

        readonly Options o;

        Fountain(Options o, Emitter e) => this.o = o;

        public override void DrawBack(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), ST = Rock();
            double ph = t / D, S = o.Seed;
            Arc[] Q = Arcs(S);
            Basin(p, ST, -1);
            Pool(p, C, ph);
            // the arcs on the far side, dimmer, behind the jet
            foreach (Arc q in Q)
                if (q.sa < 0) DrawArc(p, C, q, ph, true);
        }

        public override void Draw(Painter p, double t)
        {
            uint[] C = Fit(o.Ramp("palette"), 8), ST = Rock();
            double ph = t / D, S = o.Seed, T = ph * TAU;
            Arc[] Q = Arcs(S);
            // the spout: a little stone stem in the middle of the basin
            p.Shade(-3.5, BY - 16, 3.5, BY - LEVEL + 0.5, (x, y, @out) =>
            {
                double w = y < BY - 14 ? 3.4 : 2.2;
                if (Math.Abs(x) > w) return 0;
                @out.c = Math.Abs(x) > w - 0.8 || y < BY - 15.5 ? ST[0] : x < -0.6 ? ST[4] : x < 0.8 ? ST[3] : ST[2];
                return 0;
            });
            // the jet: a glassy column, white down its middle, wobbling a little at the top
            p.Shade(-3, BY - JET - 1, 3, BY - 16, (x, y, @out) =>
            {
                double v = (BY - 16 - y) / (JET - 16), sway = Px.Sin(T * 2 + v * 5) * 0.5 * v, w = 1.3 + 0.5 * v + 0.3 * Px.Sin(T * 3 + y * 0.4), q = (x - sway) / w;
                if (Math.Abs(q) > 1) return 0;
                double n = TileNoise(x * 0.9, y * 0.15 + ph * 16, 0, 16, S);
                @out.c = q < -0.4 ? C[6] : q < 0.35 ? (n > -0.3 ? WHITE : C[6]) : C[5];
                return 0;
            });
            // the bloom where it opens out at the top
            p.Circle(0, BY - JET - 1, 2.4, C[6]);
            p.Circle(-0.4, BY - JET - 1.5, 1.4, WHITE);
            p.Glow(0, BY - JET, 22, C[5], 0.46 + 0.06 * Px.Sin(T * 4));
            p.Glow(0, BY - JET - 1, 8, WHITE, 0.3);
            // glints wink along the arcs
            Cycle(t, D, 10, S + 30, g => g.Range(0.2, 0.35), (age, life, r, _) =>
            {
                Arc q = Q[(int)Math.Floor(r.Next() * ARCS)];
                var (x, y) = Along(q, r.Range(0.1, 0.7) * q.tl);
                double k = Px.Sin((Math.PI * age) / life);
                if (q.sa < -0.3) return;
                p.Px(x, y, WHITE);
                if (k > 0.6) foreach (int[] v in Sim.Cross) p.Px(x + v[0], y + v[1], C[6]);
                p.Glow(x, y, 5, C[5], 0.4 * k);
            });
            // foam where the jet leaves the spout
            for (int i = 0; i < 4; i++) p.Px(Px.Sin(T * 3 + i * 1.7) * 2.5, BY - 16 - (i % 2), i % 2 != 0 ? C[6] : WHITE);
            // the arcs on the near side, in front of the jet
            foreach (Arc q in Q)
                if (q.sa >= 0) DrawArc(p, C, q, ph, false);
            // where they come down: near halves of the rings, flecks and tiny crowns thrown up, light on the water
            for (int i = 0; i < Q.Length; i++)
            {
                Arc q = Q[i];
                double f = (ph * 2 + Rand(i, S + 9)) % 1;
                var (x, y) = Along(q, q.tl);
                Ring(p, x, y, 2 + f * 8, SY, 0.8 * Px.Pow(1 - f, 1.3), C, n: 1, half: 1, seed: i);
                double g = (ph * 6 + Rand(i, S + 10)) % 1;
                if (g < 0.15) p.Px(x, y - 1, WHITE);
                if (g < 0.3 && q.sa > -0.2) p.Line(x, y - 1, x + (Rand(i, S + 11) - 0.5) * 2, y - 1 - 3 * (1 - g / 0.3), C[6]);
                p.Glow(x, y, 6, C[5], 0.18 * (1 - g));
            }
            Basin(p, ST, 1);
            // a fine mist hangs round the bloom
            Cycle(t, D, 16, S + 20, g => g.Range(0.8, 1.4), (age, life, r, _) =>
            {
                double k = age / life, a = r.Range(0, TAU), rr = r.Range(4, 20) + k * 6, x = Px.Cos(a) * rr, y = BY - JET + r.Range(-4, 16) + k * 10;
                if (k > 0.75 && r.Next() < (k - 0.75) * 4) return;
                p.Px(x, y, r.Next() < 0.4 ? WHITE : C[6]);
            });
            // light: the jet, and the basin water lit from above
            p.GlowLine(0, BY - 16, 0, BY - JET, 10, C[5], 0.18);
            p.Glow(0, BY - LEVEL - 2, 46, C[4], 0.26 + 0.03 * Px.Sin(T * 2));
            p.Glow(0, BY - LEVEL - 20, 30, C[5], 0.12);
        }
    }
}
