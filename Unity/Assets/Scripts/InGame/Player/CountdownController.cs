using UnityEngine;

public class CountdownController : MonoBehaviour
{
    [SerializeField] GameObject anim;

    [SerializeField] GameObject time1;
    [SerializeField] GameObject time2;
    [SerializeField] GameObject time3;
    [SerializeField] GameObject timeGO;

    Animator animator;

    [SerializeField] AudioSource mysfx;
    [SerializeField] AudioClip startsfx;
    [SerializeField] AudioClip gosfx;

    private void Awake()
    {
        animator = anim != null ? anim.GetComponent<Animator>() : null;
    }

    private void Start()
    {
        HideAll();
    }

    private void OnEnable()
    {
        InGameManager.OnCountdownTick += HandleCountdownTick;
    }

    private void OnDisable()
    {
        InGameManager.OnCountdownTick -= HandleCountdownTick;
    }

    private void HideAll()
    {
        if (time1) time1.SetActive(false);
        if (time2) time2.SetActive(false);
        if (time3) time3.SetActive(false);
        if (timeGO) timeGO.SetActive(false);

        if (animator)
        {
            animator.SetBool("Num3", false);
        }
    }

    private void HandleCountdownTick(int t)
    {
        // t == 0 : ÀüºÎ ¼û±â±â
        if (t <= 0)
        {
            HideAll();
            return;
        }

        // °ãÄ§ ¹æÁö: ¸Å Æ½¸¶´Ù ¸®¼Â ÈÄ ÇÏ³ª¸¸ ÄÒ´Ù
        HideAll();

        if (t == 4)
        {
            if (time3) time3.SetActive(true);
            if (animator) animator.SetBool("Num3", true);
            if (mysfx && startsfx) mysfx.PlayOneShot(startsfx);
        }
        else if (t == 3)
        {
            if (time2) time2.SetActive(true);
            if (mysfx && startsfx) mysfx.PlayOneShot(startsfx);
        }
        else if (t == 2)
        {
            if (time1) time1.SetActive(true);
            if (mysfx && startsfx) mysfx.PlayOneShot(startsfx);
        }
        else if (t == 1)
        {
            if (timeGO) timeGO.SetActive(true);
            if (mysfx && gosfx) mysfx.PlayOneShot(gosfx);
        }
    }
}
