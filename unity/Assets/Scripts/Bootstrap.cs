using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Builds the entire scene from code the moment Play starts - open any
    /// empty scene and press Play; no prefabs, assets, or scene wiring needed.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            BuildSceneIfNeeded();
        }

        public static void BuildSceneIfNeeded()
        {
            SetupLightingAndSky();

            var existingController = Object.FindFirstObjectByType<FlightController>();
            if (existingController != null)
            {
                RebindScene(existingController);
                ConfigureRuntime();
                return;
            }

            var cityRoot = new GameObject("Metropolis");
            CityGenerator.Build(cityRoot.transform);

            // --- Superman -------------------------------------------------
            var super = new GameObject("Superman");
            // Above a street intersection (roads run on the +/-45 + 90k lines),
            // guaranteed clear of building geometry.
            super.transform.position = new Vector3(-135f, 60f, -225f);
            var cc = super.AddComponent<CharacterController>();
            cc.height = 1.95f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 1.0f, 0f);
            cc.slopeLimit = 60f;

            SupermanRig rig = SupermanModel.Build(super.transform);

            // --- Camera ----------------------------------------------------
            var camGO = GetOrCreateMainCamera();
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 4000f;
            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
            var camRig = camGO.GetComponent<CameraRig>();
            if (camRig == null)
                camRig = camGO.AddComponent<CameraRig>();

            var fc = super.AddComponent<FlightController>();
            fc.Init(rig, camRig);
            camRig.Init(super.transform, fc);

            var anim = super.AddComponent<SupermanAnimator>();
            anim.Init(rig, fc);

            // Aim the camera at the skyline on spawn.
            camGO.transform.position = super.transform.position + new Vector3(0f, 2.5f, -6f);
            camGO.transform.LookAt(super.transform.position + Vector3.up * 1.4f);

            // --- Touch controls (Android) ------------------------------------
            if (Object.FindFirstObjectByType<TouchControls>() == null)
                new GameObject("TouchControls").AddComponent<TouchControls>();

            // --- HUD ---------------------------------------------------------
            var hud = Object.FindFirstObjectByType<FlightHUD>();
            if (hud == null)
                hud = new GameObject("HUD").AddComponent<FlightHUD>();
            hud.controller = fc;

            ConfigureRuntime();
        }

        private static void ConfigureRuntime()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private static void RebindScene(FlightController fc)
        {
            Transform super = fc.transform;
            SupermanRig rig = CaptureRig(super);
            if (!HasCompleteRig(rig))
            {
                Debug.LogError("LastSon bootstrap found an existing Superman but could not rebind the generated rig.");
                return;
            }

            var camGO = GetOrCreateMainCamera();
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 4000f;
            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            var camRig = camGO.GetComponent<CameraRig>();
            if (camRig == null)
                camRig = camGO.AddComponent<CameraRig>();

            fc.Init(rig, camRig);
            camRig.Init(super, fc);

            var anim = super.GetComponent<SupermanAnimator>();
            if (anim == null)
                anim = super.gameObject.AddComponent<SupermanAnimator>();
            anim.Init(rig, fc);

            var anchorL = FindDeep(super, "CapeAnchorL");
            var anchorR = FindDeep(super, "CapeAnchorR");
            if (rig.cape != null && anchorL != null && anchorR != null)
            {
                var renderer = rig.cape.GetComponent<MeshRenderer>();
                Material capeMaterial = renderer != null ? renderer.sharedMaterial : null;
                rig.cape.Init(anchorL, anchorR, rig.visualRoot, capeMaterial);
            }

            var hud = Object.FindFirstObjectByType<FlightHUD>();
            if (hud != null)
                hud.controller = fc;
        }

        private static bool HasCompleteRig(SupermanRig rig)
        {
            return rig.visualRoot != null
                && rig.torso != null
                && rig.neck != null
                && rig.shoulderL != null
                && rig.shoulderR != null
                && rig.elbowL != null
                && rig.elbowR != null
                && rig.hipL != null
                && rig.hipR != null
                && rig.kneeL != null
                && rig.kneeR != null
                && rig.handL != null
                && rig.handR != null
                && rig.bootL != null
                && rig.bootR != null;
        }

        private static SupermanRig CaptureRig(Transform super)
        {
            return new SupermanRig
            {
                visualRoot = FindDeep(super, "Visual"),
                pelvis = FindDeep(super, "Pelvis"),
                torso = FindDeep(super, "Torso"),
                neck = FindDeep(super, "Neck"),
                head = FindDeep(super, "Head"),
                shoulderL = FindDeep(super, "ShoulderL"),
                shoulderR = FindDeep(super, "ShoulderR"),
                elbowL = FindDeep(super, "ElbowL"),
                elbowR = FindDeep(super, "ElbowR"),
                hipL = FindDeep(super, "HipL"),
                hipR = FindDeep(super, "HipR"),
                kneeL = FindDeep(super, "KneeL"),
                kneeR = FindDeep(super, "KneeR"),
                handL = FindDeep(super, "HandL"),
                handR = FindDeep(super, "HandR"),
                bootL = FindDeep(super, "BootL"),
                bootR = FindDeep(super, "BootR"),
                cape = Object.FindFirstObjectByType<CapeSimulation>()
            };
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject GetOrCreateMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                cam = Object.FindFirstObjectByType<Camera>();

            if (cam != null)
            {
                cam.gameObject.name = "Main Camera";
                return cam.gameObject;
            }

            var camGO = new GameObject("Main Camera");
            camGO.AddComponent<Camera>();
            return camGO;
        }

        private static void SetupLightingAndSky()
        {
            // Late-afternoon sun with soft shadows - warm key light for the suit.
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        sun = lights[i];
                        break;
                    }
                }
            }

            GameObject sunGO;
            if (sun == null)
            {
                sunGO = new GameObject("Sun");
                sun = sunGO.AddComponent<Light>();
            }
            else
            {
                sunGO = sun.gameObject;
                sunGO.name = "Sun";
            }

            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.956f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGO.transform.rotation = Quaternion.Euler(42f, 145f, 0f);

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 0.95f);
            sky.SetColor("_SkyTint", new Color(0.42f, 0.55f, 0.80f));
            sky.SetColor("_GroundColor", new Color(0.35f, 0.33f, 0.32f));
            sky.SetFloat("_Exposure", 1.15f);
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.20f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.fogDensity = 0.00085f;

            QualitySettings.shadowDistance = 320f;
        }
    }
}
