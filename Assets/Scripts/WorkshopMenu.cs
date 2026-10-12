using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VRPractice
{
    public sealed class WorkshopMenu : MonoBehaviour
    {
        public AudioMixer mixer;
        public Canvas menu;
        public Transform head;
        public Slider musicSlider, sfxSlider, ambientSlider;
        public GameObject referenceCube;
        public InputActionReference vrMenuAction;
        public bool IsOpen { get; private set; }
        private AudioMixerSnapshot normal, paused;
        private float previousTimeScale = 1;

        public static float LinearToDecibels(float value) => Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20;
        private void Start()
        {
            normal = mixer.FindSnapshot("Normal");
            paused = mixer.FindSnapshot("Paused");
            Bind(musicSlider, "MusicVolume", 0.35f);
            Bind(sfxSlider, "SFXVolume", 0.8f);
            Bind(ambientSlider, "AmbientVolume", 0.6f);
            menu.gameObject.SetActive(false);
        }
        private void OnEnable()
        {
            if (vrMenuAction) { vrMenuAction.action.Enable(); vrMenuAction.action.performed += OnVRMenu; }
        }
        private void OnDisable()
        {
            if (vrMenuAction) vrMenuAction.action.performed -= OnVRMenu;
        }
        private void OnVRMenu(InputAction.CallbackContext _) => Toggle();
        private void Bind(Slider slider, string parameter, float initial)
        {
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(parameter, initial));
            mixer.SetFloat(parameter, LinearToDecibels(slider.value));
            slider.onValueChanged.AddListener(value =>
            {
                mixer.SetFloat(parameter, LinearToDecibels(value));
                PlayerPrefs.SetFloat(parameter, value);
            });
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Toggle();
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && referenceCube)
                referenceCube.SetActive(!referenceCube.activeSelf);
        }
        public void Toggle()
        {
            IsOpen = !IsOpen;
            menu.gameObject.SetActive(IsOpen);
            if (IsOpen)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0;
                menu.transform.position = head.position + head.forward * 1.5f;
                menu.transform.rotation = head.rotation;
            }
            else Time.timeScale = previousTimeScale;
            // Separate ducking parent buses allow snapshots AND exposed user volumes.
            (IsOpen ? paused : normal)?.TransitionTo(0.5f);
            if (!IsOpen) PlayerPrefs.Save();
        }
        private void OnApplicationQuit() => PlayerPrefs.Save();
    }
}
