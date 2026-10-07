using System;
using UnityEngine;
using UnityEngine.InputSystem;
public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    [NonSerialized] public PlayerInput.OnFootActions onFoot;

    private PlayerMotor playerMotor;
    private PlayerLook playerLook;
    private PlayerHealth playerHealth;
    private PlayerInteract playerInteract;

    void Awake()
    {
        InitializeInput();
    }

    private void InitializeInput()
    {
        playerMotor = GetComponent<PlayerMotor>();
        playerLook = GetComponent<PlayerLook>();
        playerHealth = GetComponent<PlayerHealth>();
        playerInteract = GetComponent<PlayerInteract>();

        if (playerInput != null) return;
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        onFoot.Jump.performed += OnJump;
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (ForestStoryDirector.BlocksInput) return;
        if (playerMotor != null && (playerHealth == null || !playerHealth.IsDead))
            playerMotor.Jump();
    }

    void Update(){
        if (ForestStoryDirector.BlocksInput) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        // CharacterController has no Rigidbody interpolation. Move and look
        // together each rendered frame, before the weapon's LateUpdate.
        if (playerLook != null)
            playerLook.ProcessLook(onFoot.Look.ReadValue<Vector2>());
        if (playerMotor != null)
            playerMotor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
        if (playerInteract != null && playerInteract.isActiveAndEnabled)
            playerInteract.ProcessInteraction(onFoot.Interact.WasPressedThisFrame());
    }
    private void OnEnable()
    {
        // Script reload can call OnEnable without running Awake again.
        InitializeInput();
        onFoot.Enable();
    }

    private void OnDisable()
    {
        playerInput?.Disable();
    }

    private void OnDestroy()
    {
        if (playerInput == null) return;
        playerInput.Disable();
        onFoot.Jump.performed -= OnJump;
        playerInput.Dispose();
        playerInput = null;
        onFoot = default;
    }
}
