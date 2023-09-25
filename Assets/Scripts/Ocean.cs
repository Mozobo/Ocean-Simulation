using System.Collections;
using System.Collections.Generic;
using UnityEngine;
﻿using UnityEditor;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Ocean : MonoBehaviour
{
    [SerializeField, Range(1, 1000)]
    public int size = 2;
    public int texturesSize = 256;
    public float lengthScale = 1.0f;


    //Ocean parameters
    public float windSpeed = 1.0f;
    public Vector2 windDirection = new Vector2(1.0f, 1.0f);
    public float gravity = 9.81f;
    public float fetch = 1.0f;
    public float depth = 4.0f;
    public float cutoffHigh = 1.0f;
    public float cutoffLow = 0.1f;

    private Vector3[] vertices;
    private int[] triangles;
    private Mesh mesh;

    private const string texturesPath = "Assets/Textures/";

    public ComputeShader initialSpectrumComputeShader;
    private Texture2D randomNoiseTexture;
    private RenderTexture initialSpectrumTexture;
    private RenderTexture WavesDataTexture;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    int KERNEL_INITIAL_SPECTRUM;
    int KERNEL_CONJUGATED_SPECTRUM;


    private void GenerateVertices(){
        vertices = new Vector3[(size + 1) * (size + 1)];

		for (int i = 0, z = 0; z <= size; z++) {
			for (int x = 0; x <= size; x++, i++) {
				vertices[i] = new Vector3(x, 0, z);
			}
		}

        mesh.vertices = vertices;
    }

    private void GenerateTriangles(){
        int[] triangles = new int[size * size * 6];

		for (int ti = 0, vi = 0, z = 0; z < size; z++, vi++) {
			for (int x = 0; x < size; x++, ti += 6, vi++) {
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + size + 1;
				triangles[ti + 5] = vi + size + 2;
			}
		}

        mesh.triangles = triangles;
    }

    private void GenerateWaterPlane(){
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "Procedural Grid";

        GenerateVertices();
        GenerateTriangles();
    }

    private Texture2D CreateTexture2D(){
        return new Texture2D(texturesSize, texturesSize, TextureFormat.RGFloat, false, true);
    } 

    private RenderTexture CreateRenderTexture(){
        RenderTexture rt = new RenderTexture(texturesSize, texturesSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.sRGB);
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    // Generates a random number from a Normal Distribution N(0, 1)
    // Extracted from: https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/
    private float GenerateRandomNumber(){
            float v1, v2, s;
            do {
                v1 = 2.0f * Random.Range(0f,1f) - 1.0f;
                v2 = 2.0f * Random.Range(0f,1f) - 1.0f;
                s = v1 * v1 + v2 * v2;
            } while (s >= 1.0f || s == 0f);
            s = Mathf.Sqrt((-2.0f * Mathf.Log(s)) / s);
        
            return v1 * s;
    }

    // Generates a 2D Texture where each pixel contains a Vector4 on x and y are random numbers from -1 to 1 and z and w are 0
    // This texture is generated on the CPU because we don't need to generate new random noise when the ocean parameters change
    // So this texture is generated only once and at the start of the game execution
    private Texture2D GenerateRandomNoiseTexture(){
        Texture2D noiseTexture = CreateTexture2D();

        noiseTexture.filterMode = FilterMode.Point;
        for (int i = 0; i < texturesSize; i++)
        {
            for (int j = 0; j < texturesSize; j++)
            {
                noiseTexture.SetPixel(i, j, new Vector4(GenerateRandomNumber(), GenerateRandomNumber()));
            }
        }
        noiseTexture.Apply();

        #if UNITY_EDITOR
            string filename = "RandomNoiseTexture" + texturesSize.ToString() + "x" + texturesSize.ToString()+ ".asset";
            AssetDatabase.CreateAsset(noiseTexture, texturesPath + filename);
        #endif

        return noiseTexture;
    }

    // If there already exists a Random Noise texture, returns it
    // else generates a new texture
    private void GetRandomNoiseTexture(){
        string filename = "RandomNoiseTexture" + texturesSize.ToString() + "x" + texturesSize.ToString() + ".asset";
        #if UNITY_EDITOR
            Texture2D noiseTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(texturesPath + filename, typeof(Texture2D));
        #endif
        randomNoiseTexture = noiseTexture ? noiseTexture : GenerateRandomNoiseTexture();
    }

    private void CalculateInitialSpectrumTexture(){
        // Calculate the initial spectrum H0(K)
        KERNEL_INITIAL_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateInitialSpectrumTexture");
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_RandomNoise", randomNoiseTexture);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_InitialSpectrumTexture", initialSpectrumTexture);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_WavesDataTexture", WavesDataTexture);
        initialSpectrumComputeShader.SetFloat("_LengthScale", lengthScale);
        initialSpectrumComputeShader.SetFloat("_WindSpeed", windSpeed);
        initialSpectrumComputeShader.SetFloat("_WindDirectionX", windDirection.x);
        initialSpectrumComputeShader.SetFloat("_WindDirectionY", windDirection.y);
        initialSpectrumComputeShader.SetFloat("_Gravity", gravity);
        initialSpectrumComputeShader.SetFloat("_Fetch", fetch);
        initialSpectrumComputeShader.SetFloat("_CutoffHigh", cutoffHigh);
        initialSpectrumComputeShader.SetFloat("_CutoffLow", cutoffLow);
        initialSpectrumComputeShader.SetFloat("_Depth", depth);
        initialSpectrumComputeShader.Dispatch(KERNEL_INITIAL_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        // Store, in each element on the texture, the value of the complex conjugate element
        // Now the Initial spectrum texture stores H0(K) and H0(-k)*
        KERNEL_CONJUGATED_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateConjugatedInitialSpectrumTexture");
        initialSpectrumComputeShader.SetTexture(KERNEL_CONJUGATED_SPECTRUM, "_InitialSpectrumTexture", initialSpectrumTexture);
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.Dispatch(KERNEL_CONJUGATED_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);
    }

    private void GetInitialSpectrumTexture(){
        initialSpectrumTexture = CreateRenderTexture();
        CalculateInitialSpectrumTexture();
    }

    private void GetWavesDataTexture(){
        WavesDataTexture = CreateRenderTexture();
    }

    void Awake(){
        GenerateWaterPlane();
        GetRandomNoiseTexture();
        GetWavesDataTexture();
        GetInitialSpectrumTexture();
    }

    // Uncomment this function to visualize vertices
    /*private void OnDrawGizmos () {
        if (vertices == null) {
			return;
		}
		Gizmos.color = Color.black;
		for (int i = 0; i < vertices.Length; i++) {
			Gizmos.DrawSphere(vertices[i], 0.1f);
		}
	}*/
    
}