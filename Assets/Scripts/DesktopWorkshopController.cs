using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace VRPractice
{
    // Optional keyboard fallback: actual VR grabs still use XRGrabInteractable.
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopWorkshopController : MonoBehaviour
    {
        public Camera head;
        public WorkshopMenu menu;
        public XRDeviceSimulator simulator;
        public GameObject controllerRoot;
        public UnityEngine.InputSystem.XR.TrackedPoseDriver headTracking;
        public float speed = 2.5f;
        private CharacterController character;
        private Rigidbody held;
        private InteractableAudio heldAudio;
        private bool originalKinematic;
        private CollisionDetectionMode originalCollisionMode;
        private float yaw, pitch, verticalSpeed;
        private bool simulationMode;
        private bool hardwareMode;

        private void Start()
        {
            character = GetComponent<CharacterController>();
            var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances(displays);
            hardwareMode = displays.Exists(x => x.running);
            controllerRoot.SetActive(hardwareMode);
            if (headTracking) headTracking.enabled = hardwareMode;
            if (hardwareMode) enabled = false;
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f2Key.wasPressedThisFrame && !hardwareMode)
            {
                Release(false);
                simulationMode = !simulationMode;
                if (simulator) simulator.gameObject.SetActive(simulationMode);
                controllerRoot.SetActive(simulationMode);
                if (headTracking) headTracking.enabled = simulationMode;
                if (!simulationMode) { head.transform.localPosition = new Vector3(0, 1.7f, 0); head.transform.localRotation = Quaternion.identity; }
            }
            if (simulationMode || menu.IsOpen) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * .12f;
                yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y, -80, 80);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                head.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed) direction += transform.forward;
            if (keyboard.sKey.isPressed) direction -= transform.forward;
            if (keyboard.dKey.isPressed) direction += transform.right;
            if (keyboard.aKey.isPressed) direction -= transform.right;
            verticalSpeed = character.isGrounded ? -1 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            character.Move((direction.normalized * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (held) Release(false);
                else if (Physics.Raycast(head.transform.position, head.transform.forward, out RaycastHit hit, 3))
                {
                    var grab = hit.collider.GetComponentInParent<XRGrabInteractable>();
                    if (grab && !grab.isSelected)
                    {
                        held = grab.GetComponent<Rigidbody>();
                        originalKinematic = held.isKinematic;
                        originalCollisionMode = held.collisionDetectionMode;
                        held.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        held.isKinematic = true;
                        heldAudio = grab.GetComponent<InteractableAudio>();
                        heldAudio?.Grabbed();
                    }
                }
            }
            if (keyboard.qKey.wasPressedThisFrame) Release(true);
            if (keyboard.fKey.wasPressedThisFrame)
            {
                if (held) heldAudio?.Activate();
                else if (Physics.Raycast(head.transform.position, head.transform.forward, out RaycastHit hit, 3))
                {
                    hit.collider.GetComponentInParent<AudioSwitch>()?.Toggle();
                    hit.collider.GetComponentInParent<InteractableAudio>()?.Activate();
                }
            }
        }
        private void FixedUpdate()
        {
            if (!held) return;
            Vector3 target = head.transform.position + head.transform.forward * .85f - head.transform.up * .18f;
            held.MovePosition(target); held.MoveRotation(head.transform.rotation);
        }
        private void Release(bool throwObject)
        {
            if (!held) return;
            heldAudio?.Released();
            held.isKinematic = originalKinematic;
            held.collisionDetectionMode = originalCollisionMode;
            if (!originalKinematic) held.velocity = throwObject ? head.transform.forward * 5 : Vector3.zero;
            held = null; heldAudio = null;
        }
        private void OnGUI()
        {
            if (menu && menu.IsOpen) return;
            GUI.color = new Color(.02f, .06f, .09f, .92f);
            GUI.DrawTexture(new Rect(18,18,540,82), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(32,25,510,24), "VR PRACTICE / 06     •     АВТОМАСТЕРСКАЯ");
            GUI.Label(new Rect(32,49,510,22), "WASD — ходить  |  ПКМ — взгляд  |  E — взять/отпустить  |  F — включить");
            GUI.Label(new Rect(32,71,510,22), "Q — бросить  |  Esc — микшер  |  F1 — куб 1 м  |  F2 — XR Simulator");
            GUI.Label(new Rect(Screen.width/2f-5,Screen.height/2f-10,20,20), "+");
            GUI.Label(new Rect(24,Screen.height-35,700,24), simulationMode ? "XR Simulator: Tab — устройство; RMB — вращение; G — захват; LMB — активация" : "Андижан Мухаммед  •  Вариант 4  •  Проверяйте пространственный звук в наушниках");
        }
    }
}
