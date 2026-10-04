using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StellarFramework.Res
{
    /// <summary>
    /// Optional provider for updating resource content. A provider may be registered without
    /// replacing the ResKit loader used to load assets.
    /// </summary>
    public interface IResContentUpdateProvider<TOptions, TProgress, TResult>
    {
        UniTask<TResult> UpdateAsync(
            TOptions options,
            IProgress<TProgress> progress = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Optional code runtime provider. The application supplies a ResScope, so the runtime
    /// provider can load its code payload through whichever ResKit backend the project chose.
    /// </summary>
    public interface IResCodeUpdateProvider<TOptions, TResult>
    {
        UniTask<TResult> RunAsync(
            TOptions options,
            ResScope resources,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }

    public static partial class ResKit
    {
        private static readonly Dictionary<string, object> ContentUpdateProviders =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private static readonly Dictionary<string, object> CodeUpdateProviders =
            new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>Registers or removes a content update provider independently of resource loaders.</summary>
        public static void RegisterContentUpdateProvider<TOptions, TProgress, TResult>(
            string providerId,
            IResContentUpdateProvider<TOptions, TProgress, TResult> provider)
        {
            RegisterProvider(ContentUpdateProviders, providerId, provider);
        }

        /// <summary>Gets a registered content update provider by its stable provider ID.</summary>
        public static IResContentUpdateProvider<TOptions, TProgress, TResult>
            GetContentUpdateProvider<TOptions, TProgress, TResult>(string providerId)
        {
            return GetProvider<IResContentUpdateProvider<TOptions, TProgress, TResult>>(
                ContentUpdateProviders, "content update", providerId);
        }

        /// <summary>Registers or removes a code runtime provider independently of content updater choice.</summary>
        public static void RegisterCodeUpdateProvider<TOptions, TResult>(
            string providerId,
            IResCodeUpdateProvider<TOptions, TResult> provider)
        {
            RegisterProvider(CodeUpdateProviders, providerId, provider);
        }

        /// <summary>Gets a registered code runtime provider by its stable provider ID.</summary>
        public static IResCodeUpdateProvider<TOptions, TResult> GetCodeUpdateProvider<TOptions, TResult>(
            string providerId)
        {
            return GetProvider<IResCodeUpdateProvider<TOptions, TResult>>(
                CodeUpdateProviders, "code update", providerId);
        }

        private static void RegisterProvider<TProvider>(
            IDictionary<string, object> registry,
            string providerId,
            TProvider provider)
            where TProvider : class
        {
            string key = NormalizeCustomKey(providerId);
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
            }

            if (provider == null)
            {
                registry.Remove(key);
                return;
            }

            registry[key] = provider;
        }

        private static TProvider GetProvider<TProvider>(
            IDictionary<string, object> registry,
            string providerKind,
            string providerId)
            where TProvider : class
        {
            string key = NormalizeCustomKey(providerId);
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));
            }

            if (!registry.TryGetValue(key, out object provider))
            {
                throw new InvalidOperationException(
                    $"ResKit {providerKind} provider '{key}' is not registered. Register its adapter before use.");
            }

            if (!(provider is TProvider typedProvider))
            {
                throw new InvalidOperationException(
                    $"ResKit {providerKind} provider '{key}' was registered with incompatible option/result types.");
            }

            return typedProvider;
        }
    }
}
