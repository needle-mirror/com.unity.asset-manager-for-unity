using System;
using System.Linq;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Lightweight argument-validation helpers.
    /// </summary>
    static class Guard
    {
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is null.
        /// Returns the value so it can be used in assignment expressions:
        /// <c>m_Service = Guard.AgainstNull(service, nameof(service));</c>
        /// </summary>
        public static T AgainstNull<T>(T value, string paramName) where T : class
        {
            if (value == null)
                throw new ArgumentNullException(paramName);
            return value;
        }

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when <b>all</b> of the supplied arguments
        /// are null. Use this when at least one of several optional dependencies must be provided.
        /// </summary>
        public static void RequireAtLeastOne(params (object value, string name)[] arguments)
        {
            if (arguments.Any(a => a.value != null))
                return;

            var names = string.Join(", ", arguments.Select(a => a.name));
            throw new ArgumentNullException(names,
                $"At least one of the following must be non-null: {names}");
        }
    }
}

