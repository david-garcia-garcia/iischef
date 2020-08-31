using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.core.Configuration
{
    /// <summary>
    /// Convert to and from key-value pair data
    /// to real JObjects.
    /// </summary>
    public class JObjectToKeyValueConverter
    {
        /// <summary>
        /// Set a value in a JObject using a dot-separated path.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="path"></param>
        /// <param name="value"></param>
        protected void SetNestedValue(JToken source, string path, object value)
        {
            try
            {
                var parts = path.Split(".".ToCharArray());

                if (parts.Length == 1)
                {
                    if (source is JObject)
                    {
                        source[parts.First()] = value == null ? null : JToken.FromObject(value);
                    }
                    else
                    {
                        (source as JArray).Add(value);
                    }

                    return;
                }

                var current = parts.First();
                var next = parts.Skip(1).Take(1).SingleOrDefault();

                JToken child = next.All(char.IsDigit) ? (JToken)new JArray() : (JToken)new JObject();
                this.InitializeChild(source, child, current);
                string newPath = string.Join(".", parts.Skip(1).Take(parts.Length - 1));
                this.SetNestedValue(this.GetItemByKey(source, current), newPath, value);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to set nested value '{path}'", e);
            }
        }

        protected JToken GetItemByKey(JToken source, string key)
        {
            if (source is JObject)
            {
                return source[key.ToString()];
            }
            else if (source is JArray)
            {
                return source[Convert.ToInt32(key)];
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        protected void InitializeChild(JToken parent, JToken child, string key)
        {
            if (parent is JArray)
            {
                var jParent = parent as JArray;
                int index = Convert.ToInt32(key);
                while (jParent.Count < index + 1)
                {
                    jParent.Add(null);
                }

                if (jParent[index] == null || jParent[index].Type == JTokenType.Null)
                {
                    jParent[index] = child;
                }
            }
            else
            {
                if (parent[key] == null)
                {
                    parent[key] = child;
                }
            }
        }

        /// <summary>
        /// Convert key value pair data to a nested JObject.
        /// 
        /// mail.data : a
        /// mail.data2 : b
        /// 
        /// to 
        /// 
        /// mail ->
        ///   -> data : a
        ///   -> data : b
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public JObject keyValueToNested(Dictionary<string, string> settings)
        {
            JObject data = new JObject();

            foreach (var s in settings)
            {
                this.SetNestedValue(data, s.Key, s.Value);
            }

            return data;
        }

        /// <summary>
        /// Convert JObject to key-value. Arrays not allowed!
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public Dictionary<string, string> NestedToKeyValue(JObject settings)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            this._NestedToKeyValue(settings, string.Empty, result);
            return result;
        }

        /// <summary>
        /// JObject to key value pairs.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="path"></param>
        /// <param name="result"></param>
        protected void _NestedToKeyValue(JToken settings, string path, Dictionary<string, string> result)
        {
            if (settings is JObject)
            {
                foreach (JProperty t in (settings as JObject).Properties())
                {
                    var newPath = path + (string.IsNullOrEmpty(path) ? (t.Name) : ("." + t.Name));
                    this._NestedToKeyValue(t.Value as JToken, newPath, result);
                }
            }
            else if (settings is JValue)
            {
                result.Add(path, (settings as JValue).ToString());
            }
            else if (settings is JArray)
            {
                int index = 0;

                foreach (JToken arrayItem in (settings as JArray))
                {
                    var newPath = path + (string.IsNullOrEmpty(path) ? (index.ToString()) : ("." + index.ToString()));
                    this._NestedToKeyValue(arrayItem, newPath, result);
                    index++;
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
