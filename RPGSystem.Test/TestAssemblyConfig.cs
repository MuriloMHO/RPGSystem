using Xunit;

// Combat uses a shared static RNG in current RPGSystem; replacing it in tests
// requires serialized execution within this test assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
