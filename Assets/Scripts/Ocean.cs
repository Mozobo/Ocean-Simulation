using System.Collections;
using System.Collections.Generic;
using UnityEngine;
﻿using UnityEditor;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Ocean : MonoBehaviour
{
    [SerializeField, Range(1, 1000)]
    public int xSize = 2, zSize = 2;
    private Vector3[] vertices;
    private int[] triangles;
    private Mesh mesh;
    private Texture2D randomNoiseTexture;
    private const string texturesPath = "Assets/Textures/";

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

    // Generates a random number from a Normal Distribution N(0, 1)
    // Extracted from: https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/
    private float GenerateRandomNumber(){
            float v1, v2, s;
            do {
                v1 = 2.0f * Random.Range(0f,1f) - 1.0f;
                v2 = 2.0f * Random.Range(0f,1f) - 1.0f;
                s = v1 * v1 + v2 * v2;
            } while (s >= 1.0f || s == 0f);
            s = Mathf.Sqrt((-2.0f * Mathf.Log(s)) / s);
        
            return v1 * s;
    }

    private Texture2D GenerateRandomNoiseTexture(){
        Texture2D noiseTexture = new Texture2D(xSize, zSize, TextureFormat.RGFloat, false, true);

        noiseTexture.filterMode = FilterMode.Point;
        for (int i = 0; i < xSize; i++)
        {
            for (int j = 0; j < zSize; j++)
            {
                noiseTexture.SetPixel(i, j, new Vector4(GenerateRandomNumber(), GenerateRandomNumber()));
            }
        }
        noiseTexture.Apply();

        #if UNITY_EDITOR
            string filename = "RandomNoiseTexture" + xSize.ToString() + "x" + zSize.ToString()+ ".asset";
            AssetDatabase.CreateAsset(noiseTexture, texturesPath + filename);
        #endif

        return noiseTexture;
    }

    private void GetRandomNoiseTexture(){
        string filename = "RandomNoiseTexture" + xSize.ToString() + "x" + zSize.ToString() + ".asset";
        #if UNITY_EDITOR
            Texture2D noiseTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(texturesPath + filename, typeof(Texture2D));
        #endif
        randomNoiseTexture = noiseTexture ? noiseTexture : GenerateRandomNoiseTexture();
    }

    void Awake(){
        Generate();
        GetRandomNoiseTexture();
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