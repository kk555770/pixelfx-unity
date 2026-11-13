// Checks the pixelfx original for new commits, at most once a day, and lists what the C# port has to redo.
// Run it before any pixelfx task: node Tools/upstream.mjs [--pixelfx <checkout>] [--force]
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const DAY = 24 * 60 * 60 * 1000;
const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const pixelfx = resolve(root, option("--pixelfx", "../pixelfx"));
const stateFile = join(root, "Tools", ".upstream.json");
const git = (...rest) => execFileSync("git", ["-C", pixelfx, ...rest], { encoding: "utf8" }).trim();
const short = (commit) => commit.slice(0, 7);

const ported = JSON.parse(readFileSync(join(root, "Tests", "Editor", "Parity.json"), "utf8")).pixelfx.split(" ")[1];
const state = existsSync(stateFile) ? JSON.parse(readFileSync(stateFile, "utf8")) : {};
const fresh = Date.now() - Date.parse(state.checkedAt ?? 0) < DAY;

let latest = state.latest;
if (fresh && !args.includes("--force")) {
  console.log(`上次確認在 ${state.checkedAt}，未滿一天，沿用當時查到的原版 ${short(latest)}。`);
} else {
  git("fetch", "--quiet", "origin");
  latest = git("rev-parse", "origin/main");
  writeFileSync(stateFile, JSON.stringify({ checkedAt: new Date().toISOString(), latest }, null, 2) + "\n");
  console.log(`已向原版確認，最新是 ${short(latest)}。`);
}

if (latest === ported) {
  console.log(`原版沒有新版本，移植對照的 ${short(ported)} 就是最新。`);
  process.exit(0);
}

const changes = git("diff", "--name-status", `${ported}..${latest}`, "--", "src")
  .split("\n")
  .filter(Boolean)
  .map((line) => line.split("\t"));
const effect = (file) => /^src\/fx\/[^/]+\/[^_/][^/]*\.js$/.test(file);
const listed = (status) => changes.filter(([s, file]) => s.startsWith(status) && effect(file)).map(([, file]) => basename(file, ".js"));
const shared = changes.filter(([, file]) => !effect(file)).map(([, file]) => file);

const behind = () => {
  try {
    git("merge-base", "--is-ancestor", "HEAD", latest);
    return true;
  } catch {
    return false;
  }
};

if (git("rev-parse", "HEAD") === latest) {
  console.log(`原版 checkout ${pixelfx} 已經在 ${short(latest)}。`);
} else if (!git("status", "--porcelain") && behind()) {
  git("merge", "--ff-only", "--quiet", latest);
  console.log(`原版 checkout ${pixelfx} 已快轉到 ${short(latest)}。`);
} else {
  console.log(`原版 checkout ${pixelfx} 有本機改動或分岔，沒有自動快轉，請手動更新到 ${short(latest)}。`);
}

if (!changes.length) {
  console.log(`原版從 ${short(ported)} 到 ${short(latest)} 只改了 src 以外的檔案，C# 版的移植仍是最新。`);
  process.exit(0);
}

console.log(`\n原版從 ${short(ported)} 更新到 ${short(latest)}，C# 版要重新移植：`);
console.log(`- 改動的特效：${listed("M").join("、") || "無"}`);
console.log(`- 新增的特效：${listed("A").join("、") || "無"}`);
console.log(`- 刪除的特效：${listed("D").join("、") || "無"}`);
console.log(`- 改動的核心與共用檔：${shared.join("、") || "無"}（有改時，重產比對資料後跑完整比對，找出受影響的特效）`);
console.log("\n接著：node Tools/parity.mjs 重產比對資料 → 照清單重新移植 → 比對全過後提交並推上 → 各專案更新套件版本。");
process.exit(2);
