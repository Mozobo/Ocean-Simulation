using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IFFT
{
    private int texturesSize;

    private ComputeShader IFFTComputeShader;
    private RenderTexture TwiddleFactorsAndInputIndicesTexture;
    private RenderTexture PingPongTextures;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    private int KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES;
    private int KERNEL_IFFT_HORIZONTAL_STEP;
    private int KERNEL_IFFT_VERTICAL_STEP;
    private int KERNEL_IFFT_PERMUTE;


    public IFFT(ComputeShader IFFTComputeShader, int texturesSize, int nbCascades) {
        this.IFFTComputeShader = IFFTComputeShader;
        this.texturesSize = texturesSize;

        KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES = this.IFFTComputeShader.FindKernel("PrecomputeTwiddleFactorsAndInputIndices");
        KERNEL_IFFT_HORIZONTAL_STEP = this.IFFTComputeShader.FindKernel("HorizontalStepIFFT");
        KERNEL_IFFT_VERTICAL_STEP = this.IFFTComputeShader.FindKernel("VerticalStepIFFT");
        KERNEL_IFFT_PERMUTE = this.IFFTComputeShader.FindKernel("Permute");

        // Create and calculate TwiddleFactorsAndInputIndicesTexture
        int logSize = (int)Mathf.Log(texturesSize, 2);
        TwiddleFactorsAndInputIndicesTexture = new RenderTexture(logSize, texturesSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
        TwiddleFactorsAndInputIndicesTexture.filterMode = FilterMode.Point;
        TwiddleFactorsAndInputIndicesTexture.wrapMode = TextureWrapMode.Repeat;
        TwiddleFactorsAndInputIndicesTexture.enableRandomWrite = true;
        TwiddleFactorsAndInputIndicesTexture.Create();
        
        this.IFFTComputeShader.SetInt("_TextureSize", texturesSize);
        this.IFFTComputeShader.SetInt("_NbCascades", nbCascades);
        this.IFFTComputeShader.SetTexture(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        this.IFFTComputeShader.Dispatch(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, logSize, texturesSize/2/LOCAL_WORK_GROUPS_Y, 1);

        // Create PingPongTextures
        PingPongTextures = new RenderTexture(texturesSize, texturesSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
        PingPongTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        PingPongTextures.volumeDepth = nbCascades;
        PingPongTextures.useMipMap = false;
        PingPongTextures.autoGenerateMips = false;
        PingPongTextures.anisoLevel = 6;
        PingPongTextures.filterMode = FilterMode.Trilinear;
        PingPongTextures.wrapMode = TextureWrapMode.Repeat;
        PingPongTextures.enableRandomWrite = true;
        PingPongTextures.Create();
    }

    public void InverseFastFourierTransform(RenderTexture inputTextureArray) {
        int logSize = (int)Mathf.Log(texturesSize, 2);
        bool pingPong = false;

        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_InputTextures", inputTextureArray);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_PingPongTextures", PingPongTextures);

        for (int i = 0; i < logSize; i++) {
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_HORIZONTAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
            pingPong = !pingPong;
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_InputTextures", inputTextureArray);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_PingPongTextures", PingPongTextures);
        
        for (int i = 0; i < logSize; i++) {
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_VERTICAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
            pingPong = !pingPong;
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_PERMUTE, "_InputTextures", inputTextureArray);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_PERMUTE, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
    }
}
