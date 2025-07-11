using System.Runtime.CompilerServices;
using Unity.Cloud.CommonEmbedded;

[assembly: ApiSourceVersion("com.unity.cloud.identity", "1.3.1")]
#if !(UC_NUGET)
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Tests")]
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Editor")]
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Tests.Editor")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // to allow moq implementations for internal interfaces
#endif

[assembly: InternalsVisibleTo("Unity.AssetManager.Core.Editor")]

[assembly: InternalsVisibleTo("Unity.AssetManager.Core.Editor.Tests")]

[assembly: InternalsVisibleTo("Unity.AssetManager.EndToEnd.Editor.Tests")]

[assembly: InternalsVisibleTo("Unity.AssetManager.UI.Editor.Tests")]

[assembly: InternalsVisibleTo("Unity.AssetManager.Upload.Editor.Tests")]

[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Editor.Embedded")]

[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Runtime.Embedded")]

[assembly: InternalsVisibleTo("Unity.Cloud.AppLinking.Editor.Embedded")]
