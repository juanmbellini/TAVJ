using UnityEngine;

public class PlayerController : MonoBehaviour {
    public PlayerInput PlayerInput; // { get; set; }

    public void Update() {
        var up = Input.GetKey(KeyCode.W);
        var left = Input.GetKey(KeyCode.A);
        var down = Input.GetKey(KeyCode.S);
        var right = Input.GetKey(KeyCode.D);
        var shoot = Input.GetKey(KeyCode.K);

        PlayerInput = new PlayerInput(up, left, down, right, shoot);
    }
}