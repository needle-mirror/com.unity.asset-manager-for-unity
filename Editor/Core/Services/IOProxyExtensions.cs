namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Extension methods for IIOProxy to provide additional file I/O operations.
    /// </summary>
    static class IOProxyExtensions
    {
        /// <summary>
        /// Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="ioProxy">The IO proxy instance.</param>
        /// <param name="path">The directory path to ensure exists.</param>
        public static void EnsureDirectoryExists(this IIOProxy ioProxy, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            if (!ioProxy.DirectoryExists(path))
            {
                ioProxy.CreateDirectory(path);
            }
        }
    }
}
