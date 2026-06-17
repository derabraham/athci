using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    private float speed = 10;
    private float speed_rotate = 100;
    private float x;
    private float y;
    private Vector3 rotateValue;

    private void Start()
    {
        Debug.Log("Move Script");
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal_movement = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float vertical_movement = Input.GetAxis("Vertical") * speed * Time.deltaTime;
        float up_down = Input.GetAxis("UpDown") * speed * Time.deltaTime;

        y = Input.GetAxis("Horizontal_Rotation") * speed_rotate * Time.deltaTime;
        x = Input.GetAxis("Vertical_Rotation") * speed_rotate  * Time.deltaTime;
        rotateValue = new Vector3(x, y * -1, 0);
        gameObject.transform.eulerAngles = gameObject.transform.eulerAngles - rotateValue;
        gameObject.transform.Translate(horizontal_movement, up_down, vertical_movement);
 
    }



}
