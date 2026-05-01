# Federated SPARQL Execution

## Purpose

Federated SPARQL execution adds an explicit adapter boundary for read-only `SERVICE` queries against remote SPARQL endpoints such as Wikidata Query Service and against caller-bound local in-memory graphs.

The canonical graph remains the local in-memory `KnowledgeGraph`. Federation is an opt-in query adapter for cases where the caller knowingly needs remote RDF data or a cross-endpoint join.

## Scope

In scope:

- explicit read-only federated query execution through `ExecuteFederatedSelectAsync` and `ExecuteFederatedAskAsync`
- schema-aware federated search through `SearchBySchemaFederatedAsync`, which compiles caller profiles into `SERVICE` queries
- endpoint allowlists and endpoint profiles
- deterministic local service bindings for multi-graph in-memory federation
- caller-visible endpoint diagnostics
- timeout and cancellation-aware behavior
- Wikidata-specific profiles for main and scholarly endpoints

Out of scope:

- automatic fallback from local search/query into federation
- hosted query services or background sync
- network-dependent graph build steps
- write/update access to remote SPARQL endpoints

## Package Baseline

- Keep `dotNetRdf` as the core RDF/SPARQL engine.
- Keep `dotNetRdf.Shacl` for SHACL validation.
- Do not add a Wikidata-specific NuGet package to the production library for this slice.
- Do not add `dotNetRdf.Client` separately unless the repository later narrows away from the `dotNetRdf` meta-package and still needs triple-store connector features.

Reason:

- dotNetRDF already parses and executes `SERVICE` clauses and already contains the query client types needed for endpoint calls.
- Wikidata-specific client packages target MediaWiki/Wikibase APIs, not the standards-first SPARQL federation boundary that this library exposes.

## Boundaries

```mermaid
flowchart LR
    Caller["Caller"] --> Local["KnowledgeGraph local SPARQL"]
    Caller --> Fed["Federated SPARQL adapter"]
    Local --> Graph["In-memory RDF graph"]
    Fed --> Policy["Allowlist + timeout + diagnostics policy"]
    Policy --> Engine["dotNetRDF SERVICE execution"]
    Engine --> LocalBindings["Allowlisted local service bindings"]
    Engine --> WdqsMain["WDQS main endpoint"]
    Engine --> WdqsScholarly["WDQS scholarly endpoint"]
    Engine --> Other["Other allowlisted SPARQL endpoints"]
    Graph --> LocalResult["Local query result"]
    LocalBindings --> FedResult["Federated query result"]
    WdqsMain --> FedResult["Federated query result"]
    WdqsScholarly --> FedResult
    Other --> FedResult
```

## Rules

1. Federated execution must be explicit. The default local query methods remain local-only in their public contract.
2. Federated execution must remain read-only. Only `SELECT` and `ASK` are allowed.
3. The adapter must reject mutating verbs and unsafe SPARQL forms before execution.
4. The adapter must reject remote endpoints that are not explicitly allowlisted by the caller or by a named profile.
5. Local service bindings must also stay behind the same allowlist boundary; a bound in-memory graph must not bypass endpoint policy.
6. The adapter must expose caller-visible diagnostics that name each locally executed service endpoint.
7. The adapter must support cancellation and bounded timeout budgets.
8. Local query methods must reject top-level `SERVICE` clauses.
9. The adapter must reject variable or non-absolute local `SERVICE` specifiers.
10. Remote failures must fail closed by default.
11. The adapter must not automatically use legacy Wikidata full-graph endpoints as the default strategy.
12. The adapter must not change Markdown ingestion, graph build determinism, or local SPARQL execution semantics.

## Endpoint Profiles

### Wikidata Main

- Endpoint URI: `https://query.wikidata.org/sparql`
- Intended use: queries against the main WDQS graph
- Typical purpose: non-scholarly Wikidata entities

### Wikidata Scholarly

- Endpoint URI: `https://query-scholarly.wikidata.org/sparql`
- Intended use: queries against the scholarly WDQS graph
- Typical purpose: scholarly articles and closely related scholarly data

### Wikidata Main + Scholarly Federation

- Intended use: explicit caller-selected federation across the split WDQS graphs
- Default behavior: require the caller to choose this profile or enumerate both endpoints explicitly

## Which API To Use

```mermaid
flowchart LR
    Need["Need cross-graph data?"] --> Raw{"Do you already have SPARQL?"}
    Raw -->|"Yes"| Execute["ExecuteFederatedSelectAsync / ExecuteFederatedAskAsync"]
    Raw -->|"No"| Schema{"Can the query be described by predicates?"}
    Schema -->|"Yes"| Search["SearchBySchemaFederatedAsync"]
    Schema -->|"No"| Local["Use local SearchBySchemaAsync or author raw SPARQL"]
    Execute --> Allow["AllowedServiceEndpoints"]
    Search --> Allow
    Allow --> Bindings["Optional LocalServiceBindings"]
    Allow --> Remote["Optional remote endpoints"]
```

Use raw federated SPARQL when the caller knows the exact cross-service join. Use schema-aware federated search when the caller wants the library to compile a search profile into `SERVICE` blocks. Use local schema-aware search when all required data is already in one `KnowledgeGraph`.

## Raw Local Multi-Graph Example

This example federates across two in-memory graphs without network access. The endpoint URIs are logical service names owned by the host application.

```csharp
var policyGraph = (await pipeline.BuildAsync(
[
    new MarkdownSourceDocument("policy/federation.md", policyMarkdown),
])).Graph;

var runbookGraph = (await pipeline.BuildAsync(
[
    new MarkdownSourceDocument("runbooks/federation.md", runbookMarkdown),
])).Graph;

var rootGraph = (await pipeline.BuildAsync(
[
    new MarkdownSourceDocument("scratch/root.md", string.Empty),
])).Graph;

var policyEndpoint = new Uri("https://kb.example/services/policy");
var runbookEndpoint = new Uri("https://kb.example/services/runbook");

var options = new FederatedSparqlExecutionOptions
{
    AllowedServiceEndpoints =
    [
        policyEndpoint,
        runbookEndpoint,
    ],
    LocalServiceBindings =
    [
        new FederatedSparqlLocalServiceBinding(policyEndpoint, policyGraph),
        new FederatedSparqlLocalServiceBinding(runbookEndpoint, runbookGraph),
    ],
};

var sparql = """
PREFIX schema: <https://schema.org/>
SELECT ?policyTitle ?runbookTitle WHERE {
  SERVICE <https://kb.example/services/policy> {
    ?policy a schema:Article ;
            schema:name ?policyTitle ;
            schema:about ?topic .
  }

  SERVICE <https://kb.example/services/runbook> {
    ?runbook a schema:HowTo ;
             schema:name ?runbookTitle ;
             schema:about ?topic .
  }
}
""";

var result = await rootGraph.ExecuteFederatedSelectAsync(sparql, options);

Console.WriteLine(result.ServiceEndpointSpecifiers[0]);
Console.WriteLine(result.Result.Rows[0].Values["policyTitle"]);
```

The root graph does not need to contain the data being joined. It provides the execution boundary. Each `SERVICE` block is routed either to an allowlisted local binding or to a remote SPARQL endpoint.

## Raw Federated ASK Example

Use `ExecuteFederatedAskAsync` when the caller needs a boolean policy or readiness check across graph slices.

```csharp
var ask = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  SERVICE <https://kb.example/services/policy> {
    ?policy schema:about ?topic .
  }

  SERVICE <https://kb.example/services/runbook> {
    ?runbook schema:about ?topic ;
             kb:nextStep ?nextStep .
  }
}
""";

var decision = await rootGraph.ExecuteFederatedAskAsync(ask, options);

if (decision.Result)
{
    Console.WriteLine(decision.ServiceEndpointSpecifiers.Count);
}
```

## Schema-Aware Federated Search Example

`SearchBySchemaFederatedAsync` compiles a `KnowledgeGraphSchemaSearchProfile` into one `SERVICE` block per configured endpoint. It is the right path when callers want SPARQL federation but do not want to hand-author the full query string.

```csharp
var profile = new KnowledgeGraphSchemaSearchProfile
{
    Prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ex"] = "https://kb.example/vocab/",
    },
    FederatedServiceEndpoints =
    [
        new Uri("https://kb.example/services/policy"),
        new Uri("https://kb.example/services/runbook"),
    ],
    TypeFilters = ["ex:Capability"],
    TextPredicates =
    [
        new KnowledgeGraphSchemaTextPredicate("schema:name", Weight: 1.2d),
        new KnowledgeGraphSchemaTextPredicate("ex:intent", Weight: 1.5d),
    ],
    RelationshipPredicates =
    [
        new KnowledgeGraphSchemaRelationshipPredicate(
            "ex:requires",
            ["ex:symptom", "skos:prefLabel"],
            Weight: 0.9d),
    ],
    TermMode = KnowledgeGraphSchemaSearchTermMode.AllTerms,
};

var search = await rootGraph.SearchBySchemaFederatedAsync(
    "restore cache",
    profile,
    options);

Console.WriteLine(search.Explain.GeneratedSparql);
Console.WriteLine(search.ServiceEndpointSpecifiers[0]);
Console.WriteLine(search.Matches[0].Evidence[0].ServiceEndpoint);
```

Federated schema search returns primary matches and predicate evidence from service endpoints. It does not create a focused local graph because the related graph neighborhood may live only behind the remote service boundary.

## Remote Endpoint Example

Remote endpoints are allowed only when explicitly configured. Use a named profile when it matches the endpoint set, or construct an options object yourself.

```csharp
var wikidataQuery = """
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
SELECT ?item ?itemLabel WHERE {
  SERVICE <https://query.wikidata.org/sparql> {
    ?item rdfs:label ?itemLabel .
    FILTER(LANG(?itemLabel) = "en")
  }
}
LIMIT 10
""";

var remote = await graph.ExecuteFederatedSelectAsync(
    wikidataQuery,
    FederatedSparqlProfiles.WikidataMain);
```

Remote federation is query-time access only. It does not import remote triples into the local `KnowledgeGraph`; use JSON-LD/Turtle loading or a separate preprocessing step when the local graph needs to keep those facts.

## Allowlist Patterns

Recommended host policy:

- use stable logical service URIs for local graph slices, such as `https://kb.example/services/runbooks`
- allowlist every `SERVICE` endpoint, including local bindings
- bind local service endpoints with `FederatedSparqlLocalServiceBinding`
- keep remote endpoint options separate from local-only test options
- set `QueryExecutionTimeoutMilliseconds` for remote endpoints
- inspect `ServiceEndpointSpecifiers` on success and on `FederatedSparqlQueryException`

Avoid:

- passing user-authored arbitrary endpoint URIs directly into `AllowedServiceEndpoints`
- relying on variable `SERVICE ?endpoint` at the library boundary
- expecting local `ExecuteSelectAsync` or `ExecuteAskAsync` to run top-level `SERVICE`
- using federation as a hidden fallback when local schema search returns no matches
- treating remote federation as graph ingestion

## Failure Example

Unallowlisted endpoints fail before execution:

```csharp
var unsafeQuery = """
SELECT ?s WHERE {
  SERVICE <https://unknown.example/sparql> {
    ?s ?p ?o .
  }
}
""";

try
{
    await graph.ExecuteFederatedSelectAsync(unsafeQuery, FederatedSparqlProfiles.WikidataMain);
}
catch (FederatedSparqlQueryException exception)
{
    Console.WriteLine(exception.ServiceEndpointSpecifiers[0]);
}
```

Variable service specifiers also fail before execution:

```sparql
SELECT ?s WHERE {
  VALUES ?endpoint { <https://query.wikidata.org/sparql> }
  SERVICE ?endpoint {
    ?s ?p ?o .
  }
}
```

The library requires absolute endpoint IRIs in `SERVICE <...>` clauses so the allowlist can be evaluated before dotNetRDF executes the query.

## Main Flow

```mermaid
sequenceDiagram
    participant Caller
    participant Adapter as Federated SPARQL adapter
    participant Policy as Allowlist and diagnostics policy
    participant Engine as dotNetRDF SERVICE execution
    participant Local as Allowlisted local KnowledgeGraph binding
    participant Remote as WDQS / allowlisted endpoint

    Caller->>Adapter: ExecuteFederatedSelectAsync(query, options)
    Adapter->>Policy: Validate query shape and endpoints
    Policy-->>Adapter: Approved query plan
    Adapter->>Engine: Execute read-only query
    alt local binding
        Engine->>Local: Issue SERVICE subquery
        Local-->>Engine: SPARQL results
    else remote endpoint
        Engine->>Remote: Issue SERVICE request(s)
        Remote-->>Engine: SPARQL results
    end
    Engine-->>Adapter: Result set
    Adapter-->>Caller: Rows + endpoint diagnostics
```

## Failure And Edge Flows

### Unallowlisted Endpoint

- Reject before execution.
- Return a caller-readable error that identifies the endpoint and the active allowlist/profile.

### Mutating Or Unsafe Query

- Reject before execution.
- Report the exact reason:
  - unsupported query verb
  - non-read-only operation
  - blocked federation usage in a local-only executor

### Timeout Or Cancellation

- Stop execution promptly.
- Surface the timeout/cancellation reason.
- Include the endpoint or endpoint set involved if known.

### Endpoint Drift Or Schema Drift

- Fail explicitly.
- Return endpoint diagnostics that let the caller distinguish transport failure from query/schema mismatch.

## System Behavior Notes

- The local graph remains authoritative for Markdown-derived knowledge.
- Federation supplements query-time access; it does not mutate the local graph automatically.
- Schema-aware federated search uses the same `SERVICE` allowlist and local binding policy as raw federated SPARQL.
- Local service bindings give hosts and tests a deterministic way to federate across multiple in-memory graphs without network access.
- The adapter may expose endpoint profiles, but it does not own remote dataset semantics.
- Wikidata federation often needs explicit graph-shape knowledge and endpoint selection because WDQS split the main and scholarly graphs in 2025.

## Verification

- `dotnet build MarkdownLd.Kb.slnx --no-restore`
- `dotnet test --solution MarkdownLd.Kb.slnx --configuration Release`
- `dotnet format MarkdownLd.Kb.slnx --verify-no-changes`

Current verification focus:

- deterministic tests for query rejection before any HTTP call
- deterministic tests for endpoint allowlist enforcement
- deterministic tests for unsupported variable `SERVICE` specifiers
- deterministic tests for Wikidata profile selection
- deterministic tests for one-query multi-graph federation across five local graphs
- deterministic tests for federated `ASK` across multiple local graphs
- deterministic tests that local service bindings do not bypass the allowlist
- deterministic tests for schema-aware federated search over local JSON-LD service bindings

## Definition Of Done

- The public API clearly distinguishes local SPARQL from federated SPARQL.
- The adapter is opt-in and read-only.
- Endpoint allowlist/profile behavior is explicit and testable.
- Endpoint diagnostics are caller-visible.
- Wikidata main/scholarly profile behavior is documented and testable.
- Deterministic local multi-graph federation is documented and testable.
- Local graph build determinism and local SPARQL semantics remain unchanged.
