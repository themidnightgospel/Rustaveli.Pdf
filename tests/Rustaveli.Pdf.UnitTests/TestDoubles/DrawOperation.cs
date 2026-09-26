namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A drawing operation captured with its coordinates already resolved to page space.
/// </summary>
internal abstract record DrawOperation(Offset Position);
