using UnityEngine;

public class ArmPositionDebugger : MonoBehaviour
{
    [SerializeField] private CharacterAssembler characterAssembler;
    
    [ContextMenu("Print Arm Hierarchy")]
    public void PrintArmHierarchy()
    {
        if (characterAssembler == null)
        {
            Debug.LogError("CharacterAssembler not assigned!");
            return;
        }
        
        Transform leftArm = characterAssembler.GetLeftArmTransform();
        Transform rightArm = characterAssembler.GetRightArmTransform();
        
        Debug.Log("=== ARM HIERARCHY DEBUG ===");
        
        if (leftArm != null)
        {
            Debug.Log($"LEFT ARM: {leftArm.name}");
            Debug.Log($"  - World Position: {leftArm.position}");
            Debug.Log($"  - Local Position: {leftArm.localPosition}");
            Debug.Log($"  - Parent: {leftArm.parent?.name ?? "None"}");
            Debug.Log($"  - Parent Local Position: {leftArm.parent?.localPosition ?? Vector3.zero}");
        }
        else
        {
            Debug.LogWarning("Left Arm is NULL!");
        }
        
        if (rightArm != null)
        {
            Debug.Log($"RIGHT ARM: {rightArm.name}");
            Debug.Log($"  - World Position: {rightArm.position}");
            Debug.Log($"  - Local Position: {rightArm.localPosition}");
            Debug.Log($"  - Parent: {rightArm.parent?.name ?? "None"}");
            Debug.Log($"  - Parent Local Position: {rightArm.parent?.localPosition ?? Vector3.zero}");
        }
        else
        {
            Debug.LogWarning("Right Arm is NULL!");
        }
        
        Debug.Log("=========================");
    }
}
