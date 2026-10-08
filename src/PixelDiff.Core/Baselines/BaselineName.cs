namespace PixelDiff.Core.Baselines;

/// <summary>
/// Rules for the identifier that names a baseline. Names become file names, so they are validated
/// rather than escaped. Rejecting a bad name outright is safer than sanitising it, because
/// sanitising can quietly collapse two different names onto the same file.
/// </summary>
public static class BaselineName
{
    /// <summary>Longest name accepted, chosen to leave room for suffixes within path limits.</summary>
    public const int MaximumLength = 100;

    /// <summary>
    /// Device names the Windows file system still reserves regardless of extension. Creating one of
    /// these would succeed in managed code and then fail unpredictably elsewhere.
    /// </summary>
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Reports whether a name can be used without throwing.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns>True when the name satisfies every rule.</returns>
    public static bool IsValid(string? name) => Describe(name) is null;

    /// <summary>Throws when a name breaks any rule, naming the rule in the message.</summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="parameterName">Parameter name used in the thrown exception.</param>
    /// <exception cref="ArgumentException">The name cannot be used.</exception>
    public static void Validate(string? name, string parameterName = "name")
    {
        string? problem = Describe(name);
        if (problem is not null)
        {
            throw new ArgumentException(problem, parameterName);
        }
    }

    /// <summary>
    /// Returns the reason a name is unacceptable, or null when it is fine. Kept separate so the
    /// user interface can show the reason without catching an exception.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns>A sentence describing the problem, or null.</returns>
    public static string? Describe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "A baseline name cannot be empty.";
        }

        if (name.Length > MaximumLength)
        {
            return $"A baseline name cannot exceed {MaximumLength} characters.";
        }

        foreach (char character in name)
        {
            if (!IsAllowed(character))
            {
                return $"The character '{character}' is not allowed. Use letters, digits, dot, dash or underscore.";
            }
        }

        if (name[0] is '.' or '-' || name[^1] is '.' or '-')
        {
            return "A baseline name cannot start or end with a dot or a dash.";
        }

        if (name.Contains("..", StringComparison.Ordinal))
        {
            return "A baseline name cannot contain consecutive dots.";
        }

        string stem = name.Split('.', 2)[0];
        if (Array.Exists(ReservedNames, reserved => string.Equals(reserved, stem, StringComparison.OrdinalIgnoreCase)))
        {
            return $"'{stem}' is a name reserved by the operating system.";
        }

        return null;
    }

    /// <summary>
    /// Accepts the ASCII subset that behaves identically on every file system. Letters outside
    /// ASCII are excluded deliberately, because case folding and normalisation of those differ
    /// between platforms and would let two names collide.
    /// </summary>
    private static bool IsAllowed(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.' or '-' or '_';
}
