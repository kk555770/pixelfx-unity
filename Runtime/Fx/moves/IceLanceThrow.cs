namespace Pixelfx
{
    public static class IceLanceThrow
    {
        const double THROW = 0.8, FLY = 0.34, HIT = THROW + FLY, TX = 170, SPEED = (TX - 34) / FLY;

        static readonly Pose HOLD = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = 2, ["fx"] = 18, ["fy"] = -22, ["bx"] = 11, ["by"] = -17, ["ffx"] = 10, ["fbx"] = -8 };
        static readonly Pose THROWN = new Pose(Poses.READY) { ["x"] = 9, ["crouch"] = 4, ["lean"] = 6, ["fx"] = 19, ["fy"] = -24, ["bx"] = -6, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.4, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(0.06, new Pose(HOLD)),
                new PoseKey(THROW - 0.2, HOLD),
                new PoseKey(THROW - 0.03, new Pose(HOLD) { ["crouch"] = 5, ["lean"] = -2, ["fx"] = 17, ["bx"] = 5, ["by"] = -20 }),
                new PoseKey(THROW, THROWN, "snap"),
                new PoseKey(HIT + 0.3, new Pose(THROWN) { ["crouch"] = 3, ["lean"] = 4 }),
                new PoseKey(HIT + 0.9, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(HIT + 3.2, new Pose(Poses.READY) { ["x"] = -3 }),
            },
            Hits = new[]
            {
                new HitCue { T = HIT, Stop = 0.13, Shake = 0.2, Amp = 2, Push = 2.4, Straw = 12, Skin = "ice", Shiver = 1.4, Y = -26 },
                new HitCue { T = 2.75, Push = 0.3, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = THROW, X = 13, N = 6, Dir = 1, C = "#d8f2ff" } },
        };

        public static readonly Module Module = new Module("ice-lance-throw", "pixelfx/fx/moves/ice-lance-throw", 310, 136, 86, 134, HIT + 3.2, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = SpellCharge.Module, T = 0.06, Until = THROW, Options = new Options { ["palette"] = Px.PAL["ice"] } },
                new Chain.Cue { Fx = IceLance.Module, T = THROW, Until = HIT, X = t => 34 + (t - THROW) * SPEED, Y = t => -24 },
                new Chain.Cue { Fx = SnowflakeBurst.Module, T = HIT, X = t => TX, Y = t => -26 }),
            act: Act, actor: 2)
        {
            Title = "Ice lance throw",
            Desc = "Frost gathers into a spear in his hand, he throws it like a javelin, and the target ices over in a burst of snowflakes and shivers.",
            Tags = new[] { "move", "ice", "spell", "projectile", "one-shot" },
            Bg = "ground",
        };
    }
}
