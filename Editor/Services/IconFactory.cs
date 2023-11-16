using UnityEditorInternal;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IIconFactory : IService
    {
        Texture2D GetIconByType(AssetType type);
    }

    internal class IconFactory : BaseService<IIconFactory>, IIconFactory
    {
        public Texture2D GetIconByType(AssetType assetType)
        {
            switch (assetType)
            {
                case AssetType.Material:
                    return InternalEditorUtility.GetIconForFile(".mat");
                case AssetType.Model3D:
                    return InternalEditorUtility.GetIconForFile(".fbx");
                case AssetType.Asset2D:
                    return InternalEditorUtility.GetIconForFile(".png");
                case AssetType.Audio:
                    return InternalEditorUtility.GetIconForFile(".mp3");
                case AssetType.Script:
                    return InternalEditorUtility.GetIconForFile(".cs");
                case AssetType.Video:
                    return InternalEditorUtility.GetIconForFile(".mp4");
                default:
                    return null;
            }
        }
    }
}
