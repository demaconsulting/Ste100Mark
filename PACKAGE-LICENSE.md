SPDX-License-Identifier: MIT AND CC-BY-4.0 AND CC-BY-3.0-US

# License of the DemaConsulting.Ste100Mark Package

The NuGet package `DemaConsulting.Ste100Mark` contains the code of the tool and the assemblies it depends on. The code of
the tool is licensed under the MIT License. The tool depends on the package `DemaConsulting.PartOfSpeech`, whose
assembly embeds language data from third parties that is licensed under the terms below. The first line of this file is
the SPDX license expression that covers the whole package; it is a hint for license scanners, and this file is the
license. nuget.org accepts only license expressions that the Open Source Initiative or the Free Software Foundation
approve, and `CC-BY-3.0-US` is neither, so the package names this file instead of an expression.

## Code: MIT License

Copyright (c) DEMA Consulting

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Data Embedded via DemaConsulting.PartOfSpeech

The `DemaConsulting.PartOfSpeech` assembly embeds two binary files. Each is derived from third-party works, was adapted
by DEMA Consulting, and is not the original data.

- The lexicon maps a lower-case English spelling to the Universal POS tags that it may carry. It is derived from the word
  forms of Open English WordNet 2025 Edition, which is licensed under the Creative Commons Attribution 4.0 International
  License (CC BY 4.0, <https://creativecommons.org/licenses/by/4.0/>) and derived from Princeton WordNet. The tags it
  lists are attested in the Manually Annotated Sub-Corpus (MASC), which is licensed under the Creative Commons
  Attribution 3.0 United States License (CC BY 3.0 US, <https://creativecommons.org/licenses/by/3.0/us/>).
- The weights are the trained model of the tagger. They were learned in part from data adapted from MASC, so they are
  provided under the terms of the CC BY 3.0 US license.

The authors of Open English WordNet, Princeton University and the authors of MASC do not endorse this project, the
lexicon or the weights.

## Attribution and Where to Find It

The attribution, license links, citation and statement of changes that these licenses require are in the file
`THIRD-PARTY-NOTICES.md` at the root of this package, which also reproduces the Princeton WordNet license notice. This
file and `THIRD-PARTY-NOTICES.md` together are the complete notices of the package. Keep both with every copy of the
package that you redistribute.
