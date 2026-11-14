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

**IsParticipial**: classifies a `WordForm` value as participial (Past, PastParticiple, and/or
Gerund with no ThirdPersonSingular or Plural bit). The form kind already travels on each
generated spelling, so no data-model field is added; `DictionaryChecker` uses this to require
a confident verb role for participial matches.

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
  `null` when it cannot be done safely: "be", a multi-word noun, a non-letter alternative, a
  form that does not apply to the part of speech, merged forms whose spellings disagree (for
  example an irregular past and past participle, such as "gave" and "given"), an ambiguous
  participle, or a noun that already ends in a plural "s". A verb that is not in the
  irregular table is inflected by the regular rules (a conservative heuristic, so an unknown
  irregular verb may receive a regular spelling). A phrasal-verb alternative inflects its head
  word. The caller shows the base alternative and says so when the result is `null`.

#### Error Handling

No exceptions are thrown for valid input; unsupported cases return an empty list or `null`.
