using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XRays
{
    /// <summary>
    /// The name rules of the built-in categories, read from a YAML file
    /// beside the BepInEx config: the file alone says what every list holds,
    /// so a list it does not carry is empty. It is written from the rules of
    /// the code the first time it is missing, and read again every time it
    /// changes while the game runs.
    /// </summary>
    internal static class NameRulesFile
    {
        private const string FileName = "orax.XRays.names.yaml";

        /// <summary>
        /// How often the date of the file is looked at: the check costs one
        /// stat call, and a rule is never applied faster than that.
        /// </summary>
        private const float CheckInterval = 0.5f;

        private static readonly string FilePath = Path.Combine(Paths.ConfigPath, FileName);

        private static readonly IDeserializer Reader = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        /// <summary>
        /// The comment the file starts with the first time: it is the only
        /// place where the file explains itself.
        /// </summary>
        private static readonly string[] Header =
        {
            "# XRays - name rules of the built-in categories.",
            "#",
            "# This file is the only source: a list it does not carry is empty, and a",
            "# category it does not carry has no name rule at all. Deleting the file",
            "# writes the rules of the code into it again. The file is read again",
            "# every time it changes while the game runs.",
            "#",
            "# The five lists of a category, all matched against the name of the game",
            "# object:",
            "#   nameContains         case-insensitive substrings the category claims",
            "#   nameStartsWith       case-insensitive prefixes it claims",
            "#   nameMatches          regular expressions it claims, searched in the name",
            "#                        (^ and $ anchor a pattern covering the whole name)",
            "#   nameExcludes         case-insensitive substrings given away to another",
            "#                        category, which is how the ore veins are kept out",
            "#                        of the rocks",
            "#   nameExcludesMatches  regular expressions given away the same way"
        };

        /// <summary>
        /// What the code holds for every category, captured before the file is
        /// applied the first time: deleting the file brings those back.
        /// </summary>
        private static Dictionary<string, Rules> defaults;

        /// <summary>Date of the file read last (UTC), to spot a change.</summary>
        private static DateTime readDate;

        private static float checkTimer;

        /// <summary>
        /// What the file holds for one category: every list it leaves out is
        /// null, which is a category with no rule of that kind.
        /// </summary>
        private class Rules
        {
            public string[] NameContains { get; set; }
            public string[] NameStartsWith { get; set; }
            public string[] NameExcludes { get; set; }
            public string[] NameMatches { get; set; }
            public string[] NameExcludesMatches { get; set; }
        }

        /// <summary>
        /// Puts the file on the categories, writing it first when it is
        /// missing. A file that does not parse is reported and changes
        /// nothing: the rules already in force stay.
        /// </summary>
        public static void Load()
        {
            DateTime before = File.GetLastWriteTimeUtc(FilePath);
            bool loaded = false;

            try
            {
                if (defaults == null)
                {
                    defaults = CaptureDefaults();
                }

                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, DefaultsText());
                    XRays.Instance?.Log.LogInfo("Name rules written to " + FileName + ".");
                }

                Apply(Reader.Deserialize<Dictionary<string, Rules>>(File.ReadAllText(FilePath)));
                loaded = true;
                XRays.Instance?.Log.LogInfo("Name rules read from " + FileName + ".");
            }
            catch (Exception e)
            {
                XRays.Instance?.Log.LogError(
                    "XRays: " + FileName + " could not be read, the rules stay as they are: " + e
                );
            }
            finally
            {
                // a file changed while it was read is read once more, and one
                // that does not parse waits for the next save
                readDate = loaded ? before : File.GetLastWriteTimeUtc(FilePath);
            }
        }

        /// <summary>
        /// Reads the file again as soon as its date changes, so a rule can be
        /// edited while the game runs: the categories keep a name scan for a
        /// couple of seconds, then the objects follow, hidden ones included,
        /// at the next reconciliation.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            checkTimer += deltaTime;
            if (checkTimer < CheckInterval)
            {
                return;
            }

            checkTimer = 0f;

            if (File.GetLastWriteTimeUtc(FilePath) == readDate)
            {
                return;
            }

            Load();
        }

        /// <summary>
        /// Gives every category the lists of the file: a category the file
        /// does not hold, or a list it does not hold, has no rule of that
        /// kind anymore. A key naming no built-in category is reported, it is
        /// usually a typo.
        /// </summary>
        private static void Apply(Dictionary<string, Rules> read)
        {
            if (read == null)
            {
                read = new Dictionary<string, Rules>();
            }

            foreach (Category category in Categories.BuiltIn)
            {
                Rules rules;
                read.TryGetValue(category.Key, out rules);

                category.NameContains = rules?.NameContains;
                category.NameStartsWith = rules?.NameStartsWith;
                category.NameExcludes = rules?.NameExcludes;
                category.NameMatches = rules?.NameMatches;
                category.NameExcludesMatches = rules?.NameExcludesMatches;
            }

            foreach (string key in read.Keys)
            {
                if (!defaults.ContainsKey(key))
                {
                    XRays.Instance?.Log.LogWarning(
                        "XRays: " + FileName + " holds no category named \"" + key + "\"."
                    );
                }
            }
        }

        private static Dictionary<string, Rules> CaptureDefaults()
        {
            Dictionary<string, Rules> captured = new Dictionary<string, Rules>();

            foreach (Category category in Categories.DisplayOrder)
            {
                captured[category.Key] = new Rules
                {
                    NameContains = category.NameContains,
                    NameStartsWith = category.NameStartsWith,
                    NameExcludes = category.NameExcludes,
                    NameMatches = category.NameMatches,
                    NameExcludesMatches = category.NameExcludesMatches
                };
            }

            return captured;
        }

        /// <summary>The file as it is written the first time: the rules of
        /// the code, commented so they can be edited.</summary>
        private static string DefaultsText()
        {
            StringBuilder text = new StringBuilder();

            foreach (string line in Header)
            {
                text.AppendLine(line);
            }

            foreach (Category category in Categories.DisplayOrder)
            {
                Rules rules = defaults[category.Key];

                text.AppendLine();
                text.AppendLine(category.Key + ":");

                AddList(text, "nameContains", rules.NameContains);
                AddList(text, "nameStartsWith", rules.NameStartsWith);
                AddList(text, "nameExcludes", rules.NameExcludes);
                AddList(text, "nameMatches", rules.NameMatches);
                AddList(text, "nameExcludesMatches", rules.NameExcludesMatches);
            }

            return text.ToString();
        }

        private static void AddList(StringBuilder text, string key, string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            text.AppendLine("  " + key + ":");
            foreach (string value in values)
            {
                text.AppendLine("    - " + Quote(value));
            }
        }

        /// <summary>Quotes a value the YAML way, so a name or a pattern
        /// holding a colon or a hash is read back as it is.</summary>
        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
