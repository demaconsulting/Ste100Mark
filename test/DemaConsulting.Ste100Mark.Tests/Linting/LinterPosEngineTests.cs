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

using DemaConsulting.Ste100Mark.Cli;
using DemaConsulting.WordSense;
using Linter = DemaConsulting.Ste100Mark.Tests.Linting.TestLinter;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for part-of-speech engine selection in the Linter: precedence, invalid values,
///     lazy model preparation, download failure and disposal. All tests inject fake models, so none
///     downloads anything.
/// </summary>
[Collection("Sequential")]
public sealed class LinterPosEngineTests : IDisposable
{
    /// <summary>A document with several dictionary matches that need part-of-speech resolution.</summary>
    private const string HitDocument = "# Title\n\nThe impact was large.\n\nThe impact was small.\n\nThe impact was new.\n";

    /// <summary>A document with no dictionary match.</summary>
    private const string CleanDocument = "# Title\n\nOpen the panel.\n";

    private readonly string _originalDirectory;
    private readonly DirectoryInfo _tempDirectory;

    /// <summary>
    ///     Creates an isolated temporary working directory and makes it current for the test.
    /// </summary>
    public LinterPosEngineTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDirectory = Directory.CreateTempSubdirectory("ste100mark-pos-engine-test-");
        Directory.SetCurrentDirectory(_tempDirectory.FullName);
    }

    /// <summary>
    ///     Restores the original current directory and removes the temporary directory.
    /// </summary>
    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        _tempDirectory.Delete(true);
    }

    /// <summary>
    ///     Runs the linter with the given arguments and factory, capturing output and the exit code.
    /// </summary>
    private static (int ExitCode, string Output, string Error) Run(
        string[] args, Func<PartOfSpeechEngine, IWordSenseModel> factory)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            using var context = Context.Create(args);
            Linter.Run(context, factory);
            return (context.ExitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Writes the document and, optionally, a configuration file.
    /// </summary>
    private void Arrange(string document, string? yaml = null)
    {
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "doc.md"), document);
        if (yaml is not null)
        {
            File.WriteAllText(Path.Combine(_tempDirectory.FullName, ".ste100mark.yaml"), yaml);
        }
    }

    /// <summary>
    ///     Test that the built-in default engine is minilm when nothing selects one.
    /// </summary>
    [Fact]
    public void Run_NoEngineSelected_RequestsMiniLm()
    {
        // Arrange: no CLI flag, no configuration value
        Arrange(CleanDocument);
        var requested = new List<PartOfSpeechEngine>();

        // Act: run the linter
        Run(["doc.md"], e =>
        {
            requested.Add(e);
            return new PartOfSpeechResolverTests.FakeDownloadableModel();
        });

        // Assert: the default engine was requested exactly once
        Assert.Equal([PartOfSpeechEngine.MiniLm], requested);
    }

    /// <summary>
    ///     Test that the configuration value is used when the command line does not select an engine.
    /// </summary>
    [Fact]
    public void Run_YamlEngineOnly_RequestsYamlEngine()
    {
        // Arrange: configuration selects bert
        Arrange(CleanDocument, "dictionary:\n  pos-engine: bert\n");
        var requested = new List<PartOfSpeechEngine>();

        // Act: run the linter
        Run(["doc.md"], e =>
        {
            requested.Add(e);
            return new PartOfSpeechResolverTests.FakeDownloadableModel();
        });

        // Assert: bert was requested
        Assert.Equal([PartOfSpeechEngine.Bert], requested);
    }

    /// <summary>
    ///     Test that the command line wins over the configuration file.
    /// </summary>
    [Fact]
    public void Run_CliAndYamlEngine_CliWins()
    {
        // Arrange: configuration selects bert, the command line selects basic
        Arrange(CleanDocument, "dictionary:\n  pos-engine: bert\n");
        var requested = new List<PartOfSpeechEngine>();

        // Act: run the linter
        Run(["doc.md", "--pos-engine", "basic"], e =>
        {
            requested.Add(e);
            return new BasicWordSenseModel();
        });

        // Assert: the command-line engine was requested
        Assert.Equal([PartOfSpeechEngine.Basic], requested);
    }

    /// <summary>
    ///     Test that an invalid configuration value fails the run with a clear error and no model request.
    /// </summary>
    [Fact]
    public void Run_InvalidYamlEngine_FailsWithClearError()
    {
        // Arrange: configuration with an unknown engine
        Arrange(CleanDocument, "dictionary:\n  pos-engine: gpt\n");
        var requested = 0;

        // Act: run the linter
        var (exitCode, _, error) = Run(["doc.md"], _ =>
        {
            requested++;
            return new BasicWordSenseModel();
        });

        // Assert: non-zero exit code, clear message, no model created
        Assert.NotEqual(0, exitCode);
        Assert.Contains("dictionary.pos-engine", error);
        Assert.Contains("gpt", error);
        Assert.Equal(0, requested);
    }

    /// <summary>
    ///     Test that a download failure fails the run with a clear error and no silent fallback.
    /// </summary>
    [Fact]
    public void Run_DownloadFails_FailsWithClearErrorAndNoFallback()
    {
        // Arrange: a downloadable model whose download fails
        Arrange(HitDocument);
        var model = new PartOfSpeechResolverTests.FakeDownloadableModel(new ModelUnavailableException("network down"));

        // Act: run the linter
        var (exitCode, output, error) = Run(["doc.md", "--pos-engine", "minilm"], _ => model);

        // Assert: non-zero exit code, clear message, no diagnostics produced from a fallback engine
        Assert.NotEqual(0, exitCode);
        Assert.Contains("minilm", error);
        Assert.Contains("could not be prepared", error);
        Assert.Contains("network down", error);
        Assert.DoesNotContain("STE100-DICT", output);
        Assert.Equal(1, model.EnsureCalls);
        Assert.Equal(0, model.ClassifyCalls);
        Assert.True(model.Disposed);
    }

    /// <summary>
    ///     Test that the model is prepared exactly once however many dictionary matches need it.
    /// </summary>
    [Fact]
    public void Run_ManyDictionaryHits_EnsuresModelOnce()
    {
        // Arrange: three matches in separate sentences
        Arrange(HitDocument);
        var model = new PartOfSpeechResolverTests.FakeDownloadableModel();

        // Act: run the linter
        var (_, _, _) = Run(["doc.md", "--pos-engine", "minilm"], _ => model);

        // Assert: one preparation, several classifications
        Assert.Equal(1, model.EnsureCalls);
        Assert.True(model.ClassifyCalls >= 1);
    }

    /// <summary>
    ///     Test that no download is requested when no dictionary match needs resolution.
    /// </summary>
    [Fact]
    public void Run_NoDictionaryHits_DoesNotEnsureModel()
    {
        // Arrange: a document without dictionary matches
        Arrange(CleanDocument);
        var model = new PartOfSpeechResolverTests.FakeDownloadableModel();

        // Act: run the linter
        var (exitCode, _, _) = Run(["doc.md", "--pos-engine", "minilm"], _ => model);

        // Assert: success without any download; the model is still disposed
        Assert.Equal(0, exitCode);
        Assert.Equal(0, model.EnsureCalls);
        Assert.True(model.Disposed);
    }

    /// <summary>
    ///     Test that a disabled dictionary check never creates a model.
    /// </summary>
    [Fact]
    public void Run_DictionaryDisabled_DoesNotCreateModel()
    {
        // Arrange: dictionary check disabled
        Arrange(HitDocument, "dictionary:\n  enabled: false\n");
        var requested = 0;

        // Act: run the linter
        var (exitCode, _, _) = Run(["doc.md"], _ =>
        {
            requested++;
            return new PartOfSpeechResolverTests.FakeDownloadableModel();
        });

        // Assert: no model was requested
        Assert.Equal(0, exitCode);
        Assert.Equal(0, requested);
    }

    /// <summary>
    ///     Test that an empty file selection does not download a model.
    /// </summary>
    [Fact]
    public void Run_NoFiles_DoesNotEnsureModel()
    {
        // Arrange: no files match
        var model = new PartOfSpeechResolverTests.FakeDownloadableModel();

        // Act: run the linter
        Run(["no-such-file-*.md", "--allow-empty"], _ => model);

        // Assert: nothing was downloaded
        Assert.Equal(0, model.EnsureCalls);
    }
}
