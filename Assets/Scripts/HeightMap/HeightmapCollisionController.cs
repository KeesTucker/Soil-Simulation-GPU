using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System.Threading;

public class HeightmapCollisionController : MonoBehaviour
{
    struct Heightcell
    {
        public int update;
        public float height;
    };

    [SerializeField] private GameObject colliderObject;

    public int resolution;
    public float cellSize;
    public float startHeight;

    public ComputeBuffer cellBuffer;
    private Heightcell[] cells;

    private bool currentlyWorking = false;

    private BoxCollider[] colliders;

    Thread genColThread = null;

    public void Awake()
    {
        cells = new Heightcell[resolution * resolution];

        genColThread = new Thread(GenerateMeshLoop);
        genColThread.Start();
    }

    public void Setup()
    {
        colliders = new BoxCollider[resolution * resolution];

        for (int x = 0, i = 0; x < resolution; x++)
        {
            for (int z = 0; z < resolution; z++, i++)
            {
                GameObject go = Instantiate(colliderObject);
                go.transform.parent = transform;
                colliders[i] = go.GetComponent<BoxCollider>();
                Vector3 boxStart = new Vector3(x * cellSize, startHeight / 2f, z * cellSize);
                Vector3 boxSize = new Vector3(cellSize, startHeight, cellSize);
                colliders[i].center = boxStart;
                colliders[i].size = boxSize;
            }
        } 
    }

    public void UpdateCollisions()
    {
        if (!currentlyWorking && cellBuffer != null)
        {
            StartCoroutine(GetVoxelData(AsyncGPUReadback.Request(cellBuffer)));
        }
    }
    private IEnumerator GetVoxelData(AsyncGPUReadbackRequest request)
    {
        currentlyWorking = true;
        while (!request.done)
        {
            yield return new WaitForEndOfFrame();
        }
        if (!request.hasError)
        {
            cells = request.GetData<Heightcell>().ToArray();
        }
        currentlyWorking = false;
    }

    private void GenerateMeshLoop()
    {
        while (true)
        {
            if (cells.Length > 0)
            {
                UpdateMesh();
            }
        }
    }

    private void UpdateMesh()
    {  
        for (int x = 0, i = 0; x < resolution; x++)
        {
            for (int z = 0; z < resolution; z++, i++)
            {
                Vector3 boxStart = new Vector3(x * cellSize, cells[i].height / 2f, z * cellSize);
                Vector3 boxSize = new Vector3(cellSize, cells[i].height, cellSize);
                colliders[i].center = boxStart;
                colliders[i].size = boxSize;
            }
        }
    }
}
