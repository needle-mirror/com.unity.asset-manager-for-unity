namespace Unity.AssetManager.Editor
{
    internal enum ImportEndStatus
    {
        Ok = 0,
        GenericError = 1,
        HttpError = 2,
        GenerationError = 3,
        DownloadError = 4,
        Cancelled = 5
    }
}