using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fasterflect;

namespace iischef.utils
{
    /// <summary>
    /// Utilities for error handling
    /// </summary>
    public static class ErrorUtils
    {
        /// <summary>
        /// 
        /// </summary>
        static ErrorUtils()
        {
            List<string> forbiddenNamespaces = new List<string>();

            forbiddenNamespaces.Add("System");
            forbiddenNamespaces.Add("Xunit");
            forbiddenNamespaces.Add("NLog");
            forbiddenNamespaces.Add("Ninject");
            forbiddenNamespaces.Add("ReflectionAbstractionExtensions");

            List<string> expressions = new List<string>();

            foreach (string ns in forbiddenNamespaces)
            {
                expressions.Add($"^\\s*at {ns}\\.");
                expressions.Add($"^\\s*en {ns}\\.");
            }

            CleanExpression = new Regex(string.Join("|", expressions));
        }

        /// <summary>
        /// 
        /// </summary>
        public static Regex CleanExpression { get; set; }

        /// <summary>
        /// Gets a clean version of the stack trace, with summay information for the current application
        /// </summary>
        /// <returns></returns>
        public static string CleanStackTrace(string stackTrace, int skip = 0)
        {
            if (string.IsNullOrWhiteSpace(stackTrace))
            {
                return stackTrace;
            }

            var lines = Regex.Split(stackTrace, Environment.NewLine).AsQueryable();

            lines = lines.Where((i) => !CleanExpression.IsMatch(i));
            lines = lines.Skip(skip);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static object GetExceptionErrorCode(Exception ex)
        {
            try
            {
                // Error code is a very common property name
                return ex.GetPropertyValue("ErrorCode");
            }
            catch
            {
            }

            try
            {
                // Error code is a very common property name
                return ex.GetFieldValue("ErrorCode");
            }
            catch
            {
            }

            return string.Empty;
        }
    }
}
