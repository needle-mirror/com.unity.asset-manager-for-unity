namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Central constants and helpers for package documentation URLs.
    /// When upgrading the package, update <see cref="PackageDocsVersion"/> to match the major.minor in package.json
    /// so in-editor and README documentation links point to the correct manual.
    /// </summary>
    static class PackageDocumentation
    {
        /// <summary>
        /// Version segment used in docs.unity3d.com package manual URLs (major.minor only, e.g. "1.10").
        /// Must be kept in sync with package.json version when releasing a new package version.
        /// </summary>
        public const string PackageDocsVersion = "1.10";

        const string k_PackageName = "com.unity.asset-manager-for-unity";
        const string k_BaseUrl = "https://docs.unity3d.com/Packages";

        /// <summary>
        /// Base URL for this package's manual (e.g. https://docs.unity3d.com/Packages/com.unity.asset-manager-for-unity@1.10/manual).
        /// </summary>
        public static string GetPackageManualBaseUrl() =>
            $"{k_BaseUrl}/{k_PackageName}@{PackageDocsVersion}/manual";

        /// <summary>
        /// Full URL for a manual page (e.g. tracking-files -> .../manual/tracking-files.html).
        /// </summary>
        /// <param name="page">Page name without extension (e.g. "tracking-files", "upload-assets").</param>
        public static string GetPackageManualPageUrl(string page) =>
            $"{GetPackageManualBaseUrl()}/{page}.html";
    }
}
