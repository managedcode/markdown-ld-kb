# Markdown-LD Knowledge Bank

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/lqdev/markdown-ld-kb)

A Git-based knowledge bank where human-authored Markdown articles are processed by an LLM CI pipeline to extract Linked Data (RDF/JSON-LD), served as static content on Azure Static Web Apps, with a serverless SPARQL endpoint.

## Architecture

```
content/*.md → GitHub Actions → LLM (GitHub Models) → graph/*.jsonld + *.ttl
                                                         ↓
                                                  Azure Static Web Apps
                                                    ├── Static site
                                                    ├── Graph files
                                                    └── API (Azure Functions)
                                                        ├── /api/sparql  (W3C SPARQL 1.1)
                                                        └── /api/ask     (Natural language → SPARQL)
```

## Quick Start

### Prerequisites

- Python 3.11+
- [uv](https://docs.astral.sh/uv/) (Python package manager)
- Git
- Azure CLI (for deployment)

> **Tip:** Click the **Open in GitHub Codespaces** badge above to get a ready-to-code environment with all dependencies pre-installed.

### Local Development

```bash
# Install uv (if not already installed)
curl -LsSf https://astral.sh/uv/install.sh | sh

# Install dependencies
uv sync

# Run tests
uv run pytest tests/ -v

# Dry run (chunk only, no LLM)
uv run python -m tools.kg_build --dry-run

# Full build (requires GITHUB_TOKEN)
export GITHUB_TOKEN=your_token
uv run python -m tools.kg_build --repo-root . --base-url https://example.com
```

### Writing Articles

Create Markdown files in `content/` with YAML frontmatter:

```markdown
---
title: "Your Article Title"
date_published: "2026-04-15"
tags:
  - knowledge-graphs
  - rdf
entity_hints:
  - label: "RDF"
    type: "schema:Thing"
    sameAs: "https://www.wikidata.org/entity/Q54872"
---

# Your Content Here

Write naturally. The LLM pipeline extracts entities and relationships.
Use [[wikilinks]] to link between articles.
```

### Querying the SPARQL Endpoint

The knowledge graph is queryable at `/api/sparql`. It accepts standard [W3C SPARQL 1.1 Protocol](https://www.w3.org/TR/sparql11-protocol/) requests and returns [SPARQL Results JSON](https://www.w3.org/TR/sparql11-results-json/).

### Asking Questions in Natural Language

Don't know SPARQL? Use the `/api/ask` endpoint to query the knowledge graph with plain English. It uses the same LLM (GitHub Models GPT-4o-mini) to translate your question into SPARQL, execute it, and return the results alongside the generated query.

> **Note:** Requires `GITHUB_TOKEN` configured as an Azure Static Web Apps app setting (see [Deployment](#deployment) below).

**From a browser** — paste the URL with a `question` parameter (URL-encoded):
```
https://<your-swa-domain>/api/ask?question=What+entities+are+in+the+knowledge+graph?
```

**cURL (GET):**
```bash
curl "https://<your-swa-domain>/api/ask?question=What+entities+are+in+the+knowledge+graph%3F"
```

**cURL (POST)** — better for longer questions:
```bash
curl -X POST \
  -H "Content-Type: application/json" \
  -d '{"question": "What entities are in the knowledge graph?"}' \
  https://<your-swa-domain>/api/ask
```

**JavaScript:**
```js
const res = await fetch("/api/ask", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ question: "Which articles mention SPARQL?" }),
});
const data = await res.json();
console.log("Generated SPARQL:", data.sparql);
data.results.results.bindings.forEach(row => console.log(row));
```

**Python:**
```python
import requests

res = requests.post(
    "https://<your-swa-domain>/api/ask",
    json={"question": "Find all organizations"},
).json()

print("SPARQL:", res["sparql"])
for row in res["results"]["results"]["bindings"]:
    print(row)
```

The response includes the generated SPARQL query so you can learn the query language as you go:

```json
{
  "question": "What entities are in the knowledge graph?",
  "sparql": "PREFIX schema: <https://schema.org/>\nSELECT DISTINCT ?entity ?name ?type WHERE {\n  ?entity a ?type ;\n          schema:name ?name .\n  FILTER(?type != schema:Article)\n}\nLIMIT 100",
  "results": { "head": { "vars": ["entity", "name", "type"] }, "results": { "bindings": [...] } }
}
```

### Example NL Questions

These are questions you can ask the `/api/ask` endpoint — it translates them to SPARQL automatically:

| Question | What it finds |
|----------|--------------|
| "What entities are in the knowledge graph?" | All named entities (people, orgs, tools, concepts) |
| "Which articles mention SPARQL?" | Articles that reference SPARQL as an entity |
| "Find all organizations" | Entities typed as `schema:Organization` |
| "What topics does the article about knowledge graphs cover?" | Entities mentioned in articles matching "knowledge graph" |
| "Who authored the articles?" | Authors linked via `schema:author` |
| "What software tools are mentioned?" | Entities typed as `schema:SoftwareApplication` |

### Querying with SPARQL Directly

**From a browser** — paste the URL with a `query` parameter (URL-encoded):
```
https://<your-swa-domain>/api/sparql?query=PREFIX%20schema%3A%20...
```

**cURL (GET):**
```bash
curl "https://<your-swa-domain>/api/sparql?query=$(python3 -c "import urllib.parse; print(urllib.parse.quote('PREFIX schema: <https://schema.org/> SELECT ?name WHERE { ?e schema:name ?name }'))")"
```

**cURL (POST)** — better for complex queries, no URL-encoding needed:
```bash
curl -X POST \
  -H "Content-Type: application/sparql-query" \
  -d 'PREFIX schema: <https://schema.org/>
      SELECT ?article ?mentioned ?name WHERE {
        ?article schema:mentions ?mentioned .
        ?mentioned schema:name ?name .
      }' \
  https://<your-swa-domain>/api/sparql
```

**JavaScript:**
```js
const query = `PREFIX schema: <https://schema.org/>
SELECT ?name WHERE { ?e schema:name ?name }`;

const res = await fetch(`/api/sparql?query=${encodeURIComponent(query)}`);
const data = await res.json();
// data.results.bindings is the array of result rows
data.results.bindings.forEach(row => console.log(row.name.value));
```

**Python:**
```python
import requests, urllib.parse

query = """PREFIX schema: <https://schema.org/>
SELECT ?name WHERE { ?e schema:name ?name }"""

url = f"https://<your-swa-domain>/api/sparql?query={urllib.parse.quote(query)}"
data = requests.get(url).json()
for row in data["results"]["bindings"]:
    print(row["name"]["value"])
```

> **Note:** Only `SELECT` and `ASK` queries are allowed. Mutating queries (`INSERT`, `DELETE`, etc.) are blocked.

### Example SPARQL Queries

**Find all entities mentioned in an article:**
```sparql
PREFIX schema: <https://schema.org/>
SELECT ?entity ?name WHERE {
  <https://example.com/2026/04/what-is-a-knowledge-graph/> schema:mentions ?entity .
  ?entity schema:name ?name .
}
```

**Find all articles about a topic:**
```sparql
PREFIX schema: <https://schema.org/>
SELECT ?article ?title WHERE {
  ?article a schema:Article ;
           schema:mentions <https://example.com/id/knowledge-graph> ;
           schema:name ?title .
}
```

**Find connections between entities:**
```sparql
PREFIX schema: <https://schema.org/>
SELECT ?subject ?predicate ?object WHERE {
  ?subject ?predicate ?object .
  FILTER(?predicate != rdf:type)
}
LIMIT 50
```

## Deployment

The app deploys automatically via GitHub Actions to Azure Static Web Apps on every push to `main`.

### Configuring the NL Query Endpoint

The `/api/ask` endpoint requires a GitHub token to call [GitHub Models](https://github.com/marketplace/models) for NL-to-SPARQL translation. Set it as an app setting:

**Azure CLI:**
```bash
az staticwebapp appsettings set \
  --name <your-swa-name> \
  --resource-group <your-resource-group> \
  --setting-names "GITHUB_TOKEN=<your-github-token>"
```

**Azure Portal:**
1. Navigate to your Static Web App resource
2. Go to **Settings → Environment variables**
3. Add `GITHUB_TOKEN` with a GitHub token that has access to GitHub Models

> **Tip:** The SPARQL endpoint (`/api/sparql`) works without any configuration — only the NL translation endpoint needs the token.

## Project Structure

```
├── content/          # Markdown articles (human-authored)
├── ontology/         # JSON-LD context, vocabulary, SHACL shapes
├── tools/            # Extraction pipeline (chunker, LLM client, post-processor)
├── graph/            # Generated artifacts (committed by CI)
│   ├── articles/     # Per-article JSON-LD and Turtle
│   ├── views/        # Precomputed JSON views
│   ├── cache/        # Per-chunk extraction cache
│   └── manifest.json # Build metadata
├── api/              # Azure Function (SPARQL + NL query endpoints)
├── app/              # Static web app
├── tests/            # Test suite
└── .github/workflows/
    ├── kg-build.yml  # KG extraction pipeline
    └── azure-static-web-apps-*.yml # Azure SWA deployment
```

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| LLM Provider | GitHub Models (free) | Zero cost, GITHUB_TOKEN auth |
| NL→SPARQL | GPT-4o-mini + schema-injected few-shot | Same LLM as extraction; schema injection prevents hallucinated predicates |
| SPARQL Engine | RDFLib | Pure Python, small footprint, built-in JSON-LD |
| Validation | pySHACL | Standard W3C SHACL, works with RDFLib |
| Batching | 3-5 chunks/request | Stay under 8K input token limit |

## Rate Limits

GitHub Models free tier (GPT-4o-mini): 150 requests/day, 8K input tokens.
The pipeline batches 3-5 chunks per request and caches results to stay within limits.

## License

MIT
