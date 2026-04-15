const GREEN_CHECK = "\x1b[32m✓\x1b[0m";
const CYAN_INFO = "\x1b[36mℹ\x1b[0m";
const YELLOW_WARN = "\x1b[33m⚠\x1b[0m";
const GREEN_PROMPT = "\x1b[1;32m>\x1b[0m";
const RETRO_BEIGE_CODE = "\x1b[38;5;223m";
const RED = "\x1b[31m";
const DIM_GRAY = "\x1b[90m";
const RESET = "\x1b[0m";
const NEWLINE = "\n";

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
  return `${YELLOW_WARN} ${text}${RESET}`;
}

export function infoText(text) {
  return `${CYAN_INFO} ${text}${RESET}`;
}

/** Muted tip line (lightbulb + gray) — distinct from cyan {@link infoText}. */
export function tipText(text) {
  return `${DIM_GRAY}💡 ${text}${RESET}`;
}

export function errorText(text) {
  return `${RED} ${text}${RESET}`;
}

export function formatCommandSnippet(command) {
  return `     ${GREEN_PROMPT} ${RETRO_BEIGE_CODE}${command}${RESET}`;
}
