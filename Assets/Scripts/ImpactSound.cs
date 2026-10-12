using UnityEngine;

namespace VRPractice
{
    // A louder collision produces a louder sound; cooldown rejects contact chatter.
    [RequireComponent(typeof(AudioSource), typeof(Rigidbody))]
    public sealed class ImpactSound : MonoBehaviour
    {
        public AudioClip[] clips;
        public float minSpeed = 0.3f;
        public float maxSpeed = 5f;
        public float cooldown = 0.1f;
        private AudioSource source;
        private float lastPlayTime = float.NegativeInfinity;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1;
        }

        public static float VolumeForSpeed(float speed, float minimum, float maximum)
            => Mathf.InverseLerp(minimum, Mathf.Max(minimum + 0.01f, maximum), speed);

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (clips == null || clips.Length == 0 || speed < minSpeed || Time.time - lastPlayTime < cooldown) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            lastPlayTime = Time.time;
            source.pitch = Random.Range(0.9f, 1.1f);
            source.PlayOneShot(clip, VolumeForSpeed(speed, minSpeed, maxSpeed));
        }
    }
}
