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

using System.Collections.Frozen;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Grammatical inflected forms that <see cref="WordFormInflector"/> can generate. The values
///     are flags because one spelling can carry several forms at once (for example the regular
///     past tense and past participle are both "produced").
/// </summary>
[Flags]
internal enum WordForm
{
    /// <summary>The uninflected dictionary form (not a generated form).</summary>
    None = 0,

    /// <summary>The third-person singular present of a verb, for example "produces".</summary>
    ThirdPersonSingular = 1,

    /// <summary>The simple past tense of a verb, for example "produced".</summary>
    Past = 2,

    /// <summary>The past participle of a verb, for example "produced" or "taken".</summary>
    PastParticiple = 4,

    /// <summary>The -ing form of a verb, for example "producing".</summary>
    Gerund = 8,

    /// <summary>The plural of a noun, for example "boxes".</summary>
    Plural = 16
}

/// <summary>
///     One generated spelling together with the grammatical form(s) it represents.
/// </summary>
/// <param name="Spelling">The lower-case generated spelling (a phrase for a phrasal verb).</param>
/// <param name="Forms">The grammatical form(s) the spelling represents.</param>
internal readonly record struct InflectedSpelling(string Spelling, WordForm Forms);

/// <summary>
///     Generates and applies regular and a small set of irregular English inflections so that
///     <see cref="DictionaryChecker"/> can recognize inflected spellings of a disallowed term
///     and can inflect the suggested alternatives to match.
/// </summary>
/// <remarks>
///     <para>
///     The unit is pure, deterministic and stateless: no regular expressions, no I/O and no
///     shared mutable state, so it is thread-safe. It deliberately errs toward missing a form
///     rather than inventing a wrong one. An unknown irregular verb is inflected as a regular
///     verb, which yields a spelling that real prose never contains (so it never matches), and a
///     spelling is only ever used to look for a disallowed word, never to rewrite the document.
///     </para>
///     <para>
///     Scope: verbs receive third-person singular, past, past participle and -ing forms; for a
///     multi-word (phrasal) verb only the first word, the verb itself, is inflected and the rest
///     of the phrase is appended unchanged. Nouns receive plural forms (single-word nouns only).
///     Adjectives, adverbs and any-role senses are never inflected. The verb "be" is never
///     inflected because its forms (am, is, are, was, were) are too common to match safely.
///     </para>
///     <para>
///     Consonant doubling ("stop" to "stopped") is a heuristic: a final consonant-vowel-consonant
///     sequence doubles for one-syllable words and for words that end in a documented list of
///     stressed endings (see <c>StressedEndings</c>), minus a short list of exceptions.
///     </para>
/// </remarks>
internal static class WordFormInflector
{
    /// <summary>
    ///     Irregular verb forms: the past tense, the past participle(s) and an optional
    ///     third-person singular override.
    /// </summary>
    /// <param name="Past">The simple past tense.</param>
    /// <param name="PastParticiples">One or more past participles, in preference order.</param>
    /// <param name="ThirdPersonForm">The third-person singular form, or <see langword="null"/> for the regular rule.</param>
    private sealed record IrregularVerb(string Past, IReadOnlyList<string> PastParticiples, string? ThirdPersonForm);

    /// <summary>
    ///     Word endings (of the unconditioned base verb) after which a final consonant-vowel-consonant
    ///     doubles even though the word has more than one syllable, because the stress falls on the
    ///     last syllable ("permit" to "permitted").
    /// </summary>
    private static readonly string[] StressedEndings = ["mit", "pel", "fer", "cur", "patrol", "control", "quip", "regret", "begin", "forget"];

    /// <summary>
    ///     Words that match <see cref="StressedEndings"/> but are stressed on an earlier syllable
    ///     and therefore do not double ("offer" to "offered").
    /// </summary>
    private static readonly string[] DoublingExceptions = ["limit", "vomit", "offer", "suffer", "differ", "proffer"];

    /// <summary>
    ///     Documented small irregular verb table, keyed by lower-case base verb.
    /// </summary>
    private static readonly FrozenDictionary<string, IrregularVerb> IrregularVerbs = BuildIrregularVerbs();

    /// <summary>
    ///     Prefixes that form a compound of a table verb ("withstand", "overtake", "mistake",
    ///     "rewrite"); the compound takes the irregular forms of the verb it contains.
    /// </summary>
    private static readonly string[] CompoundVerbPrefixes = ["with", "under", "over", "out", "mis", "up", "fore", "re"];

    /// <summary>
    ///     Words that look like a prefix plus a table verb but are regular verbs ("relay" is not
    ///     "re" plus "lay"), so they are inflected by the regular rules.
    /// </summary>
    private static readonly FrozenSet<string> CompoundExceptions = new[] { "relay" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     The closed list of particles that make a two-word verb term a phrasal verb. Only such a
    ///     term can match with other words between the verb and the particle.
    /// </summary>
    private static readonly string[] PhrasalParticles =
    [
        "up", "down", "out", "off", "on", "in", "over", "away", "back", "through",
        "around", "apart", "together", "forward", "aside", "along", "across", "ahead"
    ];

    /// <summary>
    ///     Documented small irregular noun plural table, keyed by lower-case singular noun.
    /// </summary>
    private static readonly FrozenDictionary<string, string> IrregularPlurals = new Dictionary<string, string>
    {
        ["man"] = "men",
        ["woman"] = "women",
        ["child"] = "children",
        ["person"] = "people",
        ["foot"] = "feet",
        ["tooth"] = "teeth",
        ["mouse"] = "mice",
        ["datum"] = "data",
        ["criterion"] = "criteria",
        ["phenomenon"] = "phenomena"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     The prefixes that may precede a verb in a hyphenated form ("re-perform", "pre-produced"):
    ///     the documented allowlist used by <see cref="DictionaryChecker"/>. Any other prefix
    ///     ("non-", "self-", "in-") is not treated as a form of the verb.
    /// </summary>
    public static IReadOnlyList<string> HyphenPrefixes { get; } = ["re", "pre", "cross", "over", "under", "out", "mis", "co"];

    /// <summary>
    ///     Determines whether a dictionary term is a phrasal verb: a verb sense whose term is exactly
    ///     two words, a word of letters followed by a particle from the documented closed list.
    /// </summary>
    /// <param name="term">The dictionary term.</param>
    /// <param name="pos">The part of speech of the sense that carries the term.</param>
    /// <returns>True when the term is a phrasal verb.</returns>
    public static bool IsPhrasalVerb(string term, PartOfSpeech pos)
    {
        ArgumentNullException.ThrowIfNull(term);
        if (pos != PartOfSpeech.Verb)
        {
            return false;
        }

        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 2
               && IsLetters(words[0].ToLowerInvariant())
               && PhrasalParticles.Contains(words[1].ToLowerInvariant());
    }

    /// <summary>
    ///     Returns <see langword="true"/> when the forms are participial only: past, past participle
    ///     and/or -ing, with no third-person singular or plural bit. Such forms are also commonly
    ///     used as adjectives or nouns, so they need a confident verb role to be flagged.
    /// </summary>
    /// <param name="forms">The form flags.</param>
    /// <returns>True for participial-only forms.</returns>
    public static bool IsParticipial(WordForm forms)
        => forms != WordForm.None
           && (forms & (WordForm.ThirdPersonSingular | WordForm.Plural)) == 0;

    /// <summary>
    ///     Generates every inflected spelling of a dictionary term for a sense's part of speech.
    /// </summary>
    /// <param name="term">The dictionary term (a word, or a phrase whose first word is the verb).</param>
    /// <param name="pos">The part of speech of the sense that carries the term.</param>
    /// <returns>
    ///     The distinct lower-case inflected spellings with their forms, excluding any spelling
    ///     equal to the term itself. Empty when the part of speech is not inflected, when a noun
    ///     term has more than one word, or when the inflected word contains non-letters.
    /// </returns>
    public static IReadOnlyList<InflectedSpelling> Generate(string term, PartOfSpeech pos)
    {
        ArgumentNullException.ThrowIfNull(term);

        // Only nouns and verbs inflect; adjectives, adverbs and any-role senses are left alone.
        if (pos is not (PartOfSpeech.Verb or PartOfSpeech.Noun))
        {
            return [];
        }

        // A phrasal verb inflects its first word only; a multi-word noun is not inflected.
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || (pos == PartOfSpeech.Noun && words.Length > 1))
        {
            return [];
        }

        var head = words[0].ToLowerInvariant();
        if (!IsLetters(head))
        {
            return [];
        }

        var tail = words.Length > 1 ? " " + string.Join(' ', words.Skip(1)).ToLowerInvariant() : string.Empty;
        var forms = pos == PartOfSpeech.Verb ? VerbForms(head) : NounForms(head);

        // Merge identical spellings (past and past participle often coincide) and drop the base.
        var merged = new List<InflectedSpelling>();
        foreach (var (spelling, form) in forms)
        {
            if (spelling == head)
            {
                continue;
            }

            var index = merged.FindIndex(m => m.Spelling == spelling + tail);
            if (index >= 0)
            {
                merged[index] = merged[index] with { Forms = merged[index].Forms | form };
            }
            else
            {
                merged.Add(new InflectedSpelling(spelling + tail, form));
            }
        }

        return merged;
    }

    /// <summary>
    ///     Inflects a replacement alternative into the requested form, when that can be done
    ///     unambiguously.
    /// </summary>
    /// <param name="word">
    ///     The base-form alternative: a word of letters, or for a verb sense a phrasal verb whose
    ///     first word is the verb.
    /// </param>
    /// <param name="forms">The form(s) the flagged word had; all must produce the same spelling.</param>
    /// <param name="pos">The part of speech of the sense the alternative belongs to.</param>
    /// <returns>
    ///     The inflected alternative with the case pattern of the first word of
    ///     <paramref name="word"/> (all capitals stay all capitals, an initial capital stays an
    ///     initial capital), or <see langword="null"/> when it is unsafe to inflect: a multi-word
    ///     noun, a non-letter alternative, a form that does not apply to the part of speech,
    ///     forms that disagree (for example an irregular past and past participle), an ambiguous
    ///     participle, or a noun that already ends in "s".
    /// </returns>
    public static string? Inflect(string word, WordForm forms, PartOfSpeech pos)
    {
        ArgumentNullException.ThrowIfNull(word);

        var words = word.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || (words.Length > 1 && pos != PartOfSpeech.Verb))
        {
            return null;
        }

        // A phrasal-verb alternative inflects its first word only, like a phrasal dictionary term.
        var lower = words[0].ToLowerInvariant();
        if (forms == WordForm.None || !IsLetters(lower) || lower == "be")
        {
            return null;
        }

        // Compute the spelling for every requested form; they must all agree to be usable.
        string? result = null;
        foreach (var form in new[] { WordForm.ThirdPersonSingular, WordForm.Past, WordForm.PastParticiple, WordForm.Gerund, WordForm.Plural })
        {
            if (!forms.HasFlag(form))
            {
                continue;
            }

            var candidate = InflectOne(lower, form, pos);
            if (candidate is null || (result is not null && result != candidate))
            {
                return null;
            }

            result = candidate;
        }

        if (result is null)
        {
            return null;
        }

        var tail = words.Length > 1 ? " " + string.Join(' ', words.Skip(1)) : string.Empty;
        return ApplyCase(words[0], result) + tail;
    }

    /// <summary>
    ///     Inflects one base word into exactly one form for the given part of speech.
    /// </summary>
    private static string? InflectOne(string word, WordForm form, PartOfSpeech pos)
    {
        if (pos == PartOfSpeech.Noun)
        {
            // A noun alternative that already ends in a single "s" is probably a plural or mass
            // noun ("steps", "news"); pluralizing it would invent a word, so it is not safe.
            var alreadyPlural = word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal);
            return form == WordForm.Plural && !alreadyPlural ? Plural(word) : null;
        }

        if (pos != PartOfSpeech.Verb)
        {
            return null;
        }

        var irregular = LookupIrregular(word);
        return form switch
        {
            WordForm.ThirdPersonSingular => irregular?.ThirdPersonForm ?? ThirdPerson(word),
            WordForm.Past => irregular?.Past ?? Ed(word),
            WordForm.PastParticiple => PastParticipleOf(word, irregular),
            WordForm.Gerund => Ing(word),
            _ => null
        };
    }

    /// <summary>
    ///     Finds the irregular forms of a lower-case verb: an exact table hit, or else a compound of
    ///     one documented prefix and a table verb (at least two letters left), so "withstand" takes
    ///     the forms of "stand".
    /// </summary>
    /// <param name="word">The lower-case base verb.</param>
    /// <returns>The irregular forms, or <see langword="null"/> when the verb is regular.</returns>
    private static IrregularVerb? LookupIrregular(string word)
    {
        if (IrregularVerbs.TryGetValue(word, out var exact))
        {
            return exact;
        }

        if (CompoundExceptions.Contains(word))
        {
            return null;
        }

        foreach (var prefix in CompoundVerbPrefixes)
        {
            if (word.Length - prefix.Length >= 2
                && word.StartsWith(prefix, StringComparison.Ordinal)
                && IrregularVerbs.TryGetValue(word[prefix.Length..], out var stem))
            {
                // The third-person form is left to the regular rule: the table override exists
                // only for "have", and a compound of it would not be a word.
                return new IrregularVerb(
                    prefix + stem.Past,
                    stem.PastParticiples.Select(p => prefix + p).ToList(),
                    null);
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns the single past participle of a verb, or <see langword="null"/> when an
    ///     irregular verb has several (for example "got" and "gotten") and none is safe to pick.
    /// </summary>
    private static string? PastParticipleOf(string word, IrregularVerb? irregular)
    {
        if (irregular is null)
        {
            return Ed(word);
        }

        return irregular.PastParticiples.Count == 1 ? irregular.PastParticiples[0] : null;
    }

    /// <summary>
    ///     Lists every verb form of a lower-case base verb.
    /// </summary>
    private static List<(string Spelling, WordForm Form)> VerbForms(string verb)
    {
        var forms = new List<(string, WordForm)>();
        if (verb == "be")
        {
            return forms;
        }

        var irregular = LookupIrregular(verb);
        forms.Add((irregular?.ThirdPersonForm ?? ThirdPerson(verb), WordForm.ThirdPersonSingular));
        if (irregular is null)
        {
            forms.Add((Ed(verb), WordForm.Past | WordForm.PastParticiple));
        }
        else
        {
            forms.Add((irregular.Past, WordForm.Past));
            forms.AddRange(irregular.PastParticiples.Select(p => (p, WordForm.PastParticiple)));
        }

        forms.Add((Ing(verb), WordForm.Gerund));
        return forms;
    }

    /// <summary>
    ///     Lists the plural form of a lower-case singular noun.
    /// </summary>
    private static List<(string Spelling, WordForm Form)> NounForms(string noun)
    {
        return [(Plural(noun), WordForm.Plural)];
    }

    /// <summary>
    ///     Builds the plural of a lower-case singular noun.
    /// </summary>
    private static string Plural(string noun)
    {
        if (IrregularPlurals.TryGetValue(noun, out var irregular))
        {
            return irregular;
        }

        // Latin-derived "-is" nouns ("analysis", "basis") take "-es" in place of "-is".
        return noun.Length > 3 && noun.EndsWith("is", StringComparison.Ordinal)
            ? string.Concat(noun.AsSpan(0, noun.Length - 2), "es")
            : AddS(noun, verb: false);
    }

    /// <summary>
    ///     Builds the third-person singular present of a lower-case base verb.
    /// </summary>
    private static string ThirdPerson(string verb) => AddS(verb, verb: true);

    /// <summary>
    ///     Applies the regular "-s" / "-es" / "-ies" suffix rules shared by verbs and nouns.
    /// </summary>
    /// <param name="word">Lower-case base word.</param>
    /// <param name="verb">
    ///     <see langword="true"/> for a verb, which also takes "-es" after a consonant plus "o"
    ///     ("goes"); a noun such as "photo" takes a plain "-s".
    /// </param>
    private static string AddS(string word, bool verb)
    {
        if (EndsWithAny(word, "s", "x", "z", "ch", "sh"))
        {
            return word + "es";
        }

        if (EndsWithConsonantY(word))
        {
            return word[..^1] + "ies";
        }

        return verb && word.Length > 1 && word[^1] == 'o' && !IsVowel(word[^2]) ? word + "es" : word + "s";
    }

    /// <summary>
    ///     Builds the regular simple past / past participle of a lower-case base verb.
    /// </summary>
    private static string Ed(string verb)
    {
        if (verb.EndsWith('e'))
        {
            return verb + "d";
        }

        if (EndsWithConsonantY(verb))
        {
            return verb[..^1] + "ied";
        }

        return AddSuffixWithSpellingChange(verb, "ed");
    }

    /// <summary>
    ///     Builds the -ing form of a lower-case base verb.
    /// </summary>
    private static string Ing(string verb)
    {
        if (verb.EndsWith("ie", StringComparison.Ordinal))
        {
            return verb[..^2] + "y" + "ing";
        }

        // A silent final "e" is dropped, except after "e", "y" or "o" ("seeing", "dyeing", "toeing").
        if (verb.EndsWith('e') && !EndsWithAny(verb, "ee", "ye", "oe"))
        {
            return verb[..^1] + "ing";
        }

        return AddSuffixWithSpellingChange(verb, "ing");
    }

    /// <summary>
    ///     Appends a vowel-initial suffix to a base verb that does not end in a silent "e",
    ///     applying the "-c" to "-ck" and consonant-doubling spelling changes.
    /// </summary>
    private static string AddSuffixWithSpellingChange(string verb, string suffix)
    {
        if (verb.EndsWith('c'))
        {
            return verb + "k" + suffix;
        }

        return ShouldDoubleFinalConsonant(verb) ? verb + verb[^1] + suffix : verb + suffix;
    }

    /// <summary>
    ///     Applies the consonant-doubling heuristic described in the class remarks.
    /// </summary>
    private static bool ShouldDoubleFinalConsonant(string word)
    {
        if (word.Length < 3 || IsVowel(word[^1]) || word[^1] is 'w' or 'x' or 'y')
        {
            return false;
        }

        // Require a consonant-vowel-consonant ending, where the "u" of "qu" counts as a consonant.
        var middleIsVowel = IsVowel(word[^2]) && !(word[^2] == 'u' && word[^3] == 'q');
        var startIsConsonant = !IsVowel(word[^3]) || (word[^3] == 'u' && word.Length > 3 && word[^4] == 'q');
        if (!middleIsVowel || !startIsConsonant)
        {
            return false;
        }

        if (CountVowelGroups(word) == 1)
        {
            return true;
        }

        return StressedEndings.Any(e => word.EndsWith(e, StringComparison.Ordinal))
               && !DoublingExceptions.Any(e => word.EndsWith(e, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Counts the groups of adjacent vowels, a rough syllable count; a "y" counts as a vowel
    ///     except as the first letter.
    /// </summary>
    private static int CountVowelGroups(string word)
    {
        var groups = 0;
        var inGroup = false;
        for (var i = 0; i < word.Length; i++)
        {
            var vowel = IsVowel(word[i]) || (word[i] == 'y' && i > 0);
            if (vowel && !inGroup)
            {
                groups++;
            }

            inGroup = vowel;
        }

        return groups;
    }

    /// <summary>
    ///     Determines whether a word ends in a consonant followed by "y" ("carry", not "play").
    /// </summary>
    private static bool EndsWithConsonantY(string word)
    {
        return word.Length > 1 && word[^1] == 'y' && !IsVowel(word[^2]);
    }

    /// <summary>
    ///     Determines whether a word ends with any of the given suffixes (ordinal comparison).
    /// </summary>
    private static bool EndsWithAny(string word, params string[] suffixes)
    {
        return suffixes.Any(s => word.EndsWith(s, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Determines whether a lower-case letter is one of the vowels a, e, i, o, u.
    /// </summary>
    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u';

    /// <summary>
    ///     Determines whether a string is non-empty and made only of the letters a to z.
    /// </summary>
    private static bool IsLetters(string text)
    {
        return text.Length > 0 && text.All(c => c is >= 'a' and <= 'z');
    }

    /// <summary>
    ///     Copies the case pattern of the original alternative onto the inflected lower-case word.
    /// </summary>
    private static string ApplyCase(string original, string inflected)
    {
        if (original.Length > 1 && original.All(char.IsUpper))
        {
            return inflected.ToUpperInvariant();
        }

        return char.IsUpper(original[0])
            ? char.ToUpperInvariant(inflected[0]) + inflected[1..]
            : inflected;
    }

    /// <summary>
    ///     Builds the documented irregular verb table.
    /// </summary>
    private static FrozenDictionary<string, IrregularVerb> BuildIrregularVerbs()
    {
        // Each row: base, past, past participle(s) separated by "/". A row whose past equals the
        // base (for example "set") yields no generated past spelling, because the base form is
        // always matched anyway. "have" additionally overrides the third-person form.
        string[] rows =
        [
            "arise,arose,arisen", "awake,awoke,awoken", "bear,bore,borne", "beat,beat,beaten",
            "become,became,become", "begin,began,begun", "bend,bent,bent", "bind,bound,bound",
            "bite,bit,bitten", "blow,blew,blown", "break,broke,broken", "bring,brought,brought",
            "build,built,built", "buy,bought,bought", "catch,caught,caught", "choose,chose,chosen",
            "come,came,come", "cut,cut,cut", "do,did,done", "draw,drew,drawn", "drive,drove,driven",
            "eat,ate,eaten", "fall,fell,fallen", "feed,fed,fed", "feel,felt,felt", "fight,fought,fought",
            "find,found,found", "fly,flew,flown", "forget,forgot,forgotten", "forgive,forgave,forgiven",
            "freeze,froze,frozen", "get,got,got/gotten", "give,gave,given", "go,went,gone",
            "grow,grew,grown", "have,had,had", "hear,heard,heard", "hide,hid,hidden", "hit,hit,hit",
            "hold,held,held", "hurt,hurt,hurt", "keep,kept,kept", "know,knew,known", "lay,laid,laid",
            "lead,led,led", "leave,left,left", "lend,lent,lent", "let,let,let", "lose,lost,lost",
            "make,made,made", "mean,meant,meant", "meet,met,met", "overcome,overcame,overcome",
            "pay,paid,paid", "put,put,put", "read,read,read", "ride,rode,ridden", "ring,rang,rung",
            "rise,rose,risen", "run,ran,run", "say,said,said", "see,saw,seen", "sell,sold,sold",
            "send,sent,sent", "set,set,set", "shake,shook,shaken", "show,showed,shown/showed",
            "shut,shut,shut", "sing,sang,sung", "sit,sat,sat", "speak,spoke,spoken", "spend,spent,spent",
            "stand,stood,stood", "steal,stole,stolen", "swim,swam,swum", "take,took,taken",
            "teach,taught,taught", "tell,told,told", "think,thought,thought", "throw,threw,thrown",
            "undergo,underwent,undergone", "understand,understood,understood", "undertake,undertook,undertaken",
            "wear,wore,worn", "win,won,won", "withdraw,withdrew,withdrawn", "write,wrote,written"
        ];

        var table = new Dictionary<string, IrregularVerb>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var parts = row.Split(',');
            table[parts[0]] = new IrregularVerb(parts[1], parts[2].Split('/'), parts[0] == "have" ? "has" : null);
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
