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

using DemaConsulting.Ste100Mark.Linting;
using DictionaryChecker = DemaConsulting.Ste100Mark.Tests.Linting.TestDictionaryChecker;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     A ~100-sentence, hand-written corpus measuring <see cref="PartOfSpeechResolver"/>'s
///     false-positive rate against genuinely dual-class English noun/verb homographs (words with
///     the identical spelling in both roles, for example "test"/"TEST") - the same dictionary
///     shape that produced the reported false-positive pattern against a real, commercially
///     licensed ASD-STE100 Issue 9 dictionary.
/// </summary>
/// <remarks>
///     Ste100Mark's own embedded illustrative dictionary
///     (<c>src/DemaConsulting.Ste100Mark/Linting/DefaultDictionary.yaml</c>) cannot exercise this
///     failure mode: it is a plain-English synonym-preference list of single-role Latinate words
///     (<c>utilize</c>, <c>commence</c>, <c>component</c>, <c>duration</c>, ...) that have no
///     natural opposite-role usage in English at all - there is no legal sentence that uses
///     "component" as a verb or "utilize" as a noun. Every row here instead builds a small,
///     self-contained, single-term dictionary shaped like a real dual-class entry (for example
///     <c>test (verb) -&gt; TEST</c>) and checks that using the word in its legal, non-restricted
///     grammatical role produces zero <c>STE100-DICT</c> findings. Each row is evaluated against
///     its own isolated dictionary (<see cref="DictionaryConfig.UseEmbedded"/> disabled) so
///     sentences are free to contain ordinary English words without accidentally tripping an
///     unrelated embedded-dictionary entry.
///
///     A failure here is a genuine <see cref="PartOfSpeechResolver"/> false positive on a realistic
///     sentence, not noise - use failures to adjust the resolver's confidence threshold or document a limitation,
///     rather than treating every diagnostic as equally suspect.
/// </remarks>
public class PartOfSpeechFalsePositiveCorpusTests
{
    /// <summary>
    ///     70 verb-only-disallowed dual-class words, each with a sentence using the word in its
    ///     legal noun role. Sentences deliberately vary the noun-signal shape exercised (article,
    ///     possessive, quantifier, preposition, "of"-continuation) rather than repeating one
    ///     template, so the corpus reflects the range of real prose rather than one pattern.
    /// </summary>
    public static IEnumerable<object[]> LegalNounUsageCases()
    {
        (string Term, string Sentence)[] cases =
        [
            ("test", "Record the test on the checklist."),
            ("check", "The check indicated no faults."),
            ("control", "Set the control to the neutral position."),
            ("cycle", "The cycle of the compressor took two minutes."),
            ("filter", "Replace the filter after every service."),
            ("record", "Attach the record to the work order."),
            ("load", "The load of the trailer exceeded the rated limit."),
            ("signal", "The signal from the sensor was steady."),
            ("flag", "Raise the flag if a fault occurs."),
            ("monitor", "Connect the monitor to the display port."),
            ("mount", "Tighten the mount before operation."),
            ("seal", "Inspect the seal for damage."),
            ("clamp", "Release the clamp before removal."),
            ("latch", "Check the latch for free movement."),
            ("gauge", "Read the gauge before startup."),
            ("sample", "Send the sample to the laboratory."),
            ("scan", "The scan showed no defects."),
            ("print", "Attach the print to the report."),
            ("copy", "File the copy in the binder."),
            ("code", "Enter the code on the keypad."),
            ("plot", "Review every plot before sign-off."),
            ("trace", "The trace on the display was smooth."),
            ("stamp", "Check the stamp on the certificate."),
            ("mark", "The mark on the housing shows the batch number."),
            ("grade", "Confirm the grade of the material."),
            ("rank", "Note the technician's rank on the form."),
            ("form", "Complete the form before submission."),
            ("shape", "Inspect the shape of the bracket."),
            ("cast", "Examine the cast for cracks."),
            ("pack", "Open the pack before use."),
            ("wrap", "Remove the wrap from the assembly."),
            ("tape", "Apply the tape around the joint."),
            ("bolt", "Torque the bolt to the specified value."),
            ("screw", "Remove the screw from the panel."),
            ("hook", "Attach the hook to the bracket."),
            ("pin", "Insert the pin into the hole."),
            ("plug", "Remove the plug before draining."),
            ("patch", "Apply the patch to the surface."),
            ("guard", "Install the guard before operation."),
            ("block", "Remove the block from the wheel."),
            ("brace", "Fit the brace to the frame."),
            ("brake", "Inspect the brake before departure."),
            ("clip", "Remove the clip from the harness."),
            ("crack", "The crack in the panel was visible."),
            ("dent", "Note the dent on the inspection sheet."),
            ("chip", "Remove any chip from the surface."),
            ("bend", "Measure the bend in the tube."),
            ("twist", "Check the twist in the cable."),
            ("spin", "The spin of the rotor was smooth."),
            ("roll", "Inspect the roll of material for tears."),
            ("slide", "Lubricate the slide before assembly."),
            ("brush", "Replace the brush in the motor."),
            ("sweep", "Complete the sweep of the area."),
            ("wipe", "Use a clean wipe on the lens."),
            ("rub", "Check for a rub mark on the belt."),
            ("polish", "Apply the polish to the surface."),
            ("coat", "Inspect the coat for uniform coverage."),
            ("finish", "Check the finish for blemishes."),
            ("cover", "Replace the cover after inspection."),
            ("cap", "Remove the cap before filling."),
            ("lock", "Engage the lock before transport."),
            ("key", "Turn the key to the on position."),
            ("handle", "Grip the handle firmly."),
            ("grip", "Check the grip on the tool."),
            ("hold", "Release the hold before moving the unit."),
            ("balance", "Check the balance of the wheel."),
            ("count", "Record the count on the log sheet."),
            ("rate", "The rate on the gauge was steady."),
            ("score", "Note the score on the sheet."),
            ("report", "File the report with the supervisor.")
        ];

        foreach (var (term, sentence) in cases)
        {
            yield return [term, sentence];
        }
    }

    /// <summary>
    ///     30 noun-only-disallowed dual-class words, each with a sentence using the word in its
    ///     legal verb role. Sentences vary the verb-signal shape exercised (imperative sentence
    ///     start, infinitive marker, modal auxiliary) rather than repeating one template.
    /// </summary>
    public static IEnumerable<object[]> LegalVerbUsageCases()
    {
        (string Term, string Sentence)[] cases =
        [
            ("increase", "Increase the pressure gradually."),
            ("decrease", "Decrease the flow rate slowly."),
            ("access", "Technicians must access the panel from the rear."),
            ("process", "The operator will process the batch tomorrow."),
            ("damage", "Take care not to damage the coating during removal."),
            ("wear", "Continued use can wear the bearing surface."),
            ("repair", "Send the unit to the depot to repair the fault."),
            ("return", "Plan to return the unit to service after testing."),
            ("exchange", "Plan to exchange the sensor before the next test."),
            ("release", "Continue to release the pressure slowly."),
            ("request", "Plan to request approval before the change."),
            ("demand", "The load may demand additional current during startup."),
            ("offer", "The supplier will offer a replacement part."),
            ("supply", "The pump will supply coolant to the engine."),
            ("delay", "Plan to delay the shipment until parts arrive."),
            ("advance", "Plan to advance the schedule by one week."),
            ("display", "The system will display the fault code."),
            ("review", "The engineer will review the drawing tomorrow."),
            ("audit", "Plan to audit the process before the release."),
            ("design", "Plan to design the fixture before manufacturing."),
            ("model", "Engineers will model the airflow before testing."),
            ("sketch", "Plan to sketch the layout before cutting metal."),
            ("draft", "Plan to draft the procedure before the review."),
            ("exercise", "Plan to exercise the valve weekly to prevent seizure."),
            ("transfer", "Plan to transfer the data before shutdown."),
            ("support", "The bracket will support the load during transport."),
            ("contact", "Plan to contact the supplier before ordering parts."),
            ("document", "Plan to document the results after each run."),
            ("quote", "Plan to quote the customer before month end."),
            ("stage", "Plan to stage the components before assembly.")
        ];

        foreach (var (term, sentence) in cases)
        {
            yield return [term, sentence];
        }
    }

    /// <summary>
    ///     Test that a dual-class word disallowed only as a verb (for example
    ///     <c>test (v) -&gt; TEST</c>) is not flagged when the sentence uses it in its legal noun
    ///     role.
    /// </summary>
    [Theory]
    [MemberData(nameof(LegalNounUsageCases))]
    public void Evaluate_LegalNounUsage_VerbOnlyDisallowedEntry_NotFlagged(string term, string sentence)
    {
        // Arrange: a self-contained dictionary disallowing only the verb sense of the term
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                UseEmbedded = false,
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    [term] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = [term.ToUpperInvariant()] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment(sentence, 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary);

        // Assert: the real resolver identifies the noun role and the legal usage produces no finding
        Assert.Equal(PartOfSpeech.Noun, ResolveRole(term, sentence));
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a dual-class word disallowed only as a noun is not flagged when the sentence
    ///     uses it in its legal verb role.
    /// </summary>
    [Theory]
    [MemberData(nameof(LegalVerbUsageCases))]
    public void Evaluate_LegalVerbUsage_NounOnlyDisallowedEntry_NotFlagged(string term, string sentence)
    {
        // Arrange: a self-contained dictionary disallowing only the noun sense of the term
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                UseEmbedded = false,
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    [term] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = [term.ToUpperInvariant()] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment(sentence, 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary);

        // Assert: the real resolver identifies the verb role and the legal usage produces no finding
        Assert.Equal(PartOfSpeech.Verb, ResolveRole(term, sentence));
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Resolves the role of the first occurrence of a term in a sentence using the real resolver.
    /// </summary>
    private static PartOfSpeech? ResolveRole(string term, string sentence) =>
        TestResolvers.Basic.Resolve(
            sentence, sentence.IndexOf(term, StringComparison.OrdinalIgnoreCase), term.Length);
}
