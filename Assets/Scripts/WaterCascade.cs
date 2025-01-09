using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterCascade : MonoBehaviour
{
    // Reference wavelength value for this cascade's waves.
    // It establishes the general scale of the waves.
    // https://en.wikipedia.org/wiki/Wavelength
    public float wavelength = 10.0f;

    // The upper limit of the angular wavenumber (k) for waves considered in the initial spectrum computation for this cascade.
    // A higher value allows for the inclusion of higher-frequency (shorter wavelength) waves.
    // https://en.wikipedia.org/wiki/Wavenumber
    public float cutoffHigh = 5.0f;

    // The lower limit of the angular wavenumber (k) for waves considered in the initial spectrum computation for this cascade.
    // A lower value allows for the inclusion of lower-frequency (longer wavelength) waves.
    // https://en.wikipedia.org/wiki/Wavenumber
    public float cutoffLow = 0.0001f;
}
