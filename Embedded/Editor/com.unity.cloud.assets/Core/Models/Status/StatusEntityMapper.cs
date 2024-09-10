using System;
using System.Linq;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        static void MapFrom(this StatusFlow statusFlow, IStatusFlowData data)
        {
            statusFlow.Name = data.Name;
            statusFlow.IsDefault = data.IsDefault;
            statusFlow.StartStatusDescriptor = new StatusDescriptor(statusFlow.Descriptor, data.StartStatusId);
            statusFlow.Statuses = data.Statuses?.Select(s => s.From(statusFlow.Descriptor)).ToArray() ?? Array.Empty<IStatus>();
            statusFlow.Transitions = data.Transitions?.Select(t => t.From(statusFlow.Descriptor)).ToArray() ?? Array.Empty<IStatusTransition>();
        }

        static void MapFrom(this Status status, IStatusData data)
        {
            status.Name = data.Name;
            status.Description = data.Description;
            status.CanBeSkipped = data.CanBeSkipped;
            status.SortingOrder = data.SortingOrder;
            status.InPredicate = new StatusPredicate(data.InPredicate.Id, data.InPredicate.Name);
            status.OutPredicate = new StatusPredicate(data.OutPredicate.Id, data.OutPredicate.Name);
        }

        static void MapFrom(this StatusTransition transition, IStatusTransitionData data)
        {
            transition.FromStatus = new StatusDescriptor(transition.Descriptor.StatusFlowDescriptor, data.FromStatusId);
            transition.ToStatus = new StatusDescriptor(transition.Descriptor.StatusFlowDescriptor, data.ToStatusId);
            transition.ThroughPredicate = new StatusPredicate(data.ThroughPredicate.Id, data.ThroughPredicate.Name);
        }

        internal static IStatusFlow From(this IStatusFlowData data, OrganizationId organizationId)
        {
            var descriptor = new StatusFlowDescriptor(organizationId, data.Id);
            var statusFlow = new StatusFlow(descriptor);
            statusFlow.MapFrom(data);
            return statusFlow;
        }

        internal static IStatus From(this IStatusData data, StatusFlowDescriptor statusFlowDescriptor)
        {
            var descriptor = new StatusDescriptor(statusFlowDescriptor, data.Id);
            var status = new Status(descriptor);
            status.MapFrom(data);
            return status;
        }

        static IStatusTransition From(this IStatusTransitionData data, StatusFlowDescriptor statusFlowDescriptor)
        {
            var descriptor = new StatusTransitionDescriptor(statusFlowDescriptor, data.Id);
            var transition = new StatusTransition(descriptor);
            transition.MapFrom(data);
            return transition;
        }
    }
}
