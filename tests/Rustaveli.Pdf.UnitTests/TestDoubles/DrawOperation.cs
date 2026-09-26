namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A drawing operation captured with its coordinates already resolved to page space.
/// </summary>
public abstract record DrawOperation(Offset Position);
