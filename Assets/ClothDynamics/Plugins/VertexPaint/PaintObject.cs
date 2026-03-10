using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClothDynamics
{
    public static class DeformCoreExtension
    {
        public static bool Contains(this string source, string toCheck, System.StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }
    }

    public static class VertexShaders
    {
        static private Material vertexMaterial;
        static private Shader shader;
        static public Material ShowVertexColor
        {
            get
            {
                if (vertexMaterial != null)
                    return vertexMaterial;

                shader = Resources.Load("DebugControlMesh", typeof(Shader)) as Shader;
                vertexMaterial = new Material(shader);
                vertexMaterial.hideFlags = HideFlags.HideAndDontSave;
                vertexMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
                return vertexMaterial;
            }
            set { }
        }
    }

    [ExecuteInEditMode]
    public class PaintObject : MonoBehaviour
    {
        public enum ColorChannels : int
        {
            RED = 0,
            GREEN,
            BLUE,
            ALPHA
        }
        [HideInInspector]
        public ColorChannels useChannel = ColorChannels.GREEN;
        [HideInInspector]
        public Mesh controlMesh = null;
        [HideInInspector]
        public Texture textureForWeighting = null;
        [HideInInspector]
        public AnimationCurve paintFalloffCurve = new AnimationCurve();
        [HideInInspector]
        public float paintBlendFactor = 0.5f;
        [HideInInspector]
        public float paintRadius = 0.05f;
        [HideInInspector]
        public Color paintColor = Color.red;
        [HideInInspector]
        public Color paintClearColor = Color.black;
        [HideInInspector]
        public float blurRadius = 0.05f;
        [HideInInspector]
        public Color[] vertexColors;
        [HideInInspector]
        public Vector4 tilingOffset = new Vector4(1, 1, 0, 0);
        [HideInInspector]
        public bool frontFacesOnly = false;

        void Awake()
        {
            if (paintFalloffCurve.Evaluate(1) == 0)
                paintFalloffCurve = AnimationCurve.Linear(0, 0, 1, 1);

            var skin = this.GetComponent<SkinnedMeshRenderer>();
            if (skin)
            {
                controlMesh = skin.sharedMesh;
            }
            else if (this.GetComponent<MeshFilter>())
            {
                controlMesh = this.GetComponent<MeshFilter>().sharedMesh;
            }

            if (controlMesh != null && vertexColors != null && vertexColors.Length == controlMesh.vertexCount)
            {
                controlMesh.colors = vertexColors;
            }
        }

        public GameObject CreateGameObjectFromResource(out string path, out string fileName)
        {
            path = "";
#if UNITY_EDITOR
            Object parentObject = PrefabUtility.GetCorrespondingObjectFromSource(this.gameObject);
            path = AssetDatabase.GetAssetPath(parentObject);
            path = Path.GetDirectoryName(path) + "/Resources/";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
#endif
            fileName = this.transform.parent.name + "_CM";
            var res = Resources.Load(fileName) as GameObject;
            if (res == null) { Debug.Log(fileName + " can't be found! Check if the name of the GameObject fits to the control mesh (without _CM_Volume)"); }
            res = Resources.Load(controlMesh.name.Contains("_Volume") ? controlMesh.name.Substring(0, controlMesh.name.Length - 7) : controlMesh.name) as GameObject;
            if (res == null) { Debug.LogError(controlMesh.name + " can't be found!"); return null; }
            return (GameObject)GameObject.Instantiate(res);
        }

        public void BlurColors(Mesh tempMesh = null)
        {
            GameObject tempGO = null;
            if (tempMesh == null)
            {
                string path, fileName;
                tempGO = CreateGameObjectFromResource(out path, out fileName);
                if (tempGO)
                    tempMesh = tempGO.GetComponentInChildren<MeshFilter>().sharedMesh;
            }
            if (tempMesh == null) return;
            KDTree vertexTree = CreateVertexTree(tempMesh);
            if (vertexTree == null) return;
            int searchCount = 64;
            //var mesh = controlMesh.GetType() == typeof(Mesh) ? ((Mesh)controlMesh) : ((GameObject)controlMesh).GetComponentInChildren<MeshFilter>().sharedMesh;
            //if (mesh)
            {
                float searchRadius = blurRadius;
                //Vector3[] fullVerts = mesh.vertices;
                Vector3[] verts = tempMesh.vertices;
                for (int i = 0; i < verts.Length; ++i)
                {
                    var vec = verts[i];
                    HashSet<int> tempHash = new HashSet<int>();
                    vertexTree.nearestAddNbrs(new double[] { vec.x, vec.y, vec.z }, searchCount, ref tempHash);
                    vertexColors[i] = AccumulateColor(tempHash, vec, verts, searchRadius);
                }
            }
            if (tempGO) DestroyImmediate(tempGO);

            //FillVolumeVertices(tempMesh);
        }

        public Color AccumulateColor(HashSet<int> tempHash, Vector3 vec, Vector3[] verts, float searchRadius)
        {
            Color tempColor = Color.clear;
            var hashArray = tempHash.ToArray();
            int count = hashArray.Length;
            if (tempHash != null && count > 0)
            {
                float counter = 0;
                for (int h = 0; h < count; ++h)
                {
                    int index = hashArray[h];
                    if (Vector3.Distance(vec, verts[index]) < searchRadius)
                    {
                        tempColor += vertexColors[index];
                        counter += 1.0f;
                    }
                }
                if (counter > 0) tempColor /= counter;
            }
            return tempColor;
        }

        public void CreateControlMeshVertexColors(Mesh skinMesh = null)
        {
            if (controlMesh)
            {
                var mesh = controlMesh;// controlMesh.GetType() == typeof(Mesh) ? ((Mesh)controlMesh) : ((GameObject)controlMesh).GetComponentInChildren<MeshFilter>().sharedMesh;
                if (skinMesh != null)
                {
                    mesh = skinMesh;
                }

                if (mesh && (vertexColors == null || vertexColors.Length != mesh.vertexCount))
                {
                    vertexColors = new Color[mesh.vertexCount];
                    if (mesh.colors == null || vertexColors.Length != mesh.colors.Length)
                    {
                        for (int i = 0; i < vertexColors.Length; i++)
                            vertexColors[i] = paintClearColor;
                    }
                    else
                    {
                        vertexColors = mesh.colors;
                    }
                }
            }
        }

        public void CopyTextureWeightsToVertexColors(int selectedColorChannel = 0)
        {
            Texture2D tex2D;
            if (textureForWeighting != null)
            {
//#if UNITY_EDITOR
//                if (!textureForWeighting.isReadable)
//                {
//                    if (EditorUtility.DisplayDialog("TextureForWeighting not readable", "Do you want to overwrite the isReadable flag of \"" + textureForWeighting.name + "\" ?", "Yes", "No"))
//                    {
//                        SetReadable(textureForWeighting);
//                    }
//                }
//#endif
                tex2D = (Texture2D)textureForWeighting;
                if (tex2D != null)
                {
                    Mesh lowResMesh = null;
                    //for (int i = 0; i < lodObjects.Length; ++i)
                    {
                        //if (this.transform.childCount == 0)
                        {
                            Mesh m = null;
                            if (this.GetComponentInChildren<MeshFilter>())
                            {
                                var mf = this.GetComponentInChildren<MeshFilter>();
                                if (m == null) m = (mf as MeshFilter).sharedMesh;
                            }
                            else if (this.GetComponentInChildren<SkinnedMeshRenderer>())
                            {
                                var mf = this.GetComponentInChildren<SkinnedMeshRenderer>();
                                m = new Mesh();
                                (mf as SkinnedMeshRenderer).BakeMesh(m);
                                //m = (mf as SkinnedMeshRenderer).sharedMesh;
                            }
                            else { Debug.LogError("No mesh found"); return; }
                            lowResMesh = m;
                            //break;
                        }
                    }
                    if (lowResMesh)
                    {
                        var lowResVerts = lowResMesh.vertices;
                        var lowResUV = lowResMesh.uv;
                        if (lowResVerts.Length != lowResUV.Length) { Debug.LogError("lowResVerts.Length != lowResUV.Length"); return; }
                        KDTree vertexTree = new KDTree(3);
                        HashSet<Vector3> checkVec = new HashSet<Vector3>();
                        for (int i = 0; i < lowResVerts.Length; ++i)
                        {
                            var vec = lowResVerts[i];
                            if (checkVec.Add(vec))
                            {
                                vertexTree.insert(new double[] {
                                vec.x,
                                vec.y,
                                vec.z
                            }, i);
                            }

                        }
                        checkVec.Clear();
                        if (controlMesh)
                        {
                            var mesh = controlMesh.GetType() == typeof(Mesh) ? ((Mesh)controlMesh) : null;// ((GameObject)controlMesh).GetComponentInChildren<MeshFilter>().sharedMesh;
                            if (mesh)
                            {
                                if (mesh.vertexCount != vertexColors.Length) { Debug.LogError("mesh.vertexCount != vertexColors.Length"); return; }
                                var verts = mesh.vertices;
                                int channel = (int)useChannel;

                                var channelColor = Color.clear;
                                channelColor[Mathf.Max(0,selectedColorChannel-1)] = 1;
                                for (int i = 0; i < vertexColors.Length; ++i)
                                {
                                    var vec = verts[i];
                                    int index = (int)vertexTree.nearest(new double[] { vec.x, vec.y, vec.z });
                                    float weight = tex2D.GetPixelBilinear(lowResUV[index].x * tilingOffset.x + tilingOffset.z, lowResUV[index].y * tilingOffset.y + tilingOffset.w)[channel];
                                    vertexColors[i] = (selectedColorChannel==0 ? Color.white : channelColor) * weight;
                                }
                            }
                        }
                    }
                }
            }
        }

        KDTree CreateVertexTree(Mesh tempMesh = null)
        {
            Color[] colrs = tempMesh.colors;
            if (colrs.Length > vertexColors.Length)
                return null;
            Vector3[] verts = tempMesh.vertices;
            HashSet<Vector3> checkVec = new HashSet<Vector3>();
            KDTree vertexTree = new KDTree(3);
            for (int i = 0; i < verts.Length; ++i)
            {
                var vec = verts[i];
                if (checkVec.Add(vec))
                {
                    vertexTree.insert(new double[] {
                            vec.x,
                            vec.y,
                            vec.z
                        }, i);
                }
            }
            checkVec.Clear();
            return vertexTree;
        }


        //        private static void SetReadable(Texture tex)
        //        {
        //#if UNITY_EDITOR
        //            var filePath = AssetDatabase.GetAssetPath(tex);
        //            filePath = filePath.Replace("/", "\\");
        //            string fileText = File.ReadAllText(filePath);
        //            fileText = fileText.Replace("m_IsReadable: 0", "m_IsReadable: 1");
        //            File.WriteAllText(filePath, fileText);
        //            AssetDatabase.Refresh();
        //#endif
        //        }
    }

}
