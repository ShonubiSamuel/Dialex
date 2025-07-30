using UnityEngine;

public class PlayAnimationOnKey : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTriggerName = "PlayAnim";
    public GameObject Text;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (animator != null)
            {
                Text.SetActive(true);
                animator.SetTrigger(animationTriggerName);
               
            }
            else
            {
                Debug.LogWarning("Animator not assigned!");
            }
        }
    }
}