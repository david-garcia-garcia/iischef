using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace iischef.utils
{
    public static class ExtensionHelpers
    {
        /// <summary>
        /// Json serializar used as default when using JsonClone
        /// </summary>
        public static JsonSerializer JsonCloneSerializer;

        /// <summary>
        /// Serializador que se usa para el JSON equals
        /// </summary>
        public static JsonSerializer JsonEqualSerializerSettings;

        /// <summary>
        ///
        /// </summary>
        static ExtensionHelpers()
        {
            JsonSerializerSettings settings;

            settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Include;
            settings.DefaultValueHandling = DefaultValueHandling.Include;
            settings.TypeNameHandling = TypeNameHandling.Auto;
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            settings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            JsonCloneSerializer = JsonSerializer.Create(settings);

            settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Include;
            settings.DefaultValueHandling = DefaultValueHandling.Include;
            settings.TypeNameHandling = TypeNameHandling.Auto;
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

            // Este NONE es importante a la hora de comprar objetos, ya que de los contrario NEWTONSOFT genera un campo $id aleatorio
            // en todos los objetos que rompe la comparación
            settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            JsonEqualSerializerSettings = JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Devuelve el nombre del método actual en el contexto de ejecución, saltándose implementaciones "in-line" si las hubiera
        /// http://stackoverflow.com/questions/2652460/c-sharp-how-to-get-the-name-of-the-current-method-from-code
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentMethod()
        {
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace();
            System.Diagnostics.StackFrame sf = st.GetFrame(1);
            return sf.GetMethod().Name;
        }

        public static List<TType> ToList<TType>(this TType[] source)
        {
            List<TType> lst = new List<TType>();
            foreach (TType t in source)
            {
                lst.Add(t);
            }

            return lst;
        }

        public static int ToInt32(this object source)
        {
            return System.Convert.ToInt32(source);
        }

        public static TSourceType ifNull<TSourceType>(this TSourceType source, TSourceType alternativa)
        {
            if (source == null)
            {
                return alternativa;
            }
            else
            {
                return source;
            }
        }

        public static bool ToBoolean(this object source)
        {
            return source.ToBoolean(false);
        }

        public static bool ToBoolean(this object source, bool default_value = false)
        {
            if (source.IsInt32() && source.ToInt32() == 0)
            {
                return false;
            }

            if (source.IsInt32() && source.ToInt32() == 1)
            {
                return true;
            }

            if (source is string && string.IsNullOrEmpty((string)source))
            {
                return default_value;
            }

            return System.Convert.ToBoolean(source);
        }

        public static bool IsInt32(this object source)
        {
            try
            {
                if (source == null)
                {
                    return false;
                }

                if (source.ToString() == string.Empty)
                {
                    return false;
                }

                int a = System.Convert.ToInt32(source);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static System.Dynamic.ExpandoObject ToExpando(this object source)
        {
            if (source == null)
            {
                return null;
            }

            dynamic exando = new System.Dynamic.ExpandoObject();
            var x = exando as IDictionary<string, object>;

            foreach (var prop in source.GetType().GetProperties())
            {
                x[prop.Name] = prop.GetValue(source, null);
            }

            return exando;
        }

        public static List<System.Dynamic.ExpandoObject> ToExpandoList(this IEnumerable source)
        {
            List<System.Dynamic.ExpandoObject> result = new List<System.Dynamic.ExpandoObject>();

            foreach (var p in source)
            {
                result.Add(p.ToExpando());
            }

            return result;
        }

        public static TValueType Get<TKeyType, TValueType>(this IDictionary<TKeyType, TValueType> source, TKeyType key, TValueType defaultValue)
        {
            if (source.ContainsKey(key))
            {
                return source[key];
            }

            return defaultValue;
        }

        public static void Set<TKeyType, TValueType>(this IDictionary<TKeyType, TValueType> source, TKeyType key, TValueType value)
        {
            if (!source.ContainsKey(key))
            {
                source.Add(key, value);
            }
            else
            {
                source[key] = value;
            }
        }

        public static bool IsDouble(this object source)
        {
            try
            {
                if (source == null)
                {
                    return false;
                }

                if (source is string && ((string)source) == string.Empty)
                {
                    return false;
                }

                double a = System.Convert.ToDouble(source);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsBoolean(this object source)
        {
            try
            {
                if (source == null)
                {
                    return false;
                }

                if (source is string && ((string)source) == string.Empty)
                {
                    return false;
                }

                if (source.IsInt32() && (source.ToInt32() == 0 || source.ToInt32() == 1))
                {
                    return true;
                }

                var a = System.Convert.ToBoolean(source);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public static string ToString(this object source)
        {
            return System.Convert.ToString(source);
        }

        public static bool IsNullableType(this Type source)
        {
            if (source.IsGenericType && source.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Chef if object is null or default value.
        ///
        /// Works with nullable types and JToken's
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        public static bool IsNullOrDefault<T>(this T argument)
        {
            // deal with normal scenarios
            if (argument == null)
            {
                return true;
            }

            if (object.Equals(argument, default(T)))
            {
                return true;
            }

            if (argument is Guid && object.Equals(argument, Guid.Empty))
            {
                return true;
            }

            if (argument is JToken && (argument as JToken).Type == JTokenType.Null)
            {
                return true;
            }

            // deal with non-null nullables
            Type methodType = typeof(T);
            Type underlyingType = Nullable.GetUnderlyingType(methodType);
            if (underlyingType != null)
            {
                return false;
            }

            // deal with boxed value types
            Type argumentType = argument.GetType();
            if (argumentType.IsValueType && argumentType != methodType)
            {
                object obj = Activator.CreateInstance(argument.GetType());
                return obj.Equals(argument);
            }

            return false;
        }

        public static List<TType> CastGeneric<TType>(this List<object> source, TType objOfType)
        {
            List<TType> res = new List<TType>();
            foreach (object obj in source)
            {
                res.Add((TType)obj);
            }

            return res;
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public static T JsonClone<T>(this T source, JsonSerializer serializer = null)
        {
            serializer = serializer ?? JsonCloneSerializer;
            var serialized = serializer.SerializeObject(source);
            return (T)serializer.DeserializeCustom(serialized, source.GetType());
        }

        /// <summary>
        /// Check if two serialized objects are equal
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="equalsTo"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public static bool JsonEqual<T>(this T source, T equalsTo, JsonSerializer serializer = null)
        {
            serializer = serializer ?? JsonEqualSerializerSettings;
            var serialized1 = serializer.SerializeObject(source);
            var serialized2 = serializer.SerializeObject(equalsTo);
            return serialized1 == serialized2;
        }

        /// <summary>
        /// Serialize the object and return it as string.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string SerializeObject(this JsonSerializer serializer, object source)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.SerializeToStream(source, stream);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Serialize the object into a stream.
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="source"></param>
        /// <param name="stream"></param>
        public static void SerializeToStream(this JsonSerializer serializer, object source, Stream stream)
        {
            var sw = new StreamWriter(stream);
            using (var jsonTextWriter = new JsonTextWriter(sw))
            {
                serializer.Serialize(jsonTextWriter, source);
            }
        }

        /// <summary>
        /// Deserialize to an object
        /// </summary>
        /// <param name="serializer"></param>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object DeserializeCustom(this JsonSerializer serializer, string source, Type type)
        {
            TextReader tx = new StringReader(source);
            using (var jsonTextWriter = new JsonTextReader(tx))
            {
                object result = serializer.Deserialize(jsonTextWriter, type);
                return result;
            }
        }
    }
}
