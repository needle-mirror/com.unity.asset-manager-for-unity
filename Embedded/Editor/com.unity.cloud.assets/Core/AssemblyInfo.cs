using System.Runtime.CompilerServices;
using Unity.Cloud.CommonEmbedded;

[assembly: ApiSourceVersion("com.unity.cloud.assets", "1.4.0-exp.3")]

#if !(UC_NUGET)
[assembly: InternalsVisibleTo("Unity.Cloud.Assets.Tests.Editor")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif
