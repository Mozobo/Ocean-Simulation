using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterBody : MonoBehaviour
{
    // Plane variables
    [SerializeField, Range(1, 10000)]
    public int planeSize = 2;
    // Reduces the number of vertices used to create a mesh of given size
    [SerializeField, Range(1, 100)]
    public int trianglesSize = 2;
    private Vector3[] vertices;
    private int[] triangles;
    private Mesh mesh;
    // ---------------------------


    // Ocean parameters
    public float windSpeed = 1.0f;
    public Vector2 windDirection = new Vector2(1.0f, 1.0f);
    public float gravity = 9.81f;
    public float fetch = 1.0f;
    public float depth = 4.0f;
    // ---------------------------

    
    public Material material;

    public int texturesSize = 256;
    public ComputeShader initialSpectrumComputeShader;
    public ComputeShader timeDependentSpectrumComputeShader;
    public ComputeShader IFFTComputeShader;
    public ComputeShader resultTexturesFillerComputeShader;
    private Texture2D randomNoiseTexture;

    private IFFT IFFT;
    private RenderTexture initialSpectrumTextures;
    private RenderTexture wavesDataTextures;
    private RenderTexture DxDzTextures;
    private RenderTexture DyDxzTextures;
    private RenderTexture DyxDyzTextures;
    private RenderTexture DxxDzzTextures;

    public WaterCascade[] cascades;

    private float[] waveLengths;
    ComputeBuffer waveLengthsBuffer;
    private float[] cutoffs;
    ComputeBuffer cutoffsBuffer;
    private RenderTexture displacementsTextures;
    private RenderTexture derivativesTextures;
    private RenderTexture turbulenceTextures;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    int KERNEL_INITIAL_SPECTRUM;
    int KERNEL_CONJUGATED_SPECTRUM;
    int KERNEL_TIME_DEPENDENT_SPECTRUM;
    int KERNEL_RESULT_TEXTURES_FILLER;


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
        Texture2D noiseTexture = new Texture2D(texturesSize, texturesSize, TextureFormat.RGFloat, false, true);

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

    private RenderTexture CreateRenderTextureArray(int arrayDepth, RenderTextureFormat format, bool useMips){
        RenderTexture rt = new RenderTexture(texturesSize, texturesSize, 0, format, RenderTextureReadWrite.Linear);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        rt.volumeDepth = arrayDepth;
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 16;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private RenderTexture CreateRGRenderTextureArray(int arrayDepth, bool useMips = false) {
        return CreateRenderTextureArray(arrayDepth, RenderTextureFormat.RGFloat, useMips);
    }

    private RenderTexture CreateRGBARenderTextureArray(int arrayDepth, bool useMips = false) {
        return CreateRenderTextureArray(arrayDepth, RenderTextureFormat.ARGBFloat, useMips);
    }

    private void CalculateInitialSpectrumTextures(){
        waveLengthsBuffer = new ComputeBuffer(cascades.Length, 4, ComputeBufferType.Default);
        waveLengthsBuffer.SetData(waveLengths);
        cutoffsBuffer = new ComputeBuffer(cascades.Length * 2, 4, ComputeBufferType.Default);
        cutoffsBuffer.SetData(cutoffs);

        // Calculate the initial spectrum H0(K)
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.SetInt("_NbCascades", cascades.Length);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_RandomNoise", randomNoiseTexture);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_InitialSpectrumTextures", initialSpectrumTextures);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_WavesDataTextures", wavesDataTextures);
        initialSpectrumComputeShader.SetBuffer(KERNEL_INITIAL_SPECTRUM, "_WaveLengths", waveLengthsBuffer);
        initialSpectrumComputeShader.SetBuffer(KERNEL_INITIAL_SPECTRUM, "_Cutoffs", cutoffsBuffer);
        initialSpectrumComputeShader.SetFloat("_WindSpeed", windSpeed);
        initialSpectrumComputeShader.SetFloat("_WindDirectionX", windDirection.x);
        initialSpectrumComputeShader.SetFloat("_WindDirectionY", windDirection.y);
        initialSpectrumComputeShader.SetFloat("_Gravity", gravity);
        initialSpectrumComputeShader.SetFloat("_Fetch", fetch);
        initialSpectrumComputeShader.SetFloat("_Depth", depth);
        initialSpectrumComputeShader.Dispatch(KERNEL_INITIAL_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        // Store, in each element on the texture, the value of the complex conjugate element
        // Now the Initial spectrum texture stores H0(K) and H0(-k)*
        initialSpectrumComputeShader.SetTexture(KERNEL_CONJUGATED_SPECTRUM, "_InitialSpectrumTextures", initialSpectrumTextures);
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.SetInt("_NbCascades", cascades.Length);
        initialSpectrumComputeShader.Dispatch(KERNEL_CONJUGATED_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);
    }

    public void CalculateWavesTexturesAtTime(float time) {
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_ConjugatedInitialSpectrumTextures", initialSpectrumTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_WavesDataTextures", wavesDataTextures);
        timeDependentSpectrumComputeShader.SetFloat("_Time", time);
        timeDependentSpectrumComputeShader.SetInt("_NbCascades", cascades.Length);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DxDzTextures", DxDzTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DyDxzTextures", DyDxzTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DyxDyzTextures", DyxDyzTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DxxDzzTextures", DxxDzzTextures);
        timeDependentSpectrumComputeShader.Dispatch(KERNEL_TIME_DEPENDENT_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        IFFT.InverseFastFourierTransform(DxDzTextures);
        IFFT.InverseFastFourierTransform(DyDxzTextures);
        IFFT.InverseFastFourierTransform(DyxDyzTextures);
        IFFT.InverseFastFourierTransform(DxxDzzTextures);

        resultTexturesFillerComputeShader.SetInt("_NbCascades", cascades.Length);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DxDzTextures", DxDzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DyDxzTextures", DyDxzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DyxDyzTextures", DyxDyzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DxxDzzTextures", DxxDzzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DisplacementsTextures", displacementsTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DerivativesTextures", derivativesTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_TurbulenceTextures", turbulenceTextures);
        resultTexturesFillerComputeShader.Dispatch(KERNEL_RESULT_TEXTURES_FILLER, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        derivativesTextures.GenerateMips();
        turbulenceTextures.GenerateMips();
    }

    void Awake(){
        GenerateWaterPlane();
        GenerateRandomNoiseTexture();

        IFFT = new IFFT(IFFTComputeShader, texturesSize, cascades.Length);

        KERNEL_INITIAL_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateInitialSpectrumTexture");
        KERNEL_CONJUGATED_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateConjugatedInitialSpectrumTexture");
        KERNEL_TIME_DEPENDENT_SPECTRUM = timeDependentSpectrumComputeShader.FindKernel("CalculateTimeDependentComplexAmplitudesAndDerivatives");
        KERNEL_RESULT_TEXTURES_FILLER = resultTexturesFillerComputeShader.FindKernel("FillResultTextures");

        wavesDataTextures = CreateRGBARenderTextureArray(cascades.Length);
        initialSpectrumTextures = CreateRGBARenderTextureArray(cascades.Length);
        DxDzTextures = CreateRGRenderTextureArray(cascades.Length);
        DyDxzTextures = CreateRGRenderTextureArray(cascades.Length);
        DyxDyzTextures = CreateRGRenderTextureArray(cascades.Length);
        DxxDzzTextures = CreateRGRenderTextureArray(cascades.Length);
        displacementsTextures = CreateRGBARenderTextureArray(cascades.Length);
        derivativesTextures = CreateRGBARenderTextureArray(cascades.Length, true);
        turbulenceTextures = CreateRGBARenderTextureArray(cascades.Length, true);

        waveLengths = new float[cascades.Length];
        cutoffs = new float[cascades.Length * 2];

        for(int i = 0; i < cascades.Length; i++) {
            waveLengths[i] = cascades[i].waveLength;
            cutoffs[i*2] = cascades[i].cutoffLow;
            cutoffs[i*2 + 1] = cascades[i].cutoffHigh;
        }

        CalculateInitialSpectrumTextures();

        material.SetInt("_NbCascades", cascades.Length);
        material.SetTexture("_DisplacementsTextures", displacementsTextures);
        material.SetTexture("_DerivativesTextures", derivativesTextures);
        material.SetTexture("_TurbulenceTextures", turbulenceTextures);
        material.SetFloatArray("_WaveLengths", waveLengths);
    }

    void Update(){
        CalculateWavesTexturesAtTime(Time.time);
    }

    /* Prevent leaks from the Buffers */
    void OnDisable () {
		waveLengthsBuffer.Release();
        waveLengths = null;
        cutoffsBuffer.Release();
        cutoffsBuffer = null;
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