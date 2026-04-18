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

## Compliance in this repository

- **Redistributed artifacts.** Any corpus dump published to
  [GitHub Releases](https://github.com/altav1sta/langoose/releases) via
  `scripts/publish-{full,mini}-corpus-dump.sh` ships this file as a
  companion asset and cites Wiktionary + Kaikki in the release notes.
  The dump itself is therefore available under CC-BY-SA 4.0 to match
  the upstream.
- **UI attribution.** The Langoose web UI will render a visible
  attribution notice anywhere Wiktionary-derived content is surfaced to
  end users (translations, inflected forms, example sentences). This
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

- **wordfreq** ([#96](https://github.com/altav1sta/langoose/issues/96)) —
  frequency rankings derived from web, subtitles, and Twitter.
  CC-BY-SA.
- **CEFR-J** — English CEFR level data. Open with citation.
- **Tatoeba** ([#91](https://github.com/altav1sta/langoose/issues/91)) —
  example-sentence corpus used for generating contextual examples.
  Text is CC-BY 2.0; audio is CC-BY-NC and will not be used.

## Contact

If you believe a source used here is incorrectly attributed or
credited, please open an issue on
[github.com/altav1sta/langoose](https://github.com/altav1sta/langoose/issues).
