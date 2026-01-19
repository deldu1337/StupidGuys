using UnityEngine;

public class Slide : StateBase
{
    // �����̵� �� ũ��
    [SerializeField] float _SlideForce = 1.0f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateEnter(animator, stateInfo, layerIndex);

        // ������Ƽ�� ������ y���� ������ �� ����.
        rigidbody.linearVelocity = new Vector3(rigidbody.linearVelocity.x,
                                         0.0f,
                                         rigidbody.linearVelocity.z);

        // ForceMode(�������� ���� ���� �� ���).Impulse(���. �������� ������, ���� * �ӵ�)
        // ���� ����� �չ������� _force��ŭ �ش�.
        rigidbody.AddForce(transform.forward * _SlideForce, ForceMode.Impulse);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateUpdate(animator, stateInfo, layerIndex);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ChangeState(animator, State.Move);
    }
}