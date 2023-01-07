namespace EMQ.Shared.Quiz.Entities.Concrete;

// using System.Drawing.Point breaks the app when trimmed
public struct Point
{
    // public Point()
    // {
    //     X = 0;
    //     Y = 0;
    // }

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Point(int dw)
    {
        X = dw;
        Y = dw;
    }

    public int X { get; set; } = 0;

    public int Y { get; set; } = 0;

    public override bool Equals(object? obj) => obj is Point other && this.Equals(other);

    public bool Equals(Point p) => X == p.X && Y == p.Y;

    public override int GetHashCode() => (X, Y).GetHashCode();

    public static bool operator ==(Point lhs, Point rhs) => lhs.Equals(rhs);

    public static bool operator !=(Point lhs, Point rhs) => !(lhs == rhs);
}
