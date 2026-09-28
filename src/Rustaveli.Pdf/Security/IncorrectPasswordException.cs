namespace Rustaveli.Pdf;

/// <summary>Thrown when a protected PDF is opened without the password it needs, or with a wrong one.</summary>
public sealed class IncorrectPasswordException : Exception
{
    public IncorrectPasswordException(string message)
        : base(message)
    {
    }
}
