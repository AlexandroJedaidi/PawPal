using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroTimeOfDayLightingController : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private IntroBackdropRingController sharedTimeOfDaySource;

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Vector3 dayLightEulerAngles = new Vector3(47f, -28f, 0f);
    [SerializeField] private Vector3 nightLightEulerAngles = new Vector3(20f, -18f, 0f);
    [SerializeField] private Color dayLightColor = new Color32(255, 232, 190, 255);
    [SerializeField] private Color nightLightColor = new Color32(150, 176, 255, 255);
    [SerializeField] private float dayLightIntensity = 1.35f;
    [SerializeField] private float nightLightIntensity = 0.28f;
    [SerializeField, Range(0f, 1f)] private float dayShadowStrength = 0.85f;
    [SerializeField, Range(0f, 1f)] private float nightShadowStrength = 0.2f;

    [Header("Ambient")]
    [SerializeField] private Color dayAmbientSky = new Color32(172, 210, 255, 255);
    [SerializeField] private Color nightAmbientSky = new Color32(74, 94, 132, 255);
    [SerializeField] private Color dayAmbientEquator = new Color32(197, 214, 178, 255);
    [SerializeField] private Color nightAmbientEquator = new Color32(55, 63, 82, 255);
    [SerializeField] private Color dayAmbientGround = new Color32(111, 132, 91, 255);
    [SerializeField] private Color nightAmbientGround = new Color32(24, 28, 38, 255);
    [SerializeField, Range(0f, 2f)] private float dayAmbientIntensity = 1f;
    [SerializeField, Range(0f, 2f)] private float nightAmbientIntensity = 0.52f;
    [SerializeField, Range(0f, 2f)] private float dayReflectionIntensity = 1f;
    [SerializeField, Range(0f, 2f)] private float nightReflectionIntensity = 0.35f;

    private int lastAppliedMinuteStamp = int.MinValue;

    public void Configure(IntroBackdropRingController backdropRingController, Light resolvedDirectionalLight)
    {
        sharedTimeOfDaySource = backdropRingController;
        directionalLight = resolvedDirectionalLight != null ? resolvedDirectionalLight : directionalLight;
        ApplyLighting(true);
    }

    private void OnEnable()
    {
        ApplyLighting(true);
    }

    private void Update()
    {
        ApplyLighting(false);
    }

    private void ApplyLighting(bool force)
    {
        PawPalTimeOfDayState state = ResolveTimeOfDayState();
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        if (!force && minuteStamp == lastAppliedMinuteStamp)
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        float daylight01 = Mathf.Clamp01(state.Daylight01);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight01);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, dayAmbientEquator, daylight01);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, daylight01);
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, daylight01);
        RenderSettings.reflectionIntensity = Mathf.Lerp(nightReflectionIntensity, dayReflectionIntensity, daylight01);

        Light light = ResolveDirectionalLight();
        if (light == null)
        {
            return;
        }

        directionalLight = light;
        RenderSettings.sun = light;
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Slerp(
            Quaternion.Euler(nightLightEulerAngles),
            Quaternion.Euler(dayLightEulerAngles),
            daylight01);
        light.color = Color.Lerp(nightLightColor, dayLightColor, daylight01);
        light.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, daylight01);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = Mathf.Lerp(nightShadowStrength, dayShadowStrength, daylight01);
    }

    private PawPalTimeOfDayState ResolveTimeOfDayState()
    {
        if (sharedTimeOfDaySource != null)
        {
            return sharedTimeOfDaySource.EvaluateTimeOfDayState();
        }

        return PawPalTimeOfDayEvaluator.Evaluate(
            true,
            false,
            12f,
            7f,
            20f,
            1f);
    }

    private Light ResolveDirectionalLight()
    {
        if (directionalLight != null)
        {
            return directionalLight;
        }

        if (RenderSettings.sun != null)
        {
            return RenderSettings.sun;
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (candidate != null && candidate.type == LightType.Directional)
            {
                return candidate;
            }
        }

        return null;
    }
}
