# Third-party data attribution

Langoose's corpus-backed enrichment pipeline (see
[`apps/api/src/Langoose.Corpus.DbTool/README.md`](apps/api/src/Langoose.Corpus.DbTool/README.md))
incorporates data from open linguistic projects. This file lists every
external data source the project currently ships or redistributes, along
with its license and attribution requirements.

If you fork Langoose and ship your own corpus dumps, you must carry
equivalent attribution forward — CC-BY-SA in particular is copyleft and
obliges derivative works to keep the same license and credit the source.

## Currently shipping

### Wiktionary

- **Source:** [Wiktionary](https://www.wiktionary.org/) — the free
  dictionary project hosted by the Wikimedia Foundation.
- **What we use:** Headwords, parts of speech, inflected forms, glosses,
  translations, and etymology numbers for every imported language.
- **License:**
  [Creative Commons Attribution-ShareAlike 4.0 International (CC-BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/).
  Some older Wiktionary content is additionally available under
  CC-BY-SA 3.0 and the GNU Free Documentation License.
- **Attribution:** "Based on content from Wiktionary, used under
  CC-BY-SA 4.0." Derivative works must carry the same license.

### Kaikki.org

- **Source:** [Kaikki.org](https://kaikki.org/) — Tatu Ylonen's
  extraction and distribution of structured Wiktionary data.
- **Project home:** <https://github.com/tatuylonen/wiktextract>
- **What we use:** The per-language JSONL extracts produced by Kaikki
  from the various Wiktionary editions. These are the input to our
  `import-wiktionary` command and are the only source for rows in the
  `wiktionary_entries` table.
- **License:** Kaikki redistributes Wiktionary content under the same
  CC-BY-SA license as Wiktionary itself.
- **Attribution:** "Data extracted by Kaikki.org (wiktextract)." We
  surface the source filename and snapshot date in the `corpus_metadata`
  table (`source_version_wiktionary_<lang>`) so the exact upstream
  snapshot is always traceable.

### Tatoeba

- **Source:** [Tatoeba](https://tatoeba.org/) — open database of
  short, crowd-translated example sentences in many languages.
- **What we use:** Per-language sentence dumps (text only — audio is
  CC-BY-NC and is **not** imported) plus the global translation-links
  file. Fetched offline via
  [`scripts/download-tatoeba.sh`](scripts/download-tatoeba.sh) and
  bulk-loaded into `tatoeba_sentences` + `tatoeba_links` by
  `import-tatoeba`. Powers contextual examples in the learning loop
  (paired sentences across the user's source/target languages drive
  `EntryContext` materialisation).
- **License:**
  [Creative Commons Attribution 2.0 Generic (CC-BY 2.0)](https://creativecommons.org/licenses/by/2.0/)
  for sentence text. Audio is CC-BY-NC and is excluded from our
  imports because the dump must remain redistributable without an NC
  restriction.
- **Attribution:** "Example sentences from Tatoeba (CC-BY 2.0)." The
  source identifier (e.g. `tatoeba-2026-05-03`) is recorded in both
  the data tables (`tatoeba_sentences.source`, `tatoeba_links.source`)
  and `corpus_metadata` as `source_tatoeba_<lang>_<pair-lang>` so the
  exact upstream snapshot is always traceable. UI surfacing of this
  attribution is tracked separately under epic
  [#92](https://github.com/altav1sta/langoose/issues/92).

### wordfreq

- **Source:** [wordfreq](https://github.com/rspeer/wordfreq) — Robyn
  Speer's per-language frequency tables aggregated from web text,
  subtitles, Twitter, and other corpora.
- **What we use:** Per-language word lists with rank and Zipf score.
  Fetched offline via
  [`scripts/download-wordfreq.sh`](scripts/download-wordfreq.sh) (which
  invokes the Python `wordfreq` package — by default inside a
  `python:3-slim` Docker container, so no local Python install is
  required) and bulk-loaded into `wordfreq_rankings` by
  `import-wordfreq`. Rankings are used to filter mini dumps to
  representative everyday vocabulary, and will rank multi-candidate
  translations in the corpus-backed enrichment provider.
- **License:**
  [Creative Commons Attribution-ShareAlike 4.0 International (CC-BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/)
  for the aggregated frequency data; some upstream subtitle corpora are
  CC-BY. Treating the bundled output as CC-BY-SA covers both.
- **Attribution:** "Frequency data from wordfreq (Robyn Speer), used
  under CC-BY-SA 4.0." The source identifier (e.g. `wordfreq-2026-04-25`)
  is recorded both in the `wordfreq_rankings.source` column and in
  `corpus_metadata` as `source_version_wordfreq_<lang>` so the exact
  snapshot is traceable.

## Compliance in this repository

- **Redistributed artifacts.** Any corpus dump published to
  [GitHub Releases](https://github.com/altav1sta/langoose/releases) via
  `scripts/publish-{full,test}-corpus-dump.sh` ships this file as a
  companion asset and cites Wiktionary, Kaikki, and wordfreq in the
  release notes. The dump itself is therefore available under
  CC-BY-SA 4.0 to match the upstream.
- **UI attribution.** The Langoose web UI will render a visible
  attribution notice anywhere third-party corpus content is surfaced to
  end users — Wiktionary translations and inflected forms (CC-BY-SA),
  Tatoeba example sentences (CC-BY 2.0), and any future source. This
  work is tracked separately as part of epic
  [#92](https://github.com/altav1sta/langoose/issues/92) and is not yet
  shipped.
- **Metadata trail.** Every imported entry carries a `source_version`
  column pointing at the Kaikki snapshot it came from, so downstream
  consumers can cross-reference against the upstream archive.

## Sources evaluated but not used

These were considered during the corpus design (see
[`docs/agent/enrichment-guidance.md`](docs/agent/enrichment-guidance.md))
and intentionally left out. They are listed here so future contributors
don't re-litigate the decision silently.

- **OPUS** (<https://opus.nlpl.eu/>) — rejected because many sub-corpora
  carry non-commercial (NC) restrictions that would prevent commercial
  redistribution, and the sentence-level alignment is too noisy for our
  word-level lookup needs.
- **English Profile** — rejected because it requires a commercial
  licence for the CEFR vocabulary data we'd want.
- **Kelly Project** — deferred until commercial-redistribution terms
  are clearer.

## Planned future sources

Already queued in the enrichment epic ([#92](https://github.com/altav1sta/langoose/issues/92));
each will be added here when its import code lands.

- **CEFR-J** — English CEFR level data. Open with citation.
- **Global Voices** ([#91](https://github.com/altav1sta/langoose/issues/91))
  — second example-sentence corpus, CC-BY 3.0. Editorially reviewed
  news with a more formal register than Tatoeba.

## Contact

If you believe a source used here is incorrectly attributed or
credited, please open an issue on
[github.com/altav1sta/langoose](https://github.com/altav1sta/langoose/issues).
