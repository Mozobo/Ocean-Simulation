using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterBody : MonoBehaviour
{
    [SerializeField, Range(1, 10000)]
    public int planeSize = 2;
    // Reduces the number of vertices used to create a mesh of given size
    [SerializeField, Range(1, 100)]
    public int trianglesSize = 2;
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

    public WaterCascade oceanCascade0;
    //public OceanCascade oceanCascade1;
    //public OceanCascade oceanCascade2;

    public WaterCascade[] cascades;


    private void GenerateVertices(){
        int verticesPerRow = planeSize / trianglesSize;
        float halfLength = planeSize * 0.5f;
        float spacing = planeSize / (float)verticesPerRow;

        vertices = new Vector3[(verticesPerRow + 1) * (verticesPerRow + 1)];
        Vector3[] normals = new Vector3[(verticesPerRow + 1) * (verticesPerRow + 1)];

		for (int i = 0, z = 0; z <= verticesPerRow; z++) {
			for (int x = 0; x <= verticesPerRow; x++, i++) {
				vertices[i] = new Vector3((float)x * spacing - halfLength, 0, (float)z * spacing - halfLength);
                normals[i] = Vector3.up;
			}
		}

        mesh.vertices = vertices;
    }

    private void GenerateTriangles(){
        int verticesPerRow = planeSize / trianglesSize;
        int[] triangles = new int[verticesPerRow  * verticesPerRow  * 6];

		for (int ti = 0, vi = 0, z = 0; z < verticesPerRow ; z++, vi++) {
			for (int x = 0; x < verticesPerRow ; x++, ti += 6, vi++) {
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + verticesPerRow  + 1;
				triangles[ti + 5] = vi + verticesPerRow  + 2;
			}
		}

        mesh.triangles = triangles;
    }

    private void GenerateWaterPlane(){
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "Procedural Water plane";
        // This is important so we can generate a plane with more than 65.536 vertices
        mesh.indexFormat = IndexFormat.UInt32;

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

    private void InitializeCascade(WaterCascade cascade){
        cascade.setVariables(texturesSize, windSpeed, windDirection, gravity, fetch, depth, initialSpectrumComputeShader, TimeDependentSpectrumComputeShader, IFFTComputeShader, ResultTexturesFillerComputeShader, randomNoiseTexture);
        cascade.InitialCalculations();
    }

    void Awake(){
        GenerateWaterPlane();
        GenerateRandomNoiseTexture();
        for(int i = 0; i < cascades.Length; i++) {
            InitializeCascade(cascades[i]);
        }
        material.SetTexture("_DisplacementsC0Sampler", oceanCascade0.DisplacementsTexture, UnityEngine.Rendering.RenderTextureSubElement.Color);
        material.SetTexture("_DerivativesC0Sampler", oceanCascade0.DerivativesTexture, UnityEngine.Rendering.RenderTextureSubElement.Color);
        material.SetTexture("_TurbulenceC0Sampler", oceanCascade0.TurbulenceTexture, UnityEngine.Rendering.RenderTextureSubElement.Color);
        material.SetFloat("_C0LengthScale", oceanCascade0.lengthScale);
    }

    void Update(){
        for(int i = 0; i < cascades.Length; i++) {
            cascades[i].CalculateWavesTexturesAtTime(Time.time);
        }
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