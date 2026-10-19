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
