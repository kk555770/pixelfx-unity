// Mapping a lattice onto a dome by longitude and latitude, the ripple measured over its surface, the rim light
// and the glancing sparks are adapted from Sprite Fusion's open-source shield
// (https://www.spritefusion.com/pixel-art-vfx/shield), MIT License, Copyright (c) 2026 Hugo Duprez.
using System;
using static Pixelfx.Px;

namespace Pixelfx
{
    public sealed class ShieldBlock : Effect
    {
        static readonly Pose BRACE = new Pose(Poses.READY) { ["crouch"] = 4, ["lean"] = 2, ["fx"] = 13, ["fy"] = -28, ["bx"] = 6, ["by"] = -21, ["ffx"] = 9, ["fbx"] = -9 };

        public static readonly Act Act = new Act
        {
            Poses = new[]
            {
                new PoseKey(-0.36, Poses.READY),
                new PoseKey(-0.1, BRACE),
                new PoseKey(0, new Pose(BRACE) { ["x"] = -3, ["crouch"] = 6, ["lean"] = -2, ["fx"] = 10, ["fy"] = -26 }, "snap"),
                new PoseKey(0.18, new Pose(BRACE) { ["x"] = -4, ["crouch"] = 5, ["lean"] = 0 }),
                new PoseKey(0.65, new Pose(BRACE) { ["x"] = -3 }),
                new PoseKey(1.1, new Pose(Poses.READY) { ["x"] = 0 }),
            },
            Hits = new[] { new HitCue { T = 0.005, Stop = 0.09, Shake = 0.2, Amp = 2 } },
            Dust = new[] { new DustCue { T = 0.01, X = -10, N = 6, Dir = -1 } },
        };

        public static readonly Module Module = new Module("shield-block", "pixelfx/fx/impacts/shield-block", 148, 104, 40, 88, 1.1, false, 30,
            new Options
            {
                ["palette"] = new[] { "#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["seed"] = 12,
            },
            (o, e) => new ShieldBlock(o, e), act: Act, actor: 2)
        {
            Title = "Shield block",
            Desc = "A blow is caught on a half-dome of energy in front of the character: its diamond lattice flashes, a ripple runs over it, sparks glance off, then it blinks out cell by cell.",
            Tags = new[] { "impact", "block", "shield", "barrier", "energy", "ripple", "actor", "one-shot" },
            Bg = "ground",
        };

        const double CX = 3, RX = 28, RY = 44, GRID = 3.4; // the dome's flat face, its depth and height, lattice density
        const double HIT_LON = 1.18, HIT_LAT = 0.5, GONE = 0.5; // where the blow lands; when the dome starts to blink out

        static readonly (double t0, double k0)[] Ripples = { (0, 1), (0.12, 0.55) };

        // a point on the dome's surface (lon round from facing us, lat up from the floor) to local space
        static (double x, double y) OnDome(double lon, double lat) => (CX + RX * Px.Cos(lat) * Px.Sin(lon), -RY * Px.Sin(lat));

        readonly uint[] C, S;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly double hx, hy;
        readonly ImpactsLib.Spike[] sp;
        double t;

        ShieldBlock(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 8);
            S = Fit(o.Ramp("sparks"), 4);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            ps = new Particles(R, ground: floor, sparks: S, emitter: e);
            lights = new Lights(e);
            (hx, hy) = OnDome(HIT_LON, HIT_LAT);
            sp = ImpactsLib.Spikes(R, 8, 0.2, len0: 5, len1: 8, w0: 1.4, w1: 1.9, fwd: 1.2, back: 0.3);

            lights.Add(hx, hy, 40, S[1], 0.7, 0.2);
            lights.Add(hx, hy, 12, WHITE, 0.7, 0.06);
            // sparks glance off the dome: out and along its surface, mostly upward
            double nA = Atan2(-Px.Sin(HIT_LAT) / RY, Px.Cos(HIT_LAT) * Px.Sin(HIT_LON) / RX); // the outward normal, roughly
            for (int i = 0; i < 14; i++)
            {
                double a = nA + R.Range(-1.2, 0.9), v = R.Range(130, 300);
                ps.Spawn(P.Spark, hx + 1, hy, Px.Cos(a) * v, Px.Sin(a) * v - 30, R.Range(0.15, 0.42), WHITE, 1, i > 10 ? R.Range(0.01, 0.04) : 0);
            }
        }

        // the dome, front (back = false) or the far side seen through it (back = true): a glassy fill, a diamond
        // lattice, a bright rim where it turns away, a sheen sweeping up it
        void Dome(Painter p, bool back)
        {
            double fade = 1 - Smooth(GONE + 0.35, GONE + 0.55, t);
            if (fade <= 0) return;
            for (int y = -(int)RY - 1; y < 0; y++)
                for (int x = (int)CX; x <= CX + RX + 1; x++)
                {
                    double u = (x + 0.5 - CX) / RX, v = -(y + 0.5) / RY, q = u * u + v * v;
                    if (q > 1 || u < 0 || v < 0) continue;
                    double z = Math.Sqrt(1 - q) * (back ? -1 : 1), lon = Atan2(u, z), lat = Px.Asin(Clamp(v));
                    // the lattice, and which cell this is: each cell keeps its own clock for blinking out
                    double a = (lon + lat) * GRID, b = (lon - lat) * GRID, fa = a - Math.Floor(a), fb = b - Math.Floor(b);
                    double cell = Rand(Math.Floor(a), Math.Floor(b), 31);
                    bool line = Math.Min(Math.Min(fa, 1 - fa), Math.Min(fb, 1 - fb)) < 0.13;
                    double gone = GONE + cell * 0.35;
                    if (t > gone) continue;
                    // how far over the surface from the blow, and the two ripples running out from it
                    double d = Px.Acos(Clamp(Px.Cos(lat) * Px.Cos(HIT_LAT) * Px.Cos(lon - HIT_LON) + Px.Sin(lat) * Px.Sin(HIT_LAT), -1, 1));
                    double hit = 0;
                    foreach (var (t0, k0) in Ripples)
                    {
                        double k = t - t0, front = k * 3.6;
                        if (k < 0 || k > 0.55) continue;
                        if (Math.Abs(d - front) < 0.2) hit = Math.Max(hit, k0 * (1 - k / 0.55));
                        else if (d < front) hit = Math.Max(hit, 0.3 * k0 * (1 - k / 0.55));
                    }
                    double rim = 1 - Math.Abs(z);
                    bool flash = t < 0.05, blink = t > gone - 0.06, sheen = Math.Abs(((lat * 0.8 - lon * 0.3 - t * 1.6) % 1 + 1) % 1 - 0.5) < 0.05;
                    if (back)
                    {
                        if (line) p.Dot(x, y, C[3], 0.5 * fade);
                        continue;
                    }
                    if (q > 0.9) p.Dot(x, y, flash || hit > 0.5 ? WHITE : C[6], fade);
                    else if (line) p.Dot(x, y, flash || blink || hit > 0.5 ? WHITE : hit > 0.2 || sheen ? C[6] : rim > 0.7 ? C[5] : rim > 0.35 ? C[4] : C[3], fade);
                    else p.Blend(x, y, flash || hit > 0.35 ? C[6] : C[3], fade * Math.Min(0.85, (flash ? 0.35 : 0.1 + 0.25 * rim * rim) + hit * 0.65));
                    if (((x * 7 + y * 3) & 15) == 0) p.Light(x, y, C[4], 0.15 + 0.5 * hit);
                }
        }

        public override void Update(double dt)
        {
            t += dt;
            // as cells blink out, little motes of light lift off
            if (t > GONE && t < GONE + 0.38 && R.Chance(0.5))
            {
                double lon = R.Range(0, Math.PI / 2), lat = R.Range(0.05, 1.4);
                var (x, y) = OnDome(lon, lat);
                ps.Spawn(P.Star, x, y, R.Range(-10, 10), -R.Range(10, 40), R.Range(0.2, 0.36), R.Chance(0.5) ? C[6] : C[5], R.Range(0.6, 1.4));
            }
            ps.Update(dt);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            Dome(p, true);
        }

        public override void Draw(Painter p)
        {
            Dome(p, false);
            // where the dome meets the floor
            double fade = 1 - Smooth(GONE + 0.3, GONE + 0.55, t);
            if (fade > 0)
                for (int x = (int)CX; x <= CX + RX; x++)
                    p.Dot(x, 0, t < 0.05 ? WHITE : C[5], fade * (0.6 + 0.4 * ((x - CX) / RX)));
            // the blow: a hot star where it lands, then the sparks glancing off
            if (t < 0.03) ImpactsLib.Star(p, hx, hy, sp, 1, 1, new[] { S[3], S[0], S[1], WHITE }, 1, 2);
            else if (t < 0.06) ImpactsLib.Star(p, hx, hy, sp, 1.25, 0.55, new[] { S[3], S[1], S[2], S[0] }, 1, 2);
            ps.Draw(p);
            p.Glow(CX + RX * 0.5, -RY * 0.45, RY * 0.9, C[4], 0.2 * (1 - Smooth(GONE, GONE + 0.55, t)) + 0.35 * Exp(-t / 0.08));
            lights.Draw(p);
        }
    }
}
