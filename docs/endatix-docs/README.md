# Website

This website is built using [Docusaurus](https://docusaurus.io/), a modern static website generator.

> [!TIP]
> To learn more how to add pages and manage the content with it, check this [5 minutes guide](https://docusaurus.io/docs)

### Prerequisites

* Node.js version v21.2.0
* pnpm package manager

### Installation

```
$ pnpm install
```

### Local Development

```
$ pnpm start
```

This command starts a local development server and opens up a browser window. Most changes are reflected live without having to restart the server.

### Build

```
$ pnpm build
```

This command generates static content into the `build` directory and can be served using any static contents hosting service.

### Deployment

Using SSH:

```
$ USE_SSH=true pnpm run deploy
```

Not using SSH:

```
$ GIT_USER=<Your GitHub username> pnpm run deploy
```

If you are using GitHub pages for hosting, this command is a convenient way to build the website and push to the `gh-pages` branch.
