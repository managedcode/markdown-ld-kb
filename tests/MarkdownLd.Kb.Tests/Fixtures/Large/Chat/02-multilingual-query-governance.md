---
title: Multilingual Query Governance
canonical_url: https://large-fixture.example/playbooks/multilingual-query-governance/
summary: Guide cross-language recall, read-only SPARQL translation, semantic fallback, and release evidence for multilingual search flows.
authors:
  - Marta Levytska
  - Jules Martin
tags:
  - multilingual
  - sparql
  - semantic
  - release
about:
  - Cross-Language Recall Map
  - Read Only Query Guard
entity_hints:
  - Hybrid Semantic Fallback
  - Release Evidence Checklist
---
# Cross-Language Recall

The recall review maps Ukrainian and English paraphrases to the same workflow title so operators can explain why a hybrid match was accepted.
It keeps a cross-language recall map together with the caller's exact wording and the final graph node that won.

Cross-language recall ledger:

- Keep the original Ukrainian question even when the final release note uses English.
- Keep the transliterated variant if the operator copied the wording into a different system.
- Keep the exact workflow title that won the routing decision.
- Keep the second-place candidate when the final choice required semantic fallback.
- Keep the final explanation in plain language for reviewers who did not inspect the raw search trace.
- Keep the note about whether lexical search alone was sufficient.
- Keep the note about whether a billing or alerts synonym accidentally polluted the match.
- Keep the note about whether the same graph node also won on a later English paraphrase.

```text
recall-ledger:
query="у мене проблема з кешем і треба відновити правильний workflow"
winner="Cache Recovery Workflow"
runnerUp="Semantic Search Tuning"
reason="semantic fallback reinforced cache language without changing the graph node"
query="show me the read only sparql rules for remote endpoint review"
winner="Query Federation Runbook"
runnerUp="Release Gate Checklist"
reason="title and summary were already sufficient"
```

## Read-Only SPARQL Safety

The safety review checks that the generated query is still read only, that string literals containing DELETE are not rejected by mistake, and that the read only query guard stays active.
Every approved prompt also records the federated endpoint allowlist that was in force during the decision.

Read-only safety workbook:

- Record the final generated SPARQL.
- Record the parser outcome and the safety outcome separately.
- Record whether DELETE, INSERT, or LOAD appeared only inside quoted text.
- Record whether comments contained mutating terms that were correctly ignored.
- Record whether the endpoint allowlist still matched the release packet.
- Record whether the final answer exposed the safe query to a caller or only used it internally.

```json
{
  "safetyExamples": [
    {
      "query": "ASK WHERE { ?s ?p \"DELETE literal\" . }",
      "expectedOutcome": "allowed",
      "reason": "mutating term appears only in a string literal"
    },
    {
      "query": "# DELETE comment only\nSELECT * WHERE { ?s ?p ?o }",
      "expectedOutcome": "allowed",
      "reason": "mutating term appears only in a comment"
    },
    {
      "query": "DELETE WHERE { ?s ?p ?o }",
      "expectedOutcome": "rejected",
      "reason": "query is mutating"
    }
  ]
}
```

## Semantic Fallback

The fallback review compares lexical search against hybrid semantic fallback and logs the embedding calibration notes that justified the final threshold.
It highlights cases where multilingual alerts or billing vocabulary still point to the intended workflow after the graph search stage fails.

Semantic fallback workbook:

- Record the threshold used during the review.
- Record which candidate workflow came from canonical graph search.
- Record which candidate workflow appeared only after semantic lookup.
- Record why the final result was merged, kept canonical, or discarded.
- Record whether the vocabulary drift came from alerts, billing, genealogy, or cache operations.
- Record whether the fallback changed only ranking or changed the final primary workflow.

```text
semantic-fallback-trace:
query="multilingual alerts workflow" graph=weak semantic=strong final=Semantic Search Tuning
query="billing synonym near notification text" graph=weak semantic=discarded final=none
query="read only sparql endpoint review" graph=strong semantic=optional final=Query Federation Runbook
```

## Release Evidence

The final release evidence package includes the release evidence checklist, the coverage dashboard snapshot, and the query safety review that justified the shipped prompts.
No multilingual prompt change is approved until the release evidence checklist and rollback evidence ledger are both attached.

Release evidence appendix:

- Keep the checklist id that matched the rehearsal.
- Keep the coverage dashboard timestamp.
- Keep the query safety review identifier.
- Keep the rollback evidence ledger identifier when a prompt changed.
- Keep the exact multilingual example that motivated the prompt update.
- Keep the final approval note and the reviewer name.

```yaml
releaseEvidence:
  checklist: release-evidence-checklist
  coverageSnapshot: coverage-dashboard-snapshot
  querySafetyReview: read-only-query-guard-review
  rollbackLedger: rollback-evidence-ledger
  multilingualExample: "українські запити про cache recovery"
  approvalReviewer: release-review
```
