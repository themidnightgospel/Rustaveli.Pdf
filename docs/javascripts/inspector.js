// The home page's inspector: the frames the preview's inspector records on the invoice's page, written by the tests
// (tests/Rustaveli.Pdf.ConformanceTests/Guide/HomePageTests.cs). Pointing at a frame in the list outlines it on the page.
(() => {
  const inspector = document.querySelector(".rp-inspector");

  if (inspector === null) {
    return;
  }

  const tree = inspector.querySelector(".rp-inspector__tree");
  const outline = inspector.querySelector(".rp-inspector__outline");
  const label = inspector.querySelector(".rp-inspector__label");
  const pageWidth = Number(inspector.dataset.pageWidth);
  const pageHeight = Number(inspector.dataset.pageHeight);

  const points = value => value.toLocaleString("en", { maximumFractionDigits: 1 });

  function choose(row, frame) {
    tree.querySelectorAll('[aria-pressed="true"]').forEach(other => other.setAttribute("aria-pressed", "false"));
    row.setAttribute("aria-pressed", "true");

    outline.style.left = frame.x * 100 + "%";
    outline.style.top = frame.y * 100 + "%";
    outline.style.width = frame.width * 100 + "%";
    outline.style.height = frame.height * 100 + "%";
    outline.hidden = false;

    label.textContent =
      frame.name + " · " + points(frame.width * pageWidth) + " × " + points(frame.height * pageHeight) + " pt";
  }

  fetch(inspector.dataset.frames)
    .then(response => response.json())
    .then(frames => {
      let first = null;

      for (const frame of frames) {
        const row = document.createElement("button");
        row.type = "button";
        row.className = "rp-inspector__row";
        row.style.setProperty("--depth", frame.depth);
        row.setAttribute("aria-pressed", "false");
        row.textContent = frame.name;

        const select = () => choose(row, frame);
        row.addEventListener("mouseenter", select);
        row.addEventListener("focus", select);
        row.addEventListener("click", select);
        tree.append(row);

        if (first === null && frame.name === "Table") {
          first = select;
        }
      }

      if (first !== null) {
        first();
      }
    })
    .catch(() => {
      label.textContent = "The frames could not be loaded.";
    });
})();
