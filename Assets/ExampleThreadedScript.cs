using System;
using System.Collections.Generic;
using System.Threading;
using Honey.Meshing;
using Honey.Threading;
using UnityEngine;
using Random = System.Random;

public class ExampleThreadedScript : MonoBehaviour
{
    public Material MeshMaterial;
    public ExampleType Example;
    public ChunkScript ChunkScriptPrefab;
    public ChunkAdvancedScript ChunkAdvancedScriptPrefab;
    void Start()
    {
        switch (Example)
        {
            case ExampleType.DummyJobs:
                var randomSeed = new Random();
                Debug.Log("Testing Background Threads");
                for (var i = 0; i < 20; i++)
                {
                    var i1 = i;
                    ThreadManager.Execute((container) =>
                    {
                        var random = new Random(randomSeed.Next());
                        var delay = random.Next(0, 10000);
                        Debug.Log($"Job: {i1} Executes and takes {delay}ms!");
                        Thread.Sleep(delay);
                        Debug.Log($"Job: {i1} Complete!");
                    });
                }
                break;
            case ExampleType.AccessMainThreadVariableBad:
                ThreadManager.Execute((container) =>
                {
                    Debug.Log("You cannot access certain variables from background threads, only the main thread.");
                    Debug.Log("This will throw an error:");
                    try
                    {
                        Debug.Log($"Current time: {Time.time}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                });
                break;
            case ExampleType.AccessMainThreadVariableGood:
                ThreadManager.Execute((container) =>
                {
                    Debug.Log("To access Main Thread only variables, you can use ExecuteOnMainThreadAndWait()");
                    var time = 0f;
                    ThreadManager.ExecuteOnMainThreadAndWait(() => { time = Time.time; });
                    Debug.Log($"The time was {time}");
                });
                break;
            case ExampleType.PerformActionMainThread:
                ThreadManager.Execute((container) =>
                {
                    Debug.Log("If you need to do something on the Main Thread (such as Mesh assignment), you can do it with ExecuteOnMainThread()");
                    Debug.Log("You can check if you are on the Main Thread (but generally you should always know where your code is executing). This action will throw a warning");
                    MainThreadAction();
                    Debug.Log("Lets try that again on the Main Thread");
                    ThreadManager.ExecuteOnMainThread(MainThreadAction);
                });
                break;
            case ExampleType.PerformAssignmentMainThread:
                // Lets create a dummy GameObject to work with
                var go = new GameObject("Dummy", typeof(MeshRenderer), typeof(MeshFilter));
                var meshRenderer = go.GetComponent<MeshRenderer>();
                var meshFilter = go.GetComponent<MeshFilter>();

                ThreadManager.Execute((container) =>
                {
                    Debug.Log("Now lets try making a mesh using the thread pool");

                    // Generate mesh data
                    var vertices = new List<Vector3>();
                    var uvs = new List<Vector2>();
                    var normals = new List<Vector3>();
                    var indices = new List<int>();
                    CubeData.AddCube(ref vertices, ref uvs, ref normals, ref indices, Vector3.zero, false);

                    // Convert these lists to arrays for the mesh
                    var verts = vertices.ToArray();
                    var uv = uvs.ToArray();
                    var norm = normals.ToArray();
                    var ind = indices.ToArray();

                    // Now that we have the data, assign it on the main thread
                    ThreadManager.ExecuteOnMainThread(() =>
                    {
                        meshRenderer.material = MeshMaterial;
                        var mesh = new Mesh
                        {
                            vertices = verts,
                            uv = uv,
                            normals = norm,
                            triangles = ind,
                        };
                        meshFilter.mesh = mesh;
                        Debug.Log("Mesh Assigned!");
                    });
                });
                break;
            case ExampleType.Chunk:
                var chunk = Instantiate(ChunkScriptPrefab.gameObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
                break;
            case ExampleType.ChunkAdvanced:
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(ChunkAdvancedScript.ChunkWidth, 0f, 0f), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(0f, 0f, ChunkAdvancedScript.ChunkDepth), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(ChunkAdvancedScript.ChunkWidth, 0f, ChunkAdvancedScript.ChunkDepth), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(0f, ChunkAdvancedScript.ChunkHeight, 0f), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(ChunkAdvancedScript.ChunkWidth, ChunkAdvancedScript.ChunkHeight, 0f), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(0f, ChunkAdvancedScript.ChunkHeight, ChunkAdvancedScript.ChunkDepth), Quaternion.identity);
                Instantiate(ChunkAdvancedScriptPrefab.gameObject, new Vector3(ChunkAdvancedScript.ChunkWidth, ChunkAdvancedScript.ChunkHeight, ChunkAdvancedScript.ChunkDepth), Quaternion.identity);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void MainThreadAction()
    {
        if (!ThreadManager.IsMainThread())
        {
            Debug.LogWarning("Not on Main Thread!");
            return;
        }
        Debug.Log("On Main Thread!");
        // Do Main Thread Only actions

        Debug.Log($"Current Time: {Time.time}");
    }

    public enum ExampleType
    {
        DummyJobs,
        AccessMainThreadVariableBad,
        AccessMainThreadVariableGood,
        PerformActionMainThread,
        PerformAssignmentMainThread,
        Chunk,
        ChunkAdvanced
    }
}
