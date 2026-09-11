using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PixelDataProgramari.Logic.Tests
{
    // Localizează fișierele copiate în folderul de output de Logic.Tests.csproj.
    public static class Fixtures
    {
        public static string ExamplesDir
        {
            get { return Path.Combine(AppContext.BaseDirectory, "examples"); }
        }

        public static string MappingsFilePath
        {
            get { return Path.Combine(AppContext.BaseDirectory, "data", "pixeldata-mappings.json"); }
        }

        public static IEnumerable<object[]> ValidExampleFiles()
        {
            return JsonFilesIn(Path.Combine("examples", "valid"));
        }

        public static IEnumerable<object[]> BusinessInvalidExampleFiles()
        {
            return JsonFilesIn(Path.Combine("examples", "invalid", "business"));
        }

        public static string Read(string relativePath)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, relativePath));
        }

        // Returnează căi relative (numele apare lizibil în rezultatele testelor).
        private static IEnumerable<object[]> JsonFilesIn(string relativeDir)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, relativeDir);
            if (!Directory.Exists(dir))
            {
                throw new DirectoryNotFoundException("Lipsesc exemplele copiate în output: " + dir);
            }

            string[] files = Directory.GetFiles(dir, "*.json")
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
            {
                throw new InvalidOperationException("Niciun fișier .json în " + dir);
            }

            return files.Select(f => new object[] { Path.Combine(relativeDir, Path.GetFileName(f)) });
        }
    }
}
