namespace Pixelfx
{
    public static class EarthStomp
    {
        const double STOMP = 0.06, RAM = STOMP + 0.43, TX = 112;

        static readonly Pose KNEE = new Pose(Poses.READY) { ["crouch"] = 1, ["lean"] = -3, ["fx"] = 3, ["fy"] = -34, ["bx"] = 11, ["by"] = -30, ["ffx"] = 9, ["ffy"] = -15, ["fbx"] = -5 };
        static readonly Pose SLAM = new Pose(Poses.READY) { ["crouch"] = 7, ["lean"] = 3, ["fx"] = 14, ["fy"] = -14, ["bx"] = -5, ["by"] = -18, ["ffx"] = 11, ["fbx"] = -9 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.5, Poses.READY),
                new PoseKey(-0.14, KNEE),
                new PoseKey(STOMP - 0.005, new Pose(KNEE) { ["ffy"] = -17 }),
                new PoseKey(STOMP, SLAM, "snap"),
                new PoseKey(RAM + 0.5, new Pose(SLAM) { ["crouch"] = 5 }),
                new PoseKey(RAM + 1.1, Poses.READY),
                new PoseKey(STOMP + 3.4, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = STOMP, Stop = 0.1, Shake = 0.2, Amp = 3, Target = false },
                new HitCue { T = RAM, Stop = 0.14, Shake = 0.28, Amp = 3, Push = -3.2, Straw = 30, Y = -10 },
            },
            Dust = new[] { new DustCue { T = STOMP, X = 11, N = 9 } },
        };

        public static readonly Module Module = new Module("earth-stomp", "pixelfx/fx/moves/earth-stomp", 290, 142, 50, 128, STOMP + 3.4, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(new Chain.Cue { Fx = EarthSpike.Module, T = STOMP, X = t => TX }),
            act: Act, actor: 2)
        {
            Title = "Earth stomp",
            Desc = "A knee comes up, the boot slams down, the floor heaves under the target and a stone spike rams up through it.",
            Tags = new[] { "move", "earth", "spell", "stomp", "one-shot" },
            Bg = "ground",
        };
    }
}
