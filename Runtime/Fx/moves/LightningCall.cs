namespace Pixelfx
{
    public static class LightningCall
    {
        const double BOLT = 0.76, TX = 130;

        static readonly Pose UP = new Pose(Poses.READY) { ["crouch"] = 1, ["lean"] = -2, ["fx"] = 2, ["fy"] = -44, ["bx"] = 8, ["by"] = -19, ["ffx"] = 7, ["fbx"] = -5 };
        static readonly Pose DOWN = new Pose(Poses.READY) { ["crouch"] = 5, ["lean"] = 5, ["fx"] = 18, ["fy"] = -27, ["bx"] = -7, ["by"] = -24, ["ffx"] = 12, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(0, Poses.READY),
                new PoseKey(0.26, UP),
                new PoseKey(BOLT - 0.04, new Pose(UP) { ["y"] = -1, ["crouch"] = -1 }),
                new PoseKey(BOLT, DOWN, "snap"),
                new PoseKey(1.4, new Pose(DOWN) { ["crouch"] = 3, ["lean"] = 4 }),
                new PoseKey(2, Poses.READY),
                new PoseKey(3, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = BOLT, Stop = 0.14, Shake = 0.32, Amp = 3, Push = 2.8, Straw = 24, Skin = "shock", Shiver = 1.2 },
                new HitCue { T = 1.04, Stop = 0.03, Push = 1.2, Straw = 8 },
                new HitCue { T = 1.26, Stop = 0.03, Push = -1, Straw = 6 },
                new HitCue { T = 2.5, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = BOLT, X = 14, N = 6, Dir = 1 } },
        };

        public static readonly Module Module = new Module("lightning-call", "pixelfx/fx/moves/lightning-call", 290, 266, 42, 231, 3, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(new Chain.Cue { Fx = LightningStrike.Module, T = 0, X = t => TX }),
            act: Act, actor: 2)
        {
            Title = "Lightning call",
            Desc = "An arm goes up, a sigil lights under the target, and on the downswing a thick bolt comes down on it; the target jolts through the aftershocks.",
            Tags = new[] { "move", "lightning", "spell", "strike", "one-shot" },
            Bg = "ground",
        };
    }
}
