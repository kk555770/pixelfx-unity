# Core map

Every export of pixelfx's `src/core` and where it lives in `Runtime/Core` (namespace `Pixelfx`). Static helpers in `Px`
are written as `using static Pixelfx.Px;` in the effects, so they read like the JavaScript.

| pixelfx (`src/core`) | C# |
|---|---|
| **math.js** | `Px.cs` |
| `TAU` `clamp` `lerp` `invLerp` `remap` `fract` `wrap` `seg` `smooth` `bump` `pingpong` `dist` `angleTo` | `Px.TAU` `Clamp` `Lerp` `InvLerp` `Remap` `Fract` `Wrap` `Seg` `Smooth` `Bump` `Pingpong` `Dist` `AngleTo` |
| `ease` | `Px.Ease` |
| `hash` `rand` | `Px.Hash` `Px.Rand` |
| `Rng` / `rng` | `Rng` / `Px.NewRng` |
| `noise` `noise2` `fbm` `tileNoise` `tileFbm` `loopNoise` | `Px.Noise` `Noise2` `Fbm` `TileNoise` `TileFbm` `LoopNoise` |
| `Math.round` `Math.sign` `x \| 0` (JS semantics) | `Px.Round` `RoundInt` `Sign` `ToInt32` |
| `Math.sin` `cos` `tan` `asin` `acos` `atan` `atan2` `exp` `log` `log2` `cbrt` `hypot` `pow` (as V8 computes them) | `Px.Sin` `Cos` `Tan` `Asin` `Acos` `Atan` `Atan2` `Exp` `Log` `Log2` `Cbrt` `Hypot` (2 or 3 arguments) `Pow` |
| **color.js** | `Px.cs` |
| `rgb` `red` `green` `blue` `hex` `toHex` `mix` `scale` `luma` `ramp` `fit` `at` `blend` | `Px.Rgb` `Red` `Green` `Blue` `Hex` `ToHex` `Mix` `Scale` `Luma` `Ramp` `Fit` `At` `Blend` |
| `PAL` `RAMP` `WHITE` `BLACK` `INK` `PATCH` | `Px.PAL` `RAMP` `WHITE` `BLACK` `INK` `PATCH` |
| **particles.js** | `Px.cs`, `SimpleParticles.cs` |
| `fly` `flyV` `burst` `cycle` `stream` | `Px.Fly` `FlyV` `Burst` `Cycle` `Stream` |
| `Particles` (shadowed in `index.js` by sim.js's `Particles`) | `SimpleParticles` |
| **shapes.js** | `Shapes.cs` |
| `bolt` `forks` `star` `ngon` `trail` | `Shapes.Bolt` `Forks` `Star` `Ngon` `Trail` |
| **sim.js** | `Sim.cs` |
| `GRAVITY` `FIRE_RAMP` `SMOKE_RAMP` | `Sim.GRAVITY` `FIRE_RAMP` `SMOKE_RAMP` |
| `P` `PF` `B` | enums `P` `PF` `B` |
| `Particles` `Blazes` `Bits` `Lights` | `Particles` `Blazes` `Bits` `Lights` (each also takes an `Emitter`, see below) |
| `sparkle` `flare` `band` `crystal` | `Sim.Sparkle` `Flare` `Band` `Crystal` |
| **painter.js** | `Painter.cs`, `PainterText.cs` |
| `Painter` | `Painter` (`mask` is `MaskFill`; `sprite`, `text`, `measure`, `save`/`restore` are in `PainterText.cs`) |
| **font.js** | `Font.cs` |
| `FONTS` `textWidth` `rasterText` | `Font.Fonts` `Font.TextWidth` `Font.RasterText` |
| **fx.js** | `Painter.cs`, `PainterText.cs`, `Fx.cs` |
| `createFrame` `clearFrame` `composite` | `new Frame(w, h)` `frame.Clear()` `frame.Composite(rgba, bg, lightK)` |
| `painterFor` | `Painter.For(frame)` |
| `resolveOptions` | `Options.Resolve` |
| `createFx` | `new Fx(module, options)`; a module is `Module`, an effect instance `Effect` |
| **actor.js** | `Actor.cs`, `Fx.cs` |
| `stockActor` `drawActor` | `Actor.Stock` `Actor.Draw` |
| `actorAlphaOf` | `Module.ActorAlphaAt` |
| `HERO` `HERO2` `HERO_PAL` `DUMMY_PAL` | `Actor.HERO` `HERO2` `HERO_PAL` `DUMMY_PAL` |
| **rig.js** | `Rig.cs` |
| `STAND` `WEAPONS` `shortfall` `posed` `skeleton` `posedParts` | `Rig.STAND` `WEAPONS` `Shortfall` `Posed` `SkeletonOf` `PosedParts` (`Rig.ClearCache` empties the pose cache) |
| **poses.js** | `Pose.cs` |
| `READY` | `Poses.READY` |
| **act.js** | `Pose.cs`, `Act.cs` |
| an effect's `act` export (poses, hits, dust, target) | `Act` with `PoseKey`, `HitCue`, `DustCue`; a pose is `Pose` |
| `lerpPose` `poseAt` `actSpan` `actTiming` `actBounds` | `Act.LerpPose` `PoseAt` `Span` `TimingOf` `Bounds` |
| `Target` `SKINS` | `Target` `Target.SKINS` |
| `createAct` | `new Performance(module, options)` |
| **chain.js** | `Chain.cs` |
| `chain` `shiftKeys` `shiftHits` | `Chain.Of` `ShiftKeys` `ShiftHits` (and `ShiftDust` for the dust lists the moves shift by hand) |
| **scene.js** | `Scene.cs` |
| `backdrop` `BACKDROPS` | `Scene.Backdrop` `Scene.BACKDROPS` |

## Additions

- `Emitter`: where an effect is in the world. Particles remember the emitter's position when they're born and stay there
  when it moves. With the emitter at (0, 0) every frame is pixelfx's.
- `Canvas` and `Look`: a module drawn into an RGBA buffer, and a module with a game's options, turn directions, margin and
  hit-stops.
- `Effects.All` / `Effects.Get(name)`: every ported module, found by reflection.

- `Painter.ClipTo(w, h)`: the painter, dropping every pixel outside its local rectangle; the `Clipped` painter that
  pixelfx's transition effects build in their `_lib.js`.

## Math

Effects call the `Px` functions above instead of .NET's `Math`: they are ports of the fdlibm and FreeBSD code V8 runs, and
.NET's C runtime rounds a share of inputs the other way (1 to 2 % of sines and cosines, a quarter of cube roots), which is
enough to move a pixel across a dither threshold. V8's `Math.pow` is the C runtime's pow behind V8's own answers for squares,
square roots, NaN exponents and 1 to an infinite power, so `Px.Pow` does the same: on Windows it calls ucrtbase's pow, the
one .NET's `Math.Pow` calls too, because Mono's own pow rounds two or three inputs in a thousand the other way. That makes
the parity data a Windows reference: node's pow and ucrtbase's still differ on about one input in five thousand, and no
library effect at its defaults reaches one. `Math.Sqrt`, `Floor`, `Ceiling`, `Abs`, `Min` and `Max` are exact in both.

## Differences

- Effects in a `Chain.Of` move each draw on their own zero `Emitter`, placed at the move's painter origin, so their
  particles follow the move rather than staying where they were born when the move's emitter travels.
