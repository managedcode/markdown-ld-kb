using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class TiktokenMultilingualWeightingFlowTests
{
    private const string BaseUriText = "https://multi-token.example/";
    private const string EnglishPath = "content/english.md";
    private const string UkrainianPath = "content/ukrainian.md";
    private const string FrenchPath = "content/french.md";
    private const string GermanPath = "content/german.md";
    private const int QueryLimit = 1;
    private const int ExpectedSameLanguageMinimumHits = 8;
    private const int ExpectedCrossLanguageMaximumHits = 3;
    private static readonly Uri BaseUri = new(BaseUriText);

    private const string EnglishMarkdown = """
The observatory stores telescope images in a cold archive near the mountain lab.
River sensors use cached forecasts to protect orchards from frost.
The clinic schedule groups urgent vaccine visits before routine checkups.
Solar pumps move clean water into the village storage tank during daylight.
A robotics team labels damaged bridge photos before training the inspection model.
The library catalog connects oral history recordings with family names and dates.
Farm drones map dry soil zones so irrigation crews can prioritize fields.
The museum preserves ceramic fragments with humidity sensors and numbered boxes.
Harbor dispatchers compare storm alerts with cargo departure plans.
The school kitchen tracks allergy meals separately from the regular lunch menu.
""";

    private const string UkrainianMarkdown = """
Обсерваторія зберігає знімки телескопа в холодному архіві біля гірської лабораторії.
Річкові датчики використовують кешовані прогнози, щоб захистити сади від морозу.
Розклад клініки групує термінові візити вакцинації перед плановими оглядами.
Сонячні насоси переміщують чисту воду в сільський накопичувальний бак удень.
Команда робототехніки позначає фото пошкодженого мосту перед навчанням моделі інспекції.
Бібліотечний каталог поєднує записи усної історії з родинними іменами та датами.
Фермерські дрони картографують сухі зони грунту, щоб бригади поливу визначали пріоритетні поля.
Музей зберігає керамічні фрагменти з датчиками вологості та нумерованими коробками.
Диспетчери гавані порівнюють штормові попередження з планами відправлення вантажів.
Шкільна кухня відстежує алергенні страви окремо від звичайного обіднього меню.
""";

    private const string FrenchMarkdown = """
L'observatoire conserve les images du télescope dans une archive froide près du laboratoire de montagne.
Les capteurs de rivière utilisent des prévisions en cache pour protéger les vergers du gel.
Le planning de la clinique regroupe les visites urgentes de vaccination avant les contrôles courants.
Les pompes solaires déplacent l'eau propre dans le réservoir du village pendant la journée.
Une équipe de robotique étiquette les photos du pont endommagé avant d'entraîner le modèle d'inspection.
Le catalogue de la bibliothèque relie les enregistrements d'histoire orale aux noms de famille et aux dates.
Les drones agricoles cartographient les zones de sol sec afin de prioriser les équipes d'irrigation.
Le musée conserve les fragments de céramique avec des capteurs d'humidité et des boîtes numérotées.
Les répartiteurs du port comparent les alertes de tempête avec les plans de départ du fret.
La cuisine de l'école suit les repas allergènes séparément du menu de midi habituel.
""";

    private const string GermanMarkdown = """
Die Sternwarte speichert Teleskopbilder in einem kalten Archiv nahe dem Berglabor.
Flusssensoren nutzen zwischengespeicherte Vorhersagen, um Obstgärten vor Frost zu schützen.
Der Klinikplan gruppiert dringende Impftermine vor routinemäßigen Untersuchungen.
Solarpumpen bewegen sauberes Wasser tagsüber in den Speichertank des Dorfes.
Ein Robotikteam markiert Fotos einer beschädigten Brücke vor dem Training des Inspektionsmodells.
Der Bibliothekskatalog verbindet Aufnahmen mündlicher Geschichte mit Familiennamen und Daten.
Landwirtschaftliche Drohnen kartieren trockene Bodenzonen, damit Bewässerungsteams Felder priorisieren.
Das Museum bewahrt Keramikfragmente mit Feuchtigkeitssensoren und nummerierten Kisten auf.
Hafendisponenten vergleichen Sturmwarnungen mit Plänen für den Frachtabgang.
Die Schulküche verfolgt allergene Mahlzeiten getrennt vom gewöhnlichen Mittagsmenü.
""";

    private static readonly TokenDistanceExpectation[] EnglishQueries =
    [
        new("telescope images cold archive mountain lab", "The observatory stores telescope images in a cold archive near the mountain lab."),
        new("orchard frost forecasts river sensors", "River sensors use cached forecasts to protect orchards from frost."),
        new("urgent vaccine visits clinic schedule", "The clinic schedule groups urgent vaccine visits before routine checkups."),
        new("solar pump clean water village tank daylight", "Solar pumps move clean water into the village storage tank during daylight."),
        new("damaged bridge photos robotics inspection model", "A robotics team labels damaged bridge photos before training the inspection model."),
        new("oral history recordings family names library catalog", "The library catalog connects oral history recordings with family names and dates."),
        new("dry soil zones irrigation crews farm drones", "Farm drones map dry soil zones so irrigation crews can prioritize fields."),
        new("ceramic fragments humidity sensors numbered boxes", "The museum preserves ceramic fragments with humidity sensors and numbered boxes."),
        new("storm alerts cargo departure harbor dispatchers", "Harbor dispatchers compare storm alerts with cargo departure plans."),
        new("allergy meals school kitchen lunch menu", "The school kitchen tracks allergy meals separately from the regular lunch menu."),
    ];

    private static readonly TokenDistanceExpectation[] UkrainianQueries =
    [
        new("знімки телескопа холодний архів гірська лабораторія", "Обсерваторія зберігає знімки телескопа в холодному архіві біля гірської лабораторії."),
        new("річкові датчики прогнози захистити сади мороз", "Річкові датчики використовують кешовані прогнози, щоб захистити сади від морозу."),
        new("термінові візити вакцинації розклад клініки", "Розклад клініки групує термінові візити вакцинації перед плановими оглядами."),
        new("сонячні насоси чиста вода сільський бак", "Сонячні насоси переміщують чисту воду в сільський накопичувальний бак удень."),
        new("фото пошкодженого мосту робототехніка модель інспекції", "Команда робототехніки позначає фото пошкодженого мосту перед навчанням моделі інспекції."),
        new("усна історія родинні імена бібліотечний каталог", "Бібліотечний каталог поєднує записи усної історії з родинними іменами та датами."),
        new("сухі зони грунту полив фермерські дрони", "Фермерські дрони картографують сухі зони грунту, щоб бригади поливу визначали пріоритетні поля."),
        new("керамічні фрагменти датчики вологості коробки", "Музей зберігає керамічні фрагменти з датчиками вологості та нумерованими коробками."),
        new("штормові попередження відправлення вантажів гавань", "Диспетчери гавані порівнюють штормові попередження з планами відправлення вантажів."),
        new("алергенні страви шкільна кухня обіднє меню", "Шкільна кухня відстежує алергенні страви окремо від звичайного обіднього меню."),
    ];

    private static readonly TokenDistanceExpectation[] FrenchQueries =
    [
        new("images télescope archive froide laboratoire montagne", "L'observatoire conserve les images du télescope dans une archive froide près du laboratoire de montagne."),
        new("capteurs rivière prévisions protéger vergers gel", "Les capteurs de rivière utilisent des prévisions en cache pour protéger les vergers du gel."),
        new("visites urgentes vaccination planning clinique", "Le planning de la clinique regroupe les visites urgentes de vaccination avant les contrôles courants."),
        new("pompes solaires eau propre réservoir village", "Les pompes solaires déplacent l'eau propre dans le réservoir du village pendant la journée."),
        new("photos pont endommagé robotique modèle inspection", "Une équipe de robotique étiquette les photos du pont endommagé avant d'entraîner le modèle d'inspection."),
        new("histoire orale noms famille catalogue bibliothèque", "Le catalogue de la bibliothèque relie les enregistrements d'histoire orale aux noms de famille et aux dates."),
        new("zones sol sec irrigation drones agricoles", "Les drones agricoles cartographient les zones de sol sec afin de prioriser les équipes d'irrigation."),
        new("fragments céramique capteurs humidité boîtes", "Le musée conserve les fragments de céramique avec des capteurs d'humidité et des boîtes numérotées."),
        new("alertes tempête départ fret port", "Les répartiteurs du port comparent les alertes de tempête avec les plans de départ du fret."),
        new("repas allergènes cuisine école menu midi", "La cuisine de l'école suit les repas allergènes séparément du menu de midi habituel."),
    ];

    private static readonly TokenDistanceExpectation[] GermanQueries =
    [
        new("Teleskopbilder kaltes Archiv Berglabor Sternwarte", "Die Sternwarte speichert Teleskopbilder in einem kalten Archiv nahe dem Berglabor."),
        new("Flusssensoren Vorhersagen Obstgärten Frost schützen", "Flusssensoren nutzen zwischengespeicherte Vorhersagen, um Obstgärten vor Frost zu schützen."),
        new("dringende Impftermine Klinikplan Untersuchungen", "Der Klinikplan gruppiert dringende Impftermine vor routinemäßigen Untersuchungen."),
        new("Solarpumpen sauberes Wasser Speichertank Dorf", "Solarpumpen bewegen sauberes Wasser tagsüber in den Speichertank des Dorfes."),
        new("Fotos beschädigte Brücke Robotikteam Inspektionsmodell", "Ein Robotikteam markiert Fotos einer beschädigten Brücke vor dem Training des Inspektionsmodells."),
        new("mündliche Geschichte Familiennamen Bibliothekskatalog", "Der Bibliothekskatalog verbindet Aufnahmen mündlicher Geschichte mit Familiennamen und Daten."),
        new("trockene Bodenzonen Bewässerungsteams landwirtschaftliche Drohnen", "Landwirtschaftliche Drohnen kartieren trockene Bodenzonen, damit Bewässerungsteams Felder priorisieren."),
        new("Keramikfragmente Feuchtigkeitssensoren nummerierte Kisten", "Das Museum bewahrt Keramikfragmente mit Feuchtigkeitssensoren und nummerierten Kisten auf."),
        new("Sturmwarnungen Frachtabgang Hafendisponenten", "Hafendisponenten vergleichen Sturmwarnungen mit Plänen für den Frachtabgang."),
        new("allergene Mahlzeiten Schulküche Mittagsmenü", "Die Schulküche verfolgt allergene Mahlzeiten getrennt vom gewöhnlichen Mittagsmenü."),
    ];

    [Test]
    public async Task Subword_tfidf_mode_retrieves_same_language_segments_for_four_languages()
    {
        var englishGraph = await BuildTokenGraphAsync(EnglishPath, EnglishMarkdown, TokenVectorWeighting.SubwordTfIdf);
        var ukrainianGraph = await BuildTokenGraphAsync(UkrainianPath, UkrainianMarkdown, TokenVectorWeighting.SubwordTfIdf);
        var frenchGraph = await BuildTokenGraphAsync(FrenchPath, FrenchMarkdown, TokenVectorWeighting.SubwordTfIdf);
        var germanGraph = await BuildTokenGraphAsync(GermanPath, GermanMarkdown, TokenVectorWeighting.SubwordTfIdf);

        (await CountTopHitsAsync(englishGraph, EnglishQueries)).ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
        (await CountTopHitsAsync(ukrainianGraph, UkrainianQueries)).ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
        (await CountTopHitsAsync(frenchGraph, FrenchQueries)).ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
        (await CountTopHitsAsync(germanGraph, GermanQueries)).ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
    }

    [Test]
    public async Task Subword_tfidf_mode_keeps_cross_language_lexical_hits_low_without_embedding_model()
    {
        var ukrainianGraph = await BuildTokenGraphAsync(UkrainianPath, UkrainianMarkdown, TokenVectorWeighting.SubwordTfIdf);
        var frenchGraph = await BuildTokenGraphAsync(FrenchPath, FrenchMarkdown, TokenVectorWeighting.SubwordTfIdf);
        var germanGraph = await BuildTokenGraphAsync(GermanPath, GermanMarkdown, TokenVectorWeighting.SubwordTfIdf);

        (await CountAlignedHitsAsync(ukrainianGraph, EnglishQueries, UkrainianQueries)
            + await CountAlignedHitsAsync(frenchGraph, UkrainianQueries, FrenchQueries)
            + await CountAlignedHitsAsync(germanGraph, FrenchQueries, GermanQueries)
            + await CountAlignedHitsAsync(ukrainianGraph, GermanQueries, UkrainianQueries))
            .ShouldBeLessThanOrEqualTo(ExpectedCrossLanguageMaximumHits);
    }

    [Test]
    public async Task Tiktoken_mode_supports_all_local_weighting_experiments()
    {
        foreach (var weighting in Enum.GetValues<TokenVectorWeighting>())
        {
            var graph = await BuildTokenGraphAsync(EnglishPath, EnglishMarkdown, weighting);
            var hits = await CountTopHitsAsync(graph, EnglishQueries);

            graph.CanSearchByTokenDistance.ShouldBeTrue();
            hits.ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
        }
    }

    private static async Task<KnowledgeGraph> BuildTokenGraphAsync(
        string path,
        string markdown,
        TokenVectorWeighting weighting)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: new TiktokenKnowledgeGraphOptions
            {
                MaxRelatedSegments = 2,
                Weighting = weighting,
            });

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(path, markdown),
        ]);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.Tiktoken);
        return result.Graph;
    }

    private static Task<int> CountTopHitsAsync(KnowledgeGraph graph, IReadOnlyList<TokenDistanceExpectation> expectations)
    {
        return CountTopHitsAsync(graph, expectations, expectations);
    }

    private static async Task<int> CountAlignedHitsAsync(
        KnowledgeGraph graph,
        IReadOnlyList<TokenDistanceExpectation> queries,
        IReadOnlyList<TokenDistanceExpectation> expectedMatches)
    {
        queries.Count.ShouldBe(expectedMatches.Count);

        var hits = 0;
        for (var index = 0; index < queries.Count; index++)
        {
            var matches = await graph.SearchByTokenDistanceAsync(queries[index].Query, QueryLimit);
            if (matches.Single().Text == expectedMatches[index].ExpectedText)
            {
                hits++;
            }
        }

        return hits;
    }

    private static async Task<int> CountTopHitsAsync(
        KnowledgeGraph graph,
        IReadOnlyList<TokenDistanceExpectation> queries,
        IReadOnlyList<TokenDistanceExpectation> expectedMatches)
    {
        var hits = 0;
        foreach (var query in queries)
        {
            var matches = await graph.SearchByTokenDistanceAsync(query.Query, QueryLimit);
            if (expectedMatches.Any(expected => expected.ExpectedText == matches.Single().Text))
            {
                hits++;
            }
        }

        return hits;
    }

    private sealed record TokenDistanceExpectation(string Query, string ExpectedText);
}
