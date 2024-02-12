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

        public static bool HasValue(this Microsoft.Web.Administration.ConfigurationElement elem, string name, string value)
        {
            string val = (string)elem.Attributes[name].Value;
            return val == value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="containerElementName"></param>
        public static void UpsertNameValueElementInCollection(
            this ConfigurationElementCollection a, 
            string name, 
            string value,
            string containerElementName)
        {
            // We want each site to have it's own PHP.ini contained in the site
            // itself, so use the PHPRC environment variable.
            ConfigurationElement configurationElement = null;

            foreach (var evv in a)
            {
                if (evv.HasValue("name", name))
                {
                    configurationElement = evv;
                    break;
                }
            }

            bool elementIsNew = false;

            if (configurationElement == null)
            {
                configurationElement = a.CreateElement(containerElementName);
                elementIsNew = true;
            }

            configurationElement.SetAttributeValue("name", name);
            configurationElement.SetAttributeValue("value", value);

            if (elementIsNew)
            {
                a.Add(configurationElement);
            }
        }
    }
}
