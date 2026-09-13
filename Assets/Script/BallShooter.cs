using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallShooter : MonoBehaviour
{
    public PlayerInputAction action;
    public GameObject ballPrefab;
    public Transform shootPoint;
    public float shootForce = 10f;
    private void Awake()
    {
        action = new PlayerInputAction();
    }
    private void OnEnable()
    {
        action.Enable();
    }
    private void OnDisable()
    {
        action.Disable();
    }
    void Update()
    {
        action.Player.Fire.started += ShootBall;
    }

    private void ShootBall(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        GameObject ball = Instantiate(ballPrefab, shootPoint.position, shootPoint.rotation);
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        rb.AddForce(-shootPoint.up * shootForce, ForceMode.Impulse);
    }
}
