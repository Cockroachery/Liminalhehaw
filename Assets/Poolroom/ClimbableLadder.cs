using UnityEngine;

[DisallowMultipleComponent]
public sealed class ClimbableLadder : MonoBehaviour
{
    [SerializeField] private Vector3 localDismountDirection = Vector3.forward;

    public Vector3 DismountDirection => transform.TransformDirection(localDismountDirection).normalized;
}
