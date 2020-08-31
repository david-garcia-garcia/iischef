using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace iischef.utils
{
    /// <summary>
    /// Use KnownType Attribute to match a divierd class based on the class given to the serilaizer
    /// Selected class will be the first class to match all properties in the json object.
    /// </summary>
    public class ArrayToDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            // FrameWork 4.5
            // return typeof(T).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
            // Otherwise
            return typeof(Type).IsAssignableFrom(objectType);
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            bool isdictionary = typeof(IDictionary).IsAssignableFrom(objectType);

            CustomContractResolver resolver = (CustomContractResolver)serializer.ContractResolver;
            resolver.Skip = true;

            if (isdictionary && reader.TokenType != JsonToken.StartObject)
            {
                IDictionary result = (IDictionary)System.Activator.CreateInstance(objectType);

                Type[] arguments = result.GetType().GetGenericArguments();
                Type keyType = arguments[0];
                Type valueType = arguments[1];

                var listedType = typeof(List<>).MakeGenericType(valueType);

                // This will loop in itself badly...
                IEnumerable temp = (IEnumerable)serializer.Deserialize(reader, listedType);

                int pos = 0;
                foreach (var o in temp)
                {
                    // We "might" be able to get the keys from the ID's themselves
                    // to provide backwards compatibility...
                    string key = this.setOrGetIdValue(pos.ToString(), o);
                    if (result.Contains(key))
                    {
                        throw new Exception("CE: Error while converting array to associative map. Possible duplicated ID's in array definition.");
                    }

                    result.Add(key, o);
                    pos++;
                }

                return result;
            }
            else
            {
                // This will loop in itself badly...
                IDictionary result = (IDictionary)serializer.Deserialize(reader, objectType);

                // Now populate ID's with keys when possible...
                foreach (string key in result.Keys)
                {
                    this.setOrGetIdValue(key, result[key], true);
                }

                return result;
            }

            throw new Exception("CE: Unsupported operation.");
        }

        /// <summary>
        /// Will try to set an ID, or try to retrieve an existing one....
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected string setOrGetIdValue(string key, object value, bool onlyset = false)
        {
            var prop = value.GetType().GetProperty("id");

            if (prop == null)
            {
                prop = value.GetType().GetProperty("Id");
            }

            if (prop != null && prop.PropertyType == typeof(string))
            {
                string currentvalue = (string)prop.GetValue(value);
                if (string.IsNullOrWhiteSpace(currentvalue))
                {
                    prop.SetValue(value, key);
                    return key;
                }
                else
                {
                    return currentvalue;
                }
            }

            if (value is JToken || value is JObject)
            {
                string id = (string)((JToken)value)["id"];
                if (id == null)
                {
                    id = (string)((JToken)value)["Id"];
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    ((JToken)value)["id"] = key;
                    return key;
                }
                else
                {
                    return id;
                }
            }

            if (!onlyset)
            {
                throw new Exception("Failure in JSON id array-to-key conversion.");
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
