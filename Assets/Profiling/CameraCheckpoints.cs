using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

namespace VRPractice.Profiling
{
    public sealed class CameraCheckpoints : MonoBehaviour
    {
        public XROrigin origin;
        public Transform[] points;
        public ProfilingCapture capture;
        public int Current { get; private set; } = 1;

        private void Update()
        {
            if (capture && capture.IsCapturing) return;
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.digit1Key.wasPressedThisFrame) GoTo(1);
            if (keys.digit2Key.wasPressedThisFrame) GoTo(2);
            if (keys.digit3Key.wasPressedThisFrame) GoTo(3);
        }

        public void GoTo(int index)
        {
            if (!origin || points == null || index < 1 || index > points.Length) return;
            var point = points[index - 1];
            var controller = origin.GetComponent<CharacterController>();
            var desktop = origin.GetComponent<DesktopWorkshopController>();
            desktop?.PrepareForTeleport();
            if (desktop && desktop.enabled && (!desktop.headTracking || !desktop.headTracking.enabled))
                desktop.head.transform.localRotation = Quaternion.identity;
            bool wasEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            float height = Mathf.Max(.1f, origin.Camera.transform.position.y - origin.transform.position.y);
            origin.MatchOriginUpCameraForward(Vector3.up, point.forward);
            origin.MoveCameraToWorldLocation(point.position + Vector3.up * height);
            if (controller) controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            desktop?.SynchronizeViewRotation();
            Current = index;
        }
    }
}
