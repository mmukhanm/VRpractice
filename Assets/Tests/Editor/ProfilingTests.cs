using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRPractice.Editor;
using VRPractice.Profiling;
using Object = UnityEngine.Object;

namespace VRPractice.Tests
{
    public sealed class ProfilingTests
    {
        [TestCase(90f)]
        [TestCase(-135f)]
        public void CheckpointMovesOffsetHeadAndMatchesHorizontalGaze(float heading)
        {
            var rig = new GameObject("Checkpoint test rig");
            var target = new GameObject("Checkpoint test target");
            try
            {
                var head = new GameObject("Tracked head");
                head.transform.SetParent(rig.transform, false);
                head.transform.localPosition = new Vector3(.6f, 1.7f, -.4f);
                head.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
                var origin = rig.AddComponent<Unity.XR.CoreUtils.XROrigin>();
                origin.Camera = head.AddComponent<Camera>();
                target.transform.SetPositionAndRotation(new Vector3(4f, 0f, 7f),
                    Quaternion.Euler(0f, heading, 0f));
                var checkpoints = rig.AddComponent<CameraCheckpoints>();
                checkpoints.origin = origin;
                checkpoints.points = new[] { target.transform };

                checkpoints.GoTo(1);

                Assert.That(Vector3.Distance(head.transform.position,
                    target.transform.position + Vector3.up * 1.7f), Is.LessThan(.001f));
                Assert.That(Vector3.Angle(Vector3.ProjectOnPlane(head.transform.forward, Vector3.up),
                    target.transform.forward), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(rig); Object.DestroyImmediate(target); }
        }

        [Test]
        public void StressGridIsRepeatableAndInsideTwentyMetreZone()
        {
            var center = new Vector3(0,0,12); var size = new Vector2(20,20);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(2000));
            for(int i=0;i<2000;i++)
            {
                Vector3 p=StressSpawner.PositionFor(i,columns,center,size);
                Assert.That(p.x,Is.InRange(-10,10));Assert.That(p.z,Is.InRange(2,22));
                Assert.That(p,Is.EqualTo(StressSpawner.PositionFor(i,columns,center,size)));
            }
        }

        [Test]
        public void GoodBehaviourPreservesLiteralStickyRedWithoutMaterialCloning()
        {
            var player=new GameObject("XR Origin (XR Rig)");
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer=obj.GetComponent<Renderer>(); Material original=renderer.sharedMaterial;
            try
            {
                var good=obj.AddComponent<GoodBehaviour>();good.Initialize(player.transform);
                obj.transform.position=Vector3.one*.1f;good.CheckDistance();
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor(Shader.PropertyToID("_BaseColor")),Is.EqualTo(Color.red));
                obj.transform.position=Vector3.one*10;good.CheckDistance();renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor(Shader.PropertyToID("_BaseColor")),Is.EqualTo(Color.red));
                Assert.That(renderer.sharedMaterial,Is.SameAs(original));
            }
            finally { Object.DestroyImmediate(obj);Object.DestroyImmediate(player); }
        }

        [Test]
        public void GeneratedSceneSupportsLiteralBadScriptAndAllExperiments()
        {
            ProfilingSceneBuilder.Validate();
            var stress=Object.FindFirstObjectByType<StressSpawner>();
            Assert.That(stress.prefab.GetComponent<Renderer>(),Is.Not.Null);
            Assert.That(stress.prefab.GetComponent<LODGroup>().lodCount,Is.EqualTo(3));
            Assert.That(stress.zoneSize,Is.EqualTo(new Vector2(20,20)));
            var points=Object.FindFirstObjectByType<CameraCheckpoints>();
            Assert.That(points.points.Select(p=>p.name),Is.EqualTo(new[]{"CP1","CP2","CP3"}));
            Assert.That(Object.FindFirstObjectByType<ProfilingCapture>().frameCount,Is.GreaterThanOrEqualTo(300));
        }
    }
}
