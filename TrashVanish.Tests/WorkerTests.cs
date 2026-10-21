using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TrashVanish.Tests
{
    [Trait("Category", "Integration")]
    public class WorkerTests : IDisposable
    {
        private readonly TemporaryDirectory fixture = new TemporaryDirectory();
        private readonly string source;
        private readonly string destination;
        private readonly List<FileProgress> events = new List<FileProgress>();

        public WorkerTests()
        {
            source = fixture.Subdirectory("source");
            destination = fixture.Subdirectory("destination");
        }

        private string Input(string name = "report.txt", string content = "new")
        {
            string path = Path.Combine(source, name);
            File.WriteAllText(path, content);
            return path;
        }

        private RunResult Run(bool move = false, bool overwrite = true, params RuleModel[] rules)
        {
            return Worker.Run(source,
                RulePlannerTests.Plan(rules.Length == 0 ? new[] { RulePlannerTests.Rule(destination: destination) } : rules),
                move, overwrite, new InlineProgress(events.Add), CancellationToken.None);
        }

        [Theory]
        [InlineData(false, FileOutcome.Copied)]
        [InlineData(true, FileOutcome.Moved)]
        public void FinalizesCopyBeforeOptionallyDeletingSource(bool move, FileOutcome outcome)
        {
            string input = Input();

            var result = Run(move);

            Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "report.txt")));
            Assert.Equal(!move, File.Exists(input));
            Assert.Equal(1, result.Copied);
            Assert.Equal(move ? 1 : 0, result.Moved);
            Assert.Equal(0, result.Failed);
            Assert.Equal(outcome, Assert.Single(events).Outcome);
            Assert.Single(Directory.GetFiles(destination));
        }

        [Fact]
        public void EmptySourceProducesNoWork()
        {
            var result = Run();

            Assert.Equal(0, result.Copied + result.Moved + result.Failed + result.Skipped);
            Assert.Empty(events);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExistingDestinationRespectsOverwriteSetting(bool overwrite)
        {
            string input = Input();
            File.WriteAllText(Path.Combine(destination, "report.txt"), "old");

            var result = Run(move: true, overwrite: overwrite);

            Assert.Equal(overwrite ? "new" : "old", File.ReadAllText(Path.Combine(destination, "report.txt")));
            Assert.Equal(!overwrite, File.Exists(input));
            Assert.Equal(overwrite ? 1 : 0, result.Moved);
            Assert.Equal(overwrite ? FileOutcome.Moved : FileOutcome.SkippedExisting, Assert.Single(events).Outcome);
        }

        [Fact]
        public void UnmatchedFileIsReportedAndRetained()
        {
            string input = Input("report.txtx");

            var result = Run(move: true);

            Assert.Equal(1, result.Skipped);
            Assert.True(File.Exists(input));
            Assert.Empty(Directory.GetFiles(destination));
            Assert.Equal(FileOutcome.NoMatch, Assert.Single(events).Outcome);
        }

        [Fact]
        public void FailedSelfCopyFallsBackToNextMatchingRule()
        {
            string input = Input();

            var result = Run(false, true, RulePlannerTests.Rule("1", source), RulePlannerTests.Rule("2", destination));

            Assert.Equal(1, result.Failed);
            Assert.Equal(1, result.Copied);
            Assert.True(File.Exists(input));
            Assert.Equal(new[] { FileOutcome.CopyFailed, FileOutcome.Copied }, events.Select(e => e.Outcome));
        }

        [Fact]
        public void SkippedDestinationCanFallBackToAnotherRule()
        {
            Input();
            File.WriteAllText(Path.Combine(destination, "report.txt"), "old");
            string fallback = fixture.Subdirectory("fallback");

            var result = Run(false, false, RulePlannerTests.Rule("1", destination), RulePlannerTests.Rule("2", fallback));

            Assert.Equal(1, result.Copied);
            Assert.Equal(1, result.Skipped);
            Assert.Equal("old", File.ReadAllText(Path.Combine(destination, "report.txt")));
            Assert.Equal("new", File.ReadAllText(Path.Combine(fallback, "report.txt")));
        }

        [Fact]
        public void LockedSourceDoesNotCreateDestinationOrDeleteSource()
        {
            string input = Input();
            using (File.Open(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = Run(move: true);

                Assert.Equal(1, result.Failed);
                Assert.Equal(0, result.Copied);
                Assert.True(File.Exists(input));
                Assert.Empty(Directory.GetFiles(destination));
                Assert.Equal(FileOutcome.CopyFailed, Assert.Single(events).Outcome);
            }
        }

        [Fact]
        public void DeleteFailureRetainsBothCopiesAndDoesNotTryAnotherDestination()
        {
            string input = Input();
            string fallback = fixture.Subdirectory("fallback");
            using (File.Open(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = Run(true, true, RulePlannerTests.Rule("1", destination), RulePlannerTests.Rule("2", fallback));

                Assert.Equal(1, result.Copied);
                Assert.Equal(0, result.Moved);
                Assert.Equal(1, result.Failed);
                Assert.True(File.Exists(input));
                Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "report.txt")));
                Assert.Empty(Directory.GetFiles(fallback));
                Assert.Equal(FileOutcome.DeleteFailed, Assert.Single(events).Outcome);
            }
        }

        [Fact]
        public void SuccessfulCopyStopsTryingFurtherRules()
        {
            Input();
            string fallback = fixture.Subdirectory("fallback");

            Run(false, true, RulePlannerTests.Rule("1", destination), RulePlannerTests.Rule("2", fallback));

            Assert.Single(events);
            Assert.Empty(Directory.GetFiles(fallback));
        }

        [Fact]
        public void CancellationBetweenFilesRetainsCompletedWorkAndUntouchedSources()
        {
            Input("a.txt");
            Input("b.txt");
            using (var cancellation = new CancellationTokenSource())
            {
                var progress = new InlineProgress(e => cancellation.Cancel());

                Assert.Throws<OperationCanceledException>(() => Worker.Run(source,
                    RulePlannerTests.Plan(RulePlannerTests.Rule(destination: destination)), true, true,
                    progress, cancellation.Token));

                Assert.False(File.Exists(Path.Combine(source, "a.txt")));
                Assert.True(File.Exists(Path.Combine(destination, "a.txt")));
                Assert.True(File.Exists(Path.Combine(source, "b.txt")));
                Assert.False(File.Exists(Path.Combine(destination, "b.txt")));
            }
        }

        [Fact]
        public async Task AsyncEntryPointHonorsAlreadyCancelledToken()
        {
            string input = Input();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new Worker().RunVanisherAsync(source,
                    new[] { RulePlannerTests.Rule(destination: destination) }, new Classes.SetModel[0],
                    true, true, null, cancellation.Token));

                Assert.True(File.Exists(input));
                Assert.Empty(Directory.GetFiles(destination));
            }
        }

        [Fact]
        public async Task AsyncEntryPointExecutesThePlan()
        {
            Input();

            var result = await new Worker().RunVanisherAsync(source,
                new[] { RulePlannerTests.Rule(destination: destination) }, new Classes.SetModel[0],
                false, true, null, CancellationToken.None);

            Assert.Equal(1, result.Copied);
            Assert.Equal("new", File.ReadAllText(Path.Combine(destination, "report.txt")));
        }

        public void Dispose() { fixture.Dispose(); }

        private sealed class InlineProgress : IProgress<FileProgress>
        {
            private readonly Action<FileProgress> report;
            internal InlineProgress(Action<FileProgress> report) { this.report = report; }
            public void Report(FileProgress value) { report(value); }
        }
    }
}
