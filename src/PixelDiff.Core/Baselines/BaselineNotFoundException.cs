namespace PixelDiff.Core.Baselines;

/// <summary>
/// Thrown when a baseline is requested by a name nothing is stored under. Distinct from
/// <see cref="FileNotFoundException"/> so a caller can tell a missing baseline apart from a
/// storage location that has gone away entirely.
/// </summary>
public sealed class BaselineNotFoundException : Exception
{
    /// <summary>Creates the exception for a named baseline.</summary>
    /// <param name="name">The baseline that could not be found.</param>
    public BaselineNotFoundException(string name)
        : base($"No baseline is stored under the name '{name}'.")
    {
        Name = name;
    }

    /// <summary>Creates the exception with a custom message.</summary>
    /// <param name="name">The baseline that could not be found.</param>
    /// <param name="message">The message to report.</param>
    public BaselineNotFoundException(string name, string message)
        : base(message)
    {
        Name = name;
    }

    /// <summary>Creates the exception with a custom message and an underlying cause.</summary>
    /// <param name="name">The baseline that could not be found.</param>
    /// <param name="message">The message to report.</param>
    /// <param name="innerException">The cause.</param>
    public BaselineNotFoundException(string name, string message, Exception innerException)
        : base(message, innerException)
    {
        Name = name;
    }

    /// <summary>The baseline name that was requested.</summary>
    public string Name { get; }
}
