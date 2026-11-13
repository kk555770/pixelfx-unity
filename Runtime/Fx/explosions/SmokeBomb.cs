using System;
using System.Collections.Generic;
using System.Linq;
using static Pixelfx.Px;
using static Pixelfx.ExplosionsLib;

namespace Pixelfx
{
    public sealed class SmokeBomb : Effect
    {
        const double Duration = 5;
        const int W = 260, H = 176, OX = 130, OY = 156;

        public static readonly Module Module = new Module("smoke-bomb", "pixelfx/fx/explosions/smoke-bomb", W, H, OX, OY, Duration, false, 30,
            new Options
            {
                ["palette"] = new[] { "#0e0b14", "#1e1828", "#30283e", "#463c58", "#62567a", "#867a9e", "#aea4c4" },
                ["canister"] = new[] { "#16121e", "#2a2434", "#443c52", "#686078", "#9c94ac" },
                ["sparks"] = new[] { "#fff3a6", "#ffe26b", "#ff8a2a", "#f2561d" },
                ["seed"] = 3,
            },
            (o, e) => new SmokeBomb(o, e))
        {
            Title = "Smoke bomb",
            Desc = "A round canister clinks down, fuse sparking, and pops: thick grey-violet smoke gushes out, rolls along the floor, billows into a cloud that hides all, then thins to wisps.",
            Tags = new[] { "smoke", "smoke-bomb", "ninja", "vanish", "escape", "ground", "one-shot" },
            Bg = "ground",
        };

        const double POP = 0.95, R0 = 4;
        const double X0 = -OX + 3, X1 = W - OX - 3, Y0 = -OY + 3, Y1 = H - OY - 3;
        static readonly uint[] BAND = { Hex("#3a1418"), Hex("#7a2a24"), Hex("#b84a34") };
        static readonly uint[] GRIT = { Hex("#5a4f6b"), Hex("#3e3650"), Hex("#776c88") };
        static readonly uint FUSE = Hex("#8a6a48");
        static readonly int[] KINDS = { 0, 0, 1, 1, 2, 3 };

        sealed class Can
        {
            public double x, y, vx, vy, va, a;
            public bool rest;

            public Can Copy() => (Can)MemberwiseClone();
        }

        sealed class Puff
        {
            public int kind;
            public double t0, vx, vh, d, r0, r1, f, wob, x, h, age;
            public bool on;
        }

        // the canister's flight: it arcs down, bounces with a clink, rolls and stops. hit(x, speed) on each bounce
        static void Step(Can c, double dt, Action<double, double> hit)
        {
            if (c.rest) return;
            c.vy += 900 * dt;
            c.x += c.vx * dt;
            c.y += c.vy * dt;
            if (c.y >= -R0 && c.vy > 0)
            {
                c.y = -R0;
                if (c.vy > 80)
                {
                    hit?.Invoke(c.x, c.vy);
                    c.vy = -c.vy * 0.38;
                    c.vx *= 0.78;
                }
                else c.vy = 0;
            }
            if (c.vy == 0 && c.y >= -R0 - 0.01)
            {
                c.vx -= c.vx * 3 * dt;
                if (Math.Abs(c.vx) < 2)
                {
                    c.vx = 0;
                    c.rest = true;
                }
            }
            c.a += (c.vy == 0 ? c.vx / R0 : c.va) * dt;
        }

        readonly uint[] C, K, SM;
        readonly uint spark1, spark2;
        readonly Rng R;
        readonly Particles ps;
        readonly Lights lights;
        readonly Can can;
        readonly Puff[] lobes;
        double t;
        bool popped;

        SmokeBomb(Options o, Emitter e)
        {
            C = Fit(o.Ramp("palette"), 7);
            K = Fit(o.Ramp("canister"), 5);
            R = new Rng(o.Seed);
            Func<double, double> floor = x => 0;
            SM = new[] { C[5], C[4], C[3], C[2], C[1] };
            string[] sparks = o.Ramp("sparks");
            spark1 = Hex(sparks[1]);
            spark2 = Hex(sparks[2]);
            ps = new Particles(rng: R, ground: floor, sparks: Ramp(sparks), emitter: e);
            lights = new Lights(e);
            // thrown in from the left so that it comes to rest on the anchor: run its path once to see where it stops
            var start = new Can { x = -70, y = -60, vx = 125, vy = -80, va = 7, a = 0, rest = false };
            Can sim = start.Copy();
            for (int i = 0; i < 2400 && !sim.rest; i++) Step(sim, 1.0 / 240, null);
            can = start.Copy();
            can.x = start.x - sim.x;
            // the smoke, gushing out front-loaded: rollers that slide out low along the floor, a fat core that stays over the
            // canister, a plume driven up, and a low trickle that keeps the base filled; they rise as they billow
            const int N = 52;
            lobes = new Puff[N];
            for (int i = 0; i < N; i++)
            {
                int kind = KINDS[i % 6], side = R.Sign();
                double ang = kind == 0 || kind == 3 ? (side > 0 ? -R.Range(0, 0.25) : Math.PI + R.Range(0, 0.25)) : -Math.PI / 2 + R.Range(-0.6, 0.6);
                double sp = new[] { R.Range(70, 170), R.Range(0, 25), R.Range(45, 95), R.Range(20, 60) }[kind];
                double r1 = new[] { R.Range(12, 17), R.Range(16, 22), R.Range(13, 18), R.Range(14, 19) }[kind];
                double t0 = kind == 3 ? POP + R.Range(0.45, 1.5) : POP + 0.6 * Px.Pow((double)i / (N - 1), 1.6) + R.Range(0, 0.03);
                lobes[i] = new Puff
                {
                    kind = kind, t0 = t0, vx = Px.Cos(ang) * sp, vh = -Px.Sin(ang) * sp, d = R.Range(-1, 1), r0 = R.Range(5, 8), r1 = r1, f = R.Range(-0.45, 0.4), wob = R.Range(0, TAU),
                    on = false, x = 0, h = 0, age = 0,
                };
            }
        }

        (double x, double y) FuseTip() => (can.x + Px.Sin(can.a) * (R0 + 2.5), can.y - Px.Cos(can.a) * (R0 + 2.5));

        double Thin(Puff b) => Smooth(2.5 + b.f, 3.9 + b.f * 0.6, t);

        double Radius(Puff b) => (b.r0 + (b.r1 - b.r0) * (1 - Exp(-b.age / 0.28))) * (1 + 0.05 * Px.Sin(t * 3 + b.wob)) * (1 + 0.35 * Thin(b));

        public override void Update(double dt)
        {
            t += dt;
            Step(can, dt, (x, sp) =>
            {
                // a clink: a few bright specks and a puff of dust
                for (int i = 0; i < 3; i++) ps.Spawn(P.Spark, x, -1, R.Range(-70, 70), -R.Range(40, 110), R.Range(0.06, 0.14));
                for (int i = 0; i < 3; i++) ps.Spawn(P.Dust, x + R.Range(-2, 2), -0.5, R.Sign() * R.Range(10, 40), -R.Range(4, 14), R.Range(0.3, 0.55), GRIT[2], R.Range(1.4, 2.4));
            });
            if (t < POP)
            {
                // the fuse: sparks spitting off its tip, quicker as it burns down, and a thread of smoke
                var (fx, fy) = FuseTip();
                double hurry = Smooth(POP - 0.4, POP, t);
                if (R.Chance((26 + 50 * hurry) * dt)) ps.Spawn(P.Spark, fx, fy, R.Range(-50, 50), -R.Range(30, 100), R.Range(0.08, 0.18));
                if (R.Chance(8 * dt)) ps.Spawn(P.Vapour, fx, fy - 1, R.Range(-4, 4), -R.Range(10, 18), R.Range(0.5, 0.8), C[5], R.Range(1, 1.8));
            }
            else if (!popped)
            {
                popped = true;
                // the pop: a flash, a soft light that dies fast, sparks and grit, the canister jolted
                lights.Add(can.x, -9, 64, C[6], 0.75, 0.22);
                lights.Add(can.x, -5, 22, WHITE, 0.7, 0.06);
                for (int i = 0; i < 30; i++)
                {
                    double a = R.Range(-Math.PI - 0.35, 0.35), sp = R.Range(120, 300);
                    ps.Spawn(P.Spark, can.x, -5, Px.Cos(a) * sp, Px.Sin(a) * sp - 40, R.Range(0.15, 0.4));
                }
                for (int i = 0; i < 26; i++)
                {
                    double u = R.Range(-1, 1), a = -Math.PI / 2 + u * 0.9, sp = R.Range(80, 220) * (1 - 0.35 * Math.Abs(u));
                    ps.Spawn(P.Grit, can.x + u * 4, -1, Px.Cos(a) * sp, Px.Sin(a) * sp, R.Range(0.9, 1.6), R.Pick(GRIT), R.Chance(0.35) ? 2 : 1);
                }
                can.vy = -95;
                can.vx = R.Range(-25, 25);
                can.va = R.Sign() * 9;
                can.rest = false;
            }
            // the cloud: lobes slide out low, slow, then rise as they billow; a breeze takes them late on
            double wind = 6 * Smooth(1.8, 3.2, t);
            foreach (Puff b in lobes)
            {
                if (!b.on)
                {
                    if (t < b.t0) continue;
                    b.on = true;
                    b.x = can.x + R.Range(-2, 2);
                    b.h = 1;
                }
                b.age += dt;
                b.vx -= b.vx * 2.4 * dt;
                // rollers hug the floor for a while; everything rises as it billows, gently
                double lift = b.kind == 0 ? 14 * Smooth(0.7, 1.6, b.age) : b.kind == 1 ? 3 + 9 * Smooth(0.8, 1.8, b.age) : b.kind == 2 ? 6 + 12 * Smooth(0.3, 1.4, b.age) : 10 * Smooth(0.8, 1.6, b.age);
                b.vh += lift * dt;
                b.vh -= b.vh * 1.6 * dt;
                b.x += (b.vx + wind) * dt;
                b.h = Math.Max(0, b.h + b.vh * dt);
                // thinning, it sheds wisps that drift off
                double k = Thin(b);
                if (k > 0.12 && k < 0.85 && R.Chance(1.8 * dt))
                {
                    double r = Radius(b), life = Math.Min(R.Range(1, 1.6), Duration - 0.15 - t);
                    if (life > 0.3) ps.Spawn(P.Vapour, b.x + R.Range(-r, r) * 0.5, -(b.h + r * 1.4), wind + R.Range(-6, 10), -R.Range(8, 20), life, C[4], r * R.Range(0.3, 0.45));
                }
            }
            // the spent canister keeps smoking a little
            if (t > POP + 0.5 && t < 4.1 && R.Chance(4 * dt)) ps.Spawn(P.Vapour, can.x, can.y - 3, R.Range(-3, 3), -R.Range(8, 16), R.Range(0.6, 0.9), C[4], R.Range(1.5, 2.5));
            ps.Update(dt);
            Cull(ps, X0, Y0, X1, Y1);
            FadeEdges(ps, X0, X1, 20, Y1);
            lights.Update(dt);
        }

        public override void DrawBack(Painter p)
        {
            ps.Draw(p, Layer.Back);
            Clouds(p, true);
        }

        public override void Draw(Painter p)
        {
            // the canister: live with its fuse burning, then spent; it fades as the last smoke clears
            double fade = 1 - Smooth(4.3, 4.8, t);
            if (fade > 0) DrawCanister(p, K, can, t < POP, fade);
            if (t < POP)
            {
                var (fx, fy) = FuseTip();
                p.Px(fx, fy, Px.Sin(t * 50) > 0 ? WHITE : spark1);
                p.Glow(fx, fy, 6, spark2, 0.45 + 0.2 * Px.Sin(t * 37));
            }
            Clouds(p, false);
            ps.Draw(p, Layer.Front);
            // the flash of the pop
            double u = t - POP;
            if (u >= 0 && u < 0.067)
            {
                p.Circle(can.x, -5, 6.5, C[5]);
                p.Circle(can.x, -5, 4.5, WHITE);
            }
            if (u >= 0 && u < 0.16) Sim.Flare(p, can.x, -5, 22 * (1 - u / 0.16), 1 - u / 0.16, new[] { C[3], C[4], C[5], C[6] });
            if (u >= 0 && u < 0.35) Pool(p, can.x, 1, 50, 8, C[6], 0.5 * (1 - u / 0.35));
            lights.Draw(p);
        }

        // far lobes behind, near ones in front; each a cel-shaded ball: a lit crescent on top, a mid tone, a dark underside.
        // The cloud is lit from above, so lobes low in it sit in its shadow a step darker; anything nearing the top of the
        // canvas thins out
        void Clouds(Painter p, bool back)
        {
            List<Puff> on = lobes.Where(b => b.on).ToList();
            if (on.Count == 0) return;
            double top = 0;
            foreach (Puff b in on) top = Math.Min(top, -(b.h + Radius(b) * 1.92));
            foreach (Puff b in on.Where(b => (b.d < -0.35) == back).OrderBy(b => b.d).ToList())
            {
                double r = Radius(b), y = -(b.h + r * 0.92) + b.d * 2.5, k = Thin(b);
                double a = Math.Min(1, b.age / 0.05) * Px.Pow(1 - k, 1.4) * 0.97 * (1 - Smooth(-OY + 34, -OY + 6, y - r));
                if (a <= 0.01) continue;
                int shade = (y - top) / Math.Max(20, -top) > 0.55 ? 1 : 0;
                Lobe(p, b.x, y, r, SM, SM, 0, 0, a, b.d, Math.Min(2, shade + (k > 0.6 ? 1 : 0)), 1);
            }
        }

        // a round canister turned by its roll: dark metal lit from the top left, a red band, an outline, and a fuse on top
        // while it's live (spent, its cap is blown off)
        static void DrawCanister(Painter p, uint[] K, Can c, bool live, double a)
        {
            double ca = Px.Cos(c.a), sa = Px.Sin(c.a);
            p.Shade(c.x - R0 - 2, c.y - R0 - 2, c.x + R0 + 2, c.y + R0 + 2, (px, py, o) =>
            {
                double dx = px - c.x, dy = py - c.y, d = Hypot(dx, dy);
                if (d > R0 + 1) return 0;
                o.a = a;
                if (d > R0) return OUTLINE;
                double v = -dx * sa + dy * ca, lit = -(dx * 0.6 + dy * 0.8) / R0;
                if (Math.Abs(v) < 0.9) return BAND[lit > 0.3 ? 2 : lit > -0.4 ? 1 : 0];
                if (d < R0 * 0.5 && lit > 0.45) return K[4];
                return K[lit > 0.5 ? 3 : lit > 0 ? 2 : lit > -0.5 ? 1 : 0];
            });
            double cx = c.x + sa * R0, cy = c.y - ca * R0;
            if (live) p.Line(cx, cy, cx + sa * 2.5, cy - ca * 2.5, FUSE, a);
            else p.Dot(cx, cy, K[0], a);
        }
    }
}
