using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    public sealed class TimeKitPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimeKitUsesUnscaledTimeAndExplicitPause()
        {
            TimeKit.Reset(new GameDateTime(1, 1, 1));
            TimeKit.Resume();
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            long before = TimeKit.Tick;
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(TimeKit.Tick, Is.GreaterThan(before));

            TimeKit.Pause();
            long pausedTick = TimeKit.Tick;
            yield return new WaitForSecondsRealtime(0.04f);
            Assert.That(TimeKit.Tick, Is.EqualTo(pausedTick));
            TimeKit.Resume();
            Time.timeScale = previousScale;
        }
    }
}
