using UnityEngine;

namespace VRPractice.Profiling
{
    // Cleanup is outside the measured interval; the required BadBehaviour stays untouched.
    public sealed class StressObjectLifetime : MonoBehaviour
    {
        public Material original;
        private void OnDestroy()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer && renderer.sharedMaterial && renderer.sharedMaterial != original)
                Destroy(renderer.sharedMaterial);
        }
    }
}
