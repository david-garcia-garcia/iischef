using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Dynamic;
using System.IO;
using YamlDotNet.Serialization;

namespace iischef.utils
{
    public static class UtilsYaml
    {
        /// <summary>
        /// Converts a YAML configuration file to a JTOKEN
        /// compatible JSON representation
        /// </summary>
        /// <typeparam name="TTokenType"></typeparam>
        /// <param name="source">Yaml or JSON</param>
        /// <returns></returns>
        public static TTokenType YamlOrJsonToKtoken<TTokenType>(string source)
            where TTokenType : JToken
        {
            source = EnsureJson(source);
            return UtilsJson.DeserializeObject<TTokenType>(source);
        }

        /// <summary>
        /// Convert a JSON string document to YAML
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string JsonToYaml(string source)
        {
            var expConverter = new ExpandoObjectConverter();
            var serializer = new YamlDotNet.Serialization.Serializer();

            object data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(source, expConverter);

            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, data);
                var yaml = writer.ToString();
                return yaml;
            }
        }

        public static string SerializeToJson(this object source)
        {
            return JsonConvert.SerializeObject(source, Formatting.Indented);
        }

        public static string EnsureJson(string yamlOrJson)
        {
            try
            {
                var r = new StringReader(yamlOrJson);
                var deserializer = new Deserializer();
                var yamlObject = deserializer.Deserialize(r);
                return yamlObject.SerializeToJson();
            }
#pragma warning disable CS0168 // The variable 'e' is declared but never used
            catch (Exception e)
#pragma warning restore CS0168 // The variable 'e' is declared but never used
            {
                // ignored
            }

            try
            {
                var parsedObject = JToken.Parse(yamlOrJson);
                return parsedObject.SerializeToJson();
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}
