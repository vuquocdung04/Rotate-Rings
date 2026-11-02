using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Tools > Rotate Rings > Level Editor (Odin window).
    /// Place presets from the ShapeLibrary, connect them with links / arms / clamps by clicking in the Scene view,
    /// apply arm layouts in one click, check / regenerate solvability, and save/load levels as JSON.
    /// Everything is built through LevelBuilder, exactly like at runtime.
    /// </summary>
    public class LevelEditorWindow : OdinEditorWindow
    {
        enum Mode { Select, Link, Arm, Clamp, Delete }

        const string LevelsDir = "Assets/01_RotateRings/Levels";
        const string RootName = "Level";
        const float TileW = 78f, TileH = 74f;
        const string Main = "Main";

        [MenuItem("Tools/Rotate Rings/Level Editor")]
        static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(320f, 400f);
        }

        // ---------- Assets ----------

        [FoldoutGroup("Assets", Expanded = false), Required, SerializeField]
        PieceStyle style;

        [FoldoutGroup("Assets"), Required, SerializeField]
        ShapeLibrary library;

        [FoldoutGroup("Assets"), Button("Rebuild Assets"), ShowIf(nameof(AssetsReady))]
        [PropertyTooltip("Re-imports textures, rebuilds materials, sprites and the style, and refreshes the pieces in this scene.")]
        void RebuildAssets() => RingAssetSetup.Rebuild(out style, out library);

        [Button("Create Assets (style, library, materials)", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.5f)]
        [HideIf(nameof(AssetsReady)), PropertyOrder(1)]
        void CreateAssets() => RingAssetSetup.Rebuild(out style, out library);

        // ---------- No Level root yet ----------

        [Button("Open Level Editor Scene", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f), ShowIf(nameof(NeedsRoot)), PropertyOrder(2)]
        [InfoBox("No 'Level' root in this scene.")]
        void OpenEditorScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(RingAssetSetup.EditorScenePath))
            {
                EditorSceneManager.OpenScene(RingAssetSetup.EditorScenePath);
                root = FindRoot();
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = CreateRoot();
            EditorSceneManager.SaveScene(scene, RingAssetSetup.EditorScenePath);
        }

        [Button("Create Level Root here  (+ camera for play testing)"), ShowIf(nameof(NeedsRoot)), PropertyOrder(3)]
        void CreateRootHere() => root = CreateRoot();

        // ---------- Level ----------

        [ShowIfGroup(Main, Condition = nameof(Ready))]
        [BoxGroup(Main + "/Level"), HorizontalGroup(Main + "/Level/File"), HideLabel, SerializeField]
        string levelName = "level_001";

        [HorizontalGroup(Main + "/Level/File", Width = 46), Button("New")]
        void BtnNew() => NewLevel();

        [HorizontalGroup(Main + "/Level/File", Width = 46), Button("Load")]
        void BtnLoad() => LoadLevel();

        [HorizontalGroup(Main + "/Level/File", Width = 46), Button("Save"), GUIColor(0.4f, 0.9f, 0.5f)]
        void BtnSave() => SaveLevel();

        [HorizontalGroup(Main + "/Level/Snap"), LabelWidth(34), MinValue(0), SerializeField]
        float grid = 0.25f;

        [HorizontalGroup(Main + "/Level/Snap"), LabelText("Angle"), LabelWidth(40), MinValue(1), SerializeField]
        float angleStep = 15f;

        [HorizontalGroup(Main + "/Level/Snap", Width = 70), Button("Snap All")]
        void BtnSnapAll() => SnapAll();

        // ---------- Scene click mode ----------

        [BoxGroup(Main + "/Scene Click"), EnumToggleButtons, HideLabel, SerializeField]
        [OnValueChanged(nameof(OnModeChanged)), InfoBox("$" + nameof(ModeHelp), InfoMessageType.None)]
        Mode mode;

        string ModeHelp => mode switch
        {
            Mode.Link => linkFrom == null
                ? "Click the PARENT piece (the one that holds)."
                : $"Parent: {linkFrom.name}. Now click the CHILD. Esc cancels.",
            Mode.Arm => "Click on a ring or pipe: adds an arm pointing toward the click.",
            Mode.Clamp => "Click on a pipe: adds a metal clamp there.",
            Mode.Delete => "Click a clamp, sleeve (removes the link), arm, or piece.",
            _ => "Normal selection. Move/rotate with Unity's tools; Save snaps to the grid.",
        };

        void OnModeChanged()
        {
            linkFrom = null;
            SceneView.RepaintAll();
        }

        // ---------- Shapes ----------

        [BoxGroup(Main + "/Shapes  ·  click to place at the Scene view center"), OnInspectorGUI]
        void DrawShapes()
        {
            InitStyles();
            DrawTiles(PieceKind.Ring, "Rings");
            EditorGUILayout.Space(4f);
            DrawTiles(PieceKind.Pipe, "Pipes");
        }

        // ---------- Color / finish ----------

        [BoxGroup(Main + "/Color  ·  Finish"), EnumToggleButtons, HideLabel, SerializeField]
        Finish finish;

        [BoxGroup(Main + "/Color  ·  Finish"), OnInspectorGUI]
        void DrawPalette()
        {
            InitStyles();
            if (style.finishes.Count == 0)
                EditorGUILayout.LabelField("Style has no finishes yet: open Assets and click Rebuild Assets.", helpText);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < style.palette.Count; i++)
                {
                    Rect r = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f), GUILayout.Height(24f));
                    if (i == colorIndex) EditorGUI.DrawRect(r, Color.white);
                    EditorGUI.DrawRect(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), style.palette[i]);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) colorIndex = i;
                }
                GUILayout.FlexibleSpace();
            }
        }

        [HorizontalGroup(Main + "/Color  ·  Finish/Apply"), Button("Apply Color"), EnableIf(nameof(HasSelection))]
        void BtnApplyColor() => ApplyColorToSelection();

        [HorizontalGroup(Main + "/Color  ·  Finish/Apply"), Button("$" + nameof(ApplyFinishLabel)), EnableIf(nameof(HasSelection))]
        void BtnApplyFinish() => ApplyFinishToSelection();

        [HorizontalGroup(Main + "/Color  ·  Finish/Apply"), Button("Both"), EnableIf(nameof(HasSelection))]
        void BtnApplyBoth()
        {
            ApplyColorToSelection();
            ApplyFinishToSelection();
        }

        string ApplyFinishLabel => $"Apply {finish}";

        // ---------- Selection ----------

        [BoxGroup(Main + "/Selection", false), OnInspectorGUI]
        void DrawSelection()
        {
            InitStyles();
            var selected = SelectedPieces();
            EditorGUILayout.LabelField(SelectionTitle, EditorStyles.boldLabel);
            if (selected.Count == 0)
            {
                EditorGUILayout.LabelField("Select a piece to rotate it or add arm layouts.", helpText);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Rotate left {angleStep}°", EditorStyles.miniButtonLeft)) RotateSelection(angleStep);
                if (GUILayout.Button($"Rotate right {angleStep}°", EditorStyles.miniButtonRight)) RotateSelection(-angleStep);
            }
            if (selected.Count != 1) return;

            var piece = selected[0];
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Arms  ·  replaces the current arms", EditorStyles.miniBoldLabel);
            var layouts = ArmLayouts.For(piece);
            const int perRow = 2;
            for (int i = 0; i < layouts.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + perRow, layouts.Count); j++)
                        if (GUILayout.Button(layouts[j].label, GUILayout.Height(22))) ApplyArmLayout(piece, layouts[j]);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 80f;
                EditorGUI.BeginChangeCheck();
                float len = EditorGUILayout.FloatField("Arm length", style.armLength);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(style, "Arm length");
                    style.armLength = len;
                    EditorUtility.SetDirty(style);
                }
                EditorGUIUtility.labelWidth = 0f;
                if (GUILayout.Button("Clear Arms", EditorStyles.miniButton, GUILayout.Width(80))) ClearArms(piece);
            }
        }

        string SelectionTitle
        {
            get
            {
                var selected = SelectedPieces();
                return selected.Count == 1 ? $"Selected  ·  {selected[0].name}" : $"Selected  ·  {selected.Count} pieces";
            }
        }

        // ---------- Solvability ----------

        [BoxGroup(Main + "/Solvability"), HorizontalGroup(Main + "/Solvability/Params"), LabelWidth(58), MinValue(1), SerializeField]
        [LabelText("Attempts")]
        int rerollAttempts = 40;

        [HorizontalGroup(Main + "/Solvability/Params"), LabelWidth(52), MinValue(0.1), LabelText("Max sec"), SerializeField]
        float solveSeconds = 2f;

        [HorizontalGroup(Main + "/Solvability/Run"), Button("Check Solvable", ButtonSizes.Medium)]
        void BtnCheck() => CheckSolvable();

        [HorizontalGroup(Main + "/Solvability/Run"), Button("$" + nameof(RegenerateLabel), ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        void BtnRegenerate() => RegenerateSolvable();

        string RegenerateLabel => HasSelection ? "Regenerate (selected rings)" : "Regenerate (all rings)";

        [BoxGroup(Main + "/Solvability"), OnInspectorGUI]
        void DrawSolveResult()
        {
            InitStyles();
            EditorGUILayout.LabelField(
                "Regenerate keeps positions, links, arms and clamps; it only re-rolls ring rotations " +
                $"(snapped to {angleStep}°) until the level is solvable, keeping the hardest of the attempts. " +
                "Select rings first to re-roll only those. Max sec = search time per attempt.", helpText);

            if (solveResult == null) return;
            EditorGUILayout.HelpBox(solveResult.Summary, solveResult.solved ? MessageType.Info : MessageType.Warning);
            if (!solveResult.solved || solveResult.moves.Count == 0) return;
            showSolution = EditorGUILayout.Foldout(showSolution, $"Solution ({solveResult.moves.Count} moves) · click to select the piece", true);
            if (!showSolution) return;
            for (int i = 0; i < solveResult.moves.Count; i++)
            {
                var move = solveResult.moves[i];
                if (GUILayout.Button($"{i + 1}. {move}", moveButton) && move.piece != null)
                {
                    Selection.activeGameObject = move.piece.gameObject;
                    EditorGUIUtility.PingObject(move.piece.gameObject);
                }
            }
        }

        // ---------- Footer ----------

        [BoxGroup(Main + "/Footer", false), OnInspectorGUI]
        void DrawFooter()
        {
            var counts = $"Pieces {root.GetComponentsInChildren<Piece>(true).Length}   ·   " +
                         $"Links {root.GetComponentsInChildren<PieceLink>(true).Length}   ·   " +
                         $"Arms {root.GetComponentsInChildren<PieceArm>(true).Length}   ·   " +
                         $"Clamps {root.GetComponentsInChildren<PipeClamp>(true).Length}";
            EditorGUILayout.LabelField(counts, EditorStyles.centeredGreyMiniLabel);
        }

        // ---------- State ----------

        Transform root;
        int colorIndex;
        Piece linkFrom;
        LevelSolver.Result solveResult;
        bool showSolution;

        static GUIStyle tileLabel, helpText, moveButton;
        static readonly List<Vector2> IconPoints = new();

        bool AssetsReady => style != null && library != null;
        bool NeedsRoot => AssetsReady && Root == null;
        bool Ready => AssetsReady && Root != null;
        bool HasSelection => SelectedPieces().Count > 0;

        Transform Root
        {
            get
            {
                if (root == null) root = FindRoot();
                return root;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += Repaint;
            if (style == null) style = AssetDatabase.LoadAssetAtPath<PieceStyle>(RingAssetSetup.StylePath);
            if (library == null) library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(RingAssetSetup.LibraryPath);
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= Repaint;
        }

        static void InitStyles()
        {
            if (tileLabel != null) return;
            tileLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            helpText = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
            moveButton = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
        }

        void DrawTiles(PieceKind kind, string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 40f) / (TileW + 4f)));
            var presets = library.presets.FindAll(p => p.kind == kind);
            for (int i = 0; i < presets.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + perRow, presets.Count); j++)
                        if (ShapeTile(presets[j])) Place(presets[j]);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        bool ShapeTile(ShapePreset preset)
        {
            Rect r = GUILayoutUtility.GetRect(TileW, TileH, GUILayout.Width(TileW), GUILayout.Height(TileH));
            bool clicked = GUI.Button(r, GUIContent.none);
            if (Event.current.type == EventType.Repaint)
                DrawShapeIcon(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 22f), preset, style.GetColor(colorIndex));
            GUI.Label(new Rect(r.x + 2f, r.yMax - 17f, r.width - 4f, 15f), preset.name, tileLabel);
            return clicked;
        }

        /// <summary>All presets share one scale (5.4 world units across a tile) so sizes compare at a glance.</summary>
        void DrawShapeIcon(Rect r, ShapePreset preset, Color color)
        {
            var shape = preset.kind == PieceKind.Ring
                ? PathShape.Ring(preset.radius, 90f, preset.gapAngle, style.thickness)
                : PathShape.Polyline(preset.points, style.pipeCornerRadius);
            shape.Sample(IconPoints, null, null, 6f, 0.05f);
            if (IconPoints.Count < 2) return;
            if (shape.closed) IconPoints.Add(IconPoints[0]);

            Vector2 min = IconPoints[0], max = IconPoints[0];
            foreach (var p in IconPoints)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 c = (min + max) * 0.5f;
            float s = Mathf.Min(r.width, r.height) / 5.4f;

            var pts = new Vector3[IconPoints.Count];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new Vector3(r.center.x + (IconPoints[i].x - c.x) * s, r.center.y - (IconPoints[i].y - c.y) * s, 0f);
            Handles.color = color;
            Handles.DrawAAPolyLine(Mathf.Max(2.5f, style.thickness * s), pts);
        }

        // ---------- Scene view ----------

        void OnSceneGUI(SceneView view)
        {
            if (mode == Mode.Select || root == null || style == null) return;
            var e = Event.current;

            // Take over left clicks while a mode is active.
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            Vector2 world = MouseWorld(e.mousePosition);
            if (mode == Mode.Link && linkFrom != null)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(linkFrom.transform.position, world, 3f);
                view.Repaint();
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                linkFrom = null;
                e.Use();
                Repaint();
                return;
            }
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;

            switch (mode)
            {
                case Mode.Link: ClickLink(world); break;
                case Mode.Arm: ClickArm(world); break;
                case Mode.Clamp: ClickClamp(world); break;
                case Mode.Delete: ClickDelete(world); break;
            }
            e.Use();
            Repaint();
        }

        static Vector2 MouseWorld(Vector2 guiPos)
        {
            var ray = HandleUtility.GUIPointToWorldRay(guiPos);
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        Piece PickPiece(Vector2 world)
        {
            Piece best = null;
            float bestScore = float.MaxValue;
            foreach (var p in root.GetComponentsInChildren<Piece>())
            {
                if (!p.TryPick(world, 1.5f, out bool onBody, out float interior)) continue;
                float score = onBody ? -10000f - p.sortingOrder : interior;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        void ClickLink(Vector2 world)
        {
            var p = PickPiece(world);
            if (p == null) return;
            if (linkFrom == null)
            {
                linkFrom = p;
                return;
            }
            if (p == linkFrom) return;
            foreach (var existing in linkFrom.GetComponentsInChildren<PieceLink>(true))
                if (existing.child == p) { linkFrom = null; return; }

            var link = LevelBuilder.CreateLink(linkFrom, p, style, style.sleeveOffset);
            Undo.RegisterCreatedObjectUndo(link.gameObject, "Add Link");
            linkFrom = null;
            LevelBuilder.AssignSortingOrders(root);
            MarkDirty();
        }

        void ClickArm(Vector2 world)
        {
            var p = PickPiece(world);
            if (p == null) return;
            Vector2 local = p.transform.InverseTransformPoint(world);
            PieceArm arm;
            if (p.shape.TryGetRingCircle(out Vector2 c, out _))
            {
                Vector2 d = local - c;
                arm = LevelBuilder.CreateArm(p, Snap(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, angleStep), style.armLength);
            }
            else
            {
                float s = p.shape.ClosestAlong(local, out Vector2 tangent);
                Vector2 onPath = p.shape.Evaluate(s, out _);
                float side = Vector2.Dot(local - onPath, new Vector2(tangent.y, -tangent.x)) >= 0f ? 1f : -1f;
                arm = LevelBuilder.CreateArm(p, 0f, style.armLength, s / Mathf.Max(1e-4f, p.shape.Length), side);
            }
            Undo.RegisterCreatedObjectUndo(arm.gameObject, "Add Arm");
            MarkDirty();
        }

        void ClickClamp(Vector2 world)
        {
            var p = PickPiece(world);
            if (p == null || p.shape.TryGetRingCircle(out _, out _)) return; // clamps go on pipes
            var clamp = LevelBuilder.CreateClamp(p, world, style, root);
            Undo.RegisterCreatedObjectUndo(clamp.gameObject, "Add Clamp");
            MarkDirty();
        }

        void ClickDelete(Vector2 world)
        {
            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>())
                if (Vector2.Distance(world, clamp.transform.position) < clamp.CollisionRadius)
                {
                    Undo.DestroyObjectImmediate(clamp.gameObject);
                    MarkDirty();
                    return;
                }

            foreach (var link in root.GetComponentsInChildren<PieceLink>())
                if (link.sleeve != null && Vector2.Distance(world, link.sleeve.transform.position) < link.sleeveSize.x * 0.5f)
                {
                    Undo.DestroyObjectImmediate(link.gameObject);
                    LevelBuilder.AssignSortingOrders(root);
                    MarkDirty();
                    return;
                }

            foreach (var arm in root.GetComponentsInChildren<PieceArm>())
            {
                arm.GetSegment(out Vector2 a, out Vector2 b, out float pad);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(world - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                if (Vector2.Distance(world, a + ab * t) < pad * 1.5f)
                {
                    Undo.DestroyObjectImmediate(arm.gameObject);
                    MarkDirty();
                    return;
                }
            }

            var piece = PickPiece(world);
            if (piece == null) return;
            // Remove everything that points at the piece too.
            foreach (var link in root.GetComponentsInChildren<PieceLink>(true))
                if (link.child == piece) Undo.DestroyObjectImmediate(link.gameObject);
            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>(true))
                if (clamp.child == piece) Undo.DestroyObjectImmediate(clamp.gameObject);
            Undo.DestroyObjectImmediate(piece.gameObject);
            LevelBuilder.AssignSortingOrders(root);
            MarkDirty();
        }

        // ---------- Actions ----------

        void Place(ShapePreset preset)
        {
            var view = SceneView.lastActiveSceneView;
            Vector2 center = view != null ? (Vector2)view.pivot : Vector2.zero;
            var data = new PieceData
            {
                id = preset.name.Replace(" ", "_"),
                kind = preset.kind,
                pos = new Vector2(Snap(center.x, grid), Snap(center.y, grid)),
                color = colorIndex,
                finish = finish,
                radius = preset.radius,
                gapAngle = preset.gapAngle,
                gapCenter = 90f,
                points = new List<Vector2>(preset.points),
                cornerRadius = style.pipeCornerRadius,
            };
            var piece = LevelBuilder.CreatePiece(data, style, root);
            piece.Rebuild();
            Undo.RegisterCreatedObjectUndo(piece.gameObject, "Place " + preset.name);
            Selection.activeGameObject = piece.gameObject;
            MarkDirty();
        }

        void ApplyArmLayout(Piece piece, ArmLayouts.Layout layout)
        {
            ClearArms(piece);
            foreach (var spec in layout.arms)
            {
                var arm = LevelBuilder.CreateArm(piece, spec.angle, style.armLength, spec.t, spec.side);
                Undo.RegisterCreatedObjectUndo(arm.gameObject, "Arm Layout");
            }
            MarkDirty();
        }

        void ClearArms(Piece piece)
        {
            foreach (var arm in piece.GetComponentsInChildren<PieceArm>(true))
                Undo.DestroyObjectImmediate(arm.gameObject);
            MarkDirty();
        }

        void ApplyColorToSelection()
        {
            foreach (var p in SelectedPieces())
            {
                Undo.RecordObject(p, "Color");
                p.color = style.GetColor(colorIndex);
                p.Rebuild();
            }
            MarkDirty();
        }

        void ApplyFinishToSelection()
        {
            foreach (var p in SelectedPieces())
            {
                Undo.RecordObject(p, "Finish");
                foreach (var link in p.GetComponentsInChildren<PieceLink>(true))
                {
                    Undo.RecordObject(link, "Finish");
                    if (link.stem != null) Undo.RecordObject(link.stem, "Finish");
                    if (link.sleeve != null) Undo.RecordObject(link.sleeve, "Finish");
                }
                style.ApplyFinish(p, finish);
                p.Rebuild();
            }
            MarkDirty();
        }

        void RotateSelection(float degrees)
        {
            foreach (var p in SelectedPieces())
            {
                Undo.RecordObject(p.transform, "Rotate");
                p.transform.rotation = Quaternion.Euler(0f, 0f, Snap(p.transform.eulerAngles.z + degrees, angleStep));
            }
            MarkDirty();
        }

        void SnapAll()
        {
            foreach (var p in root.GetComponentsInChildren<Piece>(true))
            {
                Undo.RecordObject(p.transform, "Snap");
                Vector3 pos = p.transform.position;
                p.transform.SetPositionAndRotation(
                    new Vector3(Snap(pos.x, grid), Snap(pos.y, grid), 0f),
                    Quaternion.Euler(0f, 0f, Snap(p.transform.eulerAngles.z, angleStep)));
            }
            foreach (var arm in root.GetComponentsInChildren<PieceArm>(true))
            {
                if (!arm.GetComponentInParent<Piece>().shape.TryGetRingCircle(out _, out _)) continue;
                Undo.RecordObject(arm, "Snap");
                arm.angle = Snap(arm.angle, angleStep);
            }
            LevelBuilder.AssignSortingOrders(root);
            MarkDirty();
        }

        void NewLevel()
        {
            if (!EditorUtility.DisplayDialog("New level", "Remove every piece under the Level root?", "Clear", "Cancel")) return;
            ClearRoot();
        }

        void ClearRoot()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
            MarkDirty();
        }

        void SaveLevel()
        {
            SnapAll();
            var level = LevelBuilder.Export(root, style, levelName);
            Directory.CreateDirectory(LevelsDir);
            string path = $"{LevelsDir}/{levelName}.json";
            File.WriteAllText(path, JsonUtility.ToJson(level, true));
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[LevelEditor] Saved {path}: {level.pieces.Count} pieces, {level.links.Count} links, " +
                      $"{level.arms.Count} arms, {level.clamps.Count} clamps.");
        }

        void LoadLevel()
        {
            Directory.CreateDirectory(LevelsDir);
            string full = EditorUtility.OpenFilePanel("Load level", LevelsDir, "json");
            if (string.IsNullOrEmpty(full)) return;
            var level = JsonUtility.FromJson<LevelData>(File.ReadAllText(full));
            if (level == null) return;
            ClearRoot();
            var map = LevelBuilder.Build(level, style, root);
            foreach (var p in map.Values) Undo.RegisterCreatedObjectUndo(p.gameObject, "Load Level");
            foreach (var c in root.GetComponentsInChildren<PipeClamp>(true)) Undo.RegisterCreatedObjectUndo(c.gameObject, "Load Level");
            levelName = Path.GetFileNameWithoutExtension(full);
            MarkDirty();
        }

        // ---------- Solvability ----------

        void CheckSolvable()
        {
            LevelBuilder.AssignSortingOrders(root); // fresh link layout before the snapshot
            solveResult = new LevelSolver(root).Solve(solveSeconds);
            showSolution = false;
            Debug.Log("[LevelEditor] " + solveResult.Summary);
        }

        /// <summary>Rings whose rotation matters (a gap or arms): the selected ones, or all of them.</summary>
        List<Piece> RerollTargets()
        {
            var selected = SelectedPieces();
            var source = selected.Count > 0 ? selected : new List<Piece>(root.GetComponentsInChildren<Piece>());
            return source.FindAll(p => p.TryGetComponent<RingShape>(out var ring) &&
                                       (ring.gapAngle > 0f || p.GetComponentInChildren<PieceArm>() != null));
        }

        /// <summary>
        /// Re-rolls ring rotations (positions, links, arms, clamps untouched) and keeps the solvable attempt whose
        /// search was the hardest. Attempts are rejected when a link would start in a ring's gap or a re-rolled ring
        /// overlaps something. Restores the original rotations if nothing solvable turns up.
        /// </summary>
        void RegenerateSolvable()
        {
            var targets = RerollTargets();
            if (targets.Count == 0)
            {
                solveResult = new LevelSolver.Result { note = "No ring with a gap or arms to re-roll." };
                return;
            }

            var transforms = targets.ConvertAll(p => p.transform);
            var original = transforms.ConvertAll(t => t.rotation);
            Undo.RecordObjects(transforms.ToArray(), "Regenerate Solvable");

            int slots = Mathf.Max(1, Mathf.RoundToInt(360f / Mathf.Max(1f, angleStep)));
            float[] best = null;
            LevelSolver.Result bestResult = null;
            int found = 0, rejectGap = 0, rejectOverlap = 0, unsolvable = 0, unknown = 0, tried = 0;
            var angles = new float[targets.Count];
            try
            {
                for (int a = 0; a < rerollAttempts; a++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Regenerate Solvable",
                            $"Attempt {a + 1}/{rerollAttempts}  ·  solvable so far: {found}", (float)a / rerollAttempts))
                        break;
                    tried++;

                    for (int i = 0; i < targets.Count; i++)
                    {
                        angles[i] = Random.Range(0, slots) * 360f / slots;
                        transforms[i].rotation = Quaternion.Euler(0f, 0f, angles[i]);
                    }
                    foreach (var link in root.GetComponentsInChildren<PieceLink>()) link.Layout();

                    if (!LinksClearOfGaps(targets))
                    {
                        rejectGap++;
                        continue;
                    }
                    var solver = new LevelSolver(root);
                    if (solver.OverlappingAtStart(targets).Count > 0)
                    {
                        rejectOverlap++;
                        continue;
                    }
                    var result = solver.Solve(solveSeconds);
                    if (!result.solved)
                    {
                        if (result.gaveUp) unknown++;
                        else unsolvable++;
                        continue;
                    }
                    found++;
                    if (bestResult == null || result.states > bestResult.states ||
                        (result.states == bestResult.states && result.moves.Count > bestResult.moves.Count))
                    {
                        bestResult = result;
                        best = (float[])angles.Clone();
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            for (int i = 0; i < targets.Count; i++)
                transforms[i].rotation = best != null ? Quaternion.Euler(0f, 0f, best[i]) : original[i];
            LevelBuilder.AssignSortingOrders(root);
            MarkDirty();

            string stats = $"{tried} attempts on {targets.Count} rings: {found} solvable, {unsolvable} unsolvable, " +
                           $"{unknown} search limit, {rejectGap} link in a gap, {rejectOverlap} overlapping.";
            if (bestResult != null)
            {
                bestResult.note = "Regenerated. " + stats;
                solveResult = bestResult;
            }
            else
            {
                solveResult = new LevelSolver(root).Solve(solveSeconds);
                solveResult.note = "Nothing solvable found, rotations restored. " + stats +
                                   (unsolvable == 0 && found == 0 && unknown > 0 ? " Try a larger Max sec." : " The layout itself may be blocked.");
            }
            showSolution = false;
            Debug.Log("[LevelEditor] " + solveResult.Summary);
        }

        /// <summary>Every link touching a re-rolled ring must sit well inside that ring's arc, not in or at its gap.</summary>
        bool LinksClearOfGaps(List<Piece> rings)
        {
            foreach (var link in root.GetComponentsInChildren<PieceLink>())
            {
                var parent = link.GetComponentInParent<Piece>();
                if (parent == null || link.child == null) continue;
                if (rings.Contains(link.child) && !WellInside(link.child, link.GripWorldPos, link.sleeveSize.x * 0.5f))
                    return false;
                if (rings.Contains(parent))
                {
                    link.GetCapsule(out Vector2 start, out Vector2 end, out _, out _);
                    Vector2 anchor = start - (end - start).normalized * (parent.thickness * 0.5f);
                    if (!WellInside(parent, anchor, link.stemWidth * 0.5f)) return false;
                }
            }
            return true;
        }

        static bool WellInside(Piece p, Vector2 world, float margin)
        {
            if (p.shape.closed) return true;
            float s = p.shape.ClosestAlong(p.transform.InverseTransformPoint(world), out _);
            float m = margin + p.thickness * 0.5f;
            return s >= m && s <= p.shape.Length - m;
        }

        // ---------- Helpers ----------

        List<Piece> SelectedPieces()
        {
            var list = new List<Piece>();
            if (root == null) return list;
            foreach (var go in Selection.gameObjects)
            {
                var p = go.GetComponentInParent<Piece>();
                if (p != null && !list.Contains(p)) list.Add(p);
            }
            return list;
        }

        static float Snap(float v, float step) => step > 0f ? Mathf.Round(v / step) * step : v;

        static Transform FindRoot()
        {
            var go = GameObject.Find(RootName);
            return go != null ? go.transform : null;
        }

        static Transform CreateRoot()
        {
            var go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "Create Level Root");
            if (Object.FindFirstObjectByType<PieceInput>() == null)
            {
                var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera") { tag = "MainCamera" };
                if (!camGo.TryGetComponent<Camera>(out var cam)) cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                camGo.AddComponent<PieceInput>().cam = cam;
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            }
            return go.transform;
        }

        static void MarkDirty() => EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
