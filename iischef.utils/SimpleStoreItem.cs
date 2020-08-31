using System;

namespace iischef.utils
{
    public class SimpleStoreItem<T>
    {
        /// <summary>
        /// The key
        /// </summary
        public string Key { get; set; }

        /// <summary>
        /// The item was created at...
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// The data
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Time to live in minutes
        /// </summary>
        public int Ttl { get; set; }

        /// <summary>
        /// Where is this item stored
        /// </summary>
        public string StorePath { get; set; }
    }
}
