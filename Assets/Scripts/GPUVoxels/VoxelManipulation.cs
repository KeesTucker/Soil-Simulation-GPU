//Adapted from: https://www.ben-drury.co.uk/index.php/2016/12/19/generating-collision-mesh-voxel-chunk/
// https://80.lv/articles/simple-multithreading-for-unity/

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System.Threading;
using Unity.Collections;

public class VoxelManipulation : MonoBehaviour
{
    public int resolution;
    private int resolution2;
    private int resolution3;
    public float voxelSize;
    public ComputeBuffer voxelBuffer;

    struct Voxel
    {
        public int update;
        public int solid;
        public float density;
        public Vector4 position;
        public int indicy;
    }

    private Voxel[] voxels;

    private bool currentlyWorking = false;

    struct Box
    {
        public Vector3Int pos;
        public Vector3Int size;
    }

    List<Box> boxesPending;
    Box[] boxesWorking;
    //Dictionary<Vector3Int, Vector3Int> boxes = new Dictionary<Vector3Int, Vector3Int>();
    private List<BoxCollider> colliders = new List<BoxCollider>();

    Thread genColThread = null;

    public void Awake()
    {
        voxels = new Voxel[resolution3];

        genColThread = new Thread(GenerateMeshLoop);
        genColThread.Start();
    }

    public void Setup()
    {
        resolution2 = resolution * resolution;
        resolution3 = resolution * resolution * resolution;
        boxesPending = new List<Box>();
    }

    void Update()
    {
        //mainThreadWait.WaitOne();
        //mainThreadWait.Reset();

        if (!currentlyWorking && voxelBuffer != null)
        {
            StartCoroutine(GetVoxelData(AsyncGPUReadback.Request(voxelBuffer)));
        }
        if (voxels.Length > 0)
        {
            SetCollisionMesh();
        }

        //genColThreadWait.Set();

        //SetCollisionMesh();

        //voxelBuffer.GetData(voxelsPending);

        //WaitHandle.SignalAndWait(genColThreadWait, mainThreadWait);
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
            voxels = request.GetData<Voxel>().ToArray();
        }
        currentlyWorking = false;
    }

    private void GenerateMeshLoop()
    {
        //genColThreadWait.Reset();
        //genColThreadWait.WaitOne();
        while (true)
        {
            //genColThreadWait.Reset();

            if (voxels.Length > 0)
            {
                boxesWorking = new Box[boxesPending.Count];
                boxesPending.CopyTo(boxesWorking);
                boxesPending.Clear();
                GenerateMesh();
            }
            
            //WaitHandle.SignalAndWait(mainThreadWait, genColThreadWait);
        }
    }

    private void GenerateMesh()
    {
        //Keeps track of whether a voxel has been checked.  
        bool[,,] tested = new bool[resolution, resolution, resolution];
        for (int x = 0, i = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int z = 0; z < resolution; z++, i++)
                {
                    if (!tested[x, y, z])
                    {
                        tested[x, y, z] = true;
                        if (voxels[i].solid > 0)  //If the voxel contributes to the collision mesh.
                        {
                            Vector3Int boxStart = new Vector3Int(x, y, z);
                            Vector3Int boxSize = new Vector3Int(1, 1, 1);
                            bool canSpreadX = true;
                            bool canSpreadY = true;
                            bool canSpreadZ = true;
                            //Attempts to expand in all directions and stops in each direction when it no longer can.
                            while (canSpreadX || canSpreadY || canSpreadZ)
                            {
                                canSpreadX = TrySpreadX(canSpreadX, ref tested, boxStart, ref boxSize);
                                canSpreadY = TrySpreadY(canSpreadY, ref tested, boxStart, ref boxSize);
                                canSpreadZ = TrySpreadZ(canSpreadZ, ref tested, boxStart, ref boxSize);
                            }
                            Box box;
                            box.pos = boxStart;
                            box.size = boxSize;
                            boxesPending.Add(box);
                        }
                    }
                }
            }
        }
    }
    private bool TrySpreadX(bool canSpreadX, ref bool[,,] tested, Vector3Int boxStart, ref Vector3Int boxSize)
    {
        //Checks the square made by the Y and Z size on the X index one larger than the size of the
        //box.
        int yLimit = boxStart.y + boxSize.y;
        int zLimit = boxStart.z + boxSize.z;
        for (int y = boxStart.y; y < yLimit && canSpreadX; y++)
        {
            for (int z = boxStart.z; z < zLimit; z++)
            {
                int newX = boxStart.x + boxSize.x;
                int newIndex = newX * resolution2 + y * resolution + z;
                if (newX >= resolution || tested[newX, y, z] || voxels[newIndex].solid == 0)
                {
                    canSpreadX = false;
                }
            }
        }
        //If the box can spread, mark it as tested and increase the box size in the X dimension.
        if (canSpreadX)
        {
            for (int y = boxStart.y; y < yLimit; y++)
            {
                for (int z = boxStart.z; z < zLimit; z++)
                {
                    int newX = boxStart.x + boxSize.x;
                    tested[newX, y, z] = true;
                }
            }
            boxSize.x++;
        }
        return canSpreadX;
    }

    private bool TrySpreadY(bool canSpreadY, ref bool[,,] tested, Vector3Int boxStart, ref Vector3Int boxSize)
    {
        //Checks the square made by the X and Z size on the Y index one larger than the size of the
        //box.
        int xLimit = boxStart.x + boxSize.x;
        int zLimit = boxStart.z + boxSize.z;
        for (int x = boxStart.x; x < xLimit && canSpreadY; x++)
        {
            for (int z = boxStart.z; z < zLimit; z++)
            {
                int newY = boxStart.y + boxSize.y;
                int newIndex = x * resolution2 + newY * resolution + z;
                if (newY >= resolution || tested[x, newY, z] || voxels[newIndex].solid == 0)
                {
                    canSpreadY = false;
                }
            }
        }
        //If the box can spread, mark it as tested and increase the box size in the Y dimension.
        if (canSpreadY)
        {
            for (int x = boxStart.x; x < xLimit; ++x)
            {
                for (int z = boxStart.z; z < zLimit; ++z)
                {
                    int newY = boxStart.y + boxSize.y;
                    tested[x, newY, z] = true;
                }
            }
            boxSize.y++;
        }
        return canSpreadY;
    }

    private bool TrySpreadZ(bool canSpreadZ, ref bool[,,] tested, Vector3Int boxStart, ref Vector3Int boxSize)
    {
        //Checks the square made by the X and Y size on the Z index one larger than the size of the
        //box.
        int xLimit = boxStart.x + boxSize.x;
        int yLimit = boxStart.y + boxSize.y;
        for (int x = boxStart.x; x < xLimit && canSpreadZ; x++)
        {
            for (int y = boxStart.y; y < yLimit; y++)
            {
                int newZ = boxStart.z + boxSize.z;
                int newIndex = x * resolution2 + y * resolution + newZ;
                if (newZ >= resolution || tested[x, y, newZ] || voxels[newIndex].solid == 0)
                {
                    canSpreadZ = false;
                }
            }
        }
        //If the box can spread, mark it as tested and increase the box size in the Z dimension.
        if (canSpreadZ)
        {
            for (int x = boxStart.x; x < xLimit; x++)
            {
                for (int y = boxStart.y; y < yLimit; y++)
                {
                    int newZ = boxStart.z + boxSize.z;
                    tested[x, y, newZ] = true;
                }
            }
            boxSize.z++;
        }
        return canSpreadZ;
    }

    private void SetCollisionMesh()
    {
        int colliderIndex = 0;
        int existingColliderCount = colliders.Count;
        foreach (Box box in boxesWorking) {
            //Position is the centre of the box collider for Unity3D.
            Vector3 position = box.pos + ((Vector3)box.size / 2.0f);
            if (colliderIndex < existingColliderCount)  //If an old collider can be reused.
            {
                colliders[colliderIndex].center = position * voxelSize;
                colliders[colliderIndex].size = (Vector3)box.size * voxelSize;
            }
            else  //Else if there were more boxes on this mesh generation than there were on the previous one.
            {
                GameObject boxObject = new GameObject(string.Format("Collider {0}", colliderIndex));
                boxObject.tag = "voxelCollider";
                BoxCollider boxCollider = boxObject.AddComponent<BoxCollider>();
                Transform boxTransform = boxObject.transform;
                boxTransform.parent = transform;
                boxTransform.localPosition = new Vector3();
                boxTransform.localRotation = new Quaternion();
                boxCollider.center = position * voxelSize;
                boxCollider.size = (Vector3)box.size * voxelSize;
                colliders.Add(boxCollider);
            }
            colliderIndex++;
        }
        //Deletes all the unused boxes if this mesh generation had less boxes than the previous one.
        if (colliderIndex < existingColliderCount)
        {
            for (int i = existingColliderCount - 1; i >= colliderIndex; --i)
            {
                Destroy(colliders[i].gameObject);
            }
            colliders.RemoveRange(colliderIndex, existingColliderCount - colliderIndex);
        }
    }
}
