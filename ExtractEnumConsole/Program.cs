#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

#endregion

namespace ExtractEnumConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var path in args)
            {
                ProcessPath(path);
            }
        }

        private static void ProcessPath(string path)
        {
            var dictionary = new Dictionary<string, List<EnumEntry>>();

            var directory = new DirectoryInfo(path);

            var files = directory.GetFiles("*.h", SearchOption.AllDirectories).Union(directory.GetFiles("*.cpp", SearchOption.AllDirectories));

            foreach (var file in files)
            {
                using (var sr = file.OpenText())
                {
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (Regex.IsMatch(line, @"^([\s|\t]+|)enum([\s|\t]+)([A-Za-z0-9_]+)([\s|\t]+|)(\/\/|)(.*)"))
                        {
                            var enumName = Regex.Replace(line, @"^([\s|\t]+|)enum([\s|\t]+)([A-Za-z0-9_]+)([\s|\t]+|)(\/\/|)(.*)", "$3");

                            dictionary.Add(enumName, new List<EnumEntry>());

                            ProcessEnum(enumName, sr, dictionary);
                        }
                    }
                }
            }

            var table = GetResourceString("Table");
            var insert = GetResourceString("Insert");

            using (var sw = new StreamWriter(".\\output.txt"))
            {
                foreach (var item in dictionary.Where(_ => _.Value.Any()))
                {
                    sw.WriteLine(table.Replace("{ENUMNAME}", item.Key));

                    foreach (var subitem in item.Value)
                    {
                        sw.WriteLine(
                            insert
                                .Replace("{ENUMNAME}", item.Key)
                                .Replace("{VALUE}", subitem.Hex + subitem.Value)
                                .Replace("{NAME}", subitem.Name)
                                .Replace("{DESCRIPTION}", string.IsNullOrWhiteSpace(subitem.Description) ? string.Empty : EncodeMySqlString(subitem.Description)));
                    }
                }
            }

            Console.WriteLine(dictionary.Count);
            Console.ReadLine();
        }

        private static string EncodeMySqlString(string value)
        {
            return value.Replace(@"\", @"\\").Replace("'", @"\'");
        }

        private static string GetResourceString(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"ExtractEnumConsole.{name}.txt";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static void ProcessEnum(string enumName, StreamReader sr, Dictionary<string, List<EnumEntry>> dictionary)
        {
            Console.WriteLine(enumName);
            var hashTable = dictionary[enumName];

            var parseEnums = false;
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (Regex.IsMatch(line, @"^([\s|\t]+|)\{"))
                {
                    parseEnums = true;
                    continue;
                }

                if (Regex.IsMatch(line, @"^([\s|\t]+|)\};"))
                    break;

                if (parseEnums)
                {
                    if (Regex.IsMatch(line, @"^([\s|\t]+|)\/\/"))
                    {
                        continue;
                    }

                    var t = Regex.Matches(line, @"^([\s|\t]+|)([A-Z0-9_]+)([\s|\t]+|)\=([\s|\t]+|)(0x[A-F0-9-]+|)([0-9-]+)(,|)([\s|\t]+|)(//|)(.*|)");
                    if (t.Count > 0)
                    {
                        hashTable.Add(new EnumEntry(t));
                    }
                }
            }
        }

        public class EnumEntry
        {
            public EnumEntry(MatchCollection mc)
            {
                Name = mc[0].Groups[2].Value;
                Hex = mc[0].Groups[5].Value;
                Value = mc[0].Groups[6].Value;
                Description = mc[0].Groups[10].Value.Trim();

                Console.WriteLine($"{Name} {Hex} {Value} {Description}");
            }

            public string Name { get; }

            public string Hex { get; }

            public string Value { get; }

            public string Description { get; }
        }
    }
}