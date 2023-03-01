using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class InstancedSpriteRenderer : MonoBehaviour
{
    public Mesh quadMesh;

    public Vector4 pivot;
    public Vector4 newUV;

    private static Dictionary<Texture2D, int> s_textureIndexes = new();
    private static Texture2DArray s_spriteTextures;
    private static Material s_spriteMaterial;
    private static int s_spriteTextureCount = 0;

    private MaterialPropertyBlock props;
    private int textureID = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        s_textureIndexes = new();
        s_spriteTextures = null;
        s_spriteMaterial = null;
        s_spriteTextureCount = 0;
    }

    private void Start()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == false)
            return;

        // Sprite instancing implementation
        if (spriteRenderer.sprite.texture.width != 512) // Texture2DArray needs same size and format
            return;

        Texture2D tex = spriteRenderer.sprite.texture;

        if (s_spriteTextures == false)
        {
            s_spriteTextures = new Texture2DArray(tex.width, tex.height, 128, tex.format, false);
            s_spriteTextureCount = 0;
        }

        props ??= new MaterialPropertyBlock();

        if (s_textureIndexes.TryGetValue(tex, out textureID) == false)
        {
            Graphics.CopyTexture(tex, 0, 0, s_spriteTextures, s_spriteTextureCount, 0);
            textureID = s_spriteTextureCount;
            s_textureIndexes[tex] = textureID;
            s_spriteTextureCount++;
        }

        Sprite sprite = spriteRenderer.sprite;

        DestroyImmediate(spriteRenderer);

        var go = this.gameObject;

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.enabled = true;

        if (s_spriteMaterial == false)
        {
            var materialPrefab = Resources.Load<Material>("InstancedSprite");
            s_spriteMaterial = new Material(materialPrefab);
            s_spriteMaterial.SetTexture("_Textures", s_spriteTextures);
        }

        meshRenderer.sharedMaterial = s_spriteMaterial;
        meshFilter.sharedMesh = quadMesh;

        // Calculate vertices translate and scale value
        pivot.x = sprite.rect.width / sprite.pixelsPerUnit;
        pivot.y = sprite.rect.height / sprite.pixelsPerUnit;
        pivot.z = ((sprite.rect.width / 2) - sprite.pivot.x) / sprite.pixelsPerUnit;
        pivot.w = ((sprite.rect.height / 2) - sprite.pivot.y) / sprite.pixelsPerUnit;

        // Calculate uv translate and scale value
        newUV.x = sprite.uv[1].x - sprite.uv[0].x;
        newUV.y = sprite.uv[0].y - sprite.uv[2].y;
        newUV.z = sprite.uv[2].x;
        newUV.w = sprite.uv[2].y;

        var positions = new Vector3[1];
        positions[0] = transform.position;
        var lightProbes = new UnityEngine.Rendering.SphericalHarmonicsL2[1];
        var occlusionProbes = new Vector4[1];

        LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, lightProbes, occlusionProbes);

        // Set MaterialPropertyBlock
        props.CopySHCoefficientArraysFrom(lightProbes);
        props.CopyProbeOcclusionArrayFrom(occlusionProbes);
        meshRenderer.lightProbeUsage = LightProbeUsage.CustomProvided;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        props.SetFloat("_TextureIndex", textureID);
        props.SetVector("_Pivot", pivot);
        props.SetVector("_NewUV", newUV);
        meshRenderer.SetPropertyBlock(props);
    }
}
