using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotPlatform_Up : MonoBehaviour
{
    public float rotatingSpeed = 130;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.up, rotatingSpeed * Time.deltaTime);

    }
}

//using Unity.Netcode;
//using UnityEngine;

//public class RotPlatform_Up : NetworkBehaviour
//{
//    public float rotatingSpeed = 130f;

//    void Update()
//    {
//        if (!IsServer) return;

//        transform.Rotate(Vector3.up, rotatingSpeed * Time.deltaTime, Space.Self);
//        transform.rotation = Quaternion.Normalize(transform.rotation);
//    }
//}
