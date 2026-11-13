// The JS original's side of the parity check. Hashes are all that's kept: 16 hex characters of SHA-256 per frame.
//   node Tools/parity.mjs                      every effect's frames -> Tests/Editor/Parity.json
//   node Tools/parity.mjs --acts               every act's timing and frames -> Tests/Editor/ActParity.json
//   node Tools/parity.mjs --frames DIR name... write those effects' frames as PNGs to DIR, to look at a mismatch
//   node Tools/parity.mjs --act-frames DIR name  write that act's frames as PNGs to DIR
// --pixelfx PATH points at the pixelfx checkout (default ../pixelfx), with its dev dependencies installed (npm install there).
// The JS original runs with Math.pow and ** correctly rounded (pow.mjs), as Px.Pow computes them, on any platform.
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { register } from "node:module";
import { basename, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { pow } from "./pow.mjs";

const SELF = fileURLToPath(import.meta.url);
const PACKAGE = fileURLToPath(new URL("..", import.meta.url));
const OUT = join(PACKAGE, "Tests", "Editor", "Parity.json");
const ACT_OUT = join(PACKAGE, "Tests", "Editor", "ActParity.json");

const args = process.argv.slice(2);
const flag = (name, otherwise) => (args.includes(name) ? args[args.indexOf(name) + 1] : otherwise);
const pixelfx = resolve(PACKAGE, flag("--pixelfx", "../pixelfx"));
const framesDir = flag("--frames", null);
const actFramesDir = flag("--act-frames", null);
const oneAct = flag("--act", null);
const wanted = args.filter((a, i) => !a.startsWith("--") && !args[i - 1]?.startsWith("--"));

// The parity data is a Windows x64 reference: node on macOS arm64 computes Math.sin, Math.exp and others differently in
// places (force-field's frame 18 is one), so the JSON files are written only from Windows x64.
const writes = !oneAct && !framesDir && !actFramesDir && wanted.length === 0;
const platform = `${process.platform}-${process.arch}`;
if (writes && platform !== "win32-x64")
  throw new Error(`the parity data is a Windows x64 reference and node on ${platform} draws some frames differently: write it from Windows x64`);

Math.pow = pow;
register("./pow.mjs", import.meta.url, {
  data: { esbuild: pathToFileURL(join(pixelfx, "node_modules", "esbuild", "lib", "main.js")).href, src: pathToFileURL(join(pixelfx, "src")).href + "/" },
});

const { allIds, loadEffect, renderFrames } = await import(pathToFileURL(join(pixelfx, "tools", "lib", "fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(join(pixelfx, "tools", "lib", "png.mjs")).href);
const core = await import(pathToFileURL(join(pixelfx, "src", "core", "index.js")).href);

const hash = (rgba) => createHash("sha256").update(rgba).digest("hex").slice(0, 16);
const version = JSON.parse(readFileSync(join(pixelfx, "package.json"), "utf8")).version;
const commit = execFileSync("git", ["-C", pixelfx, "rev-parse", "HEAD"], { encoding: "utf8" }).trim();
const FX = join(pixelfx, "src", "fx");

// ---- acts: timing as one line of text (Editor/ParityRun.cs writes the same), and frames at 30 fps on transparency
const value = (v) => (v === undefined ? "-" : v === null ? "null" : String(v));
const poseText = (p) => "{" + Object.keys(p).sort().map((k) => `${k}=${value(p[k])}`).join(",") + "}";
function timingText(mod) {
  const a = core.actTiming(mod);
  if (!a) return "none";
  return [
    `windup=${value(a.windup)}`,
    `recovery=${value(a.recovery)}`,
    `onBody=${a.onBody}`,
    `targets=${a.targets.map(value).join(",")}`,
    `hits=${a.hits.map((h) => `t=${value(h.t)},freeze=${value(h.freeze)},shake=${value(h.shake)},amp=${value(h.amp)},push=${value(h.push)},skin=${value(h.skin)},at=${value(h.at)}`).join("|")}`,
    `poses=${a.poses.map((k) => `${value(k.t)}/${k.how}/${poseText(k.pose)}`).join("|")}`,
  ].join(";");
}
const PAD = 6, FPS = 30;
function actCase(mod, id, pngDir) {
  const b = core.actBounds(mod), w = b.x1 - b.x0 + PAD * 2, h = b.y1 - b.y0 + PAD * 2;
  const frame = core.createFrame(w, h), act = core.createAct(mod, { x: PAD - b.x0, y: PAD - b.y0 });
  const frames = [], max = Math.round((mod.meta.loop ? mod.meta.duration * 2 - act.t : 8) * FPS);
  const show = () => {
    core.clearFrame(frame);
    act.draw(frame);
    const rgba = core.composite(frame, new Uint8ClampedArray(w * h * 4), null), [sx, sy] = act.shake;
    if (pngDir) writeFileSync(join(pngDir, `${id}-act-${frames.length}.js.png`), encodePNG(w, h, rgba));
    frames.push(hash(rgba) + (sx || sy ? `~${sx},${sy}` : "") + (act.frozen ? "*" : ""));
  };
  show();
  for (let i = 0; i < max && !act.done(); i++) {
    act.update(1 / FPS);
    show();
  }
  return { id, timing: timingText(mod), w, h, frames };
}

const ids = allIds(FX);
const idOf = (name) => ids.find((i) => basename(i) === name);

if (oneAct) {
  // one act in a process of its own: the rig caches poses for the life of the process, and a fresh one starts the way C# does
  const fx = await loadEffect(idOf(oneAct), FX);
  if (actFramesDir) mkdirSync(actFramesDir, { recursive: true });
  process.stdout.write(JSON.stringify(actCase(fx.mod, oneAct, actFramesDir)));
} else if (args.includes("--acts")) {
  const cases = [];
  for (const id of ids) {
    const name = basename(id);
    if (wanted.length > 0 && !wanted.includes(name)) continue;
    const fx = await loadEffect(id, FX);
    if (!fx.mod.act) continue;
    cases.push(JSON.parse(execFileSync(process.execPath, [SELF, "--pixelfx", pixelfx, "--act", name], { encoding: "utf8", maxBuffer: 1 << 26 })));
    console.log(`${name} ${cases.at(-1).w}x${cases.at(-1).h} ${cases.at(-1).frames.length}f`);
  }
  if (wanted.length === 0) writeFileSync(ACT_OUT, JSON.stringify({ pixelfx: `${version} ${commit}`, cases }, null, 1) + "\n");
} else if (actFramesDir) {
  for (const name of wanted) execFileSync(process.execPath, [SELF, "--pixelfx", pixelfx, "--act", name, "--act-frames", actFramesDir]);
} else {
  const cases = [];
  for (const id of ids) {
    const name = basename(id);
    if (framesDir ? !wanted.includes(name) : wanted.length > 0 && !wanted.includes(name)) continue;
    const { w, h, frames } = renderFrames(await loadEffect(id, FX));
    cases.push({ id: name, w, h, frames: frames.map((f) => hash(f.clear)) });
    if (framesDir) {
      mkdirSync(framesDir, { recursive: true });
      frames.forEach((f, i) => writeFileSync(join(framesDir, `${name}-${i}.js.png`), encodePNG(w, h, f.clear)));
    }
  }
  if (!framesDir && wanted.length === 0) writeFileSync(OUT, JSON.stringify({ pixelfx: `${version} ${commit}`, cases }, null, 1) + "\n");
  console.log(cases.map((c) => `${c.id} ${c.w}x${c.h} ${c.frames.length}f`).join("\n"));
}
