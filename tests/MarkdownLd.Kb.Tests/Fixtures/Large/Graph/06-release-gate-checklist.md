---
title: Release Gate Checklist
summary: Verify build, test, coverage, cache directory audit, and deterministic graph snapshots before a release is approved.
tags:
  - release
  - checklist
  - coverage
  - validation
about:
  - Coverage Dashboard
  - Deterministic Snapshot Report
  - Cache Directory Audit
entity_hints:
  - Coverage Dashboard
  - Deterministic Snapshot Report
graph_groups:
  - Release Operations
  - Graph Operations
graph_related:
  - https://large-fixture.example/runbooks/graph-ingestion-playbook/
  - https://large-fixture.example/runbooks/incident-triage-guide/
---
# Release Gate Checklist

The final gate verifies deterministic graph snapshots, coverage dashboards, and cache directory audits before the package ships.
Each checklist entry maps back to the exact runbook that produced the evidence.

article --mentions--> Coverage Dashboard
Coverage Dashboard --sameAs--> https://example.com/components/coverage-dashboard
article --mentions--> Deterministic Snapshot Report
article --mentions--> Cache Directory Audit

## Approval Notes

1. Build the full graph corpus and compare the deterministic snapshot report against the previous baseline.
2. Run the release checklist after incident triage, cache recovery, and query federation notes are green.
3. Reject the release when the coverage dashboard or cache directory audit is missing.

The approval packet intentionally includes a long checklist so the fixture looks like a real release workbook:

- Verify that the graph ingestion playbook still maps to the expected article URI.
- Verify that the query federation runbook still keeps read-only safety material in the search context.
- Verify that the cache recovery workflow still explains distinct document identity and source path cases.
- Verify that the semantic search tuning note still contains multilingual recall examples.
- Verify that the incident triage guide still points at rollback evidence and release review.
- Verify that the coverage dashboard snapshot belongs to the same rehearsal.
- Verify that the deterministic snapshot report was regenerated after the latest markdown edits.
- Verify that the cache directory audit reflects the same model id and prompt version expected for the run.
- Verify that temporary files are not left behind after cache writes.
- Verify that the evidence bundle references the exact commands used for build, test, and validation.
- Verify that residual risks are explicitly written down instead of silently assumed.

```json
{
  "releaseGate": {
    "requiredArtifacts": [
      "coverage-dashboard",
      "deterministic-snapshot-report",
      "cache-directory-audit",
      "query-safety-review",
      "rollback-evidence-ledger"
    ],
    "blockingConditions": [
      "missing artifact",
      "stale baseline",
      "unexplained cache collision",
      "unsafe read-only query decision",
      "coverage regression"
    ]
  }
}
```

## Evidence Bundle

The evidence bundle keeps command output, selected SPARQL queries, and a short explanation of any remaining residual risk.

Evidence inventory:

| Artifact | Why the reviewer wants it |
| --- | --- |
| coverage dashboard | proves tests still cover the critical flow |
| deterministic snapshot report | proves the graph did not drift unexpectedly |
| cache directory audit | proves cache writes and keying stayed sane |
| selected SPARQL queries | proves caller-visible graph shape |
| focused search screenshots | proves related and next-step context stayed understandable |
| residual risk note | keeps unresolved concerns explicit |

```text
release-evidence-bundle:
artifact=coverage-dashboard status=required
artifact=deterministic-snapshot-report status=required
artifact=cache-directory-audit status=required
artifact=query-safety-review status=required
artifact=rollback-evidence-ledger status=required
artifact=focused-search-evidence status=recommended
artifact=residual-risk-note status=required
```

## Release Evidence Replay Appendix

The appendix keeps the repetitive rollout evidence language that tends to accumulate near the end of real release checklists.
It expands the corpus with another dense operational tail without changing the canonical title or summary contract.

Replay 01: reviewer searched for coverage dashboard before the formal checklist title.
Replay 02: reviewer searched for deterministic snapshot report before the formal checklist title.
Replay 03: reviewer searched for cache directory audit before the formal checklist title.
Replay 04: reviewer searched for rollback evidence ledger before the formal checklist title.
Replay 05: reviewer searched for release packet diff before the formal checklist title.
Replay 06: reviewer searched for prompt version gate before the formal checklist title.
Replay 07: reviewer searched for rollout packet approval before the formal checklist title.
Replay 08: reviewer searched for evidence bundle completeness before the formal checklist title.
Replay 09: reviewer searched for prior baseline snapshot before the formal checklist title.
Replay 10: reviewer searched for final gate confirmation before the formal checklist title.
Replay 11: the checklist recorded whether the coverage dashboard was current.
Replay 12: the checklist recorded whether the deterministic snapshot matched the previous run.
Replay 13: the checklist recorded whether the cache directory audit matched the final corpus.
Replay 14: the checklist recorded whether the rollback evidence ledger was attached.
Replay 15: the checklist recorded whether the final rollout packet referenced the same graph revision.
Replay 16: the checklist recorded whether the reviewer re-opened the evidence packet after a late edit.
Replay 17: the checklist recorded whether the release packet diff explained every last-minute change.
Replay 18: the checklist recorded whether the prompt version gate stayed unchanged during sign-off.
Replay 19: the checklist recorded whether the coverage export matched the final build.
Replay 20: the checklist recorded whether the rollout packet stayed blocked until every gate passed.

| Evidence item | Gate outcome |
| --- | --- |
| coverage dashboard | required |
| deterministic snapshot report | required |
| cache directory audit | required |
| rollback evidence ledger | required |
| release packet diff | required |

```text
release-replay-log:
09:02 query="coverage dashboard" outcome="checklist evidence route"
09:06 query="deterministic snapshot report" outcome="checklist evidence route"
09:11 query="cache directory audit" outcome="checklist evidence route"
09:15 query="rollback evidence ledger" outcome="checklist evidence route"
09:19 note="kept late-stage review wording in the appendix for search stress"
```

Replay tail note 21: the checklist still documented which evidence item blocked the rollout.
Replay tail note 22: the checklist still documented which evidence item unblocked the rollout.
Replay tail note 23: the checklist still documented which reviewer reopened the packet.
Replay tail note 24: the checklist still documented which graph revision was approved.
Replay tail note 25: the checklist still documented which cache audit matched the release.
Replay tail note 26: the checklist still documented which snapshot report matched the release.
Replay tail note 27: the checklist still documented which coverage export matched the release.
Replay tail note 28: the checklist still documented which residual risk remained after approval.
