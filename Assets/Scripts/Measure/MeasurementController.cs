using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Experimental.XR;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the UX for measuring real world objects.
/// </summary>
/// <remarks>
/// Cursor Management:
/// A ray is cast into the real world from the center of the phone's screen
/// to detect a hit point.
/// If no hit points are detected, the cursor is colored red
/// to denote an invalid cursor position.
/// If a hit point is detected, the cursor is turned green.
/// 
/// Measuring real world objects:
/// - When a tap is detected, a cube is placed at the cursor's location and
///   a line is rendered from the cube to the current cursor position.
/// - The length is displayed based on the distance between the cursor and the endpoint.
/// - On the second tap, another endpoint is placed and the length is displayed at the center of
///   the measured line.
/// 
/// AR Tracking Loss/Gain:
/// - The SessionManager is responsible for detecting the AR system state
///   displaying relevant UX and enabling/disabing the MeasurementController component.
/// 
/// App Focus Management:
/// - If a measurement is in progress, the measurement operation is reset on focus loss.
/// </remarks>
public class MeasurementController : MonoBehaviour
{
    // Settings
    [Tooltip("The ARSessionOrigin to use for Raycasting")]
    [SerializeField]
    private ARSessionOrigin m_arSessionOrigin;

    [Tooltip("Scene object to be used as the cursor")]
    [SerializeField]
    private GameObject m_cursor;

    [Tooltip("The prefab to be used as the endpoints of a measurement")]
    [SerializeField]
    private GameObject m_endpointPrefab;

    [Tooltip("The material to be used for rendering measurement lines")]
    [SerializeField]
    private Material m_lineMaterial;

    [Tooltip("The prefab to be used to display the measured length")]
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
        if (m_arSessionOrigin == null || m_cursor == null || 
            m_lineMaterial == null || m_endpointPrefab == null || m_lengthMeasureText == null)
        {
            throw new System.InvalidOperationException("All script inputs need to be passed in to MeasurementController");
        }
    }

    void Update()
    {
        if (Camera.current == null)
        {
            return;
        }

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
            var endpoint = Instantiate(m_endpointPrefab, m_cursor.transform.position, Quaternion.identity);

            if (!m_isMeasurementInProgress)
            {
                // The user has started a measurement operation.
                // Add a line renderer & text renderer to the starting endpoint, 
                // that tracks the visualization of the measurement.
                AddLineRendererToGameObject(endpoint);
                AddMeasurementTextToGameObject(endpoint);
                m_currentEndpoint = endpoint;

                StartMeasurementMode();
            }
            else
            {
                // Place text at center of measurement line.
                UpdateMeasurementText(false);
                StopMeasurementMode();
            }
        }

        if (m_isMeasurementInProgress)
        {
            // If the user is in the middle of measuring, 
            // update the line and length text.
            m_currentLineRenderer.SetPosition(1, m_cursor.transform.position);

            // Place text near cursor.
            UpdateMeasurementText(true);
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
    private void EnableCursor()
    {
        SetCursorColor(Color.green);
    }

    private void DisableCursor()
    {
        SetCursorColor(Color.red);
    }

    private void SetCursorColor(Color c)
    {
        if (m_cursor != null)
        {
            var cursorCenter = m_cursor.transform.GetChild(0);
            cursorCenter.GetComponent<Renderer>().material.color = c;
        }
    }

    private void StartMeasurementMode()
    {
        m_isMeasurementInProgress = true;
    }

    private void StopMeasurementMode()
    {
        m_isMeasurementInProgress = false;
        m_currentTextField = null;
        m_currentLineRenderer = null;
        m_currentMeasurementText = null;
        m_currentEndpoint = null;
    }

    private void CancelCurrentMeasurement()
    {
        if (m_isMeasurementInProgress)
        {
            // If a measurement is in progress, cleanup the endpoint object.
            Destroy(m_currentEndpoint);

            StopMeasurementMode();
        }
    }

    private void AddLineRendererToGameObject(GameObject go)
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

    private void AddMeasurementTextToGameObject(GameObject go)
    {
        m_currentMeasurementText = Instantiate(m_lengthMeasureText, go.transform.position, Quaternion.identity);
        m_currentMeasurementText.transform.SetParent(go.transform);

        m_currentTextField = m_currentMeasurementText.GetComponentInChildren<TextMeshPro>();
    }

    // Returns the length to be displayed based on the distance between the cursor
    // and the endpoint object that is currently set.
    private string GetCurrentDistance()
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
    private void UpdateMeasurementText(bool placeNearCursor)
    {
        m_currentTextField.SetText(GetCurrentDistance());

        // The text will always face the camera due to the Billboard component attached to the prefab.
        Vector3 textPos;
        if (placeNearCursor)
        {
            // Place the length text at a fixed distance in front of the camera.
            textPos = Camera.current.transform.position + Camera.current.transform.forward * 0.9f;
        }
        else
        {
            // Set the text at the center of the line.
            textPos = (m_cursor.transform.position + m_currentEndpoint.transform.position) / 2;
        }
        m_currentMeasurementText.transform.SetPositionAndRotation(textPos, Quaternion.identity);
    }
}