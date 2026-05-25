using UnityEngine;

public sealed class CameraFollow : MonoBehaviour
{
    private void Start()
    {
        transform.position = new Vector3(0f, 0f, -10f);
    }

    public void SetPlayer(Transform t) { }
}
