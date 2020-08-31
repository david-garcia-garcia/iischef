namespace iischef.core.Storage
{
    public enum AppBaseStorageType
    {
        /// <summary>
        /// Points to original code
        /// </summary>
        Original = 0,

        /// <summary>
        /// Is a copy
        /// </summary>
        Transient = 1,

        /// <summary>
        /// Is a symlink
        /// </summary>
        Symlink = 2
    }
}
