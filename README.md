# Ocean-Simulation

Real-time rendering of realistic ocean-like water surfaces using the Inverse Fast Fourier Transform (IFFT), in combination with the Joint North Sea Wave Project (JONSWAP) spectrum and the Texel-Marsen-Arsloe (TMA) modification, within Unity's Universal Render Pipeline (URP).

> [!NOTE]  
> An image or video showcasing the ocean will be added here.

## Table of contents

- [Mesh generation](#mesh-generation)
- [Ocean spectrum](#ocean-spectrum)
- [IFFT](#ifft)
- [Cascades](#cascades)
- [Shader](#shader)
  - [Tessellation](#tessellation)
  - [Vertex displacement, normals and LODs](#vertex-displacement-normals-and-lods)
  - [Refraction and underwater fog](#refraction-and-underwater-fog)
  - [Subsurface scattering](#subsurface-scattering)
  - [Sky reflection](#sky-reflection)
  - [Sun reflections](#sun-reflections)
  - [Shadows](#shadows)
  - [Final light model](#final-light-model)
- [Buoyancy](#buoyancy)
- [References](#references)

## Mesh generation

The first step is to generate a mesh that forms the base of the water body. In this project, a low-resolution plane is created. The idea is to increase the number of vertices in the areas close to the camera using a tessellation shader. The code in ```MeshGenerator.cs``` is based on [Catlike Coding's Procedural Grid tutorial](https://catlikecoding.com/unity/tutorials/procedural-grid/), but adjusted to have the plane centered at the GameObject's position and to allow the modification of the triangles's size.

> [!TIP]
> For big planes I recommend keeping the triangles's size between 25 and 100 . Below 25 will be too resource consuming due to the shader running for each tesselated triangle and above 100 will probably not have enough resolution even with the tessellation.

> [!IMPORTANT]  
> The plane is created at the start of the execution and modifying plane size and triangle size at run-time will not change it. Only changes in position, rotation and scale will be reflected.

<img src="https://github.com/user-attachments/assets/39e73ab3-db44-4682-a9ea-aca59c19caf8" alt="MeshExample1" width="49.5%"/> <img src="https://github.com/user-attachments/assets/4cc2ce08-c568-4bd6-a4ad-c83c1ff8d334" alt="MeshExample2" width="49.5%"/>

<p align="center">Wireframes of 10000 x 10000 planes with triangles size 25 (left) and 50 (right).</p>

![MeshExample3](https://github.com/user-attachments/assets/697679d0-7f58-44a3-9504-9d263c1c180c)

<p align="center">Shaded plane in both resolutions.</p>

## Ocean spectrum

The method detailed by [Tessendorf](https://www.researchgate.net/publication/264839743_Simulating_Ocean_Water) is used to generate realistic ocean waves. Instead of using the Phillips spectrum, a more physically accurate choice is the TMA spectrum explained by [Horvath](https://dl.acm.org/doi/10.1145/2791261.2791267). The TMA spectrum extends the [JONSWAP](https://wikiwaves.org/Ocean-Wave_Spectra#JONSWAP_Spectrum) spectrum, which models wind-driven waves in deep water, and adjusts it for the effects of shallow water.

The key parameters for the spectrum are:
- $`U_{10}`$: wind speed at a height of 10m above the sea surface.
- $`\theta_{wind}`$: wind direction.
- $`g`$: gravitational acceleration.
- $`F`$: fetch, distance from a lee shore or the distance over which the wind acts on the surface.
- $`D`$: water depth.

The TMA spectrum begins with the [JONSWAP](https://wikiwaves.org/Ocean-Wave_Spectra#JONSWAP_Spectrum) spectrum, defined as:

```math
\alpha=0.076\left( \frac{U_{10}^{2}}{Fg} \right)^{0.22}
```
```math
\gamma=3.3
```
```math
\omega_p=22\left(\frac{g^2}{U_{10}F}\right)^{1/3}
```
```math
r = exp\left[ -\frac{(\omega - \omega_{p})^{2}}{2\sigma^{2}\omega_{p}^{2}} \right]
```
```math
\sigma = \begin{cases} 0.07 & \omega \le \omega_p \\ 0.09 & \omega \gt  \omega_p \end{cases}
```
```math
S_{JONSWAP}(\omega) =\frac{\alpha g^{2}}{\omega^{5}}exp\left[ -\frac{5}{4} \left( \frac{\omega_{p}}{\omega} \right)^{4} \right]\gamma^{r}
```

And modifies it to account for shallow water effects using a depth-limiting factor $\Phi(\omega)$:

```math
\omega_{h} = \omega\sqrt{\frac{D}{g}}
```
```math
\Phi(\omega) = \frac{1}{2}w_{h}^{2} + (-w_{h}^{2} + 2w_{h} - 1) · step(w_{h} - 1)
```
```math
S_{TMA}(\omega) = S_{JONSWAP}(\omega) * \Phi(\omega)
```

To simulate realistic [directional spreading](https://www.sciencedirect.com/topics/engineering/directional-spreading), the wave energy distribution across angles $\theta$ and frequencies $\omega$ is calculated:

```math
s = \begin{cases} 6.97\left( \frac{\omega}{\omega_{p}} \right)^{4.06} & \omega \lt 1.05\omega_p \\ 9.77\left( \frac{\omega}{\omega_{p}} \right)^{\mu} & \omega \ge  1.05\omega_p \end{cases} \space + \space 16tanh(\frac{\omega}{\omega_{p}}) · swell^{2}
```
```math
\mu = -2.33 - 1.45\left( \frac{U_{10}\omega_{p}}{g} - 1.17 \right)
```
```math
D(\omega, \theta) = Q(s)cos^{2s}\left\{ \frac{\left| \theta - \theta_{wind} \right|}{2} \right\}
```

Here, the swell parameter enhances low-frequency energy contributions, capturing the effect of long-period waves.

To suppress small-amplitude, high-frequency waves that add negligible visual or physical effects, [Tessendorf](https://www.researchgate.net/publication/264839743_Simulating_Ocean_Water)'s factor is applied:

```math
exp(-k^{2}fade^{2})
```

The complete directional ocean spectrum is given by:
```math
S(\omega, \theta) = S_{TMA}(\omega) · D(\omega, \theta) · exp(-k^{2}fade^{2})
```

The result of the Fourier amplitudes calculation, implemented in the ```WaterBody.cs``` script and the ```InitialSpectrum.compute``` compute shader, is a texture representing wave energy values distributed across frequencies and directions:

<p align="center">
  <img src="https://github.com/user-attachments/assets/615e731b-0309-4c79-a711-793efbc788a7" alt="RedChannel"/>
  <img src="https://github.com/user-attachments/assets/2c00fb19-38ac-468e-8fa5-2e428cc3307c" alt="GreenChannel"/>
</p>
<p align="center">Red and green channels of a resulting texture example (brightness multiplied by 5 for clearer visibility).</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/3a3212a3-69c0-4a63-8793-0f1a24193d0c" alt="BlueChannel"/>
  <img src="https://github.com/user-attachments/assets/ce3a30c1-234c-4a51-afb5-80bbd3703497" alt="AlphaChannel"/>
</p>
<p align="center">Blue and alpha channels of a resulting texture example (brightness multiplied by 5 for clearer visibility).</p>


This texture encodes the energy distribution of various wave components. Each value corresponds to a specific combination of frequency and direction, defining the amplitude of a wave in the frequency domain.

## IFFT

The Inverse Fast Fourier Transform is a mathematical algorithm used to convert the frequency-domain data into its corresponding time-domain representation. The implementation in the ```IFFT.cs``` script and the ```IFFT.compute``` compute shader follows the [Cooley-Tukey](https://en.wikipedia.org/wiki/Cooley%E2%80%93Tukey_FFT_algorithm) IFFT algorithm walkthrough by [Fynn-Jorin Flügge](https://doi.org/10.15480/882.1436).

## Cascades

To maintain real-time performance, the size of the generated texture must remain within certain limits. However, this often results in noticeable tiling artifacts, especially when observing the water from elevated perspectives.

![TilingExample](https://github.com/user-attachments/assets/28f59d13-7dff-488f-88ca-27313344f023)
<p align="center">Ocean with only one cascade. Visible tiling.</p>

An approach to mitigate this issue is to use multiple cascades instead of relying on a single texture. The wave generation process will be performed for each cascade based on their wavelength, blending them together to create a more natural water surface.

In Unity, this functionality is implemented using [RenderTexture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTexture.html) variables configured as [texture arrays](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.TextureDimension.Tex2DArray.html). Texture arrays allow multiple layers (each representing a cascade) to be stored and accessed efficiently within a single object, enabling the compute shaders to process multiple cascades simultaneously.

> [!TIP]  
> This approach introduces additional computational overhead because each cascade requires its own set of calculations and resources. I recommend a maximum of 4 cascades.

> [!IMPORTANT]  
> In the current implementation, cascades cannot be dynamically added or removed during execution. Any changes to the number of cascades or their properties must be done before entering Play Mode.

![CascadesExample](https://github.com/user-attachments/assets/ba79956d-2529-44d0-9eab-1ca6c2079e18)
<p align="center">Ocean with three cascades. No visible tiling.</p>

## Shader

### Tessellation

The mesh detail is enhanced by applying [tessellation](https://www.khronos.org/opengl/wiki/tessellation) through the shader. [```Water.shader```](https://github.com/Mozobo/Ocean-Simulation/blob/main/Assets/Shaders/Water.shader) follows [NedMakesGames's amazing explanation](https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e) on writing  tessellation shaders for Unity.

Tessellation factors are calculated based on the distance between the triangles and the camera. Within a customizable maximum radius, the tessellation level decreases from an adjustable maximum to none as the distance from the camera increases. For large water bodies to appear realistic, both the tessellation range and maximum tessellation level need to have large values. A linear decrease, like the ones provided by Unity's [Tessellation.cginc](https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/Tessellation.cginc) and [Tessellation.hlsl](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl) APIs, would add too many vertices to regions where the detail wouldn't be noticeable, wasting computing resources. This is why I've implemented an adjustable exponential decay factor that provides enough detail to be convincing while maintaining good performance.

https://github.com/user-attachments/assets/1c8b2512-b94c-465c-b2cf-795aa9eb958d

### Vertex displacement, normals and LODs

The visual movement of the water is achieved by applying the result of the IFFT stage to the vertices of the mesh. After tessellation, the Domain shader function is used to modify the position of each vertex. The total displacement of a vertex is calculated by iterating through all cascades and summing the contributions. A similar process is followed for the normals.

An optimization technique used is [Level Of Detail](https://en.wikipedia.org/wiki/Level_of_detail_(computer_graphics)) (LOD), which determines the [mipmap](https://en.wikipedia.org/wiki/Mipmapping) level of the texture to sample based on the distance from the camera, reducing the workload for distant objects.

https://github.com/user-attachments/assets/518b0be2-b5aa-46c7-b29c-0bf99a76755b

### Refraction and underwater fog

The method used to simulate both effects comes from [Catlike Coding's Looking Through Water tutorial](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/). In this method, the scene rendered behind the water is distorted and blended with an adjustable color based on its distance from the water surface. However, Catlike Coding's tutorial is designed for Unity's Built-in Render Pipeline and relies on the [GrabPass](https://docs.unity3d.com/es/530/Manual/SL-GrabPass.html) feature, which is not well-supported in URP or HDRP. To adapt it for a URP shader using HLSLPROGRAM syntax, the GrabPass is replaced with the [Opaque Texture](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@7.1/manual/universalrp-asset.html#general), declared in the shader as:
```
TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);
```
And the Depth Texture declared as:
```
TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;
```
For these values to be automatically filled by the engine in URP, the Depth and Opaque Textures must be enabled in both the pipeline asset settings and the camera settings.

<p align="center">
  <img src="https://github.com/user-attachments/assets/b2fb8b18-3b97-42a0-bbcd-747ee5ba6758" alt="PipelineAssetEnabledOptions"/>
  <img src="https://github.com/user-attachments/assets/36cf9d9c-2448-4540-829b-7a689c755dbf" alt="CameraSettings"/>
</p>

https://github.com/user-attachments/assets/77a77be7-25ff-4183-b148-ec2de364f504

### Subsurface scattering

[Subsurface scattering](https://en.wikipedia.org/wiki/Subsurface_scattering) happens when light penetrates a surface, such as water, and scatters beneath it before exiting. I've developed an approach that, even though not as realistic or physically accurate as ray marching or others, is very fast and visually convincing.

```math
L_{SS} = C_{SS} · C_{L} · I_{SS} · max(0, H) · (max(0, dot(L, V)))^{4}
```
Where:
- $`I_{SS}`$: Subsurface scattering intensity.
- $`H`$: Wave height. It is clamped to be at least 0, because negative values would result in negative subsurface scattering irradiance, interfering with other components when computing the final light model.
- $`L`$: Light direction.
- $`V`$: View direction. The dot product with the light direction is also clamped for the same reason as the wave height. In this formula, an even exponent ensures the value remains non-negative which technically allows us to omit the clamping, but I leave it as a safeguard for anyone that uses this approach and changes the exponent value because it suits them better.
- $`C_{SS}`$: Subsurface scattering color.
- $`C_{L}`$: Light color.

https://github.com/user-attachments/assets/c5478d7d-75a4-4d4d-93fc-f29d1c3aad05

### Sky reflection

Water reflects the sky’s colors on its surface. A simple way to achieve this is by sampling ```unity_SpecCube0```, a shader variable that stores the currently active reflection probe’s cubemap.

```
float3 reflectionDir = reflect(viewDir, normalWS);
half3 environment = SAMPLE_TEXTURECUBE(unity_SpecCube0, samplerunity_SpecCube0, reflectionDir);
```

If you don't need real-time reflections or a higher-resolution cubemap, you can skip the next paragraphs.

If no reflection probe exists in the scene, ```unity_SpecCube0``` defaults to the skybox, otherwise it stores the cubemap of the last baked probe. By default, reflection probes in URP are baked because it is optimized for performance, meaning they capture a static reflection of the environment rather than updating dynamically each frame. 

To have real-time updates on ```unity_SpecCube0```, instantiate a real-time [ReflectionProbe](https://docs.unity3d.com/2018.4/Documentation/Manual/class-ReflectionProbe.html) at the start of the execution and Unity will do its thing, there is no need to assign it to any shader or material:

```
GameObject probeObject = new GameObject("RealtimeReflectionProbe");
ReflectionProbe reflectionProbe = probeObject.AddComponent<ReflectionProbe>();

reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
reflectionProbe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
reflectionProbe.cullingMask = 0;
```

If performance is a concern, you can use other time-slicing modes or refresh the probe only when necessary.

Unity may use a lower-resolution render target or compress the cubemap's data. You can improve the resolution by assigning a custom cubemap [RenderTexture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTexture.html):

```
RenderTexture realtimeTexture = new RenderTexture(reflectionProbe.resolution, reflectionProbe.resolution, 16);
realtimeTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
realtimeTexture.Create();

reflectionProbe.realtimeTexture = realtimeTexture;
```

This results in more detailed data in ```unity_SpecCube0``` and sharper reflections compared to the default behavior.

https://github.com/user-attachments/assets/4a65c616-c1b7-47f1-8b68-50b893b05602
<p align="center">Sky reflection from <a href="https://assetstore.unity.com/packages/2d/textures-materials/sky/allsky-free-10-sky-skybox-set-146014">AllSky Free's</a> Cartoon Base NightSky, Cold Night and Deep Dusk skyboxes. Real-time skybox changes.</p>


If you are working with bright skyboxes like Unity's default skybox, you can sample it using only the normals for a more uniform result.

![SamplingDifferences](https://github.com/user-attachments/assets/319d2504-8d2e-4697-8997-73f856446406)
<p align="center">Unity's default skybox sampling. Left using view direction and normals, right using only normals.</p>

### Sun reflections

Sun reflections are handled using a [BRDF](https://en.wikipedia.org/wiki/Bidirectional_reflectance_distribution_function) approach. The [Cook–Torrance BRDF](https://graphicscompendium.com/gamedev/15-pbr) (using [Tarun Ramaswamy's implementation](https://rtarun9.github.io/blogs/physically_based_rendering/#what-is-physically-based-rendering)) is the main model since it works well with the microfaceted nature of water. But as the sun lowers, reflections become more scattered and stretched across the surface. To handle this shift, the Cook–Torrance model blends with the [Ashikhmin–Shirley BRDF](https://www.researchgate.net/publication/2523875_An_anisotropic_phong_BRDF_model), which I think better captures how light spreads at glancing angles.

https://github.com/user-attachments/assets/ccbc7531-01de-406d-a3a4-e8311fdf97c3
<p align="center">Sun reflections using Unity's default skybox. As the sun sets, the model goes from plain Cook-Torrance to a Cook-Torrance + Ashikhmin–Shirley hybrid.</p>

### Shadows

Even though water is transparent, objects can still cast visible shadows on its surface. By default, URP does not allow transparent materials to receive shadows, as they are typically excluded from shadow calculations. To work around this, we explicitly sample the [shadow map](https://docs.unity3d.com/Manual/shadow-mapping.html) in the shader to determine the amount of shadow on a fragment:

```
float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
float realtimeShadow  = MainLightRealtimeShadow(shadowCoord);
```

You can tweak the pipeline asset's shadows settings, such as the number of cascades, rendering distance or soft shadows, to match your needs:
<p align="center">
  <img src="https://github.com/user-attachments/assets/786d7aa2-4844-421a-860f-43f23687701d" alt="PipelineAssetShadowsSettings"/>
</p>

In some projects, I've seen shadow map sampling in the vertex shader. This is often done for performance reasons since vertex shading is less expensive than per-fragment calculations, but at the cost of lower resolution:

<p align="center">
  <img src="https://github.com/user-attachments/assets/9c25ef41-1ca3-4208-86de-73d60ba03671" alt="ShadowSamplingVertexShader" width="49.5%"/>
  <img src="https://github.com/user-attachments/assets/b0e6a61f-3595-49f7-b417-007ccb32ae4b" alt="AlphaChannel" width="49.5%"/>
  <img src="https://github.com/user-attachments/assets/46a76fb1-5b8d-48e5-a34a-31e0739a500e" alt="AlphaChannel" width="49.5%"/>
</p>
<p align="center">Results of the different ways to sample the shadows. 
  1 - Get both shadow coords and occlusion amount in the vertex/domain shader, bad resolution. 
  2 - Get shadow coords in the vertex/domain shader and the occlusion amount i the fragment shader, visible separation at cascade transitions. 
  3 - Get both shadow coords and occlusion amount in the fragment shader, good resolution.</p>

https://github.com/user-attachments/assets/59e1a9dc-f387-44a8-bb9f-74961ac4c46f
<p align="center">Real-time shadows with manual sampling.</p>

### Final light model

The final light model combines the previous sections. There are two main light groups: light components that come from under the water, and light components that come from the water surface.

The underwater group is determined by the refracted and attenuated light, as well as subsurface scattering:

```math
L_{underwater} = L_{refraction + fog} + L_{SSS}
```

The surface group consists of sky reflection and direct sunlight reflection, modulated by shadow occlusion:

```math
L_{surface} = L_{sky} + L_{sun} · O_{shadow}
```

[Fresnel effect](https://www.dorian-iten.com/fresnel/), computed using [Schlick's approximation](https://en.wikipedia.org/wiki/Schlick%27s_approximation) and its approximation for the BRDF [suggested by ATLAS developers](https://gpuopen.com/gdc-presentations/2019/gdc-2019-agtd6-interactive-water-simulation-in-atlas.pdf#page=49), governs the blending between them:

```math
R_{0} = \left( \frac{n_{1} - n_{2}}{n_{1} + n_{2}} \right)^{2}
```

```math
R(\theta) = R_{0} + (1 - R_{0}) · \frac{(1 - cos(\theta))^{5 · exp(-2.69 · \alpha_{v})}}{1 + 22.7 · \alpha_{v}^{1.5}}
```

```math
L_{blend} = lerp(L_{underwater}, \space L_{surface}, \space R_{\theta})
```

where:

- $n_{1}$, $n_{2}$: Refractive indices of air and water, respectively.
- $R_{0}$: Reflectance at normal incidence.
- $\alpha_{v}$: Surface roughness
- $\theta$: Angle between the view direction and the surface normal.

Then the blended result is blended again with the shadow color based on the occlusion:

```math
L_{final} = lerp(L_{blend}, \space shadowColor, \space 1 - O_{shadow})
```

<br>
<br>

https://github.com/user-attachments/assets/5f009d10-fcf5-4b18-89fc-641b98ba20e9

https://github.com/user-attachments/assets/43178a34-5ee0-40db-9d9c-4249d91813a9

<p align="center">Examples of the final light model with Unity's default skybox. Sky reflection using only normals.</p>



## Buoyancy

Very simple buoyancy system. The idea is to sample the ocean's height at specific positions so objects can "float" depending on how much volume is submerged.

This is done by sampling the displacement textures, but since the data in RenderTextures is a GPU resource and we need to access it on the CPU, it must be transfered. Unity provides a mechanism for this through [AsyncGPUReadback](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.html). However, as the documentation states, this method introduces a latency of a few frames, delaying the buoyancy behavior in relation to the movement of the waves.

Every frame, [```Waterbody.cs```](https://github.com/Mozobo/Ocean-Simulation/blob/main/Assets/Scripts/WaterBody.cs) requests displacement data from the GPU. Only the first cascade of the displacements is sampled because sampling more cascades would introduce excessive latency. The data in each pixel is then stored in an array:

```
void Update() {
    ...
    AsyncGPUReadback.Request(displacementsTextures, 0, request => {
        if (request.hasError) {
            Debug.LogError("Async GPU Readback failed!");
            return;
        }
        buoyancyData = request.GetData<Color>().ToArray();
    });
}
```

Which allows to have a public function ```GetWaterHeight``` that maps any given position to an index in the array and returns the corresponding water height.

Each object with the [```BuoyantObject.cs```](https://github.com/Mozobo/Ocean-Simulation/blob/main/Assets/Scripts/BuoyantObject.cs) script attached checks the water height at its position, calculates the submerged volume, and applies forces to its rigidbody. This simulates both buoyancy and water drag effects. As this is a very simple buoyancy system, the volume calculation is a simplified approximation based on the object's dimensions (x, y, and z) and the difference between the y-coordinate and the water height. It obviously does not accurately represent objects with non-rectangular shapes, but it provides a fast approach.

> [!NOTE]  
> A video showing boyancy will be added here.

## References

**Mesh generation**

Flick, Jasper. (n.d.). Procedural Grid, a Unity C# tutorial. Catlike Coding. https://catlikecoding.com/unity/tutorials/procedural-grid/

<br>

**Ocean spectrum**

Tessendorf, Jerry. (2001). Simulating Ocean Water. SIG-GRAPH'99 Course Note. ResearchGate. https://www.researchgate.net/publication/264839743_Simulating_Ocean_Water

Christopher J. Horvath. (2015). Empirical directional wave spectra for computer graphics. In Proceedings of the 2015 Symposium on Digital Production (DigiPro '15). Association for Computing Machinery, New York, NY, USA, 29–39. https://doi.org/10.1145/2791261.2791267

WikiWaves. (n.d.). Ocean-Wave Spectra. Wikiwaves. https://wikiwaves.org/Ocean-Wave_Spectra#JONSWAP_Spectrum

Jump Trajectory. (2020, December 6). Ocean waves simulation with Fast Fourier transform. YouTube. https://www.youtube.com/watch?v=kGEqaX4Y4bQ

Acerola. (2023, August 31). I tried simulating the entire ocean. YouTube. https://www.youtube.com/watch?v=yPfagLeUa7k

ScienceDirect (n. d.). Directional Spreading. ScienceDirect. https://www.sciencedirect.com/topics/engineering/directional-spreading

Gamper, Thomas. (2018, Aug 28). Ocean Surface Generation and Rendering. TU Wien. https://www.cg.tuwien.ac.at/research/publications/2018/GAMPER-2018-OSG/GAMPER-2018-OSG-thesis.pdf

Zucconi, Alan. (2015, Sep 16). How to generate Gaussian distributed numbers. Alan Zucconi. https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/

<br>

**IFFT**

Flügge, Fynn-Jorin. (2017). Realtime GPGPU FFT ocean water simulation. TUHH Open Research. https://doi.org/10.15480/882.1436

Wikipedia contributors. (2025, February 18). Cooley–Tukey FFT algorithm. Wikipedia. https://en.wikipedia.org/wiki/Cooley%E2%80%93Tukey_FFT_algorithm

Tessendorf, Jerry. (2001). Simulating Ocean Water. SIG-GRAPH'99 Course Note. ResearchGate. https://www.researchgate.net/publication/264839743_Simulating_Ocean_Water

Jump Trajectory. (2020, December 6). Ocean waves simulation with Fast Fourier transform. YouTube. https://www.youtube.com/watch?v=kGEqaX4Y4bQ

Acerola. (2023, August 31). I tried simulating the entire ocean. YouTube. https://www.youtube.com/watch?v=yPfagLeUa7k

Wolfram Research, Inc. (n.d.). Complex Multiplication. Wolfram MathWorld. https://mathworld.wolfram.com/ComplexMultiplication.html

Wikipedia contributors. (2025, Jan 12). Euler’s formula. Wikipedia. https://en.wikipedia.org/wiki/Euler%27s_formula

Wikipedia contributors. (2025, Jan 21). Butterfly diagram. Wikipedia. https://en.wikipedia.org/wiki/Butterfly_diagram

<br>

**Cascades**

Unity Technologies. (n.d.). Unity - Scripting API: RenderTexture. Unity Documentation. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTexture.html

Unity Technologies. (n.d.). Unity - Scripting API: Rendering.TextureDimension.Tex2DArray. Unity Documentation. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.TextureDimension.Tex2DArray.html

Wikipedia contributors. (2025, Feb 4). Wavelength. Wikipedia. https://en.wikipedia.org/wiki/Wavelength

Wikipedia contributors. (2025, Feb 10). Wavenumber. Wikipedia. https://en.wikipedia.org/wiki/Wavenumber

<br>

**Tesselation**

OpenGL Wiki. (2020, Oct 11). Tessellation. OpenGL Wiki. https://www.khronos.org/opengl/wiki/tessellation

NedMakesGames. (2021, Nov 24). Mastering tessellation shaders and their many uses in unity. Medium. https://nedmakesgames.medium.com/mastering-tessellation-shaders-and-their-many-uses-in-unity-9caeb760150e

Flick, Jasper. (2017, Nov 30). Tessellation. Catlike Coding. https://catlikecoding.com/unity/tutorials/advanced-rendering/tessellation/

Unity Technologies. (n.d.). Unity - Manual: Tessellation Surface Shader examples in the Built-In Render Pipeline. Unity Documentation. https://docs.unity3d.com/Manual/SL-SurfaceShaderTessellation.html

TwoTailsGames. (n.d.). Unity-Built-in-Shaders/CGIncludes/Tessellation.cginc at master · TwoTailsGames/Unity-Built-in-Shaders. Github. https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/Tessellation.cginc

Unity Technologies. (n.d.). Graphics/Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl at master · Unity-Technologies/Graphics. GitHub. https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.core/ShaderLibrary/Tessellation.hlsl

<br>

**Vertex displacement, normals and LODs**

Wikipedia contributors. (2025, Feb 5). Level of detail (computer graphics). Wikipedia. https://en.wikipedia.org/wiki/Level_of_detail_(computer_graphics)

Wikipedia contributors. (2024, November 28). MIPMap. Wikipedia. https://en.wikipedia.org/wiki/Mipmapping

<br>

**Refraction and underwater fog**

Flick, Jasper. (2018, August 30). Looking through water. Catlike Coding. https://catlikecoding.com/unity/tutorials/flow/looking-through-water/

Unity Technologies. (n.d.). Unity - Manual: ShaderLab: GrabPass. Unity Documentation. https://docs.unity3d.com/es/530/Manual/SL-GrabPass.html

Unity Technologies. (n.d.). Universal Render Pipeline Asset. Unity Documentation. https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@7.1/manual/universalrp-asset.html#general

<br>

**Subsurface scattering**

Wikipedia contributors. (2024, May 18). Subsurface scattering. Wikipedia. https://en.wikipedia.org/wiki/Subsurface_scattering

Zucconi, Alan. (2017, Aug 30). Fast subsurface Scattering in unity (Part 1). Alan Zucconi. https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-1/

Zucconi, Alan. (2017, Aug 30). Fast subsurface Scattering in unity (Part 2). Alan Zucconi. https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-2/

Andersson, Tomas. (2018, May 19). Real-time water shader in unity. Real-time Water Shader in Unity. https://unitywatershader.wordpress.com/

<br>

**Sky reflection**

rpgwhitelock. (2024, Jul 20). AllSky Free - 10 Sky / SkyBox Set | 2D Sky | Unity Asset Store. Unity Asset Store. https://assetstore.unity.com/packages/2d/textures-materials/sky/allsky-free-10-sky-skybox-set-146014

<br>

**Sun reflections**

Wikipedia contributors. (2024, Oct 3). Bidirectional reflectance distribution function. Wikipedia. https://en.wikipedia.org/wiki/Bidirectional_reflectance_distribution_function

Dunn, Ian & Wood, Zoë. (n.d.). Cook-Torrance Reflectance Model. Graphics Programming Compendium. https://graphicscompendium.com/gamedev/15-pbr

Ramaswamy, Tarun. (n. d.). Notes on physically based rendering. Tarun Ramaswamy. https://rtarun9.github.io/blogs/physically_based_rendering/

Ashikhmin, Michael & Shirley, Peter. (2001). An anisotropic phong BRDF model. Journal of Graphics Tools. 5. 10.1080/10867651.2000.10487522. https://www.researchgate.net/publication/2523875_An_anisotropic_phong_BRDF_model

<br>

**Shadows**

Unity Technologies. (n.d.). Use shadows in a custom URP shader. Unity Documentation  https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/use-built-in-shader-methods-shadows.html

Unity Technologies. (n.d.). Unity - Manual: Shadow mapping. Unity Documentation. https://docs.unity3d.com/Manual/shadow-mapping.html

Ned Makes Games. (2022, August 15). Let There Be Light (And Shadow) | Writing Unity URP Code Shaders Tutorial. YouTube. https://www.youtube.com/watch?v=1bm0McKAh9E

<br>

**Final light model**

Iten, Dorian. (n.d.). Understanding the Fresnel Effect. Doprian Iten. https://www.dorian-iten.com/fresnel/

Wikipedia contributors. (2024, Dec 26). Schlick’s approximation. Wikipedia. https://en.wikipedia.org/wiki/Schlick%27s_approximation

Mihelich, Mark & Tcheblokov, Tim. (2019, Mar 18). Wakes, Explosions and Lighting: Interactive Water Simulation in 'ATLAS'. Game Developers Conference. https://gpuopen.com/gdc-presentations/2019/gdc-2019-agtd6-interactive-water-simulation-in-atlas.pdf

<br>

**Buoyancy**

Unity Technologies. (n.d.). Unity - Scripting API: AsyncGPUReadback. Unity Documentation. https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.html
