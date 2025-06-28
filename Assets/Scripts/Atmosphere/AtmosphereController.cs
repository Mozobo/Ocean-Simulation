using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AtmosphereController : MonoBehaviour
{
    public static readonly int SUN_DIRECTION = Shader.PropertyToID("_SunDirection");

    public int transmittanceLUTWidth = 64;
    public int transmittanceLUTHeight = 256;

    public int multiscatteringLUTWidth = 64;
    public int multiscatteringLUTHeight = 64;

    public int skyViewLUTWidth = 256;
    public int skyViewLUTHeight = 128;

    public float planetRadius = 6360000;
    public float atmosphereRadius = 6420000;

    public Vector3 rayleighScatteringCoefficient = new Vector3(5.802e-6f, 13.558e-6f, 33.1e-6f);
    public Vector3 rayleighAbsorptionCoefficient = new Vector3(0f, 0f, 0f);
    public float rayleighScaleHeight = 8000;

    public Vector3 mieScatteringCoefficient = new Vector3(3.996e-6f, 3.996e-6f, 3.996e-6f);
    public Vector3 mieAbsorptionCoefficient = new Vector3(4.4e-6f, 4.4e-6f, 4.4e-6f);
    public float mieScaleHeight = 1200;
    public float g = 0.8f;
    
    public Vector3 ozoneScatteringCoefficient = new Vector3(0f, 0f, 0f);
    public Vector3 ozoneAbsorptionCoefficient = new Vector3(0.65e-6f, 1.881e-6f, 0.085e-6f);

    public Vector3 groundAlbedo = new Vector3(0.0f, 0.0f, 0.0f);

    private RenderTexture transmittanceLUT;
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

    private void InitializeTransmittanceLUTComputeShader() {
        transmittanceLUTComputeShader.SetInt("_LutWidth", transmittanceLUTWidth);
        transmittanceLUTComputeShader.SetInt("_LutHeight", transmittanceLUTHeight);
        transmittanceLUTComputeShader.SetFloat("_PlanetRadius", planetRadius);
        transmittanceLUTComputeShader.SetFloat("_AtmosphereRadius", atmosphereRadius);

        transmittanceLUTComputeShader.SetVector("_RayleighScatteringCoefficient", new Vector4(rayleighScatteringCoefficient.x, rayleighScatteringCoefficient.y, rayleighScatteringCoefficient.z));
        transmittanceLUTComputeShader.SetVector("_MieScatteringCoefficient", new Vector4(mieScatteringCoefficient.x, mieScatteringCoefficient.y, mieScatteringCoefficient.z));
        transmittanceLUTComputeShader.SetVector("_OzoneScatteringCoefficient", new Vector4(ozoneScatteringCoefficient.x, ozoneScatteringCoefficient.y, ozoneScatteringCoefficient.z));

        transmittanceLUTComputeShader.SetVector("_RayleighAbsorptionCoefficient", new Vector4(rayleighAbsorptionCoefficient.x, rayleighAbsorptionCoefficient.y, rayleighAbsorptionCoefficient.z));
        transmittanceLUTComputeShader.SetVector("_MieAbsorptionCoefficient", new Vector4(mieAbsorptionCoefficient.x, mieAbsorptionCoefficient.y, mieAbsorptionCoefficient.z));
        transmittanceLUTComputeShader.SetVector("_OzoneAbsorptionCoefficient", new Vector4(ozoneAbsorptionCoefficient.x, ozoneAbsorptionCoefficient.y, ozoneAbsorptionCoefficient.z));

        transmittanceLUTComputeShader.SetFloat("_RayleighScaleHeight", rayleighScaleHeight);
        transmittanceLUTComputeShader.SetFloat("_MieScaleHeight", mieScaleHeight);

        transmittanceLUTComputeShader.SetTexture(KERNEL_TRANSMITTANCE_LUT, "_TransmittanceLUT", transmittanceLUT);
    }

    private void ComputeTransmittanceLUT() {
        transmittanceLUTComputeShader.Dispatch(KERNEL_TRANSMITTANCE_LUT, transmittanceLUTWidth/LOCAL_WORK_GROUPS_X, transmittanceLUTHeight/LOCAL_WORK_GROUPS_Y, 1);
    }

    private void InitializeMultiscatteringLUTComputeShader() {
        multiscatteringLUTComputeShader.SetInt("_LutWidth", multiscatteringLUTWidth);
        multiscatteringLUTComputeShader.SetInt("_LutHeight", multiscatteringLUTHeight);
        multiscatteringLUTComputeShader.SetFloat("_PlanetRadius", planetRadius);
        multiscatteringLUTComputeShader.SetFloat("_AtmosphereRadius", atmosphereRadius);

        multiscatteringLUTComputeShader.SetVector("_RayleighScatteringCoefficient", new Vector4(rayleighScatteringCoefficient.x, rayleighScatteringCoefficient.y, rayleighScatteringCoefficient.z));
        multiscatteringLUTComputeShader.SetVector("_MieScatteringCoefficient", new Vector4(mieScatteringCoefficient.x, mieScatteringCoefficient.y, mieScatteringCoefficient.z));
        multiscatteringLUTComputeShader.SetVector("_OzoneScatteringCoefficient", new Vector4(ozoneScatteringCoefficient.x, ozoneScatteringCoefficient.y, ozoneScatteringCoefficient.z));

        multiscatteringLUTComputeShader.SetVector("_RayleighAbsorptionCoefficient", new Vector4(rayleighAbsorptionCoefficient.x, rayleighAbsorptionCoefficient.y, rayleighAbsorptionCoefficient.z));
        multiscatteringLUTComputeShader.SetVector("_MieAbsorptionCoefficient", new Vector4(mieAbsorptionCoefficient.x, mieAbsorptionCoefficient.y, mieAbsorptionCoefficient.z));
        multiscatteringLUTComputeShader.SetVector("_OzoneAbsorptionCoefficient", new Vector4(ozoneAbsorptionCoefficient.x, ozoneAbsorptionCoefficient.y, ozoneAbsorptionCoefficient.z));

        multiscatteringLUTComputeShader.SetFloat("_RayleighScaleHeight", rayleighScaleHeight);
        multiscatteringLUTComputeShader.SetFloat("_MieScaleHeight", mieScaleHeight);

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
        skyViewLUTComputeShader.SetFloat("_PlanetRadius", planetRadius);
        skyViewLUTComputeShader.SetFloat("_AtmosphereRadius", atmosphereRadius);
        skyViewLUTComputeShader.SetFloat("_MieG", g);

        skyViewLUTComputeShader.SetVector("_RayleighScatteringCoefficient", new Vector4(rayleighScatteringCoefficient.x, rayleighScatteringCoefficient.y, rayleighScatteringCoefficient.z));
        skyViewLUTComputeShader.SetVector("_MieScatteringCoefficient", new Vector4(mieScatteringCoefficient.x, mieScatteringCoefficient.y, mieScatteringCoefficient.z));
        skyViewLUTComputeShader.SetVector("_OzoneScatteringCoefficient", new Vector4(ozoneScatteringCoefficient.x, ozoneScatteringCoefficient.y, ozoneScatteringCoefficient.z));

        skyViewLUTComputeShader.SetVector("_RayleighAbsorptionCoefficient", new Vector4(rayleighAbsorptionCoefficient.x, rayleighAbsorptionCoefficient.y, rayleighAbsorptionCoefficient.z));
        skyViewLUTComputeShader.SetVector("_MieAbsorptionCoefficient", new Vector4(mieAbsorptionCoefficient.x, mieAbsorptionCoefficient.y, mieAbsorptionCoefficient.z));
        skyViewLUTComputeShader.SetVector("_OzoneAbsorptionCoefficient", new Vector4(ozoneAbsorptionCoefficient.x, ozoneAbsorptionCoefficient.y, ozoneAbsorptionCoefficient.z));

        skyViewLUTComputeShader.SetFloat("_RayleighScaleHeight", rayleighScaleHeight);
        skyViewLUTComputeShader.SetFloat("_MieScaleHeight", mieScaleHeight);

        skyViewLUTComputeShader.SetVector("_SunDirection", -this.transform.forward);

        skyViewLUTComputeShader.SetTexture(KERNEL_SKYVIEW_LUT, "_TransmittanceLUT", transmittanceLUT);
        skyViewLUTComputeShader.SetTexture(KERNEL_SKYVIEW_LUT, "_MultiscatteringLUT", multiscatteringLUT);
        skyViewLUTComputeShader.SetTexture(KERNEL_SKYVIEW_LUT, "_SkyViewLUT", skyViewLUT);
    }

    private void ComputeSkyViewLUT() {
        skyViewLUTComputeShader.Dispatch(KERNEL_SKYVIEW_LUT, skyViewLUTWidth/LOCAL_WORK_GROUPS_X, skyViewLUTHeight/LOCAL_WORK_GROUPS_Y, 1);
    }

    void Awake() {
        transmittanceLUT = CreateRenderTexture(transmittanceLUTWidth, transmittanceLUTHeight, RenderTextureFormat.ARGBFloat, false);
        multiscatteringLUT = CreateRenderTexture(multiscatteringLUTWidth, multiscatteringLUTHeight, RenderTextureFormat.ARGBFloat, false);
        skyViewLUT = CreateRenderTexture(skyViewLUTWidth, skyViewLUTHeight, RenderTextureFormat.ARGBFloat, false);

        KERNEL_TRANSMITTANCE_LUT = transmittanceLUTComputeShader.FindKernel("ComputeTransmittanceLUT");
        KERNEL_MULTISCATTERING_LUT = multiscatteringLUTComputeShader.FindKernel("ComputeMultiscatteringLUT");
        KERNEL_SKYVIEW_LUT = skyViewLUTComputeShader.FindKernel("ComputeSkyViewLUT");

        InitializeTransmittanceLUTComputeShader();
        ComputeTransmittanceLUT();

        InitializeMultiscatteringLUTComputeShader();
        ComputeMultiscatteringLUT();

        InitializeSkyViewLUTComputeShader();
        ComputeSkyViewLUT();

        material.SetTexture("_SkyViewLUT", skyViewLUT);
        material.SetTexture("_TransmittanceLUT", transmittanceLUT);
    }

    void Update() {
        skyViewLUTComputeShader.SetVector("_SunDirection", -this.transform.forward);
        ComputeSkyViewLUT();
    }
}
