/**
 * Parses a string value into a boolean
 * Intended to be used for parsing environment variables & query parameters
 * @param value - The string value to parse, can be undefined
 * @returns true if value is 'true' or '1' (case-insensitive), false otherwise
 */
function parseBoolean(value: string | undefined): boolean {
  const trimmedValue = value?.trim();
  if (!trimmedValue) {
    return false;
  }

  const normalizedValue = trimmedValue.toLowerCase();
  return normalizedValue === "true" || normalizedValue === "1";
}

export { parseBoolean };
