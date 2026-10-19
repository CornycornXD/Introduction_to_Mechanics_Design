#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GDEV50024.Week01.Tests
{
    /// <summary>
    /// Plays the real Week 01 scene with a virtual keyboard. Game time advances by exactly one
    /// physics step per rendered frame, so every press can be placed on a precise frame.
    ///
    /// Timing model (Unity player loop): FixedUpdate + physics step, then input is processed,
    /// then Update. A key event queued by the test is therefore judged in the NEXT frame,
    /// after that frame's physics step.
    /// </summary>
    public class StrictBaselineTests
    {
        const string ScenePath = "Assets/Week01_StrictBaseline/Scenes/Week01_StrictBaseline.unity";
        const float HalfWidth = 0.4f;
        const float HalfHeight = 0.5f;

        Keyboard keyboard;
        readonly HashSet<Key> held = new HashSet<Key>();
        InputSettings originalSettings;
        InputSettings testSettings;
        FixedStepCounter stepCounter;

        StrictPlatformerController player;
        Rigidbody2D body;
        float dt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dt = Time.fixedDeltaTime;
            Time.captureDeltaTime = dt; // one physics step per frame

            // Let the virtual keyboard reach the game even when the Game view is not focused.
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testSettings;

            keyboard = InputSystem.AddDevice<Keyboard>("Week01TestKeyboard");
            held.Clear();

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            player = Object.FindAnyObjectByType<StrictPlatformerController>();
            Assert.IsNotNull(player, "The scene must contain the StrictPlatformerController.");
            body = player.GetComponent<Rigidbody2D>();
            stepCounter = new GameObject("FixedStepCounter").AddComponent<FixedStepCounter>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings;
            if (testSettings != null)
                Object.Destroy(testSettings);
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        // ------------------------------------------------------------------------------ timing

        [UnityTest]
        public IEnumerator Timing_ExactlyOnePhysicsStepPerFrame()
        {
            yield return null;
            int before = stepCounter.Count;
            yield return Frames(20);
            Assert.AreEqual(20, stepCounter.Count - before, "The frame-exact tests below rely on one physics step per frame.");
        }

        // ------------------------------------------------------------------------------ movement

        [UnityTest]
        public IEnumerator Move_StopsOnRelease_AndReversesInstantly()
        {
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(3f, floor);

            Press(Key.D);
            yield return Frames(2);
            float speed = body.linearVelocity.x;
            Assert.Greater(speed, 0f, "Holding Right moves right.");

            Release(Key.D);
            yield return Frames(2);
            Assert.AreEqual(0f, body.linearVelocity.x, 1e-4f, "Releasing stops at once.");
            float stoppedX = body.position.x;
            yield return Frames(10);
            Assert.AreEqual(stoppedX, body.position.x, 1e-4f, "No sliding after release.");

            Press(Key.A);
            yield return Frames(2);
            Assert.AreEqual(-speed, body.linearVelocity.x, 1e-4f, "Holding Left moves left at the same speed.");
            Assert.Less(GameObject.Find("Player/Eye").transform.localPosition.x, 0f, "The eye faces left.");

            Release(Key.A);
            Press(Key.D);
            yield return Frames(2);
            Assert.AreEqual(speed, body.linearVelocity.x, 1e-4f, "Direction reverses immediately.");
            Assert.Greater(GameObject.Find("Player/Eye").transform.localPosition.x, 0f, "The eye faces right.");
            Assert.IsTrue(player.IsGrounded(), "Walking on flat ground stays grounded.");
        }

        // ------------------------------------------------------------------------------ jump

        [UnityTest]
        public IEnumerator GroundedJump_IsAcceptedEveryTime_WithAFixedHeight()
        {
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(3f, floor);

            for (int i = 0; i < 5; i++)
            {
                int jumps = player.JumpsPerformed;
                Tap(Key.Space); // down and up inside a single frame
                yield return null;
                Assert.AreEqual(jumps + 1, player.JumpsPerformed, $"Grounded press #{i + 1} must jump.");
                Assert.AreEqual(player.JumpSpeed, body.linearVelocity.y, 1e-3f, "Every jump has the same strength.");

                float peakBottom = body.position.y - HalfHeight;
                float speedBeforeLanding = body.linearVelocity.y;
                for (int f = 0; f < 120 && !player.IsGrounded(); f++)
                {
                    speedBeforeLanding = body.linearVelocity.y;
                    yield return null;
                    peakBottom = Mathf.Max(peakBottom, body.position.y - HalfHeight);
                }
                Assert.IsTrue(player.IsGrounded(), "The character lands again.");
                Assert.Less(speedBeforeLanding, -5f,
                    "Ground is detected on the frame of touchdown: one frame earlier the body was still falling fast.");
                Assert.That(peakBottom - floor, Is.InRange(2.2f, 2.6f), "Peak height of a jump.");
                Debug.Log($"[Week01Evidence] Grounded jump #{i + 1}: accepted, peak {peakBottom - floor:F3} units, resting gap {body.position.y - HalfHeight - floor:F4}.");
                // The loop ends on the first grounded frame, so the next press is judged one frame after landing.
            }
            Assert.AreEqual(0, player.JumpPressesIgnored, "No grounded press may be dropped.");
        }

        [UnityTest]
        public IEnumerator SecondPressInTheAir_DoesNotJump()
        {
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(3f, floor);

            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed);

            yield return Frames(8); // still rising
            float risingSpeed = body.linearVelocity.y;
            Assert.Greater(risingSpeed, 0f);
            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed, "A press while rising must not jump.");
            Assert.AreEqual(1, player.JumpPressesIgnored);
            Assert.Less(body.linearVelocity.y, risingSpeed, "The rise was not refreshed.");

            yield return WaitUntil(() => body.linearVelocity.y <= 0f, 60, "the top of the jump");
            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed, "A press at the top of the jump must not jump.");
            Assert.AreEqual(2, player.JumpPressesIgnored);

            yield return WaitUntil(() => player.IsGrounded(), 60, "landing");
            yield return Frames(30);
            Assert.AreEqual(1, player.JumpsPerformed, "Ignored presses must not fire later.");
        }

        [UnityTest]
        public IEnumerator HoldingJumpThroughLanding_DoesNotJumpAgain()
        {
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(3f, floor);

            Press(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed);

            yield return WaitUntil(() => player.IsGrounded(), 120, "landing while Jump is held");
            yield return Frames(30);
            Assert.AreEqual(1, player.JumpsPerformed, "Holding Jump through landing must not jump again.");
            Assert.IsTrue(player.IsGrounded());
            Assert.AreEqual(0f, body.linearVelocity.y, 0.05f);

            Release(Key.Space);
            yield return null;
            Press(Key.Space);
            yield return null;
            Assert.AreEqual(2, player.JumpsPerformed, "A new press after releasing jumps normally.");
        }

        // ------------------------------------------------------------------------------ edge

        [UnityTest]
        public IEnumerator JumpOnTheLastFrameOverTheEdge_IsAccepted()
        {
            Rect ledge = Solid("LedgeA");
            yield return PlaceOnGround(ledge.xMax - 1.5f, ledge.yMax);
            Press(Key.D);

            // Stop when the NEXT physics step is the last one that keeps part of the body over the ledge.
            yield return WalkUntil(() =>
            {
                float nextLeft = body.position.x - HalfWidth + body.linearVelocity.x * dt;
                float stepAfter = nextLeft + body.linearVelocity.x * dt;
                return body.linearVelocity.x > 0f && nextLeft <= ledge.xMax - 0.02f && stepAfter > ledge.xMax;
            }, ledge);

            Tap(Key.Space);
            yield return null;
            Assert.Less(body.position.x - HalfWidth, ledge.xMax, "Judged while still over the ledge.");
            Assert.AreEqual(1, player.JumpsPerformed, "The last grounded frame before the edge must still jump.");
            Debug.Log($"[Week01Evidence] Edge: press judged with {ledge.xMax - (body.position.x - HalfWidth):F3} units of the body still over the ledge; accepted.");
        }

        [UnityTest]
        public IEnumerator JumpJustAfterLeavingTheEdge_IsRejected()
        {
            Rect ledge = Solid("LedgeA");
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(ledge.xMax - 1.5f, ledge.yMax);
            Press(Key.D);

            // Stop when the NEXT physics step carries the whole body past the edge.
            yield return WalkUntil(() =>
            {
                float nextLeft = body.position.x - HalfWidth + body.linearVelocity.x * dt;
                return body.linearVelocity.x > 0f && nextLeft > ledge.xMax + 0.02f;
            }, ledge);

            Tap(Key.Space);
            yield return null;
            float left = body.position.x - HalfWidth;
            Assert.Greater(left, ledge.xMax, "Judged in the first frame after leaving the ledge.");
            Assert.Less(left, ledge.xMax + 0.2f, "...only one physics step after leaving.");
            Assert.AreEqual(ledge.yMax, body.position.y - HalfHeight, 0.05f, "...before the fall has visibly begun.");
            Assert.AreEqual(0, player.JumpsPerformed, "A press after leaving the edge must not jump.");
            Assert.AreEqual(1, player.JumpPressesIgnored);
            Debug.Log($"[Week01Evidence] Edge: press judged with the body {left - ledge.xMax:F3} units past the edge, " +
                      $"feet still {body.position.y - HalfHeight - ledge.yMax:F3} above the ledge top (not yet falling); rejected.");

            Release(Key.D);
            yield return WaitUntil(() => player.IsGrounded(), 120, "landing on the floor below");
            Assert.AreEqual(floor, body.position.y - HalfHeight, 0.05f);
            yield return Frames(30);
            Assert.AreEqual(0, player.JumpsPerformed, "The rejected press must not fire on landing.");
        }

        // ------------------------------------------------------------------------------ landing

        [UnityTest]
        public IEnumerator TapShortlyBeforeLanding_IsRejectedAndNotQueued()
        {
            yield return PressShortlyBeforeLanding(holdThroughLanding: false);
        }

        [UnityTest]
        public IEnumerator PressAndHoldShortlyBeforeLanding_IsRejectedAndNotQueued()
        {
            yield return PressShortlyBeforeLanding(holdThroughLanding: true);
        }

        IEnumerator PressShortlyBeforeLanding(bool holdThroughLanding)
        {
            float floor = Solid("Floor").yMax;
            player.ResetTo(new Vector2(4f, floor + HalfHeight + 2f));
            yield return null;

            // Press when the next physics step still leaves the body in the air,
            // but landing is at most two steps away.
            float previousVy = body.linearVelocity.y;
            bool pressed = false;
            for (int f = 0; f < 120 && !pressed; f++)
            {
                yield return null;
                float vy = body.linearVelocity.y;
                float accel = (vy - previousVy) / dt;
                previousVy = vy;
                if (vy >= 0f) continue;

                float bottom = body.position.y - HalfHeight;
                float nextBottom = bottom + (vy + accel * dt) * dt;
                if (nextBottom > floor + 0.05f && nextBottom < floor + 0.35f)
                {
                    Press(Key.Space);
                    if (!holdThroughLanding) Release(Key.Space);
                    pressed = true;
                }
            }
            Assert.IsTrue(pressed, "Could not find the frame just before landing.");

            yield return null;
            float heightWhenJudged = body.position.y - HalfHeight - floor;
            Assert.Greater(heightWhenJudged, 0.05f, "The press was judged while still in the air.");
            Assert.AreEqual(0, player.JumpsPerformed, "A press just before landing must not jump.");
            Assert.AreEqual(1, player.JumpPressesIgnored);

            int framesToLand = 0;
            while (!player.IsGrounded() && framesToLand < 10)
            {
                yield return null;
                framesToLand++;
            }
            Assert.IsTrue(player.IsGrounded(), "The character lands.");
            Assert.LessOrEqual(framesToLand, 2, $"The press came shortly before landing ({heightWhenJudged:F2} units above the floor).");
            Debug.Log($"[Week01Evidence] Before landing (hold={holdThroughLanding}): press judged {heightWhenJudged:F3} units above the floor, " +
                      $"{framesToLand} frame(s) = {framesToLand * dt * 1000f:F0} ms before touching down; rejected.");

            yield return Frames(40);
            Assert.AreEqual(0, player.JumpsPerformed, "The rejected press must not execute on landing.");
            Assert.IsTrue(player.IsGrounded());
        }

        // ------------------------------------------------------------------------------ walls and ceilings

        [UnityTest]
        public IEnumerator WallContact_IsNotGround_AndDoesNotSlowTheFall()
        {
            Rect wall = Solid("WallRight");
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(wall.xMin - 1.5f, floor);

            Press(Key.D);
            yield return WaitUntil(() => body.position.x + HalfWidth >= wall.xMin - 0.03f, 60, "reaching the wall");
            yield return Frames(5);
            Assert.LessOrEqual(body.position.x + HalfWidth, wall.xMin + 0.01f, "No pushing into the wall.");
            Assert.IsTrue(player.IsGrounded(), "Standing on the floor beside a wall: the floor counts.");

            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed);

            // Rise and fall along the wall while still holding Right, pressing Jump every third frame.
            float previousVy = body.linearVelocity.y;
            float? gravityStep = null;
            int presses = 0, airborneFrames = 0;
            for (int f = 0; f < 120; f++)
            {
                bool nearlyLanded = body.linearVelocity.y < 0f && body.position.y - HalfHeight <= floor + 0.3f;
                if (nearlyLanded) break;
                if (f % 3 == 1)
                {
                    Tap(Key.Space);
                    presses++;
                }
                yield return null;
                airborneFrames++;

                Assert.IsFalse(player.IsGrounded(), "Touching a wall in mid-air is not ground.");
                Assert.LessOrEqual(body.position.x + HalfWidth, wall.xMin + 0.01f, "No clipping into the wall.");

                float vy = body.linearVelocity.y;
                float step = vy - previousVy;
                previousVy = vy;
                if (gravityStep == null) gravityStep = step;
                else Assert.AreEqual(gravityStep.Value, step, 0.01f, "Pushing into the wall must not slow the fall (no friction).");
            }
            Assert.Greater(airborneFrames, 25, "The in-air checks covered most of the jump.");
            Release(Key.D);
            yield return WaitUntil(() => player.IsGrounded(), 30, "landing beside the wall");
            Debug.Log($"[Week01Evidence] Wall: {airborneFrames} airborne frames pressed against the wall, " +
                      $"{presses} Jump presses ignored, gravity step {gravityStep:F3} per frame, gap to wall {wall.xMin - (body.position.x + HalfWidth):F4}.");
            yield return Frames(20);
            Assert.AreEqual(1, player.JumpsPerformed, "Presses against the wall in mid-air never jump.");
            Assert.AreEqual(presses, player.JumpPressesIgnored);

            // The rule also holds with zero vertical speed: a wall beside the feet is never ground.
            player.ResetTo(new Vector2(wall.xMin - HalfWidth - 0.005f, floor + 3f));
            Assert.IsFalse(player.IsGrounded(), "A wall beside the character is not ground, even at zero vertical speed.");
            player.ResetTo(new Vector2(wall.xMin - HalfWidth + 0.005f, floor + 3f));
            Assert.IsFalse(player.IsGrounded(), "...even when overlapping the wall slightly.");
        }

        [UnityTest]
        public IEnumerator LowCeiling_BlocksUpwardTravel_WithoutSticking()
        {
            Rect ceiling = Solid("LowCeiling");
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(ceiling.center.x - 1f, floor);

            Press(Key.D);
            yield return null;
            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed);

            float highestTop = 0f;
            int bumpFrame = -1, fallFrame = -1, frame = 0;
            float previousX = body.position.x;
            while (!player.IsGrounded() && frame < 60)
            {
                yield return null;
                frame++;
                float top = body.position.y + HalfHeight;
                highestTop = Mathf.Max(highestTop, top);
                if (bumpFrame < 0 && top >= ceiling.yMin - 0.03f)
                {
                    bumpFrame = frame;
                    Assert.IsFalse(player.IsGrounded(), "Touching the ceiling is not ground.");
                }
                if (fallFrame < 0 && body.linearVelocity.y < 0f) fallFrame = frame;
                Assert.Greater(body.position.x, previousX, "Horizontal movement continues under the ceiling.");
                previousX = body.position.x;
            }

            Assert.GreaterOrEqual(bumpFrame, 0, "The jump reaches the low ceiling.");
            Assert.LessOrEqual(highestTop, ceiling.yMin + 0.01f, "The ceiling blocks upward travel.");
            Assert.LessOrEqual(fallFrame - bumpFrame, 1, "Falling starts right after the bump (no sticking).");
            Assert.IsTrue(player.IsGrounded(), "The character lands again.");
            Assert.Less(frame, 30, "The bump cut the jump short.");
            Debug.Log($"[Week01Evidence] Ceiling: highest head {highestTop:F3} vs ceiling {ceiling.yMin:F3}; bump at frame {bumpFrame}, " +
                      $"falling from frame {fallFrame}, landed at frame {frame} (a full jump takes about 40).");

            yield return WaitUntil(() => body.position.x - HalfWidth > ceiling.xMax, 60, "walking out of the low passage");
            Release(Key.D);
        }

        [UnityTest]
        public IEnumerator Post_StopsTheCharacterAtItsFace_AndCanBeJumped()
        {
            Rect post = Solid("Post1");
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(post.xMin - 2f, floor);

            Press(Key.D);
            yield return WaitUntil(() => body.position.x + HalfWidth >= post.xMin - 0.03f, 60, "reaching the post");
            yield return Frames(5);
            Assert.LessOrEqual(body.position.x + HalfWidth, post.xMin + 0.01f, "No clipping into the post.");
            Assert.IsTrue(player.IsGrounded());

            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(1, player.JumpsPerformed);
            yield return WaitUntil(() => body.position.x - HalfWidth > post.xMax, 60, "clearing the post");
            Release(Key.D);
        }

        [UnityTest]
        public IEnumerator FastFall_LandsOnTheThinLedge()
        {
            Rect ledge = Solid("LedgeA");
            player.ResetTo(new Vector2(ledge.center.x, 9f));
            yield return WaitUntil(() => player.IsGrounded(), 120, "landing on Ledge A");
            Assert.AreEqual(ledge.yMax, body.position.y - HalfHeight, 0.05f, "Landed on top of the 0.5-unit ledge, not through it.");
        }

        // ------------------------------------------------------------------------------ restart

        [UnityTest]
        public IEnumerator Restart_ReturnsToAUsableStart()
        {
            Vector2 spawn = GameObject.Find("Lab/SpawnPoint").transform.position;
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(18f, floor);

            Press(Key.D);
            yield return null;
            Tap(Key.Space);
            yield return Frames(6); // moving and in the air
            Assert.IsFalse(player.IsGrounded());

            Release(Key.D);
            Tap(Key.R);
            yield return null;
            Assert.AreEqual(0f, Vector2.Distance(spawn, body.position), 1e-3f, "Restart moves the character to the spawn point.");
            Assert.AreEqual(Vector2.zero, body.linearVelocity, "Restart clears all motion.");

            yield return WaitUntil(() => player.IsGrounded(), 30, "settling at the start");
            int jumps = player.JumpsPerformed;
            Tap(Key.Space);
            yield return null;
            Assert.AreEqual(jumps + 1, player.JumpsPerformed, "Jump works after a restart.");
            yield return WaitUntil(() => player.IsGrounded(), 120, "landing");

            Press(Key.D);
            yield return Frames(3);
            Assert.Greater(body.linearVelocity.x, 0f, "Movement works after a restart.");
        }

        [UnityTest]
        public IEnumerator HoldingJumpDuringRestart_DoesNotJumpAfterwards()
        {
            float floor = Solid("Floor").yMax;
            yield return PlaceOnGround(6f, floor);

            Press(Key.Space);
            yield return Frames(5);
            int jumps = player.JumpsPerformed;
            Tap(Key.R);
            yield return null;
            yield return WaitUntil(() => player.IsGrounded(), 30, "settling at the start");
            yield return Frames(30);
            Assert.AreEqual(jumps, player.JumpsPerformed, "A Jump key held through a restart does not jump.");
        }

        // ------------------------------------------------------------------------------ scene

        [UnityTest]
        public IEnumerator Scene_IsWiredAndLabelledNeutrally()
        {
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<StrictPlatformerController>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(Object.FindAnyObjectByType<LabRestart>());
            Assert.IsNotNull(Camera.main.GetComponent<LabCameraFollow>());

            List<InputAction> enabled = InputSystem.ListEnabledActions();
            InputAction move = enabled.Single(a => a.name == "Move" && a.actionMap.name == "Lab");
            InputAction jump = enabled.Single(a => a.name == "Jump" && a.actionMap.name == "Lab");
            InputAction restart = enabled.Single(a => a.name == "Restart" && a.actionMap.name == "Lab");
            CollectionAssert.IsSubsetOf(new[] { "<Keyboard>/a", "<Keyboard>/d", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" },
                move.bindings.Select(b => b.path).ToArray());
            CollectionAssert.AreEquivalent(new[] { "<Keyboard>/space", "<Keyboard>/w", "<Keyboard>/upArrow" },
                jump.bindings.Select(b => b.path).ToArray());
            CollectionAssert.AreEquivalent(new[] { "<Keyboard>/r" }, restart.bindings.Select(b => b.path).ToArray());

            string[] texts = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text).ToArray();
            string all = string.Join("\n", texts);
            foreach (string expected in new[] { "1 · Flat ground", "2 · Platform edge", "3 · Landing area",
                                                "4 · Wall and low ceiling", "Restart", "R", "Space",
                                                "What were you trying to do?", "What happened?", "Which rule produced that result?" })
                StringAssert.Contains(expected, all);
            foreach (string loaded in new[] { "coyote", "buffer", "broken", "bug", "forgiv", "should" })
                StringAssert.DoesNotContain(loaded, all.ToLowerInvariant(), "On-screen text stays neutral.");
        }

        // ------------------------------------------------------------------------------ helpers

        void Press(Key key) { held.Add(key); SendKeyboardState(); }
        void Release(Key key) { held.Remove(key); SendKeyboardState(); }
        void Tap(Key key) { Press(key); Release(key); }
        void SendKeyboardState() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray()));

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, int maxFrames, string description)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.IsTrue(condition(), $"Timed out after {maxFrames} frames waiting for {description}.");
        }

        IEnumerator PlaceOnGround(float x, float groundTop)
        {
            player.ResetTo(new Vector2(x, groundTop + HalfHeight + 0.01f));
            yield return WaitUntil(() => player.IsGrounded(), 30, "the character to settle on the ground");
        }

        IEnumerator WalkUntil(System.Func<bool> condition, Rect ledge)
        {
            for (int i = 0; i < 100; i++)
            {
                yield return null;
                Assert.LessOrEqual(body.position.x - HalfWidth, ledge.xMax, "Walked off the ledge before the test could press.");
                if (condition()) yield break;
            }
            Assert.Fail("Never reached the ledge edge.");
        }

        static Rect Solid(string name)
        {
            GameObject solid = GameObject.Find("Level/" + name);
            Assert.IsNotNull(solid, $"Missing Level/{name}");
            Vector2 size = solid.transform.lossyScale;
            Vector2 centre = solid.transform.position;
            return new Rect(centre - size * 0.5f, size);
        }
    }

    /// <summary>Counts physics steps so the tests can confirm their timing assumption.</summary>
    public class FixedStepCounter : MonoBehaviour
    {
        public int Count;
        void FixedUpdate() => Count++;
    }
}
#endif
