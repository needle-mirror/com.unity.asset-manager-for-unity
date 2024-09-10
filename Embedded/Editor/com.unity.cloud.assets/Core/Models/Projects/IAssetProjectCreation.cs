namespace Unity.Cloud.AssetsEmbedded
{
    interface IAssetProjectCreation
    {
        string Name { get; }
        IDeserializable Metadata { get; }
    }
}
