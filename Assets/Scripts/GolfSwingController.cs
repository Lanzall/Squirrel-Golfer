using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;
using Unity.VisualScripting;

public class GolfSwingController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody ball;
    public Slider powerMeter;
    public Image powerMeterFill;

    [Header("Tuning")]
    public float maxBackswingDistance = 400f;
    public float powerMultiplier = 35f;
    public float maxPowerCap = 90f;
    public float straightnessThreshold = 15f;
    public float maxAimAngle = 30f;

    //NewInput System references
    private GolfControls controls;
    private InputAction swingAction;
    private InputAction positionAction;

    private List<Vector2> mousePositions = new List<Vector2>();
    private List<float> mouseTimes = new List<float>();
    private Vector2 swingStartPos;
    private float maxBackswingY = 0f;
    private bool isDragging = false;
    private bool hasStartedForward = false;

    private void Awake()
    {
        controls = new GolfControls();
        swingAction = controls.GolfSwing.Swing;
        positionAction = controls.GolfSwing.MousePosition;
    }
    private void OnEnable()
    {
        controls.Enable();
        swingAction.performed += OnSwingStarted;
        swingAction.canceled += OnSwingReleased;
    }
    private void OnDisable()
    {
        controls.Disable();
        swingAction.performed -= OnSwingStarted;
        swingAction.canceled -= OnSwingReleased;
    }
    private void OnSwingStarted(InputAction.CallbackContext ctx)
    {
        if (ball.linearVelocity.magnitude > 0.2f) return;
        isDragging = true;
        hasStartedForward = false;
        mousePositions.Clear();
        mouseTimes.Clear();
        swingStartPos = positionAction.ReadValue<Vector2>();
        mousePositions.Add(swingStartPos);
        mouseTimes.Add(Time.time);
        maxBackswingY = swingStartPos.y;
        if (powerMeter) powerMeter.value = 0f;
    }
    private void Update()
    {
        if (!isDragging) return;
        Vector2 currentPos = positionAction.ReadValue<Vector2>();
        mousePositions.Add(currentPos);
        mouseTimes.Add(Time.time);
        // Track deepest backswing
        if (currentPos.y < maxBackswingY)
            maxBackswingY = currentPos.y;
        // Detect when forward swing begins (mouse moving up)
        if (!hasStartedForward && currentPos.y > maxBackswingY + 20f)
            hasStartedForward = true;
        // Update power meter during forward swing (Wii gauge feel)
        if (hasStartedForward && powerMeter)
        {
            float progress = Mathf.InverseLerp(maxBackswingY, swingStartPos.y, currentPos.y);
            powerMeter.value = Mathf.Clamp01(progress * 1.1f); // slight overshoot possible
                                                               // Color change like Wii (blue ? yellow ? red)
            if (powerMeterFill)
            {
                if (progress < 0.7f) powerMeterFill.color = Color.cyan;
                else if (progress < 0.95f) powerMeterFill.color = Color.yellow;
                else powerMeterFill.color = Color.red;
            }
        }

        
    }
    private void OnSwingReleased(InputAction.CallbackContext ctx)
    {
        if (!isDragging) return;
        isDragging = false;
        if (mousePositions.Count < 15) return;
        ProcessWiiStyleSwing();
    }
    private void ProcessWiiStyleSwing()
    {
        // Find end of backswing (lowest Y)
        int backswingEndIndex = 0;
        float lowestY = mousePositions[0].y;
        for (int i = 1; i < mousePositions.Count; i++)
        {
            if (mousePositions[i].y < lowestY)
            {
                lowestY = mousePositions[i].y;
                backswingEndIndex = i;
            }
        }
        if (backswingEndIndex >= mousePositions.Count - 1) return;
        // Forward swing data only
        List<Vector2> forwardPath = new List<Vector2>();
        List<float> forwardTimes = new List<float>();
        for (int i = backswingEndIndex; i < mousePositions.Count; i++)
        {
            forwardPath.Add(mousePositions[i]);
            forwardTimes.Add(mouseTimes[i]);
        }
        // Power = (backswing depth) * (forward speed)
        float backswingDepth = swingStartPos.y - lowestY;
        float normalizedBackswing = Mathf.Clamp01(backswingDepth / maxBackswingDistance);
        float maxForwardSpeed = 0f;
        Vector2 prev = forwardPath[0];
        for (int i = 1; i < forwardPath.Count; i++)
        {
            float dist = Vector2.Distance(forwardPath[i], prev);
            float dt = forwardTimes[i] - forwardTimes[i - 1];
            if (dt > 0) maxForwardSpeed = Mathf.Max(maxForwardSpeed, dist / dt);
            prev = forwardPath[i];
        }
        float power = normalizedBackswing * maxForwardSpeed * powerMultiplier;
        power = Mathf.Min(power, maxPowerCap);
        // Straightness calculation (same as before, but only on forward path)
        Vector2 lineStart = forwardPath[0];
        Vector2 lineEnd = forwardPath[^1];
        Vector2 lineDir = (lineEnd - lineStart).normalized;
        float lineLength = Vector2.Distance(lineStart, lineEnd);
        float maxDeviation = 0f;
        foreach (Vector2 p in forwardPath)
        {
            Vector2 toP = p - lineStart;
            float proj = Vector2.Dot(toP, lineDir);
            Vector2 closest = lineStart + lineDir * proj;
            maxDeviation = Mathf.Max(maxDeviation, Vector2.Distance(p, closest));
        }
        float horizontalFactor = lineLength > 0 ? (lineEnd.x - lineStart.x) / lineLength : 0f;
        // Apply force
        Vector3 mainDir = transform.forward;
        Quaternion aimRot = Quaternion.Euler(0, horizontalFactor * maxAimAngle, 0);
        mainDir = aimRot * mainDir;
        Vector3 force = mainDir * power + Vector3.up * (power * 0.28f);
        ball.AddForce(force, ForceMode.Impulse);
        // Hook/slice on bad form or overswing
        bool overSwing = powerMeter && powerMeter.value > 1.05f; // went into red
        if (maxDeviation > straightnessThreshold || overSwing)
        {
            float sideSign = Mathf.Sign(horizontalFactor + (maxDeviation * 0.05f));
            Vector3 sideForce = transform.right * sideSign * (maxDeviation * 1.2f);
            ball.AddForce(sideForce, ForceMode.Impulse);
        }
        // Reset meter
        if (powerMeter) powerMeter.value = 0f;
        Debug.Log($"Wii-style Swing! Backswing: {normalizedBackswing:F2} | Power: {power:F1} | Deviation: {maxDeviation:F1}");
    }

}
