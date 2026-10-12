using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRPractice
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class InteractableAudio : MonoBehaviour
    {
        public AudioSource feedbackSource;
        public AudioSource loopSource;
        public AudioClip grabClip, releaseClip, switchClip;
        public bool toggleOnActivate;
        private XRGrabInteractable interactable;

        private void OnEnable()
        {
            interactable = GetComponent<XRGrabInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            interactable.activated.AddListener(OnActivate);
        }
        private void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
            interactable.activated.RemoveListener(OnActivate);
        }
        private void OnGrab(SelectEnterEventArgs _) => Grabbed();
        private void OnRelease(SelectExitEventArgs _) => Released();
        private void OnActivate(ActivateEventArgs _) => Activate();
        public void Grabbed() { if (feedbackSource && grabClip) feedbackSource.PlayOneShot(grabClip); }
        public void Released() { if (feedbackSource && releaseClip) feedbackSource.PlayOneShot(releaseClip); }
        public void Activate()
        {
            if (!toggleOnActivate || !loopSource) return;
            if (feedbackSource && switchClip) feedbackSource.PlayOneShot(switchClip);
            if (loopSource.isPlaying) loopSource.Stop(); else loopSource.Play();
        }
    }
}
