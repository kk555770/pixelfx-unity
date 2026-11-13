namespace Pixelfx
{
    public static class MeteorCall
    {
        const double BOLT = 1.52, HIT = 2.05, TX = 170;

        static readonly Pose CHANT = new Pose { ["turn"] = 1.1, ["crouch"] = 1, ["fx"] = -1, ["fy"] = -26, ["bx"] = 2, ["by"] = -26, ["armsFront"] = true, ["hy"] = 1, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose UP = new Pose { ["turn"] = 1.15, ["crouch"] = -1, ["fx"] = -7, ["fy"] = -44, ["bx"] = 8, ["by"] = -44, ["ffx"] = -3, ["fbx"] = 4 };
        static readonly Pose POINT = new Pose(Poses.READY) { ["crouch"] = 3, ["lean"] = 5, ["fx"] = 17, ["fy"] = -37, ["bx"] = -6, ["by"] = -24, ["ffx"] = 12, ["fbx"] = -9, ["toeB"] = true };
        static readonly Pose FLINCH = new Pose { ["turn"] = 0.9, ["x"] = -3, ["crouch"] = 6, ["lean"] = -4, ["fx"] = -4, ["fy"] = -36, ["bx"] = 5, ["by"] = -33, ["ffx"] = 4, ["fbx"] = -4 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.34, Poses.READY),
                new PoseKey(0, CHANT),
                new PoseKey(0.75, new Pose(CHANT) { ["crouch"] = 2 }),
                new PoseKey(1.4, UP),
                new PoseKey(BOLT - 0.02, new Pose(UP) { ["y"] = -1 }),
                new PoseKey(BOLT, POINT, "snap"),
                new PoseKey(HIT - 0.02, new Pose(POINT) { ["crouch"] = 3 }),
                new PoseKey(HIT, FLINCH, "snap"),
                new PoseKey(HIT + 0.9, new Pose(FLINCH) { ["crouch"] = 3, ["lean"] = -2 }),
                new PoseKey(HIT + 1.7, new Pose(Poses.READY) { ["x"] = 0 }),
                new PoseKey(HIT + 4.3, Poses.READY),
            },
            Hits = new[]
            {
                new HitCue { T = BOLT, Stop = 0.06, Shake = 0.16, Amp = 2, Target = false },
                new HitCue { T = HIT, Stop = 0.22, Shake = 0.55, Amp = 4, Push = 3.8, Straw = 44, Skin = "char" },
                new HitCue { T = HIT + 3.9, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
        };

        public static readonly Module Module = new Module("meteor-call", "pixelfx/fx/moves/meteor-call", 380, 240, 92, 212, HIT - 0.62 + 5, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = MagicCircleCast.Module, T = 0 },
                new Chain.Cue { Fx = Meteor.Module, T = HIT - 0.62, X = t => TX }),
            act: Act, actor: 2)
        {
            Title = "Meteor call",
            Desc = "A long chant: a circle draws itself round him, it flares, he points, and a burning rock comes down on the target and flattens the scene.",
            Tags = new[] { "move", "fire", "spell", "meteor", "ultimate", "one-shot" },
            Bg = "ground",
        };
    }
}
