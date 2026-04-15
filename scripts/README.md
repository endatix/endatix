# OSS Scripts

Lightweight shared utilities for OSS scripts live in `oss/scripts/lib`.

## Local Testing

Run tests with Node's built-in test runner (no extra dependencies):

```bash
node --test ./scripts/__tests__/*.test.mjs
```

Optional syntax check:

```bash
node --check ./scripts/lib/secret-generation.mjs
node --check ./scripts/lib/bicepparam-utils.mjs
node --check ./scripts/lib/console-format.mjs
```

## Conventions

- Keep helpers dependency-free and Node built-in only.
- Use `__tests__` for script/unit tests.
- Keep entrypoints under feature/sample folders (for example `samples/deployment-scripts/azure`).
