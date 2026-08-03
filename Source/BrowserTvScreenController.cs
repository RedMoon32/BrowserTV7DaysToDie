using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(AudioSource))]
public class BrowserTvScreenController : MonoBehaviour
{
    private const string MainTex = "_MainTex";
    private static readonly Color ScreenGlow = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color VideoGlow = new Color(0.32f, 0.32f, 0.32f, 1f);
    private static readonly Vector2 NormalTextureScale = Vector2.one;
    private static readonly Vector2 NormalTextureOffset = Vector2.zero;
    private static readonly Vector2 VideoTextureScale = new Vector2(1f, -1f);
    private static readonly Vector2 VideoTextureOffset = new Vector2(0f, 1f);

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

        ApplyScreenMaterial(texture, state == BrowserTvScreenState.Off ? Color.black : ScreenGlow, false, false);
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
            ApplyScreenMaterial(texture, VideoGlow, true, true);
        }
    }

    public bool TryGetBrowserCoordinates(Ray worldRay, out Vector2 coordinates, out float distance)
    {
        coordinates = Vector2.zero;
        distance = 0f;
        if (screenRenderer == null)
        {
            return false;
        }

        MeshFilter meshFilter = screenRenderer.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        if (mesh == null || mesh.vertices == null || mesh.triangles == null || mesh.uv == null)
        {
            return false;
        }

        Vector3 localOrigin = transform.InverseTransformPoint(worldRay.origin);
        Vector3 localDirection = transform.InverseTransformDirection(worldRay.direction).normalized;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        int[] triangles = mesh.triangles;
        float nearestT = float.MaxValue;
        Vector2 nearestUv = Vector2.zero;

        for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
        {
            int first = triangles[triangle];
            int second = triangles[triangle + 1];
            int third = triangles[triangle + 2];
            if (first >= vertices.Length || second >= vertices.Length || third >= vertices.Length ||
                first >= uv.Length || second >= uv.Length || third >= uv.Length)
            {
                continue;
            }

            if (!TryIntersectTriangle(localOrigin, localDirection, vertices[first], vertices[second], vertices[third], out float t, out float barycentricB, out float barycentricC) || t >= nearestT)
            {
                continue;
            }

            float barycentricA = 1f - barycentricB - barycentricC;
            nearestT = t;
            nearestUv = uv[first] * barycentricA + uv[second] * barycentricB + uv[third] * barycentricC;
        }

        if (nearestT == float.MaxValue)
        {
            return false;
        }

        Vector3 localHit = localOrigin + localDirection * nearestT;
        Vector3 worldHit = transform.TransformPoint(localHit);
        distance = Vector3.Distance(worldRay.origin, worldHit);
        coordinates = new Vector2(Mathf.Clamp01(nearestUv.x), Mathf.Clamp01(1f - nearestUv.y));
        return true;
    }

    private static bool TryIntersectTriangle(Vector3 origin, Vector3 direction, Vector3 first, Vector3 second, Vector3 third, out float t, out float barycentricB, out float barycentricC)
    {
        const float Epsilon = 0.000001f;
        Vector3 firstEdge = second - first;
        Vector3 secondEdge = third - first;
        Vector3 perpendicular = Vector3.Cross(direction, secondEdge);
        float determinant = Vector3.Dot(firstEdge, perpendicular);
        t = 0f;
        barycentricB = 0f;
        barycentricC = 0f;
        if (Mathf.Abs(determinant) < Epsilon)
        {
            return false;
        }

        float inverseDeterminant = 1f / determinant;
        Vector3 offset = origin - first;
        barycentricB = Vector3.Dot(offset, perpendicular) * inverseDeterminant;
        if (barycentricB < 0f || barycentricB > 1f)
        {
            return false;
        }

        Vector3 cross = Vector3.Cross(offset, firstEdge);
        barycentricC = Vector3.Dot(direction, cross) * inverseDeterminant;
        if (barycentricC < 0f || barycentricB + barycentricC > 1f)
        {
            return false;
        }

        t = Vector3.Dot(secondEdge, cross) * inverseDeterminant;
        return t > Epsilon;
    }

    private void ApplyScreenMaterial(Texture texture, Color glowColor, bool flipVertical, bool isVideo)
    {
        if (screenMaterial == null)
        {
            return;
        }

        ConfigureScreenMaterial();
        screenMaterial.SetTexture(MainTex, texture);
        ApplyTextureTransform(flipVertical);
        SetColorIfPresent("_GlowColor", glowColor);
        SetFloatIfPresent("_Exposure", isVideo ? 0.8f : 0.25f);
        SetFloatIfPresent("_Gamma", isVideo ? 1.35f : 1f);
        SetFloatIfPresent("_Contrast", 1f);
        SetFloatIfPresent("_Saturation", 1f);
    }

    private void ApplyTextureTransform(bool flipVertical)
    {
        Vector2 scale = flipVertical ? VideoTextureScale : NormalTextureScale;
        Vector2 offset = flipVertical ? VideoTextureOffset : NormalTextureOffset;
        SetTextureTransformIfPresent(MainTex, scale, offset);
    }

    private void SetTextureTransformIfPresent(string propertyName, Vector2 scale, Vector2 offset)
    {
        if (screenMaterial != null && screenMaterial.HasProperty(propertyName))
        {
            screenMaterial.SetTextureScale(propertyName, scale);
            screenMaterial.SetTextureOffset(propertyName, offset);
        }
    }

    private void ConfigureScreenMaterial()
    {
        if (screenMaterial == null)
        {
            return;
        }

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
