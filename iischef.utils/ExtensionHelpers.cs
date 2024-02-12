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
    }
}
