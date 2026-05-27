using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("TARGET")]
    public Transform target;

    [Header("CAMERA SETTINGS")]
    public float height = 2f;
    public float smoothSpeed = 10f;

    [Header("ZOOM")]
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float zoomSpeed = 5f;
    public float zoomSmoothSpeed = 10f;

    [Header("MOUSE LOOK")]
    public float mouseSensitivity = 100f;
    public float minYAngle = -30f;
    public float maxYAngle = 60f;

    [Header("CAMERA COLLISION")]
    public bool checkCollision = true;
    public LayerMask collisionMask;

    private PlayerControls controls;
    

    // ROTATION
    private float currentX = 0f;
    private float currentY = 20f;

    // ZOOM
    private float currentDistance;
    private float targetDistance;

    void Awake()
    {
        controls = new PlayerControls();

        // LOOK INPUT
        controls.Player.Look.performed += ctx =>
        {
            Vector2 lookInput = ctx.ReadValue<Vector2>();

            currentX += lookInput.x * mouseSensitivity * Time.deltaTime;

            currentY -= lookInput.y * mouseSensitivity * Time.deltaTime;

            currentY = Mathf.Clamp(
                currentY,
                minYAngle,
                maxYAngle
            );
        };
    }

    void Start()
    {
        // INIT ZOOM
        currentDistance = distance;
        targetDistance = distance;

        // AUTO FIND TARGET
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    void OnEnable()
    {
        controls.Enable();

        // LOCK CURSOR
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        controls.Disable();

        // UNLOCK CURSOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        HandleZoom();

        SmoothZoom();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // ROTATION
        Quaternion rotation =
            Quaternion.Euler(currentY, currentX, 0);

        // CAMERA OFFSET
        Vector3 offset =
            new Vector3(0, height, -currentDistance);

        // DESIRED POSITION
        Vector3 desiredPosition =
            target.position + rotation * offset;

        // COLLISION CHECK
        if (checkCollision)
        {
            desiredPosition = CheckCameraCollision(
                target.position + Vector3.up * height,
                desiredPosition
            );
        }

        // SMOOTH MOVE
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime * smoothSpeed
        );

        // LOOK AT TARGET
        transform.LookAt(
            target.position + Vector3.up * height
        );
    }

    void HandleZoom()
    {
        if (Mouse.current == null)
            return;

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -=
                scroll * 0.01f * zoomSpeed;

            targetDistance = Mathf.Clamp(
                targetDistance,
                minDistance,
                maxDistance
            );
        }
    }

    void SmoothZoom()
    {
        currentDistance = Mathf.Lerp(
            currentDistance,
            targetDistance,
            Time.deltaTime * zoomSmoothSpeed
        );
    }

    Vector3 CheckCameraCollision(
        Vector3 targetPos,
        Vector3 desiredPos
    )
    {
        Vector3 direction =
            desiredPos - targetPos;

        float targetDistance =
            direction.magnitude;

        RaycastHit hit;

        if (
            Physics.Raycast(
                targetPos,
                direction.normalized,
                out hit,
                targetDistance,
                collisionMask
            )
        )
        {
            float safeDistance =
                Mathf.Max(hit.distance - 0.2f, minDistance);

            return targetPos +
                   direction.normalized * safeDistance;
        }

        return desiredPos;
    }

    public void ToggleCursorLock()
    {
        if (Cursor.lockState ==
            CursorLockMode.Locked)
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
    }

    void OnDrawGizmos()
    {
        if (target == null)
            return;

        // CAMERA TARGET
        Gizmos.color = Color.green;

        Gizmos.DrawSphere(
            target.position + Vector3.up * height,
            0.2f
        );

        // CAMERA LINE
        Gizmos.color = Color.blue;

        Gizmos.DrawLine(
            target.position + Vector3.up * height,
            transform.position
        );
    }
}