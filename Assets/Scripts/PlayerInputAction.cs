using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // TAMBAHKAN BARIS INI

public class PlayerInputAction : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Animator animator;
    public Transform groundCheck;
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 2f;
    public float sprintSpeed = 5f;
    public float jumpHeight = 5f;
    public float gravity = -20f;
    public float rotationSpeed = 10f;

    [Header("Ground Check")]
    public float groundDistance = 0.3f;
    public LayerMask groundMask;

    [Header("Animation")]
    public float animationSmoothTime = 0.1f;

    [Header("Climb")]
    public float climbCheckDistance = 1f;
    public float climbMoveForward = 1f;
    public float climbMoveUp = 1.5f;
    public float climbDuration = 1f;
    public LayerMask climbMask;

    [Header("Climb Cooldown")]
    public float climbCooldown = 0.2f;

    private float lastClimbTime;
    private PlayerControls controls;
    private Vector2 moveInput;
    private Vector3 velocity;

    private bool isGrounded;
    private bool isSprinting;
    private bool isClimbing;

    private float currentAnimSpeed;
    private float speedVelocity;

    [Header("Slide")]
    public float slideSpeed = 10f;
    public float slideDuration = 0.8f;
    public float slideHeight = 1f; // Tinggi kapsul saat nunduk/slide

    private bool isSliding;
    private float originalHeight; // Menyimpan tinggi asli kapsul
    private Vector3 originalCenter; // Menyimpan titik tengah asli kapsul

    [Header("Respawn & Finish")]
    public Transform respawnPoint;      // Titik di mana pemain akan hidup lagi
    public float fallThreshold = -15f;  // Batas ketinggian Y sebelum dianggap jatuh
    public string nextSceneName = "Level2"; // NAMA SCENE TUJUAN

    void Awake()
    {
        controls = new PlayerControls();

        // MOVE
        controls.Player.Move.performed += ctx =>
        {
            moveInput = ctx.ReadValue<Vector2>();
        };

        controls.Player.Move.canceled += ctx =>
        {
            moveInput = Vector2.zero;
        };

        // SPRINT
        controls.Player.Sprint.performed += ctx =>
        {
            isSprinting = true;
        };

        controls.Player.Sprint.canceled += ctx =>
        {
            isSprinting = false;
        };

        // JUMP / CLIMB
        controls.Player.Jump.performed += ctx =>
        {
            TryJumpOrClimb();
        };

        // SLIDE
        controls.Player.Slide.performed += ctx =>
        {
            TrySlide();
        };
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Simpan ukuran asli Character Controller
        originalHeight = controller.height;
        originalCenter = controller.center;
    }

    void Update()
    {
        GroundCheck();

        // CEK APAKAH JATUH
        if (transform.position.y < fallThreshold)
        {
            Respawn();
        }

        if (!isClimbing && !isSliding)
        {
            Move();
            RotatePlayer();
            ApplyGravity();
        }

        UpdateAnimator();
    }

    public void Respawn()
    {
        // 1. Matikan Character Controller sementara
        controller.enabled = false;

        // 2. Pindahkan pemain ke titik Respawn
        transform.position = respawnPoint.position;
        
        // 3. Reset kecepatan jatuh / gravitasi agar tidak langsung melesat ke bawah
        velocity = Vector3.zero; 

        // 4. Nyalakan lagi Character Controller-nya
        controller.enabled = true;
        
        Debug.Log("Pemain Respawn!");
    }

    // Fungsi bawaan Unity untuk mendeteksi tabrakan dengan "Trigger"
    void OnTriggerEnter(Collider other)
    {
        // JIKA MENYENTUH GARIS FINISH
        if (other.CompareTag("Finish"))
        {
            Debug.Log("Finish! Pindah ke " + nextSceneName);
            
            // Perintah untuk memuat Scene baru
            SceneManager.LoadScene(nextSceneName); 
        }
        
        // JIKA MENYENTUH BAHAYA / DURI
        if (other.CompareTag("Danger"))
        {
            Respawn();
        }
    }

    void GroundCheck()
    {
        if (isClimbing)
            return;

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundDistance,
            groundMask
        );

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    void Move()
    {
        Vector3 inputDirection =
            new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = cameraTransform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            Vector3 moveDirection =
                (cameraForward * inputDirection.z) +
                (cameraRight * inputDirection.x);

            moveDirection.Normalize();

            float currentSpeed =
                isSprinting ? sprintSpeed : walkSpeed;

            controller.Move(
                moveDirection *
                currentSpeed *
                Time.deltaTime
            );
        }
    }

    void RotatePlayer()
    {
        Vector3 inputDirection =
            new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = cameraTransform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            Vector3 moveDirection =
                (cameraForward * inputDirection.z) +
                (cameraRight * inputDirection.x);

            moveDirection.Normalize();

            float targetAngle =
                Mathf.Atan2(moveDirection.x, moveDirection.z)
                * Mathf.Rad2Deg;

            float angle = Mathf.LerpAngle(
                transform.eulerAngles.y,
                targetAngle,
                Time.deltaTime * rotationSpeed
            );

            transform.rotation =
                Quaternion.Euler(0f, angle, 0f);
        }
    }

    void TrySlide()
    {
        // Hanya bisa slide kalau di tanah, lagi lari, belum memanjat, dan belum slide
        if (isGrounded && isSprinting && !isClimbing && !isSliding && moveInput.magnitude > 0.1f)
        {
            StartCoroutine(SlideRoutine());
        }
    }

    IEnumerator SlideRoutine()
    {
        isSliding = true;
        animator.SetTrigger("Slide");

        Vector3 slideDirection = transform.forward; 

        float heightDifference = originalHeight - slideHeight;
        float newCenterY = originalCenter.y - (heightDifference / 2f);

        // 1. SAAT MENGECIL: Ubah HEIGHT dulu, baru CENTER
        controller.height = slideHeight;
        controller.center = new Vector3(originalCenter.x, newCenterY, originalCenter.z);

        float timer = 0f;

        // FASE 1: MELUNCUR NORMAL
        while (timer < slideDuration)
        {
            timer += Time.deltaTime;

            controller.Move(slideDirection * slideSpeed * Time.deltaTime);

            // Jaga gravitasi agar tidak menumpuk terlalu besar saat slide
            if (controller.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }
            
            controller.Move(velocity * Time.deltaTime);

            yield return null; 
        }

        // FASE 2: SENSOR ATAP (CEK TEROWONGAN)
        bool isUnderCeiling = true;

        while (isUnderCeiling)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * (slideHeight / 2f);
            float checkDistance = originalHeight - (slideHeight / 2f) + 0.2f;

            if (Physics.Raycast(rayOrigin, Vector3.up, checkDistance))
            {
                // Terus jalan kalau masih di dalam terowongan
                controller.Move(slideDirection * walkSpeed * Time.deltaTime);
                
                if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
                else velocity.y += gravity * Time.deltaTime;
                
                controller.Move(velocity * Time.deltaTime);
                
                yield return null; 
            }
            else
            {
                isUnderCeiling = false; 
            }
        }

        // 3. SAAT MEMBESAR (BERDIRI): Ubah CENTER dulu, baru HEIGHT!
        // Ini memastikan kaki kapsul ditarik ke atas dulu, lalu dipanjangkan ke bawah
        // sehingga tidak pernah menembus aspal/lantai.
        controller.center = originalCenter;
        controller.height = originalHeight;

        isSliding = false;
    }

    void TryJumpOrClimb()
    {
        if (isClimbing)
            return;

        if (Time.time < lastClimbTime + climbCooldown)
            return;

        RaycastHit hit;

        Vector3 rayOrigin =
            transform.position + Vector3.up;

        // CHECK CLIMB
        if (
            Physics.Raycast(
                rayOrigin,
                transform.forward,
                out hit,
                climbCheckDistance,
                climbMask
            )
        )
        {
            lastClimbTime = Time.time;
            StartCoroutine(ClimbRoutine());
            return;
        }

        // NORMAL JUMP (Diperbaiki)
        // NORMAL JUMP
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Cek apakah pemain sedang sprint dan bergerak
            if (isSprinting && moveInput.magnitude > 0.1f)
            {
                animator.SetTrigger("RunJump");
            }
            else
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    IEnumerator ClimbRoutine()
    {
        isClimbing = true;
        animator.SetTrigger("Climb");
        velocity = Vector3.zero;

        Vector3 startPosition = transform.position;
        Vector3 targetPosition =
            transform.position +
            transform.forward * climbMoveForward +
            Vector3.up * climbMoveUp;

        float timer = 0f;

        while (timer < climbDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                timer / climbDuration
            );

            Vector3 nextPosition =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t
                );

            Vector3 moveDelta =
                nextPosition - transform.position;

            controller.Move(moveDelta);
            yield return null;
        }

        isClimbing = false;
    }

    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void UpdateAnimator()
    {
        float targetSpeed = moveInput.magnitude;

        currentAnimSpeed = Mathf.SmoothDamp(
            currentAnimSpeed,
            targetSpeed,
            ref speedVelocity,
            animationSmoothTime
        );

        animator.SetFloat(
            "Speed",
            currentAnimSpeed
        );

        animator.SetBool(
            "IsRunning",
            isSprinting && moveInput.magnitude > 0.1f
        );

        animator.SetBool(
            "IsGrounded",
            isGrounded
        );

        animator.SetFloat(
            "VerticalVelocity",
            velocity.y
        );

        animator.SetBool(
            "IsClimbing",
            isClimbing
        );
        // Tambahkan ini di bagian bawah fungsi UpdateAnimator()
        animator.SetBool("IsSliding", isSliding);
    }

    void OnDrawGizmosSelected()
    {
        // GROUND CHECK
        if (groundCheck != null)
        {
            Gizmos.color =
                isGrounded ? Color.green : Color.red;

            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundDistance
            );
        }

        // CLIMB RAY
        Gizmos.color = Color.blue;

        Vector3 rayOrigin =
            transform.position + Vector3.up;

        Gizmos.DrawRay(
            rayOrigin,
            transform.forward * climbCheckDistance
        );
    }
}