namespace Rustaveli.Pdf;

/// <summary>
/// Content composed afresh for every page it reaches, knowing the page and the room left on it: a running total
/// carried from page to page, say, or a list set only as far as the page holds.
/// </summary>
/// <typeparam name="TState">
/// How far the content has got, handed from one page to the next. Composing must not change it: a page may be
/// measured more than once before it is drawn, so each composition returns the state after it instead.
/// </typeparam>
public interface IDynamicContent<TState>
{
    /// <summary>How far the content has got before its first page.</summary>
    TState Initial { get; }

    /// <summary>What to draw on <paramref name="page"/>, having got as far as <paramref name="state"/>.</summary>
    DynamicPart<TState> Compose(DynamicPage page, TState state);
}
