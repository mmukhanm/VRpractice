using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace VRPractice.Editor
{
    // Builds scenes from imported files, so a fresh clone does not need opaque hand-edited YAML.
    // It only regenerates Assets/Generated and the two scene files when explicitly requested.
    [InitializeOnLoad]
    public static class WorkshopBuilder
    {
        private const string Generated = "Assets/Generated";
        private const string SetupVersion = "Unity 6000.6.0f1 / URP 17.6.0 / XRI 2.6.5 / Profiling 1";
        public const string CommonScene = "Assets/Scenes/Import_Audio_Андижан.unity";
        public const string VariantScene = "Assets/Scenes/Variant_Андижан.unity";
        private static AudioMixer mixer;
        private static Material floorMat, wallMat, yellowMat, metalMat, blueMat;
        private static Font font;
        private static InputActionAsset actions;

        static WorkshopBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(Generated + "/SetupComplete.txt"))
                {
                    if (!File.ReadAllText(Generated + "/SetupComplete.txt").Contains(SetupVersion))
                        Debug.Log("VR Practice: project upgraded to " + SetupVersion + ". Save your scene changes, then run VR Practice/Build workshop scenes to refresh generated assets.");
                    return; // Never overwrite existing user scenes automatically during an upgrade.
                }
                try { Build(); }
                catch (Exception error) { Debug.LogException(error); }
            };
        }

        [MenuItem("VR Practice/Build workshop scenes")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
            // Preserve an existing unsaved scene before an explicit rebuild.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            PlayerSettings.companyName = "VRpractice";
            PlayerSettings.productName = "Мастерская — Практическая работа 6";
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First());
            var inputHandling = playerSettings.FindProperty("activeInputHandler");
            if (inputHandling != null) { inputHandling.intValue = 1; playerSettings.ApplyModifiedPropertiesWithoutUndo(); }
            SetupPipeline();
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/MainMixer.mixer");
            if (!mixer || !mixer.FindSnapshot("Normal") || !mixer.FindSnapshot("Paused"))
                throw new InvalidOperationException("MainMixer or its snapshots could not be imported.");
            foreach (string parameter in new[] { "MusicVolume", "SFXVolume", "AmbientVolume" })
                if (!mixer.GetFloat(parameter, out _)) throw new InvalidOperationException("Missing exposed parameter: " + parameter);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            floorMat = Material("Concrete", new Color(.14f, .19f, .22f));
            wallMat = Material("Wall", new Color(.25f, .31f, .34f));
            yellowMat = Material("Safety yellow", new Color(.95f, .58f, .07f));
            metalMat = Material("Metal", new Color(.3f, .36f, .4f), .65f);
            blueMat = Material("Petrol blue", new Color(.045f, .28f, .4f), .3f);
            var package = PackageInfo.FindForAssembly(typeof(XRDeviceSimulator).Assembly);
            if (package == null) throw new InvalidOperationException("XR Interaction Toolkit is not installed.");
            // Copy samples to a local generated directory: Unity does not import Samples~ directly.
            CopySample(Path.Combine(package.resolvedPath, "Samples~/Starter Assets"), Generated + "/Starter Assets");
            CopySample(Path.Combine(package.resolvedPath, "Samples~/XR Device Simulator"), Generated + "/XR Device Simulator");
            AssetDatabase.Refresh();
            actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Generated + "/Starter Assets/XRI Default Input Actions.inputactions");
            if (!actions) throw new InvalidOperationException("Default XRI input actions failed to import.");
            ConfigureFanImport();
            CreateScene(CommonScene, false);
            CreateScene(VariantScene, true);
            if (!File.Exists(ProfilingSceneBuilder.ScenePath)) ProfilingSceneBuilder.BuildFromVariant();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(VariantScene, true), new EditorBuildSettingsScene(CommonScene, true), new EditorBuildSettingsScene(ProfilingSceneBuilder.ScenePath, true) };
            AssetDatabase.SaveAssets();
            File.WriteAllText(Generated + "/SetupComplete.txt", SetupVersion + "\nWorkshop built from committed source assets.\n");
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(VariantScene);
            Debug.Log("VR Practice: both scenes built. Run VR Practice/Validate scenes, then Play.");
        }

        private static void CopySample(string source, string destination)
        {
            if (Directory.Exists(destination)) return;
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(source)) CopySample(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static void SetupPipeline()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Generated + "/WorkshopRenderer.asset");
            if (!data)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, Generated + "/WorkshopRenderer.asset");
            }
            // URP's official asset creation also reloads default renderer shaders/resources.
            ResourceReloader.ReloadAllNullIn(data, UniversalRenderPipelineAsset.packagePath);
            EditorUtility.SetDirty(data);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated + "/WorkshopURP.asset");
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(data);
                pipeline.msaaSampleCount = 4;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                AssetDatabase.CreateAsset(pipeline, Generated + "/WorkshopURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
        }

        private static Material Material(string name, Color color, float metallic = 0)
        {
            string path = Generated + "/" + name.Replace(" ", "_") + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = name; AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color; mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", .35f);
            return mat;
        }

        private static void ConfigureFanImport()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Models/Fan/Fan.fbx");
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("Fan FBX has no animation take.");
            foreach (var clip in clips) { clip.name = "FanLoop"; clip.loopTime = true; clip.loopPose = false; }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void CreateScene(string path, bool variant)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.45f, .51f, .56f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.08f,.12f,.16f); RenderSettings.fogDensity = .012f;
            var sun = new GameObject("Workshop daylight");
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f; light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50,-25,0);
            sun.AddComponent<UniversalAdditionalLightData>();
            Box("Ground", new Vector3(0,-.1f,0), new Vector3(80,.2f,80), floorMat);
            Box("Room / back wall", new Vector3(0,1.7f,4.8f), new Vector3(12,3.4f,.18f), wallMat);
            Box("Room / west wall", new Vector3(-6,1.7f,0), new Vector3(.18f,3.4f,9.6f), wallMat);
            Box("Room / east wall", new Vector3(6,1.7f,0), new Vector3(.18f,3.4f,9.6f), wallMat);
            // Fourth wall has a door-sized passage to allow inside/outside comparisons.
            Box("Room / front wall left", new Vector3(-3.9f,1.7f,-4.8f), new Vector3(4.2f,3.4f,.18f), wallMat);
            Box("Room / front wall right", new Vector3(3.9f,1.7f,-4.8f), new Vector3(4.2f,3.4f,.18f), wallMat);
            Box("Room / door lintel", new Vector3(0,3.1f,-4.8f), new Vector3(3.6f,.6f,.18f), wallMat);
            Box("Room / ceiling", new Vector3(0,3.5f,0), new Vector3(12,.15f,9.6f), wallMat);
            for (int i=0;i<4;i++)
            {
                Box("Safety stripe", new Vector3(-5.85f+i*3.9f,.015f,0), new Vector3(.045f,.025f,8.7f), yellowMat);
                var fixture = new GameObject("Ceiling light"); fixture.transform.position = new Vector3(-4.5f+i*3,3.2f,1);
                var point=fixture.AddComponent<Light>();point.type=LightType.Point;point.range=6;point.intensity=1.2f;point.color=new Color(1,.92f,.78f);
            }
            var room = new GameObject("Room reverberation");
            var reverb = room.AddComponent<AudioReverbZone>(); reverb.reverbPreset = AudioReverbPreset.Room;
            reverb.minDistance = 3.5f; reverb.maxDistance = 5.5f;
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            var table = Model("Table", "Table.fbx", new Vector3(-3.4f,0,2.9f));
            AddBoxCollider(table); MakeStatic(table);
            var chair = Model("Chair", "Chair.obj", new Vector3(-3.5f,0,1.4f));
            AddChairLOD(chair);
            // A cheap convex collision hull avoids the 255-face limit of a render mesh.
            var convex = chair.AddComponent<MeshCollider>(); convex.sharedMesh = CubeMesh(); convex.convex = true;
            Bounds chairBounds=BoundsOf(chair); convex.sharedMesh = BoxHull(chairBounds, chair.transform);
            MakeGrab(chair, 3);
            var lamp=Model("Lamp", "Lamp.glb",new Vector3(-4,.9f,2.9f)); AddBoxCollider(lamp); MakeGrab(lamp,.4f);
            var radio=Model("Radio", "Radio.fbx",new Vector3(-3.15f,.9f,2.9f)); AddBoxCollider(radio); MakeGrab(radio,.8f);
            var radioAudio=Sound(radio,"RadioLoop","Ambient",true,1,10,AudioRolloffMode.Logarithmic);
            radio.GetComponent<InteractableAudio>().loopSource=radioAudio;
            radio.GetComponent<InteractableAudio>().toggleOnActivate=true;

            var fan=Model("Fan", "Fan.fbx",new Vector3(4.5f,0,3));AddBoxCollider(fan);
            var animator=fan.GetComponentInChildren<Animator>();
            if (!animator) animator=fan.transform.GetChild(0).gameObject.AddComponent<Animator>();
            string controllerPath=Generated+"/Fan.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Fan/Fan.fbx").OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));
                var state=controller.layers[0].stateMachine.AddState("FanLoop");state.motion=clip;
                controller.layers[0].stateMachine.defaultState=state;
            }
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var fanAudio=Sound(fan,"FanLoop","Ambient",true, .5f,4,AudioRolloffMode.Custom);
            fanAudio.spread=90;fanAudio.SetCustomCurve(AudioSourceCurveType.CustomRolloff,new AnimationCurve(new Keyframe(0,1),new Keyframe(.5f,.8f),new Keyframe(.85f,.5f),new Keyframe(1,0)));
            var drone=Model("Drone","Drone.glb",new Vector3(5,2.6f,0));
            var droneBody=drone.AddComponent<Rigidbody>();droneBody.isKinematic=true;droneBody.useGravity=false;droneBody.interpolation=RigidbodyInterpolation.Interpolate;
            drone.AddComponent<OrbitingSource>();
            var motor=Sound(drone,"MotorLoop","Ambient",true,1,12,AudioRolloffMode.Linear);motor.dopplerLevel=1;motor.velocityUpdateMode=AudioVelocityUpdateMode.Fixed;
            var cube=Box("Reference cube / 1 metre",new Vector3(4.5f,.5f,-2.8f),Vector3.one,yellowMat);
            Label("ЭТАЛОН 1 × 1 × 1 М\nF1 — скрыть",new Vector3(4.5f,1.3f,-2.8f),180,.009f);

            if (variant)
            {
                var car=Model("Car","Car.fbx",new Vector3(.8f,0,.8f));AddBoxCollider(car);MakeStatic(car);
                var engine=Sound(car,"EngineLoop","Ambient",false,1,12,AudioRolloffMode.Logarithmic);
                AddSwitch(car,engine);
                var compressor=Model("Compressor","Compressor.glb",new Vector3(4.5f,0,1));AddBoxCollider(compressor);MakeStatic(compressor);
                AddSwitch(compressor,Sound(compressor,"CompressorLoop","Ambient",false,.5f,6,AudioRolloffMode.Linear));
                for(int i=0;i<3;i++)
                {
                    var wrench=Model("Wrench","Wrench.fbx",new Vector3(-3.9f+i*.25f,.93f,2.55f));AddBoxCollider(wrench);MakeGrab(wrench,.35f);
                }
                Label("04 / АВТОМАСТЕРСКАЯ\nАНДИЖАН МУХАММЕД",new Vector3(0,2.7f,4.65f),180,.015f);
                Label("ДВИГАТЕЛЬ\nВыберите лучом + курок\nПК: наведитесь и нажмите F",new Vector3(.8f,1.9f,1),180,.009f);
                Label("ИНСТРУМЕНТЫ\nСильнее удар → громче звук",new Vector3(-3.5f,1.55f,3.7f),180,.009f);
            }
            else Label("06 / ИМПОРТ И SPATIAL AUDIO\nАНДИЖАН МУХАММЕД",new Vector3(0,2.7f,4.65f),180,.015f);
            var music=new GameObject("2D background music");
            var background=music.AddComponent<AudioSource>();background.clip=Clip("Music","WorkshopMusic");background.loop=true;background.playOnAwake=true;
            background.spatialBlend=0;background.volume=.35f;background.outputAudioMixerGroup=Group("Music");
            SetupPlayer(cube);
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),path);
        }

        private static GameObject Model(string name,string filename,Vector3 position)
        {
            string path="Assets/Models/"+name+"/"+filename;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Model import failed: "+path);
            var root=new GameObject(name);root.transform.position=position;
            var child=Object.Instantiate(prefab,root.transform);child.name=name+" imported mesh";
            child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one;
            foreach(var renderer in child.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>
                {
                    if (!m) return metalMat;
                    // Imported materials become separate URP assets, without mutating source meshes.
                    string matName=name+"_"+m.name;
                    var mat=Material(matName,m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.color:Color.gray);
                    Texture texture=m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):m.HasProperty("_MainTex")?m.GetTexture("_MainTex"):null;
                    if (texture) mat.SetTexture("_BaseMap",texture);
                    Texture normal=m.HasProperty("_BumpMap")?m.GetTexture("_BumpMap"):null;
                    if (normal) { mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP"); }
                    return mat;
                }).ToArray();
            }
            return root;
        }
        private static Bounds BoundsOf(GameObject obj)
        {
            var renderers=obj.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new InvalidOperationException("No mesh renderers on "+obj.name);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        private static void AddBoxCollider(GameObject obj)
        {
            var bounds=BoundsOf(obj);var c=obj.AddComponent<BoxCollider>();
            c.center=obj.transform.InverseTransformPoint(bounds.center);c.size=bounds.size;
        }
        private static Mesh CubeMesh()
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var mesh=cube.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(cube);return mesh;
        }
        private static Mesh BoxHull(Bounds bounds,Transform root)
        {
            var mesh=Object.Instantiate(CubeMesh());var center=root.InverseTransformPoint(bounds.center);
            mesh.vertices=mesh.vertices.Select(v=>Vector3.Scale(v,bounds.size)+center).ToArray();mesh.RecalculateBounds();
            string path=Generated+"/ChairCollision.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static void MakeGrab(GameObject obj,float mass)
        {
            foreach(var t in obj.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            var body=obj.AddComponent<Rigidbody>();body.mass=mass;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
            var grab=obj.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.VelocityTracking;grab.throwOnDetach=true;
            var source=Sound(obj,null,"SFX",false,.2f,8,AudioRolloffMode.Linear);
            var impact=obj.AddComponent<ImpactSound>();impact.clips=Enumerable.Range(1,3).Select(i=>Clip("SFX","Impact"+i)).ToArray();
            var feedback=obj.AddComponent<InteractableAudio>();feedback.feedbackSource=source;
            feedback.grabClip=Clip("SFX","Grab");feedback.releaseClip=Clip("SFX","Release");feedback.switchClip=Clip("SFX","Switch");
        }
        private static void AddSwitch(GameObject obj,AudioSource loop)
        {
            obj.AddComponent<XRSimpleInteractable>();var sw=obj.AddComponent<AudioSwitch>();sw.loopSource=loop;
            sw.feedbackSource=Sound(obj,null,"SFX",false,.2f,6,AudioRolloffMode.Linear);sw.switchClip=Clip("SFX","Switch");
        }
        private static void AddChairLOD(GameObject chair)
        {
            var original=chair.GetComponentsInChildren<Renderer>();
            var simplified=Model("Chair","Chair_LOD1.obj",chair.transform.position);simplified.name="LOD1 / 50%";simplified.transform.SetParent(chair.transform,true);
            var bounds=BoundsOf(chair);
            var proxy=Box("LOD2 / chair proxy",bounds.center,bounds.size,blueMat);Object.DestroyImmediate(proxy.GetComponent<Collider>());proxy.transform.SetParent(chair.transform,true);
            var lod=chair.AddComponent<LODGroup>();lod.SetLODs(new[] {new LOD(.25f,original),new LOD(.08f,simplified.GetComponentsInChildren<Renderer>()),new LOD(.026f,proxy.GetComponentsInChildren<Renderer>())});
            lod.fadeMode=LODFadeMode.None;lod.RecalculateBounds();
        }
        private static AudioMixerGroup Group(string name)
        {
            var group=mixer.FindMatchingGroups(name).FirstOrDefault(x=>x.name==name);
            if(!group)throw new InvalidOperationException("Missing mixer group: "+name);return group;
        }
        private static AudioClip Clip(string folder,string name)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/"+folder+"/"+name+".wav");
            if(!clip)throw new InvalidOperationException("Missing AudioClip: "+name);return clip;
        }
        private static AudioSource Sound(GameObject obj,string name,string group,bool play,float min,float max,AudioRolloffMode rolloff)
        {
            var source=obj.AddComponent<AudioSource>();source.outputAudioMixerGroup=Group(group);source.spatialBlend=1;
            source.minDistance=min;source.maxDistance=max;source.rolloffMode=rolloff;source.dopplerLevel=0;
            source.loop=name!=null;source.playOnAwake=play;source.volume=.65f;if(name!=null)source.clip=Clip("Ambient",name);
            source.spatialize=false; // No HRTF plug-in is installed; never pretend stereo panning is HRTF.
            return source;
        }
        private static void MakeStatic(GameObject obj)
        {
            foreach(var t in obj.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
        }
        private static GameObject Box(string name,Vector3 position,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.position=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;MakeStatic(obj);return obj;
        }
        private static void SetupPlayer(GameObject cube)
        {
            var origin=new GameObject("XR Origin / desktop + simulator");origin.transform.position=new Vector3(0,.05f,-3.5f);
            var xr=origin.AddComponent<XROrigin>();xr.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
            var offset=new GameObject("Camera Offset");offset.transform.SetParent(origin.transform,false);xr.CameraFloorOffsetObject=offset;xr.CameraYOffset=0;
            var cameraObj=new GameObject("Main Camera");cameraObj.tag="MainCamera";cameraObj.transform.SetParent(offset.transform,false);cameraObj.transform.localPosition=new Vector3(0,1.7f,0);
            var camera=cameraObj.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.fieldOfView=65;camera.backgroundColor=new Color(.07f,.11f,.15f);
            cameraObj.AddComponent<AudioListener>();cameraObj.AddComponent<UniversalAdditionalCameraData>();xr.Camera=camera;
            var pose=cameraObj.AddComponent<TrackedPoseDriver>();
            pose.positionInput=Action("XRI Head/Position");pose.rotationInput=Action("XRI Head/Rotation");pose.trackingStateInput=Action("XRI Head/Tracking State");pose.enabled=false;
            var input=origin.AddComponent<InputActionManager>();input.actionAssets=new System.Collections.Generic.List<InputActionAsset>{actions};
            var controllers=new GameObject("XR Controllers");controllers.transform.SetParent(offset.transform,false);
            AddController(controllers,"Left");AddController(controllers,"Right");
            var locomotion=origin.AddComponent<LocomotionSystem>();locomotion.xrOrigin=xr;
            var move=controllers.AddComponent<ActionBasedContinuousMoveProvider>();move.system=locomotion;move.forwardSource=camera.transform;move.moveSpeed=2.5f;
            move.leftHandMoveAction=Action("XRI LeftHand Locomotion/Move");move.rightHandMoveAction=Action("XRI RightHand Locomotion/Move");
            var turn=controllers.AddComponent<ActionBasedSnapTurnProvider>();turn.system=locomotion;turn.turnAmount=45;
            turn.rightHandSnapTurnAction=Action("XRI RightHand Locomotion/Turn");
            var character=origin.AddComponent<CharacterController>();character.height=1.7f;character.center=new Vector3(0,.85f,0);character.radius=.22f;
            var simulatorPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Generated+"/XR Device Simulator/XR Device Simulator.prefab");
            if(!simulatorPrefab)throw new InvalidOperationException("XR Device Simulator prefab missing.");
            var simObj=Object.Instantiate(simulatorPrefab);simObj.name="XR Device Simulator / F2";
            var simulator=simObj.GetComponent<XRDeviceSimulator>();simulator.cameraTransform=camera.transform;simObj.SetActive(false);
            var menu=origin.AddComponent<WorkshopMenu>();menu.mixer=mixer;menu.head=camera.transform;menu.referenceCube=cube;
            // A separate menu action uses the primary (A/X) button; grip and trigger remain available.
            string menuAssetPath=Generated+"/WorkshopMenu.inputactions";
            if (!File.Exists(menuAssetPath))
            {
                var menuAsset=ScriptableObject.CreateInstance<InputActionAsset>();var map=menuAsset.AddActionMap("Workshop");
                var menuAction=map.AddAction("Menu",InputActionType.Button,"<XRController>{LeftHand}/primaryButton");
                menuAction.AddBinding("<XRController>{RightHand}/primaryButton");
                File.WriteAllText(menuAssetPath,menuAsset.ToJson());Object.DestroyImmediate(menuAsset);AssetDatabase.ImportAsset(menuAssetPath);
            }
            var importedMenu=AssetDatabase.LoadAssetAtPath<InputActionAsset>(menuAssetPath);
            menu.vrMenuAction=Reference(importedMenu.FindAction("Workshop/Menu",true),"WorkshopMenu");
            BuildMenu(menu);
            var eventObj=new GameObject("EventSystem");eventObj.AddComponent<EventSystem>();eventObj.AddComponent<XRUIInputModule>();
            var desktop=origin.AddComponent<DesktopWorkshopController>();desktop.head=camera;desktop.menu=menu;desktop.simulator=simulator;desktop.controllerRoot=controllers;desktop.headTracking=pose;
            controllers.SetActive(false);
        }
        private static InputActionReference Reference(InputAction action,string name)
        {
            string path=Generated+"/"+name.Replace("/","_").Replace(" ","_")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<InputActionReference>(path);if(existing)return existing;
            var reference=InputActionReference.Create(action);AssetDatabase.CreateAsset(reference,path);return reference;
        }
        private static InputActionProperty Action(string name)=>new InputActionProperty(Reference(actions.FindAction(name,true),name));
        private static void AddController(GameObject parent,string hand)
        {
            var obj=new GameObject(hand+" Controller");obj.transform.SetParent(parent.transform,false);
            var controller=obj.AddComponent<ActionBasedController>();string device="XRI "+hand+"Hand/",interaction="XRI "+hand+"Hand Interaction/";
            controller.positionAction=Action(device+"Position");controller.rotationAction=Action(device+"Rotation");controller.isTrackedAction=Action(device+"Is Tracked");controller.trackingStateAction=Action(device+"Tracking State");
            controller.selectAction=Action(interaction+"Select");controller.selectActionValue=Action(interaction+"Select Value");
            controller.activateAction=Action(interaction+"Activate");controller.activateActionValue=Action(interaction+"Activate Value");
            controller.uiPressAction=Action(interaction+"UI Press");controller.uiPressActionValue=Action(interaction+"UI Press Value");
            var ray=obj.AddComponent<XRRayInteractor>();ray.enableUIInteraction=true;ray.maxRaycastDistance=8;ray.useForceGrab=true;
            var line=obj.AddComponent<LineRenderer>();line.positionCount=2;line.startWidth=.007f;line.endWidth=.002f;
            line.sharedMaterial=Material("ControllerRay",new Color(.05f,.85f,1));
            var visual=obj.AddComponent<XRInteractorLineVisual>();visual.validColorGradient=new Gradient{colorKeys=new[]{new GradientColorKey(Color.cyan,0),new GradientColorKey(Color.cyan,1)},alphaKeys=new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)}};
        }
        private static GameObject UI(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);
            var rect=(RectTransform)obj.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return obj;
        }
        private static Text Text(string content,Transform parent,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var obj=UI(content,parent,position,size);var text=obj.AddComponent<Text>();text.font=font;text.text=content;text.fontSize=fontSize;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        private static void BuildMenu(WorkshopMenu menu)
        {
            var panel=new GameObject("Pause / audio mixer",typeof(RectTransform));
            var canvas=panel.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=menu.head.GetComponent<Camera>();
            var rect=(RectTransform)panel.transform;rect.sizeDelta=new Vector2(660,470);rect.localScale=Vector3.one*.002f;
            panel.AddComponent<TrackedDeviceGraphicRaycaster>();panel.AddComponent<GraphicRaycaster>();
            var image=panel.AddComponent<Image>();image.color=new Color(.025f,.06f,.085f,.97f);
            Text("АВТОМАСТЕРСКАЯ / МИКШЕР",panel.transform,new Vector2(0,170),new Vector2(600,60),28,Color.white);
            Text("SFX и окружение приглушены снимком Paused",panel.transform,new Vector2(0,123),new Vector2(600,40),18,new Color(.55f,.7f,.75f));
            menu.musicSlider=VolumeSlider(panel.transform,"МУЗЫКА / 2D",60);
            menu.sfxSlider=VolumeSlider(panel.transform,"ЭФФЕКТЫ / SFX",-25);
            menu.ambientSlider=VolumeSlider(panel.transform,"ОКРУЖЕНИЕ / 3D",-110);
            var close=UI("Resume",panel.transform,new Vector2(0,-188),new Vector2(240,44));close.AddComponent<Image>().color=new Color(.95f,.58f,.07f);
            var button=close.AddComponent<Button>();UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,menu.Toggle);
            Text("ПРОДОЛЖИТЬ",close.transform,Vector2.zero,new Vector2(230,40),20,new Color(.04f,.08f,.1f));
            menu.menu=canvas;
        }
        private static Slider VolumeSlider(Transform parent,string title,float y)
        {
            Text(title,parent,new Vector2(0,y+23),new Vector2(550,28),18,Color.white);
            var root=UI(title,parent,new Vector2(0,y-9),new Vector2(500,24));var slider=root.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
            root.AddComponent<Image>().color=new Color(.12f,.21f,.25f);
            var fill=UI("Fill",root.transform,Vector2.zero,new Vector2(500,18));var fillRect=(RectTransform)fill.transform;fillRect.anchorMin=Vector2.zero;fillRect.anchorMax=Vector2.one;fillRect.offsetMin=fillRect.offsetMax=Vector2.zero;fill.AddComponent<Image>().color=new Color(.95f,.58f,.07f);
            var handle=UI("Handle",root.transform,Vector2.zero,new Vector2(24,34));var handleImage=handle.AddComponent<Image>();handleImage.color=Color.white;
            slider.fillRect=fillRect;slider.handleRect=(RectTransform)handle.transform;slider.targetGraphic=handleImage;return slider;
        }
        private static void Label(string text,Vector3 position,float yaw,float scale)
        {
            var obj=new GameObject("Sign / "+text.Split('\n')[0],typeof(RectTransform));var canvas=obj.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            obj.transform.position=position;obj.transform.rotation=Quaternion.Euler(0,yaw,0);obj.transform.localScale=Vector3.one*scale;
            ((RectTransform)obj.transform).sizeDelta=new Vector2(600,120);Text(text,obj.transform,Vector2.zero,new Vector2(600,120),28,new Color(1,.8f,.3f));
        }

        [MenuItem("VR Practice/Validate scenes")]
        public static void ValidateScenes()
        {
            foreach(string path in new[]{CommonScene,VariantScene})
            {
                EditorSceneManager.OpenScene(path);
                if(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length!=1)throw new InvalidOperationException(path+": expected exactly one listener.");
                if(!Object.FindFirstObjectByType<XROrigin>())throw new InvalidOperationException("Missing XR Origin");
                foreach(var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    if(!source.outputAudioMixerGroup)throw new InvalidOperationException("Unrouted AudioSource: "+source.name);
                    if(source.spatialBlend==1 && source.clip)
                    {
                        var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.clip));
                        if(!importer.forceToMono)throw new InvalidOperationException("3D clip is not forced mono: "+source.clip.name);
                    }
                }
                foreach(var grab in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
                    if(!grab.GetComponent<Rigidbody>() || grab.GetComponentsInChildren<Collider>().Length==0 || !grab.GetComponent<ImpactSound>())throw new InvalidOperationException("Incomplete grab: "+grab.name);
                if(Object.FindFirstObjectByType<LODGroup>().lodCount!=3)throw new InvalidOperationException("Expected 3 LOD levels");
                foreach(var root in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.EndsWith(" imported mesh")))
                    if(root.localScale!=Vector3.one || root.localRotation!=Quaternion.identity)throw new InvalidOperationException("Model transform correction: "+root.name);
            }
            Debug.Log("Scene validation passed: XR origin, one listener, mono 3D sources, routing, grabs and 3 LOD levels.");
        }
    }
}
