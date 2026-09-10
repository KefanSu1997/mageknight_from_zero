using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[Category("AgentA")]
public class AgentASceneAutomationTests
{
    private SceneSetup[] originalSceneSetup;

    [SetUp]
    public void SetUp()
    {
        originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
    }

    [TearDown]
    public void TearDown()
    {
        if (originalSceneSetup != null && originalSceneSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
        }
        else
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        }
    }

    [Test]
    public void AgentAScene_HasRotatingCube()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/AgentA_TestScene.unity", OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid() && scene.isLoaded, "场景加载失败");

        var rotateCube = Object.FindFirstObjectByType<RotateCube>();
        Assert.IsNotNull(rotateCube, "未找到 RotateCube 组件");

        var meshRenderer = rotateCube.GetComponent<MeshRenderer>();
        Assert.IsNotNull(meshRenderer, "旋转立方体缺少 MeshRenderer");
    }
}
