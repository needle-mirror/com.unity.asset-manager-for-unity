namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// This struct contains the identifiers for an status transition.
    /// </summary>
    readonly struct StatusTransitionDescriptor
    {
        /// <summary>
        /// The flow the transition belongs to.
        /// </summary>
        public readonly StatusFlowDescriptor StatusFlowDescriptor;

        /// <summary>
        /// A unique id for the transition. Uniqueness is scoped to the flow.
        /// </summary>
        public readonly string StatusTransitionId;

        /// <summary>
        /// Creates an instance of the <see cref="StatusTransitionDescriptor"/> struct.
        /// </summary>
        /// <param name="statusFlowDescriptor">The flow of the transition belongs to.</param>
        /// <param name="statusTransitionId">The unique id of the transition.</param>
        public StatusTransitionDescriptor(StatusFlowDescriptor statusFlowDescriptor, string statusTransitionId)
        {
            StatusFlowDescriptor = statusFlowDescriptor;
            StatusTransitionId = statusTransitionId;
        }

        /// <summary>
        /// Returns whether two <see cref="StatusTransitionDescriptor"/> objects are equals.
        /// </summary>
        /// <param name="other">Compare the values with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instance have the same values;
        /// <see langword="false"/> otherwise.
        /// </returns>
        public bool Equals(StatusTransitionDescriptor other)
        {
            return StatusFlowDescriptor.Equals(other.StatusFlowDescriptor) &&
                StatusTransitionId.Equals(other.StatusTransitionId);
        }

        /// <summary>
        /// Validate <paramref name="obj"/> is a <see cref="StatusTransitionDescriptor"/> instance and have the same values as this instance.
        /// </summary>
        /// <param name="obj">Compare the values with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instance have the same values;
        /// <see langword="false"/> otherwise.
        /// </returns>
        public override bool Equals(object obj) => obj is StatusTransitionDescriptor other && Equals(other);

        /// <summary>
        /// Compute a hash code for the object.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        /// <remarks>
        /// * You should not assume that equal hash codes imply object equality.
        /// * You should never persist or use a hash code outside the application domain in which it was created,
        ///   because the same object may hash differently across application domains, processes, and platforms.
        /// </remarks>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StatusFlowDescriptor.GetHashCode();
                hashCode = (hashCode * 397) ^ StatusTransitionId.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Get if two <see cref="StatusTransitionDescriptor"/> represent the same.
        /// </summary>
        /// <param name="left">Compare with this first instance.</param>
        /// <param name="right">Compare with this other instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instances represent the same;
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool operator ==(StatusTransitionDescriptor left, StatusTransitionDescriptor right) => left.Equals(right);

        /// <summary>
        /// Get if two <see cref="StatusTransitionDescriptor"/> does not represent the same.
        /// </summary>
        /// <param name="left">Compare with this first instance.</param>
        /// <param name="right">Compare with this other instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instances are not the same;
        /// <see langword="false"/> if both instances are the same.
        /// </returns>
        public static bool operator !=(StatusTransitionDescriptor left, StatusTransitionDescriptor right) => !left.Equals(right);
    }
}
