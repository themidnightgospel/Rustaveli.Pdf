# Approved snapshots

Pages of the conformance specimens as rendered by PDFium at 96 DPI, named `<specimen>.page<N>.png`. A visual test
fails when a page drifts from its snapshot beyond anti-aliasing noise, and writes the received image and a diff to
`artifacts/snapshots`.

When a change to what pages look like is intended, inspect the received and diff images, then approve them:

    dotnet run eng/approve-snapshots.cs

Approval is the review: a snapshot is only as correct as the page it was approved from.
