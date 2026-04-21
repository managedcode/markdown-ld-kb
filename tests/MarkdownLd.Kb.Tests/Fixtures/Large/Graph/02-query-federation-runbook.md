---
title: Query Federation Runbook
summary: Review read-only SPARQL, remote endpoints, and provenance constraints before enabling federated lookup.
tags:
  - query
  - sparql
  - federation
  - provenance
about:
  - SPARQL
  - Provenance Audit
  - Remote Endpoint Allowlist
entity_hints:
  - Read Only Query Guard
  - Remote Endpoint Allowlist
graph_groups:
  - Query Operations
  - Reliability Operations
graph_related:
  - https://large-fixture.example/runbooks/graph-ingestion-playbook/
  - https://large-fixture.example/runbooks/semantic-search-tuning/
graph_next_steps:
  - https://large-fixture.example/runbooks/incident-triage-guide/
  - https://large-fixture.example/runbooks/release-gate-checklist/
---
# Query Federation Runbook

The runbook explains how operators keep read-only query safety while still inspecting remote graphs and provenance signals.
It compares endpoint allowlists, graph snapshots, and caller-visible query diagnostics before any federated query is approved.

article --mentions--> Read Only Query Guard
Read Only Query Guard --sameAs--> https://example.com/components/read-only-query-guard
article --mentions--> Remote Endpoint Allowlist
Remote Endpoint Allowlist --sameAs--> https://example.com/components/remote-endpoint-allowlist
article --mentions--> Provenance Audit

## Federated Endpoint Review

The review board records the remote endpoint allowlist beside the schema mapping contract and the last successful release evidence bundle.
Every remote service must be referenced by a durable URI and a short operational note.

The endpoint notebook contains long-form operational detail because real runbooks accumulate many near-duplicate reviews over time:

- Endpoint owners record the transport, authentication, throttling budget, and fallback expectations for each remote dataset.
- The allowlist notes whether the endpoint is used only for diagnostics or whether it participates in a caller-visible ranking flow.
- Each remote graph receives a short explanation of why local triples were insufficient for the question that triggered federation.
- A review note captures the last prompt version that generated a safe query for the endpoint.
- Reviewers track whether the endpoint was consulted through a manual SPARQL notebook, a library integration test, or a production support investigation.
- The on-call engineer writes down whether the remote schema aligns with the local `schema.org` and `kb` vocabulary assumptions.
- If the query references literals such as `DELETE` inside examples, the review explains why the safety layer still treated the request as read only.
- Each approval references the provenance audit that was attached to the final incident or release bundle.
- When the endpoint is removed from the allowlist, the notebook keeps the decommission reason rather than silently forgetting the integration ever existed.
- Remote timeout behavior is documented because a logically valid query may still be operationally unsafe if it hangs in caller flows.
- The query team distinguishes between an article that describes federation and an article that merely mentions a remote endpoint as background context.

```json
{
  "endpointReviews": [
    {
      "id": "wikidata-reference",
      "uri": "https://query.wikidata.org/sparql",
      "purpose": "background vocabulary lookup",
      "allowedVerbs": ["SELECT", "ASK"],
      "rejectionReasons": [
        "mutating query type",
        "missing provenance note",
        "unsafe endpoint ownership",
        "undocumented timeout budget"
      ]
    },
    {
      "id": "internal-catalog-mirror",
      "uri": "https://catalog.example/sparql",
      "purpose": "release diagnostics only",
      "allowedVerbs": ["SELECT"],
      "rejectionReasons": [
        "caller-visible routing decision",
        "schema drift without mapping",
        "missing allowlist approval"
      ]
    }
  ],
  "reviewQuestions": [
    "Does the generated query stay read only after prefix expansion?",
    "Does every remote identifier have a caller-visible explanation?",
    "Would a timeout still let the local graph answer the minimal question?",
    "Is the provenance audit preserved in the final release evidence package?"
  ]
}
```

## Query Safety Checklist

1. Ensure the candidate query is `SELECT` or `ASK`.
2. Reject `DELETE`, `INSERT`, `LOAD`, or `CLEAR` when they appear as executable verbs.
3. Capture a provenance audit trail for each remote endpoint that participates in the lookup.

The safety checklist also keeps a detailed ledger so long documents exercise prompt extraction and search over dense text:

| Checkpoint | Reviewer note |
| --- | --- |
| Prefix expansion reviewed | Prefixes were expanded in the dry-run notebook before final approval. |
| String literal inspected | Literal examples containing DELETE were confirmed to be quoted data, not executable verbs. |
| Comment handling reviewed | Comment lines were ignored by the read-only safety scan. |
| Endpoint ownership confirmed | The endpoint owner and on-call rotation were attached to the review. |
| Provenance path recorded | Every remote binding was tied back to a provenance audit note. |
| Retry budget limited | The query notebook documented retry and timeout expectations. |
| Release note linked | The release evidence package referenced the exact safe query revision. |

```text
federation-audit-log:
2026-04-02T08:00:00Z reviewer=query-oncall action=draft-safe-select note="expanded prefixes and checked comments"
2026-04-02T08:14:00Z reviewer=query-oncall action=review-string-literals note="DELETE found only inside a quoted example"
2026-04-02T08:26:00Z reviewer=security-review action=endpoint-ownership note="allowlist entry linked to owner and incident contact"
2026-04-02T08:41:00Z reviewer=release-review action=provenance-note note="release evidence package references provenance audit"
2026-04-02T08:55:00Z reviewer=graph-team action=approved note="safe for diagnostics and bounded support scenarios"
```

```ttl
<https://large-fixture.example/runbooks/query-federation-runbook/>
    <https://schema.org/about> <https://large-fixture.example/id/remote-endpoint-allowlist> .
```

## Endpoint Review Transcript Appendix

The appendix keeps redacted, reviewer-written notes that look much closer to a real runbook tail than a toy example.
It exists so token-distance search can work through dense operational prose, endpoint URIs, quoted read-only guidance, and repeated provenance language without making the canonical graph depend on arbitrary body text.

Review 01: reviewer noted that `https://query.wikidata.org/sparql` remained allowed only for bounded vocabulary lookups.
Review 02: reviewer noted that `https://catalog.example/sparql` stayed diagnostics-only and never participated in caller-visible routing.
Review 03: reviewer noted that the provenance audit note must survive export into the release evidence bundle.
Review 04: reviewer noted that the remote endpoint allowlist and the query safety review must be attached together.
Review 05: reviewer noted that a quoted `"DELETE"` token in transcript data did not make the generated query mutating.
Review 06: reviewer noted that a quoted `"INSERT"` token in transcript data did not make the generated query mutating.
Review 07: reviewer noted that the dry-run notebook stored the final `SELECT` candidate beside the original endpoint owner note.
Review 08: reviewer noted that the support engineer copied the endpoint URI from a ticket, not from a generated prompt.
Review 09: reviewer noted that the provenance audit remained the deciding document when two remote datasets overlapped.
Review 10: reviewer noted that timeout budgets were stricter for caller-visible support tooling than for offline diagnostics.
Review 11: reviewer noted that the allowlist review referenced both schema drift and owner drift.
Review 12: reviewer noted that comment lines in the SPARQL draft were ignored by the safety scan.
Review 13: reviewer noted that literal examples containing the word `CLEAR` stayed inside explanation text only.
Review 14: reviewer noted that literal examples containing the word `LOAD` stayed inside explanation text only.
Review 15: reviewer noted that the remote endpoint explanation named the exact reason local triples were insufficient.
Review 16: reviewer noted that provenance notes were attached before the endpoint notebook was approved.
Review 17: reviewer noted that the bounded lookup explanation was visible to the release reviewer.
Review 18: reviewer noted that the endpoint owner, on-call contact, and timeout ceiling stayed in one review cell.
Review 19: reviewer noted that a federated query with safe verbs could still fail operationally if the timeout budget was absent.
Review 20: reviewer noted that an archived endpoint review must not silently disappear from the notebook.
Review 21: reviewer noted that the endpoint URI must stay searchable in deep lexical modes for transcript forensics.
Review 22: reviewer noted that canonical graph search must still prefer the runbook title, summary, and entity labels.
Review 23: reviewer noted that the local graph remained authoritative even when remote review notes were long.
Review 24: reviewer noted that allowlist removal reasons were kept as human-readable prose rather than short codes.
Review 25: reviewer noted that diagnostic-only endpoints could not influence final ranked search decisions.
Review 26: reviewer noted that provenance audit text was copied into a release packet as a final approval guard.
Review 27: reviewer noted that owner confirmation was re-run after the endpoint certificate changed.
Review 28: reviewer noted that a retry budget without a provenance note was rejected.
Review 29: reviewer noted that a provenance note without owner confirmation was rejected.
Review 30: reviewer noted that a safe query revision without timeout evidence was rejected.

### Remote Schema Delta Matrix

| Delta | Local interpretation | Review action |
| --- | --- | --- |
| missing schema:name label | local graph cannot explain bindings | reject until mapping note exists |
| endpoint owner changed | prior allowlist approval is stale | re-run owner review |
| timeout ceiling raised | bounded diagnostics assumption changed | re-run operational review |
| provenance note absent | caller cannot explain remote evidence | reject |
| comment lines contain quoted verbs | safety scan should ignore comments | record the example only |
| endpoint removed from allowlist | archive the review, keep the reason | mark historical |
| endpoint reused for ranking | diagnostics-only contract violated | reject |
| remote graph renamed a field | mapping drift becomes caller-visible | open schema review |
| prompt revision changed | notebook evidence may no longer align | re-run dry run |
| release packet missing | approval chain is incomplete | block federation |

```text
endpoint-review-log:
2026-04-02T09:04:00Z endpoint=https://query.wikidata.org/sparql note="background vocabulary lookup kept read only"
2026-04-02T09:08:00Z endpoint=https://catalog.example/sparql note="diagnostics only, not caller visible"
2026-04-02T09:12:00Z note="quoted DELETE stayed in transcript data only"
2026-04-02T09:16:00Z note="provenance audit attached before owner approval"
2026-04-02T09:19:00Z note="timeout ceiling rechecked after certificate rollover"
2026-04-02T09:23:00Z note="schema drift required a fresh mapping review"
2026-04-02T09:28:00Z note="allowlist removal reason archived for future incident review"
2026-04-02T09:34:00Z note="bounded diagnostics contract preserved"
```
