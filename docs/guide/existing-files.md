# Existing files

The `Rustaveli.Pdf.Operations` package works with PDF files already written — by this library or anything else. It
reads them in managed code, repairing what it can of a damaged file, and writes the result anew.

## Combining documents while composing

Documents composed with this library are combined before export with `Document.Merge`. Page numbers run on from one
part to the next, or restart in each part:

```csharp
Document cover = Document.Compose(composition => composition.Section(section => section.Body().Text("Cover")));
Document report = Document.Compose(composition => composition.Section(section => section.Body().Text("Report")));

Document merged = Document.Merge(cover, report);
Document numberedApart = Document.Merge(cover, report).NumberPartsSeparately();
```

## Putting files together

`PdfFile` opens a file, changes it step by step, and saves the result:

```csharp
PdfFile.Open("contract.pdf")
    .KeepPages("1-3, 5, 8-last")
    .Append("appendix.pdf")
    .Append("terms.pdf", pages: "2")
    .Save("contract-with-appendix.pdf");
```

Page lists read as a print dialog's do: numbers from 1, ranges, `last`, in any order — `"last-1"` reverses the file.

## Stamps and letterheads

`Overlay` draws another file's pages over these; `Underlay` draws them beneath, as a letterhead. The pages of the
other file are used in turn, starting again when they run out, so a one-page stamp goes on every page:

```csharp
PdfFile.Open("letter.pdf")
    .Underlay("letterhead.pdf", onto: "1")
    .Overlay("copy-stamp.pdf")
    .Save("letter-on-letterhead.pdf");
```

## Attachments and electronic invoices

A file can carry other files. For an electronic invoice — ZUGFeRD, Factur-X — the XML travels inside a PDF/A-3
document as an alternative form of it, and is described in its metadata:

```csharp
PdfFile.Open("invoice.pdf")
    .Attach(new FileAttachment("factur-x.xml", File.ReadAllBytes("factur-x.xml"))
    {
        MediaType = "text/xml",
        Description = "Factur-X invoice",
        Relationship = AttachmentRelationship.Alternative,
    })
    .AddMetadata(File.ReadAllText("factur-x-metadata.xmp"))
    .Save("invoice-with-data.pdf");
```

A PDF/A-3 file stays PDF/A-3 with its attachments; this library's tests check such an invoice with veraPDF.

## Protection and web viewing

```csharp
PdfFile.Open("report.pdf")
    .Protect(new Protection { UserPassword = "open sesame", AllowPrinting = false })
    .Save("report-protected.pdf");

PdfFile.Open("report-protected.pdf", password: "open sesame")
    .Unprotect()
    .OptimizeForWeb()
    .Save("report-for-the-web.pdf");
```

A protected file opens with its user or owner password; saved without `Protect` or `Unprotect`, it keeps the
protection it had. `OptimizeForWeb` linearises the file so a browser shows the first page while the rest downloads.
`LiftRestrictions` drops the restrictions a signature places on a file, for a file that is to change anyway.
