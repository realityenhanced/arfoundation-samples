using UnityEngine;

/// <summary>
/// Orients the object to always face the camera.
/// </summary>
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        transform.LookAt(transform.position + Camera.current.transform.rotation * Vector3.forward,
            Camera.current.transform.rotation * Vector3.up);
    }
}