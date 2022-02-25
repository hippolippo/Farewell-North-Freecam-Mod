using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using UnityEngine.InputSystem;
using FarewellNorth.Camera;
using FarewellNorth.Managers.Impl;
using FarewellNorth.Coloring;
using Cinemachine;
using UnityEngine.Rendering.Universal;
using FarewellNorth.Camera.Settings;
using FarewellNorth.Core.Settings;
using FarewellNorth.Characters.Player;
using FarewellNorth.Characters.Girl;
using FarewellNorth.Actions.Sitting;
namespace FNFreeCam {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Farewell North")]
    public class FNFreeCam : BaseUnityPlugin {
        
        
        public const string pluginGuid = "org.bepinex.plugins.FNFreeCam";
        public const string pluginName = "FN Free Cam";
        public const string pluginVersion = "1.0.0";
        static FNFreeCam instance;
        static GameObject camobj;
        static Camera cam;
        static bool inFreeCam = false;
        static bool isSprinting = false;
        static bool mouseLocked = false;
        static bool usingGameCam = false;
        static VirtualCamera _active;
        static PlayerManager playerManager;
        static GirlManager girlManager;
        static FieldInfo vcam;
        static FieldInfo shader;
        const float normal_speed = 1.0f;
        const float sprint_speed = 4.0f;
        const float sensitivity = 0.6f;
        
        public FNFreeCam(){
            instance = this;
            vcam = typeof(VirtualCamera).GetField("_vcam", BindingFlags.Instance | BindingFlags.NonPublic);
            shader = typeof(ColoringShaderParams).GetField("MATERIAL_PARAM_CAMERA_TARGET_POSITION", BindingFlags.Static | BindingFlags.NonPublic);
        }
        void Awake(){
            Logger.LogInfo("Loading Farewell North Freecam Mod");
            Harmony.CreateAndPatchAll(typeof(FNFreeCam));
        }
        void Update(){
            mouseLocked = false;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if(keyboard.cKey.wasPressedThisFrame){
                if(inFreeCam){
                    Logger.LogInfo("Leaving Free Camera");
                    if(!usingGameCam){
                        camobj.SetActive(false);
                    }
                    _active.SetEnabled(true);
                    
                    inFreeCam = false;

                }else{
                    Logger.LogInfo("Entering Free Camera");
                    usingGameCam = false;
                    camobj = Instantiate(new GameObject("FreeCamera"), _active.transform);
                    cam = camobj.AddComponent<Camera>();
                    _active.SetEnabled(false);
                    camobj.transform.position = _active.transform.position;
                    camobj.transform.eulerAngles = _active.transform.eulerAngles;
                    inFreeCam = true;
                    isSprinting = false;
                    Cursor.lockState = CursorLockMode.None;
                    CinemachineBrain brain = CinemachineCore.Instance.FindPotentialTargetBrain((CinemachineVirtualCameraBase)vcam.GetValue(_active));
                    if(brain != null){
                        Camera gamecam = brain.OutputCamera;
                        cam.CopyFrom(gamecam);
                        Component original = gamecam.GetComponent<UniversalAdditionalCameraData>();
                        Component copy = camobj.AddComponent<UniversalAdditionalCameraData>();
                        System.Reflection.FieldInfo[] fields = typeof(UniversalAdditionalCameraData).GetFields(); 
                        foreach (System.Reflection.FieldInfo field in fields)
                        {
                            field.SetValue(copy, field.GetValue(original));
                        }
                        Component original2 = gamecam.GetComponent<CameraSettingsApplier>();
                        Component copy2 = camobj.AddComponent<CameraSettingsApplier>();
                        System.Reflection.FieldInfo[] fields2 = typeof(CameraSettingsApplier).GetFields(); 
                        foreach (System.Reflection.FieldInfo field2 in fields2)
                        {
                            field2.SetValue(copy2, field2.GetValue(original2));
                        }
                        System.Reflection.MethodInfo method2 = typeof(CameraSettingsApplier).GetMethod("ApplySettings", BindingFlags.NonPublic | BindingFlags.Instance);
                        CameraSettingsApplier.SettingTier settings = new CameraSettingsApplier.SettingTier();
                        settings.AllowMSAA = true;
                        settings.PostProcessingEnabled = true;
                        settings.Level = QualitySetting.QualityLevel.Ultra;
                        method2.Invoke(copy2, new object[]{settings});
                        //usingGameCam = true;
                        //camobj = gamecam.gameObject;
                    }else{
                        Logger.LogWarning("Failed to find brain");
                    }
                    camobj.SetActive(true);
                    mouse.WarpCursorPosition(new Vector2(Screen.width/2, Screen.height/2));
                }
            }
            else if(inFreeCam){
                Cursor.lockState = CursorLockMode.None;
                isSprinting = keyboard.leftShiftKey.isPressed;
                float speed = isSprinting ? sprint_speed : normal_speed;
                var rotate_angle = camobj.transform.rotation;
                rotate_angle.eulerAngles = new Vector3(0f, rotate_angle.eulerAngles.y, 0f);
                if(keyboard.wKey.isPressed){
                    camobj.transform.position = camobj.transform.position + rotate_angle*new Vector3(0f, 0f, speed);
                }
                if(keyboard.sKey.isPressed){
                    camobj.transform.position = camobj.transform.position + rotate_angle*new Vector3(0f, 0f, -speed);
                }
                if(keyboard.aKey.isPressed){
                    camobj.transform.position = camobj.transform.position + rotate_angle*new Vector3(-speed, 0f, 0f);
                }
                if(keyboard.dKey.isPressed){
                    camobj.transform.position = camobj.transform.position + rotate_angle*new Vector3(speed, 0f, 0f);
                }
                if(keyboard.spaceKey.isPressed){
                    camobj.transform.position = camobj.transform.position + rotate_angle*new Vector3(0f, speed, 0f);
                }
                if(keyboard.leftCtrlKey.isPressed){
                    camobj.transform.position = camobj.transform.position + rotate_angle*new Vector3(0f, -speed, 0f);
                }
                if(keyboard.tKey.wasPressedThisFrame && playerManager != null){
                    Vector3 pos = camobj.transform.position + (camobj.transform.rotation * new Vector3(0f,0f,1f));
                    playerManager.Player.gameObject.transform.position = pos;
                    FieldInfo girl = typeof(GirlManager).GetField("_girl", BindingFlags.NonPublic | BindingFlags.Instance);
                    ((GirlCharacter)girl.GetValue(girlManager)).transform.position = pos;
                }
                if(mouse.wasUpdatedThisFrame){
                    Vector2 mousepos = mouse.position.ReadValue();
                    //mousepos = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                    Vector3 newEulers = camobj.transform.eulerAngles + new Vector3(-(mousepos.y - Screen.height/2)*sensitivity, (mousepos.x - Screen.width/2)*sensitivity, 0f);
                    if(newEulers.x > 90 && newEulers.x < 180){
                        newEulers.x = 90;
                    }
                    if(newEulers.x < 270 && newEulers.x > 180){
                        newEulers.x = 270;
                    }
                    camobj.transform.eulerAngles = newEulers;
                    mouse.WarpCursorPosition(new Vector2(Screen.width/2, Screen.height/2));
                }
                mouseLocked = true;
            }
        }

        [HarmonyPatch(typeof(InputManager), "IsInputLocked")]
        [HarmonyPrefix]
        private static void lockInputPatch(ref bool __result, ref bool __runOriginal){
            if(inFreeCam){
                __result = true;
                __runOriginal = false;
            }
            __runOriginal = true;
        }

        [HarmonyPatch(typeof(PlayerCharacter), "IsMovementLocked")]
        [HarmonyPrefix]
        private static void lockMovementPatch(ref bool __result, ref bool __runOriginal){
            if(inFreeCam){
                __result = true;
                __runOriginal = false;
            }
            __runOriginal = true;
        }

        [HarmonyPatch(typeof(VirtualCameraManager), "UpdateActiveCamera")]
        [HarmonyPostfix]
        private static void VirtualCameraStartPostfixPatch(VirtualCameraManager __instance){
            _active = __instance.PrimaryCamera;
        }

        [HarmonyPatch(typeof(Mouse),nameof(Mouse.WarpCursorPosition))]
        [HarmonyPrefix]
        private static void movecursorpatch(ref bool __runOriginal){
            __runOriginal = !mouseLocked;
        }

        [HarmonyPatch(typeof(PlayerManager), "Awake")]
        [HarmonyPostfix]
        private static void getplayermanager(ref PlayerManager __instance){
            playerManager = __instance;
        }
        
        [HarmonyPatch(typeof(GirlManager), "OnEnable")]
        [HarmonyPostfix]
        private static void getgirlmanager(ref GirlManager __instance){
            girlManager = __instance;
        }
    }
}