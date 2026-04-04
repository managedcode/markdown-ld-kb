# Low-Cost Knowledge Bank on GitHub: Markdown Articles → Linked Data → Serverless SPARQL on Azure

## Executive summary

This design treats a Git repo as the *canonical knowledge source* (human-authored Markdown) and produces a *versioned, queryable knowledge graph* (JSON-LD/Turtle) as a build artifact committed back into the repo for ultra-low serving costs. Azure Static Web Apps (SWA) can host the site cheaply (Free plan includes static hosting features) while Azure Functions provides a serverless API surface; consumption pricing includes a free monthly grant (e.g., 1M requests/month plus free GB-s) that fits “knowledge bank” workloads when most access is static and only a minority of requests hit SPARQL. citeturn0search0turn0search1

The template below implements two “fast paths” to keep cost down: precomputed JSON views served as static files (no compute), and cached GET-based SPARQL queries (compute only when needed). It uses official standards: JSON-LD for Linked Data in JSON citeturn1search1, SPARQL protocol semantics for the `/sparql` endpoint citeturn2search2, schema.org for baseline vocabulary (e.g., `CreativeWork/about`, `CreativeWork/mentions`, `Thing/sameAs`) citeturn8search0turn8search1turn7search2, and W3C PROV-O for provenance citeturn7search3. Validation is defined via SHACL to prevent graph drift and keep AI-facing data reliable. citeturn2search3

## Design goals and system shape

### Goals

The system optimizes for: human-first authoring (plain Markdown), deterministic builds (same input → same graph), cost control by shifting compute to CI (build-time) rather than runtime (query-time), and AI-accessibility via a stable RDF/JSON-LD layer. JSON-LD is explicitly designed to serialize Linked Data in JSON for web programming environments and interoperable services. citeturn1search1

### Serving model and why it stays cheap

Azure Static Web Apps routing/config is defined via `staticwebapp.config.json` (the older `routes.json` is deprecated), enabling the “most requests are static” strategy. citeturn3search0 Most “AI reads” should hit either the static site HTML+embedded JSON-LD or precomputed `/graph/views/*.json` files; SPARQL is a fallback for complex queries. The SPARQL endpoint uses the SPARQL 1.1 Protocol pattern (a single `query` parameter; response depends on query form and negotiation). citeturn2search2

### Query engine options and why both are included

The Azure Function can run SPARQL using RDFLib’s SPARQL 1.1 implementation (`Graph.query()`), returning a `Result` object that supports serializers (including JSON) for SPARQL results. citeturn1search2turn9search1 For improved speed and on-disk indexing in serverless scenarios, `pyoxigraph` is a viable alternative: it is a Python graph database implementing SPARQL and supports common RDF formats including JSON-LD and Turtle. citeturn2search0turn2search15

## Repo template artifact

```org
#+TITLE: Knowledge Bank Repo Template (Shovel-Ready)
#+LANGUAGE: en
#+OPTIONS: toc:2 num:nil

* Overview
This repo holds human-authored Markdown under /content and machine-authored Linked Data under /graph. The site is hosted on Azure Static Web Apps (SWA). A serverless SPARQL endpoint is implemented as Azure Functions under /api and can be deployed via SWA integrated API or as a separate Function App.

Links (official):
- Azure SWA docs: https://learn.microsoft.com/en-us/azure/static-web-apps/
- SWA configuration (staticwebapp.config.json): https://learn.microsoft.com/en-us/azure/static-web-apps/configuration
- Add API to SWA (Functions in /api): https://learn.microsoft.com/en-us/azure/static-web-apps/add-api
- SWA build configuration (GitHub Actions): https://learn.microsoft.com/en-us/azure/static-web-apps/build-configuration
- Azure Functions Python reference: https://learn.microsoft.com/en-us/azure/azure-functions/functions-reference-python
- SPARQL Protocol (W3C): https://www.w3.org/TR/sparql11-protocol/
- JSON-LD 1.1 (W3C): https://www.w3.org/TR/json-ld11/
- SHACL (W3C): https://www.w3.org/TR/shacl/
- PROV-O (W3C): https://www.w3.org/TR/prov-o/
- schema.org: https://schema.org/

* Repo layout (authoring vs artifacts)
#+CAPTION: File layout (requested table)
| Path | Purpose | Human-edited? | Notes |
|------+---------+--------------+-------|
| /content/ | Canonical Markdown knowledge | yes | deterministic file naming |
| /app/ | Static site (e.g., Astro/Eleventy/Docsify) | yes | must produce at least 1 HTML page for SWA |
| /api/ | Azure Functions (SPARQL endpoint) | yes | SWA can deploy API from this folder |
| /ontology/ | Minimal ontology + JSON-LD context | yes | schema.org + kb: extensions |
| /graph/ | Generated RDF + views + manifests | no | committed by CI bot |
| /tools/ | Extraction + post-processing code | yes | pure-Python CLI tools |
| /tests/ | Unit + golden tests | yes | ensures deterministic builds |
| /.github/workflows/ | CI/CD workflows | yes | KG build + SWA deploy |

* File naming conventions
** Markdown articles (/content)
- Location: /content/YYYY/MM/<slug>.md
- Slug: lowercase, digits, hyphens; no spaces
- One primary topic per file; subtopics via headings.
- Stable canonical URL derived from path:
  - https://<YOUR_DOMAIN>/YYYY/MM/<slug>/
** Entity IDs (/graph/id/)
- URI pattern: https://<YOUR_DOMAIN>/id/<entity-slug>
- entity-slug = canonicalized label (lowercase, hyphenated)
** Chunk IDs
- chunk_id = sha256(normalized_chunk_text)[:16]
- chunk_uri: urn:kb:chunk:<doc_id>:<chunk_id>

* Markdown authoring guidelines
** Frontmatter (required)
YAML frontmatter at top of each article:

#+begin_src yaml
---
title: "What is a Knowledge Bank?"
date_published: "2026-04-04"
date_modified: "2026-04-04"
authors:
  - name: "Luis"
tags: ["knowledge-graph", "sparql", "markdown"]
about:               # optional: explicit topics (strings or URIs)
  - "https://schema.org/KnowledgeGraph"
summary: "Human-friendly Markdown with machine-friendly Linked Data."
canonical_url: "https://<YOUR_DOMAIN>/2026/04/knowledge-bank/"
entity_hints:        # optional: help disambiguation (cheap + high quality)
  - label: "Schema.org"
    sameAs: "https://www.wikidata.org/wiki/Q347669"
  - label: "SPARQL"
    sameAs: "https://www.wikidata.org/wiki/Q54872"
---
#+end_src

** Wikilinks (optional but recommended)
Use wikilinks to mark entity mentions without harming readability:
- [[Entity Name]]
- [[Entity Name|display text]]

Rules:
- Use wikilinks for “important nouns” you want in the graph.
- Avoid wikilinking every noun; bias toward durable entities (tools, orgs, standards, people).
- Optional disambiguation inline:
  - [[Schema.org]]{sameAs=https://www.wikidata.org/wiki/Q347669}

* Minimal ontology (schema.org base + tiny extensions)
** Approach
- Use schema.org as the primary vocabulary:
  - Articles: schema:Article (subclass of schema:CreativeWork)
  - Topics/entities: schema:Thing
  - Linking: schema:sameAs
  - Topicalization: schema:about
  - Mentions: schema:mentions
- Add a small “kb:” namespace for extraction metadata only.

** JSON-LD context (/ontology/context.jsonld)
#+begin_src json
{
  "@context": {
    "schema": "https://schema.org/",
    "prov": "http://www.w3.org/ns/prov#",
    "kb": "https://<YOUR_DOMAIN>/vocab/kb#",

    "id": "@id",
    "type": "@type",

    "confidence": {"@id": "kb:confidence", "@type": "http://www.w3.org/2001/XMLSchema#decimal"},
    "source": {"@id": "prov:wasDerivedFrom", "@type": "@id"},
    "chunk": {"@id": "kb:chunk", "@type": "@id"},
    "docPath": "kb:docPath",
    "charStart": {"@id": "kb:charStart", "@type": "http://www.w3.org/2001/XMLSchema#integer"},
    "charEnd": {"@id": "kb:charEnd", "@type": "http://www.w3.org/2001/XMLSchema#integer"},

    "relatedTo": {"@id": "kb:relatedTo", "@type": "@id"}
  }
}
#+end_src

** kb: extension terms (/ontology/kb.ttl)
#+begin_src ttl
@prefix kb: <https://<YOUR_DOMAIN>/vocab/kb#> .
@prefix schema: <https://schema.org/> .
@prefix prov: <http://www.w3.org/ns/prov#> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

kb:confidence a rdf:Property ;
  rdfs:label "confidence" ;
  rdfs:comment "Extractor confidence in [0,1]." ;
  rdfs:range xsd:decimal .

kb:relatedTo a rdf:Property ;
  rdfs:label "related to" ;
  rdfs:comment "Fallback relation when schema.org lacks an appropriate property." ;
  rdfs:domain schema:Thing ;
  rdfs:range schema:Thing .

kb:chunk a rdf:Property ;
  rdfs:comment "Chunk that produced this assertion." .

kb:docPath a rdf:Property .
kb:charStart a rdf:Property .
kb:charEnd a rdf:Property .
#+end_src

* AI extraction pipeline (GitHub Actions)
#+CAPTION: CI steps (requested table)
| Step | What it does | Key outputs | Cost control |
|------+--------------+-------------+--------------|
| Detect changed docs | git diff | list of .md | avoids reprocessing |
| Chunk deterministically | heading/paragraph chunks | chunks.jsonl | stable IDs |
| LLM extract (batched) | entities + relations → JSON | raw_extractions.jsonl | concurrency + budget |
| Post-process | canonicalize + dedup + provenance | graph.jsonld + graph.ttl | cache reuse |
| Validate | SHACL + unit tests | CI pass/fail | blocks drift |
| Commit /graph | bot commit w/ skip | updated /graph | prevents infinite loop |

** Workflow: .github/workflows/kg-build.yml
#+begin_src yaml
name: build-knowledge-graph

on:
  push:
    branches: [ "main" ]
    paths:
      - "content/**"
      - "ontology/**"
      - "tools/**"
      - "tests/**"
      - ".github/workflows/kg-build.yml"
    paths-ignore:
      - "graph/**"  # prevent commit loop
  pull_request:
    branches: [ "main" ]

permissions:
  contents: write  # needed to commit /graph back to repo
  models: read     # needed for GITHUB_TOKEN to access GitHub Models API

jobs:
  kg:
    runs-on: ubuntu-latest
    env:
      LLM_PROVIDER: "github_models"    # options: github_models | openai | azure_openai | anthropic | local
      LLM_MODEL: "openai/gpt-4o-mini"  # publisher/model format required by models.github.ai
      LLM_ENDPOINT: "https://models.github.ai/inference"
      MAX_USD_PER_RUN: "1.00"          # hard budget gate
      MAX_CONCURRENCY: "2"             # rate limit safety
      CHUNK_TOKEN_TARGET: "750"        # tune per model/context
      CHUNK_BATCH_SIZE: "3"            # chunks per LLM request (stay under 8K input tokens)
      GRAPH_BASE_URL: "https://<YOUR_DOMAIN>"
    steps:
      - uses: actions/checkout@v5
        with:
          fetch-depth: 0

      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: "3.11"

      - name: Install tools
        run: |
          python -m pip install --upgrade pip
          pip install -r tools/requirements.txt

      - name: Build graph
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}  # native token; no separate secret needed
        run: |
          python -m tools.kg_build \
            --repo-root . \
            --content-glob "content/**/*.md" \
            --out-dir graph

      - name: Run tests
        run: |
          pytest -q

      - name: Commit graph artifacts
        if: github.event_name == 'push'
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
          git add graph
          git diff --cached --quiet || git commit -m "chore(graph): update artifacts [skip ci]"
          git push
#+end_src

** Secrets (CI)
- GITHUB_TOKEN (built-in): automatically provided by GitHub Actions; used for GitHub Models API authentication via `permissions: models: read`
- AZURE_STATIC_WEB_APPS_API_TOKEN (if SWA deploy workflow is enabled)
- AZURE_CREDENTIALS (only if deploying a separate Function App via az login)
- Note: No separate LLM_API_KEY is needed when using GitHub Models as the provider

* Concrete extraction steps (tools/)
** Chunking strategy (deterministic)
Algorithm:
1. Parse Markdown into blocks.
2. Split at H1/H2 headings; within section, group paragraphs until ~CHUNK_TOKEN_TARGET.
3. For each chunk produce:
   - doc_id (from canonical_url or path)
   - chunk_id (sha256 over normalized text)
   - heading_path
   - charStart/charEnd offsets in original file
4. Emit JSONL: /graph/intermediate/chunks.jsonl

** Prompt schema (system + ontology + rules + examples)
File: /tools/prompts/extract_rdf_v1.txt

#+begin_src text
SYSTEM:
You are a deterministic RDF extraction engine. Output MUST be valid JSON per the schema. Do not invent facts.

ONTOLOGY (use schema.org first; kb: only for extraction metadata):
- Types: schema:Article, schema:Person, schema:Organization, schema:SoftwareApplication, schema:CreativeWork, schema:Thing
- Preferred properties: schema:about, schema:mentions, schema:sameAs, schema:author, schema:creator, schema:datePublished, schema:dateModified
- Fallback: kb:relatedTo
- Provenance: prov:wasDerivedFrom (use chunk URI)
- Confidence: kb:confidence (0..1)

RULES:
- Only add relations explicitly stated or strongly implied by the text.
- Prefer schema.org properties; use kb:relatedTo only if no schema.org property fits.
- Every entity MUST have: id, type, label.
- Use stable ids:
  - articles: canonical_url
  - entities: GRAPH_BASE_URL + "/id/" + slug(label)
- If a wikilink or entity_hints provides sameAs, use it.
- Emit 0..N triples. Avoid duplicates.

OUTPUT JSON SCHEMA:
{
  "entities":[{"id":"...","type":"schema:Thing","label":"...","sameAs":["..."]}],
  "assertions":[
     {"s":"...","p":"schema:mentions","o":"...","confidence":0.85,"source":"urn:kb:chunk:..."}
  ]
}

FEW-SHOT EXAMPLES:
TEXT: "Neo4j was created by Emil Eifrem."
OUTPUT:
{"entities":[
 {"id":"https://<YOUR_DOMAIN>/id/neo4j","type":"schema:SoftwareApplication","label":"Neo4j"},
 {"id":"https://<YOUR_DOMAIN>/id/emil-eifrem","type":"schema:Person","label":"Emil Eifrem"}
],
"assertions":[
 {"s":"https://<YOUR_DOMAIN>/id/neo4j","p":"schema:creator","o":"https://<YOUR_DOMAIN>/id/emil-eifrem","confidence":0.92,"source":"urn:kb:chunk:EXAMPLE"}
]}

TEXT: "This article mentions SPARQL and JSON-LD."
OUTPUT:
{"entities":[
 {"id":"https://<YOUR_DOMAIN>/id/sparql","type":"schema:Thing","label":"SPARQL"},
 {"id":"https://<YOUR_DOMAIN>/id/json-ld","type":"schema:Thing","label":"JSON-LD"}
],
"assertions":[
 {"s":"<ARTICLE_ID>","p":"schema:mentions","o":"https://<YOUR_DOMAIN>/id/sparql","confidence":0.70,"source":"urn:kb:chunk:EXAMPLE"},
 {"s":"<ARTICLE_ID>","p":"schema:mentions","o":"https://<YOUR_DOMAIN>/id/json-ld","confidence":0.70,"source":"urn:kb:chunk:EXAMPLE"}
]}
#+end_src

** LLM call patterns (batching + rate/cost controls)
- Process chunks in deterministic order.
- Batch: send up to N chunks per request if provider supports JSON array outputs; otherwise 1 chunk/request.
- Concurrency: MAX_CONCURRENCY (default 2).
- Backoff on 429/5xx: exponential + jitter.
- Cost guard:
  - estimate tokens per chunk, refuse run if > MAX_USD_PER_RUN equivalent (requires provider pricing config).
- Cache:
  - per-chunk cache keyed by (chunk_id + prompt_version + model):
    /graph/cache/<chunk_id>.<prompt_version>.json

** Post-processing
1. Canonicalization:
   - slugify labels, normalize whitespace/case
   - merge entities by sameAs first, then exact label match
2. Dedup:
   - drop duplicate assertions (s,p,o)
3. Confidence:
   - preserve LLM confidence; optionally down-rank kb:relatedTo edges
4. Provenance:
   - emit prov:wasDerivedFrom for each assertion source chunk
   - store docPath/charStart/charEnd in chunk node
5. Output:
   - /graph/articles/<doc_id>.jsonld
   - /graph/articles/<doc_id>.ttl
   - /graph/dataset.trig (optional but recommended for GRAPH provenance)

* Output formats (/graph)
- JSON-LD: easiest for AI tooling and embedding into HTML.
- Turtle: human-debuggable and SPARQL-friendly.
- Optional TriG/N-Quads: for named graph provenance.

* Tests
- Unit: chunking determinism, slugify, dedup rules.
- Golden: sample article → expected JSON-LD/Turtle hashes.
- Validation: SHACL shapes in /ontology/shapes.ttl run against /graph/dataset.trig.

* Azure deployment (SWA + Functions)
#+CAPTION: Azure resources (requested table)
| Resource | Purpose | Cheapest default | Notes |
|----------+---------+------------------+-------|
| Static Web App | host site + API | SWA Free | includes deployment token |
| Functions (as SWA API) | /api/sparql | Consumption via SWA | simplest: single deployment |
| (Optional) Separate Function App | heavier SPARQL usage | Consumption | more control, more ops |
| (Optional) Storage account | store large graph | Standard/LRS | only if graph too big for repo |
| (Optional) App Insights | observability | basic | can sample logs |

** SWA config: /app/staticwebapp.config.json
#+begin_src json
{
  "routes": [
    { "route": "/sparql", "rewrite": "/api/sparql" }
  ],
  "globalHeaders": {
    "Cache-Control": "public, max-age=300"
  }
}
#+end_src

** Azure Function: /api/function_app.py (RDFLib option)
#+begin_src python
import azure.functions as func
import json, os, time, hashlib
from rdflib import Dataset

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

_DATASET = None
_DATASET_ETAG = None
_LAST_LOAD = 0

def _load_dataset():
    global _DATASET, _DATASET_ETAG, _LAST_LOAD
    path = os.path.join(os.path.dirname(__file__), "..", "graph", "dataset.trig")
    path = os.path.abspath(path)

    # Lightweight reload check: mtime + size
    st = os.stat(path)
    etag = hashlib.sha256(f"{st.st_mtime_ns}:{st.st_size}".encode()).hexdigest()

    if _DATASET is None or etag != _DATASET_ETAG:
        ds = Dataset()
        ds.parse(path, format="trig")
        _DATASET = ds
        _DATASET_ETAG = etag
        _LAST_LOAD = time.time()

def _sparql_json(result):
    # rdflib Result supports serialize(format="json")
    return result.serialize(format="json")

@app.route(route="sparql", methods=["GET","POST"])
def sparql(req: func.HttpRequest) -> func.HttpResponse:
    _load_dataset()

    query = req.params.get("query")
    if not query:
        try:
            body = req.get_json()
            query = body.get("query")
        except Exception:
            query = None

    if not query:
        return func.HttpResponse(
            json.dumps({"error":"Missing SPARQL query parameter 'query'"}),
            status_code=400,
            mimetype="application/json"
        )

    try:
        res = _DATASET.query(query)
        payload = _sparql_json(res)
        return func.HttpResponse(payload, status_code=200, mimetype="application/sparql-results+json")
    except Exception as e:
        return func.HttpResponse(
            json.dumps({"error":"SPARQL execution failed","detail":str(e)}),
            status_code=400,
            mimetype="application/json"
        )
#+end_src

** Azure Function: /api/function_app.py (PyOxigraph option)
#+begin_src python
import azure.functions as func
import json, os, hashlib
from pyoxigraph import Store, QueryResultsFormat

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

_STORE = None
_ETAG = None

def _load_store():
    global _STORE, _ETAG
    trig_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "graph", "dataset.trig"))
    st = os.stat(trig_path)
    etag = hashlib.sha256(f"{st.st_mtime_ns}:{st.st_size}".encode()).hexdigest()
    if _STORE is None or etag != _ETAG:
        store = Store()          # in-memory
        store.load(trig_path)    # auto-detect format by extension
        _STORE, _ETAG = store, etag

@app.route(route="sparql", methods=["GET","POST"])
def sparql(req: func.HttpRequest) -> func.HttpResponse:
    _load_store()
    query = req.params.get("query")
    if not query:
        try:
            query = req.get_json().get("query")
        except Exception:
            query = None
    if not query:
        return func.HttpResponse(json.dumps({"error":"Missing 'query'"}), status_code=400)

    try:
        r = _STORE.query(query)
        payload = r.serialize(format=QueryResultsFormat.JSON)
        return func.HttpResponse(payload, mimetype="application/sparql-results+json")
    except Exception as e:
        return func.HttpResponse(json.dumps({"error":str(e)}), status_code=400)
#+end_src

** Cold-start + caching strategies
- Keep dependencies minimal; avoid large model SDKs in the function.
- Cache dataset/store in module globals; rely on warm instances.
- Prefer GET for cacheable queries; POST for long queries.
- Add Cache-Control for stable results; consider persisted queries for hot paths.

** Deploy commands (az CLI)
Option A: Create SWA (site+API) via CLI:
#+begin_src bash
# prerequisites: az login, GitHub repo connected or token-based deployment
az staticwebapp create \
  --name "<NAME>" \
  --resource-group "<RG>" \
  --location "centralus" \
  --source "https://github.com/<ORG>/<REPO>" \
  --branch "main" \
  --app-location "app" \
  --api-location "api" \
  --output-location "dist"
#+end_src

Option B: Create SWA via Bicep (infra-as-code):
- Docs: https://learn.microsoft.com/en-us/azure/static-web-apps/publish-bicep

* Developer docs (deliverables)
- /README.org: quickstart, local dev, deploy
- /docs/design.org: system design artifact (see separate doc below)
- Local dev:
  - site: run your static site generator (UNSPECIFIED)
  - functions: use Azure Functions Core Tools (func start)
- Unit tests: pytest
- Samples:
  - /content/2026/04/sample-knowledge-bank.md
  - /graph/articles/sample-knowledge-bank.jsonld
  - /graph/articles/sample-knowledge-bank.ttl
  - /docs/examples.sparql
```

## System design artifact

```org
#+TITLE: Knowledge Bank System Design (Markdown → Linked Data → SPARQL)
#+LANGUAGE: en
#+OPTIONS: toc:2 num:nil

* Problem statement
We want a low-cost, Git-based “knowledge bank” where:
- Humans write normal Markdown articles.
- A CI pipeline extracts Linked Data (RDF) for AI and query use.
- The site is served as static content (cheap).
- A SPARQL endpoint exists for precise queries (serverless).

* Design goals
- Human-first authoring; minimal semantic burden.
- Deterministic builds and reproducible graphs.
- Strong provenance: every assertion traceable to a doc+chunk.
- Cost-control: pay per content change, not per read.
- Interoperability: schema.org baseline + W3C standards.

Non-goals:
- Real-time graph updates per page view.
- Full OWL reasoning and heavy triple-store ops.

* Architecture
#+begin_src mermaid
flowchart TD
  A[GitHub repo: /content Markdown] -->|Push/PR| B[GitHub Actions: KG Build]
  B --> C[/graph artifacts\nJSON-LD + Turtle + TriG]
  C --> D[Azure Static Web Apps\nserves /app + /graph]
  C --> E[Azure Functions /api/sparql\nloads dataset + answers SPARQL]
  D --> F[Humans & Crawlers]
  E --> G[AI tools / Agents / Apps]
#+end_src

* Data model
- Document node: schema:Article
  - @id = canonical_url
  - schema:datePublished, schema:dateModified
  - schema:about topics, schema:mentions entities
- Entity nodes: schema:Thing subclasses when known (Person/Org/SoftwareApplication)
- Linking: schema:sameAs to Wikidata/other canonical URIs
- Assertion metadata:
  - prov:wasDerivedFrom = chunk URI
  - kb:confidence in [0,1]

* Extraction pipeline design
** Deterministic chunking
- Split by headings; pack paragraphs to token target.
- Stable chunk ids via hash to enable caching and incremental rebuilds.

** Prompting and safety
- Constrain output to strict JSON schema.
- Prefer schema.org; kb: is metadata only.
- Disallow invention; confidence required on every assertion.

** Post-processing rules
- Canonicalize entity ids via slugify(label).
- Resolve identity via sameAs first, then exact label match.
- Drop duplicate (s,p,o) assertions.
- Persist manifest:
  - build timestamp
  - git commit hash
  - extractor version
  - prompt version
  - model/provider (unspecified)

* Provenance and versioning
- Commit /graph with bot commits; treat as build artifact.
- Include:
  - /graph/manifest.json
  - /graph/chunks.jsonl
  - /graph/cache/*
- Versioning:
  - prompt_version: v1, v2...
  - schema_version: lock schema.org release date optionally
  - breaking changes require bump of graph schema + migration note.

* Cost strategy
Primary: static-first.
- Most users/agents fetch static JSON-LD/Turtle or precomputed /graph/views.
- SPARQL is “fallback”; cache GET queries.

Tactics:
- Precompute views:
  - /graph/views/entities.json
  - /graph/views/articles_by_tag.json
- Cache at edge/clients with Cache-Control headers.
- Lazy-load dataset in Function; reuse warm instance cache.
- Guardrails on LLM usage:
  - only changed chunks
  - cache by chunk_id+prompt_version+model
  - hard MAX_USD_PER_RUN

* Deployment strategy
Two modes:
A) Cheapest: Azure Static Web Apps + integrated Functions in /api
B) Higher control: separate Azure Function App (zip deploy; run-from-package).

* Implementation timeline
#+begin_src mermaid
gantt
  title Knowledge Bank Implementation Timeline
  dateFormat  YYYY-MM-DD
  section Repo + content
  Repo scaffold + samples            :a1, 2026-04-06, 3d
  section KG build (CI)
  Chunker + prompt + LLM client      :a2, after a1, 5d
  Post-process + outputs + tests     :a3, after a2, 5d
  section Azure
  SWA deploy + API wiring            :a4, after a1, 3d
  SPARQL Function + caching          :a5, after a4, 4d
  section Hardening
  SHACL validation + golden tests    :a6, after a3, 3d
  Cost controls + docs               :a7, after a5, 2d
#+end_src

* Example SPARQL queries
#+begin_src sparql
PREFIX schema: <https://schema.org/>
SELECT ?article ?title WHERE {
  ?article a schema:Article ;
           schema:name ?title ;
           schema:mentions <https://<YOUR_DOMAIN>/id/sparql> .
} LIMIT 50
#+end_src

#+begin_src sparql
PREFIX schema: <https://schema.org/>
SELECT ?entity (COUNT(?article) AS ?n) WHERE {
  ?article a schema:Article ;
           schema:mentions ?entity .
} GROUP BY ?entity ORDER BY DESC(?n) LIMIT 25
#+end_src
```

## Research grounding and implementation notes

Key implementation decisions above follow current official guidance: SWA uses `staticwebapp.config.json` for routing/config and deprecates `routes.json` citeturn3search0; SWA deploy configuration uses `app_location/api_location/output_location` in the GitHub Actions workflow model citeturn3search1turn3search5 and deployments rely on a deployment token stored as a GitHub secret by default. citeturn5search4 GitHub Actions secrets and token usage are handled via standard GitHub mechanisms (repository secrets; `GITHUB_TOKEN` auth; ability to skip workflow runs with commit message directives like `[skip ci]`). citeturn4search3turn4search0turn4search1

On the query side, the `/sparql` function aligns with SPARQL protocol expectations (single `query` string; result formats based on query form) citeturn2search2 and uses either RDFLib’s SPARQL engine (`Graph.query()` with result serialization options like JSON) citeturn1search2turn9search1 or PyOxigraph’s Store abstraction for SPARQL on an RDF dataset. citeturn2search0turn2search15 For data quality enforcement, SHACL is the W3C standard for validating RDF graphs against constraints citeturn2search3 and PROV-O provides a standard RDF vocabulary for exchanging provenance across systems. citeturn7search3
## Validation corrections (April 2026)

The following corrections were applied to this document after independent validation research.

### Critical fixes applied

1. **GitHub Models endpoint updated**: The Azure inference endpoint `models.inference.ai.azure.com` was deprecated in July 2025 and stopped responding in October 2025. All references updated to the current endpoint: `https://models.github.ai/inference`. See: [GitHub Blog - Deprecation of Azure endpoint for GitHub Models](https://github.blog/changelog/2025-07-17-deprecation-of-azure-endpoint-for-github-models/).

2. **Model naming format**: GitHub Models now requires the `publisher/model` format (e.g., `openai/gpt-4o-mini` instead of `gpt-4o-mini`). Updated throughout.

3. **Authentication simplified**: As of April 2025, `GITHUB_TOKEN` natively supports GitHub Models access from within GitHub Actions workflows. The workflow requires `permissions: models: read`. No separate `LLM_API_KEY` secret is needed. See: [GitHub Blog - GitHub Actions token integration now GA in GitHub Models](https://github.blog/changelog/2025-04-14-github-actions-token-integration-now-generally-available-in-github-models/).

4. **PyOxigraph API corrected**: The `.to_json()` method does not exist on PyOxigraph query results. The correct API is `.serialize(format=QueryResultsFormat.JSON)` which returns bytes conforming to the W3C SPARQL Query Results JSON specification. See: [PyOxigraph SPARQL docs](https://pyoxigraph.readthedocs.io/en/stable/sparql.html).

5. **LLM provider default set**: Changed `LLM_PROVIDER` from `"UNSPECIFIED"` to `"github_models"` and `LLM_MODEL` to `"openai/gpt-4o-mini"` to reflect the chosen provider.

### Important corrections applied

6. **`permissions: models: read` added** to the GitHub Actions workflow YAML - required for `GITHUB_TOKEN` to authenticate with the GitHub Models API.

7. **Bot commit identity** updated to use the standard `github-actions[bot]` convention: `41898282+github-actions[bot]@users.noreply.github.com`.

## GitHub Models - rate limits and batching strategy

### Free tier limits (GPT-4o-mini)

| Metric | Limit |
|--------|-------|
| Requests per day | 150 |
| Input tokens per request | 8,000 |
| Output tokens per request | 4,000 |
| Rate (RPM) | ~15-30 |

Other models: GPT-4o is limited to 50 req/day. Phi and Llama models have separate quotas. Each model's limit is tracked independently per user (UserByModelByDay).

### Batching strategy (mandatory)

For a knowledge bank with ~100 content chunks at 750 tokens each:

- **System prompt overhead**: ~2,000 tokens (ontology + rules + few-shot examples)
- **Per-chunk overhead**: ~750 tokens input + ~200-500 tokens output
- **Batch 3-5 chunks per request**: 2,000 + (5 x 750) = 5,750 tokens fits in 8K limit
- **Total API calls**: 100 chunks / 5 per batch = **20 requests** (well within 150/day)
- **Incremental builds**: Only process changed chunks (git diff detection) - most runs process <20 chunks

### Caching (mandatory)

Per-chunk extraction cache keyed by `(chunk_id, prompt_version, model)`:
- Location: `/graph/cache/<chunk_id>.<prompt_version>.json`
- On cache hit: skip LLM call entirely
- Cache invalidation: chunk content change (new sha256) or prompt version bump

### Python SDK integration

```python
from openai import OpenAI
import os

client = OpenAI(
    base_url="https://models.github.ai/inference",
    api_key=os.environ["GITHUB_TOKEN"]
)

response = client.chat.completions.create(
    model="openai/gpt-4o-mini",
    messages=[
        {"role": "system", "content": "...extraction prompt..."},
        {"role": "user", "content": "...batched chunks..."}
    ],
    temperature=0,
    response_format={"type": "json_object"}
)
```

## Azure Static Web Apps - Free plan details

### Resource limits

| Resource | Free plan | Standard plan |
|----------|-----------|---------------|
| Storage per environment | 250 MB | 500 MB |
| Total storage | 500 MB | 2 GB |
| File count | 15,000 | 15,000 |
| Bandwidth | 100 GB/month | 100 GB/month (overages billable) |
| Custom domains | 2 | 5 |
| Preview environments | 3 | 10 |
| SLA | None | 99.95% |

### Managed Functions (API) constraints on Free plan

- **Triggers**: HTTP only (no timer, queue, event grid)
- **Runtime**: Python 3.9-3.11 supported
- **Execution timeout**: 10 minutes (inherited from Consumption plan)
- **Free grant**: 1 million requests/month + 400,000 GB-s per subscription
- **No managed identity**: Cannot use Azure Key Vault references
- **Not standalone**: API only accessible via the SWA domain (not separately addressable)

### Why this is sufficient for the knowledge bank

- Most reads hit static files (JSON-LD, Turtle, precomputed views) - zero compute cost
- SPARQL queries are the minority of requests - Consumption plan free grant covers this
- 250 MB storage is adequate: typical graph artifacts are <10 MB; cache can be excluded from deployment
- 10-minute timeout is more than enough for SPARQL queries against an in-memory RDF dataset

## Python RDF library stack

### Recommended stack

| Library | Version | Size | Type | Purpose |
|---------|---------|------|------|---------|
| RDFLib | >=7.1.1, <8.0 | ~615 KB | Pure Python | SPARQL engine, JSON-LD, Turtle, TriG |
| pySHACL | >=0.31.0 | ~445 KB | Pure Python | SHACL validation |
| openai | >=1.0 | ~200 KB | Pure Python | GitHub Models SDK |

**Combined footprint: ~1.3 MB** - excellent for Azure Functions cold start (<1s).

### RDFLib capabilities (validated)

- `Graph.query()` and `Dataset.query()`: Full SPARQL 1.1 support
- `result.serialize(format="json")`: Produces W3C SPARQL Results JSON
- Built-in JSON-LD parser/serializer (since v6.0.1; separate `rdflib-jsonld` plugin is archived/deprecated)
- Supports: Turtle, TriG, N-Triples, N-Quads, RDF/XML, JSON-LD
- Python requirement: 3.8+

### PyOxigraph (alternative, validated)

- Rust-based bindings (5-8 MB wheel, platform-specific)
- Faster SPARQL execution than RDFLib for large datasets
- Correct query API: `store.query(sparql_str)` then result `.serialize(format=QueryResultsFormat.JSON)`
- **Note**: `.to_json()` does NOT exist - always use `.serialize()`
- `store.load(path)` auto-detects format by file extension

### Recommendation

Use **RDFLib** for the SWA managed function (pure Python, no binary dependencies, simpler deployment). Consider **PyOxigraph** only if SPARQL query performance becomes a bottleneck with large graph datasets.

## HuggingFace models - assessment for future versions

### Available task-specific models

| Task | Model | Size | CPU Speed | Accuracy |
|------|-------|------|-----------|----------|
| NER | `dslim/bert-base-NER` | 330 MB | ~100ms/doc | ~90% F1 (PER/ORG/LOC/MISC) |
| Entity Linking | `facebook/GENRE` | 1 GB | ~200ms/doc | Good (Wikipedia titles) |
| Zero-shot Classification | `facebook/bart-large-mnli` | 700 MB | ~150ms/doc | ~85-90% |
| Relation Extraction | Various | ~700 MB | ~200ms/doc | 75-85% (less mature) |

### Hybrid pipeline (NER then LLM)

A hybrid approach could reduce LLM calls by 5-10x:
1. **NER** (local, free): Extract entity mentions - PER, ORG, LOC, MISC
2. **Entity Linking** (local, free): Link entities to Wikidata for sameAs URIs
3. **LLM** (GitHub Models): Only for relation extraction + schema.org classification
4. **SHACL** (local, free): Validate output

### Verdict: Skip for v1

- Single LLM prompt produces richer, more schema.org-aligned output
- HuggingFace NER types (PER/ORG/LOC/MISC) don't map cleanly to schema.org without fine-tuning
- Adds pipeline orchestration complexity (multiple model downloads, dependency management)
- **Revisit for v2** when: content volume exceeds GitHub Models daily limits, or extraction quality needs improvement on specific entity types