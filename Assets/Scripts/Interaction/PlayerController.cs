using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public Rigidbody r;
    public float force = 5f;
    public float height = 1f;
    public float move = 0;

    void FixedUpdate()
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(transform.position, -transform.up, out hitInfo, height))
        {
            r.AddForce(transform.up * force * Time.deltaTime * 72f);
        }
        r.AddForce(transform.forward * force * move * Time.deltaTime * 72f);
    }

    public void Rotate(float amount)
    {
        transform.Rotate(transform.up, amount * Time.deltaTime * 72f);
    }
}
