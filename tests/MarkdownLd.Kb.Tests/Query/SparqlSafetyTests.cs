using Shouldly;
using ManagedCode.MarkdownLd.Kb.Query;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class SparqlSafetyTests
{
    [Test]
    [Arguments("INSERT DATA { <a> <b> <c> }")]
    [Arguments("DELETE WHERE { ?s ?p ?o }")]
    [Arguments("LOAD <http://example.com/data.ttl>")]
    [Arguments("CLEAR GRAPH <http://example.com/g>")]
    [Arguments("DROP GRAPH <http://example.com/g>")]
    [Arguments("CREATE GRAPH <http://example.com/g>")]
    public void RejectsMutatingQueries(string query)
    {
        var result = SparqlSafety.EnforceReadOnly(query);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Only SELECT and ASK");
    }

    [Test]
    public void AppendsDefaultLimitToSelectQueries()
    {
        var result = SparqlSafety.EnforceReadOnly("SELECT ?s WHERE { ?s ?p ?o }");

        result.IsAllowed.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.Query.ShouldContain("LIMIT 100");
    }

    [Test]
    public void AllowsAskQueriesWithoutInjectingLimit()
    {
        var result = SparqlSafety.EnforceReadOnly("ASK { ?s ?p ?o }");

        result.IsAllowed.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.Query.ShouldNotContain("LIMIT");
    }

    [Test]
    public void PreservesExistingLimit()
    {
        var result = SparqlSafety.EnforceReadOnly("SELECT ?s WHERE { ?s ?p ?o } LIMIT 25");

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain("LIMIT 25");
        result.Query.ShouldNotContain("LIMIT 100");
    }

    [Test]
    public void RejectsUnsupportedQueryForms()
    {
        var result = SparqlSafety.EnforceReadOnly("CONSTRUCT WHERE { ?s ?p ?o }");

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Only SELECT and ASK");
    }

    [Test]
    public void ReportsSyntaxErrors()
    {
        var result = SparqlSafety.EnforceReadOnly("SELEC ?s WHERE { ?s ?p ?o }");

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Test]
    public void AllowsStringLiteralsContainingMutatingKeywords()
    {
        var result = SparqlSafety.EnforceReadOnly("""
SELECT ?label WHERE {
  VALUES ?label { "DELETE" }
}
""");

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain("LIMIT 100");
    }
}
