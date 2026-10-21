using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TrashVanish;
using TrashVanish.Classes;
class CoreChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
    static RuleModel Rule(string id, string destination, string includes)
    {
        return new RuleModel { ruleID = id, ruleExtension = ".txt", ruleIncludes = includes, rulePath = destination,
                               ruleIsCaseSensetive = 0 };
    }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "trashvanish-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source"), destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "one.txt"), "one");
            File.WriteAllText(Path.Combine(source, "two.txtx"), "two");
            var plan = RulePlanner.Build(new[] { Rule("2", destination, ""), Rule("1", source, "") }, new SetModel[0]);
            var result = Worker.Run(source, plan, false, true, null, CancellationToken.None);
            Check(result.Copied == 1 && result.Failed == 1, "copy success/failure accounting");
            Check(File.Exists(Path.Combine(destination, "one.txt")), "fallback after failed self copy");
            Check(!File.Exists(Path.Combine(destination, "two.txtx")), "extension matching not exact");
            Check(File.Exists(Path.Combine(source, "one.txt")), "source deleted in copy-only mode");
            File.WriteAllText(Path.Combine(source, "one.txt"), "replacement");
            result = Worker.Run(source, RulePlanner.Build(new[] { Rule("1", destination, "") }, new SetModel[0]), true,
                                true, null, CancellationToken.None);
            Check(result.Moved == 1 && !File.Exists(Path.Combine(source, "one.txt")), "move finalization");
            Check(File.ReadAllText(Path.Combine(destination, "one.txt")) == "replacement", "overwrite content");
            string locked = Path.Combine(source, "locked.txt");
            File.WriteAllText(locked, "locked data");
            using (var stream = File.Open(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                var blocked = Worker.Run(source, RulePlanner.Build(new[] { Rule("1", destination, "locked") }, new SetModel[0]), true, true, null, CancellationToken.None);
                Check(blocked.Copied == 0 && blocked.Failed == 1, "locked copy falsely counted as success");
                Check(File.Exists(locked) && !File.Exists(Path.Combine(destination, "locked.txt")), "locked source lost");
            }
            using (var stream = File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var retained = Worker.Run(source, RulePlanner.Build(new[] { Rule("1", destination, "locked") }, new SetModel[0]), true, true, null, CancellationToken.None);
                Check(retained.Copied == 1 && retained.Moved == 0 && retained.Failed == 1, "delete failure accounting");
                Check(File.Exists(locked) && File.Exists(Path.Combine(destination, "locked.txt")), "delete failure lost source or destination");
            }
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                Worker.Run(source, plan, false, true, null, cancellation.Token);
                throw new Exception("No cancellation");
            }
            catch (OperationCanceledException)
            {
            }
            Console.WriteLine(
                "TrashVanish rule ordering, fallback, extension, overwrite, move and cancellation checks passed");
        }
        finally
        {
            if (!root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unsafe fixture path");
            Directory.Delete(root, true);
        }
    }
}
