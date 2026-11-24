using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using EscapeFromWork.Gameplay;

namespace EscapeFromWork.Tests
{
    public class WhiteboxSmokeTests
    {
        [UnityTest]
        public IEnumerator Whitebox_PlayerSettlesOnFloor()
        {
            SceneManager.LoadScene("Whitebox");
            yield return null;
            yield return new WaitForSeconds(1.2f);

            GameObject player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null, "Player object missing from Whitebox");

            CharacterController controller = player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null, "CharacterController missing");

            FirstPersonController fps = player.GetComponent<FirstPersonController>();
            Assert.That(fps, Is.Not.Null, "FirstPersonController missing");

            Transform cameraRig = player.transform.Find("PlayerCamera");
            Assert.That(cameraRig, Is.Not.Null, "PlayerCamera missing");
            Assert.That(cameraRig.GetComponent<Camera>(), Is.Not.Null, "Camera missing on rig");

            Debug.Log(string.Format("[Smoke] y={0:0.000} grounded={1} velocity={2} ticks={3} cursorLocked={4} enabled={5} dt={6:0.0000}",
                player.transform.position.y, controller.isGrounded, controller.velocity, fps.Ticks, fps.CursorLocked, fps.enabled, Time.deltaTime));

            Assert.That(player.transform.position.y, Is.InRange(-1.0f, 1.0f), "Player did not settle on the floor");
            Assert.That(fps.IsGrounded, Is.True, "Player should be grounded after settling");
        }

        [UnityTest]
        public IEnumerator Whitebox_HasExpectedArenaObjects()
        {
            SceneManager.LoadScene("Whitebox");
            yield return null;

            Assert.That(GameObject.Find("Floor"), Is.Not.Null);
            Assert.That(GameObject.Find("CrouchBeam"), Is.Not.Null);
            Assert.That(GameObject.Find("Landing"), Is.Not.Null);
            Assert.That(GameObject.Find("Pillar_A"), Is.Not.Null);
            Assert.That(GameObject.Find("Slab"), Is.Not.Null, "Structural slab missing");
            Assert.That(GameObject.Find("Ceiling_W"), Is.Not.Null, "Suspended ceiling missing");
            Assert.That(GameObject.Find("LightPanel_0_0"), Is.Not.Null, "Ceiling light panels missing");
        }
    }
}
