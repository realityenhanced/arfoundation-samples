using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Manages the UX to be shown when AR tracking is lost or gained.
/// </summary>
public class SessionManager : MonoBehaviour
{
    // Settings
    [Tooltip("The animation used to notify the user to move the device")]
    [SerializeField]
    private Animator m_moveDeviceAnimation;

    [Tooltip("The animation used to notify the user to start measuring")]
    [SerializeField]
    private Animator m_tapToPlaceAnimation;

    [Tooltip("The MeasurementController that will be enabled/disabled on AR tracking gain/loss.")]
    [SerializeField]
    private MeasurementController m_measurementController;

    // Constants
    const string c_fadeOffAnim = "FadeOff";
    const string c_fadeOnAnim = "FadeOn";

    // Privates
    // Flags to track the state of device tracking loss/gain UX.
    private bool m_isMoveDeviceUXActive = true;
    private bool m_isTapToPlaceUXActive = false;

    private void Start()
    {
        if (m_moveDeviceAnimation == null || m_tapToPlaceAnimation == null 
            || m_measurementController == null)
        {
            throw new System.InvalidOperationException("All script inputs need to be passed in to SessionManager");
        }
    }

    private void OnEnable()
    {
        ARSubsystemManager.systemStateChanged += ARSystemStateChanged;

        // [ARFoundation Doc Improvement] Documentation for ARSystemState
        // is not clear whether this is needed. Picked this up from
        // a forum post.
        // https://forum.unity.com/threads/checking-for-ar-availability-before-launching-ar-scene.579130/
        ARSubsystemManager.CreateSubsystems();
        StartCoroutine(ARSubsystemManager.CheckAvailability());
    }

    private void OnDisable()
    {
        ARSubsystemManager.systemStateChanged -= ARSystemStateChanged;
    }

    private void ARSystemStateChanged(ARSystemStateChangedEventArgs obj)
    {
        if (gameObject.activeSelf)
        {
            HandleARState(obj.state);
        }
    }

    void Update()
    {
        if (m_isTapToPlaceUXActive 
            && Input.touchCount > 0 
            && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            // On the first tap after session tracking is lost, remove the 
            // "Tap to place" UX.
            RemoveTapToPlaceUX();
        }
    }

    // Helpers

    // Helper to handle the current AR state & update UX.
    private void HandleARState(ARSystemState arState)
    {
        switch (arState)
        {
            case ARSystemState.SessionTracking:
                // Tracking has been started. Update the UX & enable measurements.
                RemoveMoveDeviceUX();
                StartTapToPlaceUX();
                m_measurementController.enabled = true;
                break;
            case ARSystemState.Unsupported:
            // Notify user that AR is not supported.
            //break;
            case ARSystemState.NeedsInstall:
            // Notify user that an install is needed for AR support.
            //break;
            default:
                // The AR Session has not started or lost tracking.
                // Update the UX & Disable measurements.
                StartMoveDeviceUX();
                RemoveTapToPlaceUX();
                m_measurementController.enabled = false;
                break;
        }
    }

    private void StartTapToPlaceUX()
    {
        if (!m_isTapToPlaceUXActive && m_tapToPlaceAnimation != null)
        {
            m_tapToPlaceAnimation.SetTrigger(c_fadeOnAnim);
            m_isTapToPlaceUXActive = true;
        }
    }

    private void RemoveTapToPlaceUX()
    {
        if (m_isTapToPlaceUXActive && m_tapToPlaceAnimation != null)
        {
            m_tapToPlaceAnimation.SetTrigger(c_fadeOffAnim);
            m_isTapToPlaceUXActive = false;
        }
    }

    private void StartMoveDeviceUX()
    {
        if (!m_isMoveDeviceUXActive && m_moveDeviceAnimation != null)
        {
            m_moveDeviceAnimation.SetTrigger(c_fadeOnAnim);
            m_isMoveDeviceUXActive = true;
        }
    }

    private void RemoveMoveDeviceUX()
    {
        if (m_isMoveDeviceUXActive && m_moveDeviceAnimation != null)
        {
            m_moveDeviceAnimation.SetTrigger(c_fadeOffAnim);
            m_isMoveDeviceUXActive = false;
        }
    }
}
