using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IFFT : MonoBehaviour
{
    private int KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES;
    private int KERNEL_IFFT_HORIZONTAL_STEP;
    private int KERNEL_IFFT_VERTICAL_STEP;
    private int KERNEL_IFFT_SCALE;
    private int KERNEL_IFFT_PERMUTE;
    private ComputeShader IFFTComputeShader;
    private RenderTexture TwiddleFactorsAndInputIndicesTexture;
    private RenderTexture PingPongTexture;
    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;
    private int texturesSize;

    private RenderTexture CreateRenderTexture(){
        RenderTexture rt = new RenderTexture(texturesSize, texturesSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.sRGB);
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.anisoLevel = 6;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void CalculateTwiddleFactorsAndInputIndicesTexture(){
        int logSize = (int)Mathf.Log(texturesSize, 2);
        TwiddleFactorsAndInputIndicesTexture = CreateRenderTexture();
        
        IFFTComputeShader.SetInt("_TextureSize", texturesSize);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, logSize, texturesSize/2/LOCAL_WORK_GROUPS_Y, 1);
    }

    public IFFT(ComputeShader IFFTComputeShader, int texturesSize){
        this.IFFTComputeShader = IFFTComputeShader;
        this.texturesSize = texturesSize;

        KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES = this.IFFTComputeShader.FindKernel("PrecomputeTwiddleFactorsAndInputIndices");
        KERNEL_IFFT_HORIZONTAL_STEP = this.IFFTComputeShader.FindKernel("HorizontalStepIFFT");
        KERNEL_IFFT_VERTICAL_STEP = this.IFFTComputeShader.FindKernel("VerticalStepIFFT");
        KERNEL_IFFT_SCALE = this.IFFTComputeShader.FindKernel("Scale");
        KERNEL_IFFT_PERMUTE = this.IFFTComputeShader.FindKernel("Permute");

        CalculateTwiddleFactorsAndInputIndicesTexture();
        PingPongTexture = CreateRenderTexture();
    }

    public void InverseFastFourierTransform(RenderTexture inputTexture) {
        int logSize = (int)Mathf.Log(texturesSize, 2);
        bool pingPong = false;

        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_InputTexture", inputTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_PingPongTexture", PingPongTexture);
        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_HORIZONTAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_InputTexture", inputTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_PingPongTexture", PingPongTexture);
        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_VERTICAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_PERMUTE, "_InputTexture", inputTexture);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_PERMUTE, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);

        IFFTComputeShader.SetTexture(KERNEL_IFFT_SCALE, "_InputTexture", inputTexture);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_SCALE, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
    }
}
