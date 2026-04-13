# Branch Coverage Improvement Brainstorm

## Problem

The verified coverage baseline for the Tiktoken graph extraction work was 95.39% line coverage and 83.28% branch coverage. The branch number was low because several parser, converter, search, and Tiktoken entity-hint boundary paths were exercised only on their common paths.

## Scope

In scope:

- Add meaningful tests for public or flow-level behavior.
- Use coverage XML to target uncovered branch paths.
- Update README with verified coverage numbers and coverage-tool wording.

Out of scope:

- Adding Coverlet.
- Changing the production coverage collector.
- Refactoring defensive branches only to improve coverage.

## Options

### Tiktoken Entity-Hint Branches

Cover scalar, numeric, `value`, `same_as`, blank, null, and empty-map entity hint front matter shapes through the real Tiktoken graph flow.

### Parser And Converter Boundary Branches

Cover empty front matter, null metadata values, BOM-only source, blank YAML keys, default converter paths, media type override trimming, unsupported directory entries, and no-extension files.

### Query Search Merge Branches

Cover duplicate SPARQL result rows produced by repeated optional values, proving the search service keeps one caller-visible article result.

## Recommendation

Use all three categories because they are caller-visible boundary conditions and map directly to uncovered coverage report lines. Avoid direct private-method testing or production refactors unless a test exposes a real behavior bug.

## Result

The implemented tests raised coverage from 95.39% line / 83.28% branch to 96.30% line / 85.23% branch, with 77 tests passing.
