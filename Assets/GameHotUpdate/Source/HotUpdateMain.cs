namespace HotUpdate
{
    public static class HotUpdateMain
    {
        public static void Main()
        {
            UnityEngine.Debug.Log("<color=red>Hello HybridCLR , 热更成功 ;</color>");
        }

        /// <summary>
        /// Production-like Publisher consumer entry used by the separate business YooAsset package.
        /// Keeping this separate from Main preserves the verification-only package's existing contract.
        /// </summary>
        public static void PublisherConsumerE2E()
        {
            using (StellarFramework.Res.ResScope scope =
                   StellarFramework.Res.ResKit.CreateCustomScope("YooAsset", "HotUpdatePublisherConsumerE2E"))
            {
                UnityEngine.TextAsset behavior = scope.Load<UnityEngine.TextAsset>("HotUpdateBehavior");
                if (behavior == null || string.IsNullOrWhiteSpace(behavior.text))
                {
                    throw new System.InvalidOperationException(
                        "The Publisher Consumer E2E behavior asset is missing or empty.");
                }

                string behaviorValue = behavior.text.Trim();
                UnityEngine.Debug.Log("[HotUpdatePublisherConsumerE2E] behavior=" + behaviorValue);
            }

            UnityEngine.Debug.Log("<color=red>Hello HybridCLR , 热更成功 ;</color>");
        }
    }
}
