---
title: Semantic Search Tuning
summary: Tune graph search, lexical recall, and multilingual semantic fallback so support questions still land on the intended workflow.
tags:
  - search
  - semantic
  - multilingual
  - ranking
about:
  - Hybrid Retrieval Panel
  - Cross-Language Recall Map
  - Embedding Calibration Notes
entity_hints:
  - Hybrid Retrieval Panel
  - Cross-Language Recall Map
graph_groups:
  - Search Operations
  - Query Operations
graph_related:
  - https://large-fixture.example/runbooks/query-federation-runbook/
  - https://large-fixture.example/runbooks/graph-ingestion-playbook/
graph_next_steps:
  - https://large-fixture.example/runbooks/release-gate-checklist/
---
# Semantic Search Tuning

The search team balances graph-native labels against semantic fallback so multilingual questions still find the best article title.
Operators record Ukrainian and English paraphrases, inspect missed lexical hits, and only then approve hybrid ranking weights.

article --mentions--> Hybrid Retrieval Panel
Hybrid Retrieval Panel --sameAs--> https://example.com/components/hybrid-retrieval-panel
article --mentions--> Cross-Language Recall Map
Cross-Language Recall Map --sameAs--> https://example.com/components/cross-language-recall-map
article --mentions--> Embedding Calibration Notes

## Recall Analysis

Queries such as "українські запити про cache recovery" or "semantic fallback for multilingual alerts" must still retrieve the intended workflow.
The recall notebook keeps false positives separate from the final hybrid retrieval panel.

The longer recall notebook below gives the suite something closer to a real operating document:

- Track the original user wording even when the final support summary uses cleaner English phrasing.
- Keep Ukrainian, English, and mixed-language paraphrases in the same notebook entry when they refer to the same operational question.
- Separate lexical misses from semantic misses because a graph title problem is different from an embedding problem.
- Record which workflow title won, which candidates came second, and why the team still trusted the final choice.
- Keep billing-oriented vocabulary apart from notification-oriented vocabulary so semantic fallback does not overfit one domain.
- Note whether the match came from title, summary, related labels, or semantic-only recall.
- Preserve the exact threshold that moved a candidate from ignored to merged.
- Keep the failure cases in the appendix instead of deleting them, because real ranking notes accumulate over time.

```yaml
recallNotebook:
  - query: "українські запити про cache recovery"
    language: uk
    intendedWorkflow: Cache Recovery Workflow
    lexicalOutcome: weak
    semanticOutcome: merged
    note: The query shares intent with cache rewrite review even when the exact English title is absent.
  - query: "semantic fallback for multilingual alerts"
    language: en
    intendedWorkflow: Semantic Search Tuning
    lexicalOutcome: partial
    semanticOutcome: merged
    note: Alert vocabulary should not outrank the core search tuning workflow.
  - query: "read only sparql guard for remote endpoint review"
    language: en
    intendedWorkflow: Query Federation Runbook
    lexicalOutcome: strong
    semanticOutcome: optional
    note: Graph-native labels are sufficient when the wording stays near the title.
  - query: "release evidence bundle for graph validation"
    language: en
    intendedWorkflow: Release Gate Checklist
    lexicalOutcome: strong
    semanticOutcome: optional
    note: The release checklist should win without requiring semantic rescue.
```

## Ranking Notes

The graph ranker prefers exact titles and summaries.
Semantic fallback is only merged when the candidate article is still anchored to a real graph node and an explicit search context.

The tuning appendix keeps a richer calibration trace:

| Candidate | Canonical score note | Semantic score note | Final outcome |
| --- | --- | --- | --- |
| Semantic Search Tuning | title and summary matched ranking vocabulary | semantic evidence reinforced the same node | merged |
| Query Federation Runbook | related labels matched part of the query | semantic evidence was secondary | canonical |
| Cache Recovery Workflow | lexical note mentioned cache rewrite but not ranking | semantic evidence was insufficient for primary routing | ignored |
| Release Gate Checklist | release and evidence labels matched directly | semantic signal was optional | canonical |

```text
calibration-trace:
2026-04-05T07:00:00Z query="semantic fallback multilingual workflow" canonical=0.85 semantic=0.62 outcome=merged
2026-04-05T07:11:00Z query="remote endpoint allowlist read only safety" canonical=1.00 semantic=0.22 outcome=canonical
2026-04-05T07:25:00Z query="cache rewrite prompt version change" canonical=0.35 semantic=0.18 outcome=ignored
2026-04-05T07:44:00Z query="release evidence coverage snapshot" canonical=0.85 semantic=0.40 outcome=canonical
2026-04-05T08:02:00Z note="kept multilingual alert vocabulary in appendix to avoid overfitting"
```

```json
{ "mode": "hybrid", "minimumSemanticScore": 0.35 }
```

## Cross-Language Replay Appendix

The appendix stores multilingual and mixed-language recall fragments that are too detailed for the summary but still useful for large-corpus search tests.
It keeps real-looking phrasing variation without forcing the canonical graph surface to index every body fragment.

Replay 01: "ukrainian fallback for cache recovery alerts" should stay attached to the tuning notebook rather than a release checklist.
Replay 02: "semantic rescue for multilingual support wording" should stay attached to the tuning notebook rather than a cache workflow.
Replay 03: "read only sparql guard but ranking still wrong" should still leave query federation ahead of pure semantic notes.
Replay 04: "graph search wins when titles and summaries are already precise" should remain the stable baseline.
Replay 05: "hybrid retrieval only after lexical miss" should remain the policy anchor.
Replay 06: "mixed english and ukrainian support notes" should not collapse into random archive matches.
Replay 07: "french alert paraphrase for multilingual ranking" should stay a replay note, not a title rewrite.
Replay 08: "german support wording for the same workflow" should stay in the replay appendix for later review.
Replay 09: "semantic threshold too low and release notes started to hijack search" should remain a regression example.
Replay 10: "semantic threshold too high and legitimate multilingual matches were dropped" should remain a regression example.
Replay 11: "query about remote endpoint safety but with ranking vocabulary" should still leave federation above tuning.
Replay 12: "query about release evidence with semantic wording" should still leave release gate above tuning.
Replay 13: "query about cache rewrite with multilingual phrasing" may need semantic rescue but should still land on cache recovery.
Replay 14: "query about graph ingestion and deterministic chunking" should not require semantic rescue at all.
Replay 15: "query about vocabulary mismatch between user wording and graph labels" is the core reason this appendix exists.
Replay 16: "змішані запити про cache recovery та alert routing" stays as a body-only replay phrase for lexical stress.
Replay 17: "потрібен гібридний пошук для багатомовних алертів" stays as a body-only replay phrase for lexical stress.
Replay 18: "ranking panel compared semantic and canonical scores" stays as a body-only replay phrase for lexical stress.
Replay 19: "semantic fallback was merged only after the graph candidate remained explainable" stays as a body-only replay phrase.
Replay 20: "hybrid retrieval panel and cross-language recall map were reviewed together" stays as a body-only replay phrase.
Replay 21: "ukrainian support engineer copied the failing query from chat" stays as a body-only replay phrase.
Replay 22: "english reviewer rewrote the incident summary after confirming the original user wording" stays as a body-only replay phrase.
Replay 23: "mixed language terms should not cause release evidence notes to outrank search tuning" stays as a body-only replay phrase.
Replay 24: "semantic rescue cannot replace an absent graph explanation" stays as a body-only replay phrase.
Replay 25: "semantic rescue is not a license for vague titles" stays as a body-only replay phrase.

### Query Replay Grid

| Replay query | Intended owner |
| --- | --- |
| українські запити про cache recovery | Semantic Search Tuning |
| semantic fallback for multilingual alerts | Semantic Search Tuning |
| read only sparql guard for remote endpoint review | Query Federation Runbook |
| release evidence bundle for graph validation | Release Gate Checklist |
| mixed-language alert route paraphrase | Semantic Search Tuning |
| deterministic chunk boundaries for graph build | Graph Ingestion Playbook |
| prompt version drift after cache rewrite | Cache Recovery Workflow |
| rollback evidence timeline during production incident | Incident Triage Guide |

```text
cross-language-log:
2026-04-05T08:12:00Z query="українські запити про cache recovery" lexical=weak semantic=merged owner="Semantic Search Tuning"
2026-04-05T08:16:00Z query="semantic fallback for multilingual alerts" lexical=partial semantic=merged owner="Semantic Search Tuning"
2026-04-05T08:19:00Z query="read only sparql guard for remote endpoint review" lexical=strong semantic=optional owner="Query Federation Runbook"
2026-04-05T08:23:00Z query="release evidence bundle for graph validation" lexical=strong semantic=optional owner="Release Gate Checklist"
2026-04-05T08:28:00Z note="kept the original mixed-language user wording in the appendix"
2026-04-05T08:34:00Z note="did not turn the appendix itself into canonical graph metadata"
```
