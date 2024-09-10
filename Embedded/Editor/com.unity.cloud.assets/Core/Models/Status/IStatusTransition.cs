namespace Unity.Cloud.AssetsEmbedded
{
    interface IStatusTransition
    {
        /// <summary>
        /// The descriptor of the status transition.
        /// </summary>
        StatusTransitionDescriptor Descriptor { get; }

        /// <summary>
        /// The status from which the transition originates.
        /// </summary>
        StatusDescriptor FromStatus { get; }

        /// <summary>
        /// The status to which the transition leads.
        /// </summary>
        StatusDescriptor ToStatus { get; }

        /// <summary>
        /// ???
        /// </summary>
        // StatusPredicate ThroughPredicate { get; }
    }
}
