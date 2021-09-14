using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BucketInteractor : MonoBehaviour
{
    public VoxelComputeController voxelComputeController;
    public BucketSpawner bucketSpawner;

    private void OnTriggerStay(Collider other)
    {
        voxelComputeController.fillType = 0;
        voxelComputeController.EditVoxels(bucketSpawner.offset + other.transform.position);
        bucketSpawner.IncreaseNumHeld();
    }
}
