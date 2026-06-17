using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float sensitivity = 2.0f;

    private Vector3 rotation = Vector3.zero;

    private bool slowModeActive = false; // Hinzugefügt
    public float slowMoveSpeed = 2.0f; // Hinzugefügt, Anpassung der langsamen Geschwindigkeit

    void Update()
    {
        float currentMoveSpeed = slowModeActive ? slowMoveSpeed : moveSpeed; // Hinzugefügt, um die Geschwindigkeit anzupassen

        // Bewegung
        float horizontalMovement = Input.GetAxis("Horizontal");
        float verticalMovement = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontalMovement, 0f, verticalMovement);
        transform.Translate(moveDirection * currentMoveSpeed * Time.deltaTime);

        // Drehung
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        rotation.x -= mouseY * sensitivity;
        rotation.y += mouseX * sensitivity;

        rotation.x = Mathf.Clamp(rotation.x, -90f, 90f);

        transform.localRotation = Quaternion.Euler(rotation);
    }

    public void SetSlowMode(bool slow) // Hinzugefügt
    {
        slowModeActive = slow;
    }
}