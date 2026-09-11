using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    // Fișierul real robot/Data/pixeldata-mappings.json, copiat în output ca data/pixeldata-mappings.json.
    public class PixelDataMappingsFileTests
    {
        // Nicio intrare reală nu folosește modalitatea asta, deci se potrivește doar „*”.
        private const string OnlyStarModality = "TEST-MODALITATE-INEXISTENTA";

        private static string ReadFile()
        {
            Assert.True(File.Exists(Fixtures.MappingsFilePath), "Lipsește " + Fixtures.MappingsFilePath);
            return File.ReadAllText(Fixtures.MappingsFilePath);
        }

        [Fact]
        public void RealFile_Parses_WithVersion1()
        {
            PixelDataMappings mappings = PixelDataMappings.FromJson(ReadFile());

            Assert.Equal("1", mappings.MappingsVersion);
            Assert.False(string.IsNullOrWhiteSpace(mappings.InitialStatus));
        }

        [Fact]
        public void RealFile_TodoResource_ResourceMappingMissing()
        {
            string json = ReadFile();
            List<KeyValuePair<string, string>> todo = UniqueTodoResources(json);
            if (todo.Count == 0)
            {
                // După completarea fișierului nu mai există TODO: adaugă o intrare fictivă, ca testul să verifice mereu ceva.
                JsonNode root = JsonNode.Parse(json);
                root["resources"].AsArray().Add(new JsonObject
                {
                    ["branchName"] = "TEST SUCURSALA INEXISTENTA",
                    ["modality"] = "*",
                    ["pixelDataResource"] = "TODO",
                });
                json = root.ToJsonString();
                todo = UniqueTodoResources(json);
            }

            PixelDataMappings mappings = PixelDataMappings.FromJson(json);
            Assert.NotEmpty(todo);
            foreach (KeyValuePair<string, string> entry in todo)
            {
                string modality = entry.Value == "*" ? OnlyStarModality : entry.Value;

                var ex = Assert.Throws<BusinessRuleViolation>(() => mappings.ResolveResource(entry.Key, modality));

                Assert.Equal("RESOURCE_MAPPING_MISSING", ex.Code);
            }
        }

        // (branchName, modality) cu pixelDataResource = "TODO", fără alte intrări pe aceeași sucursală
        // care ar putea câștiga potrivirea (exactă sau „*”).
        private static List<KeyValuePair<string, string>> UniqueTodoResources(string json)
        {
            var all = new List<string[]>();
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement resource in doc.RootElement.GetProperty("resources").EnumerateArray())
                {
                    all.Add(new[]
                    {
                        resource.GetProperty("branchName").GetString().Trim(),
                        resource.GetProperty("modality").GetString().Trim(),
                        resource.GetProperty("pixelDataResource").GetString(),
                    });
                }
            }

            var result = new List<KeyValuePair<string, string>>();
            foreach (string[] entry in all)
            {
                if (!string.Equals(entry[2], "TODO", StringComparison.Ordinal))
                {
                    continue;
                }

                int sameBranch = 0;
                foreach (string[] other in all)
                {
                    if (string.Equals(other[0], entry[0], StringComparison.OrdinalIgnoreCase))
                    {
                        sameBranch++;
                    }
                }

                if (sameBranch == 1)
                {
                    result.Add(new KeyValuePair<string, string>(entry[0], entry[1]));
                }
            }

            return result;
        }
    }
}
