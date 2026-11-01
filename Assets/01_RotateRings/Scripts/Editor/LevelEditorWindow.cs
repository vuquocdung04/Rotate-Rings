using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Tools > Rotate Rings > Level Editor.
    /// Place presets from the ShapeLibrary, connect them with links / arms / clamps by clicking in the Scene view,
    /// apply arm layouts in one click, and save/load levels as JSON. Everything is built through LevelBuilder,
    /// exactly like at runtime.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        enum Mode { Select, Link, Arm, Clamp, Delete }

        const string LevelsDir = "Assets/01_RotateRings/Levels";
        const string RootName = "Level";
        const float TileW = 78f, TileH = 74f;

        PieceStyle style;
        ShapeLibrary library;
        Transform root;
        string levelName = "level_001";
        float grid = 0.25f;
        float angleStep = 15f;
        int colorIndex;
        Mode mode;
        Piece linkFrom;
        Vector2 scroll;
        bool showAssets;

        static GUIStyle sectionTitle, tileLabel, helpText;
        static readonly List<Vector2> IconPoints = new();

        [MenuItem("Tools/Rotate Rings/Level Editor")]
        static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(300f, 400f);
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += Repaint;
            style = AssetDatabase.LoadAssetAtPath<PieceStyle>(RingSceneBuilder.StylePath);
            library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(RingSceneBuilder.LibraryPath);
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= Repaint;
        }

        static void InitStyles()
        {
            if (sectionTitle != null) return;
            sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            tileLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            helpText = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
        }

        // ---------- Window ----------

        void OnGUI()
        {
            InitStyles();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!DrawAssets())
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            if (root == null) root = FindRoot();
            if (root == null)
            {
                using (Section("Level"))
                {
                    EditorGUILayout.HelpBox("No 'Level' root in this scene.", MessageType.Info);
                    if (GUILayout.Button("Create Level Root  (+ camera for play testing)", GUILayout.Height(28))) root = CreateRoot();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawLevel();
            DrawMode();
            DrawShapes();
            DrawColors();
            DrawSelection();
            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        bool DrawAssets()
        {
            bool ready = style != null && library != null;
            showAssets = EditorGUILayout.Foldout(showAssets || !ready, "Assets", true);
            if (showAssets || !ready)
            {
                EditorGUI.indentLevel++;
                style = (PieceStyle)EditorGUILayout.ObjectField("Piece Style", style, typeof(PieceStyle), false);
                library = (ShapeLibrary)EditorGUILayout.ObjectField("Shape Library", library, typeof(ShapeLibrary), false);
                EditorGUI.indentLevel--;
            }
            if (style != null && library != null) return true;
            if (GUILayout.Button("Create Default Style + Library", GUILayout.Height(26)))
                RingSceneBuilder.EnsureDefaultAssets(out style, out library);
            return false;
        }

        void DrawLevel()
        {
            using (Section("Level"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    levelName = EditorGUILayout.TextField(levelName);
                    if (GUILayout.Button("New", EditorStyles.miniButtonLeft, GUILayout.Width(44))) NewLevel();
                    if (GUILayout.Button("Load", EditorStyles.miniButtonMid, GUILayout.Width(44))) LoadLevel();
                    if (GUILayout.Button("Save", EditorStyles.miniButtonRight, GUILayout.Width(44))) SaveLevel();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUIUtility.labelWidth = 34f;
                    grid = EditorGUILayout.FloatField("Grid", grid, GUILayout.MinWidth(70));
                    EditorGUIUtility.labelWidth = 40f;
                    angleStep = EditorGUILayout.FloatField("Angle", angleStep, GUILayout.MinWidth(70));
                    EditorGUIUtility.labelWidth = 0f;
                    if (GUILayout.Button("Snap All", EditorStyles.miniButton, GUILayout.Width(70))) SnapAll();
                }
            }
        }

        void DrawMode()
        {
            using (Section("Scene Click"))
            {
                var newMode = (Mode)GUILayout.Toolbar((int)mode, new[] { "Select", "Link", "Arm", "Clamp", "Delete" }, GUILayout.Height(24));
                if (newMode != mode)
                {
                    mode = newMode;
                    linkFrom = null;
                    SceneView.RepaintAll();
                }
                string help = mode switch
                {
                    Mode.Link => linkFrom == null
                        ? "Click the <b>parent</b> piece (the one that holds)."
                        : $"Parent: <b>{linkFrom.name}</b>. Now click the <b>child</b>. Esc cancels.",
                    Mode.Arm => "Click on a ring or pipe: adds an arm pointing toward the click.",
                    Mode.Clamp => "Click on a pipe: adds a metal clamp there.",
                    Mode.Delete => "Click a clamp, sleeve (removes the link), arm, or piece.",
                    _ => "Normal selection. Move/rotate with Unity's tools; Save snaps to the grid.",
                };
                EditorGUILayout.LabelField(help, helpText);
            }
        }

        void DrawShapes()
        {
            using (Section("Shapes  ·  click to place at the Scene view center"))
            {
                DrawTiles(PieceKind.Ring, "Rings");
                EditorGUILayout.Space(4f);
                DrawTiles(PieceKind.Pipe, "Pipes");
            }
        }

        void DrawTiles(PieceKind kind, string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 30f) / (TileW + 4f)));
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

        void DrawColors()
        {
            using (Section("Color"))
            {
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
                GUI.enabled = SelectedPieces().Count > 0;
                if (GUILayout.Button("Apply To Selection", EditorStyles.miniButton)) ApplyColorToSelection();
                GUI.enabled = true;
            }
        }

        void DrawSelection()
        {
            var selected = SelectedPieces();
            using (Section(selected.Count == 1 ? $"Selected  ·  {selected[0].name}" : $"Selected  ·  {selected.Count} pieces"))
            {
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
        }

        void DrawFooter()
        {
            var counts = $"Pieces {root.GetComponentsInChildren<Piece>(true).Length}   ·   " +
                         $"Links {root.GetComponentsInChildren<PieceLink>(true).Length}   ·   " +
                         $"Arms {root.GetComponentsInChildren<PieceArm>(true).Length}   ·   " +
                         $"Clamps {root.GetComponentsInChildren<PipeClamp>(true).Length}";
            EditorGUILayout.LabelField(counts, EditorStyles.centeredGreyMiniLabel);
        }

        static EditorGUILayout.VerticalScope Section(string title)
        {
            EditorGUILayout.Space(6f);
            var scope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, sectionTitle);
            return scope;
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
