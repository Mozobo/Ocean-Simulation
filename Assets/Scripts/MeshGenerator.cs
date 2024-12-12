using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour
{
    [SerializeField, Range(1, 10000)]
    public int planeSize = 1000;
    // Reduces the number of vertices used to create a mesh of given size
    [SerializeField, Range(1, 100)]
    public int trianglesSize = 10;
    private Mesh mesh;

    private void GenerateVertices(){
        int verticesPerRow = planeSize / trianglesSize;
        float halfLength = planeSize * 0.5f;
        float spacing = planeSize / (float)verticesPerRow;

        Vector3[] vertices = new Vector3[(verticesPerRow + 1) * (verticesPerRow + 1)];

		for (int i = 0, z = 0; z <= verticesPerRow; z++) {
			for (int x = 0; x <= verticesPerRow; x++, i++) {
				vertices[i] = new Vector3((float)x * spacing - halfLength, 0, (float)z * spacing - halfLength);
			}
		}

        mesh.vertices = vertices;
    }

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
        // This is needed to generate a plane with more than 65.536 vertices
        mesh.indexFormat = IndexFormat.UInt32;

        GenerateVertices();
        GenerateTriangles();
    }
}
