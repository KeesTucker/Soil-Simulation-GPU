using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LeftController : MonoBehaviour
{
    public InputActionReference triggerReference = null;
    private float triggerPressed = 0.0f;
    public bool triggerDown;

    public InputActionReference gripReference = null;
    private float gripPressed = 0.0f;
    public bool gripDown;

    public InputActionReference joystickReference = null;
    public Vector2 joystickVector;
    public bool joystickFwd;
    public bool joystickDown;
    public bool joystickLeft;
    public bool joystickRight;


    // Update is called once per frame
    private void Update()
    {
        triggerPressed = triggerReference.action.ReadValue<float>();
        if (triggerPressed > 0.1)
        {
            triggerDown = true;
        }
        else
        {
            triggerDown = false;
        }
        gripPressed = gripReference.action.ReadValue<float>();
        if (gripPressed > 0.1)
        {
            gripDown = true;
        }
        else
        {
            gripDown = false;
        }
        joystickVector = joystickReference.action.ReadValue<Vector2>();
        if (joystickVector.x > 0.4)
        {
            joystickRight = true;
        }
        else if (joystickVector.x < 0.4)
        {
            joystickRight = false;
        }
        joystickVector = joystickReference.action.ReadValue<Vector2>();
        if (joystickVector.x < -0.4)
        {
            joystickLeft = true;
        }
        else if (joystickVector.x > -0.4)
        {
            joystickLeft = false;
        }
        joystickVector = joystickReference.action.ReadValue<Vector2>();
        if (joystickVector.y > 0.4)
        {
            joystickFwd = true;
        }
        else if (joystickVector.y < 0.4)
        {
            joystickFwd = false;
        }
        joystickVector = joystickReference.action.ReadValue<Vector2>();
        if (joystickVector.y < -0.4)
        {
            joystickDown = true;
        }
        else if (joystickVector.y > -0.4)
        {
            joystickDown = false;
        }
    }
}
