namespace Rustaveli.Pdf.Benchmarks;

/// <summary>One row of an invoice or a large table.</summary>
public sealed record LineItem(string Code, string Description, string Amount);
