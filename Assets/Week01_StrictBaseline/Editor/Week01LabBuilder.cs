using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GDEV50024.Week01.EditorTools
{
    /// <summary>
    /// Builds the Week 01 movement lab scene from code, so the layout is easy to review and can be
    /// regenerated. Students never need this: the saved scene already contains everything.
    /// Menu: Tools > GDEV50024 > Week 01 > Rebuild Movement Lab Scene.
    /// </summary>
    public static class Week01LabBuilder
    {
        public const string ScenePath = "Assets/Week01_StrictBaseline/Scenes/Week01_StrictBaseline.unity";
        const string ControlsPath = "Assets/Week01_StrictBaseline/Input/Week01Controls.inputactions";
        const string FrictionlessPath = "Assets/Week01_StrictBaseline/Physics/PlayerFrictionless.physicsMaterial2D";
        const string SquareSpritePath = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png";
        const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        // Pastel palette.
        static readonly Color Background = Hex("#F6F1EA"); // warm cream
        static readonly Color Solid = Hex("#7686B8");      // dusty periwinkle: every solid surface
        static readonly Color PlayerFill = Hex("#F28B82"); // pastel coral
        static readonly Color Ink = Hex("#2F2A44");        // deep plum: text, player outline and eye
        static readonly Color InkSoft = Hex("#554F72");
        static readonly Color PanelColor = Hex("#FFFDF9");  // opaque, so nothing shows through it

        // Layout, in world units. The floor surface is y = 0. The character is 0.8 wide and 1 tall.
        static readonly Vector2 SpawnPosition = new Vector2(1.5f, 0.52f);
        const float LabMinX = -3f, LabMaxX = 61f, LabTopY = 10f;
        const float CameraY = 4.5f, CameraSize = 6f;
        const float LabelY = 7.4f; // above the highest point the character can reach (about 6.6)

        [MenuItem("Tools/GDEV50024/Week 01/Rebuild Movement Lab Scene")]
        static void RebuildFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog("Rebuild Week 01 Movement Lab",
                "This replaces " + ScenePath + " with a freshly generated copy. Manual edits to that scene will be lost.",
                "Rebuild", "Cancel");
            if (!confirmed || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Debug.Log(Build());
        }

        /// <summary>Generates and saves the lab scene. Returns a short summary.</summary>
        public static string Build()
        {
            Sprite square = Load<Sprite>(SquareSpritePath);
            Material unlit = Load<Material>(UnlitSpriteMaterialPath);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            PhysicsMaterial2D frictionless = EnsureFrictionlessMaterial();

            // Check the input actions before touching any scene, so a missing action fails early.
            ResolveActions(out _, out _, out _);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Opening a new scene unloads unused assets, so the action references are loaded again here.
            ResolveActions(out InputActionReference move, out InputActionReference jump, out InputActionReference restartAction);

            // ---------------------------------------------------------------- Camera
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(SpawnPosition.x, CameraY, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 50f;
            cameraObject.AddComponent<AudioListener>();

            // ---------------------------------------------------------------- Level (every solid surface)
            Transform level = new GameObject("Level").transform;

            // Lab boundary.
            Block(level, "Floor", square, unlit, LabMinX, LabMaxX, -2f, 0f);
            Block(level, "WallLeft", square, unlit, LabMinX, LabMinX + 1f, 0f, LabTopY);
            Block(level, "Top", square, unlit, LabMinX, LabMaxX, LabTopY, LabTopY + 1f);

            // 1. Flat ground: open floor from x = -2 to x = 9.5.

            // 2. Platform edge: a step up to a raised ledge, a 5-unit gap, then a second ledge.
            //    Neither ledge can be reached from the floor with one jump, so a missed gap means
            //    walking back under Ledge A to the step.
            Block(level, "StepUp", square, unlit, 9.5f, 11f, 0f, 1.6f);
            Block(level, "LedgeA", square, unlit, 12.5f, 17f, 2.7f, 3.2f);
            Block(level, "LedgeB", square, unlit, 22f, 25f, 0f, 3.2f);

            // 3. Landing area: three low posts. Clearing one post lands the character shortly
            //    before the next one.
            Block(level, "Post1", square, unlit, 34f, 34.5f, 0f, 1f);
            Block(level, "Post2", square, unlit, 39.3f, 39.8f, 0f, 1f);
            Block(level, "Post3", square, unlit, 44.6f, 45.1f, 0f, 1f);

            // 4. Wall and low ceiling: a passage 1.8 units high, then open space up to a tall wall.
            Block(level, "LowCeiling", square, unlit, 50f, 54f, 1.8f, LabTopY);
            Block(level, "WallRight", square, unlit, LabMaxX - 1f, LabMaxX, 0f, LabTopY);

            // ---------------------------------------------------------------- Player
            var player = new GameObject("Player");
            player.transform.position = SpawnPosition;

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 1f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BoxCollider2D box = player.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1f);
            box.offset = Vector2.zero;
            box.edgeRadius = 0f;
            box.sharedMaterial = frictionless;

            // The outline is exactly the size of the collider, so what you see is what collides.
            Visual(player.transform, "Outline", square, unlit, Ink, Vector2.zero, new Vector2(0.8f, 1f), 10);
            Visual(player.transform, "Fill", square, unlit, PlayerFill, Vector2.zero, new Vector2(0.72f, 0.92f), 11);
            Transform eye = Visual(player.transform, "Eye", square, unlit, Ink, new Vector2(0.2f, 0.2f), new Vector2(0.12f, 0.12f), 12).transform;

            var controller = player.AddComponent<StrictPlatformerController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("moveAction").objectReferenceValue = move;
            controllerData.FindProperty("jumpAction").objectReferenceValue = jump;
            controllerData.FindProperty("eye").objectReferenceValue = eye;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- Lab rules (restart)
            var lab = new GameObject("Lab");
            var spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.SetParent(lab.transform, false);
            spawnPoint.position = SpawnPosition;

            var restart = lab.AddComponent<LabRestart>();
            var restartData = new SerializedObject(restart);
            restartData.FindProperty("restartAction").objectReferenceValue = restartAction;
            restartData.FindProperty("player").objectReferenceValue = controller;
            restartData.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            restartData.ApplyModifiedPropertiesWithoutUndo();

            var follow = cameraObject.AddComponent<LabCameraFollow>();
            var followData = new SerializedObject(follow);
            followData.FindProperty("target").objectReferenceValue = player.transform;
            followData.FindProperty("labMinX").floatValue = LabMinX;
            followData.FindProperty("labMaxX").floatValue = LabMaxX;
            followData.ApplyModifiedPropertiesWithoutUndo();

            // ---------------------------------------------------------------- Area labels (situations only)
            Transform labels = new GameObject("Labels").transform;
            AreaLabel(labels, camera, font, "Label1_FlatGround", 6f, 7f,
                "1 · Flat ground", "Open floor: walk, stop, turn around, jump.");
            AreaLabel(labels, camera, font, "Label2_PlatformEdge", 19.5f, 8f,
                "2 · Platform edge", "A raised ledge, a gap, and a second ledge.");
            AreaLabel(labels, camera, font, "Label3_LandingArea", 39.5f, 8f,
                "3 · Landing area", "Low posts with short landings in between.");
            AreaLabel(labels, camera, font, "Label4_WallAndLowCeiling", 57f, 6f,
                "4 · Wall and low ceiling", "A low passage, then a tall wall.");

            // ---------------------------------------------------------------- Introductory panel
            BuildIntroPanel(font, panelSprite);

            SceneManager.SetActiveScene(scene);
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException("Could not save " + ScenePath);

            // Guard against silently saving unassigned input: every action field must point at the asset.
            foreach (var (component, field) in new (Object, string)[]
                     { (controller, "moveAction"), (controller, "jumpAction"), (restart, "restartAction") })
            {
                Object value = new SerializedObject(component).FindProperty(field).objectReferenceValue;
                if (value == null || AssetDatabase.GetAssetPath(value) != ControlsPath)
                    throw new System.InvalidOperationException($"{component.GetType().Name}.{field} was not saved correctly.");
            }

            return "Week 01 movement lab saved to " + ScenePath;
        }

        // ------------------------------------------------------------------------------------------

        static GameObject Block(Transform parent, string name, Sprite sprite, Material material,
                                float xMin, float xMax, float yMin, float yMax)
        {
            var block = new GameObject(name);
            block.transform.SetParent(parent, false);
            block.transform.position = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
            block.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);

            var renderer = block.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = Solid;

            var collider = block.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one; // the sprite is 1 x 1 unit; the transform scale sizes it
            collider.offset = Vector2.zero;
            return block;
        }

        static GameObject Visual(Transform parent, string name, Sprite sprite, Material material, Color color,
                                 Vector2 localPosition, Vector2 size, int sortingOrder)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        static void AreaLabel(Transform parent, Camera camera, Font font, string name,
                              float x, float width, string title, string description)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            label.transform.SetParent(parent, false);

            var canvas = label.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            label.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;

            // 100 canvas units = 1 world unit.
            var rect = (RectTransform)label.transform;
            rect.sizeDelta = new Vector2(width * 100f, 100f);
            rect.localScale = Vector3.one * 0.01f;
            rect.position = new Vector3(x, LabelY, 0f);

            var layout = label.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;

            AddText(label.transform, font, "Title", title, 40, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            AddText(label.transform, font, "Description", description, 28, FontStyle.Normal, InkSoft, TextAnchor.MiddleCenter);
        }

        static void BuildIntroPanel(Font font, Sprite panelSprite)
        {
            var canvasObject = new GameObject("IntroCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("IntroPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);

            var image = panel.GetComponent<Image>();
            image.sprite = panelSprite;
            image.type = Image.Type.Sliced;
            image.color = PanelColor;
            image.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 20, 22);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel.transform, font, "Title", "Week 01 · Movement Lab", 30, FontStyle.Bold, Ink, TextAnchor.UpperLeft);
            ControlRow(panel.transform, font, "Move", "A / D   or   Left / Right arrows");
            ControlRow(panel.transform, font, "Jump", "Space,  W   or   Up arrow");
            ControlRow(panel.transform, font, "Restart", "R");

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(panel.transform, false);
            spacer.GetComponent<LayoutElement>().minHeight = 6f;

            AddText(panel.transform, font, "PromptHeading", "As you play, notice:", 21, FontStyle.Italic, InkSoft, TextAnchor.UpperLeft);
            AddText(panel.transform, font, "Prompt",
                "What were you trying to do?\nWhat happened?\nWhich rule produced that result?",
                22, FontStyle.Normal, Ink, TextAnchor.UpperLeft);
        }

        static void ControlRow(Transform parent, Font font, string control, string keys)
        {
            var row = new GameObject("Row_" + control, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Text name = AddText(row.transform, font, "Control", control, 23, FontStyle.Bold, Ink, TextAnchor.UpperLeft);
            name.gameObject.AddComponent<LayoutElement>().minWidth = 100f;
            AddText(row.transform, font, "Keys", keys, 23, FontStyle.Normal, Ink, TextAnchor.UpperLeft);
        }

        static Text AddText(Transform parent, Font font, string name, string content, int size,
                            FontStyle style, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static PhysicsMaterial2D EnsureFrictionlessMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            if (material == null)
            {
                EnsureFolder(Path.GetDirectoryName(FrictionlessPath).Replace('\\', '/'));
                material = new PhysicsMaterial2D("PlayerFrictionless");
                AssetDatabase.CreateAsset(material, FrictionlessPath);
            }

            // No friction, so pushing into a wall in mid-air never slows the fall.
            material.friction = 0f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine2D.Minimum;
            material.bounceCombine = PhysicsMaterialCombine2D.Minimum;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void ResolveActions(out InputActionReference move, out InputActionReference jump, out InputActionReference restart)
        {
            InputActionReference[] actions = AssetDatabase.LoadAllAssetsAtPath(ControlsPath).OfType<InputActionReference>().ToArray();
            move = FindAction(actions, "Move");
            jump = FindAction(actions, "Jump");
            restart = FindAction(actions, "Restart");
        }

        static InputActionReference FindAction(InputActionReference[] actions, string actionName)
        {
            // Only the persisted sub-asset references can be saved into a scene; the importer also
            // keeps hidden in-memory copies, which are skipped here.
            InputActionReference reference = actions.FirstOrDefault(a =>
                a != null && AssetDatabase.IsSubAsset(a) && (a.hideFlags & HideFlags.HideInHierarchy) == 0 &&
                a.action != null && a.action.name == actionName);
            if (reference == null)
                throw new System.InvalidOperationException($"Action '{actionName}' not found in {ControlsPath}");
            return reference;
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException($"Missing {typeof(T).Name} at {path}");
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color color);
            return color;
        }
    }
}
