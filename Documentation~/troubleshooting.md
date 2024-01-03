# Troubleshooting

This section describes issues you might have while installing or using the package.

## Installation issues

**The Unity Editor can't resolve the package**  
The Asset Manager package is compatible with Unity 2022.3 or later.

## Application issues

**I can't see any projects**
- Ensure you have access to the Asset Manager service.
- Ensure you have linked the project to an organization by going to the Project Settings > Services in the Editor.
- Ensure your organization has enabled the Asset Manager service and that you have created at least one cloud project with the Asset Manager service.

**My project shows no assets**
- Add some assets on the Asset Manager service by going to the (https://cloud.unity3d.com/asset-manager).  
- Note that only users with sufficient permission level will be able to publish assets.
- The Asset Manager Editor window will only display published assets. Draft assets are only visible on the website.

**I get an error when I try to load content from my project**  
- The window will try to communicate with the Asset Manager service. 
Make sure that you have access to the service by going to the [Asset Manager section on the dashboard](https://cloud.unity3d.com/asset-manager).  
- You can also verify the status of Unity services by going to the [Unity status page](https://status.unity.com/).

**I get an error when I try to load content from the In Project section**
- Ensure you have access to the Asset Manager service.
- Ensure that you have access to the projects that the imported assets come from.
