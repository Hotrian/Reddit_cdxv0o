using System;
using System.Collections.Generic;
using UnityEngine;

namespace Honey.Meshing
{
    public static class CubeData
    {
        /*
         *  Y
         *  | Z
         *  |/
         *  .———X
         *
         *    6———7
         *   /|  /|
         *  2———3 |
         *  | 4—|—5
         *  |/  |/ 
         *  0———1
         *
         *  Sides:
         *  2———3  3———7  7———6  6———2
         *  | Z |  | X |  | Z |  | X |
         *  | - |  | + |  | + |  | - |
         *  0———1  1———5  5———4  4———0
         *  
         *  Bottom/Top
         *  0———1  6———7
         *  | Y |  | Y |
         *  | - |  | + |
         *  4———5  2———3
         *
         *  2———3                3         2———3 
         *  |   |  becomes      /|   and   |  / 
         *  |   |             /  |         |/ 
         *  0———1            0———1         0 
         *
         *  Triangles 3-1-0 and 2-3-0
         *  Quad 0-1-3-2
         */
        public static Vector3[] Vertices = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(0f, 1f, 1f),
            new Vector3(1f, 1f, 1f)
        };
        public static Vector2[] UVs = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        public static Vector3[] Normals = new[]
        {
            new Vector3(0f, 0f, -1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f)
        };
        public static int[][] Indices = new[]
        {
            new[] {0, 3, 1, 0, 2, 3}, // Z-
            new[] {4, 2, 0, 4, 6, 2}, // X-
            new[] {4, 1, 5, 4, 0, 1}, // Y-
            new[] {5, 6, 4, 5, 7, 6}, // Z+
            new[] {1, 7, 5, 1, 3, 7}, // X+
            new[] {2, 7, 3, 2, 6, 7}  // Y+
        };

        public static void AddCube(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, Vector3 pos, bool recycleVertices = false)
        {
            AddCube(ref v, ref u, ref n, ref i, pos, Vector3.one, recycleVertices);
        }
        public static void AddCube(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, Vector3 pos, Vector3 scale, bool recycleVertices = false)
        {
            var count = v.Count;
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Back, scale, recycleVertices);
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Left, scale, recycleVertices);
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Bottom, scale, recycleVertices);
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Front, scale, recycleVertices);
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Right, scale, recycleVertices);
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Top, scale, recycleVertices);
        }
        public static void AddCube(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, Vector3 pos, byte[] neighbors, bool recycleVertices = false)
        {
            AddCube(ref v, ref u, ref n, ref i, pos, Vector3.one, neighbors, recycleVertices);
        }
        public static void AddCube(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, Vector3 pos, Vector3 scale, byte[] neighbors, bool recycleVertices = false)
        {
            var count = v.Count;
            if (neighbors[(int)CubeFace.Back] == 0)   AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Back, scale, recycleVertices);
            if (neighbors[(int)CubeFace.Left] == 0)   AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Left, scale, recycleVertices);
            if (neighbors[(int)CubeFace.Bottom] == 0) AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Bottom, scale, recycleVertices);
            if (neighbors[(int)CubeFace.Front] == 0)  AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Front, scale, recycleVertices);
            if (neighbors[(int)CubeFace.Right] == 0)  AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Right, scale, recycleVertices);
            if (neighbors[(int)CubeFace.Top] == 0)    AddFace(ref v, ref u, ref n, ref i, ref count, pos, CubeFace.Top, scale, recycleVertices);
        }

        public static void AddFace(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, ref int count, Vector3 pos, CubeFace face, bool recycleVertices = false)
        {
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, face, Vector3.one, recycleVertices);
        }

        public static void AddFace(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, ref int count, Vector3 pos, CubeFace face, float scale, bool recycleVertices = false)
        {
            AddFace(ref v, ref u, ref n, ref i, ref count, pos, face, new Vector3(scale, scale, scale), recycleVertices);
        }

        public static void AddFace(ref List<Vector3> v, ref List<Vector2> u, ref List<Vector3> n, ref List<int> i, ref int count, Vector3 pos, CubeFace face, Vector3 scale, bool recycleVertices = false)
        {
            var f = (int)face;
            if (recycleVertices)
            {
                var newVertices = 0;
                for (var j = 0; j < Indices[f].Length; j++)
                {
                    int? index = null;
                    for (var k = 0; k < v.Count; k++)
                    {
                        // Compare point position
                        if (!(Math.Abs(v[k].x - (pos.x + (Vertices[Indices[f][j]].x * scale.x))) < 0.000001f) ||
                            !(Math.Abs(v[k].y - (pos.y + (Vertices[Indices[f][j]].y * scale.y))) < 0.000001f) ||
                            !(Math.Abs(v[k].z - (pos.z + (Vertices[Indices[f][j]].z * scale.z))) < 0.000001f)) continue;
                        // Compare point normals
                        if (!(Math.Abs(n[k].x - Normals[f].x) < 0.000001f) ||
                            !(Math.Abs(n[k].y - Normals[f].y) < 0.000001f) ||
                            !(Math.Abs(n[k].z - Normals[f].z) < 0.000001f)) continue;
                        index = k;
                        break;
                    }

                    if (index != null)
                    {
                        i.Add(index.Value);
                    }
                    else
                    {
                        v.Add(new Vector3(pos.x + (Vertices[Indices[f][j]].x * scale.x),
                                          pos.y + (Vertices[Indices[f][j]].y * scale.y),
                                          pos.z + (Vertices[Indices[f][j]].z * scale.z)));
                        u.Add(UVs[j]);
                        n.Add(Normals[f]);
                        i.Add(count + newVertices);
                        newVertices++;
                    }
                }
                count += newVertices;
            }
            else
            {
                for (var j = 0; j < Indices[f].Length; j++)
                {
                    v.Add(new Vector3(pos.x + (Vertices[Indices[f][j]].x * scale.x),
                                      pos.y + (Vertices[Indices[f][j]].y * scale.y),
                                      pos.z + (Vertices[Indices[f][j]].z * scale.z)));
                    u.Add(UVs[j]);
                    n.Add(Normals[f]);
                    i.Add(count + j);
                }
                count += Indices[f].Length;
            }
        }
        public enum CubeFace
        {
            ZNeg,
            XNeg,
            YNeg,
            ZPos,
            XPos,
            YPos,

            Back = 0,
            Left,
            Bottom,
            Front,
            Right,
            Top
        }
    }
}