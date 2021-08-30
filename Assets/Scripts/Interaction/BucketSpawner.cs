using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BucketSpawner : MonoBehaviour
{
    [SerializeField]
    private VoxelComputeController voxelComputeController;
    public int numHeld;
    [SerializeField] 
    private float angleOfRelease = 45;
    [SerializeField] 
    private int speedOfRelease = 1;
    private bool holding = true;

    void Update()
    {
        if (Vector3.Angle(Vector3.up, transform.up) > angleOfRelease)
        {
            if (numHeld > 0)
            {
                for (int i = 0; i < speedOfRelease; i++)
                {
                    numHeld -= 4;
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
