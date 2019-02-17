using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Experimental.XR;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the UX for measuring real world objects.
/// </summary>
public class MeasurementController : MonoBehaviour
{
    // Settings
    [Tooltip("The ARSessionOrigin to use for Raycasting")]
    [SerializeField]
    private ARSessionOrigin m_arSessionOrigin;

    [Tooltip("Scene object to be used as the cursor")]
    [SerializeField]
    private GameObject m_cursor;

    [Tooltip("The prefab to be used as the endpoint of a measurement")]
    [SerializeField]
    private GameObject m_endpointPrefab;

    [Tooltip("The material to be used for rendering measurement lines")]
    [SerializeField]
    private Material m_lineMaterial;

    [Tooltip("The prefab to be used to display the measured length as text")]
    [SerializeField]
    private GameObject m_lengthMeasureText;

    // Private members

    // Dynamic array to store ray cast hits.
    private const int c_maxRayCastHits = 16; 
    private static List<ARRaycastHit> s_rayCastHits = new List<ARRaycastHit>(c_maxRayCastHits);

    // A flag to track if the user is drawing a line between two points.
    private bool m_isMeasurementInProgress = false;

    // The endpoint being used to measure the length from.
    private GameObject m_currentEndpoint;

    // The line renderer that is used to draw a line from the starting endpoint
    // to the cursor when a measurement is in progress.
    // [PERF IMPROVEMENT] Use XRLineRenderer. (https://github.com/Unity-Technologies/XRLineRenderer)
    private LineRenderer m_currentLineRenderer;

    // The measurement text object that is currently being rendered to.
    private GameObject m_currentMeasurementText;

    // Text mesh component being used to render the current length.
    private TextMeshPro m_currentTextField;

    void Start()
    {
        // TODO: Validate settings
    }

    void Update()
    {
        if (Camera.current == null)
        {
            return;
        }

        // TODO: Add init game object to scene to display "Move around", and remove it
        // when AR is available.

        // Shoot a ray from the center of the screen and retrieve the hit points.
        var screenCenter = Camera.current.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));
        if (m_arSessionOrigin.Raycast(screenCenter, s_rayCastHits, TrackableType.All)
            && s_rayCastHits.Count > 0)
        {
            // Place the cursor at the first hit point.
            EnableCursor();
            m_cursor.transform.SetPositionAndRotation(s_rayCastHits[0].pose.position, Quaternion.identity);
        }
        else
        {
            // Unable to find any hits in the world, disable the cursor.
            DisableCursor();
        }
        s_rayCastHits.Clear();

        // On a tap, place an endpoint object at the cursor.
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            // TODO: How to use Anchors to better stabilize the arrows?
            var endpoint = Instantiate(m_endpointPrefab, m_cursor.transform.position, Quaternion.identity);

            if (!m_isMeasurementInProgress)
            {
                // The user has started a measurement operation.
                // Add a line renderer & text renderer, that tracks the visualization of the measurement,
                // to the starting endpoint.
                AddLineRendererToGameObject(endpoint);
                AddMeasurementTextToGameObject(endpoint);
                m_currentEndpoint = endpoint;

                StartMeasurementMode();
            }
            else
            {
                UpdateMeasurementText(false /*Place text at center of measurement line*/);
                StopMeasurementMode();
            }
        }

        if (m_isMeasurementInProgress)
        {
            // If the user is in the middle of measuring, 
            // update the line and length text.
            m_currentLineRenderer.SetPosition(1, m_cursor.transform.position);
            UpdateMeasurementText(true /*Place text near cursor*/);
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            // If a measurement is in progress, cleanup on focus loss.
            CancelCurrentMeasurement();
        }
    }

    private void OnDisable()
    {
        // If a measurement is in progress, cleanup.
        CancelCurrentMeasurement();
        DisableCursor();
    }

    private void OnEnable()
    {
        EnableCursor();
    }

    // HELPERS
    void EnableCursor()
    {
        SetCursorColor(Color.green);
    }

    void DisableCursor()
    {
        SetCursorColor(Color.red);
    }

    void SetCursorColor(Color c)
    {
        var cursorCenter = m_cursor.transform.GetChild(0);
        cursorCenter.GetComponent<Renderer>().material.color = c;
    }

    void StartMeasurementMode()
    {
        m_isMeasurementInProgress = true;
    }

    void StopMeasurementMode()
    {
        m_isMeasurementInProgress = false;
        m_currentTextField = null;
        m_currentLineRenderer = null;
        m_currentMeasurementText = null;
        m_currentEndpoint = null;
    }

    void CancelCurrentMeasurement()
    {
        if (m_isMeasurementInProgress)
        {
            // If a measurement is in progress, cleanup the endpoint object.
            Destroy(m_currentEndpoint);

            StopMeasurementMode();
        }
    }

    void AddLineRendererToGameObject(GameObject go)
    {
        m_currentLineRenderer = go.AddComponent<LineRenderer>();
        m_currentLineRenderer.material = m_lineMaterial;
        m_currentLineRenderer.widthMultiplier = 0.0025f;

        // A measurement operation will always be between 2 endpoints.
        m_currentLineRenderer.positionCount = 2;

        // Use the game object's world space position as the start & end of the
        // line.
        m_currentLineRenderer.SetPosition(0, go.transform.position);
        m_currentLineRenderer.SetPosition(1, go.transform.position);
    }

    void AddMeasurementTextToGameObject(GameObject go)
    {
        var cameraForward = Camera.current.transform.forward;
        var rotationToFaceCamera = Quaternion.LookRotation(cameraForward);
        m_currentMeasurementText = Instantiate(m_lengthMeasureText, go.transform.position, rotationToFaceCamera);
        m_currentMeasurementText.transform.SetParent(go.transform);

        m_currentTextField = m_currentMeasurementText.GetComponentInChildren<TextMeshPro>();
    }

    // Returns the length to be displayed based on the distance between the cursor
    // and the endpoint object that is currently set.
    string GetCurrentDistance()
    {
        float distance = Vector3.Distance(m_cursor.transform.position, m_currentEndpoint.transform.position);
        if (distance < 1.0f)
        {
            // Use centimeters, if distance is less than a meter.
            distance *= 100;
            return distance.ToString("0.##") + "cm";
        }
        return distance.ToString("0.##") + "m";
    }

    // Updates the value & placement of the text object.
    void UpdateMeasurementText(bool placeNearCursor)
    {
        m_currentTextField.SetText(GetCurrentDistance());

        // Place the text on the cursor by default.
        Vector3 textPos = m_cursor.transform.position;
        Quaternion textRot;

        if (placeNearCursor)
        {
            // Place the length text at a fixed distance in front of the camera.
            textPos = Camera.current.transform.position + Camera.current.transform.forward * 0.9f;

            // Face the camera.
            var cameraForward = Camera.current.transform.forward;
            textRot = Quaternion.LookRotation(cameraForward);
        }
        else
        {
            // Set the text at the center of the line.
            textPos = (textPos + m_currentEndpoint.transform.position) / 2;

            // Rotate the text's x-axis to be parallel to the measurement line.
            var target = m_currentEndpoint.transform.position - m_cursor.transform.position;
            textRot = Quaternion.FromToRotation(-Vector3.right, target);
        }

        m_currentMeasurementText.transform.SetPositionAndRotation(textPos, textRot);
    }
}