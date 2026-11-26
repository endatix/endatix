# Website

This website is built using [Docusaurus](https://docusaurus.io/), a modern static website generator.

> [!TIP]
> To learn more how to add pages and manage the content with it, check this [5 minutes guide](https://docusaurus.io/docs)

### Prerequisites

* Node.js 22 (non-EOL LTS — see [Node.js EOL](https://nodejs.org/en/about/eol))
* pnpm package manager

### Installation

```
$ pnpm install
```

### Local Development

```
$ pnpm start
```

This command starts a local development server and opens up a browser window. Most changes are reflected live without having to restart the server. Site should run on [http://localhost:3005](http://localhost:3005/)

### Build

```
$ pnpm build
```

This command generates static content into the `build` directory and can be served using any static contents hosting service.

### Deployment

A push to `main` that touches this folder deploys the site two ways while it moves off Azure:

- `.github/workflows/deploy-endatix-docs.yml` uploads `build/` to Azure Static Web Apps, with `staticwebapp.config.json` for its routing rules, then submits changed URLs to IndexNow.
- `.github/workflows/publish-docs-image.yml` builds `ghcr.io/endatix/endatix-docs:<commit SHA>`: nginx serving `build/` as a non-root user on port 8080 (`docker/Dockerfile`). It asks `endatix-infra` to pin that tag, and ArgoCD rolls it out on the Hetzner cluster. `docker/nginx.conf` carries the rules from `staticwebapp.config.json` (redirects, `trailingSlash: never`, the 404 page, headers, the `.json` MIME type), so change both together until the Azure deploy is removed. HSTS comes from the cluster's Ingress.

To check the image locally:

```
$ pnpm build
$ docker build -f docker/Dockerfile -t endatix-docs .
$ docker run --rm --read-only --tmpfs /tmp -p 8080:8080 endatix-docs
```
