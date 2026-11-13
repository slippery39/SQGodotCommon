// DEV-7: test classes run in parallel, the tests within one class in order (each class keeps its own fields). The
// engine is built for concurrent games (AiMatch runs them in Parallel.For; the evaluator's switches are AsyncLocal).
// A class that can't share the machine is [NonParallelizable]; a wall-clock check is an explicit Performance test.
[assembly: Parallelizable(ParallelScope.Fixtures)]
