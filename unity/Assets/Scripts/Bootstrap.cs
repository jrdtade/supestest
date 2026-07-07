using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Builds the entire scene from code the moment Play starts - open any
    /// empty scene and press Play; no prefabs, assets, or scene wiring needed.
    /// Spawns Superman on foot in the middle of a test range: training
    /// robots, pedestrians, parked cars, crates, and fuel tankers, all
    /// reactive to every power.
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
            Registry.Clear();
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

            // --- Superman: starts on foot at a street intersection -----------
            var super = new GameObject("Superman");
            super.transform.position = new Vector3(-135f, 0.4f, -225f);
            var cc = super.AddComponent<CharacterController>();
            cc.height = 1.95f;
            cc.radius = 0.42f;
            cc.center = new Vector3(0f, 1.0f, 0f);
            cc.slopeLimit = 60f;
            Registry.Player = super.transform;

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
            Registry.Cam = camRig;

            var fc = super.AddComponent<FlightController>();
            fc.Init(rig, camRig);
            camRig.Init(super.transform, fc);

            var powers = super.AddComponent<PowersController>();
            powers.Init(fc, rig, cam);

            var combat = super.AddComponent<CombatSystem>();
            combat.Init(fc, rig, powers);
            fc.combat = combat;

            var anim = super.AddComponent<SupermanAnimator>();
            anim.Init(rig, fc, combat);

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
            hud.powers = powers;
            hud.combat = combat;

            EnsureMultiplayer();

            SpawnTestRange();

            ConfigureRuntime();
        }

        /// <summary>Spawn the multiplayer manager and its lobby menu once.</summary>
        private static void EnsureMultiplayer()
        {
            if (Object.FindFirstObjectByType<LanMultiplayer>() != null) return;
            var netGO = new GameObject("Multiplayer");
            var mp = netGO.AddComponent<LanMultiplayer>();
            netGO.AddComponent<NetworkMenu>().net = mp;
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
            Registry.Player = super;

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
            Registry.Cam = camRig;

            fc.Init(rig, camRig);
            camRig.Init(super, fc);

            var powers = super.GetComponent<PowersController>();
            if (powers == null)
                powers = super.gameObject.AddComponent<PowersController>();
            powers.Init(fc, rig, cam);

            var combat = super.GetComponent<CombatSystem>();
            if (combat == null)
                combat = super.gameObject.AddComponent<CombatSystem>();
            combat.Init(fc, rig, powers);
            fc.combat = combat;

            var anim = super.GetComponent<SupermanAnimator>();
            if (anim == null)
                anim = super.gameObject.AddComponent<SupermanAnimator>();
            anim.Init(rig, fc, combat);

            var anchorL = FindDeep(super, "CapeAnchorL");
            var anchorR = FindDeep(super, "CapeAnchorR");
            if (rig.cape != null && anchorL != null && anchorR != null)
            {
                var renderer = rig.cape.GetComponent<MeshRenderer>();
                Material capeMaterial = renderer != null ? renderer.sharedMaterial : null;
                rig.cape.Init(anchorL, anchorR, rig.visualRoot, capeMaterial);
            }

            if (Object.FindFirstObjectByType<TouchControls>() == null)
                new GameObject("TouchControls").AddComponent<TouchControls>();

            var hud = Object.FindFirstObjectByType<FlightHUD>();
            if (hud == null)
                hud = new GameObject("HUD").AddComponent<FlightHUD>();
            hud.controller = fc;
            hud.powers = powers;
            hud.combat = combat;

            EnsureMultiplayer();

            // Registry lists don't survive a scene save; rebuild them.
            RebuildRegistryFromScene();
            if (Object.FindFirstObjectByType<RobotNPC>() == null)
                SpawnTestRange();
        }

        /// <summary>Repopulate X-ray occluders (and NPC lists) for a scene that
        /// was saved with the generated objects already in it.</summary>
        private static void RebuildRegistryFromScene()
        {
            var city = GameObject.Find("Metropolis");
            if (city != null)
            {
                foreach (var r in city.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.gameObject.name == "Ground" || r.gameObject.name == "Globe") continue;
                    Registry.XRayOccluders.Add(r);
                }
            }
            foreach (var robot in Object.FindObjectsOfType<RobotNPC>())
                foreach (var r in robot.GetComponentsInChildren<Renderer>())
                    Registry.Highlights.Add(new HighlightEntry(r, new Color(1f, 0.25f, 0.2f)));
            foreach (var civ in Object.FindObjectsOfType<CivilianNPC>())
            {
                Registry.Civilians.Add(civ);
                foreach (var r in civ.GetComponentsInChildren<Renderer>())
                    Registry.Highlights.Add(new HighlightEntry(r, new Color(0.3f, 1f, 0.5f)));
            }
            foreach (var prop in Object.FindObjectsOfType<ThrowableProp>())
                foreach (var r in prop.GetComponentsInChildren<Renderer>())
                    Registry.Highlights.Add(new HighlightEntry(r, new Color(1f, 0.85f, 0.3f)));
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
            var rig = new SupermanRig
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
                eyeL = FindDeep(super, "EyeL"),
                eyeR = FindDeep(super, "EyeR"),
                cape = Object.FindFirstObjectByType<CapeSimulation>()
            };
            // Scenes saved before heat vision existed lack the eye anchors.
            if (rig.head != null)
            {
                if (rig.eyeL == null) rig.eyeL = MakeAnchor(rig.head, "EyeL", new Vector3(-0.040f, 0.177f, 0.112f));
                if (rig.eyeR == null) rig.eyeR = MakeAnchor(rig.head, "EyeR", new Vector3(0.040f, 0.177f, 0.112f));
            }
            return rig;
        }

        private static Transform MakeAnchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
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

        /// <summary>
        /// The playground around the spawn intersection. Roads run along the
        /// x = -135 and z = -225 lines (each ~20 m wide), so everything is
        /// placed on the pavement, clear of building footprints.
        /// </summary>
        private static void SpawnTestRange()
        {
            // Training robots: a firing-line ahead of spawn plus a patrol group.
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2 == 0) ? -140f : -130f;
                RobotNPC.Spawn(new Vector3(x, 0f, -210f + i * 4f));
            }
            for (int i = 0; i < 4; i++)
            {
                RobotNPC.Spawn(new Vector3(-135f + Random.Range(-5f, 5f), 0f, -168f + i * 6f));
            }

            // Pedestrians along both streets.
            for (int i = 0; i < 10; i++)
            {
                var civ = CivilianNPC.Spawn(new Vector3(
                    Random.Range(-140f, -130f), 0f, Random.Range(-272f, -238f)));
                civ.wanderAxis = new Vector3(0.3f, 0f, 1f);
            }
            for (int i = 0; i < 6; i++)
            {
                var civ = CivilianNPC.Spawn(new Vector3(
                    Random.Range(-195f, -150f), 0f, Random.Range(-230f, -220f)));
                civ.wanderAxis = new Vector3(1f, 0f, 0.3f);
            }

            // Parked cars: a rank south of spawn and one on the cross street.
            for (int i = 0; i < 6; i++)
            {
                PropFactory.Car(new Vector3(-128.5f, 0.05f, -244f - i * 9f), 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                PropFactory.Car(new Vector3(-160f - i * 12f, 0.05f, -231.5f), 90f);
            }

            // Crates to punt around, next to the intersection.
            for (int i = 0; i < 6; i++)
            {
                PropFactory.Crate(new Vector3(
                    -131f + Random.Range(-3f, 3f), 0.05f, -231f + Random.Range(-3f, 3f)));
            }

            // Fuel tankers: the fireworks.
            PropFactory.Tanker(new Vector3(-135.5f, 0.05f, -165f), 0f);
            PropFactory.Tanker(new Vector3(-192f, 0.05f, -228f), 90f);
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
