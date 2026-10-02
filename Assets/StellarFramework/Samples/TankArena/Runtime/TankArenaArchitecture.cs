using StellarFramework;
using StellarFramework.Bindable;
using StellarFramework.Event;
using StellarFramework.FSM;

namespace HotUpdate
{
    public enum TankArenaPhase
    {
        Active,
        Paused,
        GameOver
    }

    public enum TankArenaEventKind
    {
        Elimination,
        WaveAdvanced,
        HullRepaired,
        HullDamaged
    }

    public sealed class TankArenaSignal : ITypeEvent
    {
        public TankArenaEventKind Kind { get; }
        public int Value { get; }
        public int Wave { get; }

        public TankArenaSignal(TankArenaEventKind kind, int value, int wave)
        {
            Kind = kind;
            Value = value;
            Wave = wave;
        }
    }

    public interface IReadOnlyTankArenaModel : IReadOnlyModel
    {
        IReadOnlyBindableProperty<int> Score { get; }
        IReadOnlyBindableProperty<int> Hull { get; }
        IReadOnlyBindableProperty<int> Wave { get; }
        IReadOnlyBindableProperty<int> Eliminations { get; }
        IReadOnlyBindableProperty<TankArenaPhase> Phase { get; }
    }

    public sealed class TankArenaModel : AbstractModel, IReadOnlyTankArenaModel
    {
        public readonly BindableProperty<int> Score = new BindableProperty<int>(0);
        public readonly BindableProperty<int> Hull = new BindableProperty<int>(100);
        public readonly BindableProperty<int> Wave = new BindableProperty<int>(1);
        public readonly BindableProperty<int> Eliminations = new BindableProperty<int>(0);
        public readonly BindableProperty<TankArenaPhase> Phase =
            new BindableProperty<TankArenaPhase>(TankArenaPhase.Active);

        IReadOnlyBindableProperty<int> IReadOnlyTankArenaModel.Score => Score;
        IReadOnlyBindableProperty<int> IReadOnlyTankArenaModel.Hull => Hull;
        IReadOnlyBindableProperty<int> IReadOnlyTankArenaModel.Wave => Wave;
        IReadOnlyBindableProperty<int> IReadOnlyTankArenaModel.Eliminations => Eliminations;
        IReadOnlyBindableProperty<TankArenaPhase> IReadOnlyTankArenaModel.Phase => Phase;

        public override void Init()
        {
            base.Init();
            Reset();
        }

        public void Reset()
        {
            Score.Value = 0;
            Hull.Value = 100;
            Wave.Value = 1;
            Eliminations.Value = 0;
            Phase.Value = TankArenaPhase.Active;
        }
    }

    public sealed class TankArenaService : AbstractService
    {
        private FSM<TankArenaService> _phaseMachine;
        private int _scorePerElimination = 150;
        private int _eliminationsPerWave = 5;

        public string CurrentStateName => _phaseMachine?.CurrentStateName ?? "Uninitialized";

        public override void Init()
        {
            base.Init();
            _phaseMachine = new FSM<TankArenaService>(this);
            _phaseMachine.AddState<ActivePhaseState>();
            _phaseMachine.AddState<PausedPhaseState>();
            _phaseMachine.AddState<GameOverPhaseState>();
            _phaseMachine.ChangeState<ActivePhaseState>();
        }

        public void ConfigureScoring(int scorePerElimination, int eliminationsPerWave)
        {
            _scorePerElimination = UnityEngine.Mathf.Max(1, scorePerElimination);
            _eliminationsPerWave = UnityEngine.Mathf.Max(1, eliminationsPerWave);
        }

        public void ResetMatch()
        {
            GetModel<TankArenaModel>()?.Reset();
            SetPhase(TankArenaPhase.Active);
        }

        public void RegisterElimination()
        {
            TankArenaModel model = GetModel<TankArenaModel>();
            if (model == null) return;

            int eliminations = model.Eliminations.Value + 1;
            int previousWave = model.Wave.Value;
            model.Eliminations.Value = eliminations;
            model.Score.Value += _scorePerElimination;
            model.Wave.Value = 1 + eliminations / _eliminationsPerWave;
            GlobalTypeEvent.Broadcast(new TankArenaSignal(
                TankArenaEventKind.Elimination, eliminations, model.Wave.Value));
            if (model.Wave.Value > previousWave)
            {
                GlobalTypeEvent.Broadcast(new TankArenaSignal(
                    TankArenaEventKind.WaveAdvanced, model.Wave.Value, model.Wave.Value));
            }
        }

        public void ApplyDamage(int damage)
        {
            TankArenaModel model = GetModel<TankArenaModel>();
            if (model == null || model.Phase.Value != TankArenaPhase.Active) return;

            int hullBefore = model.Hull.Value;
            model.Hull.Value = UnityEngine.Mathf.Max(0, model.Hull.Value - UnityEngine.Mathf.Max(0, damage));
            GlobalTypeEvent.Broadcast(new TankArenaSignal(
                TankArenaEventKind.HullDamaged, hullBefore - model.Hull.Value, model.Wave.Value));
            if (model.Hull.Value == 0)
            {
                SetPhase(TankArenaPhase.GameOver);
            }
        }

        public void RepairHull(int amount)
        {
            TankArenaModel model = GetModel<TankArenaModel>();
            if (model == null || model.Phase.Value != TankArenaPhase.Active) return;
            int hullBefore = model.Hull.Value;
            model.Hull.Value = UnityEngine.Mathf.Min(100, model.Hull.Value + UnityEngine.Mathf.Max(0, amount));
            int restored = model.Hull.Value - hullBefore;
            if (restored > 0)
            {
                GlobalTypeEvent.Broadcast(new TankArenaSignal(
                    TankArenaEventKind.HullRepaired, restored, model.Wave.Value));
            }
        }

        public void SetPhase(TankArenaPhase phase)
        {
            if (_phaseMachine == null) return;
            switch (phase)
            {
                case TankArenaPhase.Active:
                    _phaseMachine.ChangeState<ActivePhaseState>();
                    break;
                case TankArenaPhase.Paused:
                    _phaseMachine.ChangeState<PausedPhaseState>();
                    break;
                case TankArenaPhase.GameOver:
                    _phaseMachine.ChangeState<GameOverPhaseState>();
                    break;
            }
        }

        private void ApplyPhase(TankArenaPhase phase)
        {
            TankArenaModel model = GetModel<TankArenaModel>();
            if (model != null) model.Phase.Value = phase;
        }

        private sealed class ActivePhaseState : FSMState<TankArenaService>
        {
            public override void OnEnter() => Owner.ApplyPhase(TankArenaPhase.Active);
        }

        private sealed class PausedPhaseState : FSMState<TankArenaService>
        {
            public override void OnEnter() => Owner.ApplyPhase(TankArenaPhase.Paused);
        }

        private sealed class GameOverPhaseState : FSMState<TankArenaService>
        {
            public override void OnEnter() => Owner.ApplyPhase(TankArenaPhase.GameOver);
        }
    }

    public sealed class TankArenaArchitecture : Architecture<TankArenaArchitecture>
    {
        protected override void InitModules()
        {
            RegisterModel(new TankArenaModel());
            RegisterService(new TankArenaService());
            RegisterService(new TankArenaLocalizationService());
        }
    }
}
