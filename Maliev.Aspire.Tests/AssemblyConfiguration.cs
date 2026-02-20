// Disable parallel test execution across test classes in this assembly.
// Domain integration tests (MalievTestBase subclasses) each start a full
// DistributedApplicationFactory. Running multiple factories in parallel
// exhausts system resources and causes Polly timeout failures during DCP startup.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
