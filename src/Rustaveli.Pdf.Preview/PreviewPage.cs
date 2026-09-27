namespace Rustaveli.Pdf.Preview;

/// <summary>
/// The page the browser shows: the document's pages one under another at their size on screen, redrawn when the
/// document changes, and a failure laid over them when it cannot be set.
/// </summary>
internal static class PreviewPage
{
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Preview</title>
        <style>
          body { margin: 0; background: #3a3a3a; color: #eee; font: 13px system-ui, sans-serif; }
          #pages { display: flex; flex-direction: column; align-items: center; gap: 16px; padding: 24px 16px 48px; }
          #pages img { background: #fff; box-shadow: 0 2px 14px rgba(0, 0, 0, .5); max-width: calc(100vw - 32px); height: auto; }
          #failure { display: none; position: fixed; inset: 0; margin: 0; padding: 32px; overflow: auto; background: rgba(40, 0, 0, .94);
                     color: #ffd9d9; font: 13px ui-monospace, Consolas, monospace; white-space: pre-wrap; }
          #status { position: fixed; right: 12px; bottom: 8px; opacity: .65; }
        </style>
        </head>
        <body>
        <div id="pages"></div>
        <pre id="failure"></pre>
        <div id="status">Connecting</div>
        <script>
          let shown = -1;

          async function look() {
            try {
              const state = await (await fetch('/state', { cache: 'no-store' })).json();

              if (state.version !== shown) {
                shown = state.version;
                const failure = document.getElementById('failure');
                failure.textContent = state.error ?? '';
                failure.style.display = state.error ? 'block' : 'none';

                if (!state.error) {
                  const pages = document.getElementById('pages');
                  pages.replaceChildren(...state.pages.map(([width, height], index) => {
                    const image = new Image(width, height);
                    image.src = '/pages/' + (index + 1) + '?v=' + state.version;
                    image.alt = 'Page ' + (index + 1);
                    return image;
                  }));
                }

                document.getElementById('status').textContent =
                  state.error ? 'Cannot be set' : state.pages.length + (state.pages.length === 1 ? ' page' : ' pages');
              }
            } catch {
              document.getElementById('status').textContent = 'Disconnected';
            }

            setTimeout(look, 600);
          }

          look();
        </script>
        </body>
        </html>
        """;
}
