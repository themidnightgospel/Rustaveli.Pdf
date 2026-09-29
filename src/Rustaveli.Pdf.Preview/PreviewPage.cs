namespace Rustaveli.Pdf.Preview;

/// <summary>
/// The page the browser shows: the document's pages one under another at their size on screen, redrawn when the
/// document changes, a failure laid over them when it cannot be set, and an inspector beside them that outlines the
/// frame under the pointer, lists every frame drawn on a page within the one that drew it, and opens the line of code
/// that made each one in the editor.
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
        <link rel="icon" type="image/png" href="/icon.png">
        <style>
          :root { --ink: #e53935; --panel: #262626; --line: #3d3d3d; --dim: #9a9a9a; }
          body { margin: 0; height: 100vh; display: flex; background: #3a3a3a; color: #eee; font: 13px system-ui, sans-serif; }
          #pages { flex: 1; overflow: auto; display: flex; flex-direction: column; align-items: center; gap: 16px; padding: 24px 16px 48px; }
          .page { position: relative; flex: none; cursor: crosshair; }
          .page img { display: block; background: #fff; box-shadow: 0 2px 14px rgba(0, 0, 0, .5); max-width: calc(100vw - 32px); height: auto; }
          body.inspecting .page img { max-width: calc(100vw - 392px); }
          .box { display: none; position: absolute; pointer-events: none; box-sizing: border-box; }
          .hover { border: 1px dashed var(--ink); background: rgba(229, 57, 53, .08); }
          .chosen { border: 2px solid var(--ink); }
          #inspector { display: none; width: 360px; flex: none; flex-direction: column; background: var(--panel); border-left: 1px solid var(--line); }
          body.inspecting #inspector { display: flex; }
          #inspector header { padding: 10px 12px; border-bottom: 1px solid var(--line); font-weight: 600; }
          #chosen { padding: 10px 12px; border-bottom: 1px solid var(--line); line-height: 1.6; }
          #chosen:empty { display: none; }
          #tree { flex: 1; overflow: auto; padding: 6px 0 24px; }
          #tree details > div, #tree .leaf { padding-left: 14px; }
          #tree summary { list-style-position: outside; }
          .row { display: flex; gap: 8px; align-items: baseline; padding: 1px 12px 1px 4px; cursor: default; white-space: nowrap; }
          .row:hover { background: #333; }
          .row.selected { background: #4a2523; }
          .row .size { color: var(--dim); font-size: 11px; }
          a { color: #8ab4f8; text-decoration: none; }
          a:hover { text-decoration: underline; }
          .dim { color: var(--dim); }
          #failure { display: none; position: fixed; inset: 0; margin: 0; padding: 32px; overflow: auto; background: rgba(40, 0, 0, .94);
                     color: #ffd9d9; font: 13px ui-monospace, Consolas, monospace; white-space: pre-wrap; }
          #bar { position: fixed; left: 12px; bottom: 8px; display: flex; gap: 12px; align-items: center; }
          #bar button { font: inherit; color: inherit; background: #555; border: 0; border-radius: 4px; padding: 3px 10px; cursor: pointer; }
          #status { opacity: .65; }
        </style>
        </head>
        <body>
        <div id="pages"></div>
        <aside id="inspector">
          <header id="heading">Click a page to inspect it</header>
          <div id="chosen"></div>
          <div id="tree"></div>
        </aside>
        <pre id="failure"></pre>
        <div id="bar"><button id="toggle" type="button" title="Inspect frames (I)">Inspect</button><span id="status">Connecting</span></div>
        <script>
          let shown = -1;
          let frames = {};
          let inspected = null;
          let chosen = null;

          const element = id => document.getElementById(id);

          function make(tag, className, text) {
            const made = document.createElement(tag);
            if (className) made.className = className;
            if (text !== undefined) made.textContent = text;
            return made;
          }

          function framesOf(number) {
            frames[number] ??= fetch('/frames/' + number + '?v=' + shown, { cache: 'no-store' }).then(answer => answer.ok ? answer.json() : []);
            return frames[number];
          }

          function lineage(nodes, parent) {
            for (const node of nodes) {
              node.parent = parent;
              lineage(node.children, node);
            }
            return nodes;
          }

          function deepest(nodes, x, y) {
            for (let index = nodes.length - 1; index >= 0; index--) {
              const node = nodes[index];
              if (x >= node.x && y >= node.y && x <= node.x + node.width && y <= node.y + node.height)
                return deepest(node.children, x, y) ?? node;
            }
            return null;
          }

          function outline(box, node) {
            if (!node) { box.style.display = 'none'; return; }
            const page = box.parentElement;
            const scale = page.querySelector('img').clientWidth / Number(page.dataset.width);
            Object.assign(box.style, {
              display: 'block', left: node.x * scale + 'px', top: node.y * scale + 'px',
              width: Math.max(node.width * scale, 1) + 'px', height: Math.max(node.height * scale, 1) + 'px'
            });
          }

          const points = value => Math.round(value * 10) / 10;
          const size = node => points(node.width) + ' × ' + points(node.height) + ' pt';

          function source(node) {
            const at = node.source;
            if (!at) return make('span', 'dim', 'no line known');
            const link = make('a', '', at.split(/[\\/]/).pop());
            link.href = 'vscode://file/' + encodeURI(at.replace(/\\/g, '/'));
            link.title = 'Open ' + at + ' in the editor';
            return link;
          }

          function pageOf(number) { return document.querySelectorAll('.page')[number - 1]; }

          function row(node, number) {
            const line = make('div', 'row');
            line.append(make('span', '', node.name), make('span', 'size', size(node)));
            if (node.source) line.append(source(node));
            line.onmouseenter = () => outline(pageOf(number).querySelector('.hover'), node);
            line.onmouseleave = () => outline(pageOf(number).querySelector('.hover'), null);
            line.onclick = () => choose(number, node, false);
            node.row = line;
            return line;
          }

          function branch(node, number, depth) {
            if (!node.children.length) {
              const leaf = make('div', 'leaf');
              leaf.append(row(node, number));
              return leaf;
            }
            const folder = make('details');
            folder.open = depth < 3;
            const summary = make('summary');
            summary.append(row(node, number));
            const inner = make('div');
            inner.append(...node.children.map(child => branch(child, number, depth + 1)));
            folder.append(summary, inner);
            node.folder = folder;
            return folder;
          }

          function inspect(number) {
            if (inspected?.number !== number) {
              inspected = { number, ready: (async () => {
                chosen = null;
                element('chosen').replaceChildren();
                const nodes = lineage(await framesOf(number), null);
                element('heading').textContent = 'Page ' + number;
                element('tree').replaceChildren(...nodes.map(node => branch(node, number, 0)));
              })() };
            }
            return inspected.ready;
          }

          async function choose(number, node, fromPage) {
            document.body.classList.add('inspecting');
            await inspect(number);
            if (chosen?.row) chosen.row.classList.remove('selected');
            chosen = node;
            for (const box of document.querySelectorAll('.chosen')) outline(box, null);
            outline(pageOf(number).querySelector('.chosen'), node);
            for (let parent = node.parent; parent; parent = parent.parent) if (parent.folder) parent.folder.open = true;
            node.row.classList.add('selected');
            if (fromPage) node.row.scrollIntoView({ block: 'nearest' });

            const facts = element('chosen');
            facts.replaceChildren(make('div', '', node.name),
              make('div', 'dim', 'at ' + points(node.x) + ', ' + points(node.y) + ' pt, ' + size(node)));
            const from = make('div');
            from.append(source(node));
            facts.append(from);
          }

          function page(width, height, number) {
            const holder = make('div', 'page');
            holder.dataset.width = width * 72 / 96;
            const image = new Image(width, height);
            image.src = '/pages/' + number + '?v=' + shown;
            image.alt = 'Page ' + number;
            const hover = make('div', 'box hover');
            holder.append(image, hover, make('div', 'box chosen'));

            const at = async event => {
              const nodes = lineage(await framesOf(number), null);
              const scale = image.clientWidth / Number(holder.dataset.width);
              const bounds = image.getBoundingClientRect();
              return { nodes, node: deepest(nodes, (event.clientX - bounds.left) / scale, (event.clientY - bounds.top) / scale) };
            };

            holder.onmousemove = async event => {
              if (document.body.classList.contains('inspecting')) outline(hover, (await at(event)).node);
            };
            holder.onmouseleave = () => outline(hover, null);
            holder.onclick = async event => {
              await inspect(number);
              const { node } = await at(event);
              if (node) choose(number, node, true);
            };
            return holder;
          }

          async function look() {
            try {
              const state = await (await fetch('/state', { cache: 'no-store' })).json();

              if (state.version !== shown) {
                shown = state.version;
                frames = {};
                const failure = element('failure');
                failure.textContent = state.error ?? '';
                failure.style.display = state.error ? 'block' : 'none';

                if (!state.error) {
                  element('pages').replaceChildren(...state.pages.map(([width, height], index) => page(width, height, index + 1)));
                  const again = inspected?.number;
                  inspected = null;
                  if (again && again <= state.pages.length) inspect(again);
                  else { element('tree').replaceChildren(); element('chosen').replaceChildren(); element('heading').textContent = 'Click a page to inspect it'; }
                }

                element('status').textContent =
                  state.error ? 'Cannot be set' : state.pages.length + (state.pages.length === 1 ? ' page' : ' pages');
              }
            } catch {
              element('status').textContent = 'Disconnected';
            }

            setTimeout(look, 600);
          }

          element('toggle').onclick = () => document.body.classList.toggle('inspecting');
          document.addEventListener('keydown', event => {
            if (event.key === 'i' && !event.ctrlKey && !event.metaKey && !event.altKey) document.body.classList.toggle('inspecting');
          });

          look();
        </script>
        </body>
        </html>
        """;

    /// <summary>The library's logo, the icon of the browser tab the preview is shown in.</summary>
    public static byte[] Icon { get; } = ReadIcon();

    private static byte[] ReadIcon()
    {
        using Stream stream = typeof(PreviewPage).Assembly.GetManifestResourceStream("Rustaveli.Pdf.Preview.Icon.png")!;
        using MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
