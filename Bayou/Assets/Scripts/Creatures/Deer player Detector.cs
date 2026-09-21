using Bayou.Player;
using UnityEngine;

public class DeerplayerDetector : MonoBehaviour
{
    [SerializeField] private Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        animator.SetBool("doesNoticePlayer", true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        animator.SetBool("doesNoticePlayer", false);
    }

    private static bool IsPlayer(Collider other) =>
       other != null &&
       (other.CompareTag("Player") || other.GetComponentInParent<BayouCharacterMotor>() != null);
}
