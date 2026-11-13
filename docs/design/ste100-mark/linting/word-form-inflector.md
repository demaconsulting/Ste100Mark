### WordFormInflector

![Linting Structure](LintingView.svg)

#### Purpose

`WordFormInflector` is a stateless, internal static helper that generates the inflected
spellings of a dictionary term and inflects a replacement word to a given form. It lets
`DictionaryChecker` match "produced" when "produce" is disallowed, and show "made" in the
message instead of "make".

#### Data Model

**WordForm**: a flags enumeration with `None`, `ThirdPersonSingular`, `Past`, `PastParticiple`,
`Gerund`, and `Plural`. Spellings that coincide (for example a regular past and past
participle) are merged into one `InflectedSpelling(Spelling, Forms)` record.

**Irregular verb table**: a small, fixed, documented table (about 85 verbs, for example
"take", "give", "begin", "have") mapping each base to its past, participle, and where
needed its gerund. The verb "be" is never inflected. A small table of irregular noun plurals
(for example "child", "person", "datum") is kept alongside it.

**Regular rules**: verbs take -s, -es (after s, x, z, ch, sh, and o), y to -ies/-ied after a
consonant, -ed/-d, and -ing with final-e dropping. A final consonant is doubled for a
single-syllable consonant-vowel-consonant verb, or for a verb with a known stressed ending
(for example "-mit", "-pel", "-fer"), minus a short exception list (for example "limit",
"offer"). Nouns take -s, -es, or y to -ies; a noun ending in -is takes -es.

#### Key Methods

**Generate**: Returns every inflected spelling for a term and a part of speech.

- *Parameters*: `string term` - dictionary term; `PartOfSpeech pos` - the sense's role.
- *Returns*: `IReadOnlyList<InflectedSpelling>` - empty for Adjective, Adverb, and Any senses.
  For a multi-word term only a verb sense whose phrase is a phrasal verb produces forms, by
  inflecting the head (first) word; a multi-word noun is not inflected.

**Inflect**: Inflects a replacement word to the form(s) of a flagged spelling.

- *Parameters*: `string word` - alternative; `WordForm forms` - the flagged form;
  `PartOfSpeech pos` - the role of the sense.
- *Returns*: `string?` - the inflected word, preserving the case pattern of the input, or
  `null` when it cannot be done safely (an irregular verb outside the table, "be", or a flag
  covering forms that differ for this word). A phrasal-verb alternative inflects its head
  word. A noun alternative that already ends in a plural "s" is left unchanged. The caller
  then shows the base alternative and says so.

#### Error Handling

No exceptions are thrown for valid input; unsupported cases return an empty list or `null`.
