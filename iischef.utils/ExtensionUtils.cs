using Microsoft.Web.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.utils
{
    public static class ExtensionUtils
    {
        public static double ToUnixTimestamp(this DateTime dateTime)
        {
            return (TimeZoneInfo.ConvertTimeToUtc(dateTime) -
                   new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
        }

        public static TType castTo<TType>(this JToken source)
        {
            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(source, Newtonsoft.Json.Formatting.Indented);
            var result = UtilsJson.DeserializeObject<TType>(serialized);
            return result;
        }

        /// <summary>
        /// Convert a single element to a list.
        /// </summary>
        /// <typeparam name="TObjectType"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<TObjectType> AsIterable<TObjectType>(this IEnumerable<TObjectType> source)
        {
            if (source != null)
            {
                return source;
            }

            return new List<TObjectType>();
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<T2> AsIterable<T1, T2>(this IDictionary<T1, T2> source)
        {
            if (source != null)
            {
                return source.Values.ToList();
            }

            return new List<T2>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IDictionary<T, T2> AsIterableDictionary<T, T2>(this IDictionary<T, T2> source)
        {
            if (source == null)
            {
                return new Dictionary<T, T2>();
            }

            return source;
        }

        public static bool HasValue(this ConfigurationElement elem, string name, string value)
        {
            string val = (string)elem.Attributes[name].Value;
            return val == value;
        }

        public static void AddOrUpdateConfigurationElementInCollection(this ConfigurationElementCollection a, string name, string value)
        {
            // We want each site to have it's own PHP.ini contained in the site
            // itself, so use the PHPRC environment variable.
            ConfigurationElement elemPHPRC = null;
            foreach (var evv in a)
            {
                if (evv.HasValue("name", name))
                {
                    elemPHPRC = evv;
                    break;
                }
            }

            bool addElemPHPRC = false;
            if (elemPHPRC == null)
            {
                elemPHPRC = a.CreateElement("environmentVariable");
                addElemPHPRC = true;
            }

            elemPHPRC.SetAttributeValue("name", name);
            elemPHPRC.SetAttributeValue("value", value);

            if (addElemPHPRC)
            {
                a.Add(elemPHPRC);
            }
        }
    }
}
