using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class UserChip : Chip
    {
        public UserChip(UserInfo userInfo) : base(userInfo.Name)
        {
            var initialsCircle = InitialsIconHelper.CreateInitialsIcon(userInfo.Name, userInfo.UserId);
            Add(initialsCircle);
            m_Label.PlaceInFront(initialsCircle);

            tooltip = userInfo.Name;
        }
    }
}
