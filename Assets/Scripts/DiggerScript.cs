using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiggerScript : MonoBehaviour
{
    public GameObject Excavator;
    public HingeJoint pivot;
    public HingeJoint bucketHinge;
    public HingeJoint firstHinge;
    public HingeJoint secondHinge;
    public GameObject controlReference;
    public RightController right;
    public LeftController left;
    public bool grounded;
    JointSpring bucketSpring, firstSpring, secondSpring, pivotSpring;
    private Rigidbody Excavator_rb;
    public float speed = 0.5f;

    // Start is called before the first frame update
    void Start()
    {
        bucketSpring = bucketHinge.spring;
        firstSpring = firstHinge.spring;
        secondSpring = secondHinge.spring;
        pivotSpring = pivot.spring;
        Excavator_rb = Excavator.GetComponent<Rigidbody>();

    }

    // Update is called once per frame
    void Update()
    {
        if (right.triggerDown)
        {
            if (right.joystickFwd)
            {
                if (bucketSpring.targetPosition < 40)
                {
                    bucketSpring.targetPosition += speed;
                    bucketHinge.spring = bucketSpring;
                }
            }
            if (right.joystickDown)
            {
                if (bucketSpring.targetPosition > -80)
                {
                    bucketSpring.targetPosition -= speed;
                    bucketHinge.spring = bucketSpring;
                }
            }

        }
        if (right.gripDown)
        {
            if (right.joystickFwd)
            {
                if (secondSpring.targetPosition < 100)
                {
                    secondSpring.targetPosition += speed;
                    secondHinge.spring = secondSpring;
                }
            }
            if (right.joystickDown)
            {
                if (secondSpring.targetPosition > -20)
                {
                    secondSpring.targetPosition -= speed;
                    secondHinge.spring = secondSpring;
                }
            }
         
        }
        else
        {
            if (!right.triggerDown && !right.gripDown)
            {

                if (right.joystickFwd)
                {
                    if (firstSpring.targetPosition > -30)
                    {
                        firstSpring.targetPosition -= speed;
                        firstHinge.spring = firstSpring;
                    }
                }
                if (right.joystickDown)
                {
                    if (firstSpring.targetPosition < 30)
                    {
                        firstSpring.targetPosition += speed;
                        firstHinge.spring = firstSpring;
                    }
                }
                if (right.joystickLeft)
                {
                    if (pivotSpring.targetPosition > -80)
                    {
                        pivotSpring.targetPosition -= speed;
                        pivot.spring = pivotSpring;
                    }
                }
                if (right.joystickRight)
                {
                    if (pivotSpring.targetPosition < 80)
                    {
                        pivotSpring.targetPosition += speed;
                        pivot.spring = pivotSpring;
                    }
                }
            }
        }
    }
}
