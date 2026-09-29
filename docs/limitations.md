# Known limitations

**Complex scripts need the Shaping package.** Without `Rustaveli.Pdf.Shaping`, Arabic, Hebrew points and Indic and
South-East Asian scripts are set without their shaping rules: letters unjoined, marks unplaced.

**SVG is read as drawing tools write it.** Radial gradients are drawn in the mean of their colours, and filters,
masks, patterns and markers are left out.

**XPS is written on Windows only**, as it relies on the platform's XPS support.

