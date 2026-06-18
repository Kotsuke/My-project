using UnityEngine;

public class ClosingDoor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator doorAnimator;

    [Header("Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float triggerDistance = 10f;
    [SerializeField] private string closeTriggerName = "Close";

    private Transform playerTransform;
    private bool hasTriggered;

    private void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            playerTransform = player.transform;

        if (doorAnimator == null)
            doorAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (hasTriggered || playerTransform == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            playerTransform.position
        );

        if (distance <= triggerDistance)
            TriggerDoorClose();
    }

    private void TriggerDoorClose()
    {
        hasTriggered = true;

        if (doorAnimator != null)
            doorAnimator.SetTrigger(closeTriggerName);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }
}