using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float turnSpeed = 80f;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        moveInput = 0f;
        turnInput = 0f;

        if (Input.GetKey(KeyCode.W))
            moveInput = 1f;
        else if (Input.GetKey(KeyCode.S))
            moveInput = -1f;

        if (Input.GetKey(KeyCode.A))
            turnInput = -1f;
        else if (Input.GetKey(KeyCode.D))
            turnInput = 1f;
    }

    void FixedUpdate()
    {
        Vector3 move = transform.forward * moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            Quaternion turn = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turn);
        }
    }
}