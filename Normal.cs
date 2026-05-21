using UnityEngine;

public class Normal
{
    public Vector3 trueNormal;
    public Vector3 surfaceNormal;

    //constructor
    public Normal(Vector3 trueNormal, Vector3 surfaceNormal)
    {
        this.trueNormal = trueNormal;
        this.surfaceNormal = surfaceNormal;
    }

    /// Summary:
    ///     Returns a new Normal with both vectors negated, representing
    ///     the contact normal from the opposing entity's perspective.
    public Normal Flipped() => new Normal(-trueNormal, -surfaceNormal);
}
