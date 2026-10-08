namespace PixelDiff.Cli;

/// <summary>The usage text printed by the help command and after a usage error.</summary>
internal static class HelpText
{
    /// <summary>The complete help text.</summary>
    public const string Usage = """
        pixeldiff - visual regression testing for screen captures

        Usage:
          pixeldiff run     [--suite <dir>] [--name <baseline>] [--output <dir>]
          pixeldiff list    [--suite <dir>]
          pixeldiff approve --name <baseline> [--suite <dir>]
          pixeldiff help

        Commands:
          run      Recapture every baseline, compare it and write report.json. Candidate and
                   diff images are written for each comparison that does not match.
          list     Show every baseline with its recorded size and capture target.
          approve  Recapture one baseline and store it as the new approved image.
          help     Show this text.

        Options:
          --suite   Suite directory. Defaults to the current directory.
          --name    Restrict the command to a single baseline.
          --output  Directory for run artifacts. Defaults to a timestamped directory under the suite.

        Exit codes:
          0  Every comparison matched, or a non comparison command succeeded.
          1  At least one comparison did not match.
          2  The arguments were wrong or incomplete.
          3  The command failed at runtime, for example a window was not found.
        """;
}
