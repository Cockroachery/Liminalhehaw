using UnityEngine;

[DisallowMultipleComponent]
public sealed class SwimmableWater : MonoBehaviour
{
    [SerializeField] private float surfaceOffset = 0f;

    public float SurfaceHeight => transform.position.y + surfaceOffset;
}
