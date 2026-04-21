namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public static class KnowledgeGraphShaclShapes
{
    public const string DefaultTurtle = """
@prefix sh: <http://www.w3.org/ns/shacl#> .
@prefix schema: <https://schema.org/> .
@prefix kb: <urn:managedcode:markdown-ld-kb:vocab:> .
@prefix prov: <http://www.w3.org/ns/prov#> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

kb:ArticleShape a sh:NodeShape ;
  sh:targetClass schema:Article ;
  sh:property [
    sh:path schema:name ;
    sh:minCount 1 ;
    sh:nodeKind sh:Literal ;
    sh:message "Every Article must have a schema:name." ;
  ] ;
  sh:property [
    sh:path prov:wasDerivedFrom ;
    sh:minCount 1 ;
    sh:nodeKind sh:IRI ;
    sh:message "Every Article must have provenance." ;
  ] .

kb:EntityNameShape a sh:NodeShape ;
  sh:targetClass schema:Thing, schema:Person, schema:Organization, schema:SoftwareApplication, schema:CreativeWork, schema:DefinedTerm ;
  sh:property [
    sh:path schema:name ;
    sh:minCount 1 ;
    sh:nodeKind sh:Literal ;
    sh:message "Every entity must have a schema:name." ;
  ] .

kb:SameAsShape a sh:NodeShape ;
  sh:targetSubjectsOf schema:sameAs ;
  sh:property [
    sh:path schema:sameAs ;
    sh:nodeKind sh:IRI ;
    sh:message "schema:sameAs values must be IRIs." ;
  ] .

kb:ProvenanceShape a sh:NodeShape ;
  sh:targetSubjectsOf prov:wasDerivedFrom ;
  sh:property [
    sh:path prov:wasDerivedFrom ;
    sh:nodeKind sh:IRI ;
    sh:message "prov:wasDerivedFrom values must be IRIs." ;
  ] .

kb:StatementShape a sh:NodeShape ;
  sh:targetClass rdf:Statement ;
  sh:property [
    sh:path rdf:subject ;
    sh:minCount 1 ;
    sh:maxCount 1 ;
    sh:nodeKind sh:IRI ;
    sh:message "An RDF statement must have one IRI subject." ;
  ] ;
  sh:property [
    sh:path rdf:predicate ;
    sh:minCount 1 ;
    sh:maxCount 1 ;
    sh:nodeKind sh:IRI ;
    sh:message "An RDF statement must have one IRI predicate." ;
  ] ;
  sh:property [
    sh:path rdf:object ;
    sh:minCount 1 ;
    sh:maxCount 1 ;
    sh:nodeKind sh:IRI ;
    sh:message "A graph assertion must have one IRI object." ;
  ] ;
  sh:property [
    sh:path kb:confidence ;
    sh:minCount 1 ;
    sh:maxCount 1 ;
    sh:datatype xsd:decimal ;
    sh:minInclusive 0.0 ;
    sh:maxInclusive 1.0 ;
    sh:message "kb:confidence must be a decimal from 0 through 1." ;
  ] .
""";
}
