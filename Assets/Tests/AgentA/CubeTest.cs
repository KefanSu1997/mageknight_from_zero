using NUnit.Framework;
using UnityEngine;

public class CubeTest
{
    [Test]
    public void CubeExists()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.AddComponent<RotateCube>();

        Assert.IsNotNull(cube.GetComponent<RotateCube>());
    }
}
