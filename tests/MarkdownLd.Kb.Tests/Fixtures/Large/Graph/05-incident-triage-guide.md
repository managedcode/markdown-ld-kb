---
title: Incident Triage Guide
summary: Coordinate rollback evidence, query safety review, and cache diagnostics during production incidents.
tags:
  - incident
  - triage
  - rollback
  - diagnostics
about:
  - Rollback Evidence Ledger
  - Query Safety Review
  - Hotfix Timeline
entity_hints:
  - Rollback Evidence Ledger
  - Hotfix Timeline
graph_groups:
  - Reliability Operations
  - Release Operations
graph_related:
  - https://large-fixture.example/runbooks/cache-recovery-workflow/
  - https://large-fixture.example/runbooks/query-federation-runbook/
graph_next_steps:
  - https://large-fixture.example/runbooks/release-gate-checklist/
---
# Incident Triage Guide

The triage guide keeps rollback evidence, query safety review notes, and a short hotfix timeline for every runtime incident.
It explains when to isolate cache corruption from ranking regressions and when to escalate a federated endpoint investigation.

article --mentions--> Rollback Evidence Ledger
Rollback Evidence Ledger --sameAs--> https://example.com/components/rollback-evidence-ledger
article --mentions--> Query Safety Review
article --mentions--> Hotfix Timeline

## First Fifteen Minutes

Capture the failing query, the latest cache directory audit, and the exact article titles that the caller expected to see.
Do not rewrite graph facts until the rollback evidence ledger and prompt version gate are both attached to the incident thread.

The triage packet keeps a dense incident notebook so the document behaves like a real support manual:

- Record the first caller-visible symptom in the exact words reported by the support engineer.
- Record the expected workflow title and the workflow title that actually surfaced.
- Record the latest safe query revision used by the caller or agent.
- Record whether the cache directory audit already showed stale or conflicting entries.
- Record whether the graph mismatch appeared only in ranked search or also in direct SPARQL lookup.
- Record whether the incident started after a release checklist was approved or while the release was still in rehearsal.
- Record whether a rollback was possible without changing markdown source documents.
- Record whether the prompt version gate changed during the incident window.
- Record whether the failure involved a remote endpoint, a parser change, or a cache collision.
- Record whether the evidence package already contains the offending prompt and chunk source URI.
- Record whether the incident only affected multilingual recall or also harmed exact title lookup.

```yaml
incidentNotebook:
  startedAt: 2026-04-07T11:03:00Z
  workflowExpectation:
    expectedTitle: Cache Recovery Workflow
    actualTitle: Query Federation Runbook
  evidence:
    - rollback-evidence-ledger
    - cache-directory-audit
    - query-safety-review
    - hotfix-timeline
  firstQuestions:
    - Did the prompt version gate change?
    - Did the cache slot collapse two article identities?
    - Did the read-only guard reject or allow the wrong query?
    - Did the release checklist capture the previous baseline?
```

## Escalation Rules

- Escalate query safety review when a read-only guard allows a mutating verb.
- Escalate cache recovery when two canonical article identities collapse into one on-disk slot.
- Escalate release review when the coverage dashboard is stale.

Escalation trace:

| Trigger | Owner |
| --- | --- |
| wrong workflow won ranked search | graph-oncall |
| safe query rejected incorrectly | query-review |
| cache collision for same source path | cache-review |
| multilingual recall regressed | search-tuning |
| release evidence missing | release-review |
| coverage dashboard stale | release-review |
| rollback note absent | incident-commander |

```text
triage-timeline:
11:03 symptom reported by support engineer
11:07 expected and actual workflow titles recorded
11:12 rollback evidence ledger opened
11:18 cache directory audit attached
11:24 prompt version gate compared with previous baseline
11:31 query safety review linked
11:44 release checklist consulted
11:58 escalation path selected
12:05 hotfix timeline updated
12:21 final reviewer note captured
```

## Triage Replay Appendix

The appendix keeps dense incident replay notes that are useful for realistic retrieval tests and for stress-checking large operational documents.

Replay 01: support engineer searched for rollback evidence before the formal incident title.
Replay 02: support engineer searched for prompt version gate before the formal incident title.
Replay 03: support engineer searched for cache directory audit before the formal incident title.
Replay 04: support engineer searched for wrong workflow won ranked search before the formal incident title.
Replay 05: support engineer searched for query safety review before the formal incident title.
Replay 06: support engineer searched for release evidence missing before the formal incident title.
Replay 07: the triage packet linked cache diagnostics and query safety notes in one timeline.
Replay 08: the triage packet linked rollback evidence and release review notes in one timeline.
Replay 09: the first responder recorded the exact expected title and actual title mismatch.
Replay 10: the first responder recorded whether the failure lived in graph search or direct SPARQL.
Replay 11: the timeline stayed open until the prompt version gate comparison was attached.
Replay 12: the timeline stayed open until the cache directory audit was attached.
Replay 13: the timeline stayed open until the rollback evidence ledger was attached.
Replay 14: the timeline stayed open until the release checklist reference was attached.
Replay 15: the timeline stayed open until the final hotfix note was attached.
Replay 16: the incident commander kept the original user wording instead of rewriting the symptom.
Replay 17: the graph on-call engineer asked whether the cache slot collapsed two article identities.
Replay 18: the query reviewer asked whether the read-only guard rejected the wrong query.
Replay 19: the release reviewer asked whether the prior baseline snapshot was still trustworthy.
Replay 20: the support engineer asked whether the multilingual recall miss was actually a ranking miss.

| Replay trigger | Escalation |
| --- | --- |
| wrong workflow won | graph-oncall |
| cache slot collapsed two identities | cache-review |
| safe query rejected incorrectly | query-review |
| release evidence packet missing | release-review |
| rollback note absent | incident-commander |

```text
triage-replay-log:
12:26 query="wrong workflow won ranked search" owner=graph-oncall
12:29 query="cache slot collapsed two article identities" owner=cache-review
12:33 query="safe query rejected incorrectly" owner=query-review
12:38 query="release evidence packet missing" owner=release-review
12:44 note="kept replay terms in the appendix for search stress"
```
