using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRPractice
{
    // Static engine/air compressor can be selected by a ray, then activated.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class AudioSwitch : MonoBehaviour
    {
        public AudioSource loopSource;
        public AudioSource feedbackSource;
        public AudioClip switchClip;
        private XRSimpleInteractable interactable;
        private void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.activated.AddListener(OnActivate);
        }
        private void OnDisable() => interactable.activated.RemoveListener(OnActivate);
        private void OnActivate(ActivateEventArgs _) => Toggle();
        public void Toggle()
        {
            if (!loopSource) return;
            if (loopSource.isPlaying) loopSource.Stop(); else loopSource.Play();
            if (feedbackSource && switchClip) feedbackSource.PlayOneShot(switchClip);
        }
    }
}
