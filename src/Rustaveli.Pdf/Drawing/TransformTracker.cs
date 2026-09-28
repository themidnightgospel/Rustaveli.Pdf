using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Follows the transforms a surface is given, saved and restored as it is, for surfaces that must know where their
/// origin lies on the page without drawing anything themselves.
/// </summary>
internal sealed class TransformTracker
{
    private readonly Stack<Transform> _saved = new Stack<Transform>();

    public Transform Current { get; private set; } = Transform.Identity;

    /// <summary>Where the origin lies on the page, in the engine's own space: from the top left, Y down.</summary>
    public Offset Origin
    {
        get
        {
            (double x, double y) = Current.Apply(0, 0);
            return new Offset((float)x, (float)y);
        }
    }

    public void Reset()
    {
        _saved.Clear();
        Current = Transform.Identity;
    }

    public void Save() => _saved.Push(Current);

    public void Restore() => Current = _saved.Pop();

    public void Translate(Offset offset) => Current = Current.After(Transform.Translation(offset.X, offset.Y));

    public void Scale(float scaleX, float scaleY) => Current = Current.After(Transform.Scaling(scaleX, scaleY));

    public void Rotate(float degrees) => Current = Current.After(Transform.Rotation(degrees));
}
