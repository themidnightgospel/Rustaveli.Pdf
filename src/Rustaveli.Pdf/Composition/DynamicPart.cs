namespace Rustaveli.Pdf;

/// <summary>What dynamic content draws on one page, how far it has got after it, and whether it goes on.</summary>
/// <typeparam name="TState">How far the content has got.</typeparam>
/// <param name="Content">Composes what is drawn on the page; it is drawn as far as it fits.</param>
/// <param name="Next">How far the content has got once this page is drawn.</param>
/// <param name="HasMore">Whether the content goes on to another page.</param>
public sealed record DynamicPart<TState>(Action<IFrame> Content, TState Next, bool HasMore);
