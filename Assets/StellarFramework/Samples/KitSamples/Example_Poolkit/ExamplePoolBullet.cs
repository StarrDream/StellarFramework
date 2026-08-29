using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 提供独立脚本文件，避免 Bullet 直接序列化到 Prefab 时出现 Missing Script。
    /// </summary>
    public class ExamplePoolBullet : Bullet
    {
    }
}
