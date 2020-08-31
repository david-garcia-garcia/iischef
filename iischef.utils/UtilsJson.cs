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
    public class UtilsJson
    {
        /// <summary>
        /// Get an associative array from a JOBJECT or JARRAY. Prepopulates the ID
        /// property of the object (in case it does not exist) with the associative array key.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, JToken> keyedFromArrayOrObject(JToken source)
        {
            Dictionary<string, JToken> result = new Dictionary<string, JToken>();

            // Our custom deserialization already has support for propagating ID information from regular arrays to
            // associative arrays.
            return UtilsJson.DeserializeObject<Dictionary<string, JToken>>(source.ToString());
        }

        public static TType DeserializeObject<TType>(string value)
        {
            return (TType)DeserializeObject(value, typeof(TType));
        }

        /// <summary>
        /// Deserializes from JSON converting all JARRAYS to associative
        /// arrays...
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object DeserializeObject(string value, Type targetType)
        {
            using (var strReader = new StringReader(value))
            {
                using (var jsonReader = new CustomJsonTextReader(strReader))
                {
                    var resolver = new CustomContractResolver();
                    var serializer = new CustomJsonSerializer { ContractResolver = resolver, ObjectCreationHandling = ObjectCreationHandling.Replace };
                    object unserialized = serializer.Deserialize(jsonReader, targetType);
                    return unserialized;
                }
            }
        }
    }
}
