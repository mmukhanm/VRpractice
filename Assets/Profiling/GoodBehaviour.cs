using Unity.Profiling;
using UnityEngine;

namespace VRPractice.Profiling
{
    [RequireComponent(typeof(Renderer))]
    public sealed class GoodBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("GoodBehaviour.CheckDistance");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Transform player;
        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;
        private bool wasNear;

        private void Awake()
        {
            var found = GameObject.Find("XR Origin (XR Rig)");
            Initialize(found ? found.transform : null);
        }

        public void Initialize(Transform target)
        {
            player = target;
            cachedRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock(); // Once, before the measured steady state.
            cachedRenderer.GetPropertyBlock(block);
        }

        private void Update() => CheckDistance();

        public void CheckDistance()
        {
            using (Marker.Auto())
            {
                if (!player) return;
                bool near = (transform.position - player.position).sqrMagnitude < 9f;
                if (near && !wasNear)
                {
                    block.SetColor(BaseColor, Color.red);
                    block.SetColor(ColorId, Color.red);
                    cachedRenderer.SetPropertyBlock(block);
                }
                // Literal BadBehaviour never restores the original colour on leaving.
                wasNear = near;
            }
        }
    }
}
