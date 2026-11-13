using System;

namespace Pixelfx
{
    public static class RocketLauncher
    {
        const double FIRE = 0.6, FLY = 0.56, HIT = FIRE + FLY, TX = 200, MY = -21, MX = 34;

        static readonly Pose REST_ = new Pose(Poses.READY) { ["weapon"] = "launcher", ["two"] = true, ["sa"] = -1, ["fx"] = 9, ["fy"] = -19 };
        static readonly Pose KNEEL = new Pose(Poses.READY) { ["weapon"] = "launcher", ["two"] = true, ["sa"] = 0, ["fx"] = 9, ["fy"] = -17, ["crouch"] = 6, ["lean"] = 2, ["ffx"] = 12, ["fbx"] = -9 };
        static readonly Pose KICKED = new Pose(KNEEL) { ["x"] = -6, ["sa"] = -0.12, ["fx"] = 4, ["fy"] = -19, ["lean"] = -4, ["crouch"] = 5 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.52, REST_),
                new PoseKey(-0.18, KNEEL),
                new PoseKey(FIRE - 0.001, new Pose(KNEEL) { ["crouch"] = 7 }),
                new PoseKey(FIRE, KICKED, "snap"),
                new PoseKey(FIRE + 0.36, new Pose(KNEEL) { ["x"] = -6 }),
                new PoseKey(HIT + 0.9, new Pose(KNEEL) { ["x"] = -6, ["crouch"] = 5, ["lean"] = 1 }),
                new PoseKey(HIT + 1.5, new Pose(REST_) { ["x"] = -3 }),
                new PoseKey(HIT + 2.2, new Pose(REST_) { ["x"] = 0 }),
                new PoseKey(HIT + 5.4, REST_),
            },
            Hits = new[]
            {
                new HitCue { T = FIRE, Stop = 0.06, Shake = 0.22, Amp = 2, Target = false },
                new HitCue { T = HIT, Stop = 0.2, Shake = 0.45, Amp = 4, Push = 3.8, Straw = 44, Skin = "char" },
                new HitCue { T = HIT + 4.8, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = FIRE, X = -16, N = 10, Dir = -1 } },
        };

        public static readonly Module Module = new Module("rocket-launcher", "pixelfx/fx/moves/rocket-launcher", 480, 244, 178, 198, HIT + 5.4, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = MuzzleShotgun.Module, T = FIRE, X = t => MX, Options = new Options { ["height"] = -MY } },
                new Chain.Cue { Fx = Rocket.Module, T = FIRE, Until = HIT, X = t => MX + 10 + Px.Pow(t - FIRE, 1.5) * ((TX - MX - 16) / Px.Pow(FLY, 1.5)), Y = t => MY },
                new Chain.Cue { Fx = SmallExplosion.Module, T = HIT, X = t => TX }),
            act: Act, actor: 2)
        {
            Title = "Rocket launcher",
            Desc = "He drops to one knee, shoulders the tube and fires: the kick shoves him back along the floor, the rocket crawls out on its jet and the target goes up.",
            Tags = new[] { "move", "gun", "rocket", "projectile", "explosion", "one-shot" },
            Bg = "ground",
        };
    }
}
