using ManagedCode.MarkdownLd.Kb.Query;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class SparqlSafetyTests
{
    private const string InsertQuery = "INSERT DATA { <a> <b> <c> }";
    private const string DeleteQuery = "DELETE WHERE { ?s ?p ?o }";
    private const string LoadQuery = "LOAD <http://example.com/data.ttl>";
    private const string ClearGraphQuery = "CLEAR GRAPH <http://example.com/g>";
    private const string DropGraphQuery = "DROP GRAPH <http://example.com/g>";
    private const string CreateGraphQuery = "CREATE GRAPH <http://example.com/g>";
    private const string SelectQuery = "SELECT ?s WHERE { ?s ?p ?o }";
    private const string AskQuery = "ASK { ?s ?p ?o }";
    private const string SelectWithLimitQuery = "SELECT ?s WHERE { ?s ?p ?o } LIMIT 25";
    private const string ConstructQuery = "CONSTRUCT WHERE { ?s ?p ?o }";
    private const string InvalidSelectQuery = "SELEC ?s WHERE { ?s ?p ?o }";
    private const string OnlySelectAndAskMessage = "Only SELECT and ASK";
    private const string DefaultLimit = "LIMIT 100";
    private const string ExistingLimit = "LIMIT 25";
    private const string InvalidQueryWithStringLiteralContainsMutatingKeyword = """SELEC ?label WHERE { VALUES ?label { "DELETE" } }""";
    private const string InvalidQueryWithCommentContainsMutatingKeyword = """
        SELEC ?s WHERE {
          ?s ?p ?o
        }
        # DELETE should stay inside the comment
        """;
    private const string InvalidQueryWithIriContainsMutatingKeyword = """
        SELEC ?type WHERE {
          <https://example.com/actions/delete-action> a ?type .
        }
        """;
    private const string StringLiteralContainsMutatingKeywordQuery = """
        SELECT ?label WHERE {
          VALUES ?label { "DELETE" }
        }
        """;
    private const string IriContainsMutatingKeywordQuery = """
        SELECT ?type WHERE {
          <https://example.com/actions/delete-action> a ?type .
        }
        """;
    private const string CommentContainsMutatingKeywordQuery = """
        # DELETE should not make a read-only query fail
        SELECT ?s WHERE {
          ?s ?p ?o
        }
        """;
    private const string FederatedServiceQuery = """
        SELECT ?s WHERE {
          SERVICE <https://query.wikidata.org/sparql> {
            ?s ?p ?o
          }
        }
        """;
    [Test]
    [Arguments(InsertQuery)]
    [Arguments(DeleteQuery)]
    [Arguments(LoadQuery)]
    [Arguments(ClearGraphQuery)]
    [Arguments(DropGraphQuery)]
    [Arguments(CreateGraphQuery)]
    public void RejectsMutatingQueries(string query)
    {
        var result = SparqlSafety.EnforceReadOnly(query);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain(OnlySelectAndAskMessage);
    }

    [Test]
    public void AppendsDefaultLimitToSelectQueries()
    {
        var result = SparqlSafety.EnforceReadOnly(SelectQuery);

        result.IsAllowed.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.Query.ShouldContain(DefaultLimit);
    }

    [Test]
    public void AllowsAskQueriesWithoutInjectingLimit()
    {
        var result = SparqlSafety.EnforceReadOnly(AskQuery);

        result.IsAllowed.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.Query.ShouldNotContain(DefaultLimit);
    }

    [Test]
    public void PreservesExistingLimit()
    {
        var result = SparqlSafety.EnforceReadOnly(SelectWithLimitQuery);

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain(ExistingLimit);
        result.Query.ShouldNotContain(DefaultLimit);
    }

    [Test]
    public void RejectsUnsupportedQueryForms()
    {
        var result = SparqlSafety.EnforceReadOnly(ConstructQuery);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain(OnlySelectAndAskMessage);
    }

    [Test]
    public void ReportsSyntaxErrors()
    {
        var result = SparqlSafety.EnforceReadOnly(InvalidSelectQuery);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Test]
    [Arguments(InvalidQueryWithStringLiteralContainsMutatingKeyword)]
    [Arguments(InvalidQueryWithCommentContainsMutatingKeyword)]
    [Arguments(InvalidQueryWithIriContainsMutatingKeyword)]
    public void ReportsSyntaxErrorsWithoutTreatingMaskedKeywordsAsMutation(string query)
    {
        var result = SparqlSafety.EnforceReadOnly(query);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldNotContain(OnlySelectAndAskMessage);
    }

    [Test]
    public void AllowsStringLiteralsContainingMutatingKeywords()
    {
        var result = SparqlSafety.EnforceReadOnly(StringLiteralContainsMutatingKeywordQuery);

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain(DefaultLimit);
    }

    [Test]
    public void AllowsIrisContainingMutatingKeywords()
    {
        var result = SparqlSafety.EnforceReadOnly(IriContainsMutatingKeywordQuery);

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain(DefaultLimit);
    }

    [Test]
    public void AllowsCommentsContainingMutatingKeywords()
    {
        var result = SparqlSafety.EnforceReadOnly(CommentContainsMutatingKeywordQuery);

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain(DefaultLimit);
    }

    [Test]
    public void RejectsServiceClausesByDefault()
    {
        var result = SparqlSafety.EnforceReadOnly(FederatedServiceQuery);

        result.IsAllowed.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("SERVICE");
    }

    [Test]
    public void AllowsServiceClausesWhenFederationIsExplicitlyEnabled()
    {
        var result = SparqlSafety.EnforceReadOnly(FederatedServiceQuery, allowFederatedService: true);

        result.IsAllowed.ShouldBeTrue();
        result.Query.ShouldContain(DefaultLimit);
    }
}
