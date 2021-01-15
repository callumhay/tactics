//===========================================================================//
//                       FreeFlyCamera (Version 1.2)                         //
//                        (c) 2019 Sergey Stafeyev                           //
//===========================================================================//

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class FreeFlyCamera : MonoBehaviour {
  #region UI

  [Space]

  [SerializeField]
  [Tooltip("Camera rotation by mouse movement is active")]
  private bool _enableRotation = true;

  [SerializeField]
  [Tooltip("Sensitivity of mouse rotation")]
  private float _mouseSense = 10f;

  [Space]

  [SerializeField]
  [Tooltip("Camera zooming in/out by 'Mouse Scroll Wheel' is active")]
  private bool _enableZoom = true;

  [SerializeField]
  [Tooltip("Velocity of camera zooming in/out")]
  private float _zoomSpeed = 10f;

  [Space]

  [SerializeField]
  [Tooltip("Camera movement keys are active")]
  private bool _enableMovement = true;

  [SerializeField]
  [Tooltip("Camera movement speed")]
  private float _movementSpeed = 10f;

  [SerializeField]
  [Tooltip("Speed of the quick camera movement when holding the 'Left Shift' key")]
  private float _boostedSpeed = 30f;

  [Space]

  [SerializeField]
  [Tooltip("Acceleration at camera movement is active")]
  private bool _enableSpeedAcceleration = true;

  [SerializeField]
  [Tooltip("Rate which is applied during camera movement")]
  private float _speedAccelerationFactor = 1.25f;

  #endregion UI

  private CursorLockMode _wantedMode;

  private bool _isSpeedBoosted = false;
  private float _currentIncrease = 1;
  private float _currentIncreaseMem = 0;

  private Vector3 _initPosition;
  private Vector3 _initRotation;

  private Vector2 _inputMoveVec = new Vector2(0,0);
  private Vector2 _inputLookVec = new Vector2(0,0);
  private float _strafeUp = 0f;
  private float _strafeDown = 0f;
  private float _inputZoom = 0f;
  

#if UNITY_EDITOR
  private void OnValidate() {
    if (_boostedSpeed < _movementSpeed) {
      _boostedSpeed = _movementSpeed;
    }
  }
#endif

  private void Start() {
    _initPosition = transform.position;
    _initRotation = transform.eulerAngles;
  }

  private void OnEnable() {
    _wantedMode = CursorLockMode.Locked;
    ApplyCursorState();
  }
  private void OnDisable() {
    Cursor.lockState = _wantedMode = CursorLockMode.None;
    ApplyCursorState();
  }

  public void OnFire1(InputValue inputValue) {
    if (_wantedMode == CursorLockMode.Locked) { return; }
    _wantedMode = CursorLockMode.Locked;
    ApplyCursorState();
  }

  public void OnMove(InputValue inputValue) {
    if (Cursor.visible) { return; }
    _inputMoveVec = inputValue.Get<Vector2>();
  }

  public void OnLook(InputValue inputValue) {
    if (Cursor.visible) { return; }
    _inputLookVec = inputValue.Get<Vector2>();
  }

  public void OnZoom(InputValue inputValue) {
    if (Cursor.visible) { return; }
    _inputZoom = inputValue.Get<float>();
  }

  public void OnStrafeUp(InputValue inputValue) {
    _strafeUp = inputValue.Get<float>();
  }

  public void OnStrafeDown(InputValue inputValue) {
    _strafeDown = inputValue.Get<float>();
  }

  public void OnBoostSpeed(InputValue inputValue) {
    _isSpeedBoosted = inputValue.Get<float>() > 0;
  }

  public void OnResetPosition(InputValue inputValue) {
    if (Cursor.visible) { return; }
    if (inputValue.Get<float>() > 0) {
      transform.position = _initPosition;
      transform.eulerAngles = _initRotation;
    }
  }

  public void OnEscape(InputValue inputValue) {
    Cursor.lockState = _wantedMode = CursorLockMode.None;
    ApplyCursorState();
  }
  
  private void ApplyCursorState() {
    Cursor.lockState = _wantedMode;
    // Hide cursor when locking
    Cursor.visible = (CursorLockMode.Locked != _wantedMode);
  }

  private void CalculateCurrentIncrease(bool moving) {
    _currentIncrease = Time.deltaTime;

    if (!_enableSpeedAcceleration || _enableSpeedAcceleration && !moving) {
      _currentIncreaseMem = 0;
      return;
    }

    _currentIncreaseMem += Time.deltaTime * (_speedAccelerationFactor - 1);
    _currentIncrease = Time.deltaTime + Mathf.Pow(_currentIncreaseMem, 3) * Time.deltaTime;
  }

  private void Update() {
    if (Cursor.visible) { return; }

    if (_enableZoom) {
      transform.Translate(Vector3.forward * _inputZoom * Time.deltaTime * _zoomSpeed);
    }

    if (_enableMovement) {
      Vector3 deltaPosition = Vector3.zero;

      if (_inputMoveVec.y > 0) { deltaPosition += transform.forward; }
      else if (_inputMoveVec.y < 0) { deltaPosition -= transform.forward; }
      
      if (_inputMoveVec.x > 0) { deltaPosition += transform.right; }
      else if (_inputMoveVec.x < 0) { deltaPosition -= transform.right; }

      if (_strafeUp > 0) { deltaPosition += transform.up; }
      else if (_strafeDown > 0) { deltaPosition -= transform.up; }

      CalculateCurrentIncrease(deltaPosition != Vector3.zero);
      transform.position += deltaPosition * (_isSpeedBoosted ? _boostedSpeed : _movementSpeed) * _currentIncrease;
    }

    if (_enableRotation) {
      // Pitch
      transform.rotation *= Quaternion.AngleAxis(-_inputLookVec.y * Time.deltaTime * _mouseSense, Vector3.right);
      // Yaw
      transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y + _inputLookVec.x * Time.deltaTime * _mouseSense, transform.eulerAngles.z);
    }
  }
}
