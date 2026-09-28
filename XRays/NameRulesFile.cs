using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XRays
{
    /// <summary>
    /// The name rules of the built-in categories, read from the YAML file of
    /// the BepInEx config: that file alone says what every list holds, so a
    /// list it does not carry is empty - the code holds no name rule of its
    /// own. The mod ships the file in the plugins folder and copies it into
    /// the config folder when it is missing there or written for another
    /// version of the mod, stamping its own version on the "version" line of
    /// the copy. The copy of the config folder is the file read, and read
    /// again every time it changes while the game runs.
    /// </summary>
    internal static class NameRulesFile
    {
        /// <summary>The name of the file while the config says nothing.</summary>
        internal const string DefaultFileName = "orax.XRays.names.yaml";

        /// <summary>
        /// How often the date of the file is looked at: the check costs one
        /// stat call, and a rule is never applied faster than that.
        /// </summary>
        private const float CheckInterval = 0.5f;

        /// <summary>
        /// The line starting with "version" at the left margin: the mod
        /// stamps its own version on it, so a file written for another
        /// version of the mod is replaced by the shipped one. It is taken out
        /// before the rules are read, the version is no category.
        /// </summary>
        private static readonly Regex VersionLine = new Regex(
            @"^version[ \t]*:[^\r\n]*",
            RegexOptions.Multiline | RegexOptions.Compiled
        );

        private static readonly IDeserializer Reader = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        /// <summary>The keys the file may hold: one per built-in category.</summary>
        private static readonly HashSet<string> KnownKeys = KnownCategoryKeys();

        /// <summary>Name of the file read last, and the file itself.</summary>
        private static string loadedName;

        private static string configPath;

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
        /// Puts the file on the categories, copying it from the shipped one
        /// first when the config folder holds none or one written for another
        /// version of the mod. A file that does not parse is reported and
        /// changes nothing: the rules already in force stay.
        /// </summary>
        public static void Load()
        {
            loadedName = CurrentName();
            string path = null;
            DateTime before = DateTime.MinValue;
            bool loaded = false;

            try
            {
                if (loadedName.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    throw new ArgumentException(
                        "the file name holds characters a path cannot have"
                    );
                }

                path = Path.Combine(Paths.ConfigPath, loadedName);
                before = File.GetLastWriteTimeUtc(path);

                string text = File.Exists(path) ? File.ReadAllText(path) : null;

                if (text == null || VersionOf(text) != XRays.PluginVersion)
                {
                    text = Refresh(path, text, ref before);
                }

                if (text != null)
                {
                    Apply(
                        Reader.Deserialize<Dictionary<string, Rules>>(VersionLine.Replace(text, ""))
                    );
                    loaded = true;
                    XRays.Instance?.Log.LogInfo("Name rules read from " + loadedName + ".");
                }
                else
                {
                    Apply(null);
                    XRays.Instance?.Log.LogError(
                        "XRays: "
                            + loadedName
                            + " is missing everywhere (BepInEx\\plugins, beside the mod and in BepInEx\\scripts): every category has no name rule."
                    );
                }
            }
            catch (Exception e)
            {
                XRays.Instance?.Log.LogError(
                    "XRays: " + loadedName + " could not be read, the rules stay as they are: " + e
                );
            }
            finally
            {
                // a file changed while it was read is read once more, and one
                // that does not parse waits for the next save
                configPath = path ?? configPath;

                if (loaded)
                {
                    readDate = before;
                }
                else if (configPath != null)
                {
                    readDate = File.GetLastWriteTimeUtc(configPath);
                }
            }
        }

        /// <summary>
        /// Reads the file again as soon as its date changes, or as soon as
        /// the config asks for another file, so a rule can be edited while
        /// the game runs: the categories keep a name scan for a couple of
        /// seconds, then the objects follow, hidden ones included, at the
        /// next reconciliation.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            checkTimer += deltaTime;
            if (checkTimer < CheckInterval)
            {
                return;
            }

            checkTimer = 0f;

            if (CurrentName() != loadedName)
            {
                Load();
                return;
            }

            if (configPath == null || File.GetLastWriteTimeUtc(configPath) == readDate)
            {
                return;
            }

            Load();
        }

        /// <summary>
        /// The text to read: the file of the config folder while it is up to
        /// date, the shipped file written into it otherwise. Null when there
        /// is nothing to read anywhere.
        /// </summary>
        private static string Refresh(string path, string text, ref DateTime before)
        {
            string shipped = ShippedFile();

            if (shipped == null)
            {
                if (text != null)
                {
                    XRays.Instance?.Log.LogWarning(
                        "XRays: the shipped "
                            + loadedName
                            + " could not be found, the file of the config folder could not be replaced."
                    );
                }

                return text;
            }

            string stamped = Stamp(File.ReadAllText(shipped), XRays.PluginVersion);
            File.WriteAllText(path, stamped);
            before = File.GetLastWriteTimeUtc(path);

            XRays.Instance?.Log.LogInfo(
                text != null
                    ? "Name rules file "
                        + loadedName
                        + " is written for another version of the mod: replaced by the shipped one."
                    : "Name rules file copied to " + loadedName + "."
            );

            return stamped;
        }

        /// <summary>
        /// The shipped file of that name: looked for in BepInEx\plugins, then
        /// beside the mod assembly (the plugin folder of a normal install),
        /// then in BepInEx\scripts. Null when no folder holds it.
        /// </summary>
        private static string ShippedFile()
        {
            foreach (string directory in ShippedDirectories())
            {
                string path = Path.Combine(directory, loadedName);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static IEnumerable<string> ShippedDirectories()
        {
            yield return Paths.PluginPath;

            string assembly = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assembly))
            {
                yield return Path.GetDirectoryName(assembly);
            }

            yield return Path.Combine(Paths.BepInExRootPath, "scripts");
        }

        /// <summary>
        /// The version written on the "version" line of the text, null when
        /// the text holds no such line.
        /// </summary>
        private static string VersionOf(string text)
        {
            Match match = VersionLine.Match(text);
            if (!match.Success)
            {
                return null;
            }

            string value = match.Value.Substring(match.Value.IndexOf(':') + 1).Trim();
            int end = 0;

            while (end < value.Length && !char.IsWhiteSpace(value[end]))
            {
                end++;
            }

            return value.Substring(0, end).Trim('"', '\'');
        }

        /// <summary>
        /// The text of the shipped file with the mod version on its "version"
        /// line: the line is rewritten where it stands, or written first when
        /// the file has none.
        /// </summary>
        private static string Stamp(string text, string version)
        {
            string line = "version: " + version;

            if (VersionLine.IsMatch(text))
            {
                return VersionLine.Replace(text, match => line);
            }

            return line + Environment.NewLine + text;
        }

        /// <summary>
        /// The file name the config asks for, the default one while it says
        /// nothing.
        /// </summary>
        private static string CurrentName()
        {
            string name =
                XRays.Instance != null && XRays.Instance.NameRulesFileName != null
                    ? XRays.Instance.NameRulesFileName.Value
                    : null;

            return string.IsNullOrWhiteSpace(name) ? DefaultFileName : name.Trim();
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
                if (!KnownKeys.Contains(key))
                {
                    XRays.Instance?.Log.LogWarning(
                        "XRays: " + loadedName + " holds no category named \"" + key + "\"."
                    );
                }
            }
        }

        private static HashSet<string> KnownCategoryKeys()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (Category category in Categories.BuiltIn)
            {
                keys.Add(category.Key);
            }

            return keys;
        }
    }
}
