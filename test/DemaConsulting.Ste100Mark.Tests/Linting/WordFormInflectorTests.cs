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

namespace DemaConsulting.Ste100Mark.Tests.Linting;

using PartOfSpeech = DemaConsulting.Ste100Mark.Linting.PartOfSpeech;

/// <summary>
///     Unit tests for the WordFormInflector class.
/// </summary>
public class WordFormInflectorTests
{
    /// <summary>
    ///     Returns the spellings generated for a term as a sorted list for easy comparison.
    /// </summary>
    private static List<string> Spellings(string term, PartOfSpeech pos)
    {
        return WordFormInflector.Generate(term, pos).Select(i => i.Spelling).Order().ToList();
    }

    /// <summary>
    ///     Test that regular and irregular verbs generate their expected forms.
    /// </summary>
    [Theory]
    [InlineData("perform", "performed,performing,performs")]
    [InlineData("produce", "produced,produces,producing")]
    [InlineData("permit", "permits,permitted,permitting")]
    [InlineData("pass", "passed,passes,passing")]
    [InlineData("carry", "carried,carries,carrying")]
    [InlineData("play", "played,playing,plays")]
    [InlineData("stop", "stopped,stopping,stops")]
    [InlineData("plan", "planned,planning,plans")]
    [InlineData("visit", "visited,visiting,visits")]
    [InlineData("offer", "offered,offering,offers")]
    [InlineData("refer", "referred,referring,refers")]
    [InlineData("panic", "panicked,panicking,panics")]
    [InlineData("die", "dies,dying,died")]
    [InlineData("agree", "agreed,agreeing,agrees")]
    [InlineData("see", "seeing,sees,saw,seen")]
    [InlineData("quit", "quits,quit" + "ted,quitting")]
    [InlineData("take", "takes,taking,took,taken")]
    [InlineData("begin", "begins,beginning,began,begun")]
    [InlineData("give", "gives,giving,gave,given")]
    [InlineData("go", "goes,going,went,gone")]
    [InlineData("have", "has,having,had")]
    [InlineData("show", "shows,showing,showed,shown")]
    [InlineData("put", "puts,putting")]
    public void Generate_Verb_ProducesExpectedForms(string verb, string expected)
    {
        // Arrange / Act: generate forms for a verb sense
        var spellings = Spellings(verb, PartOfSpeech.Verb);

        // Assert: the forms match the expected set
        Assert.Equal(expected.Split(',').Order().ToList(), spellings);
    }

    /// <summary>
    ///     Test that a verb whose past and participle coincide carries both forms in one spelling.
    /// </summary>
    [Fact]
    public void Generate_RegularVerbPastEqualsParticiple_MergesForms()
    {
        // Arrange / Act: generate forms for a regular verb
        var forms = WordFormInflector.Generate("produce", PartOfSpeech.Verb);

        // Assert: "produced" is both the past tense and the past participle
        var produced = Assert.Single(forms, f => f.Spelling == "produced");
        Assert.Equal(WordForm.Past | WordForm.PastParticiple, produced.Forms);
    }

    /// <summary>
    ///     Test that noun plurals follow the regular and irregular rules.
    /// </summary>
    [Theory]
    [InlineData("box", "boxes")]
    [InlineData("city", "cities")]
    [InlineData("day", "days")]
    [InlineData("process", "processes")]
    [InlineData("analysis", "analyses")]
    [InlineData("child", "children")]
    [InlineData("photo", "photos")]
    public void Generate_Noun_ProducesPlural(string noun, string expected)
    {
        // Arrange / Act: generate forms for a noun sense
        var spellings = Spellings(noun, PartOfSpeech.Noun);

        // Assert: exactly the plural
        Assert.Equal([expected], spellings);
    }

    /// <summary>
    ///     Test that only the head verb of a phrasal verb inflects.
    /// </summary>
    [Fact]
    public void Generate_PhrasalVerb_InflectsHeadOnly()
    {
        // Arrange / Act: generate forms for a phrasal verb
        var spellings = Spellings("carry out", PartOfSpeech.Verb);

        // Assert: head inflected, particle unchanged
        Assert.Equal(["carried out", "carries out", "carrying out"], spellings);
    }

    /// <summary>
    ///     Test that multi-word nouns, other parts of speech, and non-letter terms are not inflected.
    /// </summary>
    [Fact]
    public void Generate_NotInflectedCases_ReturnEmpty()
    {
        // Arrange / Act / Assert: each unsupported case yields nothing
        Assert.Empty(WordFormInflector.Generate("trail mix", PartOfSpeech.Noun));
        Assert.Empty(WordFormInflector.Generate("quick", PartOfSpeech.Adjective));
        Assert.Empty(WordFormInflector.Generate("quickly", PartOfSpeech.Adverb));
        Assert.Empty(WordFormInflector.Generate("prior to", PartOfSpeech.Any));
        Assert.Empty(WordFormInflector.Generate("e-mail", PartOfSpeech.Verb));
        Assert.Empty(WordFormInflector.Generate("be", PartOfSpeech.Verb));
        Assert.Empty(WordFormInflector.Generate("", PartOfSpeech.Verb));
    }

    /// <summary>
    ///     Test that Inflect preserves the case pattern of the alternative.
    /// </summary>
    [Theory]
    [InlineData("use", "used")]
    [InlineData("USE", "USED")]
    [InlineData("Use", "Used")]
    public void Inflect_CasePattern_IsPreserved(string alternative, string expected)
    {
        // Arrange / Act: inflect to the regular past
        var result = WordFormInflector.Inflect(alternative, WordForm.Past | WordForm.PastParticiple, PartOfSpeech.Verb);

        // Assert: same case pattern
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Test that Inflect handles irregular alternatives and phrasal alternatives.
    /// </summary>
    [Fact]
    public void Inflect_IrregularAndPhrasalAlternatives_Inflect()
    {
        // Arrange / Act / Assert: irregular past, unambiguous participle, phrasal head
        Assert.Equal("TOOK", WordFormInflector.Inflect("TAKE", WordForm.Past, PartOfSpeech.Verb));
        Assert.Equal("taken", WordFormInflector.Inflect("take", WordForm.PastParticiple, PartOfSpeech.Verb));
        Assert.Equal("set up", WordFormInflector.Inflect("set up", WordForm.Past, PartOfSpeech.Verb));
        Assert.Equal("makes sure", WordFormInflector.Inflect("make sure", WordForm.ThirdPersonSingular, PartOfSpeech.Verb));
    }

    /// <summary>
    ///     Test that Inflect returns null whenever inflecting would be unsafe.
    /// </summary>
    [Fact]
    public void Inflect_UnsafeCases_ReturnNull()
    {
        // Arrange / Act / Assert: disagreeing forms, multi-word noun, wrong form for the part of
        // speech, already-plural noun, non-letters, base form, and "be"
        Assert.Null(WordFormInflector.Inflect("give", WordForm.Past | WordForm.PastParticiple, PartOfSpeech.Verb));
        Assert.Null(WordFormInflector.Inflect("get", WordForm.PastParticiple, PartOfSpeech.Verb));
        Assert.Null(WordFormInflector.Inflect("trail mix", WordForm.Plural, PartOfSpeech.Noun));
        Assert.Null(WordFormInflector.Inflect("tool", WordForm.Past, PartOfSpeech.Noun));
        Assert.Null(WordFormInflector.Inflect("tool", WordForm.Plural, PartOfSpeech.Verb));
        Assert.Null(WordFormInflector.Inflect("steps", WordForm.Plural, PartOfSpeech.Noun));
        Assert.Null(WordFormInflector.Inflect("e-mail", WordForm.Past, PartOfSpeech.Verb));
        Assert.Null(WordFormInflector.Inflect("use", WordForm.None, PartOfSpeech.Verb));
        Assert.Null(WordFormInflector.Inflect("be", WordForm.Past, PartOfSpeech.Verb));
        Assert.Null(WordFormInflector.Inflect("fast", WordForm.Past, PartOfSpeech.Adjective));
        Assert.Equal("licence" + "s", WordFormInflector.Inflect("licence", WordForm.Plural, PartOfSpeech.Noun));
    }
}
