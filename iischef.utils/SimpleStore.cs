using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace iischef.utils
{
    /// <summary>
    /// Simple disk store for key-value json storage (i.e. caching)
    /// </summary>
    public class SimpleStore
    {
        /// <summary>
        /// The storage directory
        /// </summary>
        protected string StorageDir { get; set; }

        /// <summary>
        /// Use the key item as the store file name
        /// </summary>
        protected bool UseKeyAsFileName { get; set; }

        /// <summary>
        /// Ge an instance of SimpleStore
        /// </summary>
        /// <param name="dir">Storage dir for kv items.</param>
        /// <param name="useKeyAsFileName"></param>
        public SimpleStore(string dir, bool useKeyAsFileName = false)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            this.UseKeyAsFileName = useKeyAsFileName;
            this.StorageDir = dir;
        }

        /// <summary>
        /// Clears all the simple store contents
        /// </summary>
        public void Clear()
        {
            foreach (var file in Directory.EnumerateFiles(this.StorageDir, "*.json", SearchOption.TopDirectoryOnly).ToList())
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Store an item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <param name="ttl">Time to live for the item in minutes. Default to 0 - no expiration.</param>
        public void Set<T>(string key, T data, int ttl = 0)
        {
            var cacheItem = new SimpleStoreItem<T>();
            cacheItem.Created = DateTime.UtcNow;
            cacheItem.Ttl = ttl;
            cacheItem.Data = data;
            cacheItem.Key = key;
            cacheItem.StorePath = this.GetDestinationPath(key);

            string serialized = JsonConvert.SerializeObject(cacheItem, Formatting.Indented);
            File.WriteAllText(cacheItem.StorePath, serialized, Encoding.UTF8);
        }

        /// <summary>
        /// Retrieve an item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="result"></param>
        public bool Get<T>(string key, out SimpleStoreItem<T> result)
        {
            result = null;

            var storeFilePath = new FileInfo(this.GetDestinationPath(key));

            if (!storeFilePath.Exists)
            {
                return false;
            }

            var contents = File.ReadAllText(storeFilePath.FullName, Encoding.UTF8);

            var restored = JsonConvert.DeserializeObject<SimpleStoreItem<T>>(contents);

            if (restored.Ttl > 0 && restored.Created.AddMinutes(restored.Ttl) < DateTime.UtcNow)
            {
                File.Delete(storeFilePath.FullName);
                return false;
            }

            restored.StorePath = storeFilePath.FullName;
            result = restored;
            return true;
        }

        /// <summary>
        /// Get the target destionation path
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected string GetDestinationPath(string key)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var sanitizedKey = string.Join("_", key.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');

            string fileName = this.UseKeyAsFileName ? sanitizedKey : UtilsEncryption.GetShortHash(key, 12);
            return Path.Combine(this.StorageDir, fileName + ".json");
        }
    }
}
