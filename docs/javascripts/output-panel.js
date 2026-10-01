// Shows the page each guide example sets beside it. On a wide screen the page appears in a panel in the right-hand
// column, following the example being read; on a narrow one it stays under the example. The images are written by
// the guide tests (tests/Rustaveli.Pdf.ConformanceTests/Guide/GuideOutput.cs).
(() => {
  const outputs = [...document.querySelectorAll(".md-content img.rp-output")];
  const column = document.querySelector(".md-sidebar--secondary .md-sidebar__inner");

  if (outputs.length === 0 || column === null) {
    return;
  }

  document.body.classList.add("rp-has-output");

  const panel = document.createElement("aside");
  panel.className = "rp-panel";
  panel.setAttribute("aria-label", "What the example sets");
  panel.innerHTML =
    '<div class="rp-panel__bar"><span>output</span><span class="rp-panel__caption"></span></div>' +
    '<div class="rp-panel__canvas"><img alt=""></div>';
  column.prepend(panel);

  const image = panel.querySelector("img");
  const caption = panel.querySelector(".rp-panel__caption");

  // An example is the code block just before its output's paragraph.
  const examples = outputs.map(output => {
    const paragraph = output.closest("p");
    const block = paragraph !== null ? paragraph.previousElementSibling : null;
    return { output, block: block !== null ? block : output };
  });

  let shown = null;

  function show(example) {
    if (example === shown) {
      return;
    }

    shown = example;
    image.src = example.output.currentSrc || example.output.src;
    image.alt = example.output.alt;
    caption.textContent = example.output.dataset.caption || "";
  }

  // The example shown is the last one whose code has reached the upper part of the window.
  function follow() {
    let current = examples[0];

    for (const example of examples) {
      if (example.block.getBoundingClientRect().top < window.innerHeight * 0.45) {
        current = example;
      }
    }

    show(current);
  }

  let pending = false;

  window.addEventListener("scroll", () => {
    if (!pending) {
      pending = true;
      window.requestAnimationFrame(() => {
        pending = false;
        follow();
      });
    }
  }, { passive: true });

  follow();
})();
