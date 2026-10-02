using System;
using System.Collections.Generic;
using System.Globalization;
using StellarFramework;
using StellarFramework.Bindable;
using StellarFramework.Event;
using StellarFramework.Localization;
using StellarFramework.Pool;
using StellarFramework.Settings;
using StellarFramework.UI.Adaptation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HotUpdate
{
    /// <summary>
    /// Small, fully hot-updated game loop used by the public Android demo.
    /// All gameplay and presentation code lives in HotUpdate.dll; the Player only supplies
    /// Unity and StellarFramework runtime Kits.
    /// </summary>
    public sealed class TankArenaGame : MonoBehaviour, IView
    {
        private const string RootName = "StellarTankArena";
        private const string SaveSlot = "tank-arena-profile";
        private const string ScreenShakeSetting = SettingsKeys.GameplayScreenShake;

        private static readonly Color Navy = new Color32(7, 16, 29, 255);
        private static readonly Color Board = new Color32(12, 29, 45, 255);
        private static readonly Color Grid = new Color32(25, 53, 70, 255);
        private static readonly Color Cyan = new Color32(79, 234, 218, 255);
        private static readonly Color Amber = new Color32(255, 190, 92, 255);
        private static readonly Color Coral = new Color32(255, 103, 99, 255);
        private static readonly Color Ink = new Color32(7, 17, 29, 255);

        private sealed class Enemy
        {
            public GameObject Root;
            public int HitPoints;
            public float ContactCooldown;
            public float Speed;
        }

        private sealed class Projectile : IPoolable
        {
            public GameObject Root;
            public Vector3 Direction;
            public float Age;

            public Projectile() { }

            public void OnAllocated()
            {
                Root = null;
                Direction = Vector3.zero;
                Age = 0f;
            }

            public void OnRecycled()
            {
                Root = null;
                Direction = Vector3.zero;
                Age = 0f;
            }
        }

        private sealed class RepairCore
        {
            public GameObject Root;
            public float Age;
        }

        private sealed class ArenaEffect : IPoolable
        {
            public GameObject Root;
            public float Age;
            public float Duration;
            public float MaximumScale;
            public float BaseWidth;

            public ArenaEffect() { }

            public void OnAllocated()
            {
                Root = null;
                Age = 0f;
                Duration = 0f;
                MaximumScale = 0f;
                BaseWidth = 0f;
            }

            public void OnRecycled() => OnAllocated();
        }

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<RepairCore> _repairCores = new List<RepairCore>();
        private readonly List<ArenaEffect> _arenaEffects = new List<ArenaEffect>();

        [Serializable]
        private sealed class DemoConfiguration
        {
            public float playerMoveSpeed = 4.25f;
            public float projectileInterval = 0.28f;
            public float enemySpawnInterval = 2.3f;
            public float minimumEnemySpawnInterval = 0.8f;
            public int maximumEnemies = 8;
            public int eliminationsPerWave = 5;
            public int scorePerElimination = 150;
        }

        [Serializable]
        private sealed class ProfileData
        {
            public int bestScore;
            public int totalEliminations;
            public int sorties;
        }

        private sealed class ProfileSection : SaveSection<ProfileData>
        {
            public static readonly ProfileSection Instance = new ProfileSection();
            public ProfileData Data = new ProfileData();

            public override SaveSectionId Id => SaveSectionId.From("tank-arena.profile");
            public override MissingSectionPolicy MissingPolicy => MissingSectionPolicy.UseDefault;
            public override ProfileData Capture(SaveCaptureContext context) => Data;
            public override ProfileData CreateDefault(SaveRestoreContext context) => new ProfileData();

            public override void Restore(ProfileData data, SaveRestoreContext context)
            {
                Data = data ?? new ProfileData();
            }
        }

        private TankArenaModel _model;
        private TankArenaService _service;
        private TankArenaLocalizationService _localization;
        private Transform _worldRoot;
        private Transform _player;
        private Transform _playerTurret;
        private Transform _hudRoot;
        private TankArenaJoystick _joystick;
        private TankArenaJoystick _aimJoystick;
        private Canvas _canvas;
        private UIAdaptationProfile _adaptationProfile;
        private RectTransform _safeAreaRoot;
        private Text _scoreText;
        private Text _waveText;
        private Text _hullText;
        private Text _objectiveText;
        private Text _fireHintText;
        private Text _phaseText;
        private Text _gameOverTitle;
        private Text _gameOverResult;
        private Text _repairNotice;
        private Image _hullFill;
        private Text _hullPercent;
        private Text _scoreCaption;
        private Text _waveCaption;
        private GameObject _pauseOverlay;
        private GameObject _gameOverOverlay;
        private GameObject _systemsOverlay;
        private Button _pauseButton;
        private Button _languageButton;
        private Button _fireModeButton;
        private Button _systemsButton;
        private Button _screenShakeButton;
        private Text _systemsPhaseText;
        private Text _systemsConfigText;
        private Text _systemsBestText;
        private Text _systemsEventText;
        private Text _systemsPoolText;
        private Text _screenShakeLabel;
        private DemoConfiguration _config = new DemoConfiguration();
        private Camera _arenaCamera;
        private Vector3 _arenaCameraStartPosition;
        private IUnRegister _signalRegistration;
        private float _spawnTimer;
        private float _fireTimer;
        private float _repairNoticeTimer;
        private float _cameraShakeTimer;
        private int _pooledProjectileAllocations;
        private int _pooledProjectileRecycles;
        private int _bestScore;
        private int _totalEliminations;
        private int _sorties;
        private int _sessionEliminations;
        private int _sessionEvents;
        private string _lastEventKey = "tank.systems_event_ready";
        private int _lastEventValue;
        private bool _systemsOpenedFromActive;
        private bool _profileSavedForCurrentMatch;
        private bool _configIsUserOverride;
        private bool _autoFire = true;
        private Vector3 _turretAimDirection = Vector3.forward;
        private bool _isBound;

        public IReadOnlyArchitecture Architecture => TankArenaArchitecture.Interface;

        public static void Launch()
        {
            if (GameObject.Find(RootName) != null)
            {
                Debug.Log("[StellarTankArena] Existing game instance is already running.");
                return;
            }

            Canvas[] oldCanvases = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < oldCanvases.Length; i++)
            {
                if (oldCanvases[i] != null) oldCanvases[i].enabled = false;
            }
            Camera[] oldCameras = FindObjectsOfType<Camera>(true);
            for (int i = 0; i < oldCameras.Length; i++)
            {
                if (oldCameras[i] != null) oldCameras[i].gameObject.SetActive(false);
            }

            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;

            TankArenaArchitecture architecture = TankArenaArchitecture.Interface;
            if (architecture.State == ArchitectureState.Uninitialized)
            {
                architecture.Init();
            }

            GameObject hostPrefab = LoadSamplePrefab("GameHost");
            GameObject root = Instantiate(hostPrefab);
            root.name = RootName;
            root.AddComponent<TankArenaGame>();
            DontDestroyOnLoad(root);
            Debug.Log("[StellarTankArena] HotUpdate game launched. marker=TankArenaMainEntered");
        }

        private async void Start()
        {
            _model = TankArenaArchitecture.Interface.GetModel<TankArenaModel>();
            _service = TankArenaArchitecture.Interface.GetService<TankArenaService>();
            _localization = TankArenaArchitecture.Interface.GetService<TankArenaLocalizationService>();
            if (_model == null || _service == null || _localization == null)
            {
                LogKit.LogError("[StellarTankArena] Architecture registration is incomplete.");
                return;
            }

            InitializeSettings();
            InitializeSaveKit();
            await LoadDemoConfigurationAsync();
            await LoadProfileAsync();
            if (this == null || gameObject == null) return;

            _service.ConfigureScoring(_config.scorePerElimination, _config.eliminationsPerWave);
            _service.ResetMatch();
            BuildWorld();
            BuildInterface();
            OnBind();
            SpawnEnemy(-2.7f, 5.7f);
            SpawnEnemy(2.5f, 6.6f);
            _spawnTimer = Time.time + _config.enemySpawnInterval;
            LogKit.Log("[StellarTankArena] Match ready. Controls: drag the left pad to move; hold FIRE to engage.");
        }

        private void InitializeSettings()
        {
            if (SettingsKit.IsInitialized) return;
            SettingsKit.ConfigureStorage(new PlayerPrefsSettingsStorage("Stellar.TankArena."));
            SettingsKit.InstallDefaultProviders(new DefaultSettingsInstallOptions
            {
                IncludeGameplay = true,
                IncludeAudio = false,
                IncludeGraphics = false,
                IncludeInput = false,
                IncludeLanguage = false
            });
            SettingsKit.Init();
        }

        private void InitializeSaveKit()
        {
            if (!SaveKit.IsInitialized)
            {
                SaveKit.Initialize(builder => builder.SetApplicationVersion(HotUpdateMain.PackageVersion));
            }

            if (!SaveKit.TryGetSection(ProfileSection.Instance.Id, out _))
            {
                SaveKit.Register(ProfileSection.Instance);
            }
        }

        private async System.Threading.Tasks.Task LoadDemoConfigurationAsync()
        {
            try
            {
                ConfigTextLoadResult result = await ConfigTextSources.Default.LoadAsync("TankArena/demo-config.json");
                if (result.IsSuccess)
                {
                    DemoConfiguration loaded = JsonUtility.FromJson<DemoConfiguration>(result.Text);
                    if (loaded != null)
                    {
                        _config = NormalizeConfiguration(loaded);
                        _configIsUserOverride = result.IsUserSave;
                        LogKit.Log("[StellarTankArena] ConfigKit loaded " +
                                   (result.IsUserSave ? "PersistentDataPath override." : "StreamingAssets defaults."));
                    }
                }
                else
                {
                    LogKit.LogWarning("[StellarTankArena] ConfigKit kept safe defaults: " + result.Error);
                }
            }
            catch (Exception exception)
            {
                LogKit.LogWarning("[StellarTankArena] ConfigKit kept safe defaults: " + exception.Message);
            }
        }

        private static DemoConfiguration NormalizeConfiguration(DemoConfiguration config)
        {
            config.playerMoveSpeed = Mathf.Clamp(config.playerMoveSpeed, 1f, 10f);
            config.projectileInterval = Mathf.Clamp(config.projectileInterval, 0.08f, 2f);
            config.enemySpawnInterval = Mathf.Clamp(config.enemySpawnInterval, 0.5f, 20f);
            config.minimumEnemySpawnInterval = Mathf.Clamp(config.minimumEnemySpawnInterval, 0.25f, config.enemySpawnInterval);
            config.maximumEnemies = Mathf.Clamp(config.maximumEnemies, 2, 20);
            config.eliminationsPerWave = Mathf.Clamp(config.eliminationsPerWave, 1, 20);
            config.scorePerElimination = Mathf.Clamp(config.scorePerElimination, 10, 10000);
            return config;
        }

        private async System.Threading.Tasks.Task LoadProfileAsync()
        {
            try
            {
                SaveResult result = await SaveKit.LoadAsync(SaveSlot);
                if (result.IsSuccess)
                {
                    ProfileSection.Instance.Data ??= new ProfileData();
                    _bestScore = ProfileSection.Instance.Data.bestScore;
                    _totalEliminations = ProfileSection.Instance.Data.totalEliminations;
                    _sorties = ProfileSection.Instance.Data.sorties;
                }
            }
            catch (Exception exception)
            {
                LogKit.LogWarning("[StellarTankArena] SaveKit profile load skipped: " + exception.Message);
            }
        }

        private async void SaveProfileAsync()
        {
            ProfileData data = ProfileSection.Instance.Data ?? (ProfileSection.Instance.Data = new ProfileData());
            data.bestScore = Mathf.Max(data.bestScore, _model.Score.Value);
            data.totalEliminations += _sessionEliminations;
            data.sorties++;
            _bestScore = data.bestScore;
            _totalEliminations = data.totalEliminations;
            _sorties = data.sorties;
            try
            {
                SaveResult result = await SaveKit.SaveAsync(SaveSlot);
                if (!result.IsSuccess)
                    LogKit.LogWarning("[StellarTankArena] SaveKit profile save failed: " + result.ErrorMessage);
                else
                    LogKit.Log("[StellarTankArena] SaveKit profile saved. best=" + _bestScore);
            }
            catch (Exception exception)
            {
                LogKit.LogWarning("[StellarTankArena] SaveKit profile save failed: " + exception.Message);
            }
            RefreshSystemsPanel();
        }

        private void OnSystemSignal(TankArenaSignal signal)
        {
            if (signal == null) return;
            _sessionEvents++;
            _lastEventValue = signal.Value;
            switch (signal.Kind)
            {
                case TankArenaEventKind.Elimination:
                    _sessionEliminations++;
                    _lastEventKey = "tank.systems_event_elimination";
                    break;
                case TankArenaEventKind.WaveAdvanced:
                    _lastEventKey = "tank.systems_event_wave";
                    break;
                case TankArenaEventKind.HullRepaired:
                    _lastEventKey = "tank.systems_event_repair";
                    break;
                default:
                    _lastEventKey = "tank.systems_event_damage";
                    break;
            }

            RefreshSystemsPanel();
        }

        public void OnBind()
        {
            if (_isBound || _model == null) return;
            _model.Score.RegisterWithInitValue(RefreshScore).UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.Wave.RegisterWithInitValue(RefreshWave).UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.Hull.RegisterWithInitValue(RefreshHull).UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.Eliminations.RegisterWithInitValue(RefreshResult).UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.Phase.RegisterWithInitValue(OnPhaseChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
            if (_localization.Text != null)
            {
                _localization.Text.LocaleChanged += OnLocaleChanged;
                RefreshLocalizedContent();
            }
            _signalRegistration = GlobalTypeEvent.Register<TankArenaSignal>(OnSystemSignal)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _isBound = true;
        }

        public void OnUnbind()
        {
            if (!_isBound) return;
            if (_localization != null && _localization.Text != null)
                _localization.Text.LocaleChanged -= OnLocaleChanged;
            _signalRegistration?.UnRegister();
            _signalRegistration = null;
            _isBound = false;
        }

        private void Update()
        {
            UpdateCameraShake();
            if (_model == null || _model.Phase.Value != TankArenaPhase.Active || _player == null) return;

            Vector2 move = _joystick != null ? _joystick.Value : Vector2.zero;
            try
            {
                move += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }
            catch (Exception)
            {
                // A project may use the new Input System only; the on-screen controls remain available.
            }
            move = Vector2.ClampMagnitude(move, 1f);

            Vector3 displacement = new Vector3(move.x, 0f, move.y) * (_config.playerMoveSpeed * Time.deltaTime);
            _player.position += displacement;
            Vector3 playerPosition = _player.position;
            playerPosition.x = Mathf.Clamp(playerPosition.x, -4.15f, 4.15f);
            playerPosition.z = Mathf.Clamp(playerPosition.z, -6.6f, 5.3f);
            _player.position = playerPosition;
            if (move.sqrMagnitude > 0.01f)
                _player.rotation = Quaternion.LookRotation(new Vector3(move.x, 0f, move.y));

            Vector2 aim = _aimJoystick != null ? _aimJoystick.Value : Vector2.zero;
            if (!_autoFire && aim.sqrMagnitude > 0.025f)
                _turretAimDirection = new Vector3(aim.x, 0f, aim.y).normalized;

            bool hasHostile = TryGetNearestHostileDirection(out Vector3 hostileDirection);
            if (_autoFire && hasHostile) _turretAimDirection = hostileDirection;
            if (_turretAimDirection.sqrMagnitude > 0.001f && _playerTurret != null)
                _playerTurret.rotation = Quaternion.LookRotation(_turretAimDirection, Vector3.up);

            bool firing = (_autoFire && hasHostile) ||
                          (!_autoFire && _aimJoystick != null && _aimJoystick.IsHeld);
            try { firing |= Input.GetKey(KeyCode.Space); }
            catch (Exception) { }
            if (firing && Time.time >= _fireTimer)
            {
                FireAlongTurretDirection();
                _fireTimer = Time.time + _config.projectileInterval;
            }

            UpdateEnemies();
            UpdateProjectiles();
            UpdateRepairCores();
            UpdateArenaEffects();
            if (_enemies.Count < _config.maximumEnemies && Time.time >= _spawnTimer)
            {
                SpawnEnemyAtRandomEdge();
                _spawnTimer = Time.time + Mathf.Max(
                    _config.minimumEnemySpawnInterval,
                    _config.enemySpawnInterval - _model.Wave.Value * 0.08f);
            }

            if (_repairNoticeTimer > 0f)
            {
                _repairNoticeTimer -= Time.deltaTime;
                if (_repairNoticeTimer <= 0f && _repairNotice != null)
                    _repairNotice.gameObject.SetActive(false);
            }
        }

        private void UpdateCameraShake()
        {
            if (_arenaCamera == null) return;
            if (_cameraShakeTimer > 0f)
            {
                _cameraShakeTimer -= Time.unscaledDeltaTime;
                float strength = Mathf.Clamp01(SettingsKit.GetValue<float>(ScreenShakeSetting, 1f));
                float fade = Mathf.Clamp01(_cameraShakeTimer / 0.22f);
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * (0.12f * strength * fade);
                _arenaCamera.transform.localPosition = _arenaCameraStartPosition +
                    new Vector3(jitter.x, 0f, jitter.y);
            }
            else if (_arenaCamera.transform.localPosition != _arenaCameraStartPosition)
            {
                _arenaCamera.transform.localPosition = _arenaCameraStartPosition;
            }
        }

        private void BuildWorld()
        {
            GameObject arena = Instantiate(LoadSamplePrefab("Arena"), transform, false);
            arena.name = "TankArenaWorld";

            _worldRoot = arena.transform.Find("ArenaWorld");
            _arenaCamera = arena.GetComponentInChildren<Camera>(true);
            if (_worldRoot == null || _arenaCamera == null)
                throw new InvalidOperationException("TankArena Arena.prefab must contain ArenaWorld and ArenaCamera.");

            _arenaCameraStartPosition = _arenaCamera.transform.localPosition;
            _player = _worldRoot.Find("PlayerTank");
            _playerTurret = _player != null ? _player.Find("TurretPivot") : null;
            if (_player == null || _playerTurret == null)
                throw new InvalidOperationException("TankArena Arena.prefab must contain PlayerTank/TurretPivot.");
        }

        private void BuildInterface()
        {
            GameObject hud = Instantiate(LoadSamplePrefab("HUD"), transform, false);
            _hudRoot = hud.transform;
            _canvas = hud.GetComponent<Canvas>();
            _safeAreaRoot = FindHud<RectTransform>("SafeAreaRoot");
            _adaptationProfile = Resources.Load<UIAdaptationProfile>("TankArena/Generated/Profiles/UIAdaptationProfile");
            UIAdaptationController controller = _canvas != null ? _canvas.GetComponent<UIAdaptationController>() : null;
            if (_canvas == null || _safeAreaRoot == null || _adaptationProfile == null || controller == null)
                throw new InvalidOperationException("TankArena HUD prefab must include Canvas, SafeAreaRoot, UIAdaptationController and its Resources profile.");

            controller.DisplayGeometryChanged += OnDisplayGeometryChanged;
            controller.Configure(_adaptationProfile, _safeAreaRoot);
            controller.ApplyCurrentScreen();

            _scoreText = FindHud<Text>("ScoreCardValue");
            _waveText = FindHud<Text>("WaveCardValue");
            _hullText = FindHud<Text>("HullLabel");
            _objectiveText = FindHud<Text>("Objective");
            _fireHintText = FindHud<Text>("FireHint");
            _phaseText = FindHud<Text>("PhaseNotice");
            _gameOverTitle = FindHud<Text>("GameOverTitle");
            _gameOverResult = FindHud<Text>("GameOverResult");
            _repairNotice = FindHud<Text>("RepairNotice");
            _hullFill = FindHud<Image>("HullFill");
            _hullPercent = FindHud<Text>("HullPercent");
            _scoreCaption = FindHud<Text>("ScoreCardCaption");
            _waveCaption = FindHud<Text>("WaveCardCaption");

            _pauseOverlay = FindHud<Transform>("PauseOverlay").gameObject;
            _gameOverOverlay = FindHud<Transform>("GameOverOverlay").gameObject;
            _systemsOverlay = FindHud<Transform>("FrameworkSystemsOverlay").gameObject;
            _pauseButton = FindHud<Button>("Pause");
            _languageButton = FindHud<Button>("Language");
            _fireModeButton = FindHud<Button>("FireModeButton");
            _systemsButton = FindHud<Button>("SystemsButton");
            _screenShakeButton = FindHud<Button>("ScreenShakeButton");
            _screenShakeLabel = FindHud<Text>("ScreenShakeButtonLabel");
            _systemsPhaseText = FindHud<Text>("SystemsBattleText");
            _systemsConfigText = FindHud<Text>("SystemsDataText");
            _systemsBestText = FindHud<Text>("SystemsDisplayText");
            _systemsEventText = FindHud<Text>("SystemsEventText");
            _systemsPoolText = FindHud<Text>("SystemsPoolText");

            _pauseButton.onClick.AddListener(OnPausePressed);
            _languageButton.onClick.AddListener(OnToggleLanguage);
            _fireModeButton.onClick.AddListener(OnToggleFireMode);
            _systemsButton.onClick.AddListener(OnOpenSystems);
            _screenShakeButton.onClick.AddListener(OnToggleScreenShake);
            FindHud<Button>("ResumeButton").onClick.AddListener(OnResumePressed);
            FindHud<Button>("PauseSystemsButton").onClick.AddListener(OnOpenSystems);
            FindHud<Button>("PauseRestartButton").onClick.AddListener(RestartMatch);
            FindHud<Button>("GameOverRestartButton").onClick.AddListener(RestartMatch);
            FindHud<Button>("SystemsCloseButton").onClick.AddListener(OnCloseSystems);

            Image movePad = FindHud<Image>("MovePad");
            _joystick = movePad.gameObject.AddComponent<TankArenaJoystick>();
            _joystick.Initialize(movePad.rectTransform);
            _joystick.SetVisuals(FindHud<RectTransform>("MovePadRing"), FindHud<RectTransform>("MovePadKnob"));

            Image aimPad = FindHud<Image>("AimPad");
            _aimJoystick = aimPad.gameObject.AddComponent<TankArenaJoystick>();
            _aimJoystick.Initialize(aimPad.rectTransform);
            _aimJoystick.SetVisuals(FindHud<RectTransform>("AimPadRing"), FindHud<RectTransform>("AimPadKnob"));

            _repairNotice.gameObject.SetActive(false);
            OnLocaleChanged(this, null);
            RefreshScore(_model.Score.Value);
            RefreshWave(_model.Wave.Value);
            RefreshHull(_model.Hull.Value);

            ActionKit.Sequence(gameObject)
                .ScaleTo(_pauseButton.transform, Vector3.one, 0.28f, Ease.OutBack)
                .Start();
        }

        private static GameObject LoadSamplePrefab(string prefabName)
        {
            GameObject prefab = Resources.Load<GameObject>("TankArena/Prefabs/" + prefabName);
            if (prefab == null)
            {
                throw new InvalidOperationException("TankArena sample prefab is missing: Resources/TankArena/Prefabs/" + prefabName + ".prefab");
            }
            return prefab;
        }

        private T FindHud<T>(string objectName) where T : Component
        {
            if (_hudRoot == null) return null;
            Transform[] objects = _hudRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                if (!string.Equals(objects[i].name, objectName, StringComparison.Ordinal)) continue;
                T component = objects[i].GetComponent<T>();
                if (component != null) return component;
            }
            throw new InvalidOperationException("TankArena HUD prefab is missing " + typeof(T).Name + " named '" + objectName + "'.");
        }

        private Text FindSystemText(string name)
        {
            Text[] texts = _systemsOverlay != null ? _systemsOverlay.GetComponentsInChildren<Text>(true) : Array.Empty<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.Equals(texts[i].name, name, StringComparison.Ordinal)) return texts[i];
            }
            return null;
        }

        private void UpdateEnemies()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _enemies[i];
                if (enemy.Root == null)
                {
                    _enemies.RemoveAt(i);
                    continue;
                }

                Vector3 direction = _player.position - enemy.Root.transform.position;
                direction.y = 0f;
                float distance = direction.magnitude;
                if (distance > 1.1f)
                {
                    direction.Normalize();
                    enemy.Root.transform.position += direction * enemy.Speed * Time.deltaTime;
                    enemy.Root.transform.rotation = Quaternion.LookRotation(direction);
                }
                else
                {
                    enemy.ContactCooldown -= Time.deltaTime;
                    if (enemy.ContactCooldown <= 0f)
                    {
                        _service.ApplyDamage(10);
                        enemy.ContactCooldown = 1.05f;
                        if (_model.Hull.Value <= 0) return;
                    _cameraShakeTimer = 0.22f;
                    }
                }
            }
        }

        private void UpdateProjectiles()
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _projectiles[i];
                if (projectile.Root == null)
                {
                    RecycleProjectileAt(i, false);
                    continue;
                }

                projectile.Age += Time.deltaTime;
                projectile.Root.transform.position += projectile.Direction * (13.5f * Time.deltaTime);
                bool consumed = false;
                for (int enemyIndex = _enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
                {
                    Enemy enemy = _enemies[enemyIndex];
                    if (enemy.Root == null ||
                        FlatDistance(projectile.Root.transform.position, enemy.Root.transform.position) > 0.7f)
                        continue;

                    enemy.HitPoints--;
                    Vector3 hitPosition = enemy.Root.transform.position;
                    if (enemy.HitPoints <= 0)
                    {
                        Vector3 destroyedAt = hitPosition;
                        Destroy(enemy.Root);
                        _enemies.RemoveAt(enemyIndex);
                        _service.RegisterElimination();
                        SpawnRingEffect(destroyedAt, Amber, 1.35f, 0.34f);
                        SpawnRingEffect(destroyedAt, new Color32(255, 112, 87, 255), 0.82f, 0.22f);
                        if (_model.Eliminations.Value % 4 == 0) SpawnRepairCore(destroyedAt);
                        LogKit.Log($"[StellarTankArena] Hostile destroyed. eliminations={_model.Eliminations.Value}");
                    }
                    else
                    {
                        enemy.Root.transform.localScale *= 0.92f;
                        SpawnRingEffect(hitPosition, new Color32(255, 222, 163, 255), 0.62f, 0.18f);
                    }
                    consumed = true;
                    break;
                }

                if (consumed || projectile.Age > 1.7f ||
                    Mathf.Abs(projectile.Root.transform.position.x) > 5f ||
                    Mathf.Abs(projectile.Root.transform.position.z) > 8f)
                {
                    RecycleProjectileAt(i, true);
                }
            }
        }

        private void UpdateRepairCores()
        {
            for (int i = _repairCores.Count - 1; i >= 0; i--)
            {
                RepairCore core = _repairCores[i];
                if (core.Root == null)
                {
                    _repairCores.RemoveAt(i);
                    continue;
                }
                core.Age += Time.deltaTime;
                core.Root.transform.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);
                core.Root.transform.position = new Vector3(
                    core.Root.transform.position.x,
                    0.4f + Mathf.Sin(core.Age * 3f) * 0.08f,
                    core.Root.transform.position.z);
                if (FlatDistance(_player.position, core.Root.transform.position) > 0.8f) continue;

                Destroy(core.Root);
                _repairCores.RemoveAt(i);
                _service.RepairHull(25);
                _repairNotice.text = _localization.Get("tank.pickup");
                _repairNotice.gameObject.SetActive(true);
                _repairNoticeTimer = 2f;
                ActionKit.Sequence(gameObject)
                    .ScaleTo(_repairNotice.transform, Vector3.one * 1.12f, 0.15f, Ease.OutBack)
                    .ScaleTo(_repairNotice.transform, Vector3.one, 0.16f)
                    .Start();
            }
        }

        private bool TryGetNearestHostileDirection(out Vector3 direction)
        {
            direction = _turretAimDirection;
            if (_enemies.Count == 0) return false;
            Enemy closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Root == null) continue;
                float distance = FlatDistance(_player.position, _enemies[i].Root.transform.position);
                if (distance < closestDistance)
                {
                    closest = _enemies[i];
                    closestDistance = distance;
                }
            }
            if (closest == null) return false;

            direction = closest.Root.transform.position - _player.position;
            direction.y = 0f;
            direction.Normalize();
            return true;
        }

        private void FireAlongTurretDirection()
        {
            Vector3 direction = _turretAimDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();
            Vector3 muzzle = _playerTurret != null
                ? _playerTurret.position + direction * 1.04f + Vector3.up * 0.03f
                : _player.position + direction * 0.9f + Vector3.up * 0.36f;
            GameObject shot = InstantiateSamplePrefab("Projectile", _worldRoot);
            shot.transform.position = muzzle;
            shot.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
            Projectile projectile = PoolKit.Allocate<Projectile>();
            projectile.Root = shot;
            projectile.Direction = direction;
            _projectiles.Add(projectile);
            _pooledProjectileAllocations++;
            SpawnRingEffect(muzzle, new Color32(255, 226, 162, 255), 0.42f, 0.13f);
        }

        private void RecycleProjectileAt(int index, bool destroyRoot)
        {
            if (index < 0 || index >= _projectiles.Count) return;
            Projectile projectile = _projectiles[index];
            _projectiles.RemoveAt(index);
            if (destroyRoot && projectile.Root != null) Destroy(projectile.Root);
            PoolKit.Recycle<Projectile>(projectile);
            _pooledProjectileRecycles++;
            RefreshSystemsPanel();
        }

        private void SpawnEnemyAtRandomEdge()
        {
            float x = UnityEngine.Random.Range(-3.6f, 3.6f);
            float z = UnityEngine.Random.value > 0.5f
                ? UnityEngine.Random.Range(5.7f, 7f)
                : UnityEngine.Random.Range(-6.5f, -5.8f);
            SpawnEnemy(x, z);
        }

        private void SpawnEnemy(float x, float z)
        {
            bool heavy = _model.Eliminations.Value >= 5 && UnityEngine.Random.value > 0.68f;
            GameObject enemyPrefab = InstantiateSamplePrefab(heavy ? "EnemyHeavy" : "EnemyLight", _worldRoot);
            enemyPrefab.name = "HostileTank";
            enemyPrefab.transform.position = new Vector3(x, 0f, z);
            _enemies.Add(new Enemy
            {
                Root = enemyPrefab,
                HitPoints = heavy ? 3 : 2,
                Speed = heavy ? 0.95f : 1.25f + Mathf.Min(0.5f, _model.Wave.Value * 0.035f)
            });
        }

        private void SpawnRepairCore(Vector3 position)
        {
            GameObject core = InstantiateSamplePrefab("RepairCore", _worldRoot);
            core.transform.position = new Vector3(position.x, 0.22f, position.z);
            _repairCores.Add(new RepairCore { Root = core });
        }

        private void SpawnRingEffect(Vector3 position, Color color, float maximumScale, float duration)
        {
            GameObject effectObject = InstantiateSamplePrefab("ShockRing", _worldRoot);
            effectObject.name = "CombatShockRing";
            effectObject.transform.position = new Vector3(position.x, 0.015f, position.z);
            effectObject.transform.localScale = Vector3.one * 0.08f;

            LineRenderer line = effectObject.GetComponent<LineRenderer>();
            line.startColor = color;
            line.endColor = color;

            ArenaEffect effect = PoolKit.Allocate<ArenaEffect>();
            effect.Root = effectObject;
            effect.Duration = Mathf.Max(0.05f, duration);
            effect.MaximumScale = Mathf.Max(0.1f, maximumScale);
            effect.BaseWidth = line.widthMultiplier;
            _arenaEffects.Add(effect);
        }

        private GameObject InstantiateSamplePrefab(string prefabName, Transform parent)
        {
            GameObject instance = Instantiate(LoadSamplePrefab(prefabName), parent, false);
            instance.name = prefabName;
            return instance;
        }

        private void UpdateArenaEffects()
        {
            for (int i = _arenaEffects.Count - 1; i >= 0; i--)
            {
                ArenaEffect effect = _arenaEffects[i];
                if (effect.Root == null)
                {
                    RecycleArenaEffectAt(i, false);
                    continue;
                }

                effect.Age += Time.deltaTime;
                float progress = Mathf.Clamp01(effect.Age / effect.Duration);
                float eased = 1f - Mathf.Pow(1f - progress, 2f);
                effect.Root.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, effect.MaximumScale, eased);
                LineRenderer line = effect.Root.GetComponent<LineRenderer>();
                if (line != null) line.widthMultiplier = Mathf.Lerp(effect.BaseWidth, effect.BaseWidth * 0.35f, progress);
                if (progress < 1f) continue;

                RecycleArenaEffectAt(i, true);
            }
        }

        private void RecycleArenaEffectAt(int index, bool destroyRoot)
        {
            if (index < 0 || index >= _arenaEffects.Count) return;
            ArenaEffect effect = _arenaEffects[index];
            _arenaEffects.RemoveAt(index);
            if (destroyRoot && effect.Root != null) Destroy(effect.Root);
            PoolKit.Recycle<ArenaEffect>(effect);
        }

        private void OnDisplayGeometryChanged(UIDisplayGeometry geometry)
        {
            float width = Mathf.Max(1, geometry.Width);
            float height = Mathf.Max(1, geometry.Height);
            Vector2 expectedMin = new Vector2(geometry.SafeArea.xMin / width, geometry.SafeArea.yMin / height);
            Vector2 expectedMax = new Vector2(geometry.SafeArea.xMax / width, geometry.SafeArea.yMax / height);
            bool anchorsMatch = _safeAreaRoot != null &&
                Vector2.Distance(_safeAreaRoot.anchorMin, expectedMin) <= 0.005f &&
                Vector2.Distance(_safeAreaRoot.anchorMax, expectedMax) <= 0.005f;
            string message = $"[StellarTankArena][UIAdaptation] result={(anchorsMatch ? "PASS" : "FAIL")}, " +
                             $"resolution={geometry.Width}x{geometry.Height}, safeArea={geometry.SafeArea}, " +
                             $"insets={geometry.HasSafeAreaInsets}, breakpoint={_canvas.GetComponent<UIAdaptationController>().CurrentBreakpointId}";
            if (anchorsMatch) Debug.Log(message);
            else Debug.LogError(message);
        }

        private void RefreshScore(int score)
        {
            if (_scoreText != null) _scoreText.text = score.ToString("00000", CultureInfo.InvariantCulture);
            RefreshResult(_model != null ? _model.Eliminations.Value : 0);
        }

        private void RefreshWave(int wave)
        {
            if (_waveText != null) _waveText.text = wave.ToString("00", CultureInfo.InvariantCulture);
        }

        private void RefreshHull(int hull)
        {
            if (_hullPercent != null) _hullPercent.text = hull.ToString("000", CultureInfo.InvariantCulture) + "%";
            if (_hullFill != null)
            {
                RectTransform rect = _hullFill.rectTransform;
                float minX = 0.05f;
                float maxX = Mathf.Lerp(minX, 0.95f, hull / 100f);
                rect.anchorMin = new Vector2(minX, 0.765f);
                rect.anchorMax = new Vector2(maxX, 0.778f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _hullFill.color = hull > 35 ? Cyan : Coral;
            }
        }

        private void RefreshResult(int eliminations)
        {
            if (_gameOverResult == null || _localization == null) return;
            var arguments = new[]
            {
                new LocalizationFormatArgument("score", _model.Score.Value.ToString("N0", CultureInfo.InvariantCulture)),
                new LocalizationFormatArgument("kills", eliminations.ToString(CultureInfo.InvariantCulture))
            };
            if (_localization.Text.TryFormat(LocalizationKey.From("tank.result"), arguments,
                    out string formatted, out string error))
            {
                _gameOverResult.text = formatted;
            }
            else
            {
                LogKit.LogError("[StellarTankArena] Could not format the result label: " + error);
            }
        }

        private void RefreshLocalizedContent()
        {
            if (_localization == null || _localization.Text == null) return;
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                switch (texts[i].name)
                {
                    case "GameTitle": texts[i].text = _localization.Get("tank.title"); break;
                    case "GameSubtitle": texts[i].text = _localization.Get("tank.subtitle"); break;
                    case "PackageVersionLabel":
                        texts[i].text = _localization.Get("tank.package") + "  " + HotUpdateMain.PackageVersion;
                        break;
                    case "Objective": texts[i].text = _localization.Get("tank.objective"); break;
                    case "MoveHint": texts[i].text = _localization.Get("tank.move"); break;
                    case "FireHint":
                        texts[i].text = _localization.Get(_autoFire ? "tank.fire_hint_auto" : "tank.fire_hint_manual");
                        break;
                    case "FireModeButtonLabel":
                        texts[i].text = _localization.Get(_autoFire ? "tank.mode_auto" : "tank.mode_manual");
                        break;
                    case "PausedTitle": texts[i].text = _localization.Get("tank.paused"); break;
                    case "PauseSystemsButtonLabel": texts[i].text = _localization.Get("tank.systems"); break;
                    case "SystemsTitle": texts[i].text = _localization.Get("tank.systems_title"); break;
                    case "SystemsSubtitle": texts[i].text = _localization.Get("tank.systems_subtitle"); break;
                    case "SystemsCloseButtonLabel": texts[i].text = _localization.Get("tank.systems_close"); break;
                    case "GameOverTitle": texts[i].text = _localization.Get("tank.game_over"); break;
                    case "PhaseNotice": texts[i].text = _localization.Get("tank.phase_ready"); break;
                    case "GameOverRestartButtonLabel":
                    case "PauseRestartButtonLabel": texts[i].text = _localization.Get("tank.restart"); break;
                    case "ResumeButtonLabel": texts[i].text = _localization.Get("tank.resume"); break;
                    case "HullLabel": texts[i].text = _localization.Get("tank.hull"); break;
                    case "RepairNotice":
                        if (_repairNotice != null && _repairNotice.gameObject.activeSelf)
                            texts[i].text = _localization.Get("tank.pickup");
                        break;
                }
            }

            if (_scoreCaption != null) _scoreCaption.text = _localization.Get("tank.score");
            if (_waveCaption != null) _waveCaption.text = _localization.Get("tank.wave");
            if (_hullText != null) _hullText.text = _localization.Get("tank.hull");
            if (_languageButton != null)
            {
                Text languageLabel = _languageButton.GetComponentInChildren<Text>(true);
                if (languageLabel != null) languageLabel.text = _localization.Text.CurrentLocale.Value == "en-US" ? "EN" : "中";
            }
            if (_fireModeButton != null)
            {
                Text modeLabel = _fireModeButton.GetComponentInChildren<Text>(true);
                if (modeLabel != null)
                    modeLabel.text = _localization.Get(_autoFire ? "tank.mode_auto" : "tank.mode_manual");
            }
            RefreshResult(_model.Eliminations.Value);
            RefreshSystemsPanel();
        }

        private void OnLocaleChanged(object sender, LocalizationChangedEventArgs args)
        {
            RefreshLocalizedContent();
        }

        private void OnToggleLanguage()
        {
            _localization.ToggleLanguage();
            ActionKit.Sequence(gameObject)
                .ScaleTo(_languageButton.transform, Vector3.one * 1.12f, 0.1f)
                .ScaleTo(_languageButton.transform, Vector3.one, 0.14f, Ease.OutBack)
                .Start();
        }

        private void OnToggleFireMode()
        {
            _autoFire = !_autoFire;
            RefreshLocalizedContent();
            ActionKit.Sequence(gameObject)
                .ScaleTo(_fireModeButton.transform, Vector3.one * 1.08f, 0.08f)
                .ScaleTo(_fireModeButton.transform, Vector3.one, 0.12f, Ease.OutBack)
                .Start();
        }

        private void OnPausePressed()
        {
            if (_model.Phase.Value != TankArenaPhase.Active) return;
            _service.SetPhase(TankArenaPhase.Paused);
            Time.timeScale = 0f;
        }

        private void OnResumePressed()
        {
            Time.timeScale = 1f;
            _service.SetPhase(TankArenaPhase.Active);
        }

        private void OnPhaseChanged(TankArenaPhase phase)
        {
            bool systemsVisible = _systemsOverlay != null && _systemsOverlay.activeSelf;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(phase == TankArenaPhase.Paused && !systemsVisible);
            if (_gameOverOverlay != null) _gameOverOverlay.SetActive(phase == TankArenaPhase.GameOver && !systemsVisible);
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(phase == TankArenaPhase.Active);
            if (phase == TankArenaPhase.GameOver)
            {
                Time.timeScale = 0f;
                if (_gameOverTitle != null) _gameOverTitle.text = _localization.Get("tank.game_over");
                RefreshResult(_model.Eliminations.Value);
                LogKit.Log($"[StellarTankArena] Match complete. score={_model.Score.Value}, eliminations={_model.Eliminations.Value}");
                if (!_profileSavedForCurrentMatch)
                {
                    _profileSavedForCurrentMatch = true;
                    SaveProfileAsync();
                }
            }
            else if (phase == TankArenaPhase.Paused) Time.timeScale = 0f;
            else if (phase == TankArenaPhase.Active)
            {
                Time.timeScale = 1f;
            }
            RefreshSystemsPanel();
        }

        private void OnOpenSystems()
        {
            if (_systemsOverlay == null || _systemsOverlay.activeSelf) return;
            _systemsOpenedFromActive = _model != null && _model.Phase.Value == TankArenaPhase.Active;
            _systemsOverlay.SetActive(true);
            if (_systemsOpenedFromActive) _service.SetPhase(TankArenaPhase.Paused);
            RefreshSystemsPanel();
            ActionKit.Sequence(gameObject)
                .ScaleTo(_systemsOverlay.transform, Vector3.one, 0.24f, Ease.OutBack)
                .Start();
        }

        private void OnCloseSystems()
        {
            if (_systemsOverlay == null || !_systemsOverlay.activeSelf) return;
            _systemsOverlay.SetActive(false);
            if (_systemsOpenedFromActive) _service.SetPhase(TankArenaPhase.Active);
            else if (_model != null) OnPhaseChanged(_model.Phase.Value);
            _systemsOpenedFromActive = false;
            ActionKit.Sequence(gameObject)
                .ScaleTo(_systemsButton.transform, Vector3.one * 1.08f, 0.08f)
                .ScaleTo(_systemsButton.transform, Vector3.one, 0.12f, Ease.OutBack)
                .Start();
        }

        private void OnToggleScreenShake()
        {
            float current = SettingsKit.GetValue<float>(ScreenShakeSetting, 1f);
            object next = current > 0.5f ? 0f : 1f;
            bool updated = SettingsKit.TrySetValue(ScreenShakeSetting, next, out string error);
            if (updated && !SettingsKit.Save(out error)) updated = false;
            if (!updated)
            {
                LogKit.LogWarning("[StellarTankArena] SettingsKit update failed: " + error);
            }
            RefreshSystemsPanel();
        }

        private void RefreshSystemsPanel()
        {
            if (_systemsOverlay == null || _localization == null) return;
            string locale = _localization.Text != null ? _localization.Text.CurrentLocale.Value : "unknown";
            if (_systemsPhaseText != null)
            {
                _systemsPhaseText.text = "Architecture · BindableKit · FSMKit\n" +
                    _localization.Get("tank.systems_state") + " " + (_service?.CurrentStateName ?? "Unknown");
            }
            if (_systemsConfigText != null)
            {
                _systemsConfigText.text = "ConfigKit · SaveKit\n" +
                    _localization.Get(_configIsUserOverride
                        ? "tank.systems_config_override"
                        : "tank.systems_config_default") + " · " + _config.maximumEnemies + " / " +
                    _config.enemySpawnInterval.ToString("0.0", CultureInfo.InvariantCulture) + "s\n" +
                    _localization.Get("tank.systems_best") + " " +
                    _bestScore.ToString("N0", CultureInfo.InvariantCulture) + " · " + _sorties + " · " +
                    _totalEliminations;
            }
            if (_systemsEventText != null)
            {
                _systemsEventText.text = "EventKit · " + _localization.Get("tank.systems_recent") + "\n" +
                    _localization.Get(_lastEventKey) + " " + _lastEventValue + "   ·   " + _sessionEvents;
            }
            if (_systemsPoolText != null)
            {
                _systemsPoolText.text = "PoolKit · ActionKit\n" +
                    _localization.Get("tank.systems_pool") + " " + _pooledProjectileRecycles + " / " +
                    _pooledProjectileAllocations + "\n" + _localization.Get("tank.systems_phase") + " " +
                    (_model != null ? _model.Phase.Value.ToString() : "Unknown");
            }
            if (_systemsBestText != null)
            {
                _systemsBestText.text = "LocalizationKit · UIAdaptationKit · HotUpdateKit\n" +
                    _localization.Get("tank.systems_display") + " " + locale + "  ·  " +
                    HotUpdateMain.PackageVersion;
            }
            if (_screenShakeLabel != null)
            {
                bool enabled = SettingsKit.GetValue<float>(ScreenShakeSetting, 1f) > 0.5f;
                _screenShakeLabel.text = _localization.Get(enabled
                    ? "tank.systems_feedback_on"
                    : "tank.systems_feedback_off");
            }
            Text title = FindSystemText("SystemsTitle");
            if (title != null) title.text = _localization.Get("tank.systems_title");
            Text subtitle = FindSystemText("SystemsSubtitle");
            if (subtitle != null) subtitle.text = _localization.Get("tank.systems_subtitle");
            Text close = FindSystemText("SystemsCloseButtonLabel");
            if (close != null) close.text = _localization.Get("tank.systems_close");
            Text systemsButtonText = _systemsButton != null ? _systemsButton.GetComponentInChildren<Text>(true) : null;
            if (systemsButtonText != null) systemsButtonText.text = _localization.Get("tank.systems");
        }

        private void RestartMatch()
        {
            Time.timeScale = 1f;
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Root != null) Destroy(_enemies[i].Root);
            while (_projectiles.Count > 0) RecycleProjectileAt(_projectiles.Count - 1, true);
            while (_arenaEffects.Count > 0) RecycleArenaEffectAt(_arenaEffects.Count - 1, true);
            for (int i = 0; i < _repairCores.Count; i++) if (_repairCores[i].Root != null) Destroy(_repairCores[i].Root);
            _enemies.Clear();
            _repairCores.Clear();
            _sessionEliminations = 0;
            _sessionEvents = 0;
            _profileSavedForCurrentMatch = false;
            _lastEventKey = "tank.systems_event_ready";
            _lastEventValue = 0;
            if (_player != null)
            {
                _player.position = new Vector3(0f, 0f, -4.8f);
                _player.rotation = Quaternion.identity;
            }
            _turretAimDirection = Vector3.forward;
            if (_playerTurret != null) _playerTurret.localRotation = Quaternion.identity;
            _service.ResetMatch();
            _spawnTimer = Time.time + _config.enemySpawnInterval;
            _fireTimer = 0f;
            SpawnEnemy(-2.7f, 5.7f);
            SpawnEnemy(2.5f, 6.6f);
        }

        private void OnDestroy()
        {
            OnUnbind();
            Time.timeScale = 1f;
            while (_projectiles.Count > 0) RecycleProjectileAt(_projectiles.Count - 1, false);
            while (_arenaEffects.Count > 0) RecycleArenaEffectAt(_arenaEffects.Count - 1, false);
            if (_canvas != null)
            {
                UIAdaptationController controller = _canvas.GetComponent<UIAdaptationController>();
                if (controller != null) controller.DisplayGeometryChanged -= OnDisplayGeometryChanged;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }

    public sealed class TankArenaJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _root;
        private RectTransform _knob;
        private Vector2 _value;
        private bool _isHeld;

        public Vector2 Value => _value;
        public bool IsHeld => _isHeld;

        public void Initialize(RectTransform root)
        {
            _root = root;
        }

        public void SetVisuals(RectTransform ring, RectTransform knob)
        {
            _knob = knob;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isHeld = true;
            UpdateValue(eventData);
        }
        public void OnDrag(PointerEventData eventData) => UpdateValue(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            _isHeld = false;
            _value = Vector2.zero;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }

        private void UpdateValue(PointerEventData eventData)
        {
            if (_root == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Vector2 half = _root.rect.size * 0.5f;
                _value = Vector2.ClampMagnitude(new Vector2(
                    local.x / Mathf.Max(1f, half.x), local.y / Mathf.Max(1f, half.y)), 1f);
                if (_knob != null) _knob.anchoredPosition = _value * Mathf.Min(half.x, half.y) * 0.46f;
            }
        }
    }
}
