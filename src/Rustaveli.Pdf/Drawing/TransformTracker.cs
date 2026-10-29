using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Follows the transforms a surface is given, saved and restored as it is, for surfaces that must know where their
/// origin lies on the page without drawing anything themselves.
/// </summary>
internal sealed class TransformTracker
{
    private readonly Stack<Transform> _saved = new Stack<Transform>();
    private Transform _current = Transform.Identity;

    /// <summary>Where the origin lies on the page, in the engine's own space: from the top left, Y down.</summary>
    public Offset Origin
    {
        get
        {
            (double x, double y) = _current.Apply(0, 0);
            return new Offset((float)x, (float)y);
        }
    }

    public void Reset()
    {
        _saved.Clear();
        _current = Transform.Identity;
    }

    public void Save() => _saved.Push(_current);

    public void Restore() => _current = _saved.Pop();

    public void Translate(Offset offset) => _current = _current.After(Transform.Translation(offset.X, offset.Y));

    public void Scale(float scaleX, float scaleY) => _current = _current.After(Transform.Scaling(scaleX, scaleY));

    public void Rotate(float degrees) => _current = _current.After(Transform.Rotation(degrees));

    public void Concatenate(float a, float b, float c, float d, float e, float f) =>
        _current = _current.After(new Transform(a, b, c, d, e, f));
}
