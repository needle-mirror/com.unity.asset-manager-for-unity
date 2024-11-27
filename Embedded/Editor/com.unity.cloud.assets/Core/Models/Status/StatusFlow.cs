using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    class StatusFlow : IStatusFlow
    {
        /// <inheritdoc />
        public StatusFlowDescriptor Descriptor { get; }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public bool IsDefault { get; set; }

        /// <inheritdoc />
        public string StartStatusId { get; set; }

        internal IStatus[] Statuses { get; set; } = Array.Empty<IStatus>();
        internal IStatusTransition[] Transitions { get; set; } = Array.Empty<IStatusTransition>();

        internal StatusFlow(StatusFlowDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IStatus> ListStatusesAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var (offset, length) = range.GetOffsetAndLength(Statuses.Length);

            for (var i = offset; i < offset + length && i < Statuses.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return Statuses[i];
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IStatusTransition> ListTransitionsAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var (offset, length) = range.GetOffsetAndLength(Transitions.Length);

            for (var i = offset; i < offset + length && i < Transitions.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return Transitions[i];
            }
        }
    }
}
