using UnityEngine;

namespace StellarFramework
{
    /// <summary>TimeKit 唯一的 Unity PlayerLoop 驱动器。</summary>
    internal sealed class TimeKitDriver : MonoBehaviour
    {
        private const string DriverObjectName = "[StellarFramework.TimeKit]";
        private static TimeKitDriver _instance;

        internal static void EnsureCreated()
        {
            if (_instance != null)
            {
                return;
            }

            TimeKitDriver existing = Object.FindObjectOfType<TimeKitDriver>();
            if (existing != null)
            {
                _instance = existing;
                TimeKit.SetDriver(existing);
                return;
            }

            var driverObject = new GameObject(DriverObjectName);
            DontDestroyOnLoad(driverObject);
            _instance = driverObject.AddComponent<TimeKitDriver>();
            TimeKit.SetDriver(_instance);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            TimeKit.SetDriver(this);
        }

        private void Update()
        {
            TimeKit.InternalUpdate(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                TimeKit.SetDriver(null);
            }
        }
    }
}
