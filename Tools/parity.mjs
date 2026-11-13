import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const PACKAGE = fileURLToPath(new URL("..", import.meta.url));
const OUT = join(PACKAGE, "Tests", "Editor", "Parity.json");

const args = process.argv.slice(2);
const flag = (name, otherwise) => (args.includes(name) ? args[args.indexOf(name) + 1] : otherwise);
const pixelfx = resolve(PACKAGE, flag("--pixelfx", "../pixelfx"));
const framesDir = flag("--frames", null);
const wanted = args.filter((a, i) => !a.startsWith("--") && !args[i - 1]?.startsWith("--"));

const { allIds, loadEffect, renderFrames } = await import(pathToFileURL(join(pixelfx, "tools", "lib", "fxlib.mjs")).href);
const { encodePNG } = await import(pathToFileURL(join(pixelfx, "tools", "lib", "png.mjs")).href);

const hash = (rgba) => createHash("sha256").update(rgba).digest("hex").slice(0, 16);
const version = JSON.parse(readFileSync(join(pixelfx, "package.json"), "utf8")).version;
const commit = execFileSync("git", ["-C", pixelfx, "rev-parse", "HEAD"], { encoding: "utf8" }).trim();

const cases = [];
for (const id of allIds(join(pixelfx, "src", "fx"))) {
  const name = basename(id);
  if (framesDir ? !wanted.includes(name) : wanted.length > 0 && !wanted.includes(name)) continue;
  const { w, h, frames } = renderFrames(await loadEffect(id, join(pixelfx, "src", "fx")));
  cases.push({ id: name, w, h, frames: frames.map((f) => hash(f.clear)) });
  if (framesDir) {
    mkdirSync(framesDir, { recursive: true });
    frames.forEach((f, i) => writeFileSync(join(framesDir, `${name}-${i}.js.png`), encodePNG(w, h, f.clear)));
  }
}
if (!framesDir && wanted.length === 0) writeFileSync(OUT, JSON.stringify({ pixelfx: `${version} ${commit}`, cases }, null, 1) + "\n");
console.log(cases.map((c) => `${c.id} ${c.w}x${c.h} ${c.frames.length}f`).join("\n"));
