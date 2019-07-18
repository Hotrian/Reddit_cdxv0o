using System.Collections.Generic;
using Honey.Meshing;
using Honey.Threading;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class ChunkAdvancedScript : MonoBehaviour
{
    // Set the Voxel Chunk size globally
    public static uint ChunkWidth = 16;
    public static uint ChunkHeight = 16;
    public static uint ChunkDepth = 16;

    // Set the Update Frequency for this chunk test
    public float UpdateFrequency = 0.1f;
    public int VoxelChangesPerUpdate = 3;

    // Auto Getter for our MeshRenderer
    public MeshRenderer MyMeshRenderer => _myMeshRenderer ?? (_myMeshRenderer = GetComponent<MeshRenderer>());
    private MeshRenderer _myMeshRenderer;
    // Auto Getter for our MeshFilter
    public MeshFilter MyMeshFilter => _myMeshFilter ?? (_myMeshFilter = GetComponent<MeshFilter>());
    private MeshFilter _myMeshFilter;

    // Keep track of which thread last updated our mesh
    private uint _jobId;
    private uint _lastJobId;
    private readonly object _jobLocker = new object();

    // Keep track of our current volume, just using bytes for simplicity
    private byte[][][] _voxels;
    private readonly object _voxelLocker = new object();

    private float _updateTime;

    //Debugging
    private static float _debugTime;
    private static int _debugVoxelUpdates;
    private static int _debugMeshUpdates;
    private static readonly object DebugLocker = new object();

    private volatile bool _dirty;
    void Start()
    {
        // Initialize the Voxel data for this Chunk to Air
        lock (_voxelLocker)
        {
            _voxels = new byte[ChunkWidth][][];
            for (var x = 0; x < ChunkWidth; x++)
            {
                _voxels[x] = new byte[ChunkHeight][];
                for (var y = 0; y < ChunkWidth; y++)
                {
                    _voxels[x][y] = new byte[ChunkDepth];
                    for (var z = 0; z < ChunkWidth; z++)
                    {
                        _voxels[x][y][z] = (byte) VoxelType.Air;
                    }
                }
            }
        }
    }

    void Update()
    {
        // Randomly update the Voxel to test its meshing
        if (Time.time > _updateTime + UpdateFrequency)
        {
            _updateTime += UpdateFrequency;

            for (var i = 0; i < VoxelChangesPerUpdate; i++)
            {
                // Attempt to change the Chunk state, searching for a voxel which is different
                // Normally you would just do
                // SetVoxel(Random.Range(0, (int)ChunkWidth), Random.Range(0, (int)ChunkHeight), Random.Range(0, (int)ChunkDepth), (byte)Random.Range(0, 2));
                // But in this case I wanted to test how fast we can update the Mesh
                var attempts = 0;
                var voxel = (byte)Random.Range(0, 2);
                var x = Random.Range(0, (int)ChunkWidth);
                var y = Random.Range(0, (int)ChunkHeight);
                var z = Random.Range(0, (int)ChunkDepth);
                var outOfAttempts = false;
                while (GetVoxel(x, y, z) == voxel)
                {
                    if (attempts > 20)
                    {
                        outOfAttempts = true;
                        break;
                    }
                    voxel = (byte)Random.Range(0, 2);
                    attempts++;
                }

                if (outOfAttempts)
                    continue;
                // We found a voxel that differs from the one we are setting, so try setting it
                SetVoxel(x, y, z, voxel);
            }
        }

        if (_dirty)
        {
            _dirty = false;
            ThreadManager.Execute((container) =>
            {
                // Get the job Id for this job
                var job = GetNextJobId();

                // Arrays are reference types, so create a blank array to grab the values
                var voxels = new byte[ChunkWidth][][];
                for (var x = 0; x < ChunkWidth; x++)
                {
                    voxels[x] = new byte[ChunkHeight][];
                    for (var y = 0; y < ChunkWidth; y++)
                    {
                        voxels[x][y] = new byte[ChunkDepth];
                    }
                }
                // Grab the current state
                lock (_voxelLocker)
                {
                    for (var x = 0; x < ChunkWidth; x++)
                    {
                        for (var y = 0; y < ChunkHeight; y++)
                        {
                            for (var z = 0; z < ChunkDepth; z++)
                            {
                                voxels[x][y][z] = _voxels[x][y][z];
                            }
                        }
                    }
                }

                // Grab the Lists from our Container or generate them
                if (!container.ContainerObjects.ContainsKey("VertexList")) container.ContainerObjects.Add("VertexList", new List<Vector3>());
                if (!container.ContainerObjects.ContainsKey("UVList")) container.ContainerObjects.Add("UVList", new List<Vector2>());
                if (!container.ContainerObjects.ContainsKey("NormalList")) container.ContainerObjects.Add("NormalList", new List<Vector3>());
                if (!container.ContainerObjects.ContainsKey("IndexList")) container.ContainerObjects.Add("IndexList", new List<int>());
                var vertexList = (List<Vector3>)container.ContainerObjects["VertexList"];
                var uvList = (List<Vector2>)container.ContainerObjects["UVList"];
                var normalList = (List<Vector3>)container.ContainerObjects["NormalList"];
                var indexList = (List<int>)container.ContainerObjects["IndexList"];
                vertexList.Clear();
                uvList.Clear();
                normalList.Clear();
                indexList.Clear();

                // Generate mesh data
                for (var x = 0; x < ChunkWidth; x++)
                {
                    for (var y = 0; y < ChunkHeight; y++)
                    {
                        for (var z = 0; z < ChunkDepth; z++)
                        {
                            if (voxels[x][y][z] == 0) continue;
                            CubeData.AddCube(ref vertexList, ref uvList, ref normalList, ref indexList, new Vector3(x, y, z), new byte[]
                            {
                                _getVoxel(voxels, x, y, z - 1),
                                _getVoxel(voxels, x - 1, y, z),
                                _getVoxel(voxels, x, y - 1, z),
                                _getVoxel(voxels, x, y, z + 1),
                                _getVoxel(voxels, x + 1, y, z),
                                _getVoxel(voxels, x, y + 1, z),
                            }, false);
                        }
                    }
                }

                // Convert these lists to arrays for the mesh
                var vertices = vertexList.ToArray();
                var uvs = uvList.ToArray();
                var normals = normalList.ToArray();
                var indices = indexList.ToArray();

                ThreadManager.ExecuteOnMainThread(() =>
                {
                    if (GetLastJobId() > job) // Stale Mesh
                        return;
                    SetLastJobId(job);
                    SetMesh(vertices, uvs, normals, indices);
                });
            });
        }
        //UpdateDebug();
    }

    private byte _getVoxel(byte[][][] voxels, int x, int y, int z)
    {
        if (x < 0 || x >= ChunkWidth) return 0;
        if (y < 0 || y >= ChunkHeight) return 0;
        if (z < 0 || z >= ChunkDepth) return 0;
        return voxels[x][y][z];
    }

    void OnDrawGizmos()
    {
        // Draw a Wire Cube to visualize this Chunk in the Editor   
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireCube(transform.position + new Vector3(ChunkWidth / 2f, ChunkHeight / 2f, ChunkDepth / 2f), new Vector3(ChunkWidth, ChunkHeight, ChunkDepth));
    }

    /// <summary>
    /// Thread Safe! Sets a Voxel in this Chunk and starts a Meshing job.
    /// </summary>
    private void SetVoxel(int x, int y, int z, byte voxelId)
    {
        lock (DebugLocker)
        {
            _debugVoxelUpdates++;
        }
        // Update the Chunk state and grab the current state
        lock (_voxelLocker)
        {
            if (_voxels[x][y][z] == voxelId) return; // Don't need to generate a new mesh if the state is unchanged
            _voxels[x][y][z] = voxelId;
        }

        _dirty = true;
    }

    /// <summary>
    /// Thread Safe! Gets a Voxel in this Chunk.
    /// </summary>
    private byte GetVoxel(int x, int y, int z)
    {
        lock (_voxelLocker)
        {
            return _voxels[x][y][z];
        }
    }

    /// <summary>
    /// Thread Unsafe! Sets the Mesh for this Chunk.
    /// </summary>
    private void SetMesh(Vector3[] verts, Vector2[] uv, Vector3[] norm, int[] ind)
    {
        if (!ThreadManager.IsMainThread())
        {
            Debug.LogWarning("SetMesh() called from a background thread!");
            ThreadManager.ExecuteOnMainThread(() =>
            {
                SetMesh(verts, uv, norm, ind);
            });
            return;
        }
        lock (DebugLocker)
        {
            _debugMeshUpdates++;
        }
        if (MyMeshFilter.mesh == null)
        {
            // We don't have a mesh yet, so just create a new one
            MyMeshFilter.mesh = new Mesh
            {
                vertices = verts,
                uv = uv,
                normals = norm,
                triangles = ind,
                //indexFormat = IndexFormat.UInt32,
            };
        }
        else // We already have a mesh, so update it to reduce garbage
        {
            var mesh = MyMeshFilter.mesh;
            if (verts.Length <= mesh.vertices.Length)
            {
                // The incoming mesh is smaller or the same size as the old mesh
                mesh.triangles = ind; // Update triangles first so we don't reference out of bounds vertices
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.normals = norm;
            }
            else
            {
                // The incoming mesh is larger than the old mesh
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.normals = norm;
                mesh.triangles = ind; // Update triangles last so we don't reference out of bounds vertices
            }
        }
    }

    /// <summary>
    /// Thread Safe! Increments the JobId and returns it.
    /// </summary>
    public uint GetNextJobId()
    {
        lock (_jobLocker)
        {
            _jobId++;
            return _jobId;
        }
    }
    /// <summary>
    /// Thread Safe! Gets the current JobId without incrementing it.
    /// </summary>
    public uint GetCurrentJobId()
    {
        lock (_jobLocker)
        {
            return _jobId;
        }
    }
    /// <summary>
    /// Thread Safe! Sets the last JobId which assigned a Mesh to this voxel.
    /// </summary>
    public void SetLastJobId(uint job)
    {
        lock (_jobLocker)
        {
            _lastJobId = job;
        }
    }
    /// <summary>
    /// Thread Safe! Gets the last JobId which assigned a Mesh to this voxel.
    /// </summary>
    public uint GetLastJobId()
    {
        lock (_jobLocker)
        {
            return _lastJobId;
        }
    }

    #region Debugging

    private void UpdateDebug()
    {
        if (Time.time > _debugTime)
        {
            _debugTime += 1;
            int vUpdates;
            int mUpdates;
            lock (DebugLocker)
            {
                vUpdates = _debugVoxelUpdates;
                _debugVoxelUpdates = 0;
                mUpdates = _debugMeshUpdates;
                _debugMeshUpdates = 0;
            }
            Debug.Log($"Debug: {vUpdates} VoxelUpdates per second. {mUpdates} MeshUpdates per second.");
        }
    }
    #endregion

    public enum VoxelType
    {
        Air = 0,
        Dirt,
    }
}
