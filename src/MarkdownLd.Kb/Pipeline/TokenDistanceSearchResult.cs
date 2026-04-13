namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record TokenDistanceSearchResult(
    string SegmentId,
    string DocumentId,
    string Text,
    double Distance);

internal sealed record TokenizedKnowledgeSegment(
    string Id,
    string DocumentId,
    string ParentId,
    string Text,
    int LineNumber,
    TokenVector Vector);

internal sealed record TokenizedSegmentCandidate(
    string Id,
    string DocumentId,
    string ParentId,
    string Text,
    int LineNumber,
    IReadOnlyList<int> TokenIds);

internal sealed record TokenizedKnowledgeSection(
    string Id,
    string DocumentId,
    string Label);

internal sealed record TokenizedKnowledgeTopic(
    string Id,
    string DocumentId,
    string SegmentId,
    string Label,
    double Score);

internal sealed record TokenizedKnowledgeEntityHint(
    string Id,
    string DocumentId,
    string Label,
    string Type,
    IReadOnlyList<string> SameAs);

internal sealed record TokenizedKnowledgeRelation(
    string SubjectId,
    string ObjectId,
    double Distance);
