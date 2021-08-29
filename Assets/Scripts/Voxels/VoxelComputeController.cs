/* Script written by: Kees Tucker
 * Date: August 2021
 * Inspiration taken from:
 * - Catlike Coding https://catlikecoding.com/unity/tutorials/marching-squares/
 * - Scrawk https://github.com/Scrawk/Marching-Cubes-On-The-GPU
 * - BorisTheBrave https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/
 * Big thanks to them! Without them I would have had no idea where to start.
 * 
 * This script controls all the compute shaders in charge of editing the voxels and then rendering them.
 * Basic voxel phsyics are included such as gravity and "spread". 
 * The rendering technique is experimental and takes a lot of inspiration from Dual Contouring but uses a voxel density field instead of the derivative. 
 * It is NOT marching cubes. IT IS ALSO VERY WIP.
 * It produces much lower vert counts compared with marching cubes in exhange for slightly higher computational costs. 
 * It is also very good at rendering low resolution grids and making them appear smooth and rounded.
 * It joins all verts and averages the normals in the same pass as all the other mesh generation.
 * Mesh is dynamically sized so only the correct amount of triangles are rendered. This is accomplished with Append Buffers and DrawProceduralIndirect()
 * Mesh is drawn using DrawProceduralIndirect() and no data is passed between CPU and GPU.
 * There are two seperate shaders as DrawProceduralIndirectNow() renders the geometry and a seperate shader containing only a shadow pass is called in DrawProceduralIndirect().
 * Colliders are generated from box colliders. */

using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using Unity.Collections;

public class VoxelComputeController : MonoBehaviour
{
    struct Voxel
    {
        public int update;
        public int solid;
        public float density;
        public Vector4 position;
        public int indicy;
    }
    struct Vert
    {
        public Vector4 position;
        public Vector3 normal;
        public int indicy;
    };

    //Resolution of grid (multiple of 8) (8 * 14 is max)
    const int RESOLUTION = 8 * 4;
    //Size of voxel buffer and density buffer
    const int NUM_VOXELS = RESOLUTION * RESOLUTION * RESOLUTION;
    //Size of the mesh buffer, square of resolution * number of verts per quad (6) * number of quads per voxel (6))
    const int NUM_VERTS_IN_MESH = RESOLUTION * RESOLUTION * RESOLUTION * 6 * 6;

    [SerializeField] private VoxelCollisionController voxelCollisionController;

    //Make sure this has one of the soil shaders
    [SerializeField] private Material soilMat;
    //Make sure this has the soil/shadow shader
    [SerializeField] private Material shadowMat;
    
    //Calculates edits
    [SerializeField]private ComputeShader editCompute;
    //Calculates gravity
    [SerializeField] private ComputeShader gravityCompute;
    //Calculates Spreading
    [SerializeField] private ComputeShader spreadCompute;
    //Calculates density
    [SerializeField] private ComputeShader densityCompute;
    //Calculates weighted point offsets
    [SerializeField] private ComputeShader offsetCompute;
    //Calculates mesh
    [SerializeField] private ComputeShader meshCompute;
    [SerializeField] private ComputeShader meshStripCompute;

    //Holds status for each voxel
    private ComputeBuffer voxelBuffer;
    //Holds mesh data
    private ComputeBuffer vertBuffer;
    //Indicies
    private ComputeBuffer triBuffer;
    //Args to pass to DrawIndirect()
    private ComputeBuffer drawArgsBuffer;
    //Args to pass to compute shaders
    private ComputeBuffer tablesBuffer;

    [SerializeField] private bool castShadows = true;
    //Should we calculate gravity
    [SerializeField] private bool gravityOn = false;
    //The rate at which voxels fall, arbitrary units atm
    [SerializeField] private int fallRate = 2;
    //Gradients for spreading
    [SerializeField] private int xZGradient = 1;
    [SerializeField] private int yGradient = 1;
    //Size of voxel grid
    [SerializeField] private float size = 2;
    //Half the size, used for centering
    private float halfSize;
    //Size of an individual voxel
    private float voxelSize;
    //To what extent should we round corners?
    [SerializeField] private float angularCompensation = 1f;
    //The radius of voxels we check for volumetric density
    [SerializeField] private int weightingRadius = 3;

    //Edit settings
    [SerializeField] private int radius = 3;
    public int fillType;

    //Private variables for keeping track of stuff
    private Vector3 edit = -Vector3.one;
    private bool updateVoxelPhysics = false;
    private int updateAll = 0;
    private int meshUpdateProgress = 0;

    //Requests for checking if a compute shader is finished
    private bool activeEditRequest = false;
    private AsyncGPUReadbackRequest editRequest;
    private bool activeSpreadRequest = false;
    private AsyncGPUReadbackRequest spreadRequest;
    private bool activeGravityRequest = false;
    private AsyncGPUReadbackRequest gravityRequest;

    //Bounds for drawing shadow
    private Bounds bounds = new Bounds();

    private IEnumerator Start()
    {
        //There are 8 threads run per shader group so resolution must be divisible by 8
        if (RESOLUTION % 8 != 0)
        {
            throw new System.ArgumentException("RESOLUTION must be divisible be 8");
        } 

        halfSize = size * 0.5f;
        voxelSize = size / RESOLUTION;
        transform.localPosition = new Vector3(-halfSize, -halfSize, -halfSize);

        voxelBuffer = new ComputeBuffer(NUM_VOXELS, sizeof(int) * 3 + sizeof(float) * 5);
        vertBuffer = new ComputeBuffer(NUM_VERTS_IN_MESH, sizeof(float) * 7 + sizeof(int), ComputeBufferType.Counter);
        triBuffer = new ComputeBuffer(NUM_VERTS_IN_MESH, sizeof(int), ComputeBufferType.Append);
        tablesBuffer = new ComputeBuffer(85, sizeof(float));
        drawArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        editRequest = AsyncGPUReadback.Request(voxelBuffer);
        spreadRequest = AsyncGPUReadback.Request(tablesBuffer);
        gravityRequest = AsyncGPUReadback.Request(tablesBuffer);

        //Init buffers with starting data
        Voxel[] voxelInit = new Voxel[NUM_VOXELS];
        for (int x = 0; x < RESOLUTION; x++)
        {
            for (int y = 0; y < RESOLUTION; y++)
            {
                for (int z = 0; z < RESOLUTION; z++)
                {
                    Voxel v = new Voxel();
                    v.update = 1;
                    v.density = 0;
                    v.position = new Vector4(0, 0, 0, 0);
                    v.indicy = -1;
                    if (y > RESOLUTION / 2)
                    {
                        v.solid = 0;
                    }
                    else
                    {
                        v.solid = 1;
                    }
                    voxelInit[x * RESOLUTION * RESOLUTION + y * RESOLUTION + z] = v;
                }
            }
        }
        voxelBuffer.SetData(voxelInit);

        Vert[] vertInit = new Vert[NUM_VERTS_IN_MESH];
        for (int i = 0; i < NUM_VERTS_IN_MESH; i++)
        {
            Vert vert;
            vert.position = new Vector4(0, 0, 0, -1f);
            vert.normal = Vector3.zero;
            vert.indicy = -1;
            vertInit[i] = vert;
        }
        vertBuffer.SetData(vertInit);
        //Set the lookup tables
        tablesBuffer.SetData(LookupTables.GetTables(RESOLUTION));
        //Set the DrawIndirect args
        drawArgsBuffer.SetData(new int[] { NUM_VERTS_IN_MESH, 1, 0, 0 });

        //Set up the collider generator
        voxelCollisionController.voxelBuffer = voxelBuffer;
        voxelCollisionController.resolution = RESOLUTION;
        voxelCollisionController.voxelSize = voxelSize;
        voxelCollisionController.Setup();

        updateVoxelPhysics = true;
        updateAll = 1;


        bounds.center = Vector3.zero;
        bounds.size = Vector3.one * size;

        yield return new WaitForSeconds(1f);
        //Just has to wait for buffers to all be filled, kinda janky and I should just wait on the buffer but for now its fine.
        voxelCollisionController.UpdateCollisions();
    }

    //Public method to support editing the voxels directly with the controllers
    public void EditVoxels(RaycastHit hitInfo)
    {
        if (hitInfo.collider.gameObject.tag == "voxelCollider")
        {
            Vector3 point = transform.InverseTransformPoint(hitInfo.point);
            int centerX = (int)(point.x / voxelSize);
            int centerY = (int)(point.y / voxelSize);
            int centerZ = (int)(point.z / voxelSize);
            edit = new Vector3(centerX, centerY, centerZ);
        }
    }

    //Fire off all the compute shaders here, some weird timing stuff going on here, needs to be cleaned up.
    private void Update()
    {
        if (meshUpdateProgress == 0)
        {
            if (edit != -Vector3.one && !activeEditRequest)
            {
                UpdateEdit(edit);
                GL.Flush();
                edit = -Vector3.one;
                editRequest = AsyncGPUReadback.Request(voxelBuffer);
                activeEditRequest = true;
            }
            if (updateVoxelPhysics)
            {
                tablesBuffer.SetData(LookupTables.GetTables(RESOLUTION));
                if (!activeSpreadRequest && gravityOn)
                {
                    UpdateSpread();
                    GL.Flush();
                    spreadRequest = AsyncGPUReadback.Request(tablesBuffer);
                    activeSpreadRequest = true;
                }
                if (!activeGravityRequest && gravityOn)
                {
                    UpdateGravity();
                    GL.Flush();
                    gravityRequest = AsyncGPUReadback.Request(tablesBuffer);
                    activeGravityRequest = true;
                }
                voxelCollisionController.UpdateCollisions();
            }
        }

        if (activeEditRequest)
        {
            if (editRequest.done)
            {
                activeEditRequest = false;
                updateVoxelPhysics = true;

                voxelCollisionController.UpdateCollisions();
            }
        }
        if (activeSpreadRequest)
        {
            if (spreadRequest.done)
            {
                if (!spreadRequest.hasError)
                {
                    NativeArray<float> args = spreadRequest.GetData<float>();
                    updateVoxelPhysics = (int)args[84] > 0;
                }
                else
                {
                    updateVoxelPhysics = true;
                }
                activeSpreadRequest = false;
            }
        }
        if (activeGravityRequest)
        {
            if (gravityRequest.done)
            {
                if (!gravityRequest.hasError)
                {
                    NativeArray<float> args = gravityRequest.GetData<float>();
                    if (updateVoxelPhysics == false)
                    {
                        updateVoxelPhysics = (int)args[84] > 0;
                    }
                }
                else
                {
                    updateVoxelPhysics = true;
                }
                activeGravityRequest = false;
            }
        }

        if (updateVoxelPhysics && meshUpdateProgress == 0)
        {
            UpdateDensity(updateAll);
            GL.Flush();
            meshUpdateProgress = 1;
        }
        else if (meshUpdateProgress == 1)
        {
            UpdateOffsets(updateAll);
            GL.Flush();
            meshUpdateProgress = 2;
        }
        else if (meshUpdateProgress == 2)
        {
            UpdateVerts(updateAll);
            UpdateTris();
            GL.Flush();
            meshUpdateProgress = 0;
            updateAll = 0;
            if (!gravityOn)
            {
                updateVoxelPhysics = false;
            }
        }
    }

    //Dispatch calls for the compute shaders
    private void UpdateEdit(Vector4 coord)
    {
        editCompute.SetInt("resolution", RESOLUTION);
        editCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        editCompute.SetVector("coord", coord);
        editCompute.SetInt("radius", radius);
        editCompute.SetInt("diameter", radius * 2 + 1);
        editCompute.SetInt("sqrRadius", radius * radius);
        editCompute.SetInt("fillType", fillType);
        editCompute.SetBuffer(0, "tables", tablesBuffer);
        editCompute.SetBuffer(0, "voxels", voxelBuffer);
        editCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
    private void UpdateSpread()
    {
        spreadCompute.SetInt("resolution", RESOLUTION);
        spreadCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        spreadCompute.SetInt("xZGradient", xZGradient);
        spreadCompute.SetInt("yGradient", yGradient + 1);
        spreadCompute.SetBuffer(0, "tables", tablesBuffer);
        spreadCompute.SetBuffer(0, "voxels", voxelBuffer);
        spreadCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
    private void UpdateGravity()
    {
        gravityCompute.SetInt("resolution", RESOLUTION);
        gravityCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        gravityCompute.SetInt("fallRate", Mathf.RoundToInt(fallRate * Time.deltaTime * 72));
        gravityCompute.SetBuffer(0, "tables", tablesBuffer);
        gravityCompute.SetBuffer(0, "voxels", voxelBuffer);
        gravityCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
    private void UpdateDensity(int updateAll)
    {
        densityCompute.SetInt("resolution", RESOLUTION);
        densityCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        densityCompute.SetInt("weightingRadius", weightingRadius);
        densityCompute.SetInt("diameter", weightingRadius * 2 + 1);
        densityCompute.SetInt("sqrWeightingRadius", weightingRadius * weightingRadius);
        densityCompute.SetInt("updateAll", updateAll);
        densityCompute.SetBuffer(0, "tables", tablesBuffer);
        densityCompute.SetBuffer(0, "voxels", voxelBuffer);
        densityCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
    private void UpdateOffsets(int updateAll)
    {
        offsetCompute.SetInt("resolution", RESOLUTION);
        offsetCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        offsetCompute.SetInt("updateAll", updateAll);
        offsetCompute.SetFloat("correctionFactor", 1.0f / (weightingRadius * 2.0f + 1.0f));
        offsetCompute.SetFloat("voxelSize", voxelSize);
        offsetCompute.SetFloat("halfSize", halfSize);
        offsetCompute.SetFloat("angularCompensation", angularCompensation);
        offsetCompute.SetBuffer(0, "tables", tablesBuffer);
        offsetCompute.SetBuffer(0, "voxels", voxelBuffer);
        offsetCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
    private void UpdateVerts(int updateAll)
    {
        vertBuffer.SetCounterValue(0);
        meshCompute.SetInt("resolution", RESOLUTION);
        meshCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        meshCompute.SetInt("updateAll", updateAll);
        meshCompute.SetBuffer(0, "tables", tablesBuffer);
        meshCompute.SetBuffer(0, "voxels", voxelBuffer);
        meshCompute.SetBuffer(0, "verts", vertBuffer);
        meshCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
    private void UpdateTris()
    {
        triBuffer.SetCounterValue(0);
        meshStripCompute.SetInt("resolution", RESOLUTION);
        meshStripCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        meshStripCompute.SetBuffer(0, "verts", vertBuffer);
        meshStripCompute.SetBuffer(0, "tris", triBuffer);
        meshStripCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }

    //Update everything in editor
    private void OnValidate()
    {
        //Calculate sizes and position our object so it is centered.
        halfSize = size * 0.5f;

        //Calculate size of an individual voxel
        voxelSize = size / RESOLUTION;

        if (Application.isPlaying)
        {
            voxelCollisionController.voxelSize = voxelSize;

            updateVoxelPhysics = true;
            updateAll = 1;
        }
    }

    //Render geometry
    void OnRenderObject()
    {
        soilMat.SetBuffer("vertBuffer", vertBuffer);
        soilMat.SetBuffer("triBuffer", triBuffer);
        soilMat.SetPass(0);

        //Copy vert count to the args buffer so the correct amount of verts are rendered
        ComputeBuffer.CopyCount(vertBuffer, drawArgsBuffer, 0);

        Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, drawArgsBuffer, 0);
    }

    //Render shadow if enabled
    void LateUpdate()
    {
        if (castShadows)
        {
            shadowMat.SetBuffer("vertBuffer", vertBuffer);
            shadowMat.SetBuffer("triBuffer", triBuffer);

            //Copy vert count to the args buffer so the correct amount of verts are rendered
            ComputeBuffer.CopyCount(vertBuffer, drawArgsBuffer, 0);

            Graphics.DrawProceduralIndirect(shadowMat, bounds, MeshTopology.Triangles, drawArgsBuffer, 0, null, null, ShadowCastingMode.On, true, 0);
        }
    }

    void OnDestroy()
    {
        vertBuffer.Release();
        triBuffer.Release();
        voxelBuffer.Release();
        tablesBuffer.Release();
        drawArgsBuffer.Release();
    }
}
