using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtmosphereController : MonoBehaviour
{
    public static readonly int SUN_DIRECTION = Shader.PropertyToID("_SunDirection");

    private Light sunLight;
    public Gradient colorBySunElevation;

    public int transmittanceLUTWidth = 64;
    public int transmittanceLUTHeight = 256;

    public int multiscatteringLUTWidth = 64;
    public int multiscatteringLUTHeight = 64;

    public int skyViewLUTWidth = 256;
    public int skyViewLUTHeight = 128;

    public float planetRadius = 6360000;
    public float atmosphereRadius = 6420000;

    public Vector3 rayleighScatteringCoefficient = new Vector3(5.802e-6f, 13.558e-6f, 6.5e-5f);
    public Vector3 rayleighAbsorptionCoefficient = new Vector3(0f, 0f, 0f);
    public float rayleighScaleHeight = 8000;

    public Vector3 mieScatteringCoefficient = new Vector3(3.996e-6f, 3.996e-6f, 3.996e-6f);
    public Vector3 mieAbsorptionCoefficient = new Vector3(4.4e-6f, 4.4e-6f, 4.4e-6f);
    public float mieScaleHeight = 1200;
    public float g = 0.85f;
    
    public Vector3 ozoneScatteringCoefficient = new Vector3(0f, 0f, 0f);
    public Vector3 ozoneAbsorptionCoefficient = new Vector3(0.65e-6f, 1.881e-6f, 0.085e-6f);

    public Vector3 groundAlbedo = new Vector3(0.0f, 0.0f, 0.0f);

    public RenderTexture transmittanceLUT;
    private RenderTexture multiscatteringLUT;
    private RenderTexture skyViewLUT;

    public ComputeShader transmittanceLUTComputeShader;
    public ComputeShader multiscatteringLUTComputeShader;
    public ComputeShader skyViewLUTComputeShader;
    
    public Material material;

    private int KERNEL_TRANSMITTANCE_LUT;
    private int KERNEL_MULTISCATTERING_LUT;
    private int KERNEL_SKYVIEW_LUT;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    private RenderTexture CreateRenderTexture(int xSize, int ySize, RenderTextureFormat format, bool useMips) {
        RenderTexture rt = new RenderTexture(xSize, ySize, 0, format, RenderTextureReadWrite.Linear);
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 16;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void InitializeCommonAtmosphereParams(ComputeShader shader)
    {
        shader.SetFloat("_PlanetRadius", planetRadius);
        shader.SetFloat("_AtmosphereRadius", atmosphereRadius);
        shader.SetFloat("_MieG", g);

        shader.SetVector("_RayleighScatteringCoefficient", new Vector4(rayleighScatteringCoefficient.x, rayleighScatteringCoefficient.y, rayleighScatteringCoefficient.z));
        shader.SetVector("_MieScatteringCoefficient", new Vector4(mieScatteringCoefficient.x, mieScatteringCoefficient.y, mieScatteringCoefficient.z));
        shader.SetVector("_OzoneScatteringCoefficient", new Vector4(ozoneScatteringCoefficient.x, ozoneScatteringCoefficient.y, ozoneScatteringCoefficient.z));

        shader.SetVector("_RayleighAbsorptionCoefficient", new Vector4(rayleighAbsorptionCoefficient.x, rayleighAbsorptionCoefficient.y, rayleighAbsorptionCoefficient.z));
        shader.SetVector("_MieAbsorptionCoefficient", new Vector4(mieAbsorptionCoefficient.x, mieAbsorptionCoefficient.y, mieAbsorptionCoefficient.z));
        shader.SetVector("_OzoneAbsorptionCoefficient", new Vector4(ozoneAbsorptionCoefficient.x, ozoneAbsorptionCoefficient.y, ozoneAbsorptionCoefficient.z));
        
        shader.SetFloat("_RayleighScaleHeight", rayleighScaleHeight);
        shader.SetFloat("_MieScaleHeight", mieScaleHeight);
    }

    private void InitializeTransmittanceLUTComputeShader()
    {
        transmittanceLUTComputeShader.SetInt("_LutWidth", transmittanceLUTWidth);
        transmittanceLUTComputeShader.SetInt("_LutHeight", transmittanceLUTHeight);
        InitializeCommonAtmosphereParams(transmittanceLUTComputeShader);

        transmittanceLUTComputeShader.SetTexture(KERNEL_TRANSMITTANCE_LUT, "_TransmittanceLUT", transmittanceLUT);
    }

    private void ComputeTransmittanceLUT() {
        transmittanceLUTComputeShader.Dispatch(KERNEL_TRANSMITTANCE_LUT, transmittanceLUTWidth/LOCAL_WORK_GROUPS_X, transmittanceLUTHeight/LOCAL_WORK_GROUPS_Y, 1);
    }

    private void InitializeMultiscatteringLUTComputeShader() {
        multiscatteringLUTComputeShader.SetInt("_LutWidth", multiscatteringLUTWidth);
        multiscatteringLUTComputeShader.SetInt("_LutHeight", multiscatteringLUTHeight);
        InitializeCommonAtmosphereParams(multiscatteringLUTComputeShader);

        multiscatteringLUTComputeShader.SetVector("_GroundSpectrumAlbedo", groundAlbedo);

        multiscatteringLUTComputeShader.SetTexture(KERNEL_MULTISCATTERING_LUT, "_TransmittanceLUT", transmittanceLUT);
        multiscatteringLUTComputeShader.SetTexture(KERNEL_MULTISCATTERING_LUT, "_MultiscatteringLUT", multiscatteringLUT);
    }

    private void ComputeMultiscatteringLUT() {
        multiscatteringLUTComputeShader.Dispatch(KERNEL_MULTISCATTERING_LUT, multiscatteringLUTWidth/LOCAL_WORK_GROUPS_X, multiscatteringLUTHeight/LOCAL_WORK_GROUPS_Y, 1);
    }

    private void InitializeSkyViewLUTComputeShader() {
        skyViewLUTComputeShader.SetInt("_LutWidth", skyViewLUTWidth);
        skyViewLUTComputeShader.SetInt("_LutHeight", skyViewLUTHeight);
        InitializeCommonAtmosphereParams(skyViewLUTComputeShader);

        skyViewLUTComputeShader.SetVector("_SunDirection", -this.transform.forward);

        skyViewLUTComputeShader.SetTexture(KERNEL_SKYVIEW_LUT, "_TransmittanceLUT", transmittanceLUT);
        skyViewLUTComputeShader.SetTexture(KERNEL_SKYVIEW_LUT, "_MultiscatteringLUT", multiscatteringLUT);
        skyViewLUTComputeShader.SetTexture(KERNEL_SKYVIEW_LUT, "_SkyViewLUT", skyViewLUT);
    }

    private void ComputeSkyViewLUT() {
        skyViewLUTComputeShader.Dispatch(KERNEL_SKYVIEW_LUT, skyViewLUTWidth/LOCAL_WORK_GROUPS_X, skyViewLUTHeight/LOCAL_WORK_GROUPS_Y, 1);
    }

    private void TurnTransmittanceIntoColorGradient() {
        RenderTexture.active = transmittanceLUT;

        Texture2D readableTex = new Texture2D(transmittanceLUT.width, transmittanceLUT.height, TextureFormat.RGBA32, false);
        readableTex.ReadPixels(new Rect(0, 0, transmittanceLUT.width, transmittanceLUT.height), 0, 0);
        readableTex.Apply();

        RenderTexture.active = null;

        colorBySunElevation = new Gradient();

        const int sampleCount = 8;
        float[] intervals = new float[] { 0.01f, 0.14f, 0.28f, 0.36f, 0.57f, 0.75f, 0.86f, 0.99f };
        GradientColorKey[] colorKeys = new GradientColorKey[sampleCount];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = intervals[i];
            Color color = readableTex.GetPixelBilinear(0f, t) * 2.5f;
            colorKeys[i] = new GradientColorKey(color, t);
            alphaKeys[i] = new GradientAlphaKey(1.0f, t);
        }

        colorBySunElevation.SetKeys(colorKeys, alphaKeys);
    }

    void Awake() {
        sunLight = GetComponent<Light>();

        transmittanceLUT = CreateRenderTexture(transmittanceLUTWidth, transmittanceLUTHeight, RenderTextureFormat.ARGBFloat, false);
        multiscatteringLUT = CreateRenderTexture(multiscatteringLUTWidth, multiscatteringLUTHeight, RenderTextureFormat.ARGBFloat, false);
        skyViewLUT = CreateRenderTexture(skyViewLUTWidth, skyViewLUTHeight, RenderTextureFormat.ARGBFloat, false);

        KERNEL_TRANSMITTANCE_LUT = transmittanceLUTComputeShader.FindKernel("ComputeTransmittanceLUT");
        KERNEL_MULTISCATTERING_LUT = multiscatteringLUTComputeShader.FindKernel("ComputeMultiscatteringLUT");
        KERNEL_SKYVIEW_LUT = skyViewLUTComputeShader.FindKernel("ComputeSkyViewLUT");

        InitializeTransmittanceLUTComputeShader();
        ComputeTransmittanceLUT();

        TurnTransmittanceIntoColorGradient();

        InitializeMultiscatteringLUTComputeShader();
        ComputeMultiscatteringLUT();

        InitializeSkyViewLUTComputeShader();
        ComputeSkyViewLUT();

        material.SetTexture("_SkyViewLUT", skyViewLUT);
    }

    void Update() {
        skyViewLUTComputeShader.SetVector("_SunDirection", -this.transform.forward);
        ComputeSkyViewLUT();

        // Compute the sun's elevation: 1 on zenith, -1 when below the horizon.
        // Remap the range [-1, 1] to [0, 1] for sampling the color gradient.
        float sunElevation = (Vector3.Dot(this.transform.forward, Vector3.down) + 1f) * 0.5f; 
        sunLight.color = colorBySunElevation.Evaluate(sunElevation);
    }
}
