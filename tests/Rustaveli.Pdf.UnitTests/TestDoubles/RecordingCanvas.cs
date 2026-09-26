using System.Numerics;
using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed class RecordingCanvas : IDocumentCanvas, ICanvas, IDisposable
{
    private readonly Stack<Matrix3x2> _saved = new Stack<Matrix3x2>();

    private Matrix3x2 _transform = Matrix3x2.Identity;

    private RecordedPage? _current;

    private const float Tolerance = 0.001f;

    public List<RecordedPage> Pages { get; } = new List<RecordedPage>();

    private RecordedPage Current => _current ?? throw new InvalidOperationException("No page is open.");

    public bool IsAtIdentity => Math.Abs(_transform.M11 - 1f) < 0.001f && Math.Abs(_transform.M22 - 1f) < 0.001f && Math.Abs(_transform.M12) < 0.001f && Math.Abs(_transform.M21) < 0.001f && Math.Abs(_transform.M31) < 0.001f && Math.Abs(_transform.M32) < 0.001f;

    public int PendingSaves => _saved.Count;

    public RecordedPage Page(int oneBasedNumber)
    {
        return Pages[oneBasedNumber - 1];
    }

    public void BeginPage(Size size)
    {
        _current = new RecordedPage(size);
        _transform = Matrix3x2.Identity;
        _saved.Clear();
        Pages.Add(_current);
    }

    public void EndPage()
    {
        _current = null;
    }

    public void Save()
    {
        _saved.Push(_transform);
    }

    public void Restore()
    {
        _transform = _saved.Pop();
    }

    public void Translate(Position offset)
    {
        _transform = Matrix3x2.CreateTranslation(offset.X, offset.Y) * _transform;
    }

    public void Scale(float scaleX, float scaleY)
    {
        _transform = Matrix3x2.CreateScale(scaleX, scaleY) * _transform;
    }

    public void Rotate(float degrees)
    {
        _transform = Matrix3x2.CreateRotation(degrees * (float)Math.PI / 180f) * _transform;
    }

    public void ClipRectangle(Size size)
    {
    }

    public void DrawRectangle(Position position, Size size, Color color)
    {
        Current.Operations.Add(new RectangleOperation(Resolve(position), size, color, ResolveBounds(position, size)));
    }

    public void DrawRoundedRectangle(Position position, Size size, float cornerRadius, Color color, float strokeWidth = 0f)
    {
        Current.Operations.Add(new RoundedRectangleOperation(Resolve(position), size, cornerRadius, color, strokeWidth, ResolveBounds(position, size)));
    }

    public void DrawLine(Position from, Position to, float thickness, Color color)
    {
        Current.Operations.Add(new LineOperation(Resolve(from), Resolve(to), thickness, color));
    }

    public void DrawText(string text, Position baselineStart, TextStyle style)
    {
        Current.Operations.Add(new TextOperation(Resolve(baselineStart), text, style));
    }

    public void DrawImage(IImage image, Size size)
    {
        Current.Operations.Add(new ImageOperation(Resolve(Position.Zero), size, ResolveBounds(Position.Zero, size)));
    }

    public void DrawExternalLink(string url, Size size)
    {
        Current.Operations.Add(new ExternalLinkOperation(Resolve(Position.Zero), size, url, ResolveBounds(Position.Zero, size)));
    }

    public void DrawInternalLink(string destinationName, Size size)
    {
        Current.Operations.Add(new InternalLinkOperation(Resolve(Position.Zero), size, destinationName, ResolveBounds(Position.Zero, size)));
    }

    public void DrawDestination(string destinationName)
    {
        Current.Operations.Add(new DestinationOperation(Resolve(Position.Zero), destinationName));
    }

    private Position Resolve(Position position)
    {
        Vector2 vector = Vector2.Transform(new Vector2(position.X, position.Y), _transform);
        return new Position(vector.X, vector.Y);
    }

    private Bounds ResolveBounds(Position position, Size size)
    {
        Position position2 = Resolve(position);
        Position position3 = Resolve(position + new Position(size.Width, size.Height));
        return new Bounds(Math.Min(position2.X, position3.X), Math.Min(position2.Y, position3.Y), Math.Max(position2.X, position3.X), Math.Max(position2.Y, position3.Y));
    }

    public void Dispose()
    {
    }
}
