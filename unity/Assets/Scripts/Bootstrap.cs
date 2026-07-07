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
            if (Object.FindObjectOfType<FlightController>() != null) return;

            Registry.Clear();
            SetupLightingAndSky();

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
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 4000f;
            camGO.AddComponent<AudioListener>();
            var camRig = camGO.AddComponent<CameraRig>();
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

            // --- HUD ---------------------------------------------------------
            var hud = new GameObject("HUD").AddComponent<FlightHUD>();
            hud.controller = fc;
            hud.powers = powers;
            hud.combat = combat;

            SpawnTestRange();

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
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
            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
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
