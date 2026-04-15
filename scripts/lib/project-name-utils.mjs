const DEFAULT_PROJECT_NAME = "endatix";
const PROJECT_NAME_INPUT_MAX_LENGTH = 24;

/**
 * Normalizes a project name to a consistent format.
 * @param {string} projectName - The project name to normalize.
 * @returns {string} The normalized project name.
 */
export function normalizeProjectName(projectName) {
  const input = String(projectName ?? "")
    .slice(0, PROJECT_NAME_INPUT_MAX_LENGTH)
    .trim()
    .toLowerCase();

  if (!input) return DEFAULT_PROJECT_NAME;

  const normalized = input
    .replaceAll(/[^a-z0-9]+/g, "-")
    .replace(/^(?=(-+))\1/, "") // Trims leading dashes via atomic grouping(?=(pattern))
    .replace(/(?=(-+))\1$/, ""); // Trims trailing dashes via atomic grouping(?=(pattern))

  return normalized || DEFAULT_PROJECT_NAME;
}
