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
- `Tests/Editor` runs the parity check on every ported effect.

## Parity

`Tests/Editor/Parity.json` holds, for every pixelfx library effect at its default options, the canvas size and the
first 8 bytes of the SHA-256 of each frame's RGBA. Regenerate it from a pixelfx checkout:

```sh
node Tools/parity.mjs --pixelfx ../pixelfx
```

`--pixelfx` defaults to `../pixelfx` beside this package. When a test fails, it writes the C# frame that differs to
`<temp>/pixelfx-parity/<name>-<frame>.unity.png`. Write the JavaScript frames next to it to compare:

```sh
node Tools/parity.mjs --frames <temp>/pixelfx-parity hit-spark
```
