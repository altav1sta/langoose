# Third-party data attribution

Langoose's corpus-backed features incorporate data from open linguistic
projects. This file lists our actual attribution obligations. Source
selection rationale (which corpora we evaluated, included, or deferred)
lives in [`docs/agent/parallel-corpora.md`](docs/agent/parallel-corpora.md).

## Wiktionary (via Kaikki.org)

- **License:** [CC-BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
- **What we use:** Dictionary entries, forms, glosses, translations.
  Extracted by [Kaikki.org / wiktextract](https://kaikki.org/) from
  per-language Wiktionary dumps.
- **Required attribution:** "Based on content from Wiktionary, used
  under CC-BY-SA 4.0. Extracted by Kaikki.org."

## Tatoeba

- **License:** [CC-BY 2.0](https://creativecommons.org/licenses/by/2.0/)
  for sentence text. (Audio is CC-BY-NC and is excluded from our imports.)
- **What we use:** Per-language sentence dumps and the global
  translation-links file.
- **Required attribution:** "Example sentences from Tatoeba, used under
  CC-BY 2.0."

## wordfreq

- **License:** [CC-BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)
  for the aggregated frequency data.
- **What we use:** Per-language word lists with rank and Zipf score.
  Built by Robyn Speer from web text, subtitles, and other corpora.
- **Required attribution:** "Frequency data from wordfreq (Robyn Speer),
  used under CC-BY-SA 4.0."

## UI surfacing

The Langoose web UI must render visible attribution wherever third-party
corpus content is shown to end users. Tracked under
[#92](https://github.com/altav1sta/langoose/issues/92).

## Forks

If you fork Langoose and redistribute its corpus data, you must carry
equivalent attribution forward. CC-BY-SA in particular is copyleft and
obliges derivative works to keep the same license and credit the source.
