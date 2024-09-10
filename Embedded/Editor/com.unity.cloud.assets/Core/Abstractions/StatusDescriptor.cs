namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// This struct contains the identifiers for an asset's status.
    /// </summary>
    readonly struct StatusDescriptor
    {
        /// <summary>
        /// The flow the status belongs to.
        /// </summary>
        public readonly StatusFlowDescriptor StatusFlowDescriptor;

        /// <summary>
        /// A unique id for the status. Uniqueness is scoped to the parent status flow.
        /// </summary>
        public readonly string StatusId;

        /// <summary>
        /// Creates an instance of the <see cref="StatusDescriptor"/> struct.
        /// </summary>
        /// <param name="statusFlowDescriptor">The flow the status belongs to.</param>
        /// <param name="statusId">The unique id of the status.</param>
        public StatusDescriptor(StatusFlowDescriptor statusFlowDescriptor, string statusId)
        {
            StatusFlowDescriptor = statusFlowDescriptor;
            StatusId = statusId;
        }

        /// <summary>
        /// Returns whether two <see cref="StatusDescriptor"/> objects are equals.
        /// </summary>
        /// <param name="other">Compare the values with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instance have the same values;
        /// <see langword="false"/> otherwise.
        /// </returns>
        public bool Equals(StatusDescriptor other)
        {
            return StatusFlowDescriptor.Equals(other.StatusFlowDescriptor) &&
                StatusId.Equals(other.StatusId);
        }

        /// <summary>
        /// Validate <paramref name="obj"/> is a <see cref="StatusDescriptor"/> instance and have the same values as this instance.
        /// </summary>
        /// <param name="obj">Compare the values with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instance have the same values;
        /// <see langword="false"/> otherwise.
        /// </returns>
        public override bool Equals(object obj) => obj is StatusDescriptor other && Equals(other);

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
                hashCode = (hashCode * 397) ^ StatusId.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Get if two <see cref="StatusDescriptor"/> represent the same.
        /// </summary>
        /// <param name="left">Compare with this first instance.</param>
        /// <param name="right">Compare with this other instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instances represent the same;
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool operator ==(StatusDescriptor left, StatusDescriptor right) => left.Equals(right);

        /// <summary>
        /// Get if two <see cref="StatusDescriptor"/> does not represent the same.
        /// </summary>
        /// <param name="left">Compare with this first instance.</param>
        /// <param name="right">Compare with this other instance.</param>
        /// <returns>
        /// <see langword="true"/> if both instances are not the same;
        /// <see langword="false"/> if both instances are the same.
        /// </returns>
        public static bool operator !=(StatusDescriptor left, StatusDescriptor right) => !left.Equals(right);
    }
}
