using SaturnEngine.Asset;
using Silk.NET.Assimp;
using System.Numerics;

namespace SaturnEngine.SEComponents
{
    public class Model3D : SEComponent
    {
        public class ModelData
        {
            public List<MeshData> Meshes { get; } = new List<MeshData>();
        }

        public class MeshData
        {
            public List<Vector3> Vertices { get; } = new List<Vector3>();
            public List<Vector3> Normals { get; } = new List<Vector3>();
            public List<Vector2> TexCoords { get; } = new List<Vector2>();
            public List<uint> Indices { get; } = new List<uint>();
        }
        public ModelData? md;
        public Model3D()
        {
            CType = SEComponentType.Model3D;

        }
        public unsafe void Load(string fp)
        {
            if (System.IO.File.Exists(fp))
            {
                var a = Assimp.GetApi();
                Silk.NET.Assimp.Scene* s = a.ImportFile(fp, (int)(PostProcessSteps.Triangulate
                | PostProcessSteps.FlipUVs
                | PostProcessSteps.CalculateTangentSpace
                | PostProcessSteps.GenerateNormals
                | PostProcessSteps.OptimizeMeshes));

                if (s == null || s->MFlags == Assimp.SceneFlagsIncomplete)
                    throw new Exception("导入失败");
                md = new ModelData();
                ProcessNode(s->MRootNode, s, md);
                a.ReleaseImport(s);

            }
        }
        private unsafe void ProcessNode(Node* node, Silk.NET.Assimp.Scene* scene, ModelData modelData)
        {
            for (int i = 0; i < node->MNumMeshes; i++)
            {
                var mesh = scene->MMeshes[node->MMeshes[i]];
                modelData.Meshes.Add(ProcessMesh(mesh, scene));
            }

            for (int i = 0; i < node->MNumChildren; i++)
            {
                ProcessNode(node->MChildren[i], scene, modelData);
            }
        }

        private unsafe MeshData ProcessMesh(Mesh* mesh, Silk.NET.Assimp.Scene* scene)
        {
            var meshData = new MeshData();

            // 处理顶点
            if (mesh->MVertices != null)
            {
                for (int i = 0; i < mesh->MNumVertices; i++)
                {
                    meshData.Vertices.Add(mesh->MVertices[i]);
                }
            }

            // 处理法线
            if (mesh->MNormals != null)
            {
                for (int i = 0; i < mesh->MNumVertices; i++)
                {
                    meshData.Normals.Add(mesh->MNormals[i]);
                }
            }

            // 处理纹理坐标
            if (mesh->MTextureCoords[0] != null)
            {
                for (int i = 0; i < mesh->MNumVertices; i++)
                {
                    meshData.TexCoords.Add(new Vector2(
                        mesh->MTextureCoords[0][i].X,
                        mesh->MTextureCoords[0][i].Y
                    ));
                }
            }

            // 处理索引
            if (mesh->MFaces != null)
            {
                for (int i = 0; i < mesh->MNumFaces; i++)
                {
                    var face = mesh->MFaces[i];
                    for (int j = 0; j < face.MNumIndices; j++)
                    {
                        meshData.Indices.Add(face.MIndices[j]);
                    }
                }
            }

            return meshData;
        }

    }
}
