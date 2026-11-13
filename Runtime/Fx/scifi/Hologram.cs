using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class Hologram : Effect
    {
        public static readonly Module Module = new Module("hologram", "pixelfx/fx/scifi/hologram", 112, 104, 56, 92, 4, true, 30,
            new Options
            {
                ["palette"] = new[] { "#050c1e", "#0a1f48", "#123b80", "#1d64b8", "#2e98e8", "#6cd0ff", "#c8f0ff", "#ffffff" },
                ["metal"] = new[] { "#0e0d14", "#1e1c27", "#312e3e", "#4b4759", "#6f6a82", "#9d98b0" },
                ["split"] = "#ff4fd8",
                ["seed"] = 21,
            },
            (o, e) => new Hologram(o, e))
        {
            Title = "Hologram",
            Desc = "A floor projector throws a cone of light holding a turning wireframe model: scanlines roll through it, a data ring circles it, and it flickers, tears and jitters.",
            Tags = new[] { "scifi", "hologram", "projector", "wireframe", "display", "glitch", "ground", "loop" },
            Bg = "ground",
        };

        const double D = 4, OY = -50, RAD = 16, LENS = -6;
        // the model: a tall six-sided crystal, a point at each end and a ring of six round its waist
        static readonly double[][] VERTS = new[] { new double[] { 0, 1.45, 0 }, new double[] { 0, -1.45, 0 } }
            .Concat(Enumerable.Range(0, 6).Select(i => new[] { Px.Cos((i / 6.0) * TAU) * 0.9, 0.12, Px.Sin((i / 6.0) * TAU) * 0.9 })).ToArray();
        static readonly int[][] FACES = Enumerable.Range(0, 6).SelectMany(i => new[] { new[] { 0, 2 + i, 2 + ((i + 1) % 6) }, new[] { 1, 2 + ((i + 1) % 6), 2 + i } }).ToArray();
        static readonly int[][] EDGES = Enumerable.Range(0, 6).SelectMany(i => new[] { new[] { 0, 2 + i }, new[] { 1, 2 + i }, new[] { 2 + i, 2 + ((i + 1) % 6) } }).ToArray();
        // when it glitches: [start, length, how hard]
        static readonly double[][] GLITCHES = { new[] { 0.92, 0.12, 1 }, new[] { 2.55, 0.2, 1.4 }, new[] { 3.42, 0.06, 0.7 } };

        readonly uint[] C, H;
        readonly uint SPLIT;
        readonly double S;

        Hologram(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            H = Fit(o.Ramp("metal"), 6);
            SPLIT = o.Color("split");
            S = o.Seed;
        }

        public override void Draw(Painter p, double t)
        {
            double ph = t / D, frame = Math.Floor(t * 30);
            double gl = 0;
            foreach (double[] g in GLITCHES) gl = t >= g[0] && t < g[0] + g[1] ? Math.Max(gl, g[2]) : gl;
            double flick = gl > 0 && Rand(S, frame) < 0.5 ? 1 : 0;
            // where it sits: a slow bob, a jolt when it glitches
            double cx = gl != 0 ? (Rand(S, frame, 2) < 0.5 ? -2 : 2) * gl : 0, cy = OY + 1.5 * Px.Sin(TAU * ph * 2);
            // the tears this frame: bands of rows knocked sideways
            var tears = new List<double[]>();
            if (gl != 0)
                for (int i = 0; i < 2; i++)
                    tears.Add(new[] { Math.Floor(cy - RAD + Rand(S, frame, i) * RAD * 2), 2 + Math.Floor(Rand(S, frame, i, 1) * 4), Round((Rand(S, frame, i, 2) - 0.5) * 10 * gl) });
            double TearAt(double y)
            {
                foreach (double[] tr in tears)
                    if (y >= tr[0] && y < tr[0] + tr[1]) return tr[2];
                return 0;
            }
            // every pixel of the hologram goes through here: interlaced, torn, split
            void Put(double x, double y, uint c, double a = 1)
            {
                double yy = Math.Floor(y);
                if (flick != 0 && (ToInt32(yy) & 1) != 0) return;
                double dx = TearAt(yy);
                if (dx != 0)
                {
                    p.Dot(x + dx, y, SPLIT, a * 0.8);
                    p.Dot(x + dx + Sign(dx), y, c, a);
                    return;
                }
                p.Dot(x, y, c, a);
            }

            // the projector: a steel drum on the floor with a lens on top and lights round it
            p.Shade(-16, -8, 16, 2, (x, y, o) =>
            {
                bool top = Px.Pow(x / 15, 2) + Px.Pow((y + 5) / 3, 2) <= 1, side = Math.Abs(x) <= 15 && y > -5 && y <= 0;
                if (!top && !side) return 0;
                if (top) o.c = Px.Pow(x / 15, 2) + Px.Pow((y + 5) / 3, 2) > 0.8 ? H[0] : y < -6 ? H[5] : H[3];
                else o.c = Math.Abs(x) > 14 || y == 0 ? H[0] : y == -4 ? H[4] : x < -6 ? H[3] : x > 6 ? H[1] : H[2];
                return 0;
            });
            p.Ellipse(0, LENS + 0.5, 5.5, 1.6, (d, g) => d < 0.4 ? WHITE : d < 0.8 ? C[6] : C[4]);
            for (int i = 0; i < 4; i++)
            {
                double k = 0.5 + 0.5 * Px.Cos(TAU * (2 * ph - i / 4.0));
                p.Px(-9 + i * 6, -2, k > 0.7 ? C[6] : C[3]);
            }
            // the cone of light up to the model: thin, fading as it rises, scanlines climbing it
            double top = cy + RAD * 1.3;
            for (double y = Math.Ceiling(top); y < LENS; y++)
            {
                double v = (y - LENS) / (top - LENS), w = Lerp(4, RAD + 4, v);
                bool lineOn = ((((y + t * 24) % 5) + 5) % 5) < 1;
                for (double x = Math.Floor(-w); x <= Math.Ceiling(w); x++)
                {
                    double q = Math.Abs(x + 0.5) / w;
                    if (q > 1) continue;
                    if (q > 1 - 1 / w) Put(x, y, v < 0.5 ? C[5] : C[4]);
                    else p.Blend(x + 0.5, y + 0.5, lineOn ? C[5] : C[4], (0.05 + 0.12 * (1 - v)) * (1 - 0.6 * q * q) + (lineOn ? 0.06 : 0));
                }
            }
            p.Glow(0, LENS, 12, C[5], 0.6);
            // the stage the model stands on: a thin ring where the cone ends
            for (int i = 0; i < 56; i++)
            {
                double a = (i / 56.0) * TAU - ph * TAU, x = Px.Cos(a) * (RAD + 4), y = top + Px.Sin(a) * 3;
                if (i % 7 < 5) Put(x, y, Px.Sin(a) > 0 ? C[6] : C[4], Px.Sin(a) > 0 ? 1 : 0.7);
            }
            p.GlowLine(-RAD - 4, top, RAD + 4, top, 4, C[4], 0.2);
            // data bits rising through the cone
            Cycle(t, D, 18, S + 1, r => r.Range(0.8, 1.4), (age, life, r, i) =>
            {
                double k = age / life, y = Lerp(LENS - 2, top, k), w = Lerp(4, RAD + 2, k);
                Put(r.Range(-0.8, 0.8) * w, y, r.Chance(0.3) ? WHITE : C[5], Math.Min(1, (1 - k) * 2));
            });

            // the model: turning once a loop, tilted toward us
            double spin = TAU * ph, tilt = 0.32;
            double[][] P3 = VERTS.Select(v =>
            {
                double x = v[0], y = v[1], z = v[2];
                double x1 = x * Px.Cos(spin) + z * Px.Sin(spin), z1 = -x * Px.Sin(spin) + z * Px.Cos(spin);
                double y2 = y * Px.Cos(tilt) - z1 * Px.Sin(tilt), z2 = y * Px.Sin(tilt) + z1 * Px.Cos(tilt);
                return new[] { cx + x1 * RAD, cy - y2 * RAD, z2 };
            }).ToArray();
            // a scan bar climbing through it, twice a loop
            double scanY = Lerp(cy + RAD * 1.5, cy - RAD * 1.5, ((ph * 2) % 1) / 0.8);
            // the faces that face us, filled with scanlines
            foreach (int[] f in FACES)
            {
                double[] a = P3[f[0]], b = P3[f[1]], c = P3[f[2]];
                double nz = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
                if (nz >= 0) continue;
                double shade = Clamp(0.3 + 0.7 * ((a[2] + b[2] + c[2]) / 3 + 0.5));
                p.Poly(new[] { a, b, c }, (x, y) => Math.Abs(y - scanY) < 2 ? C[6] : shade > 0.6 ? C[4] : C[3], (x, y) =>
                {
                    double yy = Math.Floor(y);
                    return (yy % 3 == 0 && !(flick != 0 && (ToInt32(yy) & 1) != 0)) || Math.Abs(y - scanY) < 1.5 ? 1 : 0;
                });
            }
            // the edges: the far ones dim, the near ones bright, hot where the scan bar crosses them
            var edges = EDGES.Select(ij => (a: P3[ij[0]], b: P3[ij[1]], z: (P3[ij[0]][2] + P3[ij[1]][2]) / 2)).OrderBy(e => e.z).ToArray();
            foreach (var (a, b, z) in edges)
            {
                double L = Hypot(b[0] - a[0], b[1] - a[1]);
                for (double d = 0; d <= L; d += 0.5)
                {
                    double x = Lerp(a[0], b[0], d / L), y = Lerp(a[1], b[1], d / L);
                    bool hot = Math.Abs(y - scanY) < 1.5;
                    if (z < -0.15) Put(x, y, hot ? C[5] : C[3], 0.7);
                    else Put(x, y, hot ? WHITE : z > 0.4 ? C[6] : C[5]);
                }
            }
            foreach (double[] v in P3)
            {
                if (v[2] < -0.15) continue;
                Put(v[0], v[1], WHITE);
                p.Glow(v[0], v[1], 4, C[5], 0.3);
            }
            // a dashed data ring round its waist, its far half behind
            for (int i = 0; i < 64; i++)
            {
                double a = (i / 64.0) * TAU + ph * TAU * 2, x = cx + Px.Cos(a) * (RAD + 9), y = cy + 3 + Px.Sin(a) * 4;
                if ((i / 3) % 3 == 2) continue;
                Put(x, y, Px.Sin(a) > 0 ? (i % 9 == 0 ? WHITE : C[5]) : C[3], Px.Sin(a) > 0 ? 1 : 0.6);
            }
            // light: the model glows, the scan bar glows, a flicker drops the light too
            double lk = flick != 0 ? 0.5 : 1;
            p.Glow(cx, cy, RAD * 2.2, C[4], 0.36 * lk);
            p.Glow(cx, cy - RAD * 0.6, RAD, C[5], 0.15 * lk);
            if (Math.Abs(scanY - cy) < RAD * 1.4) p.GlowLine(cx - RAD, scanY, cx + RAD, scanY, 5, C[5], 0.3 * lk);
            p.Glow(0, -2, 22, C[4], 0.2);
        }
    }
}
