// These tests drive two native PDF engines at once. Ours serialises its own rendering, but the QuestPDF
// reference used as an oracle does not — and it exhibits exactly the same Skia-level corruption when several of
// its renders overlap, which is itself useful corroboration that the constraint is not specific to this library.
// Running the suite sequentially keeps the comparison measuring layout rather than thread interleaving;
// concurrency of our own engine is covered deliberately by ConcurrentGenerationTests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
