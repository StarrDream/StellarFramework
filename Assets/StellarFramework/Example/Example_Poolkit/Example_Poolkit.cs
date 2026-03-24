using System.Collections.Generic;
using UnityEngine;
using StellarFramework.Pool;

namespace StellarFramework.Examples
{
    #region 纯 C# 对象定义

    /// <summary>
    /// 模拟高频产生的网络同步消息
    /// 实现 IPoolable 确保状态在复用时绝对干净
    /// </summary>
    public class PlayerMoveMsg : IPoolable
    {
        public int PlayerId;
        public Vector3 TargetPosition;

        public void OnAllocated()
        {
            PlayerId = -1;
            TargetPosition = Vector3.zero;
        }

        public void OnRecycled()
        {
            PlayerId = -1;
        }
    }

    #endregion

    #region Unity 组件定义

    /// <summary>
    /// 模拟游戏内的子弹表现实体
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        public float Speed = 20f;
    }

    #endregion

    /// <summary>
    /// PoolKit 综合使用场景演示
    /// 包含纯 C# 对象的全局门面调度，以及 GameObject 的局部工厂池调度。
    /// </summary>
    public class Example_Poolkit : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private Bullet _bulletPrefab;

        // 局部工厂池：专门负责管理当前 Manager 辖下的子弹实体
        private FactoryObjectPool<Bullet> _bulletPool;
        
        // 记录当前活跃的子弹，用于演示批量回收
        private readonly List<Bullet> _activeBullets = new List<Bullet>();

        private void Start()
        {
            if (_bulletPrefab == null)
            {
                Debug.LogError("[Example_Poolkit] 初始化失败: _bulletPrefab 为空，无法构建子弹对象池");
                return;
            }

            // 初始化 GameObject 专用池，将 Unity API 缝合进池的生命周期委托中
            _bulletPool = new FactoryObjectPool<Bullet>(
                factoryMethod: () => Instantiate(_bulletPrefab),
                allocateMethod: bullet => 
                {
                    bullet.gameObject.SetActive(true);
                },
                recycleMethod: bullet => 
                {
                    bullet.gameObject.SetActive(false);
                    // 必须重置物理状态或 Transform，防止下次出池时位置闪烁
                    bullet.transform.position = Vector3.zero; 
                },
                destroyMethod: bullet => 
                {
                    if (bullet != null)
                    {
                        Destroy(bullet.gameObject);
                    }
                },
                maxCount: 200 // 限制最大常驻内存量，超出部分在回收时直接 Destroy
            );
        }

        private void Update()
        {
            // 场景 1：模拟高频网络发包 (纯 C# 对象)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SimulateNetworkMessage(1001, transform.position);
            }

            // 场景 2：模拟开火生成实体 (GameObject 对象)
            if (Input.GetKeyDown(KeyCode.F))
            {
                FireBullet();
            }

            // 场景 3：模拟切场景或清场时的批量回收
            if (Input.GetKeyDown(KeyCode.R))
            {
                RecycleAllBullets();
            }
        }

        /// <summary>
        /// 演示：通过全局门面 PoolKit 极速存取纯 C# 对象
        /// </summary>
        private void SimulateNetworkMessage(int playerId, Vector3 pos)
        {
            // O(1) 获取对象，内部自动调用 OnAllocated
            PlayerMoveMsg msg = PoolKit.Allocate<PlayerMoveMsg>();
            msg.PlayerId = playerId;
            msg.TargetPosition = pos;

            Debug.Log($"[Example_Poolkit] 模拟发送移动消息: PlayerId={msg.PlayerId}, Pos={msg.TargetPosition}");

            // 模拟发送完毕后，立即回收并阻断后续修改可能
            PoolKit.Recycle(msg);
            msg = null; 
        }

        /// <summary>
        /// 演示：通过局部 FactoryObjectPool 存取 GameObject
        /// </summary>
        private void FireBullet()
        {
            if (_bulletPool == null)
            {
                Debug.LogError("[Example_Poolkit] FireBullet 失败: 子弹池未初始化");
                return;
            }

            // 获取实体，内部自动调用 allocateMethod (SetActive(true))
            Bullet bullet = _bulletPool.Allocate();
            bullet.transform.position = transform.position;
            
            _activeBullets.Add(bullet);
        }

        /// <summary>
        /// 演示：精确回收指定的 GameObject
        /// </summary>
        private void RecycleAllBullets()
        {
            if (_bulletPool == null) return;

            for (int i = 0; i < _activeBullets.Count; i++)
            {
                // 回收实体，内部自动调用 recycleMethod (SetActive(false) 并归零位置)
                _bulletPool.Recycle(_activeBullets[i]);
            }
            
            _activeBullets.Clear();
            Debug.Log("[Example_Poolkit] 场上所有活跃子弹已回收完毕");
        }
    }
}