export function normalizeProjectName(projectName) {
  const normalized = (projectName ?? '')
    .trim()
    .toLowerCase()
    .replaceAll(/[^a-z0-9-]/g, '-')
    .replaceAll(/-+/g, '-')
    .replaceAll(/^-+|-+$/g, '');

  return normalized || 'endatix';
}
