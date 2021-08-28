using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HeightMapController : MonoBehaviour
{
    struct Heightcell
    {
        public int update;
        public float height;
    };
    struct Vert
    {
        public Vector4 position;
        public Vector3 normal;
        public int indicy;
    };

    const int RESOLUTION = 128;
    const int NUM_CELLS = RESOLUTION * RESOLUTION;

    [SerializeField] private HeightmapCollisionController heightmapCollisionController;

    [SerializeField] private Material soilMat;
    [SerializeField] private Material shadowMat;

    [SerializeField] private ComputeShader meshCompute;
    [SerializeField] private ComputeShader editCompute;

    private ComputeBuffer cellBuffer;
    private ComputeBuffer vertBuffer;
    private ComputeBuffer bareVertBuffer;
    private ComputeBuffer triBuffer;
    private ComputeBuffer drawArgsBuffer;

    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool gravityOn = false;
    [SerializeField] private float startingHeight = 5f;
    [SerializeField] private int fallRate = 2;
    [SerializeField] private int xZGradient = 1;
    [SerializeField] private int yGradient = 1;
    [SerializeField] private float size = 16f;

    private float halfSize;
    private float cellSize;

    private Bounds bounds = new Bounds();

    private bool currentlyWorking = false;
    private Vector3[] verts;
    private Mesh colMesh;
    private int[] tris;
    [SerializeField]private MeshCollider meshCollider;

    public int fillType;
    [SerializeField] private int editRadius;
    private Vector3 edit = -Vector3.one;

    void Start()
    {
        if (RESOLUTION % 8 != 0)
        {
            throw new System.ArgumentException("RESOLUTION must be divisible be 8");
        }
        halfSize = size * 0.5f;
        cellSize = size / RESOLUTION;
        transform.localPosition = new Vector3(-halfSize, 0, -halfSize);

        cellBuffer = new ComputeBuffer(NUM_CELLS, sizeof(int) + sizeof(float));
        vertBuffer = new ComputeBuffer(NUM_CELLS, sizeof(float) * 7 + sizeof(int));
        bareVertBuffer = new ComputeBuffer(NUM_CELLS, sizeof(float) * 3);
        triBuffer = new ComputeBuffer(NUM_CELLS * 6, sizeof(int));
        drawArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        Heightcell[] cellInit = new Heightcell[NUM_CELLS];
        for (int x = 0; x < RESOLUTION; x++)
        {
            for (int z = 0; z < RESOLUTION; z++)
            {
                int i = x * RESOLUTION + z;
                Heightcell c = new Heightcell();
                c.update = 1;
                c.height = startingHeight;
                cellInit[i] = c;
            }
        }
        cellBuffer.SetData(cellInit);

        verts = new Vector3[NUM_CELLS];
        for (int x = 0; x < RESOLUTION; x++)
        {
            for (int z = 0; z < RESOLUTION; z++)
            {
                int i = x * RESOLUTION + z;
                verts[i] = new Vector3(x * cellSize, startingHeight, z * cellSize);
            }
        }

        tris = new int[NUM_CELLS * 6];
        for (int x = 0; x < RESOLUTION; x++)
        {
            for (int z = 0; z < RESOLUTION; z++)
            {
                int i = x * RESOLUTION + z;
                if (x < RESOLUTION - 1 && z < RESOLUTION - 1)
                {
                    tris[i * 6] = i;
                    tris[i * 6 + 1] = i + 1;
                    tris[i * 6 + 2] = i + RESOLUTION + 1;
                    tris[i * 6 + 3] = i + RESOLUTION + 1;
                    tris[i * 6 + 4] = i + RESOLUTION;
                    tris[i * 6 + 5] = i;
                }
                else
                {
                    tris[i * 6] = 0;
                    tris[i * 6 + 1] = 0;
                    tris[i * 6 + 2] = 0;
                    tris[i * 6 + 3] = 0;
                    tris[i * 6 + 4] = 0;
                    tris[i * 6 + 5] = 0;
                }
            }
        }
        triBuffer.SetData(tris);

        drawArgsBuffer.SetData(new int[] { NUM_CELLS * 6, 1, 0, 0 });

        bounds.center = Vector3.zero;
        bounds.size = Vector3.one * size;

        /*heightmapCollisionController.resolution = RESOLUTION;
        heightmapCollisionController.cellSize = cellSize;
        heightmapCollisionController.startHeight = startingHeight;
        heightmapCollisionController.cellBuffer = cellBuffer;
        heightmapCollisionController.Setup();*/

        colMesh = new Mesh();
        colMesh.SetVertices(verts);
        colMesh.SetTriangles(tris, 0);
        meshCollider.sharedMesh = colMesh;
    }

    void Update()
    {
        UpdateVerts();
        if (edit != -Vector3.one)
        {
            EditHeightMap();
            edit = -Vector3.one;
        }
        if (!currentlyWorking && bareVertBuffer != null)
        {
            StartCoroutine(GetVertData(AsyncGPUReadback.Request(bareVertBuffer)));
        }
    }

    private IEnumerator GetVertData(AsyncGPUReadbackRequest request)
    {
        currentlyWorking = true;
        while (!request.done)
        {
            yield return new WaitForEndOfFrame();
        }
        if (!request.hasError)
        {
            verts = request.GetData<Vector3>().ToArray();
            colMesh.SetVertices(verts);
            meshCollider.sharedMesh = colMesh;
        }
        currentlyWorking = false;
    }

    private void UpdateVerts()
    {
        meshCompute.SetInt("resolution", RESOLUTION);
        meshCompute.SetFloat("cellSize", cellSize);
        meshCompute.SetFloat("halfSize", halfSize);
        meshCompute.SetBuffer(0, "cells", cellBuffer);
        meshCompute.SetBuffer(0, "bverts", bareVertBuffer);
        meshCompute.SetBuffer(0, "verts", vertBuffer);
        meshCompute.Dispatch(0, RESOLUTION / 8, 1, RESOLUTION / 8);
    }

    private void EditHeightMap()
    {
        editCompute.SetInt("resolution", RESOLUTION);
        editCompute.SetVector("coord", edit);
        editCompute.SetInt("radius", editRadius);
        editCompute.SetInt("diameter",editRadius * 2 + 1);
        editCompute.SetInt("sqrRadius", editRadius * editRadius);
        editCompute.SetInt("fillType", fillType);
        editCompute.SetBuffer(0, "cells", cellBuffer);
        editCompute.Dispatch(0, RESOLUTION / 8, 1, RESOLUTION / 8);
    }

    public void EditCells(RaycastHit hitInfo)
    {
        if (hitInfo.collider.gameObject.tag == "voxelCollider")
        {
            Vector3 point = transform.InverseTransformPoint(hitInfo.point);
            int centerX = (int)(point.x / cellSize);
            int centerZ = (int)(point.z / cellSize);
            edit = new Vector3(centerX, 0, centerZ);
        }
    }    

    private void OnValidate()
    {
        halfSize = size * 0.5f;
        cellSize = size / RESOLUTION;
        transform.localPosition = new Vector3(-halfSize, 0, -halfSize);
    }

    void OnRenderObject()
    {
        soilMat.SetBuffer("vertBuffer", vertBuffer);
        soilMat.SetBuffer("triBuffer", triBuffer);
        soilMat.SetPass(0);

        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, drawArgsBuffer, 0);
    }

    void LateUpdate()
    {
        if (castShadows)
        {
            shadowMat.SetBuffer("vertBuffer", vertBuffer);
            shadowMat.SetBuffer("triBuffer", triBuffer);

            ComputeBuffer.CopyCount(vertBuffer, drawArgsBuffer, 0);
            Graphics.DrawProceduralIndirect(shadowMat, bounds, MeshTopology.Triangles, drawArgsBuffer, 0, null, null, ShadowCastingMode.On, true, 0);
        }
    }

    private void OnDestroy()
    {
        cellBuffer.Release();
        vertBuffer.Release();
        bareVertBuffer.Release();
        triBuffer.Release();
        drawArgsBuffer.Release();
    }
}
