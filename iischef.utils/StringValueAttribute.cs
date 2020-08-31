using System;
using System.Collections;
using System.Reflection;

namespace iischef.utils
{
    /// <summary>
    /// Simple attribute class for storing string Values
    /// </summary>
    public class StringValueAttribute : Attribute
    {
        private string value;

        /// <summary>
        /// Creates a new <see cref="StringValueAttribute"/> instance.
        /// </summary>
        /// <param name="value">Value.</param>
        public StringValueAttribute(string value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value></value>
        public string Value
        {
            get { return this.value; }
        }
    }
}
