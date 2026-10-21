using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrashVanish.Classes;

namespace TrashVanish
{
    public enum FileOutcome
    {
        Copied,
        Moved,
        SkippedExisting,
        CopyFailed,
        DeleteFailed,
        NoMatch
    }
    public sealed class FileProgress
    {
        public string Source, Destination, Error;
        public FileOutcome Outcome;
    }
    public sealed class RunResult
    {
        public int Copied, Moved, Failed, Skipped;
    }
    internal sealed class PlannedRule
    {
        public long ID;
        public string Extension, Includes, Destination;
        public bool CaseSensitive;
        public int Group;
        public bool Matches(string file)
        {
            return string.Equals(Path.GetExtension(file), Extension, StringComparison.OrdinalIgnoreCase) &&
                   Path.GetFileName(file).IndexOf(Includes, CaseSensitive ? StringComparison.Ordinal
                                                                          : StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
    internal static class RulePlanner
    {
        private static long Number(string value)
        {
            long n;
            if (!long.TryParse(value, out n))
                throw new FormatException("Invalid persisted rule ID");
            return n;
        }
        public static IList<PlannedRule> Build(IEnumerable<RuleModel> rules, IEnumerable<SetModel> sets)
        {
            var result = new List<PlannedRule>();
            foreach (var rule in rules)
                result.Add(Create(Number(rule.ruleID), 0, rule.ruleExtension, rule.ruleIncludes, rule.rulePath,
                                  rule.ruleIsCaseSensetive == 1));
            foreach (var set in sets.OrderBy(s => Number(s.setID)))
                foreach (var extension in set.extensions.OrderBy(e => Number(e.extensionID)))
                    result.Add(Create(Number(extension.extensionID), 3, extension.extension, extension.includes,
                                      set.targetPath, set.isCaseSensetive == 1));
            return result.OrderBy(r => r.Group).ThenBy(r => r.ID).ToList().AsReadOnly();
        }
        private static PlannedRule Create(long id, int group, string extension, string includes, string destination,
                                          bool sensitive)
        {
            includes = includes ?? "";
            if (string.IsNullOrWhiteSpace(extension) || !extension.StartsWith(".") ||
                extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Invalid extension in saved configuration");
            if (!Path.IsPathRooted(destination))
                throw new ArgumentException("Destination must be an absolute path");
            return new PlannedRule { ID = id,
                                     Group = group + (includes.Length == 0 ? 2
                                                      : sensitive          ? 0
                                                                           : 1),
                                     Extension = extension,
                                     Includes = includes,
                                     Destination = Path.GetFullPath(destination),
                                     CaseSensitive = sensitive };
        }
    }
    internal sealed class Worker
    {
        public Task<RunResult> RunVanisherAsync(string source, IList<RuleModel> rules, IList<SetModel> sets,
                                                bool deleteAfterCopy, bool overwrite, IProgress<FileProgress> progress,
                                                CancellationToken cancellation)
        {
            var plan = RulePlanner.Build(rules, sets);
            return Task.Run(() => Run(source, plan, deleteAfterCopy, overwrite, progress, cancellation), cancellation);
        }
        internal static RunResult Run(string source, IList<PlannedRule> plan, bool deleteAfterCopy, bool overwrite,
                                      IProgress<FileProgress> progress, CancellationToken cancellation)
        {
            var result = new RunResult();
            var files = Directory.GetFiles(source).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var file in files)
            {
                cancellation.ThrowIfCancellationRequested();
                bool matched = false;
                foreach (var rule in plan.Where(r => r.Matches(file)))
                {
                    matched = true;
                    string destination = Path.Combine(rule.Destination, Path.GetFileName(file));
                    string staging = null;
                    bool committed = false;
                    try
                    {
                        if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(destination),
                                          StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Source and destination refer to the same path");
                        Directory.CreateDirectory(rule.Destination);
                        if (File.Exists(destination) && !overwrite)
                        {
                            result.Skipped++;
                            Report(progress, file, destination, FileOutcome.SkippedExisting, null);
                            continue;
                        }
                        // Refuse aliases/reparse-point files and directories rather than deleting through them.
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                            throw new IOException("Reparse-point sources are not supported");
                        var directory = new DirectoryInfo(rule.Destination);
                        while (directory != null)
                        {
                            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                                throw new IOException("Reparse-point destinations are not supported");
                            directory = directory.Parent;
                        }
                        staging = Path.Combine(rule.Destination, "." + Guid.NewGuid().ToString("N") + ".tmp");
                        File.Copy(file, staging, false);
                        if (File.Exists(destination))
                        {
                            if (!overwrite)
                                throw new IOException("Destination appeared during copy");
                            if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                                throw new IOException("Reparse-point destination");
                            File.Replace(staging, destination, null);
                        }
                        else
                            File.Move(staging, destination);
                        staging = null;
                        committed = true;
                        result.Copied++;
                        if (deleteAfterCopy)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            File.Delete(file);
                            result.Moved++;
                            Report(progress, file, destination, FileOutcome.Moved, null);
                        }
                        else
                            Report(progress, file, destination, FileOutcome.Copied, null);
                        break;
                    }
                    catch (Exception error)
                    {
                        if (!(error is IOException) && !(error is UnauthorizedAccessException) &&
                            !(error is ArgumentException) && !(error is System.Security.SecurityException))
                            throw;
                        result.Failed++;
                        Report(progress, file, destination,
                               committed ? FileOutcome.DeleteFailed : FileOutcome.CopyFailed, error.Message);
                        if (committed)
                            break; // Never create another copy after a successful finalization.
                    }
                    finally
                    {
                        if (staging != null && File.Exists(staging))
                        {
                            try
                            {
                                File.Delete(staging);
                            }
                            catch (IOException)
                            {
                            }
                            catch (UnauthorizedAccessException)
                            {
                            }
                        }
                    }
                }
                if (!matched)
                {
                    result.Skipped++;
                    Report(progress, file, null, FileOutcome.NoMatch, null);
                }
            }
            return result;
        }
        private static void Report(IProgress<FileProgress> progress, string source, string destination,
                                   FileOutcome outcome, string error)
        {
            if (progress != null)
                progress.Report(
                    new FileProgress { Source = source, Destination = destination, Outcome = outcome, Error = error });
        }
    }
}
