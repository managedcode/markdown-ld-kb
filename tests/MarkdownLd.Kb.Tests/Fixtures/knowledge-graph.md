---
title: Markdown-LD Knowledge Bank
description: A knowledge graph built from Markdown notes.
date_published: 2026-04-11
date_modified: 2026-04-11
tags:
  - rdf
  - sparql
  - knowledge graph
about:
  - knowledge graph
author:
  - label: ManagedCode
    type: schema:Organization
entity_hints:
  - label: RDF
    type: schema:Thing
    sameAs:
      - https://www.w3.org/RDF/
  - label: SPARQL
    type: schema:Thing
    sameAs:
      - https://www.w3.org/TR/sparql11-query/
---
# Markdown-LD Knowledge Bank

Markdown-LD Knowledge Bank uses [[RDF]] and [SPARQL](https://www.w3.org/TR/sparql11-query/).

## Graph

RDF --mentions--> SPARQL
RDF --schema:creator--> ManagedCode
Malformed --schema:mentions-->
