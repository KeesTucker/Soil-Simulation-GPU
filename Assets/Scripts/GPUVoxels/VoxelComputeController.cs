using UnityEngine;
using System.Collections;

public class VoxelComputeController : MonoBehaviour
{
    public VoxelManipulation voxelManipulation;

    //Resolution of grid (multiple of 8)
    const int RESOLUTION = 8 * 4;
    //Size of voxel buffer and density buffer
    const int NUM_VOXELS = RESOLUTION * RESOLUTION * RESOLUTION;
    //Size of the mesh buffer, square of resolution * number of verts per quad (6) * number of quads per voxel (6))
    const int NUM_VERTS_IN_MESH = RESOLUTION * RESOLUTION * RESOLUTION * 6 * 6;
    //Float count per weighted point position
    const int NUM_FLOATS_PER_POS_POINT = 4;
    //Float count for a vert struct
    const int NUM_FLOATS_PER_VERT = 7;

    public Material drawBufferMat;

    //Calculates edits
    public ComputeShader editCompute;
    //Calculates gravity
    public ComputeShader gravityCompute;
    //Calculates Spreading
    public ComputeShader spreadCompute;
    //Calculates density
    public ComputeShader densityCompute;
    //Calculates weighted point offsets
    public ComputeShader offsetCompute;
    //Calculates mesh
    public ComputeShader meshCompute;
    public ComputeShader meshStripCompute;

    struct Voxel
    {
        public int update;
        public int solid;
        public float density;
        public Vector4 position;
        public int indicy;
    }

    //Holds status for each voxel
    private ComputeBuffer voxelBuffer;
    //Holds mesh data
    private ComputeBuffer vertBuffer;
    private ComputeBuffer unorderedVertBuffer;
    //Args to pass to DrawIndirect()
    private ComputeBuffer drawArgsBuffer;
    //Args to pass to compute shaders
    [SerializeField]
    public bool editTablesRealtime = true;
    public float[] tables;
    private ComputeBuffer tablesBuffer;

    //Unity size of entire grid
    public float size = 2;
    //Half the size, used for centering
    private float halfSize;
    //Size of an individual voxel
    private float voxelSize;

    //Edit settings
    public int radius = 3;
    public int fillType;

    //To what extent should we round corners?
    public float angularCompensation = 1f;
    //The radius of voxels we check for volumetric density
    public int weightingRadius = 3;
    //Should we calculate gravity
    public bool gravityOn = false;
    //The rate at which voxels fall, arbitrary units atm
    public int fallRate = 2;

    public int xZGradient = 1;
    public int yGradient = 1;

    public int groundHeight = 20;

    Vector3 edit = -Vector3.one;

    private void Start()
    {
        //Calculate sizes and position our object so it is centered.
        halfSize = size * 0.5f;

        //Calculate size of an individual voxel
        voxelSize = size / RESOLUTION;

        //Unsure if we actually need to do this now, will come back to this and potentially remove
        //MARKED FOR REMOVAL
        transform.localPosition = new Vector3(-halfSize, -halfSize, -halfSize);

        //There are 8 threads run per group so resolution must be divisible by 8
        if (RESOLUTION % 8 != 0)
            throw new System.ArgumentException("RESOLUTION must be divisible be 8");

        //Create a buffer of voxels
        voxelBuffer = new ComputeBuffer(NUM_VOXELS, 
            /*update now? is solid?*/ sizeof(int) * 2 + 
            /*density*/ sizeof(float) + 
            /*position of point*/ sizeof(float) * NUM_FLOATS_PER_POS_POINT + 
            /*position of this vert in vert array*/ sizeof(int));

        //Create a buffer of verts
        vertBuffer = new ComputeBuffer(NUM_VERTS_IN_MESH, sizeof(float) * NUM_FLOATS_PER_VERT, ComputeBufferType.Counter);
        unorderedVertBuffer = new ComputeBuffer(NUM_VERTS_IN_MESH, sizeof(float) * NUM_FLOATS_PER_VERT, ComputeBufferType.Append);

        //Create buffers for tables
        tablesBuffer = new ComputeBuffer(8 + 24 + 25 + 27, sizeof(float));

        //Buffer for DrawIndirect()
        drawArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        //INIT ALL REQUIRED BUFFERS

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
                    if (y > groundHeight)
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

        tablesBuffer.SetData(LookupTables.GetTables(RESOLUTION));

        drawArgsBuffer.SetData(new int[] { NUM_VERTS_IN_MESH, 1, 0, 0 });

        voxelManipulation.voxelBuffer = voxelBuffer;
        voxelManipulation.resolution = RESOLUTION;
        voxelManipulation.voxelSize = voxelSize;
        voxelManipulation.Setup();

        UpdateEverything(1);
    }

    private void OnValidate()
    {
        //Calculate sizes and position our object so it is centered.
        halfSize = size * 0.5f;

        //Calculate size of an individual voxel
        voxelSize = size / RESOLUTION;

        if (Application.isPlaying)
        {
            voxelManipulation.voxelSize = voxelSize;

            if (tablesBuffer != null)
            {
                if (editTablesRealtime)
                {
                    if (tables.Length == 0)
                    {
                        tables = LookupTables.GetTables(RESOLUTION);
                    }
                    tablesBuffer.SetData(tables);
                }
                else
                {
                    tablesBuffer.SetData(LookupTables.GetTables(RESOLUTION));
                }
            }
            
            UpdateEverything(1);
        }
    }

    void OnRenderObject()
    {
        //drawBufferMat.SetBuffer("vertBuffer", unorderedVertBuffer);
        drawBufferMat.SetBuffer("vertBuffer", vertBuffer);
        drawBufferMat.SetPass(0);

        //ComputeBuffer.CopyCount(vertBuffer, drawArgsBuffer, 0);

        //Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, drawArgsBuffer, 0);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, NUM_VERTS_IN_MESH);
    }

    void OnDestroy()
    {
        vertBuffer.Release();
        unorderedVertBuffer.Release();
        voxelBuffer.Release();
        tablesBuffer.Release();
        drawArgsBuffer.Release();
    }

    public void EditVoxels(RaycastHit hitInfo)
    {
        if (hitInfo.collider.gameObject.tag == "voxelCollider")
        {
            Vector3 point = transform.InverseTransformPoint(hitInfo.point);
            int centerX = (int)(point.x / voxelSize);
            int centerY = (int)(point.y / voxelSize);
            int centerZ = (int)(point.z / voxelSize);
            edit = new Vector3(centerX, centerY, centerZ);

            if (!gravityOn)
            {
                UpdateEverything(0);
            }
        }
    }

    private void Update()
    {
        if (gravityOn)
        {
            UpdateEverything(0);
        }
    }

    private void UpdateEverything(int updateAll)
    {
        if (voxelBuffer != null)
        {
            if (edit != -Vector3.one)
            {
                UpdateEdit(edit);
                GL.Flush();
                edit = -Vector3.one;
            }
            if (gravityOn)
            {
                UpdateSpread();
                GL.Flush();
                UpdateGravity();
                GL.Flush();

            }
            UpdateDensity(updateAll);
            GL.Flush();
            UpdateOffsets(updateAll);
            GL.Flush();
            UpdateMesh(updateAll);
            GL.Flush();
        }
    }

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

    private void UpdateMesh(int updateAll)
    {
        unorderedVertBuffer.SetCounterValue(0);
        vertBuffer.SetCounterValue(0);

        meshCompute.SetInt("resolution", RESOLUTION);
        meshCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        meshCompute.SetInt("updateAll", updateAll);
        meshCompute.SetBuffer(0, "tables", tablesBuffer);
        meshCompute.SetBuffer(0, "voxels", voxelBuffer);
        meshCompute.SetBuffer(0, "verts", vertBuffer);
        meshCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);

        meshStripCompute.SetInt("resolution", RESOLUTION);
        meshStripCompute.SetInt("resolution2", RESOLUTION * RESOLUTION);
        meshStripCompute.SetBuffer(0, "verts", vertBuffer);
        meshStripCompute.SetBuffer(0, "unorderedVerts", unorderedVertBuffer);
        meshStripCompute.Dispatch(0, RESOLUTION / 8, RESOLUTION / 8, RESOLUTION / 8);
    }
}
