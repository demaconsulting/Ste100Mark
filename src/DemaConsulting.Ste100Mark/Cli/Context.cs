// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace DemaConsulting.Ste100Mark.Cli;

/// <summary>
///     Context class that handles command-line arguments and program output.
/// </summary>
internal sealed class Context : IDisposable
{
    /// <summary>
    ///     Log file stream writer (if logging is enabled).
    /// </summary>
    private StreamWriter? _logWriter;

    /// <summary>
    ///     Indicates whether errors have been reported.
    /// </summary>
    private bool _hasErrors;

    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; private init; }

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; private init; }

    /// <summary>
    ///     Gets the heading depth for markdown output (default is 1).
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the positional glob patterns supplied on the command line.
    /// </summary>
    /// <remarks>
    ///     These are the non-flag tokens (tokens that do not begin with <c>-</c>) collected from
    ///     the argument list, in the order they were supplied. When empty, the linter falls back
    ///     to the <c>include</c>/<c>exclude</c> patterns from the resolved lint configuration file.
    /// </remarks>
    public IReadOnlyList<string> Globs { get; private init; } = [];

    /// <summary>
    ///     Gets the path to the lint configuration file supplied via <c>--config</c>, or
    ///     <see langword="null"/> when not specified.
    /// </summary>
    /// <remarks>
    ///     When <see langword="null"/>, the linter looks for <c>.ste100mark.yaml</c> in the
    ///     current working directory and proceeds with built-in defaults if that file is absent.
    /// </remarks>
    public string? ConfigFile { get; private init; }

    /// <summary>
    ///     Gets the diagnostic output format selected via <c>--format</c> (default is
    ///     <see cref="OutputFormat.Text"/>).
    /// </summary>
    public OutputFormat Format { get; private init; } = OutputFormat.Text;

    /// <summary>
    ///     Gets a value indicating whether the strict flag was specified.
    /// </summary>
    /// <remarks>
    ///     When <see langword="true"/>, the linter promotes all <c>warn</c>-severity findings to
    ///     <c>error</c> severity for exit-code purposes only; reported severities are unaffected.
    /// </remarks>
    public bool Strict { get; private init; }

    /// <summary>
    ///     Gets the proposed exit code for the application (0 for success, 1 for errors).
    /// </summary>
    public int ExitCode => _hasErrors ? 1 : 0;

    /// <summary>
    ///     Private constructor - use Create factory method instead.
    /// </summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a Context instance from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new Context instance.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        var result = new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            Validate = parser.Validate,
            ResultsFile = parser.ResultsFile,
            HeadingDepth = parser.HeadingDepth,
            Globs = parser.Globs,
            ConfigFile = parser.ConfigFile,
            Format = parser.Format,
            Strict = parser.Strict
        };

        // Open log file if specified
        if (parser.LogFile != null)
        {
            result.OpenLogFile(parser.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Opens the log file for writing
    /// </summary>
    /// <param name="logFile">Log file path</param>
    private void OpenLogFile(string logFile)
    {
        try
        {
            // Open with AutoFlush enabled so log entries are immediately written to disk
            // even if the application terminates unexpectedly before Dispose is called
            _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Generic catch is justified here to wrap any file system exception with context.
        // Expected exceptions include IOException, UnauthorizedAccessException, ArgumentException,
        // NotSupportedException, and other file system-related exceptions.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Helper class for parsing command-line arguments
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>
        ///     Gets a value indicating whether the version flag was specified.
        /// </summary>
        public bool Version { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the help flag was specified.
        /// </summary>
        public bool Help { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the silent flag was specified.
        /// </summary>
        public bool Silent { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the validate flag was specified.
        /// </summary>
        public bool Validate { get; private set; }

        /// <summary>
        ///     Gets the log file path.
        /// </summary>
        public string? LogFile { get; private set; }

        /// <summary>
        ///     Gets the validation results file path.
        /// </summary>
        public string? ResultsFile { get; private set; }

        /// <summary>
        ///     Gets the heading depth for markdown output.
        /// </summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>
        ///     Gets the positional glob patterns collected from non-flag arguments.
        /// </summary>
        public List<string> Globs { get; } = [];

        /// <summary>
        ///     Gets the lint configuration file path.
        /// </summary>
        public string? ConfigFile { get; private set; }

        /// <summary>
        ///     Gets the diagnostic output format.
        /// </summary>
        public OutputFormat Format { get; private set; } = OutputFormat.Text;

        /// <summary>
        ///     Gets a value indicating whether the strict flag was specified.
        /// </summary>
        public bool Strict { get; private set; }

        /// <summary>
        ///     Parses command-line arguments
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public void ParseArguments(string[] args)
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Parses a single argument
        /// </summary>
        /// <param name="arg">Argument to parse</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Updated index</returns>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--log":
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    HeadingDepth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1, 6);
                    return index + 1;

                case "--config":
                    ConfigFile = GetRequiredStringArgument(arg, args, index, "a lint configuration file argument");
                    return index + 1;

                case "--format":
                    Format = GetRequiredFormatArgument(arg, args, index);
                    return index + 1;

                case "--strict":
                    Strict = true;
                    return index;

                default:
                    // Unrecognized tokens beginning with '-' are unsupported flags and are rejected
                    // outright. Tokens that do not begin with '-' are treated as positional glob
                    // patterns for the linter (e.g. "docs/**/*.md") rather than an error, since the
                    // linter accepts zero or more glob arguments in place of the configured
                    // include/exclude patterns.
                    if (arg.StartsWith('-'))
                    {
                        throw new ArgumentException($"Unsupported argument '{arg}'", nameof(args));
                    }

                    Globs.Add(arg);
                    return index;
            }
        }

        /// <summary>
        ///     Gets a required output-format argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Parsed <see cref="OutputFormat"/> value</returns>
        private static OutputFormat GetRequiredFormatArgument(string arg, string[] args, int index)
        {
            var value = GetRequiredStringArgument(arg, args, index, "a format argument (text or json)");
            return value.ToLowerInvariant() switch
            {
                "text" => OutputFormat.Text,
                "json" => OutputFormat.Json,
                _ => throw new ArgumentException($"{arg} requires 'text' or 'json', got '{value}'", nameof(args))
            };
        }

        /// <summary>
        ///     Gets a required string argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <returns>Argument value</returns>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            return args[index];
        }

        /// <summary>
        ///     Gets a required integer argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <param name="min">Minimum valid value (inclusive)</param>
        /// <param name="max">Maximum valid value (inclusive)</param>
        /// <returns>Argument value as an integer in [min, max]</returns>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min = 1, int max = int.MaxValue)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!int.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }
    }

    /// <summary>
    ///     Writes a line of output to the console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Output is written to stdout. When <see cref="Silent"/> is <c>true</c>, stdout output is
    ///     suppressed, but the message is still written to the log file when one is open.
    /// </remarks>
    public void WriteLine(string message)
    {
        // Write to console unless silent mode is enabled
        if (!Silent)
        {
            Console.WriteLine(message);
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Writes an error message to the error console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     <c>_hasErrors</c> is set to <c>true</c> unconditionally, so <see cref="ExitCode"/> will
    ///     return 1 regardless of whether <see cref="Silent"/> suppresses the console output.
    ///     Stderr output is suppressed when <see cref="Silent"/> is <c>true</c>, but the message
    ///     is still written to the log file when one is open.
    /// </remarks>
    public void WriteError(string message)
    {
        // Mark that we have encountered errors
        _hasErrors = true;

        // Write to error console unless silent mode is enabled
        if (!Silent)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Marks that a failure has occurred without emitting any console or log output.
    /// </summary>
    /// <remarks>
    ///     Callers that already reported the failure through a different, self-contained output
    ///     channel (for example, a single buffered JSON document written via <see cref="WriteLine"/>)
    ///     use this method to drive <see cref="ExitCode"/> to 1 without corrupting that document
    ///     with an interleaved error line. Prefer <see cref="WriteError"/> whenever a
    ///     human-readable error line is also desired.
    /// </remarks>
    internal void MarkFailure()
    {
        _hasErrors = true;
    }

    /// <summary>
    ///     Disposes resources used by the Context.
    /// </summary>
    public void Dispose()
    {
        // Close and dispose the log file writer if it exists
        _logWriter?.Dispose();
        _logWriter = null;
    }
}
