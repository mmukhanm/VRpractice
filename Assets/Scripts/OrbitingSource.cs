using UnityEngine;

namespace VRPractice
{
    // Rigidbody.MovePosition supplies physical velocity for Unity's Doppler effect.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class OrbitingSource : MonoBehaviour
    {
        public Vector3 center = new Vector3(0, 2.6f, 0);
        public float radius = 5;
        public float speed = 9;
        private Rigidbody body;
        private float angle;
        private void Awake() => body = GetComponent<Rigidbody>();
        private void FixedUpdate()
        {
            angle += speed / Mathf.Max(0.01f, radius) * Time.fixedDeltaTime;
            body.MovePosition(center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            body.MoveRotation(Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle))));
        }
    }
}
