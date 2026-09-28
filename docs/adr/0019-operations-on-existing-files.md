# 0019 — Operations on existing files read them in managed code

**Status:** Accepted

## Context
Documents are not only generated: they are combined with others, cut down to some of their pages, stamped with a
watermark or laid over a letterhead, given attachments — the XML of an electronic invoice — protected by password,
and prepared for viewing over the web. Every one of these starts from a PDF this library did not write, and may
not have written well: files with cross-reference tables and streams, object streams, incremental updates, damaged
offsets, and encryption.

## Decision
The operations live in their own package, **Rustaveli.Pdf.Operations**, over a **managed reader** of the same
kind as the managed writer (ADR 0001): no native library, on every platform the core runs on.

The reader parses what a file says it holds, following each cross-reference section back through its updates,
and reads objects lazily, from object streams where they are kept there. Where the cross-reference section is
missing or wrong, it rebuilds one by scanning the file for objects, as viewers do. Streams are decoded only when an
operation needs their content; copied pages keep their streams exactly as they were encoded.

A file is edited as a list of pages, each still pointing into the file it came from. Saving writes a new file:
each page, and everything it reaches, is copied once through the core writer, whose numbering, object streams and
compression it shares with generated documents. Document-wide parts that stay valid whatever pages are kept — the
information dictionary, language, metadata, output intents — come from the first file; parts that point at pages —
outlines, named destinations, structure — are carried only while the first file's pages are all kept in order.

Protection uses PDF's standard security handler at every strength a reader still honours: RC4 at 40 and 128 bits,
AES at 128 and 256. The writer encrypts each object as it writes it, so encrypted output streams as generated output
does.

## Consequences
- Operations run wherever the core runs, with nothing to install, and are tested against qpdf, which reads,
  decrypts and checks linearisation independently.
- Files are rewritten, not updated in place: saving always produces a compact file with one cross-reference section.
- Outlines and structure of files merged in are not carried over; combining their trees is left for later.
