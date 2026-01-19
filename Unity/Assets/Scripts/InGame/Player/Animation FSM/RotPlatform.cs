using Unity.Netcode;
using UnityEngine;

public class RotPlatform : NetworkBehaviour
{
    public enum Axis { Up, Down, Forward, Back, Right, Left }

    public float rotatingSpeed = 130f;
    public Axis axis = Axis.Up;
    public Space space = Space.Self; // NetworkTransform이 Local/World 중 뭐로 동기화하느냐에 맞춰도 됨

    private Rigidbody rigid;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        rigid.isKinematic = true;
    }

    public override void OnNetworkSpawn()
    {
        // 서버 권위면 클라이언트에서는 아예 스크립트를 꺼서 transform을 건드리지 않게 한다
        if (!IsServer)
            enabled = false;
    }

    private void FixedUpdate()
    {
        Quaternion deltaRotation = Quaternion.AngleAxis(rotatingSpeed * Time.fixedDeltaTime, 
                                                        transform.TransformDirection(GetAxis()));
        rigid.MoveRotation(deltaRotation * rigid.rotation);

    }
    private void OnCollisionStay(Collision col)
    {
               
        Rigidbody playerRb = col.gameObject.GetComponent<Rigidbody>();
        if(playerRb != null )
        {
            
            Vector3 axisWorld = transform.TransformDirection(GetAxis()).normalized;
            float angularSpeedRad = rotatingSpeed * Mathf.Deg2Rad;
            Vector3 r = (playerRb.worldCenterOfMass - transform.position);
            Vector3 tangentialVelocity = Vector3.Cross(axisWorld * angularSpeedRad, r);
            tangentialVelocity.y = 0f;

            Vector3 needVelocity = tangentialVelocity - playerRb.linearVelocity;
            needVelocity.y = 0f;
            playerRb.AddForce(needVelocity, ForceMode.VelocityChange);
        }
    }
    private Vector3 GetAxis()
    {
        Vector3 a = axis switch
        {
            Axis.Up => Vector3.up,
            Axis.Down => Vector3.down,
            Axis.Forward => Vector3.forward,
            Axis.Back => Vector3.back,
            Axis.Right => Vector3.right,
            Axis.Left => Vector3.left,
            _ => Vector3.up
        };
        return a;
    }

}