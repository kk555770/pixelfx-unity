namespace Pixelfx
{
    public static class HolySmite
    {
        const double BOLT = 0.47, TX = 118;

        static readonly Pose UP = new Pose { ["turn"] = 1.15, ["crouch"] = -1, ["fx"] = -7, ["fy"] = -44, ["bx"] = 8, ["by"] = -44, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose DOWN = new Pose(Poses.READY) { ["crouch"] = 5, ["lean"] = 5, ["fx"] = 17, ["fy"] = -29, ["bx"] = 13, ["by"] = -25, ["ffx"] = 12, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.42, Poses.READY),
                new PoseKey(-0.04, UP),
                new PoseKey(BOLT - 0.01, new Pose(UP) { ["y"] = -1 }),
                new PoseKey(BOLT, DOWN, "snap"),
                new PoseKey(1.1, new Pose(DOWN) { ["crouch"] = 3, ["lean"] = 3 }),
                new PoseKey(1.7, Poses.READY),
                new PoseKey(2.8, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = BOLT, Stop = 0.14, Shake = 0.32, Amp = 3, Push = 2.8, Straw = 24, Skin = "holy", Y = -30 },
                new HitCue { T = 2.3, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = BOLT, X = 14, N = 6, Dir = 1 } },
        };

        public static readonly Module Module = new Module("holy-smite", "pixelfx/fx/moves/holy-smite", 252, 182, 42, 151, 2.8, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(new Chain.Cue { Fx = Smite.Module, T = 0, X = t => TX }),
            act: Act, actor: 2)
        {
            Title = "Holy smite",
            Desc = "Both hands go up and a glint marks the target; he brings them down and a bolt of light drops like a hammer, leaving the target blanched.",
            Tags = new[] { "move", "holy", "spell", "strike", "one-shot" },
            Bg = "ground",
        };
    }
}
