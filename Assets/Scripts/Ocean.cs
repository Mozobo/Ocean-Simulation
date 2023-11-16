using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Ocean : MonoBehaviour
{
    [SerializeField, Range(1, 1000)]
    public int size = 2;
    public int texturesSize = 256;


    //Ocean parameters
    public float windSpeed = 1.0f;
    public Vector2 windDirection = new Vector2(1.0f, 1.0f);
    public float gravity = 9.81f;
    public float fetch = 1.0f;
    public float depth = 4.0f;

    private Vector3[] vertices;
    private int[] triangles;
    private Mesh mesh;

    public Material material;

    public ComputeShader initialSpectrumComputeShader;
    public ComputeShader TimeDependentSpectrumComputeShader;
    public ComputeShader IFFTComputeShader;
    public ComputeShader ResultTexturesFillerComputeShader;
    private Texture2D randomNoiseTexture;

    public OceanCascade oceanCascade0;
    //public OceanCascade oceanCascade1;
    //public OceanCascade oceanCascade2;


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
    private void GenerateRandomNoiseTexture(){
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

        randomNoiseTexture = noiseTexture;
    }

    private void InitializeCascade(OceanCascade cascade){
        cascade.setVariables(texturesSize, windSpeed, windDirection, gravity, fetch, depth, initialSpectrumComputeShader, TimeDependentSpectrumComputeShader, IFFTComputeShader, ResultTexturesFillerComputeShader, randomNoiseTexture);
        cascade.InitialCalculations();
    }

    void Awake(){
        GenerateWaterPlane();
        GenerateRandomNoiseTexture();
        InitializeCascade(oceanCascade0);
        //oceanCascade0.CalculateWavesTexturesAtTime(1.0f);
        material.SetTexture("_DisplacementsC0Sampler", oceanCascade0.DisplacementsTexture, UnityEngine.Rendering.RenderTextureSubElement.Color);
        material.SetTexture("_DerivativesC0Sampler", oceanCascade0.DerivativesTexture, UnityEngine.Rendering.RenderTextureSubElement.Color);
        material.SetFloat("_C0LengthScale", oceanCascade0.lengthScale);
        //InitializeCascade(oceanCascade1);
        //InitializeCascade(oceanCascade2);
    }

    void Update(){
        oceanCascade0.CalculateWavesTexturesAtTime(Time.time);
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