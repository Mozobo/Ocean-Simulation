using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class WaterBody : MonoBehaviour
{
    // Ocean parameters
    public float windSpeed = 1.0f;
    public Vector2 windDirection = new Vector2(1.0f, 1.0f);
    public float gravity = 9.81f;
    public float fetch = 1.0f;
    public float depth = 4.0f;
    public float swell = 0.4f;
    public float fade = 0.1f;
    // ---------------------------

    // Buoyancy parameters
    public float density = 1f;
    public float drag = 10f;
    public float angularDrag = 1f;
    // ---------------
    
    public Material material;

    private ReflectionProbe reflectionProbe;

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

    private RenderTexture displacementsTextures;
    private RenderTexture derivativesTextures;
    private RenderTexture turbulenceTextures;

    public WaterCascade[] cascades;
    private float[] wavelengths;
    ComputeBuffer wavelengthsBuffer;
    private float[] cutoffs;
    ComputeBuffer cutoffsBuffer;

    private Color[] buoyancyData;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    int KERNEL_INITIAL_SPECTRUM;
    int KERNEL_CONJUGATED_SPECTRUM;
    int KERNEL_TIME_DEPENDENT_SPECTRUM;
    int KERNEL_RESULT_TEXTURES_FILLER;


    // Generates a random number from a Normal Distribution N(0, 1)
    // Code source: https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/
    private float GenerateRandomNumber() {
            float v1, v2, s;
            do {
                v1 = 2.0f * Random.Range(0f,1f) - 1.0f;
                v2 = 2.0f * Random.Range(0f,1f) - 1.0f;
                s = v1 * v1 + v2 * v2;
            } while (s >= 1.0f || s == 0f);
            s = Mathf.Sqrt((-2.0f * Mathf.Log(s)) / s);
        
            return v1 * s;
    }

    // Generates a 2D Texture where each pixel contains a Vector4, x and y are random numbers from -1 to 1 and z and w are 0
    // This texture is generated on the CPU because we don't need to generate new random noise when the ocean parameters change
    // So this texture is generated only once and at the start of the game execution
    private void GenerateRandomNoiseTexture() {
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

    // Creates a RenderTexture variable configured as a 2D texture array that containts a specified number of textures with the specified format
    // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTexture.html
    private RenderTexture CreateRenderTextureArray(int arrayDepth, RenderTextureFormat format, bool useMips) {
        RenderTexture rt = new RenderTexture(texturesSize, texturesSize, 0, format, RenderTextureReadWrite.Linear);
        // This is key for the RenderTexture variable to behave like an array of textures instead of a single render texture
        // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.TextureDimension.html
        rt.dimension = TextureDimension.Tex2DArray;
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

    // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTextureFormat.RGFloat.html
    private RenderTexture CreateRGRenderTextureArray(int arrayDepth, bool useMips = false) {
        return CreateRenderTextureArray(arrayDepth, RenderTextureFormat.RGFloat, useMips);
    }

    // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTextureFormat.ARGBFloat.html
    private RenderTexture CreateRGBARenderTextureArray(int arrayDepth, bool useMips = false) {
        return CreateRenderTextureArray(arrayDepth, RenderTextureFormat.ARGBFloat, useMips);
    }

    private void InitializeInitialSpectrumComputeShader() {
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.SetInt("_NbCascades", cascades.Length);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_RandomNoiseTexture", randomNoiseTexture);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_InitialSpectrumTextures", initialSpectrumTextures);
        initialSpectrumComputeShader.SetTexture(KERNEL_INITIAL_SPECTRUM, "_WavesDataTextures", wavesDataTextures);
        initialSpectrumComputeShader.SetBuffer(KERNEL_INITIAL_SPECTRUM, "_Wavelengths", wavelengthsBuffer);
        initialSpectrumComputeShader.SetBuffer(KERNEL_INITIAL_SPECTRUM, "_Cutoffs", cutoffsBuffer);
        initialSpectrumComputeShader.SetFloat("_WindSpeed", windSpeed);
        initialSpectrumComputeShader.SetFloat("_WindDirectionX", windDirection.x);
        initialSpectrumComputeShader.SetFloat("_WindDirectionY", windDirection.y);
        initialSpectrumComputeShader.SetFloat("_Gravity", gravity);
        initialSpectrumComputeShader.SetFloat("_Fetch", fetch);
        initialSpectrumComputeShader.SetFloat("_Depth", depth);
        initialSpectrumComputeShader.SetFloat("_Fade", fade);
        initialSpectrumComputeShader.SetFloat("_Swell", swell);

        initialSpectrumComputeShader.SetTexture(KERNEL_CONJUGATED_SPECTRUM, "_InitialSpectrumTextures", initialSpectrumTextures);
    }

    private void InitializeTimeDependentSpectrumComputeShader() {
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_ConjugatedInitialSpectrumTextures", initialSpectrumTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_WavesDataTextures", wavesDataTextures);
        timeDependentSpectrumComputeShader.SetInt("_NbCascades", cascades.Length);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DxDzTextures", DxDzTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DyDxzTextures", DyDxzTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DyxDyzTextures", DyxDyzTextures);
        timeDependentSpectrumComputeShader.SetTexture(KERNEL_TIME_DEPENDENT_SPECTRUM, "_DxxDzzTextures", DxxDzzTextures);
    }

    private void InitializeResultTexturesFillerComputeShader() {
        resultTexturesFillerComputeShader.SetInt("_NbCascades", cascades.Length);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DxDzTextures", DxDzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DyDxzTextures", DyDxzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DyxDyzTextures", DyxDyzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DxxDzzTextures", DxxDzzTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DisplacementsTextures", displacementsTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_DerivativesTextures", derivativesTextures);
        resultTexturesFillerComputeShader.SetTexture(KERNEL_RESULT_TEXTURES_FILLER, "_TurbulenceTextures", turbulenceTextures);
    }

    private void CalculateInitialSpectrumTextures() {
        // Calculate the initial spectrum H0(K)
        initialSpectrumComputeShader.Dispatch(KERNEL_INITIAL_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        // Store, in each element on the texture, the value of the complex conjugate element
        // Now the Initial spectrum texture stores H0(K) and H0(-k)*
        initialSpectrumComputeShader.Dispatch(KERNEL_CONJUGATED_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);
    }

    public void CalculateWavesTexturesAtTime(float time) {
        timeDependentSpectrumComputeShader.SetFloat("_Time", time);
        timeDependentSpectrumComputeShader.Dispatch(KERNEL_TIME_DEPENDENT_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        IFFT.InverseFastFourierTransform(DxDzTextures);
        IFFT.InverseFastFourierTransform(DyDxzTextures);
        IFFT.InverseFastFourierTransform(DyxDyzTextures);
        IFFT.InverseFastFourierTransform(DxxDzzTextures);

        resultTexturesFillerComputeShader.Dispatch(KERNEL_RESULT_TEXTURES_FILLER, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);

        derivativesTextures.GenerateMips();
        turbulenceTextures.GenerateMips();
    }

    public float GetWaterHeight(Vector3 worldPosition) {
        if (buoyancyData == null) return 0f;

        float u = Mathf.InverseLerp(-texturesSize / 2, texturesSize / 2, worldPosition.x);
        float v = Mathf.InverseLerp(-texturesSize / 2, texturesSize / 2, worldPosition.z);

        // Map UV to pixel coordinates
        int x = Mathf.Clamp((int)(u * displacementsTextures.width), 0, displacementsTextures.width - 1);
        int y = Mathf.Clamp((int)(v * displacementsTextures.height), 0, displacementsTextures.height - 1);

        int index = y * displacementsTextures.width + x;
        return buoyancyData[index].g;
    }

    void Awake() {
        GenerateRandomNoiseTexture();

        IFFT = new IFFT(IFFTComputeShader, texturesSize, cascades.Length);

        KERNEL_INITIAL_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateInitialSpectrumTextures");
        KERNEL_CONJUGATED_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateConjugatedInitialSpectrumTextures");
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

        wavelengths = new float[cascades.Length];
        cutoffs = new float[cascades.Length * 2];

        for(int i = 0; i < cascades.Length; i++) {
            wavelengths[i] = cascades[i].wavelength;
            cutoffs[i*2] = cascades[i].cutoffLow;
            cutoffs[i*2 + 1] = cascades[i].cutoffHigh;
        }

        wavelengthsBuffer = new ComputeBuffer(cascades.Length, 4, ComputeBufferType.Default);
        wavelengthsBuffer.SetData(wavelengths);
        cutoffsBuffer = new ComputeBuffer(cascades.Length * 2, 4, ComputeBufferType.Default);
        cutoffsBuffer.SetData(cutoffs);

        InitializeInitialSpectrumComputeShader();
        CalculateInitialSpectrumTextures();
        InitializeTimeDependentSpectrumComputeShader();
        InitializeResultTexturesFillerComputeShader();

        GameObject probeObject = new GameObject("RealtimeReflectionProbe");
        reflectionProbe = probeObject.AddComponent<ReflectionProbe>();
        probeObject.transform.SetParent(transform);

        reflectionProbe.mode = ReflectionProbeMode.Realtime;
        reflectionProbe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
        reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        reflectionProbe.clearFlags = ReflectionProbeClearFlags.Skybox;
        reflectionProbe.cullingMask = 0;
        
        // We create a custom RenderTexture so we can set the reflection texture in the material.
        // Otherwise, we would have to make the Probe generate the default texture after the game has started, causing renderProbe.texture to return null at the time of assignment to the material.
        // By creating our custom RenderTexture and assigning it to the RenderProbe, we ensure that the material receives a correct reference to the cubemap.
        RenderTexture reflectionTexture = new RenderTexture(reflectionProbe.resolution, reflectionProbe.resolution, 16);
        reflectionTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube; // Important: Set to Cubemap
        reflectionTexture.Create();
        
        reflectionProbe.realtimeTexture = reflectionTexture;

        material.SetTexture("_ReflectionCubemap", reflectionTexture);
        material.SetInt("_NbCascades", cascades.Length);
        material.SetTexture("_DisplacementsTextures", displacementsTextures);
        material.SetTexture("_DerivativesTextures", derivativesTextures);
        material.SetTexture("_TurbulenceTextures", turbulenceTextures);
        material.SetFloatArray("_Wavelengths", wavelengths);
    }

    void Update() {
        CalculateWavesTexturesAtTime(Time.time);

        AsyncGPUReadback.Request(displacementsTextures, 0, request => {
            if (request.hasError) {
                Debug.LogError("Async GPU Readback failed!");
                return;
            }

            // Convert the request data to a Color array (assuming RGBAFloat format)
            buoyancyData = request.GetData<Color>().ToArray();
        });
    }

    // Prevent leaks from the Buffers
    void OnDisable() {
		wavelengthsBuffer.Release();
        wavelengths = null;
        cutoffsBuffer.Release();
        cutoffsBuffer = null;
	}

    /* 
    OnValidate() is a Unity Editor function that is called after these scenarios:
        - A public or serialized field of a MonoBehaviour is modified in the Inspector
        - Script recompilation
        - A component containing the OnValidate function is added to a GameObject
        - A scene is loaded in the Unity Editor
    The purpose of this OnValidate is to provide a (not very good written, but functional) real-time reflection of ocean parameters changes within the Unity Editor.
    This means it only responds to changes made through the Editor’s Inspector. 
    If you need to reflect changes made programmatically at runtime, consider using a getter/setter approach as explained in
    https://discussions.unity.com/t/running-a-function-after-any-variable-has-changed/732103/3

    It can significantly impact performance, so use it only when real-time updates are necessary.
    */
    /*void OnValidate() {
        // Ensure textures are initialized before recalculating, as OnValidate may run before textures and Compute Shaders are fully set
        if (initialSpectrumTextures != null){
            initialSpectrumComputeShader.SetFloat("_WindSpeed", windSpeed);
            initialSpectrumComputeShader.SetFloat("_WindDirectionX", windDirection.x);
            initialSpectrumComputeShader.SetFloat("_WindDirectionY", windDirection.y);
            initialSpectrumComputeShader.SetFloat("_Gravity", gravity);
            initialSpectrumComputeShader.SetFloat("_Fetch", fetch);
            initialSpectrumComputeShader.SetFloat("_Depth", depth);
            initialSpectrumComputeShader.SetFloat("_Fade", fade);
            initialSpectrumComputeShader.SetFloat("_Swell", swell);
            CalculateInitialSpectrumTextures();
        }
    }*/
    
}