namespace PixelDiff.Cli;

/// <summary>
/// The parsed command line: a command word followed by <c>--name value</c> option pairs. The
/// surface is small, so parsing is done by hand rather than through a dependency.
/// </summary>
internal sealed class Arguments
{
    /// <summary>Option that selects the suite directory.</summary>
    public const string SuiteOption = "--suite";

    /// <summary>Option that selects a single baseline.</summary>
    public const string NameOption = "--name";

    /// <summary>Option that overrides the run output directory.</summary>
    public const string OutputOption = "--output";

    private readonly Dictionary<string, string> _options;

    private Arguments(string command, Dictionary<string, string> options)
    {
        Command = command;
        _options = options;
    }

    /// <summary>The lower cased command word, or an empty string when none was given.</summary>
    public string Command { get; }

    /// <summary>The suite directory, defaulting to the current directory.</summary>
    public string Suite => _options.GetValueOrDefault(SuiteOption) ?? Directory.GetCurrentDirectory();

    /// <summary>The baseline name restriction, or null when every baseline applies.</summary>
    public string? Name => _options.GetValueOrDefault(NameOption);

    /// <summary>The output directory override, or null to use a generated run directory.</summary>
    public string? Output => _options.GetValueOrDefault(OutputOption);

    /// <summary>
    /// Parses the raw arguments and validates the options against the command that was named.
    /// </summary>
    /// <param name="args">Arguments as passed to the entry point.</param>
    /// <returns>The parsed arguments.</returns>
    /// <exception cref="CliException">An option is unknown, repeated, valueless or not valid for the command.</exception>
    public static Arguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return new Arguments(string.Empty, []);
        }

        string command = args[0].ToLowerInvariant();
        HashSet<string> allowed = AllowedOptions(command);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int index = 1; index < args.Count; index++)
        {
            string option = args[index];

            if (!allowed.Contains(option))
            {
                throw CliException.Usage($"Option '{option}' is not valid for the '{command}' command.");
            }

            if (index + 1 >= args.Count)
            {
                throw CliException.Usage($"Option '{option}' requires a value.");
            }

            string value = args[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw CliException.Usage($"Option '{option}' requires a non empty value.");
            }

            if (!options.TryAdd(option, value))
            {
                throw CliException.Usage($"Option '{option}' was given more than once.");
            }
        }

        if (command == "approve" && !options.ContainsKey(NameOption))
        {
            throw CliException.Usage($"The 'approve' command requires {NameOption}.");
        }

        return new Arguments(command, options);
    }

    private static HashSet<string> AllowedOptions(string command) => command switch
    {
        "run" => [SuiteOption, NameOption, OutputOption],
        "approve" => [SuiteOption, NameOption],
        "list" => [SuiteOption],
        _ => [],
    };
}
