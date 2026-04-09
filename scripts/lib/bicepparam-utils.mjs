export function readStringParamFromBicepParam(content, paramName) {
  const regex = new RegExp(`param\\s+${paramName}\\s*=\\s*'([^']*)'`);
  const match = content.match(regex);
  return match?.[1];
}

export function replaceStringParamInBicepParam(content, paramName, value) {
  const regex = new RegExp(`(param\\s+${paramName}\\s*=\\s*)'[^']*'`);
  if (!regex.test(content)) {
    throw new Error(
      `Required param '${paramName}' was not found in parameters.bicepparam`,
    );
  }
  return content.replace(regex, `$1'${value}'`);
}

export function applyStringParamReplacements(content, replacements) {
  let updated = content;
  for (const [paramName, value] of replacements) {
    updated = replaceStringParamInBicepParam(updated, paramName, value);
  }
  return updated;
}
