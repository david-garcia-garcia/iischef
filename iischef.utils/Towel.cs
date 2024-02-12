using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace Towel
{
    /// <summary>
    /// 
    /// </summary>
    public static class Towel
    {
        /// <summary>
        /// 
        /// </summary>
        public static ConcurrentBag<Assembly> LoadedAssemblies = new ConcurrentBag<Assembly>();

        /// <summary>
        /// 
        /// </summary>
        public static Dictionary<string, string> LoadedXmlDocumentation = new Dictionary<string, string>();

        /// <summary>Gets the XML name of an <see cref="Type"/> as it appears in the XML docs.</summary>
        /// <param name="type">The field to get the XML name of.</param>
        /// <returns>The XML name of <paramref name="type"/> as it appears in the XML docs.</returns>
        public static string GetXmlName(this Type type)
        {
            _ = type ?? throw new ArgumentNullException(nameof(type));
            _ = type.FullName ?? throw new ArgumentException($"{nameof(type)}.{nameof(Type.FullName)} is null", nameof(type));
            LoadXmlDocumentation(type.Assembly);
            return "T:" + GetXmlNameTypeSegment(type.FullName);
        }

        /// <summary>Gets the XML name of an <see cref="MethodInfo"/> as it appears in the XML docs.</summary>
        /// <param name="methodInfo">The field to get the XML name of.</param>
        /// <returns>The XML name of <paramref name="methodInfo"/> as it appears in the XML docs.</returns>
        public static string GetXmlName(this MethodInfo methodInfo)
        {
            _ = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            _ = methodInfo.DeclaringType ?? throw new ArgumentException($"{nameof(methodInfo)}.{nameof(Type.DeclaringType)} is null", nameof(methodInfo));
            return GetXmlNameMethodBase(methodInfo: methodInfo);
        }

        /// <summary>Gets the XML name of an <see cref="ConstructorInfo"/> as it appears in the XML docs.</summary>
        /// <param name="constructorInfo">The field to get the XML name of.</param>
        /// <returns>The XML name of <paramref name="constructorInfo"/> as it appears in the XML docs.</returns>
        public static string GetXmlName(this ConstructorInfo constructorInfo)
        {
            _ = constructorInfo ?? throw new ArgumentNullException(nameof(constructorInfo));
            _ = constructorInfo.DeclaringType ?? throw new ArgumentException($"{nameof(constructorInfo)}.{nameof(Type.DeclaringType)} is null", nameof(constructorInfo));
            return GetXmlNameMethodBase(constructorInfo: constructorInfo);
        }

        /// <summary>Gets the XML name of an <see cref="PropertyInfo"/> as it appears in the XML docs.</summary>
        /// <param name="propertyInfo">The field to get the XML name of.</param>
        /// <returns>The XML name of <paramref name="propertyInfo"/> as it appears in the XML docs.</returns>
        public static string GetXmlName(this PropertyInfo propertyInfo)
        {
            _ = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            _ = propertyInfo.DeclaringType ?? throw new ArgumentException($"{nameof(propertyInfo)}.{nameof(Type.DeclaringType)} is null", nameof(propertyInfo));
            _ = propertyInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(propertyInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(propertyInfo));
            return "P:" + GetXmlNameTypeSegment(propertyInfo.DeclaringType.FullName) + "." + propertyInfo.Name;
        }

        /// <summary>Gets the XML name of an <see cref="FieldInfo"/> as it appears in the XML docs.</summary>
        /// <param name="fieldInfo">The field to get the XML name of.</param>
        /// <returns>The XML name of <paramref name="fieldInfo"/> as it appears in the XML docs.</returns>
        public static string GetXmlName(this FieldInfo fieldInfo)
        {
            _ = fieldInfo ?? throw new ArgumentNullException(nameof(fieldInfo));
            _ = fieldInfo.DeclaringType ?? throw new ArgumentException($"{nameof(fieldInfo)}.{nameof(Type.DeclaringType)} is null", nameof(fieldInfo));
            _ = fieldInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(fieldInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(fieldInfo));
            return "F:" + GetXmlNameTypeSegment(fieldInfo.DeclaringType.FullName) + "." + fieldInfo.Name;
        }

        /// <summary>Gets the XML name of an <see cref="EventInfo"/> as it appears in the XML docs.</summary>
        /// <param name="eventInfo">The event to get the XML name of.</param>
        /// <returns>The XML name of <paramref name="eventInfo"/> as it appears in the XML docs.</returns>
        public static string GetXmlName(this EventInfo eventInfo)
        {
            _ = eventInfo ?? throw new ArgumentNullException(nameof(eventInfo));
            _ = eventInfo.DeclaringType ?? throw new ArgumentException($"{nameof(eventInfo)}.{nameof(Type.DeclaringType)} is null", nameof(eventInfo));
            _ = eventInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(eventInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(eventInfo));
            return "E:" + GetXmlNameTypeSegment(eventInfo.DeclaringType.FullName) + "." + eventInfo.Name;
        }

        public static string
            GetXmlNameMethodBase(MethodInfo methodInfo = null, ConstructorInfo constructorInfo = null)
        {
            if (methodInfo != null && constructorInfo != null)
            {
                throw new Exception($"{nameof(GetDocumentation)} {nameof(methodInfo)} is not null && {nameof(constructorInfo)} is not null");
            }

            if (methodInfo != null)
            {
                if (methodInfo.DeclaringType is null)
                {
                    throw new ArgumentException($"{nameof(methodInfo)}.{nameof(Type.DeclaringType)} is null");
                }
                else if (methodInfo.DeclaringType.IsGenericType)
                {
                    methodInfo = methodInfo.DeclaringType.GetGenericTypeDefinition().GetMethods(
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.NonPublic).First(x => x.MetadataToken == methodInfo.MetadataToken);
                }
            }

            MethodBase methodBase = methodInfo ?? (MethodBase)constructorInfo;
            _ = methodBase ?? throw new Exception($"{nameof(GetDocumentation)} {nameof(methodInfo)} is null && {nameof(constructorInfo)} is null");
            _ = methodBase.DeclaringType ?? throw new ArgumentException($"{nameof(methodBase)}.{nameof(Type.DeclaringType)} is null");

            LoadXmlDocumentation(methodBase.DeclaringType.Assembly);

            Dictionary<string, int> typeGenericMap = new Dictionary<string, int>();
            Type[] typeGenericArguments = methodBase.DeclaringType.GetGenericArguments();
            for (int i = 0; i < typeGenericArguments.Length; i++)
            {
                Type typeGeneric = typeGenericArguments[i];
                typeGenericMap[typeGeneric.Name] = i;
            }

            Dictionary<string, int> methodGenericMap = new Dictionary<string, int>();
            if (constructorInfo is null)
            {
                Type[] methodGenericArguments = methodBase.GetGenericArguments();
                for (int i = 0; i < methodGenericArguments.Length; i++)
                {
                    Type methodGeneric = methodGenericArguments[i];
                    methodGenericMap[methodGeneric.Name] = i;
                }
            }

            ParameterInfo[] parameterInfos = methodBase.GetParameters();

            string memberTypePrefix = "M:";
            string declarationTypeString = GetXmlDocumenationFormattedString(methodBase.DeclaringType, false, typeGenericMap, methodGenericMap);
            string memberNameString =
                constructorInfo != null ? "#ctor" :
                methodBase.Name;
            string methodGenericArgumentsString =
                methodGenericMap.Count > 0 ?
                "``" + methodGenericMap.Count :
                string.Empty;
            string parametersString =
                parameterInfos.Length > 0 ?
                "(" + string.Join(",", methodBase.GetParameters().Select(x => GetXmlDocumenationFormattedString(x.ParameterType, true, typeGenericMap, methodGenericMap))) + ")" :
                string.Empty;

            string key =
                memberTypePrefix +
                declarationTypeString +
                "." +
                memberNameString +
                methodGenericArgumentsString +
                parametersString;

            if (methodInfo != null &&
                (methodBase.Name is "op_Implicit" ||
                methodBase.Name is "op_Explicit"))
            {
                key += "~" + GetXmlDocumenationFormattedString(methodInfo.ReturnType, true, typeGenericMap, methodGenericMap);
            }

            return key;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="isMethodParameter"></param>
        /// <param name="typeGenericMap"></param>
        /// <param name="methodGenericMap"></param>
        /// <returns></returns>
        public static string GetXmlDocumenationFormattedString(
            Type type,
            bool isMethodParameter,
            Dictionary<string, int> typeGenericMap,
            Dictionary<string, int> methodGenericMap)
        {
            if (type.IsGenericParameter)
            {
                var success = methodGenericMap.ContainsKey(type.Name);

                return success
                    ? "``" + methodGenericMap[type.Name]
                    : "`" + typeGenericMap[type.Name];
            }

            if (type.HasElementType)
            {
                string elementTypeString = GetXmlDocumenationFormattedString(
                    type.GetElementType() ?? throw new ArgumentException($"{nameof(type)}.{nameof(Type.HasElementType)} && {nameof(type)}.{nameof(Type.GetElementType)}() is null", nameof(type)),
                    isMethodParameter,
                    typeGenericMap,
                    methodGenericMap);

                if (type.IsPointer)
                {
                    return elementTypeString + "*";
                }

                if (type.IsByRef)
                {
                    return elementTypeString + "@";
                }

                if (type.IsArray)
                {
                    int rank = type.GetArrayRank();
                    string arrayDimensionsString = rank > 1
                        ? "[" + string.Join(",", Enumerable.Repeat("0:", rank)) + "]"
                        : "[]";
                    return elementTypeString + arrayDimensionsString;
                }

                throw new Exception($"{nameof(GetXmlDocumenationFormattedString)} encountered an unhandled element type: {type}");
            }

            string prefaceString = type.IsNested
                ? GetXmlDocumenationFormattedString(
                    type.DeclaringType ?? throw new ArgumentException($"{nameof(type)}.{nameof(Type.IsNested)} && {nameof(type)}.{nameof(Type.DeclaringType)} is null", nameof(type)),
                    isMethodParameter,
                    typeGenericMap,
                    methodGenericMap) + "."
                : type.Namespace + ".";

            string typeNameString = isMethodParameter
                ? Regex.Replace(type.Name, @"`\d+", string.Empty)
                : type.Name;

            string genericArgumentsString = type.IsGenericType && isMethodParameter
                ? "{" + string.Join(
                    ",",
                    type.GetGenericArguments().Select(argument =>
                        GetXmlDocumenationFormattedString(
                            argument,
                            isMethodParameter,
                            typeGenericMap,
                            methodGenericMap))) + "}"
                : string.Empty;

            return prefaceString + typeNameString + genericArgumentsString;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="typeFullNameString"></param>
        /// <returns></returns>
        public static string GetXmlNameTypeSegment(string typeFullNameString) =>
            Regex.Replace(typeFullNameString, @"\[.*\]", string.Empty).Replace('+', '.');

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static bool LoadXmlDocumentation(Assembly assembly)
        {
            if (LoadedAssemblies.Contains(assembly))
            {
                return false;
            }

            bool newContent = false;

            var filePath = LocateXmlDocumentationFile(assembly);

            if (filePath == null)
            {
                LoadedAssemblies.Add(assembly);
                return newContent;
            }

            try
            {
                using (StreamReader streamReader = new StreamReader(filePath))
                {
                    LoadXmlDocumentationNoLock(streamReader);
                    newContent = true;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error reading xml documentation file: " + filePath, e);
            }

            LoadedAssemblies.Add(assembly);
            return newContent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static string LocateXmlDocumentationFile(Assembly assembly)
        {
            List<string> possibleAssemblyPaths = new List<string>();
            possibleAssemblyPaths.Add(assembly.Location);
            possibleAssemblyPaths.Add(new Uri(assembly.CodeBase).LocalPath);

            foreach (var possibleAssemblyPath in possibleAssemblyPaths)
            {
                string xmlFilePath = Path.ChangeExtension(possibleAssemblyPath, ".xml");

                if (File.Exists(xmlFilePath))
                {
                    return xmlFilePath;
                }
            }

            return null;
        }

        /// <summary>Loads the XML code documentation into memory so it can be accessed by extension methods on reflection types.</summary>
        /// <param name="xmlDocumentation">The content of the XML code documentation.</param>
        public static void LoadXmlDocumentation(string xmlDocumentation)
        {
            using (StringReader stringReader = new StringReader(xmlDocumentation))
            {
                LoadXmlDocumentation(stringReader);
            }
        }

        /// <summary>Loads the XML code documentation into memory so it can be accessed by extension methods on reflection types.</summary>
        /// <param name="textReader">The text reader to process in an XmlReader.</param>
        public static void LoadXmlDocumentation(TextReader textReader)
        {
            LoadXmlDocumentationNoLock(textReader);
        }

        public static void LoadXmlDocumentationNoLock(TextReader textReader)
        {
            using (XmlReader xmlReader = XmlReader.Create(textReader))
            {
                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType is XmlNodeType.Element && xmlReader.Name is "member")
                    {
                        string rawName = xmlReader["name"];
                        if (!string.IsNullOrWhiteSpace(rawName))
                        {
                            LoadedXmlDocumentation[rawName] = xmlReader.ReadInnerXml();
                        }
                    }
                }
            }
        }

        public static string GetDocumentation(string key, Assembly assembly)
        {
            if (LoadedXmlDocumentation.ContainsKey(key))
            {
                return LoadedXmlDocumentation[key];
            }

            if (LoadXmlDocumentation(assembly))
            {
                if (LoadedXmlDocumentation.ContainsKey(key))
                {
                    return LoadedXmlDocumentation[key];
                }
            }

            return null;
        }

        /// <summary>Gets the XML documentation on a type.</summary>
        /// <param name="type">The type to get the XML documentation of.</param>
        /// <returns>The XML documentation on the type.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this Type type)
        {
            _ = type ?? throw new ArgumentNullException(nameof(type));
            _ = type.FullName ?? throw new ArgumentException($"{nameof(type)}.{nameof(Type.FullName)} is null", nameof(type));
            return GetDocumentation(type.GetXmlName(), type.Assembly);
        }

        /// <summary>Gets the XML documentation on a method.</summary>
        /// <param name="methodInfo">The method to get the XML documentation of.</param>
        /// <returns>The XML documentation on the method.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this MethodInfo methodInfo)
        {
            _ = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            _ = methodInfo.DeclaringType ?? throw new ArgumentException($"{nameof(methodInfo)}.{nameof(Type.DeclaringType)} is null", nameof(methodInfo));
            return GetDocumentation(methodInfo.GetXmlName(), methodInfo.DeclaringType.Assembly);
        }

        /// <summary>Gets the XML documentation on a constructor.</summary>
        /// <param name="constructorInfo">The constructor to get the XML documentation of.</param>
        /// <returns>The XML documentation on the constructor.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this ConstructorInfo constructorInfo)
        {
            _ = constructorInfo ?? throw new ArgumentNullException(nameof(constructorInfo));
            _ = constructorInfo.DeclaringType ?? throw new ArgumentException($"{nameof(constructorInfo)}.{nameof(Type.DeclaringType)} is null", nameof(constructorInfo));
            return GetDocumentation(constructorInfo.GetXmlName(), constructorInfo.DeclaringType.Assembly);
        }

        /// <summary>Gets the XML documentation on a property.</summary>
        /// <param name="propertyInfo">The property to get the XML documentation of.</param>
        /// <returns>The XML documentation on the property.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this PropertyInfo propertyInfo)
        {
            _ = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            _ = propertyInfo.DeclaringType ?? throw new ArgumentException($"{nameof(propertyInfo)}.{nameof(Type.DeclaringType)} is null", nameof(propertyInfo));
            _ = propertyInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(propertyInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(propertyInfo));
            return GetDocumentation(propertyInfo.GetXmlName(), propertyInfo.DeclaringType.Assembly);
        }

        /// <summary>Gets the XML documentation on a field.</summary>
        /// <param name="fieldInfo">The field to get the XML documentation of.</param>
        /// <returns>The XML documentation on the field.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this FieldInfo fieldInfo)
        {
            _ = fieldInfo ?? throw new ArgumentNullException(nameof(fieldInfo));
            _ = fieldInfo.DeclaringType ?? throw new ArgumentException($"{nameof(fieldInfo)}.{nameof(Type.DeclaringType)} is null", nameof(fieldInfo));
            _ = fieldInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(fieldInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(fieldInfo));
            return GetDocumentation(fieldInfo.GetXmlName(), fieldInfo.DeclaringType.Assembly);
        }

        /// <summary>Gets the XML documentation on an event.</summary>
        /// <param name="eventInfo">The event to get the XML documentation of.</param>
        /// <returns>The XML documentation on the event.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this EventInfo eventInfo)
        {
            _ = eventInfo ?? throw new ArgumentNullException(nameof(eventInfo));
            _ = eventInfo.DeclaringType ?? throw new ArgumentException($"{nameof(eventInfo)}.{nameof(Type.DeclaringType)} is null", nameof(eventInfo));
            _ = eventInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(eventInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(eventInfo));
            return GetDocumentation(eventInfo.GetXmlName(), eventInfo.DeclaringType.Assembly);
        }

        /// <summary>Gets the XML documentation on a member.</summary>
        /// <param name="memberInfo">The member to get the XML documentation of.</param>
        /// <returns>The XML documentation on the member.</returns>
        /// <remarks>The XML documentation must be loaded into memory for this function to work.</remarks>
        public static string GetDocumentation(this MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                    _ = fieldInfo.DeclaringType ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(Type.DeclaringType)} is null", nameof(memberInfo));
                    _ = fieldInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(memberInfo));
                    return fieldInfo.GetDocumentation();
                case PropertyInfo propertyInfo:
                    _ = propertyInfo.DeclaringType ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(Type.DeclaringType)} is null", nameof(memberInfo));
                    _ = propertyInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(memberInfo));
                    return propertyInfo.GetDocumentation();
                case EventInfo eventInfo:
                    _ = eventInfo.DeclaringType ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(Type.DeclaringType)} is null", nameof(memberInfo));
                    _ = eventInfo.DeclaringType.FullName ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(EventInfo.DeclaringType)}.{nameof(Type.FullName)} is null", nameof(memberInfo));
                    return eventInfo.GetDocumentation();
                case ConstructorInfo constructorInfo:
                    _ = constructorInfo.DeclaringType ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(Type.DeclaringType)} is null", nameof(memberInfo));
                    return constructorInfo.GetDocumentation();
                case MethodInfo methodInfo:
                    _ = methodInfo.DeclaringType ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(Type.DeclaringType)} is null", nameof(memberInfo));
                    return methodInfo.GetDocumentation();
                case Type type:
                    _ = type.FullName ?? throw new ArgumentException($"{nameof(memberInfo)}.{nameof(Type.FullName)} is null", nameof(memberInfo));
                    return type.GetDocumentation();
                case null:
                    throw new ArgumentNullException(nameof(memberInfo));
                default:
                    throw new Exception($"{nameof(GetDocumentation)} encountered an unhandled {nameof(MemberInfo)} type: {memberInfo}");
            }
        }

        /// <summary>Gets the XML documentation for a parameter.</summary>
        /// <param name="parameterInfo">The parameter to get the XML documentation for.</param>
        /// <returns>The XML documenation of the parameter.</returns>
        public static string GetDocumentation(this ParameterInfo parameterInfo)
        {
            _ = parameterInfo ?? throw new ArgumentNullException(nameof(parameterInfo));
            string memberDocumentation = parameterInfo.Member.GetDocumentation();

            if (memberDocumentation != null)
            {
                string regexPattern =
                    Regex.Escape($@"<param name=""{parameterInfo.Name}"">") +
                    ".*?" +
                    Regex.Escape($@"</param>");

                Match match = Regex.Match(memberDocumentation, regexPattern);
                if (match.Success)
                {
                    return match.Value;
                }
            }

            return null;
        }

        /// <summary>Determines if a method is a local function.</summary>
        /// <param name="methodBase">The method to determine if it is a local function.</param>
        /// <returns>True if the method is a local function. False if not.</returns>
        public static bool IsLocalFunction(this MethodBase methodBase) =>
            Regex.Match(methodBase.Name, @"g__.+\|\d+_\d+").Success;
    }
}