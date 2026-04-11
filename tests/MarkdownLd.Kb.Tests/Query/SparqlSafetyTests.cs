using ManagedCode.MarkdownLd.Kb.Query;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class SparqlSafetyTests
{
    [Theory]
    [InlineData("INSERT DATA { <a> <b> <c> }")]
    [InlineData("DELETE WHERE { ?s ?p ?o }")]
    [InlineData("LOAD <http://example.com/data.ttl>")]
    [InlineData("CLEAR GRAPH <http://example.com/g>")]
    [InlineData("DROP GRAPH <http://example.com/g>")]
    [InlineData("CREATE GRAPH <http://example.com/g>")]
    public void RejectsMutatingQueries(string query)
    {
        var result = SparqlSafety.EnforceReadOnly(query);

        Assert.False(result.IsAllowed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Only SELECT and ASK", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT ?s WHERE { ?s ?p ?o }", "LIMIT 100")]
    [InlineData("ASK { ?s ?p ?o }", null)]
    public void AllowsReadOnlyQueries(string query, string? expectedLimit)
    {
        var result = SparqlSafety.EnforceReadOnly(query);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorMessage);
        if (expectedLimit is null)
        {
            Assert.DoesNotContain("LIMIT", result.Query, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains(expectedLimit, result.Query, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PreservesExistingLimit()
    {
        var result = SparqlSafety.EnforceReadOnly("SELECT ?s WHERE { ?s ?p ?o } LIMIT 25");

        Assert.True(result.IsAllowed);
        Assert.Contains("LIMIT 25", result.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIMIT 100", result.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsUnsupportedQueryForms()
    {
        var result = SparqlSafety.EnforceReadOnly("CONSTRUCT WHERE { ?s ?p ?o }");

        Assert.False(result.IsAllowed);
        Assert.Contains("Only SELECT and ASK", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportsSyntaxErrors()
    {
        var result = SparqlSafety.EnforceReadOnly("SELEC ?s WHERE { ?s ?p ?o }");

        Assert.False(result.IsAllowed);
        Assert.NotNull(result.ErrorMessage);
    }
}
