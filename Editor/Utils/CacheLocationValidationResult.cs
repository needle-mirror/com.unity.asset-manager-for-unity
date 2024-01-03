namespace Unity.AssetManager.Editor
{
    internal enum CacheValidationResultError
    {
        None,
        PathTooLong,
        InvalidPath,
        CannotWriteToDirectory,
        DirectoryNotFound
    }
    
    internal class CacheLocationValidationResult
    {
        public bool success { get; set; }
        public CacheValidationResultError errorType { get; set; }
    }
}
