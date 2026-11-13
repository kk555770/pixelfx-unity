using System.Linq;

namespace Pixelfx
{
    public static class PistolShots
    {
        static readonly double[] SHOTS = { 0.14, 0.42, 0.7 };
        const double FLY = 0.12, TX = 180, BY = -32, MX = 28;

        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "pistol", ["sa"] = 0.9, ["fx"] = 10, ["fy"] = -17, ["bx"] = 7, ["by"] = -16 };
        static readonly Pose AIM = new Pose(REST_) { ["crouch"] = 3, ["lean"] = 2, ["fx"] = 18, ["fy"] = -28, ["sa"] = 0, ["bx"] = 12, ["by"] = -25, ["ffx"] = 11, ["fbx"] = -9 };
        static readonly Pose KICK = new Pose(AIM) { ["lean"] = 0, ["fx"] = 16, ["fy"] = -33, ["sa"] = -0.45, ["by"] = -28 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[] { new PoseKey(-0.36, REST_), new PoseKey(-0.06, AIM) }
                .Concat(SHOTS.SelectMany(t => new[] { new PoseKey(t, AIM), new PoseKey(t + 0.001, KICK, "snap"), new PoseKey(t + 0.14, AIM) }))
                .Concat(new[] { new PoseKey(1.2, new Pose(AIM) { ["crouch"] = 1 }), new PoseKey(1.7, REST_), new PoseKey(2.6, REST_) })
                .ToArray(),
            Hits = SHOTS.Select((t, i) => new HitCue
            {
                T = t + FLY,
                Stop = i == 2 ? 0.08 : 0.04,
                Shake = 0.1,
                Amp = 1 + (i == 2 ? 1 : 0),
                Push = i % 2 != 0 ? -1.8 : 2,
                Straw = 10,
                Y = -30,
            }).ToArray(),
        };

        public static readonly Module Module = new Module("pistol-shots", "pixelfx/fx/moves/pistol-shots", 378, 76, 111, 72, 2.6, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(SHOTS.SelectMany(t => new[]
            {
                new Chain.Cue { Fx = MuzzlePistol.Module, T = t, X = u => MX, Options = new Options { ["height"] = -BY } },
                new Chain.Cue { Fx = BulletTracer.Module, T = t, Until = t + FLY, X = u => MX + 4 + (u - t) * ((TX - MX - 14) / FLY), Y = u => BY },
                new Chain.Cue { Fx = HitSpark.Module, T = t + FLY, X = u => TX - 10, Options = new Options { ["height"] = 28 } },
            }).ToArray()),
            act: Act, actor: 2)
        {
            Title = "Pistol shots",
            Desc = "Three aimed shots: the gun kicks up each time and settles, tracers cross the floor, and the target staggers under each hit.",
            Tags = new[] { "move", "gun", "pistol", "projectile", "ranged", "one-shot" },
            Bg = "ground",
        };
    }
}
