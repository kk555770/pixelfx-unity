namespace Pixelfx
{
    public static class FireballThrow
    {
        const double THROW = 0.95, FLY = 0.46, HIT = THROW + FLY, SPEED = 340, TX = 190;

        static readonly Pose HOLD = new Pose(Poses.READY) { ["x"] = -3, ["crouch"] = 4, ["lean"] = 2, ["fx"] = 18, ["fy"] = -22, ["bx"] = 11, ["by"] = -17, ["ffx"] = 10, ["fbx"] = -8 };
        static readonly Pose THROWN = new Pose(Poses.READY) { ["x"] = 9, ["crouch"] = 4, ["lean"] = 6, ["fx"] = 19, ["fy"] = -22, ["bx"] = -6, ["by"] = -24, ["ffx"] = 13, ["fbx"] = -10, ["toeB"] = true };
        static readonly Pose FLINCH = new Pose { ["turn"] = 0.9, ["x"] = 7, ["crouch"] = 5, ["lean"] = -3, ["fx"] = -4, ["fy"] = -36, ["bx"] = 5, ["by"] = -33, ["ffx"] = 4, ["fbx"] = -4 };

        public static readonly Act Act = new Act
        {
            Targets = new double[] { TX },
            Poses = new[]
            {
                new PoseKey(-0.42, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(0.08, new Pose(HOLD)),
                new PoseKey(0.5, HOLD),
                new PoseKey(THROW - 0.03, new Pose(HOLD) { ["crouch"] = 5, ["lean"] = -2, ["fx"] = 17, ["bx"] = 5, ["by"] = -20 }),
                new PoseKey(THROW, THROWN, "snap"),
                new PoseKey(HIT - 0.02, new Pose(THROWN) { ["crouch"] = 3, ["lean"] = 4 }),
                new PoseKey(HIT, FLINCH, "snap"),
                new PoseKey(HIT + 0.7, new Pose(FLINCH) { ["crouch"] = 3, ["lean"] = -2 }),
                new PoseKey(HIT + 1.4, new Pose(Poses.READY) { ["x"] = 4 }),
                new PoseKey(HIT + 2.1, new Pose(Poses.READY) { ["x"] = -3 }),
                new PoseKey(HIT + 5.4, new Pose(Poses.READY) { ["x"] = -3 }),
            },
            Hits = new[]
            {
                new HitCue { T = HIT, Stop = 0.18, Shake = 0.38, Amp = 3, Push = 3.4, Straw = 34, Skin = "char" },
                new HitCue { T = HIT + 4.8, Push = 0, Straw = 0, Flash = 0, Skin = null },
            },
            Dust = new[] { new DustCue { T = THROW, X = 13, N = 7, Dir = 1 } },
        };

        public static readonly Module Module = new Module("fireball-throw", "pixelfx/fx/moves/fireball-throw", 356, 244, 64, 198, HIT + 5.4, false, 30,
            new Options { ["seed"] = 1 },
            Chain.Of(
                new Chain.Cue { Fx = SpellCharge.Module, T = 0.08, Until = THROW, Options = new Options { ["palette"] = Px.PAL["fire"] } },
                new Chain.Cue { Fx = Fireball.Module, T = THROW, Until = HIT, X = t => 28 + (t - THROW) * SPEED, Y = t => -22 },
                new Chain.Cue { Fx = SmallExplosion.Module, T = HIT, X = t => TX }),
            act: Act, actor: 2)
        {
            Title = "Fireball throw",
            Desc = "He gathers fire in his hand, leans back and hurls it: the ball flies, the target takes it in a blast and comes out charred.",
            Tags = new[] { "move", "fire", "spell", "projectile", "explosion", "one-shot" },
            Bg = "ground",
        };
    }
}
