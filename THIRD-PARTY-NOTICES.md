# Third-Party Notices

Ste100Mark is Copyright (c) DEMA Consulting and is licensed under the MIT License (see `LICENSE`). The tool depends on
the package `DemaConsulting.PartOfSpeech`, which embeds language data adapted from the third-party works below. Their
attribution and license notices are reproduced here, and this file is packed at the root of the NuGet package so that
the notices travel with every copy of the package. The file `PACKAGE-LICENSE.md`, also packed at the root of the
package, is the license of the package and states which license covers which part.

The embedded data comes from the lexicon of `DemaConsulting.PartOfSpeech` (derived from Open English WordNet and the
Moby Part-of-Speech List, with tags attested in MASC) and the trained weights of its tagger.

## The Manually Annotated Sub-Corpus (MASC)

The lexicon and the trained weights embedded in `DemaConsulting.PartOfSpeech` are adapted in part from MASC.

- **Title:** The Manually Annotated Sub-Corpus (MASC), Penn Treebank syntax annotations of the written texts
- **Creators:** the MASC project: Nancy Ide (Vassar College, principal investigator), Collin Baker (International
  Computer Science Institute), Christiane Fellbaum (Princeton University) and Rebecca Passonneau (Columbia University);
  the texts are drawn from the Open American National Corpus (OANC)
- **Source:** <https://anc.org/data/masc/>
- **License:** Creative Commons Attribution 3.0 United States (CC BY 3.0 US),
  <https://creativecommons.org/licenses/by/3.0/us/>
- **Citation:** Nancy Ide, Collin Baker, Christiane Fellbaum, Charles Fillmore and Rebecca Passonneau, "MASC: the
  Manually Annotated Sub-Corpus of American English", Proceedings of the Sixth International Conference on Language
  Resources and Evaluation (LREC 2008), <https://aclanthology.org/L08-1105/>
- **Changes:** the work was adapted by DEMA Consulting. The Penn Treebank part-of-speech tags of every token were
  converted to Universal POS (UPOS) tags and the sentences were filtered. The converted data is not the original MASC
  data. The trained weights of the tagger were learned in part from the converted data: they are numbers that a training
  program computed from the tags and the text of the converted sentences, and they contain no MASC text. The weights are
  provided under the terms of this license. The authors of MASC do not endorse this project or the converted data.

## Open English WordNet

The spelling-to-UPOS lexicon embedded in `DemaConsulting.PartOfSpeech` is derived from the word forms of Open English
WordNet.

- **Title:** Open English WordNet 2025 Edition
- **Creators:** the Open English WordNet team and contributors (John P. McCrae and others)
- **Source:** <https://en-word.net> and <https://github.com/globalwordnet/english-wordnet>
- **License:** Creative Commons Attribution 4.0 International (CC BY 4.0), <https://creativecommons.org/licenses/by/4.0/>
- **Changes:** only the spellings of the entries and their inflected forms were used. They were combined with other
  sources and reduced to a table that maps a lower-case spelling to the UPOS tags it may carry. The lexicon is not the
  original Open English WordNet data, and the glosses, examples and relations are not part of it. Open English WordNet
  and its contributors do not endorse this project.

### Princeton WordNet

Open English WordNet is derived from Princeton WordNet under the WordNet License and developed further under CC BY 4.0.
Its license says: "You may share and adapt this resource providing attribution is given to both Princeton WordNet and
the Open English Wordnet team." The notice that Open English WordNet distributes is reproduced here, as the license
requires (Princeton University, <https://wordnet.princeton.edu>):

```text
This software and database is being provided to you, the LICENSEE, by
the Open English Wordnet team under the Creative Commons Attribution 4.0
International License (CC-BY 4.0).

Open English Wordnet 2023 Copyright 2023 by the Open English Wordnet team.

Permission to use, copy, modify and distribute this software and
database and its documentation for any purpose and without fee or
royalty is hereby granted, provided that you agree to comply with
the following copyright notice and statements, including the disclaimer,
and that the same appear on ALL copies of the software, database and
documentation, including modifications that you make for internal
use or for distribution.

WordNet 3.1 Copyright 2011 by Princeton University.  All rights reserved.

THIS SOFTWARE AND DATABASE IS PROVIDED "AS IS" AND PRINCETON
UNIVERSITY MAKES NO REPRESENTATIONS OR WARRANTIES, EXPRESS OR
IMPLIED.  BY WAY OF EXAMPLE, BUT NOT LIMITATION, PRINCETON
UNIVERSITY MAKES NO REPRESENTATIONS OR WARRANTIES OF MERCHANT-
ABILITY OR FITNESS FOR ANY PARTICULAR PURPOSE OR THAT THE USE
OF THE LICENSED SOFTWARE, DATABASE OR DOCUMENTATION WILL NOT
INFRINGE ANY THIRD PARTY PATENTS, COPYRIGHTS, TRADEMARKS OR
OTHER RIGHTS.

The name of Princeton University or Princeton may not be used in
advertising or publicity pertaining to distribution of the software
and/or database.  Title to copyright in this software, database and
any associated documentation shall at all times remain with
Princeton University and LICENSEE agrees to preserve same.
```

## Courtesy Notices

These sources do not require attribution, and the notices below are given as a courtesy and for provenance.

### Moby Part-of-Speech List

The lexicon also uses the part-of-speech data of the Moby Part-of-Speech List (Moby Part-of-Speech II) by Grady Ward,
<https://www.gutenberg.org/ebooks/3203>, which the author dedicated to the public domain.

### Other training data

The weights were also learned from corpora that are dedicated to the public domain (CC0 1.0), or are in the public
domain in the United States, including sentences from the CC0 export of Tatoeba (<https://tatoeba.org>), Project
Gutenberg (<https://www.gutenberg.org>) books and US federal government documents. The details are in the notices of
the `DemaConsulting.PartOfSpeech` package.
