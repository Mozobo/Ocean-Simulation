using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Ocean : MonoBehaviour
{
    [SerializeField, Range(1, 1000)]
    public int xSize = 2, zSize = 2;
    private Vector3[] vertices;
    private int[] triangles;
    private Mesh mesh;

    private void GenerateVertices(){
        vertices = new Vector3[(xSize + 1) * (zSize + 1)];

		for (int i = 0, z = 0; z <= zSize; z++) {
			for (int x = 0; x <= xSize; x++, i++) {
				vertices[i] = new Vector3(x, 0, z);
			}
		}

        mesh.vertices = vertices;
    }

    private void GenerateTriangles(){
        int[] triangles = new int[xSize * zSize * 6];

		for (int ti = 0, vi = 0, z = 0; z < zSize; z++, vi++) {
			for (int x = 0; x < xSize; x++, ti += 6, vi++) {
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
				triangles[ti + 5] = vi + xSize + 2;
			}
		}

        mesh.triangles = triangles;
    }

    private void Generate(){
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "Procedural Grid";

        GenerateVertices();
        GenerateTriangles();
    }
    void Awake(){
        Generate();
    }

    // Uncomment this function to visualize vertices
    /*private void OnDrawGizmos () {
        if (vertices == null) {
			return;
		}
		Gizmos.color = Color.black;
		for (int i = 0; i < vertices.Length; i++) {
			Gizmos.DrawSphere(vertices[i], 0.1f);
		}
	}*/
    
}