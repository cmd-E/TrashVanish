using System;
using System.IO;

namespace TrashVanish.Tests
{
    public sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "TrashVanish.Tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Root);
        }

        public string Subdirectory(string name)
        {
            string path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            // Only this fixture's uniquely named directory is ever removed.
            Directory.Delete(Root, true);
        }
    }
}
