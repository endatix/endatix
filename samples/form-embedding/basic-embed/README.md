# Basic Endatix Hub Embed

This sample shows how to embed an Endatix Hub form in a plain HTML page and log the public embed lifecycle events.

## Usage

1. Start Endatix Hub locally so the embed script is available at `http://localhost:3000/embed/v1/embed.js`.
2. Replace `data-form-id` in `index.html` with your form id.
3. Serve this folder with any static file server, for example:

   ```bash
   python3 -m http.server 8080
   ```

4. Open `http://localhost:8080` in your browser.

Prefer the `CustomEvent` logs for application integrations. The raw `postMessage` log is included only for local diagnostics.
