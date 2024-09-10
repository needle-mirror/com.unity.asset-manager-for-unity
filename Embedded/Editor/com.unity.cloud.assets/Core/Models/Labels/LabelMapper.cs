using System.Drawing;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static void MapFrom(this LabelEntity label, ILabelData labelData)
        {
            label.Description = labelData.Description;
            label.DisplayColor = labelData.DisplayColor ?? Color.White;
            label.IsSystemLabel = labelData.IsSystemLabel;
            label.IsAssignable = labelData.IsUserAssignable;
            label.AuthoringInfo = new AuthoringInfo(labelData.CreatedBy, labelData.Created, labelData.UpdatedBy, labelData.Updated);
        }

        internal static ILabelBaseData From(this ILabelCreation labelCreation)
        {
            return new LabelBaseData
            {
                Name = labelCreation.Name,
                Description = labelCreation.Description,
                DisplayColor = labelCreation.DisplayColor
            };
        }

        internal static ILabelBaseData From(this ILabelUpdate labelUpdate)
        {
            return new LabelBaseData
            {
                Description = labelUpdate.Description,
                DisplayColor = labelUpdate.DisplayColor
            };
        }

        internal static LabelEntity From(this ILabelData data, IAssetDataSource assetDataSource, OrganizationId organizationId)
        {
            return data.From(assetDataSource, new LabelDescriptor(organizationId, data.Name));
        }

        internal static LabelEntity From(this ILabelData data, IAssetDataSource assetDataSource, LabelDescriptor labelDescriptor)
        {
            var label = new LabelEntity(assetDataSource, labelDescriptor);
            label.MapFrom(data);
            return label;
        }
    }
}
