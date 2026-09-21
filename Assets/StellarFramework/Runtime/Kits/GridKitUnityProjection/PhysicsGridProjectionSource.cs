using UnityEngine;

namespace StellarFramework.GridKit.UnityProjection
{
    public sealed class PhysicsGridProjectionSource :
        IGridProjectionSource
    {
        private readonly int _surfaceMask;
        private readonly int _obstacleMask;
        private readonly QueryTriggerInteraction
            _triggerInteraction;

        public PhysicsGridProjectionSource(
            int surfaceLayerMask,
            int obstacleLayerMask = 0,
            QueryTriggerInteraction triggerInteraction =
                QueryTriggerInteraction.Ignore)
        {
            _surfaceMask = surfaceLayerMask;
            _obstacleMask = obstacleLayerMask;
            _triggerInteraction = triggerInteraction;
        }

        public bool TrySample(
            in GridProjectionQuery query,
            out GridProjectionSample sample)
        {
            int combinedMask =
                _surfaceMask | _obstacleMask;
            if (combinedMask == 0)
            {
                sample = default(GridProjectionSample);
                return false;
            }

            if (!Physics.Raycast(
                    query.SampleOrigin,
                    query.SampleDirection,
                    out RaycastHit hit,
                    query.MaxDistance,
                    combinedMask,
                    _triggerInteraction))
            {
                sample = GridProjectionSample.Missing();
                return true;
            }

            int layerBit =
                1 << hit.collider.gameObject.layer;
            bool obstacle =
                (_obstacleMask & layerBit) != 0;
            sample =
                GridProjectionSample.Surface(
                    hit.point,
                    hit.normal,
                    hit.point.y,
                    Vector3.Angle(
                        hit.normal,
                        Vector3.up),
                    hit.collider.gameObject.layer,
                    obstacle);
            return true;
        }
    }
}
