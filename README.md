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
- [Buoyancy](#buoyancy)
- [How to use it](#how-to-use-it)
- [Coming next](#coming-next)
- [References](#references)

## Mesh generation

The first step is to generate a mesh that forms the base of the water body. In this project, a low-resolution plane is created. The idea is to increase the number of vertices in the areas close to the camera using a tesselation shader. The code in ```MeshGenerator.cs``` is based on [Catlike Coding's Procedural Grid tutorial](https://catlikecoding.com/unity/tutorials/procedural-grid/), but adjusted to have the plane centered at the GameObject's position and to allow the modification of the triangles's size.

> [!TIP]
> For big planes I recommend keeping the triangles's size between 25 and 100 . Below 25 will be too resource consuming due to the shader running for each tesselated triangle and above 100 will probably not have enough resolution even with the tesselation.

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


This texture encodes the energy distribution of various wave components. Each value corresponds to a specific combination of frequency and direction, defining the amplitude of a wave in the frequency domain. These amplitudes are then transformed via an IFFT to compute the time-domain surface height and motion of the waves.

## IFFT

The Inverse Fast Fourier Transform is a mathematical algorithm used to convert the frequency-domain data into its corresponding time-domain representation. The implementation in the ```IFFT.cs``` script and the ```IFFT.compute``` compute shader follows the [Cooley-Tukey](https://en.wikipedia.org/wiki/Cooley%E2%80%93Tukey_FFT_algorithm) algorithm walkthrough by [Fynn-Jorin Flügge](https://doi.org/10.15480/882.1436).

## Cascades

To maintain real-time performance, the size of the generated texture must remain within certain limits. However, this often results in noticeable tiling artifacts, especially when observing the water from elevated perspectives.

> [!NOTE]  
> An image showing the tiling will be added here.

An approach to mitigate this issue is to use multiple cascades instead of relying on a single texture. The wave generation process will be performed for each cascade based on their wavelength, blending them together to create a more natural water surface.

In Unity, this functionality is implemented using a [RenderTexture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderTexture.html) configured as a texture array. Texture arrays allow multiple layers to be stored and accessed efficiently within a single object, making them well-suited for passing to compute shaders. Each layer in the array represents a cascade, enabling the compute shader to process multiple cascades simultaneously while minimizing the overhead of managing individual textures.

> [!IMPORTANT]  
> This approach introduces additional computational overhead because each cascade requires its own set of calculations and resources. I recommend a maximum of 4 cascades.

> [!IMPORTANT]  
> Cascades cannot be dynamically added or removed during execution. Any changes to the number of cascades or their properties must be done before entering Play Mode.

## Shader
## Buoyancy
## How to use it
## Coming next
## References

