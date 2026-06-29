import { execFileSync } from 'node:child_process';

if (process.env.GITHUB_ACTIONS !== 'true') {
  console.log('IndexNow submission is only allowed in GitHub Actions. Skipping.');
  process.exit(0);
}


const HOST = 'docs.endatix.com';
const KEY = 'b4e2f8a1d73c950682341f7e9b0d6a4c';
const KEY_LOCATION = `https://${HOST}/${KEY}.txt`;
const BASE_URL = `https://${HOST}`;

function filePathToUrl(filePath) {
  // blog: blog/my-post.md → /blog/my-post/
  const blogMatch = filePath.match(/docs\/endatix-docs\/blog\/(.+)/);
  if (blogMatch) {
    const path = blogMatch[1].replace(/\.(md|mdx)$/, '').replace(/\/index$/, '');
    return path ? `${BASE_URL}/blog/${path}/` : `${BASE_URL}/blog/`;
  }

  // pages: src/pages/foo.md → /foo/
  const pageMatch = filePath.match(/docs\/endatix-docs\/src\/pages\/(.+)/);
  if (pageMatch) {
    const path = pageMatch[1].replace(/\.(tsx?|md|mdx)$/, '').replace(/\/index$/, '');
    return path ? `${BASE_URL}/${path}/` : `${BASE_URL}/`;
  }

  // docs: docs/getting-started/foo.md → /docs/getting-started/foo/
  const docsMatch = filePath.match(/docs\/endatix-docs\/docs\/(.+)/);
  if (!docsMatch) return null;

  let path = docsMatch[1];
  if (path.endsWith('/_category_.json') || path === '_category_.json') {
    path = path.replace(/\/?_category_\.json$/, '');
  } else if (/\.(md|mdx)$/.test(path)) {
    path = path.replace(/\.(md|mdx)$/, '').replace(/\/index$/, '');
  } else {
    return null;
  }

  return path ? `${BASE_URL}/docs/${path}/` : `${BASE_URL}/docs/`;
}

async function submit(urlList) {
  const res = await fetch('https://api.indexnow.org/indexnow', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({ host: HOST, key: KEY, keyLocation: KEY_LOCATION, urlList }),
  });
  console.log(`IndexNow: ${res.status} ${res.statusText}`);
}

let changedFiles = [];
let diffAvailable = false;
try {
  const repoRoot = execFileSync('/usr/bin/git', ['rev-parse', '--show-toplevel']).toString().trim();
  changedFiles = execFileSync(
    '/usr/bin/git',
    ['diff', '--name-only', 'HEAD~1', 'HEAD', '--', 'docs/endatix-docs/docs/', 'docs/endatix-docs/blog/', 'docs/endatix-docs/src/pages/'],
    { cwd: repoRoot }
  ).toString().trim().split('\n').filter(Boolean);
  diffAvailable = true;
} catch {
  // shallow clone or first commit — fall back to sitemap
}

if (!diffAvailable) {
  console.log('No diff available, submitting sitemap as fallback.');
  await submit([`${BASE_URL}/sitemap.xml`]);
} else if (changedFiles.length === 0) {
  console.log('No docs files changed, skipping IndexNow submission.');
} else {
  const urls = [...new Set(changedFiles.map(filePathToUrl).filter(Boolean))];
  if (urls.length === 0) {
    console.log('No indexable URLs in changed files.');
  } else {
    console.log(`Submitting ${urls.length} URL(s):`);
    urls.forEach(u => console.log(` - ${u}`));
    await submit(urls);
  }
}
