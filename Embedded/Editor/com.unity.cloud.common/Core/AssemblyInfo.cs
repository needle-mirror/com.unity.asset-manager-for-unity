using System.Runtime.CompilerServices;
using Unity.Cloud.CommonEmbedded;

[assembly: ApiSourceVersion("com.unity.cloud.common", "1.1.0")]
#if !(UC_NUGET)
[assembly: InternalsVisibleTo("Unity.Cloud.Common.Tests")]
[assembly: InternalsVisibleTo("Unity.Cloud.Common.Tests.Editor")]
[assembly: InternalsVisibleTo("Unity.Cloud.Common.Tests.ValidApiSource")]
[assembly: InternalsVisibleTo("Unity.Cloud.Common.Tests.InvalidApiSource")]
[assembly: InternalsVisibleTo("Unity.Cloud.Common.Tests.NoApiSource")]
[assembly: InternalsVisibleTo("Unity.Cloud.Common.Runtime")]
[assembly: InternalsVisibleTo("Unity.Cloud.DataStreaming.Tests.Runtime")]
[assembly: InternalsVisibleTo("Unity.Cloud.Debugging.Runtime")]
[assembly: InternalsVisibleTo("Unity.Cloud.Debugging.Editor")]
[assembly: InternalsVisibleTo("Unity.Cloud.Identity.Tests.Editor")]
[assembly: InternalsVisibleTo("Unity.Cloud.AppLinking.Tests.Editor")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

#endif
