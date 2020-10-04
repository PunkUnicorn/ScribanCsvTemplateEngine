using Newtonsoft.Json;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using YamlNodeExtensions;

namespace ScribanCsvTemplateEngine
{
    public static class Program
    {
        static int Main(string[] args)
        {           
            return Run(args);
        }

        static int Run(string[] args)
        { 
            var config = LoadYaml(args);
            if (config == null) 
                return 6;

            var inputRows = LoadAndValidateInputRows(config.input.csv_filename);
            if (inputRows.Item1 == null) 
                return 5;

            if (!ValidateHeaders(inputRows.Item1, config))
                return 4;

            if (IsNoScribanTemplates(config.template.scriban_filenames)) 
                return 3;

            if (IsScribanTemplateFilesExist(config.template.scriban_filenames)) 
                return 2;

            var templateDict = CompileScribanTemplates(config.template.scriban_filenames);
            if (templateDict == null) 
                return 1;

            FixPaths(config);

            /*                            *\

                  ██████╗  ██████╗ ██╗
                 ██╔════╝ ██╔═══██╗██║
        --       ██║  ███╗██║   ██║██║        --
                 ██║   ██║██║   ██║╚═╝
                 ╚██████╔╝╚██████╔╝██╗
                  ╚═════╝  ╚═════╝ ╚═╝  
            \*                            */

            try
            {
                Console.WriteLine($"{nameof(ScribanCsvTemplateEngine)}, featuring Scriban!\n\nLoaded configuration is...");
                Console.WriteLine(JsonConvert.SerializeObject(config, Formatting.Indented));
                Console.WriteLine("...now working, please wait...\n\n");
                ThunderbirdsAreGo(config, inputRows, templateDict);
                Console.WriteLine($"\n\nFinished! Output written to {config.output.output_path}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 9;
            }
            
            return 0;
        }

        public static void ThunderbirdsAreGo(YamlGlobalConfiguration config, (string[], IEnumerable<string[]>) inputRows, Dictionary<string, Scriban.Template> templateDict)
        {
            foreach (var templateFilename in config.template.scriban_filenames)
            { 
                var yamlFilenameForTemplateFile = GetLocalYamlFilenameForTemplateFile(templateFilename);
                var yamlForTemplateFile = GetLocalYamlForTemplateFile(yamlFilenameForTemplateFile) ?? new ExpandoObject();

                var inputHeaders = inputRows.Item1;
                foreach (var inputRow in inputRows.Item2)
                {
                    var inputRowPathPart = GetRowPathPart(inputHeaders, inputRow, config.output.csv_column_for_ouput_path_part);

                    /* Fill the model - local overrides global */
                    var model = BuildModel(config.Data, yamlFilenameForTemplateFile, yamlForTemplateFile, inputHeaders, inputRow);

                    /* Use the model */
                    var transformedTemplate
                        = templateDict[templateFilename]
                            .Render(model);

                    /* Save the transformed (template X model) */
                    var saveFilename
                        = templateFilename.StartsWith(config.template.base_path)
                            ? templateFilename.Replace(config.template.base_path, "")
                            : Path.GetFileName(templateFilename);
                    
                    var outputFn = Path.Combine(config.output.output_path, inputRowPathPart, saveFilename);

                    // Faff about
                    if (File.Exists(outputFn))
                        Console.WriteLine($"Overwriting file '{outputFn}', with row key as '{config.output.csv_column_for_ouput_path_part}', value '{inputRowPathPart}'");

                    if (!Directory.Exists(Path.GetDirectoryName(outputFn)) )
                        Directory.CreateDirectory(Path.GetDirectoryName(outputFn));

                    // Save this single transformed file back to the output
                    File.WriteAllText(outputFn, transformedTemplate);
                }
            }
        }

        private static string GetRowPathPart(string[] headers, string[] inputRow, string column_for_ouput_path)
        {
            var index = Array.IndexOf(headers, column_for_ouput_path);
            if (index == -1 || index >= inputRow.Length) throw new InvalidOperationException($"expecting column name to exist in the csv headers, with: \n{string.Join(",", headers)}\n{string.Join(",", inputRow)}\n{column_for_ouput_path}");

            return new string(inputRow[index].Where(char.IsLetterOrDigit).ToArray());
        }

        private static Dictionary<string, object> BuildModel(ExpandoObject globalData, string yamlFilenameForTemplateFile, dynamic yamlForTemplateFile, string[] inputHeaders, string[] inputRow)
        {
            var model = new Dictionary<string, object>();

            /* Template file level yaml 1st (highst order of presidence) */
            foreach (var templateFileModelItem in (yamlForTemplateFile as IDictionary<string, object>))
                if (!model.TryAdd(templateFileModelItem.Key, templateFileModelItem.Value))
                    try
                    {
                        Console.WriteLine($"possibly a duplicate key: file '{yamlFilenameForTemplateFile}', {nameof(templateFileModelItem)}.{nameof(templateFileModelItem.Key)}={templateFileModelItem.Key}");
                        continue;
                    }
                    catch (Exception e) { Console.Error.WriteLine(e.ToString()); }


            /* global yaml 2nd (second in the order of presidence) */
            foreach (var yamlModelItem in (globalData as IDictionary<string, object>))
                if (!model.TryAdd(yamlModelItem.Key, yamlModelItem.Value))
                    try
                    {
                        Console.WriteLine($"possibly a duplicate key: {nameof(yamlModelItem)}.{nameof(yamlModelItem.Key)}={yamlModelItem.Key}");
                        continue;
                    }
                    catch (Exception e) { Console.Error.WriteLine(e.ToString()); }

            /* input csv row level yaml 3rd (third order of presidence) */
            var index = 0;
            foreach (var inputModelItem in inputRow)
                if (!model.TryAdd(inputHeaders[index++], inputModelItem))
                    try
                    {
                        Console.WriteLine($"possibly a duplicate key: {inputHeaders[index - 1]}={inputModelItem}");
                        continue;
                    }
                    catch (Exception e) { Console.Error.WriteLine(e.ToString()); }

            return model;
        }

        private static string GetLocalYamlFilenameForTemplateFile(string templateFilename) => Path.ChangeExtension(templateFilename, "yaml");

        private static dynamic GetLocalYamlForTemplateFile(string yamlTemplateFilename)
        {
            if (!File.Exists(yamlTemplateFilename)) 
                return null;

            return LoadYamlUnguarded(yamlTemplateFilename);
        }

        private static Dictionary<string, Scriban.Template> CompileScribanTemplates(List<string> files)
        {
            try
            {
                var retval = new Dictionary<string, Scriban.Template>();

                foreach (var templateFilename in files)
                {
                    var compiled = Scriban.Template.Parse(File.ReadAllText(templateFilename), templateFilename, lexerOptions: new Scriban.Parsing.LexerOptions { KeepTrivia = true });
                    retval.Add(templateFilename, compiled);
                }

                return retval;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return null;
            }
        }

        private static bool IsScribanTemplateFilesExist(List<string> files)
        {
            var notFoundFiles = files.Where(doubtfulIfTheFileExists => !File.Exists(doubtfulIfTheFileExists));

            foreach (var thereIsNoFile in notFoundFiles)
                Console.Error.WriteLine($"File: '{thereIsNoFile}' not found");

            return notFoundFiles.Any();
        }

        private static bool IsNoScribanTemplates(List<string> scribanTemplateFilenames) => (scribanTemplateFilenames?.Count ?? 0) == 0;

        private static (string[], IEnumerable<string[]>) LoadAndValidateInputRows(string excelInputFilename)
        {
            var lines = File.ReadAllLines(excelInputFilename);
            if (!lines.Any()) return (null, null);

            var headers = lines[0].Split(",").Select(s => s.Trim()).ToArray();

            var rest = lines
                .Skip(1)
                .Select(sm => sm.Split(','))
                .ToList();

            return (headers, rest);
        }

        private static bool ValidateHeaders(string[] headers, YamlGlobalConfiguration config)
        {
            if (Array.IndexOf(headers, config.output.csv_column_for_ouput_path_part) != -1)
                return true;

            Console.Error.WriteLine($"column header referenced by yaml '{nameof(config.output.csv_column_for_ouput_path_part)}' ({config.output.csv_column_for_ouput_path_part}) does not exist in the csv file '{config.input.csv_filename}'");
            return false;
        }

        private static void FixPaths(YamlGlobalConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.output.output_path))
                config.output.output_path = string.Empty;
        }

        static private YamlGlobalConfiguration LoadYaml(string[] args)
        {
            try
            {
                var exePath = Assembly.GetExecutingAssembly().CodeBase;

                /* Get the yaml file from the executing program folder, with the same name as the executing dotnet console program assembly */
                var defaultYamlFilename
                    = Path.ChangeExtension(
                        Path.GetFileName(exePath), "yaml");

                /* ...but also have a look at args[0], if the default yaml filename and location does not exist... */
                var useFilename = File.Exists(defaultYamlFilename)
                    ? defaultYamlFilename
                    : args.Length > 0
                        ? args[0]
                        : throw new ArgumentException($"{defaultYamlFilename} not found.");

                return LoadYamlUnguarded<YamlGlobalConfiguration>(useFilename);
            }
            catch (Exception e)
            {
                var example = new Serializer().Serialize(new YamlGlobalConfiguration());
                Console.Error.WriteLine($@"example:\n{example}");
                Console.Error.WriteLine(e.Message);
                return null;
            }
        }

        static private T LoadYamlUnguarded<T>(string filename)
        {
            using (var reader = new StringReader(File.ReadAllText(filename)))
            {
                var yaml = new YamlStream();

                yaml.Load(reader);

                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

                return mapping.ToPoco<T>();
            }
        }

        static private dynamic LoadYamlUnguarded(string filename)
        {
            using (var reader = new StringReader(File.ReadAllText(filename)))
            {
                var yaml = new YamlStream();

                yaml.Load(reader);

                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

                return mapping.ToPoco();
            }
        }
    }
}
