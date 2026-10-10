using System;
using UnityEngine;

// membre.position contient seulement ce JSON x/y/z, jamais une session.
[Serializable]
public sealed class LibreViesPosition
{
    public float x;
    public float y;
    public float z;

    public static LibreViesPosition Depuis(Vector3 point)
    {
        return new LibreViesPosition { x = point.x, y = point.y, z = point.z };
    }
    public bool EstValide
    {
        get
        {
            return !Single.IsNaN(x) && !Single.IsInfinity(x) && Mathf.Abs(x) <= 125f
                && !Single.IsNaN(y) && !Single.IsInfinity(y) && Mathf.Abs(y) <= 500f
                && !Single.IsNaN(z) && !Single.IsInfinity(z) && Mathf.Abs(z) <= 125f;
        }
    }
    public Vector3 Point { get { return new Vector3(x, y, z); } }
}
