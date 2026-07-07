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
            if (Object.FindObjectOfType<FlightController>() != null) return;

            SetupLightingAndSky();

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
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 4000f;
            camGO.AddComponent<AudioListener>();
            var camRig = camGO.AddComponent<CameraRig>();

            var fc = super.AddComponent<FlightController>();
            fc.Init(rig, camRig);
            camRig.Init(super.transform, fc);

            var anim = super.AddComponent<SupermanAnimator>();
            anim.Init(rig, fc);

            // Aim the camera at the skyline on spawn.
            camGO.transform.position = super.transform.position + new Vector3(0f, 2.5f, -6f);
            camGO.transform.LookAt(super.transform.position + Vector3.up * 1.4f);

            // --- HUD ---------------------------------------------------------
            var hud = new GameObject("HUD").AddComponent<FlightHUD>();
            hud.controller = fc;

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
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
