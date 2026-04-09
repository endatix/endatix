const GREEN_CHECK = "\x1b[32m✓\x1b[0m";
const CYAN_INFO = "\x1b[36mℹ\x1b[0m";
const YELLOW_WARN = "\x1b[33m⚠\x1b[0m";

export function printGeneratedFiles(files) {
  console.log("\n🔐 Endatix Azure quickstart secrets generated:");
  for (const file of files) {
    console.log(`  ${GREEN_CHECK} ${file}`);
  }
  console.log("");
}

export function printNextSteps(lines) {
  console.log(`${CYAN_INFO} Next steps`);
  for (const line of lines) {
    console.log(line);
  }
  console.log("");
}

export function warningText(text) {
  return `${YELLOW_WARN} ${text}`;
}
