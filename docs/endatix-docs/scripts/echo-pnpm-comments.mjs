import fs from "fs";

const pkg = JSON.parse(fs.readFileSync("package.json", "utf8"));

const comments = pkg.pnpm?.comments;

if (!comments) process.exit(0);

console.log("\nℹ️ pnpm comments:\n");

for (const [key, value] of Object.entries(comments)) {
  if (typeof value === "string" && value.includes("|")) {
    const items = value.split("|").map((item) => item.trim()).filter((item) => item.length > 0);
    console.log(`❯ ${key}:`);
    for (const item of items) {
      console.log(`  - ${item}`);
    }
  } else {
    console.log(`❯ ${key}: ${typeof value === "string" ? value : JSON.stringify(value, null, 2)}`);
  }
}

console.log("");