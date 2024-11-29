using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuoyantObject : MonoBehaviour
{
    public WaterBody waterBody;
    private Rigidbody rb;

    void Start() {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate() {
        float waterHeight = waterBody.GetWaterHeight(transform.position);
        float heightSubmerged = Mathf.Max(0, waterHeight - transform.position.y);

        if (heightSubmerged > 0) {
            float fullVolume = transform.localScale.x * transform.localScale.y * transform.localScale.z;
            float submergedVolume = Mathf.Clamp(heightSubmerged / transform.localScale.y, 0f, 1f) * fullVolume;
            float buoyantForce = waterBody.density * submergedVolume;

            rb.AddForce(new Vector3(0, Mathf.Abs(Physics.gravity.y) * buoyantForce, 0), ForceMode.Acceleration);
            rb.AddForce(-rb.velocity * waterBody.drag * Time.fixedDeltaTime, ForceMode.VelocityChange);
            rb.AddTorque(-rb.angularVelocity * waterBody.angularDrag * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }

        rb.AddForce(Physics.gravity, ForceMode.Acceleration);
    }
} 