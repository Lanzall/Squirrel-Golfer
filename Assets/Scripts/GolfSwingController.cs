using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GolfSwingController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody ball;

    [Header("Tuning")]
    public float powerMultiplier = 25f;
    public float maxPowerCap = 80f;
    public float straightnessThreshold = 12f;
    public float maxAimAngle = 25f;

    //NewInput System references
    private GolfControls controls;
    private InputAction swingAction;
    private InputAction positionAction;

    private List<Vector2> mousePositions = new List<Vector2>();
    private List<float> mouseTimes = new List<float>();
    private bool isDragging = false;

    private void Awake()
    {
        controls = new GolfControls();
        swingAction = controls.GolfSwing.Swing;
        positionAction = controls.GolfSwing.MousePosition;
    }

    private void OnEnable()
    {
        swingAction.Enable(); positionAction.Enable();

        swingAction.performed += OnSwingStarted;    //Pressed
        swingAction.canceled += OnSwingReleased;    //Released
    }

    private void OnDisable()
    {
        swingAction.Disable(); positionAction.Disable();
        swingAction.performed -= OnSwingStarted;
        swingAction.canceled -= OnSwingReleased;
    }

    private void OnSwingStarted(InputAction.CallbackContext context)
    {
        if (ball.linearVelocity.magnitude > 0.2f) return;   //If the ball is still moving

        isDragging = true;
        mousePositions.Clear();
        mouseTimes.Clear();

        Vector2 pos = positionAction.ReadValue<Vector2>();
        mousePositions.Add(pos);
        mouseTimes.Add(Time.time);
    }

    private void Update()
    {
        if (!isDragging) return;

        //Continuously return mouse position
        Vector2 pos = positionAction.ReadValue<Vector2>();
        mousePositions.Add(pos);
        mouseTimes.Add(Time.time);
    }

    private void OnSwingReleased(InputAction.CallbackContext context)
    {
        if (!isDragging) return;

        if (mousePositions.Count > 15)
        {
            ProcessSwing();
        }
    }

    private void ProcessSwing()
    {
        //Same logic as before: find lowest Y (end of backswing)
        int minYindex = 0;
        float minY = mousePositions[0].y;
        for (int i = 1; i < mousePositions.Count; i++)
        {
            if (mousePositions[i].y < minY)
            {
                minY = mousePositions[i].y;
                minYindex = i;
            }
        }

        if (minYindex >= mousePositions.Count - 1) return;

        //Forward Swing portion
        List<Vector2> forwardPath = new List<Vector2>();
        List<float> forwardTimes = new List<float>();
        for (int i = minYindex; i < mousePositions.Count; i++)
        {
            forwardPath.Add(mousePositions[i]);
            forwardTimes.Add(mouseTimes[i]);
        }

        //Power from max upward speed
        float maxSpeed = 0f;
        Vector2 prev = forwardPath[0];
        for (int i = 1; i < forwardPath.Count; i++)
        {
            float dist = Vector2.Distance(forwardPath[i], prev);
            float dt = forwardTimes[i] - forwardTimes[i] - forwardTimes[i - 1];
            if (dt > 0f)
                maxSpeed = Mathf.Max(maxSpeed, dist / dt);
            prev = forwardPath[i];
        }

        float power = Mathf.Min(maxSpeed * powerMultiplier, maxPowerCap);

        //Straightness and Direction
        Vector2 lineStart = forwardPath[0];
        Vector2 lineEnd = forwardPath[^1];
        Vector2 lineDir = (lineEnd - lineStart).normalized;
        float lineLength = Vector2.Distance(lineStart, lineEnd);

        float maxDeviation = 0f;
        float totalDeviation = 0f;
        foreach (Vector2 p in forwardPath)
        {
            Vector2 toP = p - lineStart;
            float proj = Vector2.Dot(toP, lineDir);
            Vector2 closest = lineStart + proj * lineDir;
            float dev = Vector2.Distance(p, closest);
            maxDeviation = Mathf.Max(maxDeviation, dev);
            totalDeviation += dev;
        }
        float avgDeviation = totalDeviation / forwardPath.Count;

        //Horizontal tilt for aim (hook/slice)
        float horizontalFactor = lineLength > 0 ? (lineEnd.x - lineStart.x) / lineLength : 0f;

        //Apply force
        Vector3 mainDir = transform.forward;
        Quaternion aimRot = Quaternion.Euler(0, horizontalFactor * maxAimAngle, 0);
        mainDir = aimRot * mainDir;

        Vector3 force = mainDir * power + Vector3.up * (power * 0.25f);
        ball.AddForce(force, ForceMode.Impulse);

        //Side force / slice if wobbly
        if (maxDeviation > straightnessThreshold)
        {
            float sideSign = Mathf.Sign((lineEnd.x - lineStart.x) + (avgDeviation * 0.1f));
            Vector3 sideForce = transform.right * sideSign * (maxDeviation * 0.8f);
            ball.AddForce(sideForce, ForceMode.Impulse);
        }

        Debug.Log($"Golf Swing! Power: {power:F1} | Max Dev: {maxDeviation:F1} | Tilt: {horizontalFactor:F2}");
    }

}
