using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.XR.Interaction.Toolkit;
using VRPractice.Editor;
using Object = UnityEngine.Object;

namespace VRPractice.Tests
{
    public sealed class WorkshopTests
    {
        [TestCase(0, -80)]
        [TestCase(1, 0)]
        [TestCase(.5f, -6.0206f)]
        public void SliderConversionIsFiniteAndCorrect(float input,float expected)
            => Assert.That(WorkshopMenu.LinearToDecibels(input),Is.EqualTo(expected).Within(.001f));

        [Test]
        public void StrongImpactIsLouderAndClamped()
        {
            Assert.That(ImpactSound.VolumeForSpeed(.1f,.3f,5),Is.Zero);
            Assert.That(ImpactSound.VolumeForSpeed(1,.3f,5),Is.LessThan(ImpactSound.VolumeForSpeed(4,.3f,5)));
            Assert.That(ImpactSound.VolumeForSpeed(10,.3f,5),Is.EqualTo(1));
        }

        [Test]
        public void BuiltScenesHaveOneListenerAndSafeGrabs()
        {
            WorkshopBuilder.ValidateScenes();
            Assert.That(Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).Length,Is.GreaterThanOrEqualTo(6));
            foreach(var grab in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
                foreach(var collider in grab.GetComponentsInChildren<MeshCollider>()) Assert.That(collider.convex,Is.True);
        }

        [Test]
        public void RadioStaysSpatialAndToggleableWhenHeld()
        {
            EditorSceneManager.OpenScene(WorkshopBuilder.VariantScene);
            var radio=GameObject.Find("Radio");var feedback=radio.GetComponent<InteractableAudio>();
            Assert.That(feedback.toggleOnActivate,Is.True);
            Assert.That(feedback.loopSource.transform,Is.EqualTo(radio.transform));
            Assert.That(feedback.loopSource.spatialBlend,Is.EqualTo(1));
            Assert.That(feedback.loopSource.minDistance,Is.EqualTo(1));
            Assert.That(feedback.loopSource.maxDistance,Is.EqualTo(10));
            Assert.That(feedback.loopSource.rolloffMode,Is.EqualTo(AudioRolloffMode.Logarithmic));
        }

        [Test]
        public void MonoImportAndStreamingMusicAreConfigured()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Audio"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                if(path.Contains("/Music/")) Assert.That(importer.defaultSampleSettings.loadType,Is.EqualTo(AudioClipLoadType.Streaming));
                else Assert.That(importer.forceToMono,Is.True,path);
            }
        }

        [Test]
        public void FanIsGenericLoopingAndHasAController()
        {
            EditorSceneManager.OpenScene(WorkshopBuilder.VariantScene);
            var animator=GameObject.Find("Fan").GetComponentInChildren<Animator>();
            Assert.That(animator.runtimeAnimatorController,Is.Not.Null);
            var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Models/Fan/Fan.fbx");
            Assert.That(importer.animationType,Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(importer.clipAnimations.All(x=>x.loopTime),Is.True);
        }

        [Test]
        public void PauseSnapshotsAndThreeExposedGroupsExist()
        {
            var mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/MainMixer.mixer");
            Assert.That(mixer.FindSnapshot("Normal"),Is.Not.Null);Assert.That(mixer.FindSnapshot("Paused"),Is.Not.Null);
            foreach(string name in new[]{"Music","SFX","Ambient"})
            {
                Assert.That(mixer.FindMatchingGroups(name).Any(x=>x.name==name),Is.True);
                Assert.That(mixer.GetFloat(name+"Volume",out _),Is.True);
            }
        }

        [Test]
        public void VariantHasEngineAndCustomAmbientCutoff()
        {
            EditorSceneManager.OpenScene(WorkshopBuilder.VariantScene);
            Assert.That(GameObject.Find("Car").GetComponent<AudioSwitch>().loopSource.clip.name,Is.EqualTo("EngineLoop"));
            var fan=GameObject.Find("Fan").GetComponent<AudioSource>();
            Assert.That(fan.rolloffMode,Is.EqualTo(AudioRolloffMode.Custom));Assert.That(fan.maxDistance,Is.EqualTo(4));
            Assert.That(fan.spread,Is.EqualTo(90));
            Assert.That(fan.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(1),Is.Zero);
            var moving=Object.FindFirstObjectByType<OrbitingSource>();Assert.That(moving.speed,Is.InRange(8,10));
            Assert.That(moving.radius,Is.EqualTo(5));Assert.That(moving.GetComponent<AudioSource>().dopplerLevel,Is.EqualTo(1));
        }
    }
}
