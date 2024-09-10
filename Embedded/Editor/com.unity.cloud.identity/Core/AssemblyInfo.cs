using System.Runtime.CompilerServices;
using Unity.Cloud.CommonEmbedded;

[assembly: ApiSourceVersion("com.unity.cloud.identity", "1.2.1")]
#if !(UC_NUGET)
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Tests")]
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Editor")]
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Tests.Editor")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // to allow moq implementations for internal interfaces
#endif
