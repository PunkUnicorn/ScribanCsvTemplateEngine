using Newtonsoft.Json;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace YamlNodeExtensions
{
    public static class YamlNodeExtensions
    {
        /// <summary>
        /// The raw YAML property name is passed in, and the function is expected to return the name of the resulting POCO property
        /// </summary>
        /// <param name="uncleanNameToBeCleaned">The raw YAML property name</param>
        /// <returns>The new property name, for the resulting POCO</returns>
        public delegate string CleanNameDelegate(string uncleanNameToBeCleaned);

        public static string ToJson(this YamlNode topNode, CleanNameDelegate cleanNameFunc = null)
        {
            var poco = ToPoco(topNode, cleanNameFunc);
            return JsonConvert.SerializeObject(poco);
        }

        public static T ToPoco<T>(this YamlNode topNode, CleanNameDelegate cleanNameFunc = null)
        {
            var json = ToJson(topNode, cleanNameFunc);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static dynamic ToPoco(this YamlNode topNode, CleanNameDelegate cleanNameFunc = null)
        {
            var expandoDict =
                new ExpandoObject()
                    as IDictionary<string, object>;

            switch (topNode)
            {
                case YamlMappingNode mapping:
                    ProcessMappingChildren(expandoDict, mapping);
                    break;

                case YamlSequenceNode sequence:
                    return sequence.Select(s => ToPoco(s, cleanNameFunc)).ToArray();

                case YamlScalarNode scalar:
                    var v = ProcessScalarNode(scalar);
                    return v;
            }

            return expandoDict;
        }

        private static void ProcessMappingChildren(IDictionary<string, object> expandoDict, YamlMappingNode mapping, CleanNameDelegate cleanNameFunc = null)
        {
            if (cleanNameFunc == null)
                cleanNameFunc = DefaultCleanNameFunc;

            foreach (var entry in mapping.Children)
            {
                dynamic v;

                switch (entry.Value)
                {
                    case YamlScalarNode scalerNode:
                        v = ProcessScalarNode(scalerNode);
                        break;

                    case YamlMappingNode mappingNode:
                        v = ToPoco(mappingNode, cleanNameFunc);
                        break;

                    case YamlSequenceNode sequenceNode:
                        v = sequenceNode.Children.Select(s => ToPoco(s, cleanNameFunc)).ToArray();
                        break;

                    default:
                        v = "";
                        break;
                }
                var key = cleanNameFunc( ((YamlScalarNode)entry.Key).Value );
                expandoDict.Add(key, v);
            }
        }

        private static dynamic ProcessScalarNode(YamlScalarNode scalerNode)
        {
            dynamic v;
            if (int.TryParse(scalerNode.Value, out var i))
                v = i;
            else if (decimal.TryParse(scalerNode.Value, out var dec))
                v = dec;
            else if (bool.TryParse(scalerNode.Value, out var bo))
                v = bo;
            else
                v = scalerNode.Value;
            return v;
        }

        /// <summary>
        /// Convert from snake_case and/or dash-separated identifiers, to TitleCase, and a bit of other faffery
        /// </summary>
        private static string DefaultCleanNameFunc(string uncleanNameToBeCleaned)
        {
            var semiClean
                = new string(
                    uncleanNameToBeCleaned.Select(s => char.IsLetterOrDigit(s) ? s : '_').ToArray()
                )
                .Trim('_');

            if (semiClean.Length == 0) return string.Empty;

            //var cleaned = new StringBuilder();
            //cleaned.Append(semiClean[semiClean.Length - 1]);

            //var dirtyDirtyIndex = semiClean.Length - 2;
            //do
            //{
            //    var candidate = semiClean[dirtyDirtyIndex];
            //    if (candidate == '_')
            //    {
            //        // ...then make the next character uppercase. Safe because we start with length - 2
            //        cleaned[cleaned.Length - 1] = char.ToUpper(cleaned[cleaned.Length - 1]);
            //        continue;
            //    }
            //    cleaned.Append(candidate);
            //}
            //while (--dirtyDirtyIndex >= 0);

            //var almostFinished = cleaned.ToString().Reverse().ToArray();
            //var firstCharacter = char.IsNumber(almostFinished[0])
            //    ? ' '
            //    : char.ToUpper(almostFinished[0]);

            var almostFinished = semiClean;
            var firstCharacter = char.IsNumber(almostFinished[0])
                ? ' '
                : almostFinished[0];

            /* Join the first char with the rest of the string, and trim in case we chopped a number off the start */
            return $"{firstCharacter}{new string(almostFinished.Skip(1).ToArray())}".Trim();
        }
    }
}
