using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IFFT
{
    private int KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES;
    private int KERNEL_IFFT_HORIZONTAL_STEP;
    private int KERNEL_IFFT_VERTICAL_STEP;
    private int KERNEL_IFFT_PERMUTE;
    private ComputeShader IFFTComputeShader;
    private RenderTexture TwiddleFactorsAndInputIndicesTexture;
    private RenderTexture PingPongTextures;
    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;
    private int texturesSize;
    private int NbCascades;

    private RenderTexture CreatePingPongTextureArray(){
        RenderTexture rt = new RenderTexture(texturesSize, texturesSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        rt.volumeDepth = NbCascades;
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private RenderTexture CreateTwiddleFactorsAndInputIndicesTexture(int logSize){
        RenderTexture rt = new RenderTexture(logSize, texturesSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void CalculateTwiddleFactorsAndInputIndicesTexture(){
        int logSize = (int)Mathf.Log(texturesSize, 2);
        TwiddleFactorsAndInputIndicesTexture = CreateTwiddleFactorsAndInputIndicesTexture(logSize);
        
        IFFTComputeShader.SetInt("_TextureSize", texturesSize);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, logSize, texturesSize/2/LOCAL_WORK_GROUPS_Y, 1);
    }

    public IFFT(ComputeShader IFFTComputeShader, int texturesSize, int nbCascades){
        this.IFFTComputeShader = IFFTComputeShader;
        this.texturesSize = texturesSize;
        this.NbCascades = nbCascades;

        KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES = this.IFFTComputeShader.FindKernel("PrecomputeTwiddleFactorsAndInputIndices");
        KERNEL_IFFT_HORIZONTAL_STEP = this.IFFTComputeShader.FindKernel("HorizontalStepIFFT");
        KERNEL_IFFT_VERTICAL_STEP = this.IFFTComputeShader.FindKernel("VerticalStepIFFT");
        KERNEL_IFFT_PERMUTE = this.IFFTComputeShader.FindKernel("Permute");

        CalculateTwiddleFactorsAndInputIndicesTexture();
        PingPongTextures = CreatePingPongTextureArray();
    }

    public void InverseFastFourierTransform(RenderTexture inputTextureArray) {
        int logSize = (int)Mathf.Log(texturesSize, 2);
        bool pingPong = false;

        IFFTComputeShader.SetInt("_NbCascades", NbCascades);

        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_InputTextures", inputTextureArray);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_PingPongTextures", PingPongTextures);
        for (int i = 0; i < logSize; i++)
        {
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_HORIZONTAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
            pingPong = !pingPong;
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_InputTextures", inputTextureArray);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_PingPongTextures", PingPongTextures);
        for (int i = 0; i < logSize; i++)
        {
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_VERTICAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
            pingPong = !pingPong;
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_PERMUTE, "_InputTextures", inputTextureArray);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_PERMUTE, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
    }
}
