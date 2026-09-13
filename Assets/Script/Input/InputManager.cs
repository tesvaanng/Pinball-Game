using System;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Input
{
    public class InputManager : Singleton<InputManager>
    {
        public PowerGauge gauge = new PowerGauge();

        private PlayerInputAction action;

        public event Action<float> OnLaunch;

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

        private void OnDisable()
        {
            if (action == null) return;

            action.Player.Fire.started -= OnFireStarted;
            action.Player.Fire.canceled -= OnFireCanceled;
            action.Disable();
        }

        private void Update()
        {
            if (gauge.isCharging)
            {
                gauge.UpdateCharge(Time.deltaTime);
            }
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
