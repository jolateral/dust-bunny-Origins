using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace MeshTools
{
    public static class EdgeHelpers
    {
        public struct Edge
        {
            public int v1;
            public int v2;
            public int triangleIndex;
            public EdgeCircle circle;
            public Edge(int aV1, int aV2, int aIndex)
            {
                v1 = aV1;
                v2 = aV2;
                triangleIndex = aIndex;
                circle = null;
            }
        }

        public class EdgeCircle
        {
            internal EdgeCircle circularNext;
            internal EdgeCircle circularPrev;
        }

        public static List<Edge> GetEdges(int[] aIndices)
        {
            List<Edge> result = new List<Edge>();
            for (int i = 0; i < aIndices.Length; i += 3)
            {
                int v1 = aIndices[i];
                int v2 = aIndices[i + 1];
                int v3 = aIndices[i + 2];
                result.Add(new Edge(v1, v2, i));
                result.Add(new Edge(v2, v3, i));
                result.Add(new Edge(v3, v1, i));
            }
            return result;
        }


        public static List<Edge> FindBoundary(this List<Edge> aEdges)
        {
            List<Edge> result = new List<Edge>(aEdges);
            for (int i = result.Count - 1; i > 0; i--)
            {
                for (int n = i - 1; n >= 0; n--)
                {
                    if (result[i].v1 == result[n].v2 && result[i].v2 == result[n].v1)
                    {
                        // shared edge so remove both
                        result.RemoveAt(i);
                        result.RemoveAt(n);
                        i--;
                        break;
                    }
                }
            }
            return result;
        }

        static bool IsEdgeBoundary(Edge e)
        {
            EdgeCircle c = e.circle;
            return c != null && c.circularNext == c;
        }

        private static void CreateEdge2(Dictionary<int2, int> dictEdges, List<Edge> edges, int index, int v1, int v2, ref int edgesCount)
        {
            int2 eKey = new int2(v1, v2);
            if (v1 > v2) { eKey.x = v2; eKey.y = v1; }
            Edge edge;
            if (!dictEdges.TryGetValue(eKey, out int edgeId))
            {
                edge = new Edge(v1, v2, index);
                dictEdges.Add(eKey, edgesCount);
                AddCircle2(ref edge, new EdgeCircle());
                edges.Add(edge);
                edgesCount++;
                //return edge;
            }
            edge = edges[edgeId];
            AddCircle2(ref edge, new EdgeCircle());
            //return edge;
        }
        static void AddCircle2(ref Edge e, EdgeCircle c)
        {
            if (e.circle == null)
            {
                e.circle = c;
                c.circularNext = c.circularPrev = c;
            }
            else
            {
                c.circularPrev = e.circle;
                c.circularNext = e.circle.circularNext;

                e.circle.circularNext.circularPrev = c;
                e.circle.circularNext = c;

                e.circle = c;
            }
        }
        static public List<Edge> GetEdgesBoundary(int[] tris)
        {
            int faceCount = tris.Length / 3;

            Dictionary<int2, int> dictEdges = new Dictionary<int2, int>();
            List<Edge> edges = new List<Edge>();

            int edgesCount = 0;

            for (int i = 0; i < faceCount; i++)
            {
                int index = i * 3;
                int v1 = tris[index];
                int v2 = tris[index + 1];
                int v3 = tris[index + 2];

                CreateEdge2(dictEdges, edges, index, v1, v2, ref edgesCount);
                CreateEdge2(dictEdges, edges, index + 1, v2, v3, ref edgesCount);
                CreateEdge2(dictEdges, edges, index + 2, v3, v1, ref edgesCount);

                //AddCircle2(e[0], new EdgeCircle());
                //AddCircle2(e[1], new EdgeCircle());
                //AddCircle2(e[2], new EdgeCircle());

                //edges[edgesCount - 3] = e[0];
                //edges[edgesCount - 2] = e[1];
                //edges[edgesCount - 1] = e[2];
            }

            List<Edge> edgesList = new List<Edge>();
            for (int i = 0; i < edgesCount; i++)
            {
                var edge = edges[i];
                if (IsEdgeBoundary(edge))
                {
                    edgesList.Add(edge);
                }
            }

            return edgesList;
        }


        public static class MortonHash
        {
            // Helper function to split a 32-bit integer into its component bytes
            private static void Split(uint input, out byte b0, out byte b1, out byte b2, out byte b3)
            {
                b0 = (byte)(input & 0xFF);
                b1 = (byte)((input >> 8) & 0xFF);
                b2 = (byte)((input >> 16) & 0xFF);
                b3 = (byte)((input >> 24) & 0xFF);
            }

            //// Helper function to combine four bytes into a 32-bit integer
            //private static uint Combine(byte b0, byte b1, byte b2, byte b3)
            //{
            //    return (uint)(b3 << 24 | b2 << 16 | b1 << 8 | b0);
            //}

            public static uint Hash(Vector3 point, int range = 24)
            {
                // Scale the point coordinates to integers in the range [0, 2^24)
                int x = (int)(point.x * (1 << range));
                int y = (int)(point.y * (1 << range));
                int z = (int)(point.z * (1 << range));

                // Split the integers into their component bytes
                byte x0, x1, x2, x3;
                byte y0, y1, y2, y3;
                byte z0, z1, z2, z3;
                Split((uint)x, out x0, out x1, out x2, out x3);
                Split((uint)y, out y0, out y1, out y2, out y3);
                Split((uint)z, out z0, out z1, out z2, out z3);

                // Interleave the bits of the component bytes to create the Morton code
                uint morton = 0;
                morton |= (uint)((z0 << 0) | (y0 << 1) | (x0 << 2));
                morton |= (uint)((z1 << 3) | (y1 << 4) | (x1 << 5)) << (3 * 8);
                morton |= (uint)((z2 << 6) | (y2 << 7) | (x2 << 8)) << (6 * 8);
                morton |= (uint)((z3 << 9) | (y3 << 10) | (x3 << 11)) << (9 * 8);
                return morton;
            }

            //https://stackoverflow.com/questions/1024754/how-to-compute-a-3d-morton-number-interleave-the-bits-of-3-ints
            static uint HashCode(uint x)
            {
                x &= 0x000007FF;//2047
                x = (x | (x << 16)) & 0x030000FF;
                x = (x | (x << 8)) & 0x0300F00F;
                x = (x | (x << 4)) & 0x030C30C3;
                x = (x | (x << 2)) & 0x09249249;
                return x;
            }
            static int mValue = 10000;
            public static uint Code(Vector3 point)
            {
                uint x = (uint)(point.x * 8 * mValue);
                uint y = (uint)(point.y * 8 * mValue);
                uint z = (uint)(point.z * 8 * mValue);
                uint hash = HashCode(x);
                hash |= HashCode(y) << 1;
                hash |= HashCode(z) << 2;
                return hash;
            }
        }

        public static HashSet<uint> GetVertices(this List<Edge> aEdges, Vector3[] meshVerts/*, float3 normalizeVerts, float3 vertsMin*/)
        {
            HashSet<uint> result = new HashSet<uint>();
            int count = aEdges.Count;
            for (int i = 0; i < count; i++)
            {
                var v1 = meshVerts[aEdges[i].v1];
                //v1.x = (vertsMin.x + v1.x);// * normalizeVerts.x;
                //v1.y = (vertsMin.y + v1.y);// * normalizeVerts.y;
                //v1.z = (vertsMin.z + v1.z);// * normalizeVerts.z;
                result.Add(MortonHash.Hash(v1));

                var v2 = meshVerts[aEdges[i].v2];
                //v2.x = (vertsMin.x + v2.x) * normalizeVerts.x;
                //v2.y = (vertsMin.y + v2.y) * normalizeVerts.y;
                //v2.z = (vertsMin.z + v2.z) * normalizeVerts.z;
                result.Add(MortonHash.Hash(v2));
                //result.Add(v2); //TODO maybe v1 is enough?
            }
            return result;//.ToArray();
        }
        public static HashSet<int> GetVertexIndices(this List<Edge> aEdges/*, Vector3[] meshVerts, float3 normalizeVerts, float3 vertsMin*/)
        {
            HashSet<int> result = new HashSet<int>();
            int count = aEdges.Count;
            for (int i = 0; i < count; i++)
            {
                result.Add(aEdges[i].v1);
                result.Add(aEdges[i].v2);
            }
            return result;//.ToArray();
        }
        public static Dictionary<uint, int> GetVerticesVec(this List<Edge> aEdges, Vector3[] meshVerts, out float2 minDistSq /*, float3 normalizeVerts, float3 vertsMin*/)
        {
            minDistSq = float.MaxValue;
            minDistSq.y = 0;
            Dictionary<uint, int> result = new Dictionary<uint, int>();
            int count = aEdges.Count;

            for (int i = 0; i < count; i++)
            {
                var v1 = meshVerts[aEdges[i].v1];
                var v1Code = MortonHash.Code(v1);
                if (!result.ContainsKey(v1Code)) result.Add(v1Code, 0);

                var v2 = meshVerts[aEdges[i].v2];
                var v2Code = MortonHash.Code(v2);
                if (!result.ContainsKey(v2Code)) result.Add(v2Code, 0);

                var d = math.distancesq(v1, v2);
                minDistSq.x = math.min(minDistSq.x, d);
                minDistSq.y = math.max(minDistSq.y, d);
            }
            return result;//.ToArray();
        }

        public static List<Edge> SortEdges(this List<Edge> aEdges)
        {
            List<Edge> result = new List<Edge>(aEdges);
            for (int i = 0; i < result.Count - 2; i++)
            {
                Edge E = result[i];
                for (int n = i + 1; n < result.Count; n++)
                {
                    Edge a = result[n];
                    if (E.v2 == a.v1)
                    {
                        // in this case they are already in order so just continoue with the next one
                        if (n == i + 1)
                            break;
                        // if we found a match, swap them with the next one after "i"
                        result[n] = result[i + 1];
                        result[i + 1] = a;
                        break;
                    }
                }
            }
            return result;
        }

        public static void ExtractElement(Vector3[] verts, ref List<int> tris, out List<int> newTris, out Graph<int> graph, int startIndex = 0)
        {
            var vertices = new List<int>();
            var edges = new List<System.Tuple<int, int>>();
            //Dictionary<int, Vector3> dictVerts = new Dictionary<int, Vector3>();
            for (int i = 0; i < verts.Length; i++)
            {
                vertices.Add(i);
                //dictVerts.Add(i, verts[i]);
            }

            int tCount = tris.Count / 3;
            for (int i = 0; i < tCount; i++)
            {
                if (tris[i * 3 + 0] >= 0 && tris[i * 3 + 1] >= 0 && tris[i * 3 + 2] >= 0)
                {
                    //if (Vector2.Distance(verts[tris[i * 3 + 0]], verts[tris[i * 3 + 1]]) > range) continue;
                    //if (Vector2.Distance(verts[tris[i * 3 + 1]], verts[tris[i * 3 + 2]]) > range) continue;
                    //if (Vector2.Distance(verts[tris[i * 3 + 2]], verts[tris[i * 3 + 0]]) > range) continue;

                    edges.Add(System.Tuple.Create(tris[i * 3 + 0], tris[i * 3 + 1]));
                    edges.Add(System.Tuple.Create(tris[i * 3 + 1], tris[i * 3 + 2]));
                    edges.Add(System.Tuple.Create(tris[i * 3 + 2], tris[i * 3 + 0]));
                }
            }

            graph = new Graph<int>(vertices, edges);
            DepthFirstSearchAlgorithm algorithms = new DepthFirstSearchAlgorithm();

            HashSet<int> element = algorithms.DFS(graph, startIndex);
            newTris = new List<int>();
            //var newVerts = new HashSet<Vector3>();
            for (int i = 0; i < tCount; i++)
            {
                if (element.Contains(tris[i * 3 + 0]) || element.Contains(tris[i * 3 + 1]) || element.Contains(tris[i * 3 + 2]))
                {

                    newTris.Add(tris[i * 3 + 0]);
                    newTris.Add(tris[i * 3 + 1]);
                    newTris.Add(tris[i * 3 + 2]);

                    tris[i * 3 + 0] = -1;
                    tris[i * 3 + 1] = -1;
                    tris[i * 3 + 2] = -1;


                }
            }
            //Debug.Log(string.Join(", ", element));
        }


        /// <summary>
        /// Given a single triangle face of vertex indices, 
        /// returns a list of all the vertices of all linked faces.
        /// </summary>
        /// <param name="pickedTriangle">The known triangle to find linked faces from.</param>
        /// <param name="triangles">The index buffer triangle list of all vertices in the mesh.</param>
        /// <param name="isDestructive"></param>
        /// <returns></returns>
        public static List<int> GetElement(int[] pickedTriangle, ref List<int> triangles, bool isDestructive = true)
        {
            // Create the return result list, starting with the current picked face
            List<int> result = new List<int>(pickedTriangle);

            // Iterate through the triangle list index buffer by triangle (iterations of 3)
            for (int i = 0; i < triangles.Count; i += 3)
            {
                // Select the (i)th triangle in the index buffer
                int[] curTriangle = new int[3] { triangles[i], triangles[i + 1], triangles[i + 2] };

                // Check if faces are linked
                if (IsConnected(curTriangle, pickedTriangle))
                {
                    if (isDestructive)
                    {
                        triangles[i] = -1;
                        triangles[i + 1] = -1;
                        triangles[i + 2] = -1;
                    }

                    // Recursively add all the linked faces to the result
                    result.AddRange(GetElement(curTriangle, ref triangles));
                }
            }

            return result;
        }

        /// <summary>
        /// Given two faces, return whether they share any common vertices.
        /// </summary>
        /// <param name="faceA">Face represented as array of vertex indices.</param>
        /// <param name="faceB">Face represented as array of vertex indices.</param>
        /// <returns>bool - whether the faces are connected. </returns>
        static bool IsConnected(int[] faceA, int[] faceB)
        {
            for (int i = 0; i < faceA.Length; i++)
                for (int j = 0; j < faceB.Length; j++)
                    if (faceA[i] == faceB[j])
                        return true;
            return false;
        }

        public static List<int> RemoveDoubles(this List<int> element)
        {
            //Remove doubles
            HashSet<Vector3Int> trisList = new HashSet<Vector3Int>();
            int triCount = element.Count / 3;
            for (int i = 0; i < triCount; i++)
            {
                trisList.Add(new Vector3Int(element[i * 3 + 0], element[i * 3 + 1], element[i * 3 + 2]));
            }
            element.Clear();

            //element = trisList.SelectMany(vec => new[] { vec.x, vec.y, vec.z }).ToList();
            triCount = trisList.Count;
            for (int i = 0; i < triCount; i++)
            {
                var vec = trisList.ElementAt(i);
                element.Add(vec.x);
                element.Add(vec.y);
                element.Add(vec.z);
            }
            return element;
        }


        public static List<Vector3> WeldPoints(this List<Vector3> newVerts, float threshold = 1.0f)
        {
            int newCount = newVerts.Count;
            for (int v = 0; v < newCount; v++)
            {
                for (int n = 0; n < newCount; n++)
                {
                    if (v != n)
                    {
                        if (Vector3.Distance(newVerts[v], newVerts[n]) < threshold)
                        {
                            newVerts[v] = (newVerts[v] + newVerts[n]) * 0.5f;
                            break;
                        }
                    }
                }
            }
            return newVerts;
        }

        //public static void TestShortestPath()
        //{
        //	var vertices = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        //	var edges = new[]{Tuple.Create(1,2), Tuple.Create(1,3),
        //		Tuple.Create(2,4), Tuple.Create(3,5), Tuple.Create(3,6),
        //		Tuple.Create(4,7), Tuple.Create(5,7), Tuple.Create(5,8),
        //		Tuple.Create(5,6), Tuple.Create(8,9), Tuple.Create(9,10)};
        //	var graph = new Graph<int>(vertices, edges);
        //	var algorithms = new BreadthFirstSearchAlgorithm();
        //	var startVertex = 1;
        //	var shortestPath = algorithms.ShortestPathFunction(graph, startVertex);
        //	foreach (var vertex in vertices)
        //		Debug.Log(string.Format("shortest path to {0,2}: {1}",
        //				vertex, string.Join(", ", shortestPath(vertex))));
        //}


        public static List<int> GetShortestPath(Vector3[] verts, int[] tris, int startVertex = 0, int endVertex = 1)
        {
            var vertices = new List<int>();
            var edges = new List<System.Tuple<int, int>>();
            for (int i = 0; i < verts.Length; i++)
            {
                vertices.Add(i);
            }

            int tCount = tris.Length / 3;
            for (int i = 0; i < tCount; i++)
            {
                if (tris[i * 3 + 0] >= 0 && tris[i * 3 + 1] >= 0 && tris[i * 3 + 2] >= 0)
                {
                    edges.Add(System.Tuple.Create(tris[i * 3 + 0], tris[i * 3 + 1]));
                    edges.Add(System.Tuple.Create(tris[i * 3 + 1], tris[i * 3 + 2]));
                    edges.Add(System.Tuple.Create(tris[i * 3 + 2], tris[i * 3 + 0]));
                }
            }

            var graph = new Graph<int>(vertices, edges);
            var algorithms = new BreadthFirstSearchAlgorithm();

            var shortestPath = algorithms.ShortestPathFunction(graph, startVertex);
            //foreach (var vertex in vertices)
            //	Debug.Log(string.Format("shortest path to {0,2}: {1}",
            //			vertex, string.Join(", ", shortestPath(vertex))));
            return shortestPath(endVertex).ToList();
        }


        public class Graph<T>
        {
            public Graph() { }
            public Graph(Graph<T> collection) { AdjacencyList = new Dictionary<T, HashSet<T>>(collection.AdjacencyList); }
            public Graph(IEnumerable<T> vertices, IEnumerable<System.Tuple<T, T>> edges)
            {
                foreach (var vertex in vertices)
                    AddVertex(vertex);

                foreach (var edge in edges)
                    AddEdge(edge);
            }

            public Dictionary<T, HashSet<T>> AdjacencyList { get; } = new Dictionary<T, HashSet<T>>();

            public void AddVertex(T vertex)
            {
                AdjacencyList[vertex] = new HashSet<T>();
            }

            public void AddEdge(System.Tuple<T, T> edge)
            {
                if (AdjacencyList.ContainsKey(edge.Item1) && AdjacencyList.ContainsKey(edge.Item2))
                {
                    AdjacencyList[edge.Item1].Add(edge.Item2);
                    AdjacencyList[edge.Item2].Add(edge.Item1);
                }
            }
        }

        private class DepthFirstSearchAlgorithm
        {
            public HashSet<T> DFS<T>(Graph<T> graph, T start)
            {
                var visited = new HashSet<T>();

                if (!graph.AdjacencyList.ContainsKey(start))
                    return visited;

                var stack = new Stack<T>();
                stack.Push(start);

                while (stack.Count > 0)
                {
                    var vertex = stack.Pop();

                    if (visited.Contains(vertex))
                        continue;

                    visited.Add(vertex);

                    foreach (var neighbor in graph.AdjacencyList[vertex])
                        if (!visited.Contains(neighbor))
                            stack.Push(neighbor);
                }

                return visited;
            }
        }

        private class BreadthFirstSearchAlgorithm
        {
            public HashSet<T> BFS<T>(Graph<T> graph, T start)
            {
                var visited = new HashSet<T>();
                var queue = new Queue<T>();
                queue.Enqueue(start);
                visited.Add(start);
                while (queue.Count > 0)
                {
                    var vertex = queue.Dequeue();
                    foreach (var neighbor in graph.AdjacencyList[vertex])
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                }
                return visited;
            }

            public HashSet<T> BFS<T>(Graph<T> graph, T start, Action<T> preVisit = null)
            {
                var visited = new HashSet<T>();

                if (!graph.AdjacencyList.ContainsKey(start))
                    return visited;

                var queue = new Queue<T>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    var vertex = queue.Dequeue();

                    if (visited.Contains(vertex))
                        continue;

                    if (preVisit != null)
                        preVisit(vertex);

                    visited.Add(vertex);

                    foreach (var neighbor in graph.AdjacencyList[vertex])
                        if (!visited.Contains(neighbor))
                            queue.Enqueue(neighbor);
                }

                return visited;
            }

            public Func<T, IEnumerable<T>> ShortestPathFunction<T>(Graph<T> graph, T start)
            {
                var previous = new Dictionary<T, T>();

                var queue = new Queue<T>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    var vertex = queue.Dequeue();
                    foreach (var neighbor in graph.AdjacencyList[vertex])
                    {
                        if (previous.ContainsKey(neighbor))
                            continue;

                        previous[neighbor] = vertex;
                        queue.Enqueue(neighbor);
                    }
                }

                Func<T, IEnumerable<T>> shortestPath = v =>
                {
                    var path = new List<T> { };

                    var current = v;
                    while (!current.Equals(start))
                    {
                        path.Add(current);
                        current = previous[current];
                    };

                    path.Add(start);
                    path.Reverse();

                    return path;
                };

                return shortestPath;
            }
        }
    }

    public class BaryCentricDistance
    {

        public BaryCentricDistance(Mesh mesh)
        {
            _mesh = mesh;
            _triangles = _mesh.triangles;
            _vertices = _mesh.vertices;
        }

        public struct Result
        {
            public float distanceSquared;
            public float distance
            {
                get
                {
                    return Mathf.Sqrt(distanceSquared);
                }
            }

            public int triangle;
            public Vector3 normal;
            public Vector3 coords;
            public Vector3 closestPoint;
        }

        int[] _triangles;
        Vector3[] _vertices;
        Mesh _mesh;

        public Result GetClosestTriangleAndPoint(Vector3 point/*, Transform _transform*/)
        {

            //point = _transform.InverseTransformPoint(point);
            var minDistance = float.PositiveInfinity;
            var finalResult = new Result();
            var length = (int)(_triangles.Length / 3);
            for (var t = 0; t < length; t++)
            {
                var result = GetTriangleInfoForPoint(point, t);
                if (minDistance > result.distanceSquared)
                {
                    minDistance = result.distanceSquared;
                    finalResult = result;
                }
            }
            //finalResult.centre = _transform.TransformPoint(finalResult.centre);
            //finalResult.closestPoint = _transform.TransformPoint(finalResult.closestPoint);
            //finalResult.normal = _transform.TransformDirection(finalResult.normal);
            finalResult.distanceSquared = (finalResult.closestPoint - point).sqrMagnitude;
            return finalResult;
        }

        Result GetTriangleInfoForPoint(Vector3 point, int triangle)
        {
            Result result = new Result();

            result.triangle = triangle;
            result.distanceSquared = float.PositiveInfinity;

            if (triangle >= _triangles.Length / 3)
                return result;


            //Get the vertices of the triangle
            var p1 = _vertices[_triangles[0 + triangle * 3]];
            var p2 = _vertices[_triangles[1 + triangle * 3]];
            var p3 = _vertices[_triangles[2 + triangle * 3]];

            result.normal = Vector3.Cross((p2 - p1).normalized, (p3 - p1).normalized);

            //Project our point onto the plane
            var projected = point + Vector3.Dot((p1 - point), result.normal) * result.normal;

            //Calculate the barycentric coordinates
            var u = ((projected.x * p2.y) - (projected.x * p3.y) - (p2.x * projected.y) + (p2.x * p3.y) + (p3.x * projected.y) - (p3.x * p2.y)) /
                    ((p1.x * p2.y) - (p1.x * p3.y) - (p2.x * p1.y) + (p2.x * p3.y) + (p3.x * p1.y) - (p3.x * p2.y));
            var v = ((p1.x * projected.y) - (p1.x * p3.y) - (projected.x * p1.y) + (projected.x * p3.y) + (p3.x * p1.y) - (p3.x * projected.y)) /
                    ((p1.x * p2.y) - (p1.x * p3.y) - (p2.x * p1.y) + (p2.x * p3.y) + (p3.x * p1.y) - (p3.x * p2.y));
            var w = ((p1.x * p2.y) - (p1.x * projected.y) - (p2.x * p1.y) + (p2.x * projected.y) + (projected.x * p1.y) - (projected.x * p2.y)) /
                    ((p1.x * p2.y) - (p1.x * p3.y) - (p2.x * p1.y) + (p2.x * p3.y) + (p3.x * p1.y) - (p3.x * p2.y));

            //result.center = p1 * 0.3333f + p2 * 0.3333f + p3 * 0.3333f;

            //Find the nearest point
            var vector = (new Vector3(u, v, w)).normalized;
            result.coords = vector;

            //work out where that point is
            var nearest = p1 * vector.x + p2 * vector.y + p3 * vector.z;
            result.closestPoint = nearest;
            result.distanceSquared = (nearest - point).sqrMagnitude;

            if (float.IsNaN(result.distanceSquared))
            {
                result.distanceSquared = float.PositiveInfinity;
            }
            return result;
        }

    }

    public static class ListExtra
    {
        public static void Resize<T>(this List<T> list, int sz, T c = default(T))
        {
            int cur = list.Count;
            if (sz < cur)
                list.RemoveRange(sz, cur - sz);
            else if (sz > cur)
                list.AddRange(Enumerable.Repeat(c, sz - cur));
        }
    }
}