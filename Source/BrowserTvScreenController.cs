using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(AudioSource))]
public class BrowserTvScreenController : MonoBehaviour
{
    private const string MainTex = "_MainTex";
    private static readonly Color ScreenTint = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color NoEmission = Color.black;

    private Renderer screenRenderer;
    private Material screenMaterial;
    private Texture2D offTexture;
    private Texture2D standbyTexture;
    private Texture2D errorTexture;
    private Texture2D testTexture;
    private AudioSource audioSource;

    public TileEntityBrowserTV ParentTileEntity { get; set; }

    public void Initialize(Renderer renderer)
    {
        screenRenderer = renderer;
        screenMaterial = screenRenderer != null ? screenRenderer.material : null;
        ConfigureScreenMaterial();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.5f;

        offTexture = CreateSolidTexture("BrowserTV_Off", Color.black);
        standbyTexture = CreateSolidTexture("BrowserTV_Standby", new Color(0.02f, 0.04f, 0.08f, 1f));
        errorTexture = CreateSolidTexture("BrowserTV_Error", new Color(0.45f, 0.02f, 0.02f, 1f));
        testTexture = CreateSolidTexture("BrowserTV_Test", new Color(0.02f, 0.35f, 0.65f, 1f));

        SetState(BrowserTvScreenState.Off);
    }

    public void SetState(BrowserTvScreenState state)
    {
        if (screenRenderer == null)
        {
            Debug.LogWarning("[BrowserTV] Cannot set screen state; renderer is missing on " + gameObject.name);
            return;
        }

        Texture texture = offTexture;
        switch (state)
        {
            case BrowserTvScreenState.Standby:
                texture = standbyTexture;
                break;
            case BrowserTvScreenState.TestColor:
                texture = testTexture;
                break;
            case BrowserTvScreenState.Error:
                texture = errorTexture;
                break;
        }

        ApplyScreenMaterial(texture, state == BrowserTvScreenState.Off ? Color.white : ScreenTint);
        Debug.Log("[BrowserTV] Screen " + gameObject.name + " set to " + state);
    }

    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    public void SetExternalTexture(Texture texture)
    {
        if (screenRenderer != null && texture != null)
        {
            ApplyScreenMaterial(texture, ScreenTint);
        }
    }

    private void ApplyScreenMaterial(Texture texture, Color tint)
    {
        if (screenMaterial == null)
        {
            return;
        }

        ConfigureScreenMaterial();
        screenMaterial.SetTexture(MainTex, texture);
        SetColorIfPresent("_Color", tint);
        SetColorIfPresent("_BaseColor", tint);
        SetColorIfPresent("_TintColor", tint);
        SetFloatIfPresent("_Brightness", 0.22f);
        SetFloatIfPresent("_Intensity", 0f);
        SetFloatIfPresent("_GlowStrength", 0f);
        SetFloatIfPresent("_EmissionScaleUI", 0f);
        SetFloatIfPresent("_EmissiveIntensity", 0f);
        SetFloatIfPresent("_EmissiveExposureWeight", 0f);
        SetColorIfPresent("_EmissionColor", NoEmission);
        SetColorIfPresent("_EmissiveColor", NoEmission);
        SetColorIfPresent("_Emission", NoEmission);
    }

    private void ConfigureScreenMaterial()
    {
        if (screenMaterial == null)
        {
            return;
        }

        screenMaterial.DisableKeyword("_EMISSION");
        screenMaterial.DisableKeyword("EMISSION");
        screenMaterial.DisableKeyword("_EMISSIVE_COLOR_MAP");
        screenMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    private void SetColorIfPresent(string propertyName, Color color)
    {
        if (screenMaterial != null && screenMaterial.HasProperty(propertyName))
        {
            screenMaterial.SetColor(propertyName, color);
        }
    }

    private void SetFloatIfPresent(string propertyName, float value)
    {
        if (screenMaterial != null && screenMaterial.HasProperty(propertyName))
        {
            screenMaterial.SetFloat(propertyName, value);
        }
    }

    private static Texture2D CreateSolidTexture(string name, Color color)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.SetPixels(new[] { color, color, color, color });
        texture.Apply(false, false);
        return texture;
    }

    private void OnDestroy()
    {
        DestroyTexture(offTexture);
        DestroyTexture(standbyTexture);
        DestroyTexture(errorTexture);
        DestroyTexture(testTexture);
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
