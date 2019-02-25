using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AnimatorTextureBaker : MonoBehaviour
{
    public struct VertInfo
    {
        public Vector3 position;
        public Vector3 normal;
    }

    public ComputeShader infoTextureGenerator;



    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(BakeAnimation());
    }

    private IEnumerator BakeAnimation()
    {
        var animator = GetComponent<Animator>();

        var skin = GetComponentInChildren<SkinnedMeshRenderer>();
        var vertCount = skin.sharedMesh.vertexCount;

        var mesh = new Mesh();
        animator.speed = 0f;

        var textureWidth = Mathf.NextPowerOfTwo(vertCount);

        foreach(var clip in animator.runtimeAnimatorController.animationClips)
        {
            var frames = Mathf.NextPowerOfTwo((int)(clip.length / .05f));
            var info = new List<VertInfo>();

            var positionRenderTexture = new RenderTexture(textureWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            var normalRenderTexture = new RenderTexture(textureWidth, frames, 0, RenderTextureFormat.ARGBHalf);

            positionRenderTexture.name = $"{name}.{clip.name}.positionText";
            normalRenderTexture.name = $"{name}.{clip.name}.normalText";

            foreach(var renderTexture in new[] { positionRenderTexture, normalRenderTexture })
            {
                renderTexture.enableRandomWrite = true;
                renderTexture.Create();

                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);
            }

            animator.Play(clip.name);
            yield return 0;

            for(int i = 0; i < frames; i++)
            {
                animator.Play(clip.name);
                yield return 0;

                skin.BakeMesh(mesh);

                info.AddRange(Enumerable.Range(0, vertCount).Select(idx => new VertInfo
                {
                    position = mesh.vertices[idx],
                    normal = mesh.normals[idx]
                }));
            }

            var buffer = new ComputeBuffer(info.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(info);

            var kernel = infoTextureGenerator.FindKernel("CSMain");

            uint x, y, z;
            infoTextureGenerator.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

            infoTextureGenerator.SetInt("vertCount", vertCount);
            infoTextureGenerator.SetBuffer(kernel, "meshInfo", buffer);
            infoTextureGenerator.SetTexture(kernel, "OutPosition", positionRenderTexture);
            infoTextureGenerator.SetTexture(kernel, "OutNormal", normalRenderTexture);

            infoTextureGenerator.Dispatch(kernel, vertCount / (int)x + 1, frames / (int)y + 1, (int)z);
            buffer.Release();

#if UNITY_EDITOR
            var positionTexture = Convert(positionRenderTexture);
            var normalTexture = Convert(normalRenderTexture);

            Graphics.CopyTexture(positionRenderTexture, positionTexture);
            Graphics.CopyTexture(normalRenderTexture, normalTexture);

            AssetDatabase.CreateAsset(positionTexture, Path.Combine("Assets", positionRenderTexture.name + ".asset"));
            AssetDatabase.CreateAsset(positionTexture, Path.Combine("Assets", normalRenderTexture.name + ".asset"));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }


        yield return null;
    }

    public Texture2D Convert(RenderTexture _renderTexture)
    {
        var texture = new Texture2D(_renderTexture.width, _renderTexture.height, TextureFormat.RGBAHalf, false);
        RenderTexture.active = _renderTexture;
        texture.ReadPixels(Rect.MinMaxRect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
        RenderTexture.active = null;
        return texture;
    }
}
