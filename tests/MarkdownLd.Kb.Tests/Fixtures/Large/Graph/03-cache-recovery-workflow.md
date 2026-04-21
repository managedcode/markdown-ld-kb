---
title: Cache Recovery Workflow
summary: Repair stale extraction cache entries after prompt, model, or chunk fingerprints drift from the stored document state.
tags:
  - cache
  - recovery
  - extraction
  - reliability
about:
  - Prompt Version Gate
  - Atomic Cache File Move
  - Corruption Evidence Report
entity_hints:
  - Prompt Version Gate
  - Atomic Cache File Move
graph_groups:
  - Reliability Operations
  - Graph Operations
graph_related:
  - https://large-fixture.example/runbooks/graph-ingestion-playbook/
  - https://large-fixture.example/runbooks/incident-triage-guide/
graph_next_steps:
  - https://large-fixture.example/runbooks/release-gate-checklist/
---
# Cache Recovery Workflow

The cache workflow isolates stale entries, compares prompt versions, and rewrites the persisted extraction record only after a clean rebuild.
Operators keep a corruption evidence report and a short explanation of why the original cache slot became invalid.

article --mentions--> Prompt Version Gate
Prompt Version Gate --sameAs--> https://example.com/components/prompt-version-gate
article --mentions--> Atomic Cache File Move
Atomic Cache File Move --sameAs--> https://example.com/components/atomic-cache-file-move
article --mentions--> Corruption Evidence Report

## Stale Entry Audit

The stale entry audit compares chunk fingerprints, model identity, and document identity before any cache hit is trusted.
If a source path points at two canonical articles, the cache review must keep both identities distinct.

The audit journal keeps many more examples than a toy test would normally hold:

- Compare the document identity derived from the article URI with the source path that located the markdown file on disk.
- Compare the chunker profile used by the original run with the profile used by the current run.
- Compare prompt version identifiers even when the prose of the prompt only changed in one rule paragraph.
- Compare the chat model identifier because the same prompt version on a different model is still a different cache slot.
- Compare each chunk fingerprint in order rather than only comparing the number of chunks.
- Compare the review note that explains why the old cache slot existed in the first place.
- Compare the expected article URI when the source path moved between release branches.
- Compare whether the prior cache entry used the whole section chunker or the deterministic section chunker.
- Compare whether the final write used an atomic cache file move or a direct overwrite.
- Compare whether the corrupted entry was truncated, malformed, or valid JSON for the wrong document identity.
- Compare whether the source path is empty, inferred, or explicitly provided by the caller.
- Compare the old and new release evidence packages when a cache rewrite happens during incident triage.

```json
{
  "cacheSlotExamples": [
    {
      "sourcePath": "playbooks/extraction-operations-workbook.md",
      "documentId": "https://large-fixture.example/playbooks/extraction-operations-workbook/",
      "chunkerProfile": "whole-section-v1",
      "promptVersion": "extract-rdf-v1",
      "modelId": "large-chat-model-a",
      "chunkFingerprints": [
        "corpus-intake",
        "chunk-shaping",
        "rdf-normalization",
        "cache-rewrite-review"
      ],
      "expectedDecision": "cache-hit"
    },
    {
      "sourcePath": "playbooks/extraction-operations-workbook.md",
      "documentId": "https://large-fixture.example/playbooks/extraction-operations-workbook-alt/",
      "chunkerProfile": "whole-section-v1",
      "promptVersion": "extract-rdf-v1",
      "modelId": "large-chat-model-a",
      "chunkFingerprints": [
        "corpus-intake",
        "chunk-shaping",
        "rdf-normalization",
        "cache-rewrite-review"
      ],
      "expectedDecision": "distinct-slot-required"
    },
    {
      "sourcePath": "playbooks/extraction-operations-workbook.md",
      "documentId": "https://large-fixture.example/playbooks/extraction-operations-workbook/",
      "chunkerProfile": "deterministic-section-v1",
      "promptVersion": "extract-rdf-v1",
      "modelId": "large-chat-model-a",
      "chunkFingerprints": [
        "corpus-intake-part-1",
        "corpus-intake-part-2",
        "chunk-shaping-part-1",
        "rdf-normalization-part-1"
      ],
      "expectedDecision": "cache-miss-different-profile"
    }
  ]
}
```

## Rewrite Steps

- Move the new JSON payload into place only after the temporary file is fully written.
- Leave enough evidence to explain why the earlier cache entry could not be reused.
- Rebuild the release evidence package when the prompt version gate changes.

When the operator writes the rewrite note, the long-form record captures every step:

| Step | Detail |
| --- | --- |
| isolate old entry | copy the original JSON payload before any destructive cleanup |
| parse candidate key | record source path, document id, model id, prompt version, and chunk profile |
| compare fingerprints | note the first mismatching chunk order or content hash |
| decide rewrite | keep the final reason human-readable for reviewers |
| write temp file | ensure the new file is complete before swap-in |
| replace destination | overwrite only after the temp payload is durable |
| verify read path | immediately re-open the final file through the same cache API |
| attach evidence | keep the diff in the incident or release package |

```text
cache-rewrite-ledger:
01 open existing cache entry
02 deserialize the stored key
03 compare document id with the active article uri
04 compare source path with the active markdown path
05 compare chunker profile ids
06 compare prompt version ids
07 compare model ids
08 compare chunk fingerprint count
09 compare ordered chunk fingerprints
10 mark the first mismatch in the ledger
11 prepare the new JSON payload in a temporary file
12 flush the temporary file
13 move the temporary file into place atomically
14 re-open the destination file
15 confirm the new entry matches the active request key
16 attach the rewrite note to release evidence
17 keep the previous corrupted payload only as offline evidence
18 remove stray temporary files if they remain after a failed write
```

```text
temporary.json -> final.json
```

## Collision Replay Appendix

The appendix records replay cases that differ by only one cache-key dimension at a time.
It gives the large corpus a denser cache-specific body so search and extraction tests see more than a handful of toy examples.

Replay 01: same source path, same prompt version, same model, different document id, rewrite required.
Replay 02: same source path, same prompt version, same document id, different model id, rewrite required.
Replay 03: same source path, same model id, same document id, different prompt version, rewrite required.
Replay 04: same source path, same model id, same prompt version, different chunker profile, rewrite required.
Replay 05: same source path, different chunk order, same chunk count, rewrite required.
Replay 06: same source path, different chunk count, same title, rewrite required.
Replay 07: same source path, corrupted JSON payload, destination unreadable, offline evidence only.
Replay 08: same source path, valid JSON, wrong document identity, treat as a miss and keep evidence.
Replay 09: same source path, same document identity, same prompt version, same model id, valid hit.
Replay 10: source path moved between branches, document identity stable, old slot not reusable.
Replay 11: source path stable, document identity changed after front matter rewrite, old slot not reusable.
Replay 12: source path empty, document identity explicit, slot naming must stay deterministic.
Replay 13: source path explicit, document identity empty, slot naming must still avoid accidental collapse.
Replay 14: model id changed only by suffix revision, slot still distinct.
Replay 15: prompt version changed only in safety instructions, slot still distinct.
Replay 16: chunk fingerprint changed because one appendix heading became a real section.
Replay 17: chunk fingerprint changed because a fenced code block moved across section boundaries.
Replay 18: chunk fingerprint changed because a deterministic chunker was replaced by a whole-section chunker.
Replay 19: corrupted payload ended after key metadata and before extracted facts.
Replay 20: corrupted payload ended after extracted facts and before final closing brace.
Replay 21: temporary file existed after interrupted write and had to be ignored.
Replay 22: temporary file existed after interrupted write and had to be removed after successful recovery.
Replay 23: destination file existed but deserialize step surfaced a JSON exception.
Replay 24: destination file existed and deserialize succeeded, but key comparison failed.
Replay 25: destination file existed, key comparison succeeded, and evidence note recorded a clean reuse.
Replay 26: release packet referenced the prior broken payload for incident forensics.
Replay 27: rewrite note attached the first mismatching chunk fingerprint in order.
Replay 28: rewrite note attached the old and new prompt version identifiers.
Replay 29: rewrite note attached the old and new model identifiers.
Replay 30: rewrite note attached the old and new document identities.
Replay 31: rewrite note attached the old and new source paths.
Replay 32: rewrite note attached the old and new chunker profiles.
Replay 33: rewrite note attached the evidence path for the corrupted payload archive.
Replay 34: rewrite note confirmed the new destination file reopened cleanly.
Replay 35: rewrite note confirmed the new entry matched the active request key exactly.

### Recovery Checklist Trace

| Case | Outcome |
| --- | --- |
| same source path, different document id | distinct slot required |
| same document id, different prompt version | miss and rewrite |
| same prompt version, different model id | miss and rewrite |
| same model id, different chunker profile | miss and rewrite |
| valid JSON, wrong key | explicit miss |
| broken JSON payload | explicit failure with evidence |
| complete temp file, failed final move | retry write path |
| reopened destination matches key | cache hit on next read |

```text
collision-replay-log:
01 sourcePath=playbooks/extraction-operations-workbook.md documentId=/workbook/ prompt=extract-rdf-v1 model=large-chat-model-a decision=hit
02 sourcePath=playbooks/extraction-operations-workbook.md documentId=/workbook-alt/ prompt=extract-rdf-v1 model=large-chat-model-a decision=distinct-slot-required
03 sourcePath=playbooks/extraction-operations-workbook.md documentId=/workbook/ prompt=extract-rdf-v2 model=large-chat-model-a decision=rewrite
04 sourcePath=playbooks/extraction-operations-workbook.md documentId=/workbook/ prompt=extract-rdf-v1 model=large-chat-model-b decision=rewrite
05 sourcePath=playbooks/extraction-operations-workbook.md documentId=/workbook/ prompt=extract-rdf-v1 model=large-chat-model-a chunker=deterministic-section-v1 decision=rewrite
06 sourcePath=playbooks/extraction-operations-workbook.md payload=corrupted-json decision=fail-explicitly
07 sourcePath=playbooks/extraction-operations-workbook.md payload=temp-only decision=retry-write
08 sourcePath=playbooks/extraction-operations-workbook.md payload=reopened-clean decision=verified
```
