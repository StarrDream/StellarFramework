namespace StellarFramework.Res
{
    /// <summary>
    /// 资源加载方式枚举
    /// </summary>
    public enum ResLoaderType
    {
        Resources = 0, // 使用 UnityEngine.Resources 加载
        Addressable = 1, // 使用 Addressables 加载
        AssetBundle = 2 // 使用Ab加载
    }
}