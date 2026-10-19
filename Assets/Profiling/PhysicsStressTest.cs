using System.Collections.Generic;
using UnityEngine;

namespace VRPractice.Profiling
{
    public sealed class PhysicsStressTest : MonoBehaviour
    {
        public Mesh mesh;
        public Mesh convexHull;
        public Material material;
        private readonly List<Rigidbody> bodies = new List<Rigidbody>(300);
        public string ColliderCase { get; private set; } = "None";
        public int BodyCount => bodies.Count;
        public int AwakeBodies
        {
            get { int count = 0; foreach (var body in bodies) if (body && !body.IsSleeping()) count++; return count; }
        }
        public string Phase => AwakeBodies == 0 ? "Sleeping" : "Falling";

        public void Spawn(bool convex)
        {
            Clear(); ColliderCase = convex ? "Convex" : "Box";
            for (int i = 0; i < 300; i++)
            {
                var obj = new GameObject("Physics_" + i);
                obj.transform.SetParent(transform, false);
                obj.transform.position = new Vector3((i % 20 - 9.5f) * .8f, 6 + (i / 20) * .4f, 5 + (i / 20) * .8f);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (convex) { var collider = obj.AddComponent<MeshCollider>(); collider.sharedMesh = convexHull; collider.convex = true; }
                else { var collider = obj.AddComponent<BoxCollider>(); collider.center = mesh.bounds.center; collider.size = mesh.bounds.size; }
                var body = obj.AddComponent<Rigidbody>(); body.mass = 1;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                bodies.Add(body);
            }
        }
        public void Clear()
        {
            foreach (var body in bodies) if (body) { body.gameObject.SetActive(false); Destroy(body.gameObject); }
            bodies.Clear(); ColliderCase = "None";
        }
    }
}
