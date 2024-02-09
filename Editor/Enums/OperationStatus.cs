namespace Unity.AssetManager.Editor
{
    internal enum OperationStatus
    {
        InProgress = 0,
        InInfiniteProgress,
        Success,
        Cancelled,
        Error
    }
}
