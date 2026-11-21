package CanvasPilot_KnowledgeSearch {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_KnowledgeCatalog::*;
    private import ScalarValues::Integer;

    part def KnowledgeSearch {
        doc /* Finds the cards of a knowledge catalog that fit some words by scoring the words of the query against the identifier, other names, title, example names, tags, and summary of each card with fixed weights, with a bonus for a card that every word matches and for a card whose name is the whole query, and orders the cards by score, depth, and identifier so that the same query always gives the same list. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeSearch.cs",
                "src/DemaConsulting.CanvasPilot/KnowledgeSubsystem/KnowledgeSearchResult.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/KnowledgeSearchTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/KnowledgeSubsystem/FakeKnowledgeCatalog.cs"
            );
            designFiles = ("docs/design/canvas-pilot/knowledge-subsystem/knowledge-search.md");
            verificationFiles = ("docs/verification/canvas-pilot/knowledge-subsystem/knowledge-search.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/knowledge-subsystem/knowledge-search.yaml");
        }

        // The number of results of a search when the caller does not say.
        attribute defaultMaxResults : Integer;

        // Reads the cards of the catalog, once, when the search is created.
        dependency KnowledgeSearch to KnowledgeCatalog;
    }
}
