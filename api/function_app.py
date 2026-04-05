"""Azure Function: SPARQL endpoint and natural language query interface.

Loads all .ttl files from the graph/articles/ directory into a combined
RDFLib Dataset, then serves SPARQL queries via HTTP GET/POST and
natural language questions via the /api/ask endpoint.
"""

import json
import os
import logging
from pathlib import Path

import azure.functions as func
import rdflib
from rdflib import Dataset, Graph

from nl_to_sparql import translate, validate_sparql, enforce_safety

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

# Module-level cache: load graph once per cold start
_dataset: Dataset | None = None


def _load_dataset() -> Dataset:
    """Load all Turtle files into an RDFLib Dataset."""
    global _dataset
    if _dataset is not None:
        return _dataset

    ds = Dataset()
    graph_dir = Path(__file__).parent / "data"

    if graph_dir.exists():
        for ttl_file in graph_dir.glob("*.ttl"):
            try:
                g = Graph()
                g.parse(str(ttl_file), format="turtle")
                for triple in g:
                    ds.add(triple)
                logging.info(f"Loaded {len(g)} triples from {ttl_file.name}")
            except Exception as e:
                logging.error(f"Failed to parse {ttl_file.name}: {e}")

    logging.info(f"Total triples loaded: {len(ds)}")
    _dataset = ds
    return _dataset


@app.route(route="sparql", methods=["GET", "POST"])
def sparql_endpoint(req: func.HttpRequest) -> func.HttpResponse:
    """Handle SPARQL queries per W3C SPARQL 1.1 Protocol."""
    # Extract query
    query = None
    if req.method == "GET":
        query = req.params.get("query")
    elif req.method == "POST":
        content_type = req.headers.get("Content-Type", "")
        if "application/sparql-query" in content_type:
            query = req.get_body().decode("utf-8")
        elif "application/x-www-form-urlencoded" in content_type:
            query = req.params.get("query") or req.form.get("query")
        else:
            # Try body as raw query
            query = req.get_body().decode("utf-8")

    if not query:
        return func.HttpResponse(
            json.dumps({"error": "Missing 'query' parameter"}),
            status_code=400,
            mimetype="application/json",
        )

    # Safety: block mutating queries
    query_upper = query.strip().upper()
    if any(kw in query_upper for kw in ["INSERT", "DELETE", "LOAD", "CLEAR", "DROP", "CREATE"]):
        return func.HttpResponse(
            json.dumps({"error": "Only SELECT and ASK queries are allowed"}),
            status_code=403,
            mimetype="application/json",
        )

    # Execute query
    try:
        ds = _load_dataset()
        result = ds.query(query)
        serialized = result.serialize(format="json")
        if isinstance(serialized, bytes):
            serialized = serialized.decode("utf-8")

        return func.HttpResponse(
            serialized,
            mimetype="application/sparql-results+json",
            headers={
                "Access-Control-Allow-Origin": "*",
                "Cache-Control": "public, max-age=300",
            },
        )
    except Exception as e:
        logging.error(f"SPARQL query error: {e}")
        return func.HttpResponse(
            json.dumps({"error": f"Query execution failed: {str(e)}"}),
            status_code=400,
            mimetype="application/json",
        )


@app.route(route="ask", methods=["GET", "POST"])
def ask_endpoint(req: func.HttpRequest) -> func.HttpResponse:
    """Translate a natural language question to SPARQL and execute it."""
    # Extract question
    question = None
    if req.method == "GET":
        question = req.params.get("question")
    elif req.method == "POST":
        content_type = req.headers.get("Content-Type", "")
        if "application/json" in content_type:
            try:
                body = req.get_json()
                question = body.get("question")
            except ValueError:
                pass
        if not question:
            question = req.params.get("question")

    if not question:
        return func.HttpResponse(
            json.dumps({"error": "Missing 'question' parameter"}),
            status_code=400,
            mimetype="application/json",
        )

    # Translate to SPARQL
    sparql, error = translate(question)
    if error:
        status = 502 if "rate limit" in error.lower() or "api error" in error.lower() else 400
        return func.HttpResponse(
            json.dumps({"error": error, "question": question}),
            status_code=status,
            mimetype="application/json",
            headers={"Access-Control-Allow-Origin": "*"},
        )

    # Execute query
    try:
        ds = _load_dataset()
        result = ds.query(sparql)
        serialized = result.serialize(format="json")
        if isinstance(serialized, bytes):
            serialized = serialized.decode("utf-8")
        results = json.loads(serialized)
    except Exception as e:
        logging.error(f"SPARQL execution error for NL query: {e}")
        return func.HttpResponse(
            json.dumps({
                "error": f"Query execution failed: {str(e)}",
                "question": question,
                "sparql": sparql,
            }),
            status_code=400,
            mimetype="application/json",
            headers={"Access-Control-Allow-Origin": "*"},
        )

    return func.HttpResponse(
        json.dumps({
            "question": question,
            "sparql": sparql,
            "results": results,
        }),
        mimetype="application/json",
        headers={
            "Access-Control-Allow-Origin": "*",
            "Cache-Control": "public, max-age=300",
        },
    )
