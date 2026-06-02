using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BrowserTvScreenController : MonoBehaviour
{
    private const string MainTex = "_MainTex";

    private Renderer screenRenderer;
    private Texture2D offTexture;
    private Texture2D standbyTexture;
    private Texture2D errorTexture;
    private Texture2D testTexture;
    private AudioSource audioSource;

    public TileEntityBrowserTV ParentTileEntity { get; set; }

    public void Initialize(Renderer renderer)
    {
        screenRenderer = renderer;
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

        screenRenderer.material.SetTexture(MainTex, texture);
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
            screenRenderer.material.SetTexture(MainTex, texture);
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
