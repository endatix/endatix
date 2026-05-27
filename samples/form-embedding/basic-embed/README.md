# Basic Endatix Hub Embed

This sample shows how to embed an Endatix Hub form in a plain HTML page and log the public embed lifecycle events.

## Usage

1. Start Endatix Hub locally so the embed script is available.
2. Replace `data-form-id` in `index.html` with your form id.
3. If you serve this sample from a different origin than Hub, replace `/embed/v1/embed.js` with the trusted Hub embed script URL, for example `http://localhost:3000/embed/v1/embed.js`.
4. Serve this folder with any static file server, for example:

   ```bash
   python3 -m http.server 8080
   ```

5. Open `http://localhost:8080` in your browser.

Prefer the `CustomEvent` logs for application integrations. The embed script validates iframe messages before it dispatches these events.
