using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class FantasyEnvironment : MonoBehaviour
{
    private const string CloudRootName = "Generated Fantasy Clouds";
    private const string MoteRootName = "Generated Magic Motes";

    [Header("Sky")]
    public bool applyEnvironment = true;
    public Color skyTint = new Color(0.55f, 0.66f, 0.78f);
    public Color groundHaze = new Color(0.33f, 0.38f, 0.34f);
    public float atmosphereThickness = 1.15f;
    public float skyExposure = 1.08f;

    [Header("Sun")]
    public Color sunColor = new Color(1f, 0.88f, 0.62f);
    public float sunIntensity = 2.35f;
    public Vector3 sunEulerAngles = new Vector3(38f, -32f, 0f);

    [Header("Mist")]
    public Color fogColor = new Color(0.49f, 0.57f, 0.62f);
    public float fogDensity = 0.012f;

    [Header("Clouds")]
    public bool generateClouds = true;
    [Range(4, 28)] public int cloudCount = 16;
    public float cloudHeight = 26f;
    public float cloudRadius = 62f;
    public Vector2 cloudScaleRange = new Vector2(5.5f, 12f);
    public Color cloudColor = new Color(1f, 0.96f, 0.86f, 0.46f);
    public float cloudDriftSpeed = 0.28f;

    [Header("Magic")]
    public bool generateMagicMotes = true;
    public int magicMoteMaxParticles = 120;
    public Color moteColorA = new Color(0.46f, 0.95f, 0.82f, 0.72f);
    public Color moteColorB = new Color(1f, 0.78f, 0.38f, 0.55f);

    private Material skyboxMaterial;
    private Material cloudMaterial;
    private Material moteMaterial;
    private Texture2D moteTexture;
    private Mesh cloudMesh;
    private Light sunLight;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        ClampSettings();
        Apply();
    }

    private void Update()
    {
        if (!applyEnvironment)
            return;

        AnimateClouds();
    }

    private void OnDisable()
    {
        ClearGeneratedObjects();
        DestroyGeneratedAssets();
    }

    private void Apply()
    {
        if (!applyEnvironment)
            return;

        ClampSettings();
        sunLight = GetComponent<Light>();

        ConfigureSun();
        ConfigureSky();
        ConfigureMist();

        if (generateClouds)
            EnsureClouds();
        else
            DestroyRootObject(CloudRootName);

        if (generateMagicMotes)
            EnsureMagicMotes();
        else
            DestroyRootObject(MoteRootName);
    }

    private void ClampSettings()
    {
        sunIntensity = Mathf.Max(0f, sunIntensity);
        atmosphereThickness = Mathf.Clamp(atmosphereThickness, 0.25f, 5f);
        skyExposure = Mathf.Clamp(skyExposure, 0.2f, 3f);
        fogDensity = Mathf.Clamp(fogDensity, 0f, 0.05f);
        cloudHeight = Mathf.Max(8f, cloudHeight);
        cloudRadius = Mathf.Max(15f, cloudRadius);
        cloudScaleRange.x = Mathf.Max(1f, cloudScaleRange.x);
        cloudScaleRange.y = Mathf.Max(cloudScaleRange.x, cloudScaleRange.y);
        cloudDriftSpeed = Mathf.Max(0f, cloudDriftSpeed);
        magicMoteMaxParticles = Mathf.Clamp(magicMoteMaxParticles, 0, 500);
    }

    private void ConfigureSun()
    {
        if (sunLight == null)
            return;

        transform.rotation = Quaternion.Euler(sunEulerAngles);
        sunLight.type = LightType.Directional;
        sunLight.color = sunColor;
        sunLight.intensity = sunIntensity;
        sunLight.shadows = LightShadows.Soft;
        RenderSettings.sun = sunLight;
    }

    private void ConfigureSky()
    {
        if (skyboxMaterial == null)
            skyboxMaterial = CreateSkyboxMaterial();

        if (skyboxMaterial == null)
            return;

        SetMaterialColor(skyboxMaterial, "_SkyTint", skyTint);
        SetMaterialColor(skyboxMaterial, "_GroundColor", groundHaze);
        SetMaterialFloat(skyboxMaterial, "_AtmosphereThickness", atmosphereThickness);
        SetMaterialFloat(skyboxMaterial, "_Exposure", skyExposure);
        SetMaterialFloat(skyboxMaterial, "_SunSize", 0.07f);
        SetMaterialFloat(skyboxMaterial, "_SunSizeConvergence", 4.2f);
        SetMaterialFloat(skyboxMaterial, "_SunDisk", 2f);

        RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.43f, 0.5f);
        RenderSettings.ambientEquatorColor = new Color(0.25f, 0.29f, 0.27f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.08f);
        DynamicGI.UpdateEnvironment();
    }

    private void ConfigureMist()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private void EnsureClouds()
    {
        Transform root = FindOrCreateRoot(CloudRootName);
        if (root.childCount == cloudCount)
            return;

        ClearChildren(root);

        if (cloudMaterial == null)
            cloudMaterial = CreateTransparentMaterial("Generated Cloud Material", cloudColor, false);
        if (cloudMesh == null)
            cloudMesh = CreateCloudMesh();

        for (int i = 0; i < cloudCount; i++)
        {
            float angle = i / (float)cloudCount * Mathf.PI * 2f + Mathf.Sin(i * 12.989f) * 0.32f;
            float radius = Mathf.Lerp(cloudRadius * 0.42f, cloudRadius, Random01(i, 1));
            float height = cloudHeight + Mathf.Lerp(-4f, 5f, Random01(i, 2));

            GameObject cloud = new GameObject($"Cloud {i + 1:00}");
            cloud.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            cloud.transform.SetParent(root, false);
            cloud.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            cloud.transform.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 360f, Random01(i, 3)), 0f);

            float scale = Mathf.Lerp(cloudScaleRange.x, cloudScaleRange.y, Random01(i, 4));
            cloud.transform.localScale = new Vector3(scale, 1f, scale * Mathf.Lerp(0.55f, 1.15f, Random01(i, 5)));

            MeshFilter meshFilter = cloud.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = cloudMesh;

            MeshRenderer meshRenderer = cloud.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = cloudMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }

    private void EnsureMagicMotes()
    {
        Transform root = FindOrCreateRoot(MoteRootName);
        ParticleSystem particles = root.GetComponent<ParticleSystem>();
        if (particles == null)
            particles = root.gameObject.AddComponent<ParticleSystem>();

        root.localPosition = new Vector3(0f, 4f, 0f);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = magicMoteMaxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = magicMoteMaxParticles > 0;
        emission.rateOverTime = Mathf.Clamp(magicMoteMaxParticles * 0.08f, 2f, 16f);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(72f, 8f, 72f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(moteColorA, 0f),
                new GradientColorKey(Color.Lerp(moteColorA, moteColorB, 0.5f), 0.55f),
                new GradientColorKey(moteColorB, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.72f, 0.18f),
                new GradientAlphaKey(0.42f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetMoteMaterial();

        if (!particles.isPlaying)
            particles.Play();
    }

    private void AnimateClouds()
    {
        GameObject rootObject = GameObject.Find(CloudRootName);
        Transform root = rootObject != null ? rootObject.transform : null;
        if (root == null || cloudDriftSpeed <= 0f)
            return;

        root.Rotate(Vector3.up, cloudDriftSpeed * Time.deltaTime, Space.World);
    }

    private Transform FindOrCreateRoot(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
            return existing.transform;

        GameObject go = new GameObject(objectName);
        go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private Mesh CreateCloudMesh()
    {
        Mesh mesh = new Mesh { name = "Generated Cloud Mesh" };
        mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        const int lobes = 7;
        const int segments = 16;
        Vector3[] vertices = new Vector3[lobes * (segments + 1)];
        int[] triangles = new int[lobes * segments * 3];

        int vertex = 0;
        int triangle = 0;
        for (int lobe = 0; lobe < lobes; lobe++)
        {
            float angle = lobe / (float)lobes * Mathf.PI * 2f;
            float centerDistance = lobe == 0 ? 0f : Mathf.Lerp(0.22f, 0.48f, Random01(lobe, 7));
            Vector3 center = new Vector3(Mathf.Cos(angle) * centerDistance, 0f, Mathf.Sin(angle) * centerDistance);
            float radiusX = Mathf.Lerp(0.22f, 0.42f, Random01(lobe, 8));
            float radiusZ = Mathf.Lerp(0.16f, 0.34f, Random01(lobe, 9));
            int centerIndex = vertex;

            vertices[vertex++] = center;
            for (int segment = 0; segment < segments; segment++)
            {
                float t = segment / (float)segments * Mathf.PI * 2f;
                vertices[vertex++] = center + new Vector3(Mathf.Cos(t) * radiusX, 0f, Mathf.Sin(t) * radiusZ);
            }

            for (int segment = 0; segment < segments; segment++)
            {
                triangles[triangle++] = centerIndex;
                triangles[triangle++] = centerIndex + 1 + segment;
                triangles[triangle++] = centerIndex + 1 + ((segment + 1) % segments);
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Material CreateSkyboxMaterial()
    {
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "Generated Fantasy Procedural Sky",
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };
        return material;
    }

    private Material CreateTransparentMaterial(string materialName, Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            renderQueue = additive ? (int)RenderQueue.Transparent + 20 : (int)RenderQueue.Transparent
        };

        material.SetOverrideTag("RenderType", "Transparent");
        SetMaterialColor(material, "_BaseColor", color);
        SetMaterialColor(material, "_Color", color);
        SetMaterialFloat(material, "_Surface", 1f);
        SetMaterialFloat(material, "_Blend", additive ? 1f : 0f);
        SetMaterialFloat(material, "_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
        SetMaterialFloat(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        SetMaterialFloat(material, "_ZWrite", 0f);
        SetMaterialFloat(material, "_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        return material;
    }

    private Material GetMoteMaterial()
    {
        if (moteMaterial != null)
            return moteMaterial;

        moteMaterial = CreateTransparentMaterial("Generated Magic Mote Material", Color.white, true);
        moteTexture = CreateMoteTexture();
        SetMaterialTexture(moteMaterial, "_BaseMap", moteTexture);
        SetMaterialTexture(moteMaterial, "_MainTex", moteTexture);
        return moteMaterial;
    }

    private Texture2D CreateMoteTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Magic Mote Texture",
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float alpha = Mathf.Clamp01(1f - uv.magnitude);
                alpha = alpha * alpha;
                pixels[x + y * size] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void ClearGeneratedObjects()
    {
        DestroyRootObject(CloudRootName);
        DestroyRootObject(MoteRootName);
    }

    private void DestroyRootObject(string objectName)
    {
        GameObject rootObject = GameObject.Find(objectName);
        if (rootObject != null)
            DestroyGeneratedObject(rootObject);
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyGeneratedObject(root.GetChild(i).gameObject);
    }

    private void DestroyGeneratedAssets()
    {
        DestroyGeneratedObject(skyboxMaterial);
        DestroyGeneratedObject(cloudMaterial);
        DestroyGeneratedObject(moteMaterial);
        DestroyGeneratedObject(moteTexture);
        DestroyGeneratedObject(cloudMesh);
        skyboxMaterial = null;
        cloudMaterial = null;
        moteMaterial = null;
        moteTexture = null;
        cloudMesh = null;
    }

    private static void DestroyGeneratedObject(Object objectToDestroy)
    {
        if (objectToDestroy == null)
            return;

        if (Application.isPlaying)
            Destroy(objectToDestroy);
        else
            DestroyImmediate(objectToDestroy);
    }

    private static void SetMaterialColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetMaterialTexture(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    private static float Random01(int index, int salt)
    {
        float value = Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }
}
