using UnityEngine;
using StellarFramework.FSM;

namespace StellarFramework.Examples
{
    // 1. 定义 0GC 参数载荷 (必须使用 struct)
    public struct ChasePayload
    {
        public Transform Target;
        public float Speed;
    }

    // 2. 定义状态：待机状态 (无参)
    public class IdleState : FSMState<Example_FSMKit>
    {
        public override void OnEnter()
        {
            LogKit.Log("[IdleState] 进入待机状态，开始警戒...");
        }

        public override void OnUpdate()
        {
            // 模拟视野检测
            if (Owner.EnemyTarget != null && Vector3.Distance(Owner.transform.position, Owner.EnemyTarget.position) < 5f)
            {
                // 发现敌人，构建载荷并切换到追逐状态
                ChasePayload payload = new ChasePayload
                {
                    Target = Owner.EnemyTarget,
                    Speed = 5f
                };
                FSM.ChangeState<ChaseState, ChasePayload>(payload);
            }
        }
    }

    // 3. 定义状态：追逐状态 (带参)
    public class ChaseState : FSMState<Example_FSMKit>, IPayloadState<ChasePayload>
    {
        private Transform _target;
        private float _speed;

        public void OnEnter(ChasePayload payload)
        {
            // 规范：在 OnEnter 中接收参数并重置内部状态，防止脏数据残留
            _target = payload.Target;
            _speed = payload.Speed;
            LogKit.Log($"[ChaseState] 开始追逐目标: {_target.name}, 速度: {_speed}");
        }

        public override void OnUpdate()
        {
            // 前置拦截：目标丢失时立即回退状态
            if (_target == null)
            {
                LogKit.LogWarning("[ChaseState] 目标丢失，返回上一个状态");
                FSM.RevertToPreviousState();
                return;
            }

            // 模拟追逐移动
            Vector3 dir = (_target.position - Owner.transform.position).normalized;
            Owner.transform.position += dir * _speed * Time.deltaTime;

            // 模拟丢失视野
            if (Vector3.Distance(Owner.transform.position, _target.position) > 8f)
            {
                FSM.ChangeState<IdleState>();
            }
        }
    }

    /// <summary>
    /// FSMKit 综合使用示例 (宿主类)
    /// </summary>
    public class Example_FSMKit : MonoBehaviour
    {
        public Transform EnemyTarget;
        
        private FSM<Example_FSMKit> _fsm;

        private void Start()
        {
            // 初始化状态机，将自身作为上下文 (Context) 传入
            _fsm = new FSM<Example_FSMKit>(this);

            // 注册所有可能用到的状态
            _fsm.AddState<IdleState>();
            _fsm.AddState<ChaseState>();

            // 启动初始状态
            _fsm.ChangeState<IdleState>();
        }

        private void Update()
        {
            // 驱动状态机轮询
            _fsm?.OnUpdate();
        }

        private void OnDestroy()
        {
            // 规范：宿主销毁时必须清理状态机，断开引用防止内存泄漏
            _fsm?.Clear();
            _fsm = null;
        }
    }
}
