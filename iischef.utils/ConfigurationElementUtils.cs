using Microsoft.Web.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace iischef.utils
{
    public static class ConfigurationElementUtils
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configuration"></param>
        /// <param name="propertyAccessExpression"></param>
        /// <param name="value"></param>
        /// <param name="skipIfSet">Do not set the value if it already exists.</param>
        public static void SetValueDynamic<T>(
            this JObject configuration,
            Expression<Func<T, object>> propertyAccessExpression,
            object value,
            bool skipIfSet = true)
        {
            MemberExpression memberExpression = null;

            if (propertyAccessExpression.Body is MemberExpression mem)
            {
                memberExpression = mem;
            }

            if (propertyAccessExpression.Body is UnaryExpression unaryExpression
                && unaryExpression.Operand is MemberExpression mem2)
            {
                memberExpression = mem2;
            }

            if (memberExpression == null)
            {
                throw new Exception("Unable to determine  property name from expression.");
            }

            var propertyName = memberExpression.Member.Name;

            if (skipIfSet && configuration.ContainsKey(propertyName))
            {
                return;
            }

            var propertyInfo = typeof(T).GetProperty(propertyName);

            if (propertyInfo == null)
            {
                throw new Exception($"Invalid property name {propertyName}");
            }

            if (!propertyInfo.PropertyType.IsInstanceOfType(value))
            {
                throw new Exception($"Invalid value for property {propertyAccessExpression.Name}");
            }

            configuration[propertyName] = JValue.FromObject(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public static void ApplyConfiguration<T>(
            JObject source,
            T destination)
            where T : ConfigurationElement
        {
            bool changed = false;
            ApplyConfiguration<T>(source, destination, new List<ConfigurationElementSchema>(), ref changed);
        }

        public static void ApplyConfiguration<T>(
            JObject source,
            T destination,
            ref bool changed)
            where T : ConfigurationElement
        {
            ApplyConfiguration<T>(source, destination, new List<ConfigurationElementSchema>(), ref changed);
        }

        private static string GetSchemaPath(List<ConfigurationElementSchema> schema)
        {
            return string.Join(".", schema.Select((i) => i.Name));
        }

        /// <summary>
        /// 
        /// </summary>
        private static void ApplyConfiguration<T>(
            JObject source,
            T destination,
            List<ConfigurationElementSchema> parentSchema,
            ref bool changed)
            where T : ConfigurationElement
        {
            if (source == null)
            {
                return;
            }

            parentSchema.Add(destination.Schema);

            foreach (var property in source)
            {
                var key = property.Key;
                var value = property.Value;

                if (value is JObject jObject)
                {
                    var childElements = destination.Schema.ChildElementSchemas.Select((i) => i.Name).ToList();

                    if (!childElements.Contains(key, StringComparer.CurrentCultureIgnoreCase))
                    {
                        throw new Exception($"Current configuration '{GetSchemaPath(parentSchema)}' does not support child element '{key}'. Supported childs are {string.Join(", ", childElements)}");
                    }

                    ApplyConfiguration(jObject, destination.GetChildElement(key), parentSchema, ref changed);
                    continue;
                }

                if (value is JArray jArray)
                {
                    // Keys for collections must be in the form o

                    ConfigurationElementCollection child;

                    if (key == "_self")
                    {
                        child = destination.GetCollection();
                    }
                    else
                    {
                        child = destination.GetCollection(key);
                    }

                    foreach (var e in jArray)
                    {
                        var arrayElement = child.CreateElement("add");

                        parentSchema.Add(arrayElement.Schema);

                        ApplyConfiguration(e as JObject, arrayElement, parentSchema, ref changed);

                        child.Add(arrayElement);

                        parentSchema.RemoveAt(parentSchema.Count - 1);
                    }

                    continue;
                }

                if (!(value is JValue jValue))
                {
                    throw new Exception("Currently only simple values supported.");
                }

                var validAttributes = destination.Schema.AttributeSchemas.Select((i) => i.Name).ToList();

                if (!validAttributes.Contains(key, StringComparer.CurrentCultureIgnoreCase))
                {
                    throw new Exception($"Invalid attribute {GetSchemaPath(parentSchema)}::{key} must be one of [{string.Join(", ", validAttributes)}]");
                }

                object currentValue;

                currentValue = destination[key];

                if (object.Equals(currentValue, jValue.Value))
                {
                    continue;
                }

                var attributeSchema = destination.Schema.AttributeSchemas[key];
                var valueToSet = ConvertToSchema(attributeSchema, jValue.Value);

                try
                {
                    destination[key] = valueToSet;
                }
                catch (Exception e)
                {
                    throw new Exception($"Unable to set attribute {GetSchemaPath(parentSchema)}::{key} of type {attributeSchema.Type} with value {valueToSet}: {e.Message}", e);
                }

                changed = true;
            }

            parentSchema.RemoveAt(parentSchema.Count - 1);
        }

        public static object ConvertToSchema(ConfigurationAttributeSchema attributeSchema, object currentValue)
        {
            switch (attributeSchema.Type)
            {
                case "timeSpan":
                    return TimeSpan.Parse(currentValue.ToString());
                case "bool":
                    if (currentValue is bool boolValue)
                    {
                        return boolValue;
                    }

                    if (currentValue.ToString() == "true" || currentValue.ToString() == "false")
                    {
                        return currentValue;
                    }

                    return bool.Parse(currentValue.ToString());
                case "enum":
                    var validValues = attributeSchema.GetEnumValues()
                        .Select((i) => i.Name.ToString());

                    if (!validValues.Contains(currentValue.ToString()))
                    {
                        throw new Exception($"Enum value {currentValue} must be one of {validValues}");
                    }

                    return currentValue;
                case "uint":
                    if (int.TryParse(currentValue.ToString(), out var intValue))
                    {
                        return intValue;
                    }

                    return currentValue;
                default:
                    return currentValue;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        public static void ValidateConfiguration<T>(
            JObject source)
        {
            List<string> unkownProperties = new List<string>();

            foreach (var property in source)
            {
                var key = property.Key;
                var value = property.Value;

                var existingProperty = typeof(T).GetProperty(key);
                if (existingProperty != null)
                {
                    unkownProperties.Add(key);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="elementTagName"></param>
        /// <param name="changed"></param>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public static ConfigurationElement FindOrCreateElement(
            this ConfigurationElementCollection collection,
            string elementTagName,
            ref bool changed,
            params string[] keyValues)
        {
            foreach (ConfigurationElement element in collection)
            {
                if (string.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase))
                {
                    bool matches = true;

                    for (int i = 0; i < keyValues.Length; i += 2)
                    {
                        object o = element.GetAttributeValue(keyValues[i]);
                        string value = null;
                        if (o != null)
                        {
                            value = o.ToString();
                        }

                        if (!string.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        return element;
                    }
                }
            }

            changed = true;

            var newElement = collection.CreateElement(elementTagName);

            for (int i = 0; i < keyValues.Length; i += 2)
            {
                newElement[keyValues[i]] = keyValues[i + 1];
            }

            collection.Add(newElement);

            return newElement;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        /// <param name="changed"></param>
        /// <returns></returns>
        public static ConfigurationElement EnsureElementAttributeValue(
            this ConfigurationElement element,
            string attributeName,
            object attributeValue,
            ref bool changed)
        {
            if (!object.Equals(element[attributeName], attributeValue))
            {
                // No properly detect if the value has changed, we need to compare
                // the vlue after setting it. This is because, i.e. enum values will
                // change to it's primary data type once set
                var originalValue = element[attributeName];
                element[attributeName] = attributeValue;

                if (!object.Equals(originalValue, element[attributeName]))
                {
                    changed = true;
                }
            }

            return element;
        }
    }
}
