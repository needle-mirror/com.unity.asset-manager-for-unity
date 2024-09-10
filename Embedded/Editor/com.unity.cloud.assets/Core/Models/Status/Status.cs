namespace Unity.Cloud.AssetsEmbedded
{
    class Status : IStatus
    {
        /// <inheritdoc />
        public StatusDescriptor Descriptor { get; }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public bool CanBeSkipped { get; set; }

        /// <inheritdoc />
        public int SortingOrder { get; set; }

        /// <inheritdoc />
        public StatusPredicate InPredicate { get; set; }

        /// <inheritdoc />
        public StatusPredicate OutPredicate { get; set; }

        internal Status(StatusDescriptor descriptor)
        {
            Descriptor = descriptor;
        }
    }
}
