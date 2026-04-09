using System;
using UnityEngine;
using StellarFramework.FSM;

namespace StellarFramework.Examples
{
    #region 1. 动画定义与载荷 (0GC)

    /// <summary>
    /// 动画请求载荷
    /// </summary>
    public struct AnimRequestPayload
    {
        public readonly int StateHash;
        public readonly float TransitionDuration;

        public AnimRequestPayload(int stateHash, float transitionDuration = 0.2f)
        {
            StateHash = stateHash;
            TransitionDuration = transitionDuration;
        }
    }

    /// <summary>
    /// 动画哈希缓存表，拒绝运行时字符串分配
    /// </summary>
    public static class ExampleAnimHashes
    {
        public static readonly int Idle = Animator.StringToHash("Idle");
        public static readonly int Run = Animator.StringToHash("Run");
    }

    /// <summary>
    /// 状态切换参数载荷
    /// </summary>
    public struct ChasePayload
    {
        public Transform Target;
        public float Speed;
    }

    #endregion

    #region 2. Model 层 (数据与事件流转)

    /// <summary>
    /// 示例数据模型
    /// 我负责存储状态数据并分发表现层事件，绝对不持有 Unity 组件。
    /// </summary>
    public class ExampleModel
    {
        public event Action<AnimRequestPayload> OnAnimStateChanged;

        private AnimRequestPayload _currentAnimPayload;

        public void RequestAnimation(AnimRequestPayload payload)
        {
            if (_currentAnimPayload.StateHash == payload.StateHash)
            {
                return;
            }

            _currentAnimPayload = payload;
            OnAnimStateChanged?.Invoke(_currentAnimPayload);
        }

        public void Clear()
        {
            OnAnimStateChanged = null;
            _currentAnimPayload = default;
        }
    }

    #endregion

    #region 3. View 层 (纯粹的表现驱动)

    /// <summary>
    /// 示例表现层
    /// 我仅负责监听 Model 事件并驱动 Animator，严禁包含任何业务逻辑。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ExampleView : MonoBehaviour
    {
        private Animator _animator;
        private ExampleModel _model;

        public void BindModel(ExampleModel model)
        {
            if (model == null)
            {
                Debug.LogError($"[ExampleView] BindModel 失败: 传入的 model 为空, GameObject={gameObject.name}");
                return;
            }

            _animator = GetComponent<Animator>();
            _model = model;
            _model.OnAnimStateChanged += HandleAnimStateChanged;
        }

        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.OnAnimStateChanged -= HandleAnimStateChanged;
                _model = null;
            }
        }

        private void HandleAnimStateChanged(AnimRequestPayload payload)
        {
            if (_animator == null)
            {
                return;
            }

            // 核心：使用 CrossFade 替代连线，实现孤岛动画的丝滑过渡
            _animator.CrossFade(payload.StateHash, payload.TransitionDuration, 0);
        }
    }

    #endregion

    #region 4. Service 与 States 层 (纯粹的逻辑驱动)

    /// <summary>
    /// 待机状态
    /// </summary>
    public class ExampleIdleState : FSMState<ExampleService>
    {
        public override void OnEnter()
        {
            // 逻辑层进入待机，向 Model 下发 Idle 动画表现指令
            Owner.Model.RequestAnimation(new AnimRequestPayload(ExampleAnimHashes.Idle, 0.2f));
            Debug.Log("[ExampleIdleState] 进入待机状态，开始警戒...");
        }

        public override void OnUpdate()
        {
            if (Owner.EnemyTarget != null && Vector3.Distance(Owner.transform.position, Owner.EnemyTarget.position) < 5f)
            {
                ChasePayload payload = new ChasePayload
                {
                    Target = Owner.EnemyTarget,
                    Speed = 5f
                };
                FSM.ChangeState<ExampleChaseState, ChasePayload>(payload);
            }
        }
    }

    /// <summary>
    /// 追逐状态
    /// </summary>
    public class ExampleChaseState : FSMState<ExampleService>, IPayloadState<ChasePayload>
    {
        private Transform _target;
        private float _speed;

        public void OnEnter(ChasePayload payload)
        {
            _target = payload.Target;
            _speed = payload.Speed;

            // 逻辑层进入追逐，向 Model 下发 Run 动画表现指令
            Owner.Model.RequestAnimation(new AnimRequestPayload(ExampleAnimHashes.Run, 0.15f));
            Debug.Log($"[ExampleChaseState] 开始追逐目标: {_target.name}");
        }

        public override void OnUpdate()
        {
            if (_target == null)
            {
                Debug.LogWarning("[ExampleChaseState] 目标丢失，返回上一个状态");
                FSM.RevertToPreviousState();
                return;
            }

            Vector3 dir = (_target.position - Owner.transform.position).normalized;
            Owner.transform.position += dir * _speed * Time.deltaTime;

            if (Vector3.Distance(Owner.transform.position, _target.position) > 8f)
            {
                FSM.ChangeState<ExampleIdleState>();
            }
        }
    }

    /// <summary>
    /// FSMKit 宿主服务类
    /// 我负责组装 MSV 架构，并驱动状态机运转。
    /// </summary>
    public class ExampleService : MonoBehaviour
    {
        public Transform EnemyTarget;
        public ExampleModel Model { get; private set; }

        private FSM<ExampleService> _fsm;
        private ExampleView _view;

        private void Start()
        {
            // 1. 初始化 Model
            Model = new ExampleModel();

            // 2. 初始化 View 并绑定 Model
            _view = GetComponent<ExampleView>();
            if (_view == null)
            {
                _view = gameObject.AddComponent<ExampleView>();
            }

            _view.BindModel(Model);

            // 3. 初始化 FSM
            _fsm = new FSM<ExampleService>(this);
            _fsm.AddState<ExampleIdleState>();
            _fsm.AddState<ExampleChaseState>();

            // 4. 启动状态机
            _fsm.ChangeState<ExampleIdleState>();
        }

        private void Update()
        {
            _fsm?.OnUpdate();
        }

        private void OnDestroy()
        {
            _fsm?.Clear();
            _fsm = null;
            Model?.Clear();
            Model = null;
        }
    }

    #endregion
}