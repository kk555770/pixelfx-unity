// Shared by the explosions. Not an effect.
// The fire balls, rocks and scorch are adapted from Sprite Fusion's open-source explosion
// (https://www.spritefusion.com/pixel-art-vfx/explosion), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    internal static class ExplosionsLib
    {
        public static readonly uint OUTLINE = Hex("#1a1422"), SOOT = Hex("#0d0a12"), SOOT2 = Hex("#17121d");

        /// <summary>Stone colors from one body color: a lit face, the body, a shaded face, and the dust it makes.</summary>
        public readonly struct Stone
        {
            public readonly uint light, body, shade, dust;

            public Stone(uint c)
            {
                light = Mix(Scale(c, 1.35), WHITE, 0.08);
                body = c;
                shade = Scale(c, 0.64);
                dust = Mix(Scale(c, 1.2), Rgb(150, 140, 150), 0.3);
            }

            public Stone(uint light, uint body, uint shade)
            {
                this.light = light;
                this.body = body;
                this.shade = shade;
                dust = 0;
            }
        }

        /// <summary>A 9-step fire ramp, white-hot down to smouldering (the shape of FIRE_RAMP), from an 8-step palette (dark to bright).</summary>
        public static uint[] FireRamp(uint[] F) => new[] { F[7], F[6], F[5], F[4], F[3], F[2], F[1], Mix(F[1], F[0], 0.5), F[0] };

        /// <summary>Heat for a scorch, from a fire ramp (bright to dark) down to cinders.</summary>
        public static uint[] HeatRamp(uint[] FR)
        {
            var o = new uint[10];
            Array.Copy(FR, o, 8);
            o[8] = Mix(FR[8], SOOT, 0.3);
            o[9] = Mix(FR[8], SOOT, 0.65);
            return o;
        }

        /// <summary>A particle born where a rock was born (BigFive's moving emitter).</summary>
        public static void BornWith(Particles ps, Bits.Bit b) => ps.BornAt(b.bornX, b.bornY);

        /// <summary>One FIRE particle drawn in the fire ramp FR: a darker rim, a lit core, a tongue on top, a hot heart.</summary>
        public static void FireBall(Painter p, Particles.Particle q, uint[] FR)
        {
            double k = q.life / q.max, age = 1 - k, x = q.x, y = q.y;
            int n = FR.Length;
            double heat = Math.Min(0.999, Px.Pow(age, 0.62) + (q.seed % 1) * 0.1);
            int idx = (int)Math.Min(n - 2, Math.Floor(heat * (n - 1)));
            double r = q.size * (0.55 + 0.45 * Math.Min(1, age * 7)) * (1 - age * 0.55);
            uint c = FR[idx];
            if (idx >= n - 2)
            {
                p.Disc(x, y, r, c, Math.Min(1, k * 3) * 0.85);
                return;
            }
            // the hottest puffs get a rim a step darker still, so a mass of them reads as separate balls
            p.Circle(x, y, r, FR[Math.Min(n - 1, idx + (idx <= 2 ? 3 : 2))]);
            p.Circle(x - r * 0.16, y - r * 0.22, r * 0.78, c);
            if (r > 1.6) p.Circle(x, y - r * 0.9, r * 0.42, c);
            if (idx <= 3 && r > 1.8) p.Circle(x - r * 0.28, y - r * 0.34, r * 0.36, FR[Math.Max(0, idx - 1)]);
            if (idx < 6) p.Glow(x, y, r * 2.4 + 2, c, 0.18 * k);
        }

        // ps.list swapped for a part of it while it draws, as the JS does
        static void DrawOnly(Painter p, Particles ps, List<Particles.Particle> part, List<Particles.Particle> all, Layer layer)
        {
            ps.List.Clear();
            ps.List.AddRange(part);
            ps.Draw(p, layer);
            ps.List.Clear();
            ps.List.AddRange(all);
        }

        /// <summary>A particle system's layer (back or front) with its FIRE puffs in the fire ramp FR, so a palette recolors them.</summary>
        public static void DrawPs(Painter p, Particles ps, Layer layer, uint[] FR, Emitter e)
        {
            var all = new List<Particles.Particle>(ps.List);
            var fires = new List<Particles.Particle>();
            var rest = new List<Particles.Particle>();
            foreach (Particles.Particle q in all) (q.k == P.Fire ? fires : rest).Add(q);
            bool back = layer == Layer.Back;
            if (!back)
            {
                foreach (Particles.Particle q in fires)
                    if (q.delay <= 0 && (q.flag & PF.Back) == 0)
                    {
                        Sim.Shift(p, e, q.bornX, q.bornY);
                        FireBall(p, q, FR);
                    }
                p.Shift(0, 0);
            }
            DrawOnly(p, ps, rest, all, layer);
            if (back)
            {
                foreach (Particles.Particle q in fires)
                    if (q.delay <= 0 && (q.flag & PF.Back) != 0)
                    {
                        Sim.Shift(p, e, q.bornX, q.bornY);
                        FireBall(p, q, FR);
                    }
                p.Shift(0, 0);
            }
        }

        static bool Lifts(P k) => k == P.Smoke || k == P.Dust || k == P.Fire || k == P.Ember || k == P.Vapour;

        /// <summary>
        /// An updraft column at x: smoke, dust, fire and embers below top and within w of the axis are carried up
        /// (up px/s²) and squeezed toward it; near the floor (within low px) they're drawn in from as far as reach:
        /// the afterwinds that feed a blast's column. k scales it all.
        /// </summary>
        public static void Draft(Particles ps, double dt, double x = 0, double top = double.NegativeInfinity, double w = 12, double up = 150, double squeeze = 4,
            double pull = 0, double low = 10, double reach = 100, double k = 1)
        {
            if (k <= 0) return;
            foreach (Particles.Particle q in ps.List)
            {
                if (q.delay > 0 || !Lifts(q.k) || q.y < top) continue;
                double dx = q.x - x, ax = Math.Abs(dx), u = ax / w;
                if (u < 1)
                {
                    double f = (1 - u * u) * k;
                    q.vy -= up * f * dt;
                    q.vx -= dx * squeeze * f * dt;
                }
                if (pull != 0 && !double.IsNaN(pull) && q.y > -low && ax > w * 0.4 && ax < reach) q.vx -= Sign(dx) * pull * k * (1 - ax / reach) * dt;
            }
        }

        /// <summary>Drop particles that leave the box (local px), so nothing is drawn past the canvas.</summary>
        public static void Cull(Particles ps, double x0, double y0, double x1, double y1)
        {
            List<Particles.Particle> L = ps.List;
            for (int i = L.Count - 1; i >= 0; i--)
            {
                Particles.Particle q = L[i];
                if (q.x < x0 || q.x > x1 || q.y < y0 || q.y > y1) L.RemoveAt(i);
            }
        }

        static bool Soft(P k) => k == P.Smoke || k == P.Dust || k == P.Vapour;

        /// <summary>
        /// Soft particles (smoke, dust, vapour) fade out over w px as they near the sides x0, x1 and go before they reach
        /// them; y1, a bottom edge, gets a thin w of 6.
        /// </summary>
        public static void FadeEdges(Particles ps, double x0, double x1, double w = 30, double y1 = double.PositiveInfinity)
        {
            List<Particles.Particle> L = ps.List;
            for (int i = L.Count - 1; i >= 0; i--)
            {
                Particles.Particle q = L[i];
                if (!Soft(q.k)) continue;
                double r = q.size * 1.65, e = Math.Min((Math.Min(q.x - x0, x1 - q.x) - r) / w, (y1 - q.y - r) / 6);
                if (e <= 0) L.RemoveAt(i);
                else if (e < 1) q.life = Math.Min(q.life, q.max * 0.42 * e);
            }
        }

        /// <summary>A soft pool of light, an ellipse rx by ry at (x, y): light falling on the floor.</summary>
        public static void Pool(Painter p, double x, double y, double rx, double ry, uint c, double k)
        {
            if (k <= 0.004 || rx < 1 || ry < 0.5) return;
            p.Shade(x - rx, y - ry, x + rx, y + ry, (px, py, o) =>
            {
                double dx = (px - x) / rx, dy = (py - y) / ry, d = dx * dx + dy * dy;
                if (d >= 1) return 0;
                double f = 1 - Math.Sqrt(d);
                o.l = c;
                o.k = f * f * k;
                return 0;
            });
        }

        /// <summary>A translucent ellipse (blended, never dithered): c and a are functions of d (0 centre .. 1 rim).</summary>
        public static void Haze(Painter p, double x, double y, double rx, double ry, Func<double, uint> c, Func<double, double> a)
        {
            if (rx < 0.5 || ry < 0.5) return;
            p.Each(x - rx, y - ry, x + rx, y + ry, (lx, ly, sx, sy) =>
            {
                double dx = (lx - x) / rx, dy = (ly - y) / ry, d2 = dx * dx + dy * dy;
                if (d2 > 1) return;
                double d = Math.Sqrt(d2);
                p.BlendPx(sx, sy, c(d), a(d));
            });
        }

        /// <summary>
        /// The floor under a blast: a burn that spreads out in a blink, glows white-hot and cools through the heat ramp
        /// HR to soot over cool seconds; a few cracks hold their glow twice as long. rows deep, fade 0..1.
        /// </summary>
        public static void Scorch(Painter p, uint[] HR, double t, double rx0, double cool, double fade, int rows = 3)
        {
            if (fade <= 0) return;
            double rx = rx0 * Math.Min(1, t / 0.08), ry = rows + 1.5;
            int n = HR.Length;
            for (int y = 0; y < rows; y++)
            for (double x = Math.Floor(-rx); x <= Math.Ceiling(rx); x++)
            {
                double dx = (x + 0.5) / rx, dy = (y + 0.5) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1) continue;
                double k = Rand(x * 31 + y, 3), T = Px.Pow(Clamp(1 - t / cool), 1.4) * (1.25 - d) - k * 0.35;
                double crack = k > 0.88 && d < 0.85 ? Px.Pow(Clamp(1 - t / (cool * 2)), 1.2) * (1.15 - d) * 0.8 : 0, H = Math.Max(T, crack);
                if (H > 0.06)
                {
                    uint c = HR[(int)Math.Min(n - 1, Math.Floor((1 - Math.Min(1, H)) * (n - 1)))];
                    p.Dot(x, y, c, fade);
                    if ((ToInt32(x) & 3) == 0 || crack > T) p.Glow(x + 0.5, y, 6, c, 0.18 * H * fade);
                }
                else p.Dot(x, y, d < 0.5 ? SOOT : SOOT2, (d < 0.95 - k * 0.3 ? 1 : 1 - d) * fade);
            }
        }

        /// <summary>
        /// A rock (a Bits CHUNK): a lit side, a shaded side and an outline, turning as it spins. heat (0..1) makes
        /// it molten: its heart and shaded side glow through the fire ramp FR and it gives off light, cooling to stone.
        /// </summary>
        public static void Rock(Painter p, Emitter e, Bits.Bit b, Stone st, uint[] FR, double heat = 0)
        {
            double r = 0.6 + b.s * 0.75, fade = Math.Min(1, b.life / 0.5), lx = Px.Cos(b.a) * r * 0.4, ly = Px.Sin(b.a) * r * 0.4;
            Sim.Shift(p, e, b.bornX, b.bornY);
            p.Shade(b.x - r - 1, b.y - r - 1, b.x + r + 1, b.y + r + 1, (x, y, o) =>
            {
                double dx = x - b.x, dy = y - b.y, d = Hypot(dx * 1.15, dy);
                if (d > r + 0.9) return 0;
                o.a = fade;
                if (d > r) return OUTLINE;
                double lit = dx * lx + dy * ly;
                if (heat > 0.08)
                {
                    double g = heat * (1.1 - d / (r + 0.5)) + (lit > 0.3 ? heat * 0.45 : 0);
                    if (g > 0.5) return FR[g > 1 ? 1 : g > 0.8 ? 2 : g > 0.62 ? 3 : 4];
                    if (g > 0.32) return FR[5];
                }
                return lit < -0.4 ? st.light : lit > 0.6 ? st.shade : st.body;
            });
            if (heat > 0.05) p.Glow(b.x, b.y, r * 2 + 3, FR[3], 0.32 * heat * fade);
            p.Shift(0, 0);
        }

        /// <summary>
        /// Light hugging the upper half of a circle of radius R at (x, y) (a dome's shock), falling off over width w.
        /// box [x0, y0, x1, y1] (local) keeps it inside a canvas.
        /// </summary>
        public static void GlowDome(Painter p, double x, double y, double R, double w, uint c, double k, double[] box = null)
        {
            if (k <= 0.004 || w < 1) return;
            box = box ?? new[] { double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity };
            p.Shade(Math.Max(box[0], x - R - w), Math.Max(box[1], y - R - w), Math.Min(box[2], x + R + w), Math.Min(box[3], y + 0.5), (px, py, o) =>
            {
                double d = Math.Abs(Hypot(px - x, py - y) - R) / w;
                if (d >= 1) return 0;
                o.l = c;
                o.k = (1 - d) * (1 - d) * k;
                return 0;
            });
        }

        public struct TrailPt
        {
            public double x, y, t;
        }

        /// <summary>
        /// A burning thing's trail: pts (oldest first) at time t, drawn as a ribbon that is fire near the head (fire ramp
        /// FR) and swells into thin smoke behind (smoke ramp SM, light to dark) over life seconds.
        /// </summary>
        public static void Trail(Painter p, List<TrailPt> pts, double t, uint[] FR, uint[] SM, double life = 0.5, double w = 1, double heat = 1)
        {
            double lx = double.NaN, ly = double.NaN;
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                TrailPt q = pts[i];
                double age = (t - q.t) / life;
                if (age >= 1) break;
                if (Hypot(q.x - lx, q.y - ly) < 1.6) continue;
                lx = q.x;
                ly = q.y;
                double r = w * (0.7 + age * 2.6);
                if (age < 0.12 * heat) p.Circle(q.x, q.y - age * 6, r * 0.8, FR[age < 0.05 ? 3 : 5]);
                else p.Disc(q.x, q.y - age * 6, r, SM[(int)Math.Min(SM.Length - 1, 1 + Math.Floor(age * 3))], 0.55 * (1 - age));
            }
        }

        /// <summary>
        /// Draw a particle system's GRIT as dirt in flight: fast bits stretch into short streaks (a darker tail, the bit at
        /// the head), slow ones are the usual 1 or 2 px chips. The rest of the system draws as usual.
        /// </summary>
        public static void DrawDirt(Painter p, Particles ps, Layer layer, Emitter e)
        {
            var all = new List<Particles.Particle>(ps.List);
            var grit = new List<Particles.Particle>();
            var rest = new List<Particles.Particle>();
            foreach (Particles.Particle q in all) (q.k == P.Grit ? grit : rest).Add(q);
            DrawOnly(p, ps, rest, all, layer);
            if (layer == Layer.Back) return;
            foreach (Particles.Particle q in grit)
            {
                if (q.delay > 0) continue;
                double k = q.life / q.max, sp = Hypot(q.vx, q.vy);
                if (k < 0.25 && k * 4 < 0.5) continue;
                Sim.Shift(p, e, q.bornX, q.bornY);
                if (sp > 140)
                {
                    double L = Math.Min(5, sp * 0.012), ux = q.vx / sp, uy = q.vy / sp;
                    uint col = q.col, tail = Scale(col, 0.7);
                    p.Line(q.x - ux * L, q.y - uy * L, q.x, q.y, u => u < 0.5 ? tail : col);
                }
                else
                {
                    p.Px(q.x, q.y, q.col);
                    if (q.size >= 2)
                    {
                        p.Px(q.x + 1, q.y, q.col);
                        p.Px(q.x, q.y + 1, Scale(q.col, 0.8));
                        p.Px(q.x + 1, q.y + 1, Scale(q.col, 0.7));
                    }
                }
            }
            p.Shift(0, 0);
        }

        /// <summary>
        /// A crater in the floor at x: a dark pit with its far wall lit, dirt heaped in a lip on either side, and loose
        /// earth scattered round it. E: an earth ramp (dark to bright, 5+ steps); r: half-width; k: 0..1 how far it has
        /// opened; a: alpha.
        /// </summary>
        public static void Crater(Painter p, uint[] E, double x, double r, double k, double a = 1)
        {
            if (k <= 0 || a <= 0) return;
            int n = E.Length;
            double rx = r * k, ry = Math.Max(1.5, r * 0.17 * k);
            for (double py = -1; py <= Math.Ceiling(ry) + 1; py++)
            for (double px = Math.Floor(-rx - 1); px <= Math.Ceiling(rx + 1); px++)
            {
                double dx = (px + 0.5) / rx, dy = (py + 0.5 - ry * 0.35) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1.08) continue;
                // the pit: darkest at its far side under the lit rim, the near wall a step lighter
                uint c = d > 0.92 ? (dy < 0 ? E[n - 2] : E[2]) : dy < -0.2 ? E[0] : dy < 0.35 ? E[1] : E[2];
                p.Dot(x + px, py, c, a);
            }
            // the lip: dirt heaped up either side, lit on top, and a scatter of loose earth
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < 9; i++)
                {
                    double u = i / 8.0, h = Round(Px.Sin(Math.PI * u) * 2.2 * k + (Rand(i, s > 0 ? 3 : 4) < 0.4 ? 1 : 0) * k);
                    double hx = x + s * (rx * 0.82 + u * r * 0.55);
                    for (double j = 0; j <= h; j++) p.Dot(hx, -j, j == h ? E[n - 2] : j == h - 1 ? E[3] : E[2], a);
                }
            for (int i = 0; i < 26; i++)
            {
                double s = Rand(i, 7) < 0.5 ? -1 : 1, d = rx * (1.05 + Rand(i, 8) * 1.4), y = Round(Rand(i, 9) * 3) - 1;
                if (Rand(i, 10) < k) p.Dot(x + s * d, y, Rand(i, 11) < 0.5 ? E[3] : E[2], a);
            }
        }

        /// <summary>A hemisphere of white-hot air on the floor at x, radius r: an orange rim, a hot body, a white core; k 1 → 0 as it dies.</summary>
        public static void Dome(Painter p, uint[] F, double x, double r, double k)
        {
            double e = 1.4 / r;
            p.Shade(x - r - 1.5, -r - 1.5, x + r + 1.5, 0.5, (px, py, o) =>
            {
                double d = Hypot(px - x, py) / r;
                if (d > 1 + e) return 0;
                return d > 1 ? F[2] : d > 0.8 ? (k > 0.45 ? F[6] : F[5]) : d > 0.5 ? (k > 0.45 ? WHITE : F[6]) : k > 0.2 ? WHITE : F[6];
            });
        }

        /// <summary>
        /// A lobe of a billowing cloud, shaded like a ball. The sky lights it from the top left through the smoke ramp SM
        /// (light to dark); fire (0..1) burns in its heart in the fire ramp FR (bright to dark); glow (0..1) lights its
        /// underside from below in fire colors. a: opacity; z: -1 far .. 1 near; dark shifts the smoke down the ramp.
        /// style 1 cel-shades the smoke.
        /// </summary>
        public static void Lobe(Painter p, double x, double y, double r, uint[] FR, uint[] SM, double fire, double glow, double a, double z = 0, int dark = 0, int style = 0)
        {
            if (r < 0.7 || a <= 0.01) return;
            int n = FR.Length, m = SM.Length, back = z < -0.35 ? 1 : 0;
            double rim = 1 - Math.Min(0.35, 1.6 / r), seam = 1 - Math.Min(0.3, 1.3 / r);
            // the soft edge: a step more see-through than the body, less of a step as the whole lobe thins (no bubble rims)
            uint D(int i) => SM[Math.Min(m - 1, i + back + dark)];
            double soft = 0.55 + 0.4 * (1 - Math.Min(1, a));
            void Px(double lx, double ly, int sx, int sy)
            {
                double dx = (lx - x) / r, dy = (ly - y) / r, d2 = dx * dx + dy * dy;
                if (d2 > 1) return;
                double d = Math.Sqrt(d2), nz = Math.Sqrt(1 - d2), sky = -0.5 * dx - 0.66 * dy + 0.56 * nz;
                double v = fire * (1.32 - 0.78 * d) + 0.14 * sky * fire;
                uint c;
                double k = 0;
                bool solid = false;
                if (v > 0.44)
                {
                    int i = (int)Math.Min(n - 3, Math.Max(0, Math.Floor((1.3 - v) * (n - 1) * 0.78)));
                    if (d > seam) i = Math.Min(6, Math.Max(4, i + 2));
                    else if (d > seam - 0.22) i = Math.Min(n - 3, i + 1);
                    c = FR[i];
                    k = 0.05 * fire;
                    solid = true;
                }
                else if (style != 0)
                {
                    double lam = -0.42 * dx - 0.78 * dy + 0.46 * nz;
                    if (lam > 0.66 && d > 0.4) c = D(0);
                    else if (lam > 0.16) c = D(1);
                    else if (lam > -0.3) c = D(2);
                    else
                    {
                        c = D(lam < -0.64 ? 4 : 3);
                        double g = glow * (0.6 + 0.4 * dy);
                        if (g > 0.55)
                        {
                            c = FR[g > 0.8 ? 5 : 6];
                            k = 0.08 * g;
                        }
                        else if (g > 0.34) c = Mix(c, FR[6], 0.6);
                        else if (g > 0.16) c = Mix(c, FR[7], 0.45);
                    }
                }
                else
                {
                    double g = glow * (0.82 * dy + 0.3 * nz);
                    if (g > 0.45)
                    {
                        c = FR[g > 0.66 ? 4 : 5];
                        k = 0.1 * glow;
                    }
                    else if (g > 0.3) c = FR[6];
                    else if (g > 0.2) c = FR[7];
                    else
                    {
                        double s = sky * 0.5 + 0.5;
                        c = D(s > 0.8 ? 0 : s > 0.62 ? 1 : s > 0.42 ? 2 : s > 0.22 ? 3 : 4);
                    }
                }
                p.BlendPx(sx, sy, c, solid || d <= rim ? a : a * soft);
                if (k > 0) p.Add(sx, sy, c, k);
            }
            Painter.State ps = p.Save();
            if (ps.Sn != 0 || ps.Cs != 1)
            {
                p.Each(x - r, y - r, x + r, y + r, Px);
                return;
            }
            // unrotated (the usual case): walk the screen pixels directly
            double tx = ps.Tx, ty = ps.Ty, fx = ps.Fx;
            Frame f = p.Frame;
            double ox = f.Ox, oy = f.Oy, w = f.W, h = f.H;
            double ax = tx + (x - r) * fx, bx = tx + (x + r) * fx;
            double sx0 = Math.Max(ox, Math.Floor(Math.Min(ax, bx))), sx1 = Math.Min(ox + w - 1, Math.Ceiling(Math.Max(ax, bx)));
            double sy0 = Math.Max(oy, Math.Floor(ty + y - r)), sy1 = Math.Min(oy + h - 1, Math.Ceiling(ty + y + r));
            for (double sy = sy0; sy <= sy1; sy++)
            {
                double ly = sy + 0.5 - ty, q = (ly - y) / r;
                if (q * q >= 1) continue;
                // only the span of this row inside the circle
                double hw = r * Math.Sqrt(1 - q * q), cx = tx + x * fx;
                for (double sx = Math.Max(sx0, Math.Floor(cx - hw - 0.5)), e = Math.Min(sx1, Math.Ceiling(cx + hw)); sx <= e; sx++) Px((sx + 0.5 - tx) * fx, ly, (int)sx, (int)sy);
            }
        }

        /// <summary>
        /// A rolling fireball. n lobes ride a vortex ring that climbs, swells and rolls, while it cools from white-hot fire
        /// into smoke that glows from inside, then thins away. See the JS original for the options.
        /// </summary>
        public sealed class Plume
        {
            public sealed class Opts
            {
                public double x = 0, y = -20, rise = 100, tau = 1.2, drift = 4, grow = 0.9, swell = 0.5, spin = 2.4, spinTau = 1.6, heat = 1.15, cool = 0.45,
                    under = 0.5, tilt = 0.2, squash = 0.7, glow = 1, spread = 10;
                public double[] tube = { 8, 20 }, ring = { 1, 18 }, size = { 5, 10 }, fade = { 4, 6 };
                public int n = 40, style = 0;
                public bool inner = false, hollow = false;
                public double[] fray = null;
            }

            sealed class Seed
            {
                public double th, ph, j, s, h, dx, dy, f, gv;
            }

            public struct Shape
            {
                public double x, y, tube, ring, g;
            }

            sealed class At
            {
                public double x, y, z, up, @out, heat, fade, r, d, c;
                public Seed q;
            }

            public readonly Opts o;
            readonly Rng R;
            readonly List<Seed> lobes;
            public double t, roll;
            public Action<double, double, double, double, double> onFray;

            public Plume(Rng R, Opts o)
            {
                this.o = o;
                this.R = R;
                // spread over the ring evenly: the roll angle on a golden-ratio walk, the turn round the axis in order
                lobes = new List<Seed>(o.n);
                for (int i = 0; i < o.n; i++)
                    lobes.Add(new Seed
                    {
                        th = TAU * ((i * 0.618034 + R.Range(-0.08, 0.08)) % 1), ph = (TAU * (i + R.Range(-0.35, 0.35))) / o.n,
                        j = R.Range(-0.55, 0.12), s = R.Range(o.size[0], o.size[1]), h = R.Range(-0.22, 0.08), dx = R.Range(-1, 1), dy = R.Range(-1, 0.4),
                        f = R.Range(-0.9, 0.5), gv = R.Range(0.55, 1.15),
                    });
            }

            public Shape ShapeAt(double t)
            {
                double u = Math.Max(0, t), g = 1 - Exp(-u / o.grow);
                return new Shape
                {
                    x = o.x, y = o.y - o.rise * (1 - Exp(-u / o.tau)) - o.drift * u, tube = o.tube[0] + (o.tube[1] - o.tube[0]) * g,
                    ring = o.ring[0] + (o.ring[1] - o.ring[0]) * g, g = g,
                };
            }

            public Shape ShapeNow() => ShapeAt(t);

            public double Hot(double t) => Clamp(o.heat - o.cool * t + 0.3);
            public double Hot() => Hot(t);

            /// <summary>How strongly the inside still glows (0..1), lingering well after the fire has gone.</summary>
            public double Ember(double t) => Clamp((o.heat - o.cool * t + 1) / 1.1);
            public double Ember() => Ember(t);

            static double Or1(double v) => v != 0 && !double.IsNaN(v) ? v : 1;

            // where every lobe is at time t
            List<At> LobesAt(double t)
            {
                Shape S = ShapeAt(t);
                double @base = o.heat - o.cool * t, span = Or1(S.ring + S.tube);
                var list = new List<At>();
                foreach (Seed q in lobes)
                {
                    double fade = 1 - Smooth(o.fade[0] + q.f, o.fade[1] + q.f * 0.5, t), loose = Px.Pow(Smooth(o.fade[0] - 1.8 + q.f, o.fade[1], t), 1.5) * o.spread;
                    if (fade <= 0) continue;
                    double th = q.th - roll, rt = S.tube * (1 + q.j), rho = S.ring + rt * Px.Cos(th), up = Px.Sin(th);
                    double X = rho * Px.Cos(q.ph), Z = rho * Px.Sin(q.ph);
                    // the outside cools first; the underside keeps its heat, fed from below
                    double heat = @base + q.h - 0.18 * Math.Max(0, Px.Cos(th)) * Clamp(t * 2) + o.under * Math.Max(0, -up) * Clamp(@base + 0.6);
                    double r = q.s * (0.5 + 0.5 * S.g) * (1 + o.swell * Clamp(1 - heat)) * (1 + 0.35 * (1 - fade));
                    list.Add(new At
                    {
                        x = S.x + X + q.dx * loose, y = S.y - rt * up * o.squash + Z * o.tilt + q.dy * loose, z = Z / span, up = up, @out = Px.Cos(th), heat = heat, q = q,
                        fade = fade, r = r, d = Hypot(X, Z) / span, c = 1 - Math.Min(1, Math.Abs(X) / span),
                    });
                }
                return list.OrderBy(a => a.z).ToList();
            }

            public void Update(double dt)
            {
                t += dt;
                roll += o.spin * (0.12 + 0.88 * Exp(-t / o.spinTau)) * dt;
                // late on, the rim frays: lobes on the outside shed small puffs that drift off and thin
                if (o.fray != null && onFray != null && t > o.fray[0] && t < (o.fray.Length > 2 ? o.fray[2] : double.PositiveInfinity))
                {
                    Shape S = ShapeNow();
                    double k = o.fray[1] * dt * Smooth(o.fray[0], o.fray[0] + 1, t);
                    foreach (At e in LobesAt(t))
                    {
                        if (e.@out < 0.2 || !R.Chance(k * e.fade)) continue;
                        double ux = e.x - S.x, uy = e.y - S.y, l = Or1(Hypot(ux, uy));
                        onFray(e.x + (ux / l) * e.r * 0.6, e.y + (uy / l) * e.r * 0.6, e.r, ux / l, uy / l);
                    }
                }
            }

            public void Draw(Painter p, uint[] FR, uint[] SM, Action mid = null)
            {
                if (t >= o.fade[1] + 0.5) return;
                Shape S = ShapeAt(t);
                double @base = o.heat - o.cool * t, span = Or1(S.ring + S.tube), age = Clamp(t / o.fade[1]);
                List<At> list = LobesAt(t);
                double E = Ember(t), reach = 0.3 + 1.15 * E;
                bool drawn = false;
                foreach (At e in list)
                {
                    if (!drawn && e.z >= 0 && mid != null)
                    {
                        drawn = true;
                        mid();
                    }
                    double h = e.heat, fade = e.fade, fire = Clamp((h - 0.2) / 0.75);
                    // the glow from inside: strongest under the middle, uneven from lobe to lobe; with inner, it draws back to the
                    // middle as the cap cools
                    double glow = o.inner
                        ? E * Clamp((reach - e.d) / 0.45) * (0.45 + 0.55 * Math.Max(0, -e.up)) * e.q.gv * o.glow
                        : Clamp((@base + 0.45) / 0.85) * o.glow * (0.4 + 0.35 * Math.Max(0, -e.up) + 0.4 * e.c) * e.q.gv;
                    Lobe(p, e.x, e.y, e.r, FR, SM, fire, glow, fade * (0.82 + 0.18 * fire), e.z, (age > 0.72 ? 1 : 0) + (o.hollow && e.up < -0.35 ? 1 : 0), o.style);
                    if (fire > 0.25) p.Glow(e.x, e.y, e.r * 1.25 + 2, FR[2], 0.07 * fire);
                }
                if (!drawn && mid != null) mid();
                double hot = Hot(t);
                if (hot > 0) p.Glow(S.x, S.y, span * 1.8 + 14, FR[3], 0.32 * hot * o.glow);
                if (o.inner && E > 0.05) p.Glow(S.x, S.y + S.tube * 0.3, span * (0.5 + 0.7 * E) + 8, FR[4], 0.22 * E * E);
            }
        }

        /// <summary>
        /// A rising column: lobes boil up a stem from the floor into whatever sits on top, wide at the foot, swaying,
        /// cooling from fire to smoke and dust. Update(dt, top, feed); Draw(p, top, FR, SM, fire, glow).
        /// </summary>
        public sealed class Column
        {
            public sealed class Opts
            {
                public double w = 7, foot = 16, speed = 70, n = 18, sway = 2.5, fade = 0.5;
                public double[] size = { 5, 8 };
                public int style = 0;
            }

            sealed class Bit
            {
                public double y, x, s, ph, a, v;
            }

            readonly Rng R;
            readonly Opts o;
            readonly List<Bit> lobes = new List<Bit>();
            double t, acc;

            public Column(Rng R, Opts o)
            {
                this.R = R;
                this.o = o;
            }

            void Spawn(double y) => lobes.Add(new Bit { y = y, x = R.Range(-1, 1), s = R.Range(o.size[0], o.size[1]), ph = R.Range(0, TAU), a = 0, v = R.Range(0.8, 1.2) });

            public void Update(double dt, double top, double feed)
            {
                double L = Math.Max(10, -top);
                t += dt;
                if (feed > 0)
                    for (acc += dt * feed * o.n * (o.speed / L); acc > 1; acc--)
                        Spawn(-1);
                for (int i = lobes.Count - 1; i >= 0; i--)
                {
                    Bit b = lobes[i];
                    double u = Clamp(-b.y / L);
                    b.y -= o.speed * b.v * (0.45 + 0.55 * Math.Min(1, -b.y / 24)) * dt;
                    b.a = Math.Min(1, b.a + dt * 4);
                    if (b.y < top || (feed <= 0 && (b.a -= dt * (2 - u)) <= 0)) lobes.RemoveAt(i);
                }
            }

            public void Draw(Painter p, double top, uint[] FR, uint[] SM, double fire, double glow)
            {
                double L = Math.Max(10, -top);
                // draw from the top down so the foot sits in front
                foreach (Bit b in lobes.OrderBy(q => q.y).ToList())
                {
                    double u = Clamp(-b.y / L), foot = Px.Pow(1 - u, 4), w = o.w + o.foot * foot;
                    double x = b.x * w * 0.45 + Px.Sin(t * 1.7 + b.ph + u * 3) * o.sway * u;
                    double r = b.s * (0.75 + 0.5 * foot + 0.35 * u) * Math.Min(1, b.a * 1.5);
                    double end = 1 - Smooth(0.85, 1, u);
                    Lobe(p, x, b.y - r * 0.3 * foot, r, FR, SM, Clamp(fire * (1.1 - u * 0.9)), glow * Smooth(0.55, 1, u), Math.Min(1, b.a) * end * 0.9, 0, u > 0.5 ? 0 : 1, o.style);
                }
            }
        }
    }
}
