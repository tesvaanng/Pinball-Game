using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Pinball.Core;
using Pinball.UI;
using Pinball.Board;

namespace Pinball.Input
{
    public class InputManager : Singleton<InputManager>
    {
        public PowerGauge gauge = new PowerGauge();
        public float chargeSpeed = 1f;
        public float maxCharge = 1f;

        private PlayerInputAction action;

        public event Action<float> OnLaunch;

        public Vector2 PointerScreenPosition { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            action = new PlayerInputAction();
        }

        private void OnEnable()
        {
            if (action == null)
            {
                action = new PlayerInputAction();
            }

            action.Enable();
            action.Player.Fire.started += OnFireStarted;
            action.Player.Fire.canceled += OnFireCanceled;
        }
        private void Start()
        {
            gauge.chargeSpeed = chargeSpeed;
            gauge.maxCharge = maxCharge;
        }
        private void OnDisable()
        {
            if (action == null) return;

            action.Player.Fire.started -= OnFireStarted;
            action.Player.Fire.canceled -= OnFireCanceled;
            
            action.Disable();
        }
        private void Update()
        {
            PointerScreenPosition = ReadPointerScreenPosition();

            if (gauge.isCharging)
            {
                float currentPower = gauge.UpdateCharge(Time.deltaTime);
                if (BattleUIManager.IsInitialized)
                {
                    BattleUIManager.Instance.ShowFireCharge(currentPower);
                }
            }

        }

        private static Vector2 ReadPointerScreenPosition()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                return pointer.position.ReadValue();
            }

            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            if (Touchscreen.current != null)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }

            return Vector2.zero;
        }

        private void OnFireStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            gauge.StartCharge();
        }

        private void OnFireCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            float power = gauge.Release();
            if (OnLaunch != null)
            {
                OnLaunch(power);
            }
        }
    }
}
