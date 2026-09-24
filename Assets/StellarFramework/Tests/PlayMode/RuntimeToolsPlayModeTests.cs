using System.Collections;
using NUnit.Framework;
using StellarFramework.RuntimeTools;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    public sealed class RuntimeToolsPlayModeTests
    {
        [UnityTest]
        public IEnumerator TriggerRelayForwardsEnterAndExit()
        {
            GameObject triggerObject = null;
            GameObject moverObject = null;
            try
            {
                triggerObject = new GameObject("TriggerRelay-Test-Trigger");
                BoxCollider triggerCollider = triggerObject.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.size = Vector3.one * 2f;
                TriggerRelay relay = triggerObject.AddComponent<TriggerRelay>();

                moverObject = new GameObject("TriggerRelay-Test-Mover");
                moverObject.transform.position = new Vector3(5f, 0f, 0f);
                moverObject.AddComponent<BoxCollider>();
                Rigidbody body = moverObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = true;

                int entered = 0;
                int exited = 0;
                relay.Entered += _ => entered++;
                relay.Exited += _ => exited++;

                yield return new WaitForFixedUpdate();
                body.position = Vector3.zero;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                Assert.That(entered, Is.EqualTo(1));

                body.position = new Vector3(5f, 0f, 0f);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                Assert.That(exited, Is.EqualTo(1));
            }
            finally
            {
                if (moverObject != null) Object.Destroy(moverObject);
                if (triggerObject != null) Object.Destroy(triggerObject);
            }
        }

        [UnityTest]
        public IEnumerator CollisionRelayForwardsCollisionEnter()
        {
            GameObject receiverObject = null;
            GameObject moverObject = null;
            try
            {
                receiverObject = new GameObject("CollisionRelay-Test-Receiver");
                receiverObject.AddComponent<BoxCollider>();
                CollisionRelay relay = receiverObject.AddComponent<CollisionRelay>();

                moverObject = new GameObject("CollisionRelay-Test-Mover");
                moverObject.transform.position = new Vector3(3f, 0f, 0f);
                moverObject.AddComponent<BoxCollider>();
                Rigidbody body = moverObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                int entered = 0;
                relay.Entered += _ => entered++;

                body.velocity = Vector3.left * 12f;
                for (int i = 0; i < 20 && entered == 0; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(entered, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                if (moverObject != null) Object.Destroy(moverObject);
                if (receiverObject != null) Object.Destroy(receiverObject);
            }
        }
    }
}
