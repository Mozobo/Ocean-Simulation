# Ocean-Simulation

Real-time rendering of realistic ocean-like water surfaces using the Inverse Fast Fourier Transform (IFFT), in combination with the Joint North Sea Wave Project (JONSWAP) spectrum and the Texel-Marsen-Arsloe (TMA) modification, within Unity's Universal Render Pipeline (URP).

> [!NOTE]  
> An image or video showcasing the ocean will be added here.

## Table of contents

- [Mesh generation](#mesh-generation)
- [Ocean spectrum](#ocean-spectrum)
- [IFFT](#ifft)
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

<p align="center" size="12">Wireframes of 10000 x 10000 planes with triangles size 25 (left) and 50 (right).</p>

![MeshExample3](https://github.com/user-attachments/assets/697679d0-7f58-44a3-9504-9d263c1c180c)

<p align="center">Shaded plane in both resolutions.</p>

## Ocean spectrum
## IFFT
## Shader
## Buoyancy
## How to use it
## Coming next
## References

