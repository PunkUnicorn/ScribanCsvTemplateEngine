using System.Collections.Generic;
using System.Dynamic;

namespace YamlCodeGenThing
{
    using static YamlCodeGenThing.YamlGlobalConfigurationSections;

    public static class YamlGlobalConfigurationSections
    {
        public class InputSection
        {
            /// <summary>
            /// Filename of the Excel file, with first row header names, holding the values to be substituted in the fils in <see cref="output_path"/>
            /// </summary>
            public string csv_filename { get; set; }
        }

        public class TemplateSection
        {
            /// <summary>
            /// List of Scriban template files, with contents that will be templated from the <see cref="input_filename"/>
            /// </summary>
            public List<string> scriban_filenames { get; set; }

            /// <summary>
            /// Set this so sub folders can be preserved in the <see cref="output_path"/>. The value of this field is taken off the template full path filename to extract the resulting output folder structure.
            /// </summary>
            public string base_path { get; set; }
        }

        public class OutputSection
        {
            /// <summary>
            /// The output path where the resulting files are created
            /// </summary>
            public string output_path { get; set; }

            /// <summary>
            /// Which csv column header, from the csv file input, to use to make the unique sub-folder name to put this rows results in
            /// </summary>
            public string csv_column_for_ouput_path_part { get; set; }
        }
    }

    public class YamlGlobalConfiguration
    {
        /// <summary>
        /// Details about data row input
        /// </summary>
        public InputSection input { get; set; }

        /// <summary>
        /// Details of the template files to transform with the input rows
        /// </summary>
        public TemplateSection template { get; set; }

        /// <summary>
        /// Details of the transformed template output
        /// </summary>
        public OutputSection output { get; set; }

        /// <summary>
        /// Free form variables, passed in as part of the model for every template compiled. The structure of the yaml is preserved in the model 
        /// </summary>
        public ExpandoObject Data { get;  set; }
    }
}