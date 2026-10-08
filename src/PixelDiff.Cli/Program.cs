using System.Text.Json;
using OpenCvSharp;
using PixelDiff.Capture.Windows;
using PixelDiff.Core.Baselines;
using PixelDiff.Core.Comparison;

namespace PixelDiff.Cli;

/// <summary>Entry point. Dispatches the command and converts failures into exit codes.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Dispatch(args);
        }
        catch (CliException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return error.ExitCode;
        }
        catch (Exception error) when (error is BaselineNotFoundException
            or ScreenCaptureException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException
            or JsonException
            or OpenCVException)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return ExitCodes.RuntimeError;
        }
#pragma warning disable CA1031 // The process boundary must not leak an unhandled exception to the runtime or a build server.
        catch (Exception error)
#pragma warning restore CA1031
        {
            Console.Error.WriteLine($"error: unexpected {error.GetType().Name}: {error.Message}");
            return ExitCodes.RuntimeError;
        }
    }

    private static int Dispatch(string[] args)
    {
        Arguments arguments;
        try
        {
            arguments = Arguments.Parse(args);
        }
        catch (CliException error) when (error.ExitCode == ExitCodes.UsageError)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            Console.Error.WriteLine(HelpText.Usage);
            return ExitCodes.UsageError;
        }

        switch (arguments.Command)
        {
            case "help":
                Console.Out.WriteLine(HelpText.Usage);
                return ExitCodes.Success;

            case "run" or "list" or "approve":
                return Execute(arguments);

            default:
                Console.Error.WriteLine(arguments.Command.Length == 0
                    ? "error: no command given."
                    : $"error: unknown command '{arguments.Command}'.");
                Console.Error.WriteLine(HelpText.Usage);
                return ExitCodes.UsageError;
        }
    }

    private static int Execute(Arguments arguments)
    {
        string suite = Path.GetFullPath(arguments.Suite);

        // The store creates its directory on construction, so existence is checked first to avoid
        // silently manufacturing an empty suite from a mistyped path.
        if (!Directory.Exists(suite))
        {
            throw CliException.Runtime($"Suite directory '{suite}' does not exist.");
        }

        var store = new FileSystemBaselineStore(suite);
        var comparer = new PixelImageComparer();
        var capture = new WindowsScreenCapture();

        return arguments.Command switch
        {
            "run" => new RunCommand(store, comparer, capture).Execute(arguments.Name, arguments.Output, Console.Out),
            "list" => new ListCommand(store).Execute(Console.Out),
            _ => new ApproveCommand(store, capture).Execute(arguments.Name!, Console.Out),
        };
    }
}
