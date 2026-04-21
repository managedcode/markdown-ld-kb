---
title: Graph Ingestion Playbook
summary: Prepare markdown corpora, normalize front matter, and build deterministic RDF graphs for the knowledge bank.
tags:
  - graph
  - ingestion
  - markdown
  - rdf
about:
  - RDF
  - JSON-LD
  - Schema Mapping Contract
entity_hints:
  - Extraction Cache
  - Deterministic Section Chunker
graph_groups:
  - Graph Operations
  - Reliability Operations
graph_related:
  - https://large-fixture.example/runbooks/cache-recovery-workflow/
  - https://large-fixture.example/runbooks/semantic-search-tuning/
graph_next_steps:
  - https://large-fixture.example/runbooks/query-federation-runbook/
---
# Graph Ingestion Playbook

The ingestion crew stages markdown notes, front matter manifests, and section boundaries before any extraction call is made.
Every release keeps a plain-text change log, a chunk boundary ledger, and a canonical URI manifest.

article --mentions--> Extraction Cache
Extraction Cache --sameAs--> https://example.com/components/extraction-cache
article --mentions--> Deterministic Section Chunker
Deterministic Section Chunker --sameAs--> https://example.com/components/deterministic-section-chunker
article --mentions--> Schema Mapping Contract

## Intake Checklist

- Review `title`, `summary`, and `canonical_url`.
- Reject ambiguous `graph_groups` values when authors mix workflows and runtime entities.
- Preserve wiki links such as [[RDF]] and references to [Query Federation Runbook](./02-query-federation-runbook.md).

A build is only acceptable when the document corpus stays deterministic after repeated parse and chunk passes.

The working notebook also keeps the following operational reminders close to the authored markdown so reviewers can see the exact context that shaped the graph:

- The same article title may appear in a draft folder, a release folder, and a migrated archive, but only one canonical URI is allowed to survive the ingestion pass.
- A temporary note is not allowed to define graph groups that imply production routing if the note is only an experiment or a one-off retrospective.
- Authors are expected to describe missing front matter explicitly instead of letting a downstream workflow guess whether the field was omitted on purpose.
- The ingestion ledger records whether the source file contained wiki links, fenced code blocks, inline HTML, mermaid diagrams, or tables that might influence section chunking.
- Every repeated build keeps the previous chunk boundary ledger beside the new one so the reviewer can compare stable chunk identifiers across runs.
- When the corpus includes mixed `.md`, `.markdown`, `.mdx`, `.txt`, `.jsonl`, or `.yaml` sources, the preflight note documents which converter path produced the final markdown source document.
- A document that only differs in whitespace still gets compared through normalized content because reviewers care about caller-visible graph drift, not raw file formatting noise.
- If a title changes during front matter cleanup, the reviewer writes down whether the article identifier moved or whether the title only changed for display.
- Relative links are preserved in the staging copy so the final graph can explain how a note referenced the related workflow at authoring time.
- Each source path is logged together with the resulting article URI, chunk count, and the final summary used for graph search candidates.
- If an author provides both `canonical_url` and an inferred path-based identifier, the conflict is written down and resolved before facts enter the graph builder.
- The graph builder never receives a document whose section order is already ambiguous.

The appendix below is intentionally verbose because the suite should exercise long authored markdown, not only short snippets:

```yaml
ingestionAppendix:
  corpus:
    - sourcePath: runbooks/graph-ingestion-playbook.md
      canonicalUri: https://large-fixture.example/runbooks/graph-ingestion-playbook/
      expectedArticleType: schema:Article
      expectedGroups:
        - Graph Operations
        - Reliability Operations
      expectedMentions:
        - Extraction Cache
        - Deterministic Section Chunker
        - Schema Mapping Contract
    - sourcePath: runbooks/query-federation-runbook.md
      canonicalUri: https://large-fixture.example/runbooks/query-federation-runbook/
      expectedArticleType: schema:Article
      expectedGroups:
        - Query Operations
        - Reliability Operations
      expectedMentions:
        - Read Only Query Guard
        - Remote Endpoint Allowlist
        - Provenance Audit
    - sourcePath: runbooks/cache-recovery-workflow.md
      canonicalUri: https://large-fixture.example/runbooks/cache-recovery-workflow/
      expectedArticleType: schema:Article
      expectedGroups:
        - Reliability Operations
        - Graph Operations
      expectedMentions:
        - Prompt Version Gate
        - Atomic Cache File Move
        - Corruption Evidence Report
  preflightChecks:
    - check: title-present
      explanation: Every source must produce a stable caller-visible title.
    - check: summary-present
      explanation: Ranked graph search needs a human-friendly summary for coarse recall.
    - check: canonical-uri-stable
      explanation: The same note may move folders without changing its logical article identity.
    - check: chunk-count-repeatable
      explanation: Deterministic chunking must survive repeated runs over the same corpus.
    - check: graph-groups-readable
      explanation: Group labels are user-facing explanation nodes in focused search output.
    - check: links-preserved
      explanation: Related notes should stay discoverable even before LLM extraction runs.
  reviewLedger:
    - timestamp: 2026-04-01T09:00:00Z
      reviewer: ingestion-bot
      outcome: accepted
      note: Stable chunk identifiers and front matter mapping matched the baseline.
    - timestamp: 2026-04-03T14:15:00Z
      reviewer: release-review
      outcome: accepted
      note: Added RDF and JSON-LD appendix material without changing article identity.
    - timestamp: 2026-04-06T18:20:00Z
      reviewer: graph-oncall
      outcome: accepted
      note: Relative links and wiki links remained queryable after the parser cleanup.
```

## Failure Notes

| Symptom | Response |
| --- | --- |
| duplicate entities | merge on stable ids |
| stale cache entry | rebuild chunk fingerprints |
| missing summary | fail the validation gate |

Additional failure observations used by the suite:

| Observation | Why it matters |
| --- | --- |
| duplicate sameAs links | should be merged rather than multiply asserted |
| changing section order | can invalidate stable chunk identifiers |
| mixed article ids | produces caller-visible graph ambiguity |
| inconsistent graph groups | harms focused search explanations |
| missing related links | reduces search context labels |
| overly short notes | fail to resemble real operating manuals |
| flattened front matter | removes distinctions between authors, tags, and about fields |
| hidden migration notes | make incident recovery harder to audit |

```text
Ingestion review excerpt:
01. stage source documents into a deterministic folder snapshot
02. load markdown through the converter path that matches the file shape
03. normalize line endings and remove only irrelevant whitespace drift
04. compare inferred article ids with explicit canonical URLs
05. build the section outline before any extraction adapter is called
06. capture heading paths for every chunk candidate
07. record wiki links and markdown links as part of the authored context
08. copy summary and title fields into the review ledger
09. verify that graph group labels are readable to humans
10. inspect chunk boundary changes against the previous baseline
11. rebuild the graph and compare triple counts
12. rerun the same flow to confirm deterministic output
13. capture an explanation when a document intentionally changes identity
14. reject notes that silently move from one logical article to another
15. verify that parser diagnostics remain caller-visible
16. ensure related runbooks still resolve through their expected URIs
17. store the final evidence bundle beside the release note
18. keep the appendix material because real corpora are long and repetitive
```

```sparql
SELECT ?article WHERE {
  ?article <https://schema.org/name> "Graph Ingestion Playbook" .
}
```

## Deterministic Ingestion Replay Appendix

The replay appendix keeps the operational phrasing that authors and reviewers actually use when they inspect chunk boundaries and metadata drift.
It adds more real-looking search surface without turning every body phrase into canonical graph metadata.

Replay 01: reviewer searched for deterministic section chunker before the formal playbook title.
Replay 02: reviewer searched for schema mapping contract before the formal playbook title.
Replay 03: reviewer searched for canonical URI manifest before the formal playbook title.
Replay 04: reviewer searched for chunk boundary ledger before the formal playbook title.
Replay 05: reviewer searched for repeated build drift before the formal playbook title.
Replay 06: reviewer searched for front matter normalization before the formal playbook title.
Replay 07: reviewer searched for relative links preserved before the formal playbook title.
Replay 08: reviewer searched for wiki links preserved before the formal playbook title.
Replay 09: reviewer searched for graph group labels readable before the formal playbook title.
Replay 10: reviewer searched for article identity conflict before the formal playbook title.
Replay 11: the replay notebook kept source path, canonical URI, and summary in one row.
Replay 12: the replay notebook kept chunk count, section path, and related link count in one row.
Replay 13: the replay notebook kept parser diagnostics visible to the reviewer.
Replay 14: the replay notebook kept the previous baseline beside the current run.
Replay 15: the replay notebook kept the deterministic snapshot note beside the release packet.

| Replay question | Why it mattered |
| --- | --- |
| did the canonical URI stay stable | caller-visible identity must not drift |
| did the chunk count stay repeatable | deterministic chunking must be provable |
| did the summary stay readable | coarse search recall depends on it |
| did related links survive parsing | focused search context depends on them |
| did graph groups stay human-readable | focused explanations depend on them |

```text
ingestion-replay-log:
10:02 query="deterministic section chunker" outcome="playbook body match"
10:07 query="schema mapping contract" outcome="playbook body match"
10:11 query="canonical URI manifest" outcome="playbook body match"
10:16 query="chunk boundary ledger" outcome="playbook body match"
10:21 note="kept replay wording in the appendix for search stress"
```
