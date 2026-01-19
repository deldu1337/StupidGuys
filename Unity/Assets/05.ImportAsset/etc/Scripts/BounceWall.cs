using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BounceWall : MonoBehaviour
{
    [SerializeField] string playerTag;
    [SerializeField] float bounceForce;
    [SerializeField] AudioSource colSound;

    void Start()
    {
        if (colSound == null)
            colSound = GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.tag == playerTag)
        {
            CollisionSound();

            Rigidbody otherRB = collision.rigidbody;

            otherRB.linearVelocity = new Vector3(0, 0, 0);

            otherRB.AddForce(Vector3.back * bounceForce, ForceMode.Impulse);
        }
    }

    void Update()
    {
    }

    void CollisionSound()
    {
        // 방어: 오디오 없으면 그냥 종료
        if (colSound == null)
        {
            return;
        }

        colSound.Play();
    }
}
