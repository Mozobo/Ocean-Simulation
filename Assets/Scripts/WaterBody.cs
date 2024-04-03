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
    public ComputeShader TimeDependentSpectrumComputeShader;
    public ComputeShader IFFTComputeShader;
    public ComputeShader ResultTexturesFillerComputeShader;
    private Texture2D randomNoiseTexture;

    private IFFT IFFT;
    private RenderTexture initialSpectrumTextures;
    private RenderTexture WavesDataTextures;
    private RenderTexture DxDzTextures;
    private RenderTexture DyDxzTextures;
    private RenderTexture DyxDyzTextures;
    private RenderTexture DxxDzzTextures;

    public WaterCascade[] cascades;

    private float[] lengthScales;
    ComputeBuffer lengthScalesBuffer;
    private float[] cutoffs;
    ComputeBuffer cutoffsBuffer;
    private RenderTexture DisplacementsTextures;
    private RenderTexture DerivativesTextures;
    private RenderTexture TurbulenceTextures;

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
        lengthScalesBuffer = new ComputeBuffer(cascades.Length, 4, ComputeBufferType.Default);
        lengthScalesBuffer.SetData(lengthScales);
        cutoffsBuffer = new ComputeBuffer(cascades.Length * 2, 4, ComputeBufferType.Default);
        cutoffsBuffer.SetData(cutoffs);

        // Calculate the initial spectrum H0(K)
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.SetInt("_NbCascades", cascades.Length);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_RandomNoise", randomNoiseTexture);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_InitialSpectrumTextures", initialSpectrumTextures);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_WavesDataTextures", WavesDataTextures);
        initialSpectrumComputeShader.SetBuffer(KERNEL_INITIAL_SPECTRUM, "_LengthScales", lengthScalesBuffer);
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

    /*public void CalculateWavesTexturesAtTime(float time) {
        TimeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_ConjugatedInitialSpectrumTexture", initialSpectrumTexture);
        TimeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_WavesDataTexture", WavesDataTexture);
        TimeDependentSpectrumComputeShader.SetFloat("_Time", time);
        TimeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DxDzTexture", DxDzTexture);
        TimeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DyDxzTexture", DyDxzTexture);
        TimeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DyxDyzTexture", DyxDyzTexture);
        TimeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DxxDzzTexture", DxxDzzTexture);
        TimeDependentSpectrumComputeShader.Dispatch(KERNEL_TIME_DEPENDENT_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        IFFT.InverseFastFourierTransform(DxDzTexture);
        IFFT.InverseFastFourierTransform(DyDxzTexture);
        IFFT.InverseFastFourierTransform(DyxDyzTexture);
        IFFT.InverseFastFourierTransform(DxxDzzTexture);

        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DxDzTexture", DxDzTexture);
        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DyDxzTexture", DyDxzTexture);
        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DyxDyzTexture", DyxDyzTexture);
        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DxxDzzTexture", DxxDzzTexture);
        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DisplacementsTexture", DisplacementsTexture);
        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DerivativesTexture", DerivativesTexture);
        ResultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_TurbulenceTexture", TurbulenceTexture);
        ResultTexturesFillerComputeShader.Dispatch(KERNEL_RESULT_TEXTURES_FILLER, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        DerivativesTexture.GenerateMips();
        TurbulenceTexture.GenerateMips();
    }*/

    void Awake(){
        GenerateWaterPlane();
        GenerateRandomNoiseTexture();

        IFFT = new IFFT(IFFTComputeShader, texturesSize);

        KERNEL_INITIAL_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateInitialSpectrumTexture");
        KERNEL_CONJUGATED_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateConjugatedInitialSpectrumTexture");
        KERNEL_TIME_DEPENDENT_SPECTRUM = TimeDependentSpectrumComputeShader.FindKernel("CalculateTimeDependentComplexAmplitudesAndDerivatives");
        KERNEL_RESULT_TEXTURES_FILLER = ResultTexturesFillerComputeShader.FindKernel("FillResultTextures");

        WavesDataTextures = CreateRGBARenderTextureArray(cascades.Length);
        initialSpectrumTextures = CreateRGBARenderTextureArray(cascades.Length);
        DxDzTextures = CreateRGRenderTextureArray(cascades.Length);
        DyDxzTextures = CreateRGRenderTextureArray(cascades.Length);
        DyxDyzTextures = CreateRGRenderTextureArray(cascades.Length);
        DxxDzzTextures = CreateRGRenderTextureArray(cascades.Length);
        DisplacementsTextures = CreateRGBARenderTextureArray(cascades.Length);
        DerivativesTextures = CreateRGBARenderTextureArray(cascades.Length, true);
        TurbulenceTextures = CreateRGBARenderTextureArray(cascades.Length, true);

        lengthScales = new float[cascades.Length];
        cutoffs = new float[cascades.Length * 2];

        for(int i = 0; i < cascades.Length; i++) {
            lengthScales[i] = cascades[i].lengthScale;
            cutoffs[i*2] = cascades[i].cutoffLow;
            cutoffs[i*2 + 1] = cascades[i].cutoffHigh;
        }

        CalculateInitialSpectrumTextures();
    }

    void Update(){
        //CalculateWavesTexturesAtTime(Time.time);
    }

    /* Prevent leaks from the Buffers */
    void OnDisable () {
		lengthScalesBuffer.Release();
        lengthScales = null;
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