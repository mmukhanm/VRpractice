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
            if (!origin || !origin.Camera || points == null || index < 1 || index > points.Length) return;
            var point = points[index - 1];
            if (!point) return;
            var controller = origin.GetComponent<CharacterController>();
            var desktop = origin.GetComponent<DesktopWorkshopController>();
            desktop?.PrepareForTeleport();
            if (desktop && desktop.enabled && (!desktop.headTracking || !desktop.headTracking.enabled))
                desktop.head.transform.localRotation = Quaternion.identity;
            bool wasEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            float height = Mathf.Max(.1f, origin.Camera.transform.position.y - origin.transform.position.y);
            // Rotate around the tracked head so its room-scale offset does not move it.
            var head = origin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            var targetForward = Vector3.ProjectOnPlane(point.forward, Vector3.up);
            if (forward.sqrMagnitude > .0001f && targetForward.sqrMagnitude > .0001f)
                origin.transform.RotateAround(head.position, Vector3.up,
                    Vector3.SignedAngle(forward, targetForward, Vector3.up));
            origin.transform.position += point.position + Vector3.up * height - head.position;
            if (controller) controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            desktop?.SynchronizeViewRotation();
            Current = index;
        }
    }
}
