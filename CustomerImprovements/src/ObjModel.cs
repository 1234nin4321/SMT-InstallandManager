using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Reads a Wavefront .obj file into a Unity mesh. Only what the shopping cart needs: positions, texture
    // coordinates, normals and polygon faces, all in one mesh with one material.
    static class ObjModel
    {
        // OBJ files are right-handed and Unity is left-handed, so every point is mirrored along Z (which also turns
        // the model around) and faces are wound the other way. `transform` is then applied to every point.
        public static Mesh Load(string path, Matrix4x4 transform)
        {
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var vertices = new List<Vector3>();
            var meshUVs = new List<Vector2>();
            var meshNormals = new List<Vector3>();
            var triangles = new List<int>();
            var seen = new Dictionary<string, int>();
            bool missingNormals = false;
            var face = new List<int>();

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length < 2) continue;
                var parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "v":
                        positions.Add(transform.MultiplyPoint3x4(new Vector3(Float(parts[1]), Float(parts[2]), -Float(parts[3]))));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(Float(parts[1]), Float(parts[2])));
                        break;
                    case "vn":
                        normals.Add(transform.MultiplyVector(new Vector3(Float(parts[1]), Float(parts[2]), -Float(parts[3]))).normalized);
                        break;
                    case "f":
                        face.Clear();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (!seen.TryGetValue(parts[i], out int index))
                            {
                                var ids = parts[i].Split('/');
                                index = vertices.Count;
                                vertices.Add(positions[Index(ids[0], positions.Count)]);
                                meshUVs.Add(ids.Length > 1 && ids[1].Length > 0 ? uvs[Index(ids[1], uvs.Count)] : Vector2.zero);
                                if (ids.Length > 2 && ids[2].Length > 0) meshNormals.Add(normals[Index(ids[2], normals.Count)]);
                                else
                                {
                                    meshNormals.Add(Vector3.up);
                                    missingNormals = true;
                                }
                                seen[parts[i]] = index;
                            }
                            face.Add(index);
                        }
                        // A fan of triangles, wound the other way round for Unity
                        for (int i = 1; i + 1 < face.Count; i++)
                        {
                            triangles.Add(face[0]);
                            triangles.Add(face[i + 1]);
                            triangles.Add(face[i]);
                        }
                        break;
                }
            }

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, meshUVs);
            mesh.SetNormals(meshNormals);
            mesh.SetTriangles(triangles, 0);
            if (missingNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Float(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        // OBJ indices start at 1, and negative ones count back from the end
        static int Index(string s, int count)
        {
            int i = int.Parse(s, CultureInfo.InvariantCulture);
            return i < 0 ? count + i : i - 1;
        }
    }
}
