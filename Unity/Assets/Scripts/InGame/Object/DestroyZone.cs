using UnityEngine;

public class DestroyZone : MonoBehaviour
{
    AudioSource resSFX;
    public BallPool ballPool;
    private void Awake()
    {
        resSFX = GetComponent<AudioSource>();
    }

    // public ParticleSystem bounce;
    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Player")
        {
            collision.gameObject.transform.position = collision.gameObject.GetComponent<PlayerController>().respawnPosition;
            collision.gameObject.GetComponent<PlayerController>().ChangeStateForcely(State.Move);

            resSFX.Play();
        }
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ball"))
        {
            ballPool.ReturnBallPool(collision.gameObject);
        }
    }
}
