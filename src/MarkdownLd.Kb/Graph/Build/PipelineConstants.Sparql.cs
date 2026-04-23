namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static partial class PipelineConstants
{
    internal const string EmptySparqlQueryMessage = "SPARQL query is empty";
    internal const string ReadOnlySparqlQueryMessage = "SPARQL query is not read-only.";
    internal const string ExpectedResultSetMessage = "Expected a SPARQL result set.";
    internal const string MutatingKeywordMessagePrefix = "Mutating keyword '";
    internal const string MutatingKeywordMessageSuffix = "' is not allowed";
    internal const string SelectAskOnlyMessagePrefix = "Only ASK and SELECT queries are allowed, not ";
    internal const string ExecuteSelectRequiresSelectQueryMessage = "ExecuteSelectAsync requires a SELECT query.";
    internal const string ExecuteAskRequiresAskQueryMessage = "ExecuteAskAsync requires an ASK query.";
    internal const string ExecuteFederatedSelectRequiresSelectQueryMessage = "ExecuteFederatedSelectAsync requires a SELECT query.";
    internal const string ExecuteFederatedAskRequiresAskQueryMessage = "ExecuteFederatedAskAsync requires an ASK query.";
    internal const string FederatedServiceExecutionFailedMessage = "Query execution failed because evaluating a SERVICE clause failed - this may be due to an error with the remote service";
    internal const string DuplicateFederatedLocalServiceBindingMessagePrefix = "Federated local service binding contains a duplicate endpoint: ";
    internal const string FederatedLocalResultSetExpectedMessage = "Federated local service binding expected a SPARQL result set.";
    internal const int FederatedBindingsChunkSize = 100;
    internal const int DefaultFederatedSparqlTimeoutMilliseconds = 30000;
    internal const string WikidataMainSparqlEndpointText = "https://query.wikidata.org/sparql";
    internal const string WikidataScholarlySparqlEndpointText = "https://query-scholarly.wikidata.org/sparql";
}
