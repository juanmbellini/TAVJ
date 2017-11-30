using UnityEngine;

public class PlayerNetworkView : MonoBehaviour {
//    public int PlayerId { get; set; }

    public int PlayerId;

    public void UpdatePosition(Vector2 position) {
        transform.position = position;
    }
}