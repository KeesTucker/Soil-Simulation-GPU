using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BucketSpawner : MonoBehaviour
{
    [SerializeField]
    private VoxelComputeController voxelComputeController;
    public float numHeld;
    [SerializeField] 
    private float angleOfRelease = 45;
    [SerializeField] 
    private int speedOfRelease = 1;
    [SerializeField]
    private float pickupNumOffset = 1f;
    private bool holding = true;
    public Vector3 offset;
    public Vector3 up;

    private void Start()
    {
        up = transform.up;
    }

    void Update()
    {
        if (Vector3.Angle(up, transform.up) > angleOfRelease)
        {
            if (numHeld > 0)
            {
                for (int i = 0; i < speedOfRelease; i++)
                {
                    numHeld -= pickupNumOffset;
                    voxelComputeController.fillType = 1;
                    voxelComputeController.EditVoxels(transform.position);
                }
            }
            holding = false;
        }
        else
        {
            holding = true;
        }
    }

    public void IncreaseNumHeld()
    {
        if (holding)
        {
            numHeld++;
        } 
    }
}
