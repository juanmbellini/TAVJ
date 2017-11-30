public class PlayerInput {
    //Vector2 lookDir; //si moving esta en true mandar 2 floats

    public bool Up; //{ get; private set; }

    public bool Left; // { get; private set; }

    public bool Down; // { get; private set; }

    public bool Right; //{ get; private set; }

    public bool Shoot; //{ get; private set; }

    public PlayerInput() {
    }

    public PlayerInput(bool up, bool left, bool down, bool right, bool shoot) {
        Up = up;
        Left = left;
        Down = down;
        Right = right;
        Shoot = shoot;
    }

    public void Save(BitBuffer buffer) {
        buffer.PutBit(Up);
        buffer.PutBit(Down);
        buffer.PutBit(Left);
        buffer.PutBit(Right);
        buffer.PutBit(Shoot);
    }

    public void Load(BitBuffer buffer) {
        Up = buffer.GetBit();
        Down = buffer.GetBit();
        Left = buffer.GetBit();
        Right = buffer.GetBit();
        Shoot = buffer.GetBit();
    }
}