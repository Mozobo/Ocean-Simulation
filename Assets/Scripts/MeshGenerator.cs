using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// This script generates a procedural mesh plane
// Based on https://catlikecoding.com/unity/tutorials/procedural-grid/

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour
{
    // Length of one side of the mesh in Unity units, forming a plane of size planeSize x planeSize
    [SerializeField, Range(1, 100000)] public int planeSize = 1000;
    // Controls the resolution of the mesh by adjusting the size of the triangles generated
    [SerializeField, Range(1, 1000)] public int trianglesSize = 10; 
    private Mesh mesh;

    // Generates vertices for the mesh, centering the plane at the GameObject's position
    private void GenerateVertices(){
        int verticesPerRow = planeSize / trianglesSize;
        float halfLength = planeSize * 0.5f;
        // Spacing between vertices
        float spacing = planeSize / (float)verticesPerRow;

        Vector3[] vertices = new Vector3[(verticesPerRow + 1) * (verticesPerRow + 1)];

		for (int i = 0, z = 0; z <= verticesPerRow; z++) {
			for (int x = 0; x <= verticesPerRow; x++, i++) {
                // Calculate vertex position in object space
				vertices[i] = new Vector3((float)x * spacing - halfLength, 0, (float)z * spacing - halfLength);
			}
		}

        mesh.vertices = vertices;
    }

    // Generates triangles for the mesh using two triangles per grid cell
    private void GenerateTriangles(){
        int verticesPerRow = planeSize / trianglesSize;
        
        int[] triangles = new int[verticesPerRow  * verticesPerRow  * 6];

		for (int ti = 0, vi = 0, z = 0; z < verticesPerRow ; z++, vi++) {
			for (int x = 0; x < verticesPerRow ; x++, ti += 6, vi++) {
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + verticesPerRow  + 1;
				triangles[ti + 5] = vi + verticesPerRow  + 2;
			}
		}

        mesh.triangles = triangles;
    }

    void Awake(){
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "Procedural Water plane";
        // This is needed to generate a plane with more than 65.536 vertices, allowing up to 2^32 vertices
        mesh.indexFormat = IndexFormat.UInt32;

        GenerateVertices();
        GenerateTriangles();
    }
}
