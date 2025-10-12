using NUnit.Framework;
using UnityEngine;
using MageKnight.Scripts.Demo;

namespace MK.Tests.demo
{
    [Category("AgentB")]
    public class SphereTest
    {
        [Test]
        public void SphereExists()
        {
            var sphere = new GameObject("Sphere");
            sphere.AddComponent<BounceSphere>();
            Assert.IsNotNull(sphere.GetComponent<BounceSphere>());
        }
    }
}
