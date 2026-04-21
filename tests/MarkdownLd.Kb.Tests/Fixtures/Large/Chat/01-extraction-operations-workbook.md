---
title: Extraction Operations Workbook
canonical_url: https://large-fixture.example/playbooks/extraction-operations-workbook/
summary: Coordinate corpus intake, chunk shaping, RDF normalization, and cache rewrite review for large extraction runs.
authors:
  - Ada Lovelace
  - Linh Tran
tags:
  - extraction
  - chunking
  - rdf
  - cache
about:
  - Corpus Intake Checklist
  - RDF Normalizer
entity_hints:
  - Whole Section Chunker
  - Prompt Version Gate
---
# Corpus Intake

The intake review records a corpus intake checklist, a front matter manifest, and a short note about canonical URI ownership.
It also captures a list of markdown files that must stay deterministic across repeated parse and chunk passes.

- Confirm that every document has a title.
- Confirm that every canonical URL resolves to one intended article.
- Confirm that cache rewrite work is linked to the exact source path.

The intake worksheet intentionally stays long so the prompt includes realistic context instead of a toy paragraph:

- Record the repository root, branch, and fixture snapshot used for the extraction rehearsal.
- Record whether the corpus came from docs, logs, csv exports, yaml manifests, or previously normalized markdown.
- Record the exact source paths that were converted into markdown source documents.
- Record how many files were skipped and why.
- Record whether each file had explicit front matter or relied on inferred metadata.
- Record whether the same article identifier appeared under more than one folder.
- Record whether the author supplied tags, about fields, authors, and entity hints.
- Record whether the markdown contained tables, mermaid diagrams, JSON examples, or fenced code blocks.
- Record whether a previous cache rewrite already touched the same source path.
- Record whether the release checklist references this intake run.
- Record whether the issue was exploratory, release-bound, or incident-driven.
- Record whether the final evidence package needs to include the raw intake checklist.

```text
intake-ledger:
01 capture repository path and current branch
02 capture fixture snapshot timestamp
03 enumerate source files selected for the rehearsal
04 note each canonical url discovered in front matter
05 note each file that lacked explicit front matter
06 note whether graph groups were present
07 note whether related and next-step links were present
08 note whether code fences might affect chunk boundaries
09 note whether a prior cache slot already exists
10 note who reviewed the intake package
11 note where the final evidence bundle will be stored
12 note whether the extraction run is expected to be replayed
```

## Chunk Shaping

The chunk shaping review compares the whole section chunker against smaller deterministic splits and keeps a chunk boundary ledger for each section.
Operators store a short example of the markdown that produced each chunk so prompt regressions can be explained later.

```markdown
Chunk Shaping Example
Keep the heading label and the nearby body text together when the section is still small enough.
```

Chunk shaping notes:

| Concern | Response |
| --- | --- |
| heading too short | keep body text attached so the prompt preserves meaning |
| section too long | split only when the deterministic chunker can explain the boundary |
| inline example lost | copy the example into the ledger for future regression review |
| repeated heading | record the full heading path instead of the leaf heading alone |
| fenced block detached | keep the fence with the paragraph that introduces it |
| cache rewrite query | attach the chunk source URI to the final assertion source |

```json
{
  "chunkExamples": [
    {
      "sectionPath": "Corpus Intake",
      "expectedChunker": "whole-section-v1",
      "notes": [
        "keep the intake bullets beside the canonical URI note",
        "preserve the file-selection ledger inside the same chunk"
      ]
    },
    {
      "sectionPath": "Corpus Intake / Chunk Shaping",
      "expectedChunker": "whole-section-v1",
      "notes": [
        "retain the fenced markdown example",
        "explain later why the chunk boundary ledger exists"
      ]
    }
  ]
}
```

## RDF Normalization

The normalization stage resolves RDF Normalizer output, stable entity ids, and a schema mapping contract before the graph builder runs.
Every extraction pass notes when schema mappings or sameAs links changed between releases.

Normalization reminders:

- Keep a stable entity id even when the label gets cleaner between releases.
- Record when the schema mapping contract added or removed a predicate.
- Record whether a sameAs link came from front matter, a wiki link, or a reviewed extraction payload.
- Record whether the article itself was the subject or whether the chunk explicitly named a separate entity.
- Record why a candidate assertion was omitted when the predicate could not be justified.
- Record whether the extraction response duplicated a previous entity with richer metadata.
- Record whether the normalization pass collapsed distinct entities by mistake.
- Record whether the final graph kept caller-visible provenance for the chunk source URI.

```text
normalization-notebook:
entity=RDF Normalizer type=schema:SoftwareApplication action=accepted reason="explicitly named in the workbook"
entity=Schema Mapping Contract type=schema:CreativeWork action=accepted reason="used as durable review artifact"
assertion=article-mentions-rdf-normalizer action=accepted source=chunk
assertion=article-mentions-schema-mapping-contract action=accepted source=chunk
assertion=article-owns-everything action=rejected reason="predicate not explicit"
```

## Cache Rewrite Review

The review explains why a prompt version gate changed, why the old cache slot could not be reused, and why the atomic cache file move was safe.
When a cache rewrite happens, the operator also adds a corruption evidence report and a short rollback note.

Cache rewrite review ledger:

- Explain whether the mismatch came from document identity, source path, chunker profile, prompt version, or model id.
- Explain whether the old cache file was valid JSON for the wrong article or whether it was corrupt JSON.
- Explain whether the rewrite produced one new slot or whether it had to preserve two distinct article identities for the same source path.
- Explain whether a temporary file remained after a failed write attempt.
- Explain whether the release evidence checklist needs to be regenerated after the rewrite.
- Explain whether the rollback evidence ledger references the same prompt change.
- Explain whether the rewrite only affected one article or the entire corpus rehearsal.

```text
cache-review-log:
2026-04-04T10:00:00Z reason="prompt version changed"
2026-04-04T10:07:00Z reason="same source path now maps to a second canonical article id"
2026-04-04T10:12:00Z reason="temporary file flushed before move"
2026-04-04T10:18:00Z reason="corruption evidence report attached"
2026-04-04T10:24:00Z reason="rollback note linked to release evidence"
```
