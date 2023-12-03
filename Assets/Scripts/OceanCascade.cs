using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanCascade : MonoBehaviour
{
    private int texturesSize;

    //Ocean parameters
    private float windSpeed;
    private Vector2 windDirection;
    private float gravity;
    private float fetch;
    private float depth;

    public float cutoffHigh = 1.0f;
    public float cutoffLow = 0.0f;
    public float lengthScale = 10.0f;

    private ComputeShader initialSpectrumComputeShader;
    private ComputeShader TimeDependentSpectrumComputeShader;
    private ComputeShader IFFTComputeShader;
    private ComputeShader ResultTexturesFillerComputeShader;
    private IFFT IFFT;
    private Texture2D randomNoiseTexture;
    private RenderTexture initialSpectrumTexture;
    private RenderTexture WavesDataTexture;
    private RenderTexture DxDzTexture;
    private RenderTexture DyDxzTexture;
    private RenderTexture DyxDyzTexture;
    private RenderTexture DxxDzzTexture;

    public RenderTexture DisplacementsTexture;
    public RenderTexture DerivativesTexture;
    public RenderTexture TurbulenceTexture;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    int KERNEL_INITIAL_SPECTRUM;
    int KERNEL_CONJUGATED_SPECTRUM;
    int KERNEL_TIME_DEPENDENT_SPECTRUM;
    int KERNEL_RESULT_TEXTURES_FILLER;

    private RenderTexture CreateRenderTexture(RenderTextureFormat format, bool useMips){
        RenderTexture rt = new RenderTexture(texturesSize, texturesSize, 0, format, RenderTextureReadWrite.Linear);
        rt.useMipMap = useMips;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private RenderTexture CreateRGRenderTexture(bool useMips = false) {
        return CreateRenderTexture(RenderTextureFormat.RGFloat, useMips);
    }

    private RenderTexture CreateRGBARenderTexture(bool useMips = false) {
        return CreateRenderTexture(RenderTextureFormat.ARGBFloat, useMips);
    }

    public void setVariables(int texturesSize, float windSpeed, Vector2 windDirection, float gravity, float fetch, float depth, ComputeShader initialSpectrumComputeShader, ComputeShader TimeDependentSpectrumComputeShader, ComputeShader IFFTComputeShader, ComputeShader ResultTexturesFillerComputeShader, Texture2D randomNoiseTexture){
        this.texturesSize = texturesSize;
        this.windSpeed = windSpeed;
        this.windDirection = windDirection;
        this.gravity = gravity;
        this.fetch = fetch;
        this.depth = depth;
        this.initialSpectrumComputeShader = initialSpectrumComputeShader;
        this.TimeDependentSpectrumComputeShader = TimeDependentSpectrumComputeShader;
        this.IFFTComputeShader = IFFTComputeShader;
        this.ResultTexturesFillerComputeShader = ResultTexturesFillerComputeShader;
        this.randomNoiseTexture = randomNoiseTexture;
        IFFT = new IFFT(IFFTComputeShader, texturesSize);



        WavesDataTexture = CreateRGBARenderTexture();
        initialSpectrumTexture = CreateRGBARenderTexture();
        DxDzTexture = CreateRGRenderTexture();
        DyDxzTexture = CreateRGRenderTexture();
        DyxDyzTexture = CreateRGRenderTexture();
        DxxDzzTexture = CreateRGRenderTexture();
        DisplacementsTexture = CreateRGBARenderTexture();
        DerivativesTexture = CreateRGBARenderTexture(true);
        TurbulenceTexture = CreateRGBARenderTexture(true);

        KERNEL_INITIAL_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateInitialSpectrumTexture");
        KERNEL_CONJUGATED_SPECTRUM = initialSpectrumComputeShader.FindKernel("CalculateConjugatedInitialSpectrumTexture");
        KERNEL_TIME_DEPENDENT_SPECTRUM = TimeDependentSpectrumComputeShader.FindKernel("CalculateTimeDependentComplexAmplitudesAndDerivatives");
        KERNEL_RESULT_TEXTURES_FILLER = ResultTexturesFillerComputeShader.FindKernel("FillResultTextures");
    }

    private void CalculateInitialSpectrumTexture(){
        // Calculate the initial spectrum H0(K)
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
        initialSpectrumComputeShader.SetTexture(KERNEL_CONJUGATED_SPECTRUM, "_InitialSpectrumTexture", initialSpectrumTexture);
        initialSpectrumComputeShader.SetInt("_TextureSize", texturesSize);
        initialSpectrumComputeShader.Dispatch(KERNEL_CONJUGATED_SPECTRUM, texturesSize/LOCAL_WORK_GROUPS_X, texturesSize/LOCAL_WORK_GROUPS_Y, 1);
    }

    public void CalculateWavesTexturesAtTime(float time) {
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
    }

    public void InitialCalculations(){
        CalculateInitialSpectrumTexture();
    }
}
