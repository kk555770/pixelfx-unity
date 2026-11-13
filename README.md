# pixelfx (C#)

A Unity package that draws [pixelfx](https://github.com/patch-gg/pixelfx) effects live, frame by frame, the way pixelfx's
own gallery plays them in a browser. Every ported effect draws the same pixels as the JavaScript original, checked frame
by frame by the parity tests.

The runtime assembly `Pixelfx` has no UnityEngine references. A game turns a frame into a texture itself:

```csharp
using Pixelfx;

var canvas = new Canvas(Effects.Get("hit-spark"), new Options { ["height"] = 14.0 });
canvas.Fx.Update(Time.deltaTime);
texture.SetPixelData(canvas.Picture(), 0); // RGBA32, rows from the top
```

`Look` bundles a module with a game's options, turn directions, margin and hit-stops.

## Use it in a Unity project

Add the package and its tests to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.pixelfx.unity": "https://github.com/kk555770/pixelfx-unity.git"
  },
  "testables": ["com.pixelfx.unity"]
}
```

A local checkout works too: `"com.pixelfx.unity": "file:../../../pixelfx-unity"`, relative to the `Packages` folder.

## Layout

- `Runtime/Core` ports `src/core` of pixelfx.
- `Runtime/Fx/<category>` ports `src/fx/<category>`, one file per effect. Each effect declares a public static
  `Module`, and `Effects.All` finds it, so a new port is just a new file.
- `Editor/Parity.cs` compares a module's frames with reference hashes. Games use it for their own effects too.
- `Tests/Editor` runs the parity check on every ported effect and act.

## Parity

`Tests/Editor/Parity.json` holds, for every pixelfx library effect at its default options, the canvas size and the
first 8 bytes of the SHA-256 of each frame's RGBA. Regenerate it on Windows x64 from a pixelfx checkout with its dev
dependencies installed (`npm install` there; the JS original's `**` goes through its esbuild):

```sh
node Tools/parity.mjs --pixelfx ../pixelfx
```

`--pixelfx` defaults to `../pixelfx` beside this package. Why Windows x64, and why pow is replaced: `CORE.md`, Math. When a test fails, it writes the C# frame that differs to
`<temp>/pixelfx-parity/<name>-<frame>.unity.png`. Write the JavaScript frames next to it to compare:

```sh
node Tools/parity.mjs --frames <temp>/pixelfx-parity hit-spark
```

`Tests/Editor/ActParity.json` does the same for every act: the timing (windup, hits with their freeze, shake and push,
recovery, poses) as one line of text, and the hash of each frame of the act played at 30 fps on transparency, marked
while it shakes or freezes. Each act runs in a node process of its own, because the rig caches poses for the life of a
process; the C# side empties that cache with `Rig.ClearCache()` before each act.

```sh
node Tools/parity.mjs --acts
node Tools/parity.mjs --act-frames <temp>/pixelfx-parity sword-slash
```

To iterate without Unity, `Tools/ParityCli~` builds the runtime, `Editor/ParityRun.cs` and a console runner with the
.NET SDK (Unity ignores folders ending in `~`). Run it from inside the package:

```sh
dotnet run --project Tools/ParityCli~ -c Release -- melee
dotnet run --project Tools/ParityCli~ -c Release -- --acts melee
```

`CORE.md` maps every export of pixelfx's `src/core` to its C# counterpart.
