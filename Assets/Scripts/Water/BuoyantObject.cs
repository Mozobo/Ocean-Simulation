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

    // Very basic buoyancy system.
    // Calculates how much volume is submerged (only taking into account the difference in the y-axis) and applies forces accordingly.
    void FixedUpdate() {
        float waterHeight = waterBody.GetWaterHeight(transform.position);
        float heightSubmerged = Mathf.Max(0, waterHeight - transform.position.y);

        if (heightSubmerged > 0) {
            // The volume of the object is approximated as localScale.x * localScale.y * localScale.z
            // This simplification obviously does not accurately represent objects with non-rectangular shapes, but it provides a fast approximation for the calculations.
            float fullVolume = transform.localScale.x * transform.localScale.y * transform.localScale.z;
            float submergedVolume = Mathf.Clamp(heightSubmerged / transform.localScale.y, 0f, 1f) * fullVolume;
            float buoyantForce = waterBody.density * submergedVolume;

            // Apply the buoyant force upwards to simulate buoyancy.
            rb.AddForce(new Vector3(0, Mathf.Abs(Physics.gravity.y) * buoyantForce, 0), ForceMode.Acceleration);
            // Apply a damping force to reduce velocity, simulating drag in water.
            rb.AddForce(-rb.velocity * waterBody.drag * Time.fixedDeltaTime, ForceMode.VelocityChange);
            // Apply a damping torque to reduce angular velocity, simulating angular drag in water.
            rb.AddTorque(-rb.angularVelocity * waterBody.angularDrag * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }

        rb.AddForce(Physics.gravity, ForceMode.Acceleration);
    }
} 