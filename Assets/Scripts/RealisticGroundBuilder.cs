using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class RealisticGroundBuilder : MonoBehaviour
{
    private const string TerrainMeshName = "Generated Realistic Dirt Terrain";
    private const string GroundMaterialName = "Generated Procedural Dirt Material";
    private const string DetailRootName = "Generated Ground Details";
    private const string RockMeshName = "Generated Ground Rock Mesh";
    private const string RockMaterialName = "Generated Ground Rock Material";
    private const string GrassMeshName = "Generated Ground Grass Mesh";
    private const string GrassMaterialName = "Generated Ground Grass Material";

    [Header("Terrain")]
    public int seed = 4317;
    [Range(24, 180)] public int gridResolution = 112;
    public float terrainSize = 90f;
    public float heightScale = 1.35f;
    public float safeRadius = 6f;
    public float safeFalloff = 5f;
    public Vector2 noiseOffset = new Vector2(18.7f, -32.4f);

    [Header("Details")]
    public bool generateDetails = true;
    [Range(0, 140)] public int rockCount = 58;
    [Range(0, 360)] public int grassTuftCount = 220;
    public float detailAvoidRadius = 7f;
    public bool addDetailColliders = true;
    public bool addRockColliders = true;
    public bool addPlantColliders = true;
    public float plantColliderHeight = 0.65f;
    public float plantColliderRadius = 0.14f;

    [Header("Colors")]
    public Color darkDirt = new Color(0.22f, 0.14f, 0.08f);
    public Color midDirt = new Color(0.38f, 0.25f, 0.14f);
    public Color dryDirt = new Color(0.58f, 0.43f, 0.26f);
    public Color rockColor = new Color(0.37f, 0.34f, 0.28f);
    public Color grassColor = new Color(0.29f, 0.38f, 0.16f);

#if UNITY_EDITOR
    private bool rebuildQueued;
#endif

    private void OnEnable()
    {
        Build();
    }

    private void OnValidate()
    {
        ClampSettings();

        if (!isActiveAndEnabled)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRebuild();
            return;
        }
#endif

        Build();
    }

    [ContextMenu("Rebuild Ground")]
    public void Build()
    {
        ClampSettings();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        Mesh previousMesh = meshFilter.sharedMesh;
        Material previousMaterial = meshRenderer.sharedMaterial;

        Mesh terrainMesh = CreateTerrainMesh();
        Material terrainMaterial = CreateGroundMaterial();

        meshFilter.sharedMesh = terrainMesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = terrainMesh;
        meshRenderer.sharedMaterial = terrainMaterial;

        DestroyGeneratedMesh(previousMesh);
        DestroyGeneratedMaterial(previousMaterial);

        if (generateDetails)
            RebuildDetails();
        else
            ClearGeneratedDetails();
    }

#if UNITY_EDITOR
    private void QueueEditorRebuild()
    {
        if (rebuildQueued)
            return;

        rebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null)
                return;

            rebuildQueued = false;
            if (isActiveAndEnabled)
                Build();
        };
    }
#endif

    private void ClampSettings()
    {
        gridResolution = Mathf.Clamp(gridResolution, 24, 180);
        terrainSize = Mathf.Max(20f, terrainSize);
        heightScale = Mathf.Max(0.05f, heightScale);
        safeRadius = Mathf.Max(0f, safeRadius);
        safeFalloff = Mathf.Max(0.5f, safeFalloff);
        detailAvoidRadius = Mathf.Max(safeRadius, detailAvoidRadius);
        plantColliderHeight = Mathf.Max(0.1f, plantColliderHeight);
        plantColliderRadius = Mathf.Clamp(plantColliderRadius, 0.03f, 0.4f);
    }

    private Mesh CreateTerrainMesh()
    {
        int vertexCountPerSide = gridResolution + 1;
        Vector3[] vertices = new Vector3[vertexCountPerSide * vertexCountPerSide];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[gridResolution * gridResolution * 6];
        float halfSize = terrainSize * 0.5f;
        float step = terrainSize / gridResolution;

        for (int z = 0; z < vertexCountPerSide; z++)
        {
            for (int x = 0; x < vertexCountPerSide; x++)
            {
                int index = x + z * vertexCountPerSide;
                float localX = -halfSize + x * step;
                float localZ = -halfSize + z * step;
                float height = SampleHeight(localX, localZ);
                float moisture = Noise(localX * 0.045f + 71.4f, localZ * 0.045f - 28.2f);
                float colorBlend = Mathf.Clamp01(moisture * 0.65f + Mathf.InverseLerp(-heightScale, heightScale, height) * 0.35f);

                vertices[index] = new Vector3(localX, height, localZ);
                uvs[index] = new Vector2(x / (float)gridResolution, z / (float)gridResolution) * (terrainSize / 7f);
                colors[index] = Color.Lerp(darkDirt, Color.Lerp(midDirt, dryDirt, colorBlend), 0.82f);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < gridResolution; z++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                int i = x + z * vertexCountPerSide;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + vertexCountPerSide;
                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = i + vertexCountPerSide;
                triangles[triangleIndex++] = i + vertexCountPerSide + 1;
            }
        }

        Mesh mesh = new Mesh
        {
            name = TerrainMeshName,
            indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float SampleHeight(float localX, float localZ)
    {
        float low = Noise(localX * 0.025f + noiseOffset.x, localZ * 0.025f + noiseOffset.y);
        float mid = Noise(localX * 0.075f - noiseOffset.y, localZ * 0.075f + noiseOffset.x);
        float fine = Noise(localX * 0.24f + seed * 0.013f, localZ * 0.24f - seed * 0.017f);
        float ridgeNoise = Noise(localX * 0.055f - 91.3f, localZ * 0.055f + 44.8f);
        float ridges = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
        float terrain = (low - 0.5f) * 0.9f + (mid - 0.5f) * 0.45f + (fine - 0.5f) * 0.12f + ridges * 0.28f;
        float distanceFromSpawn = new Vector2(localX, localZ).magnitude;
        float spawnBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(safeRadius, safeRadius + safeFalloff, distanceFromSpawn));

        return terrain * heightScale * spawnBlend;
    }

    private float Noise(float x, float y)
    {
        return Mathf.Clamp01(Mathf.PerlinNoise(x, y));
    }

    private Material CreateGroundMaterial()
    {
        Texture2D texture = CreateDirtTexture();
        Material material = CreateLitMaterial(GroundMaterialName, Color.white);

        AssignTexture(material, texture, terrainSize / 7f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        return material;
    }

    private Texture2D CreateDirtTexture()
    {
        const int textureSize = 512;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true)
        {
            name = "Generated Procedural Dirt Texture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 6,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float u = x / (float)textureSize;
                float v = y / (float)textureSize;
                float broad = Noise(u * 7.5f + seed * 0.01f, v * 7.5f - seed * 0.013f);
                float grain = Noise(u * 42f - 11.2f, v * 42f + 19.6f);
                float pebble = Mathf.Pow(Noise(u * 118f + 5.4f, v * 118f - 2.1f), 7f);
                float crack = Mathf.Pow(1f - Mathf.Abs(Noise(u * 18f - 43.7f, v * 18f + 63.1f) * 2f - 1f), 5f);

                Color color = Color.Lerp(darkDirt, dryDirt, Mathf.Clamp01(broad * 0.65f + grain * 0.3f));
                color = Color.Lerp(color, rockColor, Mathf.Clamp01((pebble - 0.38f) * 1.8f));
                color = Color.Lerp(color, darkDirt * 0.72f, Mathf.Clamp01(crack * 0.35f));
                pixels[x + y * textureSize] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    private void RebuildDetails()
    {
        ClearGeneratedDetails();

        GameObject root = new GameObject(DetailRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        System.Random random = new System.Random(seed);
        Mesh rockMesh = CreateRockMesh(random);
        Mesh grassMesh = CreateGrassMesh(random);
        Material rockMaterial = CreateLitMaterial(RockMaterialName, rockColor);
        Material grassMaterial = CreateLitMaterial(GrassMaterialName, grassColor);

        if (grassMaterial.HasProperty("_Cull"))
            grassMaterial.SetFloat("_Cull", (float)CullMode.Off);

        SpawnRocks(root.transform, rockMesh, rockMaterial, random);
        SpawnGrass(root.transform, grassMesh, grassMaterial, random);
    }

    private void SpawnRocks(Transform root, Mesh rockMesh, Material rockMaterial, System.Random random)
    {
        int spawned = 0;
        int attempts = 0;
        while (spawned < rockCount && attempts < rockCount * 10)
        {
            attempts++;
            Vector2 point = RandomTerrainPoint(random);
            if (point.magnitude < detailAvoidRadius)
                continue;

            float height = SampleHeight(point.x, point.y);
            GameObject rock = new GameObject($"Rock {spawned + 1:00}");
            rock.transform.SetParent(root, false);
            rock.transform.localPosition = new Vector3(point.x, height - 0.04f, point.y);
            rock.transform.localRotation = Quaternion.Euler(RandomRange(random, -6f, 6f), RandomRange(random, 0f, 360f), RandomRange(random, -6f, 6f));

            float scale = RandomRange(random, 0.38f, 1.25f);
            rock.transform.localScale = new Vector3(scale * RandomRange(random, 0.85f, 1.35f), scale * RandomRange(random, 0.45f, 0.95f), scale * RandomRange(random, 0.85f, 1.4f));

            rock.AddComponent<MeshFilter>().sharedMesh = rockMesh;
            MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rockMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (addDetailColliders && addRockColliders)
            {
                MeshCollider collider = rock.AddComponent<MeshCollider>();
                collider.sharedMesh = rockMesh;
            }

            spawned++;
        }
    }

    private void SpawnGrass(Transform root, Mesh grassMesh, Material grassMaterial, System.Random random)
    {
        int spawned = 0;
        int attempts = 0;
        while (spawned < grassTuftCount && attempts < grassTuftCount * 12)
        {
            attempts++;
            Vector2 point = RandomTerrainPoint(random);
            if (point.magnitude < detailAvoidRadius || EstimateSlope(point.x, point.y) > 0.38f)
                continue;

            float height = SampleHeight(point.x, point.y);
            GameObject grass = new GameObject($"Grass Tuft {spawned + 1:000}");
            grass.transform.SetParent(root, false);
            grass.transform.localPosition = new Vector3(point.x, height + 0.01f, point.y);
            grass.transform.localRotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);

            float scale = RandomRange(random, 0.75f, 1.45f);
            grass.transform.localScale = new Vector3(scale, RandomRange(random, 0.7f, 1.35f), scale);

            grass.AddComponent<MeshFilter>().sharedMesh = grassMesh;
            MeshRenderer renderer = grass.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = grassMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (addDetailColliders && addPlantColliders)
            {
                CapsuleCollider collider = grass.AddComponent<CapsuleCollider>();
                collider.direction = 1;
                collider.center = new Vector3(0f, plantColliderHeight * 0.5f, 0f);
                collider.height = plantColliderHeight;
                collider.radius = plantColliderRadius;
            }

            spawned++;
        }
    }

    private Vector2 RandomTerrainPoint(System.Random random)
    {
        float half = terrainSize * 0.48f;
        return new Vector2(RandomRange(random, -half, half), RandomRange(random, -half, half));
    }

    private float EstimateSlope(float localX, float localZ)
    {
        const float delta = 0.85f;
        float dx = SampleHeight(localX + delta, localZ) - SampleHeight(localX - delta, localZ);
        float dz = SampleHeight(localX, localZ + delta) - SampleHeight(localX, localZ - delta);
        return Mathf.Sqrt(dx * dx + dz * dz) / (delta * 2f);
    }

    private Mesh CreateRockMesh(System.Random random)
    {
        const int rings = 4;
        const int segments = 9;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        vertices.Add(new Vector3(0f, 0.58f, 0f));
        for (int ring = 1; ring <= rings; ring++)
        {
            float v = ring / (float)(rings + 1);
            float angle = v * Mathf.PI;
            float y = Mathf.Cos(angle) * 0.58f;
            float radius = Mathf.Sin(angle) * 0.68f;

            for (int segment = 0; segment < segments; segment++)
            {
                float segmentAngle = segment / (float)segments * Mathf.PI * 2f;
                float jitter = RandomRange(random, 0.78f, 1.22f);
                vertices.Add(new Vector3(Mathf.Cos(segmentAngle) * radius * jitter, y * RandomRange(random, 0.82f, 1.12f), Mathf.Sin(segmentAngle) * radius * jitter));
            }
        }
        int bottomIndex = vertices.Count;
        vertices.Add(new Vector3(0f, -0.42f, 0f));

        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(0);
            triangles.Add(1 + next);
            triangles.Add(1 + segment);
        }

        for (int ring = 0; ring < rings - 1; ring++)
        {
            int ringStart = 1 + ring * segments;
            int nextRingStart = ringStart + segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(ringStart + segment);
                triangles.Add(ringStart + next);
                triangles.Add(nextRingStart + segment);
                triangles.Add(nextRingStart + segment);
                triangles.Add(ringStart + next);
                triangles.Add(nextRingStart + next);
            }
        }

        int lastRingStart = 1 + (rings - 1) * segments;
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(bottomIndex);
            triangles.Add(lastRingStart + segment);
            triangles.Add(lastRingStart + next);
        }

        Mesh mesh = new Mesh { name = RockMeshName };
        mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh CreateGrassMesh(System.Random random)
    {
        const int bladeCount = 11;
        List<Vector3> vertices = new List<Vector3>(bladeCount * 3);
        List<int> triangles = new List<int>(bladeCount * 3);

        for (int i = 0; i < bladeCount; i++)
        {
            float angle = i / (float)bladeCount * Mathf.PI * 2f + RandomRange(random, -0.25f, 0.25f);
            Vector3 forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 side = new Vector3(-forward.z, 0f, forward.x);
            Vector3 baseCenter = forward * RandomRange(random, 0.02f, 0.16f);
            float width = RandomRange(random, 0.025f, 0.06f);
            float height = RandomRange(random, 0.42f, 0.82f);
            Vector3 bend = forward * RandomRange(random, 0.03f, 0.16f);
            int start = vertices.Count;

            vertices.Add(baseCenter - side * width);
            vertices.Add(baseCenter + Vector3.up * height + bend);
            vertices.Add(baseCenter + side * width);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        Mesh mesh = new Mesh { name = GrassMeshName };
        mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material CreateLitMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        SetMaterialColor(material, color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.28f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void AssignTexture(Material material, Texture texture, float scale)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(scale, scale));
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", new Vector2(scale, scale));
        }
    }

    private void ClearGeneratedDetails()
    {
        Transform root = transform.Find(DetailRootName);
        if (root == null)
            return;

        DestroyGeneratedResources(root);
        DestroyGeneratedObject(root.gameObject);
    }

    private void DestroyGeneratedResources(Transform root)
    {
        HashSet<Mesh> meshes = new HashSet<Mesh>();
        HashSet<Material> materials = new HashSet<Material>();

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;
            if (mesh != null && (mesh.name == RockMeshName || mesh.name == GrassMeshName))
                meshes.Add(mesh);
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] sharedMaterials = renderers[i].sharedMaterials;
            for (int j = 0; j < sharedMaterials.Length; j++)
            {
                Material material = sharedMaterials[j];
                if (material != null && (material.name == RockMaterialName || material.name == GrassMaterialName))
                    materials.Add(material);
            }
        }

        foreach (Mesh mesh in meshes)
            DestroyGeneratedObject(mesh);
        foreach (Material material in materials)
            DestroyGeneratedMaterial(material);
    }

    private void DestroyGeneratedMesh(Mesh mesh)
    {
        if (mesh != null && mesh.name == TerrainMeshName)
            DestroyGeneratedObject(mesh);
    }

    private void DestroyGeneratedMaterial(Material material)
    {
        if (material == null || (material.name != GroundMaterialName && material.name != RockMaterialName && material.name != GrassMaterialName))
            return;

        Texture mainTexture = null;
        if (material.HasProperty("_BaseMap"))
            mainTexture = material.GetTexture("_BaseMap");
        else if (material.HasProperty("_MainTex"))
            mainTexture = material.GetTexture("_MainTex");

        DestroyGeneratedObject(mainTexture);
        DestroyGeneratedObject(material);
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

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
