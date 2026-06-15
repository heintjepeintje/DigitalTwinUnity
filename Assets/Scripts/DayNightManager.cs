using UnityEngine;

public class DayNightManager : MonoBehaviour
{
    [Header("Lighting")]
    public Light directionalLight;
    public Color dayAmbientColor = Color.white;
    public Color nightAmbientColor = new Color(0.15f, 0.15f, 0.2f);

    [Header("Skybox")]
    public Material daySkybox;
    public Material nightSkybox;

    [Header("Street Lamps")]
    public Renderer[] lampRenderers;
    public string emissionMaterialName = "Emissive";
    public Color lampOffEmission = Color.black;
    public Color lampOnEmission = new Color(1f, 0.8f, 0.4f) * 2f;

    public bool IsNight { get; private set; }

    void Start()
    {
        ApplyCurrentState();
    }

    public void ToggleDayNight()
    {
        IsNight = !IsNight;
        ApplyCurrentState();
    }

    void ApplyCurrentState()
    {
        if (IsNight)
        {
            if (directionalLight != null)
                directionalLight.intensity = 0.2f;

            RenderSettings.ambientLight = nightAmbientColor;

            if (nightSkybox != null)
                RenderSettings.skybox = nightSkybox;

            SetLampEmission(true);
        }
        else
        {
            if (directionalLight != null)
                directionalLight.intensity = 1f;

            RenderSettings.ambientLight = dayAmbientColor;

            if (daySkybox != null)
                RenderSettings.skybox = daySkybox;

            SetLampEmission(false);
        }

        DynamicGI.UpdateEnvironment();
    }

    void SetLampEmission(bool enabled)
    {
        foreach (Renderer rend in lampRenderers)
        {
            if (rend == null) continue;

            Material[] mats = rend.materials;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];

                if (mat == null) continue;

                if (!mat.name.StartsWith(emissionMaterialName))
                    continue;

                if (enabled)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", lampOnEmission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    mat.SetColor("_EmissionColor", lampOffEmission);
                    mat.DisableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            }
        }
    }
}