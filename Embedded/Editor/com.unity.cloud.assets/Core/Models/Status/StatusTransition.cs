using System;

namespace Unity.Cloud.AssetsEmbedded
{
    class StatusTransition : IStatusTransition
    {
        /// <inheritdoc />
        public StatusTransitionDescriptor Descriptor { get; }

        /// <inheritdoc />
        public StatusDescriptor FromStatus { get; set; }

        /// <inheritdoc />
        public StatusDescriptor ToStatus { get; set; }

        /// <inheritdoc />
        public StatusPredicate ThroughPredicate { get; set; }

        internal StatusTransition(StatusTransitionDescriptor descriptor)
        {
            Descriptor = descriptor;
        }
    }
}
