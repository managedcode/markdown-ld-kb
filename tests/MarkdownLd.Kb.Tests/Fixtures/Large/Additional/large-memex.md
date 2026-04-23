---
title: Large Markdown Memex
canonicalUrl: https://kb.example/large-memex/
description: Large markdown extraction summary.
tags:
  - memex
  - retrieval
author:
  - label: Ada Lovelace
    type: schema:Person
about:
  - Knowledge Graph
entity_hints:
  - label: Entity Extractor
    type: SoftwareApplication
    sameAs:
      - https://example.com/entity-extractor
---
Intro before heading with [[SPARQL]] and [dotNetRDF](https://dotnetrdf.org/).
The memex intake guide records how a document corpus becomes a queryable RDF graph instead of a pile of notes.
Operators review entity hints, chunk boundaries, provenance rules, and graph shape validation before the first index build.

# Knowledge Graph Intake

Large Markdown Memex --mentions--> Entity Extractor
article --mentions--> RDF
Entity Extractor --kb:relatedTo--> SPARQL

The intake stage starts with raw Markdown runbooks, architectural notes, incident ledgers, and release checklists.
Each document keeps source-relative identity so the graph can preserve both document-level and section-level provenance.
The pipeline prefers deterministic parsing because every graph build must be reproducible when the same corpus is processed twice.

## Source Inventory

The memex ingests runbooks, RFC notes, onboarding guides, schema references, and multilingual operational logs.
Every source keeps a canonical document identifier, an optional canonical URL, and stable section paths so later graph merges do not rewrite identity by accident.
Analysts inspect broken links, duplicate titles, and empty sections before extraction starts because silent substitution would poison the graph.

## Front Matter Mapping

Front matter is not treated as decorative metadata.
Tags become searchable keywords, authors become graph nodes, and `about` terms become concept references that drive ontology and SKOS enrichment.
The same front matter also seeds entity hints so a document can state that Entity Extractor is a `schema:SoftwareApplication` with a stable sameAs target.

## Canonical Identity

The graph builder normalizes document IDs, entity IDs, and predicate names before any triples are asserted.
That identity pass prevents the same concept from appearing as both `dotNetRDF`, `dotnetrdf`, and a raw URL in separate documents.
The canonical model is especially important when a federated query later joins local facts with external identifiers.

# Chunk Strategy

Chunking is intentional rather than accidental.
Large sections are cut into deterministic slices so the same paragraph will keep the same chunk fingerprint after unrelated edits in another part of the file.
Smaller sections stay whole because splitting a concise operational step would only dilute recall.

## Section Boundaries

Section headings define semantic scope.
The parser keeps both the heading tree and the local text body because callers often want to know not just what matched but where in the operational document the match came from.
This is why the memex favors section-aware chunking over a naive token window that ignores author structure.

## Chunk Fingerprints

Chunk fingerprints matter during cache reuse and provenance review.
If a document is edited, the cache can invalidate only the changed chunks instead of reprocessing the whole corpus.
That saves cost for chat-based extraction and makes regression review much easier when only one subsection changed.

## Body-Only Evidence

Some information should be searchable but not promoted to canonical graph identity.
An appendix might mention a remote endpoint, a release identifier, or a quoted support transcript that is valuable for token-distance search but not for top-level graph ranking.
That distinction lets the graph stay precise while the token index still recovers deep evidence from large documents.

# Extraction Semantics

The memex supports both deterministic graph rules and richer extraction modes.
Simple document facts such as `article --mentions--> RDF` can be parsed without a model, while larger semantic cues may come from tokenized or chat-backed extraction.
The important boundary is that every extracted fact still becomes an explicit caller-visible triple or visible skipped fact.

## Entity Hints

Entity hints provide a bridge between raw Markdown and domain ontology.
They let a document say that Entity Extractor is real, typed, and linked before body text starts to mention it in different phrasings.
This reduces alias drift and gives the graph a stronger base for inference.

## Predicate Normalization

Predicate normalization keeps query behavior sane.
Writers may use `mentions`, `schema:mentions`, or a mixed-case variant, but the graph should still settle on one canonical predicate so SPARQL queries remain readable.
That same normalization logic keeps `kb:relatedTo` and `kb:nextStep` coherent across the corpus.

## Duplicate Merge Policy

Facts from front matter, body rules, token extraction, and chat extraction can collide.
The merge policy must prefer the stronger type, keep the best sameAs links, and rewrite assertions so all edges point at the canonical entity.
Otherwise, the graph would look larger while actually becoming less coherent.

# Query Model

Once the graph exists, callers need several ways to interrogate it.
Some flows want exact SPARQL, some want graph-ranked search, some want focused search with related and next-step nodes, and some want token-distance recovery from dense body text.
The memex is valuable only if those query paths agree on identity.

## Read-Only SPARQL

The graph accepts only read-only SPARQL query forms.
`SELECT` and `ASK` are enough for library callers, while mutating forms stay blocked because the in-memory runtime is a query surface, not a writable triple store.
That safety line is especially important when external federation is enabled.

## Federated Queries

Federated queries are useful when local graph facts must be checked against remote identifiers or external concept stores.
A local document may mention [[SPARQL]] and also point at [dotNetRDF](https://dotnetrdf.org/), while a federated query brings in additional labels from an external knowledge source.
The local runtime therefore supports explicit federation flows instead of silent network access during ordinary queries.

## Token Distance Search

Token-distance search exists for the cases where graph identity alone is not enough.
A query may remember "endpoint whitelist audit" or "histogram rebucketing threshold" without remembering the exact document title.
In those cases, the token index should recover the best section while the graph stays sparse and high precision.

## Focused Search

Focused search starts from a primary match and then expands into related and next-step context.
This is useful for operational workflows because the first hit is rarely the full answer.
Callers often need the neighboring mitigation note, the release gate checklist, or the incident handoff document right after the first match.

# Ontology And Taxonomy

A memex graph is more valuable when it can project both formal ontology and practical taxonomy.
Formal ontology declares what a Markdown document, concept, concept scheme, and assertion mean.
Taxonomy then organizes authored concepts into usable schemes and labels.

## Ontology Layer

The ontology layer states that a Markdown document is still an article-shaped knowledge asset.
It also defines the custom terms that the library owns, such as `kb:MarkdownDocument`, `kb:KnowledgeConcept`, and `kb:KnowledgeConceptScheme`.
Those declarations make inference and SHACL validation much easier to reason about.

## SKOS Layer

The SKOS layer turns authored concepts into a concept scheme with labels and exact matches.
It is the practical bridge from prose topics to a browsable taxonomy that applications can render or traverse.
When `about: Knowledge Graph` appears in front matter, the graph should be able to project that into a stable SKOS concept.

## Inference

Inference should remain explicit.
Hosts may want a materialized graph where ontology and taxonomy types produce extra triples, but the default build must stay deterministic and cheap.
That is why inference is a follow-up operation rather than a hidden side effect of parsing Markdown.

# Operational Safeguards

The memex also captures what can go wrong.
Large corpora can produce expensive graph builds, misleading aliases, malformed sameAs values, or body-only evidence that should not become top-level graph identity.
These risks need explicit checks rather than optimistic assumptions.

## SHACL Validation

SHACL validates that entity names exist, sameAs targets are IRIs, and assertion metadata is structurally sound when that metadata is requested.
This turns bad graph construction into a visible report instead of a silent quality regression.
It also makes it easier to debug caller-authored graph rules.

## Throughput Guardrails

Large Markdown files should not become pathological just because they contain many sections.
The build path therefore needs efficient node reuse, lean semantic-layer scans, and optional assertion reification instead of doing the most expensive thing by default.
The service will only feel usable if large documents are still fast enough to rebuild during normal development.

## Failure Review

When a build or query behaves strangely, reviewers inspect the exact Markdown source, the extracted facts, the graph serialization, and the query result together.
That flow is only possible when the library preserves enough provenance and does not hide invalid inputs behind fallback magic.
The memex is most trustworthy when failures stay inspectable.

# Release Notes

Before shipping a new build, the team checks that parser behavior, token-distance search, SHACL validation, and federated query boundaries all still behave as expected.
Coverage and integration flow tests are part of that release gate because this library is only useful if the end-to-end Markdown to RDF to query path stays stable.
The final review also confirms that Entity Extractor, RDF, SPARQL, and [dotNetRDF](https://dotnetrdf.org/) still materialize into the graph the way callers expect.
