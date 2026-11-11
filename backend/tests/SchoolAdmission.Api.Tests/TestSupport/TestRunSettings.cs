// Each test class starts its own API with up to 40 database connections, and the
// concurrency tests use all of them. Run the classes one after another so they do not
// compete for PostgreSQL connections and turn a pool timeout into a false failure.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
