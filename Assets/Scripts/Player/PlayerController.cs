using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person controller for Dead Wave. Movement + look on the new Input System.
/// Actions are defined in code so the component works with zero editor wiring.
/// (We can migrate to the .inputactions asset later when we add rebinding.)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 7.0f;
    [SerializeField] private float gravity = -19.6f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private CharacterController body;
    private Transform cameraTransform;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction unlockCursorAction;
    private InputAction relockCursorAction;

    private float pitch;
    private float verticalVelocity;

    private void Awake()
    {
        body = GetComponent<CharacterController>();

        Camera childCamera = GetComponentInChildren<Camera>();
        if (childCamera != null)
        {
            cameraTransform = childCamera.transform;
        }
        else
        {
            Debug.LogError("PlayerController: no Camera found in children. Look pitch will not work.", this);
        }

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        unlockCursorAction = new InputAction("UnlockCursor", InputActionType.Button, "<Keyboard>/escape");
        relockCursorAction = new InputAction("RelockCursor", InputActionType.Button, "<Mouse>/leftButton");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        sprintAction.Enable();
        unlockCursorAction.Enable();
        relockCursorAction.Enable();
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        sprintAction.Disable();
        unlockCursorAction.Disable();
        relockCursorAction.Disable();
        SetCursorLocked(false);
    }

    private void Update()
    {
        HandleCursor();
        HandleLook();
        HandleMovement();
    }

    private void HandleCursor()
    {
        if (unlockCursorAction.WasPressedThisFrame())
        {
            SetCursorLocked(false);
        }
        else if (relockCursorAction.WasPressedThisFrame() && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLocked(true);
        }
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 lookDelta = lookAction.ReadValue<Vector2>() * lookSensitivity;

        transform.Rotate(0f, lookDelta.x, 0f);

        pitch = Mathf.Clamp(pitch - lookDelta.y, minPitch, maxPitch);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector2 moveInput = Cursor.lockState == CursorLockMode.Locked
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;
        Vector3 planarVelocity = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

        if (body.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;
        body.Move(velocity * Time.deltaTime);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
