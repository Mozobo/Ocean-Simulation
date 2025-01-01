using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This class implements the Inverse Fast Fourier Transform (IFFT) following https://doi.org/10.15480/882.1436.
// It is designed to process data as a variable and is not intended to be attached directly to a GameObject.
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

        // Create the texture that will store the twiddle factors and input indices for the Cooley-Tukey algorithm.
        // https://doi.org/10.15480/882.1436 ("4.2.6 Butterfly Texture" section)
        int logSize = (int)Mathf.Log(texturesSize, 2);
        TwiddleFactorsAndInputIndicesTexture = new RenderTexture(logSize, texturesSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
        TwiddleFactorsAndInputIndicesTexture.filterMode = FilterMode.Point;
        TwiddleFactorsAndInputIndicesTexture.wrapMode = TextureWrapMode.Repeat;
        TwiddleFactorsAndInputIndicesTexture.enableRandomWrite = true;
        TwiddleFactorsAndInputIndicesTexture.Create();
        
        // Set parameters and dispatch the Compute Shader to fill the texture.
        this.IFFTComputeShader.SetInt("_TextureSize", texturesSize);
        this.IFFTComputeShader.SetInt("_NbCascades", nbCascades);
        this.IFFTComputeShader.SetTexture(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        this.IFFTComputeShader.Dispatch(KERNEL_IFFT_PRECOMPUTE_FACTORS_AND_INDICES, logSize, texturesSize/2/LOCAL_WORK_GROUPS_Y, 1);

        // Create the "Ping Pong" textures that will store the intermediate IFFT computations.
        // One "Ping Pong" texture for each cascade
        // https://doi.org/10.15480/882.1436 ("4.2.5 Ping-Pong Texture" section)
        PingPongTextures = new RenderTexture(texturesSize, texturesSize, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
        // PingPongTextures is configured as an array of 2D textures.
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

    // Executes the IFFT on the input texture array, as explained in https://doi.org/10.15480/882.1436 (Chapter 2 and "4.2.1 IFFT Algorithm" section).
    // inputTexturesArray must be a RenderTexture variable configured as Tex2DArray with a volume depth equal to the number of cascades stated in the constructor.
    public void InverseFastFourierTransform(RenderTexture inputTexturesArray) {
        int logSize = (int)Mathf.Log(texturesSize, 2);
        bool pingPong = false;

        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_InputTextures", inputTexturesArray);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_HORIZONTAL_STEP, "_PingPongTextures", PingPongTextures);

        for (int i = 0; i < logSize; i++) {
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_HORIZONTAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
            pingPong = !pingPong;
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_TwiddleFactorsAndInputIndicesTexture", TwiddleFactorsAndInputIndicesTexture);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_InputTextures", inputTexturesArray);
        IFFTComputeShader.SetTexture(KERNEL_IFFT_VERTICAL_STEP, "_PingPongTextures", PingPongTextures);
        
        for (int i = 0; i < logSize; i++) {
            IFFTComputeShader.SetInt("_Step", i);
            IFFTComputeShader.SetBool("_PingPong", pingPong);
            IFFTComputeShader.Dispatch(KERNEL_IFFT_VERTICAL_STEP, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
            pingPong = !pingPong;
        }

        IFFTComputeShader.SetTexture(KERNEL_IFFT_PERMUTE, "_InputTextures", inputTexturesArray);
        IFFTComputeShader.Dispatch(KERNEL_IFFT_PERMUTE, texturesSize / LOCAL_WORK_GROUPS_X, texturesSize / LOCAL_WORK_GROUPS_Y, 1);
    }
}
