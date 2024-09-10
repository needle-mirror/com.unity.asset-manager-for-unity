using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    static class PaginationHelpers
    {
        internal delegate Task<int> GetTotalCount(CancellationToken cancellationToken);

        internal static async Task<(int Offset, int Length)> GetOffsetAndLengthAsync(this Range range, GetTotalCount getTotalCount, CancellationToken cancellationToken)
        {
            (int Offset, int Length) values = (0, int.MaxValue);

            if (range.Start.IsFromEnd || (range.End.IsFromEnd && range.End.Value != 0))
            {
                var count = await getTotalCount(cancellationToken);

                values.Offset = CheckIndex(range.Start, count);
                var endIndex =  CheckIndex(range.End, count);

                values.Length = Math.Max(0, endIndex - values.Offset);
            }
            else
            {
                values.Offset = range.Start.Value;
                if (!range.End.Equals(Index.End))
                {
                    values.Length = Math.Max(0, range.End.Value - range.Start.Value);
                }
            }

            return values;
        }

        static int CheckIndex(Index index, int totalLength)
        {
            return index.IsFromEnd
                ? Math.Max(0, totalLength - index.Value)
                : index.Value;
        }
    }
}
