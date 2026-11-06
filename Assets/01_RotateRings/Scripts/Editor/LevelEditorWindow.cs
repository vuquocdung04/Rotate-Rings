using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Tools > Rotate Rings > Level Editor (Odin window).
    /// Left: level file, shape palette, color/finish, selection, solvability. Right: a canvas that renders the level
    /// with its own hidden camera, where pieces are moved, reshaped, rotated and connected (links / arms / clamps).
    /// The level being edited is a draft living in a hidden preview scene, so nothing appears in the Hierarchy until
    /// Apply to Scene rebuilds it under the scene's Level root. Everything is built through LevelBuilder, exactly like
    /// at runtime. The draft is kept in Library/ across reloads, play mode and closing the window.
    /// </summary>
    public class LevelEditorWindow : OdinEditorWindow
    {
        enum DragOp { None, Pan, Move, Arm, Clamp, RingRadius, RingGap, RingRotate, PipePoint }

        const string LevelsDir = "Assets/01_RotateRings/Levels";
        const string RootName = "Level";
        const string DraftRootName = "LevelDraft";
        const string DraftPath = "Library/RotateRingsLevelDraft.json";
        const float TileW = 78f, TileH = 74f;
        const string Main = "Main";
        const string Tabs = Main + "/Tabs";
        const string DragKey = "RotateRings.ShapePreset";
        const float ToolbarH = 21f, StatusH = 20f, MinPanel = 260f, MinCanvas = 300f;
        const float MinZoom = 6f, MaxZoom = 400f;
        const float MinRadius = 0.5f, MaxRadius = 8f, RadiusStep = 0.05f;
        const float MaxGap = 180f; // RingShape.gapAngle range
        const float MaxCornerRadius = 2f, MinPipeSegment = 0.05f;
        const float HandlePickPixels = 9f;
        const float RotateHandlePixels = 28f; // past the ring's outer edge
        static readonly int CanvasControlHint = "RotateRingsLevelCanvas".GetHashCode();
        const int MaxGenRings = 12, MaxGenPipes = 8;
        const int GenLayouts = 6, GenAttempts = 12; // layouts tried, rotation rolls per layout
        const float GenSolveSeconds = 0.5f;

        static readonly Color DefaultBackground = new(0.96f, 0.92f, 0.84f);
        static readonly Color SelectColor = new(0.15f, 0.85f, 1f, 0.4f);
        static readonly Color HoverColor = new(1f, 1f, 1f, 0.3f);
        static readonly Color HandleColor = new(1f, 0.6f, 0.1f, 1f);
        static readonly Color RadiusHandleColor = new(0.2f, 0.8f, 1f, 1f);
        static readonly Color GapHandleColor = new(1f, 0.3f, 0.75f, 1f);
        static readonly Color RotateHandleColor = new(0.3f, 0.9f, 0.4f, 1f);
        static readonly Color PointHandleColor = new(1f, 0.85f, 0.2f, 1f);
        static readonly Color AddPointColor = new(1f, 1f, 1f, 0.75f);

        [MenuItem("Tools/Rotate Rings/Level Editor")]
        static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(MinPanel + MinCanvas, 420f);
        }

        // ---------- No assets yet / play mode ----------

        [Button("Create Assets (style, library, materials)", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.5f)]
        [HideIf(nameof(AssetsReady)), PropertyOrder(1)]
        void CreateAssets() => RingAssetSetup.Rebuild(out style, out library);

        [ShowIf(nameof(Playing)), PropertyOrder(2), OnInspectorGUI]
        void DrawPlayModeNote() => EditorGUILayout.HelpBox("Exit Play Mode to edit the level.", MessageType.Info);

        // ---------- Level file ----------

        [ShowIfGroup(Main, Condition = nameof(Ready))]
        [BoxGroup(Main + "/Level"), HorizontalGroup(Main + "/Level/File"), HideLabel, SerializeField]
        string levelName = "level_001";

        [HorizontalGroup(Main + "/Level/File", Width = 46), Button("Load")]
        void BtnLoad() => LoadLevel();

        [HorizontalGroup(Main + "/Level/File", Width = 46), Button("Save"), GUIColor(0.4f, 0.9f, 0.5f)]
        void BtnSave() => SaveLevel();

        [HorizontalGroup(Main + "/Level/Scene"), Button("$" + nameof(ApplyLabel), ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        [PropertyTooltip("Replaces everything under the scene's Level root with this draft (Ctrl+Z restores it).")]
        void BtnApply() => ApplyToScene();

        [HorizontalGroup(Main + "/Level/Scene", Width = 84), Button("From Scene", ButtonSizes.Medium)]
        [PropertyTooltip("Replaces the draft with what is under the scene's Level root.")]
        void BtnFromScene() => PullFromScene();

        [HorizontalGroup(Main + "/Level/Gen"), LabelText("Rings"), LabelWidth(38), MinValue(0), MaxValue(MaxGenRings), SerializeField]
        int genRings = 5;

        [HorizontalGroup(Main + "/Level/Gen"), LabelText("Pipes"), LabelWidth(38), MinValue(0), MaxValue(MaxGenPipes), SerializeField]
        int genPipes = 1;

        [HorizontalGroup(Main + "/Level/GenButtons"), Button("Generate Auto"), GUIColor(0.8f, 0.7f, 1f)]
        [PropertyTooltip("Replaces the draft with a random solvable layout (Ctrl+Z brings the old draft back).")]
        void BtnGenerate() => GenerateSample();

        [HorizontalGroup(Main + "/Level/GenButtons", Width = 84), Button("Clear All")]
        [PropertyTooltip("Removes every piece from the draft. The scene only changes on Apply.")]
        void BtnClearAll() => ClearAll();

        string ApplyLabel => unapplied ? "Apply to Scene  (changes pending)" : "Apply to Scene";

        // ---------- Tab: Build ----------

        [TabGroup(Tabs, "Build"), OnInspectorGUI]
        void DrawShapes()
        {
            InitStyles();
            EditorGUILayout.LabelField("Drag a tile into the canvas to place it.", helpText);
            DrawTiles();
        }

        [TabGroup(Tabs, "Build"), Title("Color  ·  Finish"), EnumToggleButtons, HideLabel, SerializeField]
        Finish finish;

        [TabGroup(Tabs, "Build"), OnInspectorGUI]
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

        [HorizontalGroup(Tabs + "/Build/Apply"), Button("Apply Color"), EnableIf(nameof(HasSelection))]
        void BtnApplyColor() => ApplyColorToSelection();

        [HorizontalGroup(Tabs + "/Build/Apply"), Button("$" + nameof(ApplyFinishLabel)), EnableIf(nameof(HasSelection))]
        void BtnApplyFinish() => ApplyFinishToSelection();

        [HorizontalGroup(Tabs + "/Build/Apply"), Button("Both"), EnableIf(nameof(HasSelection))]
        void BtnApplyBoth()
        {
            ApplyColorToSelection();
            ApplyFinishToSelection();
        }

        string ApplyFinishLabel => $"Apply {finish}";

        // ---------- Tab: Solve ----------

        [TabGroup(Tabs, "Solve"), HorizontalGroup(Tabs + "/Solve/Params"), LabelWidth(58), MinValue(1), SerializeField]
        [LabelText("Attempts")]
        int rerollAttempts = 40;

        [HorizontalGroup(Tabs + "/Solve/Params"), LabelWidth(52), MinValue(0.1), LabelText("Max sec"), SerializeField]
        float solveSeconds = 2f;

        [HorizontalGroup(Tabs + "/Solve/Run"), Button("Check Solvable", ButtonSizes.Medium)]
        void BtnCheck() => CheckSolvable();

        [HorizontalGroup(Tabs + "/Solve/Run"), Button("$" + nameof(RegenerateLabel), ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        void BtnRegenerate() => RegenerateSolvable(rerollAttempts, solveSeconds);

        string RegenerateLabel => HasSelection ? "Regenerate (selected)" : "Regenerate (all rings)";

        [TabGroup(Tabs, "Solve"), OnInspectorGUI]
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
                    SelectPieces(new List<Piece> { move.piece });
                }
            }
        }

        // ---------- Selection ----------

        [BoxGroup(Main + "/Selection", false), OnInspectorGUI]
        void DrawSelection()
        {
            InitStyles();
            var selected = SelectedPieces();
            EditorGUILayout.LabelField(SelectionTitle, EditorStyles.boldLabel);
            if (selected.Count == 0)
            {
                EditorGUILayout.LabelField("Click a piece in the canvas (Shift adds).", helpText);
                return;
            }

            if (selected.Count != 1) return;

            var piece = selected[0];
            if (piece.TryGetComponent<RingShape>(out var ring)) DrawRingFields(piece, ring);
            else if (piece.TryGetComponent<PipeShape>(out var pipe)) DrawPipeFields(piece, pipe);
        }

        void DrawRingFields(Piece piece, RingShape ring)
        {
            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            float radius = EditorGUILayout.Slider("Radius", ring.radius, MinRadius, MaxRadius);
            float gap = EditorGUILayout.Slider("Gap", ring.gapAngle, 0f, MaxGap);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObjects(new Object[] { ring, piece }, "Ring Shape");
            SetRing(ring, radius, gap);
            MarkDirty();
        }

        void DrawPipeFields(Piece piece, PipeShape pipe)
        {
            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            float corner = EditorGUILayout.Slider("Corner", pipe.cornerRadius, piece.thickness, MaxCornerRadius);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObjects(new Object[] { pipe, piece }, "Pipe Shape");
            pipe.cornerRadius = corner;
            ApplyPipe(pipe);
            MarkDirty();
        }

        string SelectionTitle
        {
            get
            {
                var selected = SelectedPieces();
                return selected.Count == 1 ? $"Selected  ·  {selected[0].name}" : $"Selected  ·  {selected.Count} pieces";
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

        // ---------- Assets ----------

        [FoldoutGroup("Assets", Expanded = false), PropertyOrder(10), Required, SerializeField]
        PieceStyle style;

        [FoldoutGroup("Assets"), PropertyOrder(10), Required, SerializeField]
        ShapeLibrary library;

        [FoldoutGroup("Assets"), PropertyOrder(10), Button("Rebuild Assets"), ShowIf(nameof(AssetsReady))]
        [PropertyTooltip("Re-imports textures, rebuilds materials, sprites and the style, and refreshes the pieces in this scene.")]
        void RebuildAssets() => RingAssetSetup.Rebuild(out style, out library);

        // ---------- Canvas settings (drawn in the canvas toolbar) ----------

        [SerializeField, HideInInspector] float grid = 0.25f;
        [SerializeField, HideInInspector] float angleStep = 15f;
        [SerializeField, HideInInspector] bool showGrid = true;
        [SerializeField, HideInInspector] float panelWidth = 320f;
        [SerializeField, HideInInspector] Vector2 viewCenter;
        [SerializeField, HideInInspector] float zoom = 45f; // pixels per world unit
        [SerializeField, HideInInspector] bool unapplied;

        string StatusHelp
        {
            get
            {
                if (linkFrom != null) return $"Link: parent {linkFrom.name}, now click the CHILD. Esc cancels.";
                if (SelectedShape<RingShape>() != null) return "Ring: green dot rotates, blue dot resizes, pink dots open the gap (Ctrl: no snap)";
                if (SelectedShape<PipeShape>() != null) return "Pipe: drag a yellow dot to bend, a small dot to add a corner · Shift+click a yellow dot removes it (Ctrl: no snap)";
                return "Right-click a piece: arm / clamp / link / delete · Drag: move (Ctrl: no snap) · Shift: add · Q/E rotate · arrows nudge · Del · F frame · wheel zoom · RMB/MMB drag pan";
            }
        }

        // ---------- State ----------

        // NonSerialized: a domain reload must not carry these over, the draft is reopened from disk instead.
        [System.NonSerialized] Scene draftScene;
        [System.NonSerialized] Transform root; // draft root, in the preview scene
        [System.NonSerialized] readonly List<Piece> selection = new();
        int colorIndex;
        Piece linkFrom;
        LevelSolver.Result solveResult;
        bool showSolution;

        Camera canvasCam;
        RenderTexture canvasRT;
        Vector2 canvasSize = new(400f, 400f);
        Vector2 mouseCanvas;
        Piece hover;
        // NonSerialized: Unity would turn these nulls into empty presets on a reload, leaving a stuck drop ghost.
        [System.NonSerialized] ShapePreset dropPreset, pressedTile;
        Vector2 tilePressPos;

        [System.NonSerialized] DragOp drag;
        bool dragMoved, dragAdditive;
        Piece dragPiece;
        PieceArm dragArm;
        PipeClamp dragClamp;
        RingShape dragRing;
        PipeShape dragPipe;
        int dragPoint;
        Vector2 dragStartMouse, dragStartWorld, movePivotStart;
        readonly List<Transform> moveTargets = new();
        readonly List<Vector3> moveStarts = new();
        bool draggingSplitter;

        static GUIStyle tileLabel, helpText, moveButton, statusLabel, statusRight;
        static readonly List<Vector2> IconPoints = new();

        bool AssetsReady => style != null && library != null;
        static bool Playing => EditorApplication.isPlayingOrWillChangePlaymode;
        bool Ready => AssetsReady && !Playing && Root != null;
        bool HasSelection => SelectedPieces().Count > 0;

        Transform Root
        {
            get
            {
                if (root == null) OpenDraft();
                return root;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += CloseDraft;
            EditorApplication.hierarchyChanged += Repaint;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
            if (style == null) style = AssetDatabase.LoadAssetAtPath<PieceStyle>(RingAssetSetup.StylePath);
            if (library == null) library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(RingAssetSetup.LibraryPath);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= CloseDraft;
            EditorApplication.hierarchyChanged -= Repaint;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            CloseDraft();
            if (canvasCam != null) DestroyImmediate(canvasCam.gameObject);
            if (canvasRT != null)
            {
                canvasRT.Release();
                DestroyImmediate(canvasRT);
            }
        }

        void OnUndoRedo()
        {
            if (root != null) LevelBuilder.AssignSortingOrders(root); // meshes and links follow the restored values
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        /// <summary>Draft pieces must not join Piece.All while the game runs, so the draft waits on disk meanwhile.</summary>
        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) CloseDraft();
            Repaint();
        }

        void OnObjectChanges(ref ObjectChangeEventStream stream) => Repaint();

        static void InitStyles()
        {
            if (tileLabel != null) return;
            tileLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            helpText = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
            moveButton = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
            statusLabel = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            statusRight = new GUIStyle(statusLabel) { alignment = TextAnchor.MiddleRight };
        }

        // ---------- Window layout: panel | canvas ----------

        protected override void OnImGUI()
        {
            if (!Ready)
            {
                base.OnImGUI();
                return;
            }
            InitStyles();
            panelWidth = Mathf.Clamp(panelWidth, MinPanel, Mathf.Max(MinPanel, position.width - MinCanvas));

            GUILayout.BeginArea(new Rect(0f, 0f, panelWidth, position.height));
            base.OnImGUI();
            GUILayout.EndArea();

            DrawSplitter(new Rect(panelWidth - 2f, 0f, 5f, position.height));
            DrawCanvasArea(new Rect(panelWidth + 1f, 0f, position.width - panelWidth - 1f, position.height));
        }

        void DrawSplitter(Rect r)
        {
            EditorGUI.DrawRect(new Rect(r.x + 2f, r.y, 1f, r.height), new Color(0f, 0f, 0f, 0.35f));
            EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                draggingSplitter = true;
                e.Use();
            }
            else if (draggingSplitter && e.type == EventType.MouseDrag)
            {
                panelWidth = e.mousePosition.x;
                e.Use();
                Repaint();
            }
            else if (draggingSplitter && e.rawType == EventType.MouseUp)
            {
                draggingSplitter = false;
            }
        }

        void DrawCanvasArea(Rect area)
        {
            var bar = new Rect(area.x, area.y, area.width, ToolbarH);
            var status = new Rect(area.x, area.yMax - StatusH, area.width, StatusH);
            var view = new Rect(area.x, bar.yMax, area.width, Mathf.Max(1f, area.height - ToolbarH - StatusH));

            DrawToolbar(bar);
            GUI.BeginClip(view);
            canvasSize = view.size;
            var local = new Rect(Vector2.zero, view.size);
            DrawCanvas(local);
            HandleCanvasInput(local);
            GUI.EndClip();
            DrawStatus(status);
        }

        void DrawToolbar(Rect r)
        {
            GUILayout.BeginArea(r, EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Grid", EditorStyles.miniLabel, GUILayout.Width(26f));
                grid = Mathf.Max(0f, EditorGUILayout.FloatField(grid, EditorStyles.toolbarTextField, GUILayout.Width(38f)));
                GUILayout.Label("Angle", EditorStyles.miniLabel, GUILayout.Width(34f));
                angleStep = Mathf.Max(1f, EditorGUILayout.FloatField(angleStep, EditorStyles.toolbarTextField, GUILayout.Width(32f)));
                showGrid = GUILayout.Toggle(showGrid, new GUIContent("#", "Show grid"), EditorStyles.toolbarButton, GUILayout.Width(22f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Snap All", EditorStyles.toolbarButton)) SnapAll();
                if (GUILayout.Button(new GUIContent("Frame", "F"), EditorStyles.toolbarButton)) FrameSelectionOrAll();
            }
            GUILayout.EndArea();
        }

        void DrawStatus(Rect r)
        {
            EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.8f, 0.8f, 0.8f));
            Vector2 w = CanvasToWorld(mouseCanvas);
            const float infoW = 150f;
            GUI.Label(new Rect(r.x + 6f, r.y, r.width - infoW - 12f, r.height), StatusHelp, statusLabel);
            GUI.Label(new Rect(r.xMax - infoW - 6f, r.y, infoW, r.height), $"x {w.x:0.00}   y {w.y:0.00}   ·   {zoom:0} px/u", statusRight);
        }

        // ---------- Canvas: drawing ----------

        Vector2 WorldToCanvas(Vector2 w) =>
            canvasSize * 0.5f + new Vector2(w.x - viewCenter.x, viewCenter.y - w.y) * zoom;

        Vector2 CanvasToWorld(Vector2 c) =>
            viewCenter + new Vector2(c.x - canvasSize.x * 0.5f, canvasSize.y * 0.5f - c.y) / zoom;

        void DrawCanvas(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tex = RenderLevel(r.size);
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false);
            else EditorGUI.DrawRect(r, DefaultBackground);
            if (showGrid) DrawGrid(r);
            DrawOverlays();
        }

        /// <summary>The level as the game camera sees it, through a hidden orthographic camera matching the canvas.</summary>
        Texture RenderLevel(Vector2 size)
        {
            float ppp = EditorGUIUtility.pixelsPerPoint;
            int w = Mathf.Max(1, Mathf.RoundToInt(size.x * ppp)), h = Mathf.Max(1, Mathf.RoundToInt(size.y * ppp));
            if (canvasRT == null || canvasRT.width != w || canvasRT.height != h)
            {
                if (canvasRT != null)
                {
                    canvasRT.Release();
                    DestroyImmediate(canvasRT);
                }
                canvasRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                    { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            }
            if (canvasCam == null)
            {
                var go = new GameObject("LevelEditorCanvasCamera") { hideFlags = HideFlags.HideAndDontSave };
                canvasCam = go.AddComponent<Camera>();
                canvasCam.enabled = false;
                canvasCam.orthographic = true;
                canvasCam.clearFlags = CameraClearFlags.SolidColor;
                canvasCam.nearClipPlane = 0.01f;
                canvasCam.farClipPlane = 100f;
            }
            var main = Camera.main;
            canvasCam.backgroundColor = main != null ? main.backgroundColor : DefaultBackground;
            canvasCam.orthographicSize = size.y * 0.5f / zoom;
            canvasCam.aspect = size.x / size.y;
            canvasCam.transform.SetPositionAndRotation(new Vector3(viewCenter.x, viewCenter.y, -10f), Quaternion.identity);
            canvasCam.targetTexture = canvasRT;
            canvasCam.scene = draftScene;

            var prev = RenderTexture.active;
            canvasCam.Render();
            RenderTexture.active = prev;
            return canvasRT;
        }

        void DrawGrid(Rect r)
        {
            Vector2 min = CanvasToWorld(new Vector2(0f, r.height)), max = CanvasToWorld(new Vector2(r.width, 0f));
            float step = grid > 0f ? grid : 1f;
            while (step * zoom < 8f) step *= 2f;

            for (float x = Mathf.Ceil(min.x / step) * step; x <= max.x; x += step)
                GridLine(x, new Vector2(x, min.y), new Vector2(x, max.y));
            for (float y = Mathf.Ceil(min.y / step) * step; y <= max.y; y += step)
                GridLine(y, new Vector2(min.x, y), new Vector2(max.x, y));
        }

        void GridLine(float v, Vector2 a, Vector2 b)
        {
            float alpha = Mathf.Abs(v) < 1e-3f ? 0.22f : Mathf.Abs(v - Mathf.Round(v)) < 1e-3f ? 0.1f : 0.045f;
            Handles.color = new Color(0f, 0f, 0f, alpha);
            Handles.DrawLine(WorldToCanvas(a), WorldToCanvas(b));
        }

        void DrawOverlays()
        {
            var selected = SelectedPieces();
            if (hover != null && drag == DragOp.None && !selected.Contains(hover))
                DrawPieceHighlight(hover, HoverColor);
            foreach (var p in selected) DrawPieceHighlight(p, SelectColor);

            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>())
                DrawHandle(clamp.transform.position, new Color(0.6f, 0.75f, 0.9f, 1f));
            foreach (var p in selected)
            foreach (var arm in p.GetComponentsInChildren<PieceArm>())
            {
                arm.GetSegment(out _, out Vector2 tip, out _);
                DrawHandle(tip, HandleColor);
            }

            var ring = SelectedShape<RingShape>();
            if (ring != null)
            {
                DrawHandle(RadiusHandle(ring), RadiusHandleColor);
                DrawHandle(GapHandle(ring, 1f), GapHandleColor);
                DrawHandle(GapHandle(ring, -1f), GapHandleColor);

                Handles.color = RotateHandleColor;
                Handles.DrawAAPolyLine(2f, WorldToCanvas(RingPoint(ring, ring.gapCenter)), WorldToCanvas(RotateHandle(ring)));
                DrawHandle(RotateHandle(ring), RotateHandleColor);
            }

            var pipe = SelectedShape<PipeShape>();
            if (pipe != null)
            {
                for (int i = 0; i + 1 < pipe.points.Count; i++) DrawHandle(PipeMidpoint(pipe, i), AddPointColor, 3.5f);
                for (int i = 0; i < pipe.points.Count; i++) DrawHandle(pipe.transform.TransformPoint(pipe.points[i]), PointHandleColor);
            }

            if (linkFrom != null)
            {
                DrawPieceHighlight(linkFrom, SelectColor);
                Handles.color = Color.cyan;
                Handles.DrawAAPolyLine(3f, WorldToCanvas(linkFrom.transform.position), mouseCanvas);
            }

            if (dropPreset != null) DrawPreset(dropPreset, SnapPos(CanvasToWorld(mouseCanvas)));
        }

        // Handles sit on the ring: the two gap edges, and the radius dot opposite the gap.
        static Vector2 RingPoint(RingShape ring, float localDeg)
        {
            float a = localDeg * Mathf.Deg2Rad;
            return ring.transform.TransformPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring.radius);
        }

        static Vector2 RadiusHandle(RingShape ring) => RingPoint(ring, ring.gapCenter + 180f);

        /// <summary>Rotate dot: out past the gap, a fixed distance on screen so it stays grabbable at any zoom.</summary>
        Vector2 RotateHandle(RingShape ring)
        {
            Vector2 center = ring.transform.position;
            Vector2 edge = RingPoint(ring, ring.gapCenter);
            return edge + (edge - center).normalized * (ring.GetComponent<Piece>().thickness * 0.5f + RotateHandlePixels / zoom);
        }

        static Vector2 GapHandle(RingShape ring, float side) => RingPoint(ring, ring.gapCenter + side * ring.gapAngle * 0.5f);

        /// <summary>World position of the add-a-corner dot on leg i of a pipe.</summary>
        static Vector2 PipeMidpoint(PipeShape pipe, int i) =>
            pipe.transform.TransformPoint((pipe.points[i] + pipe.points[i + 1]) * 0.5f);

        void DrawPieceHighlight(Piece p, Color c)
        {
            if (p.shape == null) return;
            p.shape.Sample(IconPoints, null, null, 6f, 0.05f);
            if (IconPoints.Count < 2) return;
            if (p.shape.closed) IconPoints.Add(IconPoints[0]);
            var pts = new Vector3[IconPoints.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = WorldToCanvas(p.transform.TransformPoint(IconPoints[i]));
            Handles.color = c;
            Handles.DrawAAPolyLine(p.thickness * zoom + 6f, pts);
        }

        void DrawHandle(Vector2 world, Color c, float radius = 5f)
        {
            Vector2 g = WorldToCanvas(world);
            Handles.color = c;
            Handles.DrawSolidDisc(g, Vector3.forward, radius);
            Handles.color = new Color(0f, 0f, 0f, 0.6f);
            Handles.DrawWireDisc(g, Vector3.forward, radius);
        }

        /// <summary>Ghost of a preset where it would be dropped, at the canvas scale.</summary>
        void DrawPreset(ShapePreset preset, Vector2 world)
        {
            PresetShape(preset).Sample(IconPoints, null, null, 6f, 0.05f);
            if (IconPoints.Count < 2) return;
            if (preset.kind == PieceKind.Ring && preset.gapAngle <= 0f) IconPoints.Add(IconPoints[0]);
            var pts = new Vector3[IconPoints.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = WorldToCanvas(world + IconPoints[i]);
            var c = style.GetColor(colorIndex);
            Handles.color = new Color(c.r, c.g, c.b, 0.6f);
            Handles.DrawAAPolyLine(style.thickness * zoom, pts);
        }

        PathShape PresetShape(ShapePreset preset) => preset.kind == PieceKind.Ring
            ? PathShape.Ring(preset.radius, 90f, preset.gapAngle, style.thickness)
            : PathShape.Polyline(preset.points, style.pipeCornerRadius);

        // ---------- Canvas: input ----------

        void HandleCanvasInput(Rect r)
        {
            var e = Event.current;
            // Hinted so the id stays the same when the panel gains controls mid-drag (selecting a piece adds sliders).
            int id = GUIUtility.GetControlID(CanvasControlHint, FocusType.Passive);
            bool inside = r.Contains(e.mousePosition);
            if (inside || drag != DragOp.None) mouseCanvas = e.mousePosition;
            Vector2 world = CanvasToWorld(e.mousePosition);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseMove:
                    if (!inside) break;
                    hover = PickPiece(world);
                    Repaint();
                    break;

                case EventType.ScrollWheel:
                    if (!inside) break;
                    ZoomAt(e.mousePosition, e.delta.y > 0f ? 1f / 1.15f : 1.15f);
                    e.Use();
                    break;

                case EventType.MouseDown:
                    if (!inside) break;
                    GUIUtility.keyboardControl = 0;
                    dragStartMouse = e.mousePosition;
                    dragStartWorld = world;
                    dragMoved = false;
                    if (e.button == 1 || e.button == 2 || (e.button == 0 && e.alt)) drag = DragOp.Pan;
                    else if (e.button == 0) BeginSelectDrag(world, e.shift);
                    if (drag != DragOp.None) GUIUtility.hotControl = id;
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    if ((e.mousePosition - dragStartMouse).sqrMagnitude > 9f) dragMoved = true;
                    UpdateDrag(e, world);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    bool rightClick = drag == DragOp.Pan && e.button == 1 && !dragMoved;
                    EndDrag();
                    GUIUtility.hotControl = 0;
                    if (rightClick) ShowContextMenu(world);
                    e.Use();
                    Repaint();
                    break;

                case EventType.KeyDown:
                    if (inside && !EditorGUIUtility.editingTextField && HandleKey(e.keyCode))
                    {
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!inside || DragAndDrop.GetGenericData(DragKey) is not ShapePreset preset) break;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    dropPreset = preset;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        dropPreset = null;
                        Place(preset, SnapPos(world));
                    }
                    e.Use();
                    Repaint();
                    break;

                case EventType.DragExited:
                    dropPreset = null;
                    Repaint();
                    break;
            }
        }

        /// <summary>Right-click: what can be added at this spot, without switching modes.</summary>
        void ShowContextMenu(Vector2 world)
        {
            var piece = PickPiece(world);
            var attachment = HitAttachment(world);
            if (piece == null && attachment == null) return;

            var menu = new GenericMenu();
            if (piece != null)
            {
                menu.AddItem(new GUIContent("Add Arm Here"), false, () => ClickArm(world));
                if (!piece.shape.TryGetRingCircle(out _, out _))
                    menu.AddItem(new GUIContent("Add Clamp Here"), false, () => ClickClamp(world));
                menu.AddItem(new GUIContent("Link To..."), false, () => linkFrom = piece);
                menu.AddSeparator("");

                menu.AddItem(new GUIContent($"Rotate Left {angleStep}°"), false, () => RotatePiece(piece, angleStep));
                menu.AddItem(new GUIContent($"Rotate Right {angleStep}°"), false, () => RotatePiece(piece, -angleStep));
                menu.AddSeparator("");

                foreach (var layout in ArmLayouts.For(piece)) // '/' would open a submenu
                    menu.AddItem(new GUIContent("Arm Layout/" + layout.label.Replace("/", "-")), false, () => ApplyArmLayout(piece, layout));
                if (piece.GetComponentInChildren<PieceArm>() != null)
                    menu.AddItem(new GUIContent("Clear Arms"), false, () => ClearArms(piece));
                menu.AddSeparator("");
            }
            string target = attachment switch
            {
                PipeClamp => "Clamp",
                PieceLink => "Link",
                PieceArm => "Arm",
                _ => "Piece",
            };
            menu.AddItem(new GUIContent("Delete " + target), false, () => ClickDelete(world));
            menu.ShowAsContext();
        }

        /// <summary>Shape handles of the selected piece, arm tips, clamps, then pieces; empty space deselects.</summary>
        void BeginSelectDrag(Vector2 world, bool additive)
        {
            if (linkFrom != null) // Link To... from the right-click menu is waiting for its child
            {
                ClickLink(world);
                return;
            }

            dragAdditive = additive;
            var selected = SelectedPieces();

            var pipe = SelectedShape<PipeShape>();
            if (pipe != null && BeginPipeDrag(pipe, world, additive)) return;

            var ring = SelectedShape<RingShape>();
            if (ring != null)
            {
                float pick = HandlePickPixels / zoom;
                if (Vector2.Distance(world, RotateHandle(ring)) <= pick)
                {
                    Undo.RecordObject(ring.transform, "Rotate");
                    dragRing = ring;
                    drag = DragOp.RingRotate;
                    return;
                }

                bool onRadius = Vector2.Distance(world, RadiusHandle(ring)) <= pick;
                bool onGap = Vector2.Distance(world, GapHandle(ring, 1f)) <= pick ||
                             Vector2.Distance(world, GapHandle(ring, -1f)) <= pick;
                if (onRadius || onGap)
                {
                    Undo.RecordObjects(new Object[] { ring, ring.GetComponent<Piece>() }, "Ring Shape");
                    dragRing = ring;
                    drag = onRadius ? DragOp.RingRadius : DragOp.RingGap;
                    return;
                }
            }

            float tipRadius = 8f / zoom;
            foreach (var p in selected)
            foreach (var arm in p.GetComponentsInChildren<PieceArm>())
            {
                arm.GetSegment(out _, out Vector2 tip, out float pad);
                if (Vector2.Distance(world, tip) > Mathf.Max(pad, tipRadius)) continue;
                Undo.RecordObject(arm, "Aim Arm");
                dragArm = arm;
                drag = DragOp.Arm;
                return;
            }

            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>())
            {
                if (Vector2.Distance(world, clamp.transform.position) > clamp.CollisionRadius) continue;
                Undo.RecordObject(clamp.transform, "Move Clamp");
                dragClamp = clamp;
                drag = DragOp.Clamp;
                return;
            }

            var piece = PickPiece(world);
            if (piece == null)
            {
                if (!additive) selection.Clear();
                return;
            }

            if (additive)
            {
                if (selected.Contains(piece))
                {
                    selected.Remove(piece);
                    SelectPieces(selected);
                    return;
                }
                selected.Add(piece);
            }
            else if (!selected.Contains(piece))
            {
                selected = new List<Piece> { piece };
            }
            SelectPieces(selected);
            dragPiece = piece;
            BeginMove(selected, piece);
        }

        /// <summary>Corner dots drag (or, with remove, delete the corner); leg dots insert a corner and drag it.</summary>
        bool BeginPipeDrag(PipeShape pipe, Vector2 world, bool remove)
        {
            float pick = HandlePickPixels / zoom;
            for (int i = 0; i < pipe.points.Count; i++)
            {
                if (Vector2.Distance(world, pipe.transform.TransformPoint(pipe.points[i])) > pick) continue;
                RecordPipe(pipe);
                if (remove)
                {
                    if (pipe.points.Count > 2)
                    {
                        pipe.points.RemoveAt(i);
                        ApplyPipe(pipe);
                        MarkDirty();
                    }
                    return true;
                }
                dragPipe = pipe;
                dragPoint = i;
                drag = DragOp.PipePoint;
                return true;
            }

            for (int i = 0; i + 1 < pipe.points.Count; i++)
            {
                if (Vector2.Distance(world, PipeMidpoint(pipe, i)) > pick) continue;
                RecordPipe(pipe);
                pipe.points.Insert(i + 1, (pipe.points[i] + pipe.points[i + 1]) * 0.5f);
                ApplyPipe(pipe);
                dragPipe = pipe;
                dragPoint = i + 1;
                drag = DragOp.PipePoint;
                return true;
            }
            return false;
        }

        static void RecordPipe(PipeShape pipe) =>
            Undo.RecordObjects(new Object[] { pipe, pipe.GetComponent<Piece>() }, "Pipe Shape");

        void BeginMove(List<Piece> pieces, Piece pivot)
        {
            CollectMoveTargets(pieces);
            Undo.RecordObjects(moveTargets.ToArray(), "Move Pieces");
            moveStarts.Clear();
            foreach (var t in moveTargets) moveStarts.Add(t.position);
            movePivotStart = pivot.transform.position;
            drag = DragOp.Move;
        }

        /// <summary>The pieces plus the world-fixed clamps sitting on them, so clamps travel with their pipe.</summary>
        void CollectMoveTargets(List<Piece> pieces)
        {
            moveTargets.Clear();
            foreach (var p in pieces) moveTargets.Add(p.transform);
            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>())
                if (clamp.child != null && pieces.Contains(clamp.child)) moveTargets.Add(clamp.transform);
        }

        void UpdateDrag(Event e, Vector2 world)
        {
            bool free = EditorGUI.actionKey;
            switch (drag)
            {
                case DragOp.Pan:
                    viewCenter += new Vector2(-e.delta.x, e.delta.y) / zoom;
                    break;

                case DragOp.Move:
                    if (!dragMoved) break;
                    Vector2 target = movePivotStart + (world - dragStartWorld);
                    Vector2 delta = (free ? target : SnapPos(target)) - movePivotStart;
                    for (int i = 0; i < moveTargets.Count; i++)
                        if (moveTargets[i] != null) moveTargets[i].position = moveStarts[i] + (Vector3)delta;
                    RelayoutAll();
                    break;

                case DragOp.Arm:
                    if (dragArm == null) break;
                    var owner = dragArm.GetComponentInParent<Piece>();
                    AimArm(owner, world, free ? 0f : angleStep, out dragArm.angle, out dragArm.pathT, out dragArm.side);
                    dragArm.Layout();
                    break;

                case DragOp.Clamp:
                    if (dragClamp == null) break;
                    var pos = dragClamp.transform.position;
                    dragClamp.transform.position = new Vector3(world.x, world.y, pos.z);
                    dragClamp.Layout(); // snaps back onto its pipe
                    break;

                case DragOp.RingRadius:
                    if (dragRing == null) break;
                    float radius = Vector2.Distance(world, dragRing.transform.position);
                    SetRing(dragRing, free ? radius : Snap(radius, RadiusStep), dragRing.gapAngle);
                    break;

                case DragOp.RingGap:
                    if (dragRing == null) break;
                    Vector2 local = dragRing.transform.InverseTransformPoint(world);
                    float deg = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                    float gap = Mathf.Abs(Mathf.DeltaAngle(dragRing.gapCenter, deg)) * 2f;
                    SetRing(dragRing, dragRing.radius, free ? gap : Snap(gap, angleStep));
                    break;

                case DragOp.RingRotate:
                    if (dragRing == null) break;
                    Vector2 toMouse = world - (Vector2)dragRing.transform.position;
                    float facing = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg - dragRing.gapCenter;
                    dragRing.transform.rotation = Quaternion.Euler(0f, 0f, Snap(facing, free ? 0f : angleStep));
                    RelayoutAll();
                    break;

                case DragOp.PipePoint:
                    if (dragPipe == null) break;
                    Vector2 point = dragPipe.transform.InverseTransformPoint(free ? world : SnapPos(world));
                    if (TouchesNeighbour(dragPipe, dragPoint, point)) break;
                    dragPipe.points[dragPoint] = point;
                    ApplyPipe(dragPipe);
                    break;
            }
        }

        void EndDrag()
        {
            switch (drag)
            {
                case DragOp.Move:
                    if (dragMoved) MarkDirty();
                    else if (!dragAdditive && dragPiece != null) SelectPieces(new List<Piece> { dragPiece });
                    break;
                case DragOp.Arm:
                case DragOp.Clamp:
                case DragOp.RingRadius:
                case DragOp.RingGap:
                case DragOp.RingRotate:
                case DragOp.PipePoint:
                    MarkDirty();
                    break;
            }
            drag = DragOp.None;
            dragPiece = null;
            dragArm = null;
            dragClamp = null;
            dragRing = null;
            dragPipe = null;
        }

        bool HandleKey(KeyCode key)
        {
            float step = grid > 0f ? grid : 0.05f;
            switch (key)
            {
                case KeyCode.Q: RotateSelection(angleStep); return true;
                case KeyCode.E: RotateSelection(-angleStep); return true;
                case KeyCode.Delete:
                case KeyCode.Backspace: DeleteSelection(); return true;
                case KeyCode.F: FrameSelectionOrAll(); return true;
                case KeyCode.LeftArrow: MoveSelection(new Vector2(-step, 0f)); return true;
                case KeyCode.RightArrow: MoveSelection(new Vector2(step, 0f)); return true;
                case KeyCode.UpArrow: MoveSelection(new Vector2(0f, step)); return true;
                case KeyCode.DownArrow: MoveSelection(new Vector2(0f, -step)); return true;
                case KeyCode.Escape:
                    linkFrom = null;
                    return true;
            }
            return false;
        }

        void ZoomAt(Vector2 canvasPos, float factor)
        {
            Vector2 before = CanvasToWorld(canvasPos);
            zoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            viewCenter += before - CanvasToWorld(canvasPos);
            Repaint();
        }

        void FrameSelectionOrAll()
        {
            var selected = SelectedPieces();
            var pieces = selected.Count > 0 ? selected : new List<Piece>(root.GetComponentsInChildren<Piece>());
            bool any = false;
            var bounds = new Bounds();
            foreach (var p in pieces)
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            if (!any)
            {
                viewCenter = Vector2.zero;
                Repaint();
                return;
            }
            viewCenter = bounds.center;
            zoom = Mathf.Clamp(Mathf.Min(canvasSize.x / (bounds.size.x + 1f), canvasSize.y / (bounds.size.y + 1f)), MinZoom, MaxZoom);
            Repaint();
        }

        void RelayoutAll()
        {
            foreach (var link in root.GetComponentsInChildren<PieceLink>()) link.Layout();
            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>()) clamp.Layout();
            // The preview scene gets no LateUpdate, so shadows are moved here.
            foreach (var piece in root.GetComponentsInChildren<Piece>()) piece.SyncShadow();
            foreach (var arm in root.GetComponentsInChildren<PieceArm>()) arm.SyncShadow();
        }

        void SelectPieces(List<Piece> pieces)
        {
            selection.Clear();
            selection.AddRange(pieces);
            Repaint();
        }

        // ---------- Palette tiles ----------

        float PanelWidth => Ready ? panelWidth : position.width;

        void DrawTiles()
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((PanelWidth - 40f) / (TileW + 4f)));
            var presets = library.presets;
            for (int i = 0; i < presets.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + perRow, presets.Count); j++)
                        ShapeTile(presets[j]);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>Dragging out of the tile starts a drag-and-drop into the canvas; a plain click does nothing.</summary>
        void ShapeTile(ShapePreset preset)
        {
            Rect r = GUILayoutUtility.GetRect(TileW, TileH, GUILayout.Width(TileW), GUILayout.Height(TileH));
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                pressedTile = preset;
                tilePressPos = e.mousePosition;
            }
            else if (e.type == EventType.MouseDrag && pressedTile == preset && (e.mousePosition - tilePressPos).sqrMagnitude > 36f)
            {
                pressedTile = null;
                GUIUtility.hotControl = 0;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragKey, preset);
                DragAndDrop.objectReferences = new Object[0];
                DragAndDrop.StartDrag(preset.name);
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp && pressedTile == preset)
            {
                pressedTile = null;
            }

            GUI.Button(r, GUIContent.none);
            if (Event.current.type == EventType.Repaint)
                DrawShapeIcon(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, r.height - 22f), preset, style.GetColor(colorIndex));
            GUI.Label(new Rect(r.x + 2f, r.yMax - 17f, r.width - 4f, 15f), preset.name, tileLabel);
        }

        /// <summary>All presets share one scale (5.4 world units across a tile) so sizes compare at a glance.</summary>
        void DrawShapeIcon(Rect r, ShapePreset preset, Color color)
        {
            var shape = PresetShape(preset);
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

        // ---------- Canvas: click modes ----------

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
            AimArm(p, world, angleStep, out float angle, out float t, out float side);
            var arm = LevelBuilder.CreateArm(p, angle, style.armLength, t, side);
            Undo.RegisterCreatedObjectUndo(arm.gameObject, "Add Arm");
            MarkDirty();
        }

        /// <summary>Arm pointing toward a world point: rings get a (snapped) local angle, pipes a spot along the path and a side.</summary>
        static void AimArm(Piece p, Vector2 world, float step, out float angle, out float t, out float side)
        {
            Vector2 local = p.transform.InverseTransformPoint(world);
            angle = 0f;
            t = 0.5f;
            side = 1f;
            if (p.shape.TryGetRingCircle(out Vector2 c, out _))
            {
                Vector2 d = local - c;
                angle = Snap(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, step);
                return;
            }
            float s = p.shape.ClosestAlong(local, out Vector2 tangent);
            Vector2 onPath = p.shape.Evaluate(s, out _);
            side = Vector2.Dot(local - onPath, new Vector2(tangent.y, -tangent.x)) >= 0f ? 1f : -1f;
            t = s / Mathf.Max(1e-4f, p.shape.Length);
        }

        void ClickClamp(Vector2 world)
        {
            var p = PickPiece(world);
            if (p == null || p.shape.TryGetRingCircle(out _, out _)) return; // clamps go on pipes
            var clamp = LevelBuilder.CreateClamp(p, world, style, root);
            Undo.RegisterCreatedObjectUndo(clamp.gameObject, "Add Clamp");
            MarkDirty();
        }

        /// <summary>The clamp, link sleeve or arm at this spot, if any.</summary>
        Component HitAttachment(Vector2 world)
        {
            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>())
                if (Vector2.Distance(world, clamp.transform.position) < clamp.CollisionRadius)
                    return clamp;

            foreach (var link in root.GetComponentsInChildren<PieceLink>())
                if (link.sleeve != null && Vector2.Distance(world, link.sleeve.transform.position) < link.sleeveSize.x * 0.5f)
                    return link;

            foreach (var arm in root.GetComponentsInChildren<PieceArm>())
            {
                arm.GetSegment(out Vector2 a, out Vector2 b, out float pad);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(world - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                if (Vector2.Distance(world, a + ab * t) < pad * 1.5f) return arm;
            }
            return null;
        }

        void ClickDelete(Vector2 world)
        {
            var attachment = HitAttachment(world);
            if (attachment != null)
            {
                Undo.DestroyObjectImmediate(attachment.gameObject);
                LevelBuilder.AssignSortingOrders(root); // a removed link changes the draw order
                MarkDirty();
                return;
            }

            var piece = PickPiece(world);
            if (piece == null) return;
            DeletePiece(piece);
            LevelBuilder.AssignSortingOrders(root);
            MarkDirty();
        }

        /// <summary>Removes the piece and everything that points at it (links gripping it, clamps on it).</summary>
        void DeletePiece(Piece piece)
        {
            foreach (var link in root.GetComponentsInChildren<PieceLink>(true))
                if (link.child == piece) Undo.DestroyObjectImmediate(link.gameObject);
            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>(true))
                if (clamp.child == piece) Undo.DestroyObjectImmediate(clamp.gameObject);
            if (linkFrom == piece) linkFrom = null;
            Undo.DestroyObjectImmediate(piece.gameObject);
        }

        // ---------- Actions ----------

        void Place(ShapePreset preset, Vector2 pos)
        {
            var data = new PieceData
            {
                id = preset.name.Replace(" ", "_"),
                kind = preset.kind,
                pos = pos,
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
            SelectPieces(new List<Piece> { piece });
            MarkDirty();
        }

        /// <summary>Reshapes a ring; its arms and every link touching it follow. Closing the gap fully makes it a closed ring.</summary>
        void SetRing(RingShape ring, float radius, float gap)
        {
            ring.radius = Mathf.Clamp(radius, MinRadius, MaxRadius);
            ring.gapAngle = Mathf.Clamp(gap, 0f, MaxGap);
            ring.Apply();
            ring.GetComponent<Piece>().Rebuild();
            RelayoutAll();
        }

        /// <summary>Rebuilds a pipe after its points changed; arms, links and clamps on it follow.</summary>
        void ApplyPipe(PipeShape pipe)
        {
            pipe.Apply();
            pipe.GetComponent<Piece>().Rebuild();
            RelayoutAll();
        }

        /// <summary>Two corners on the same spot would leave a zero-length leg with no direction.</summary>
        static bool TouchesNeighbour(PipeShape pipe, int index, Vector2 point)
        {
            bool Near(int i) => i >= 0 && i < pipe.points.Count && Vector2.Distance(point, pipe.points[i]) < MinPipeSegment;
            return Near(index - 1) || Near(index + 1);
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

        void RotatePiece(Piece piece, float degrees)
        {
            SelectPieces(new List<Piece> { piece });
            RotateSelection(degrees);
        }

        void RotateSelection(float degrees)
        {
            var selected = SelectedPieces();
            if (selected.Count == 0) return;
            foreach (var p in selected)
            {
                Undo.RecordObject(p.transform, "Rotate");
                p.transform.rotation = Quaternion.Euler(0f, 0f, Snap(p.transform.eulerAngles.z + degrees, angleStep));
            }
            RelayoutAll();
            MarkDirty();
        }

        void MoveSelection(Vector2 delta)
        {
            var selected = SelectedPieces();
            if (selected.Count == 0) return;
            CollectMoveTargets(selected);
            Undo.RecordObjects(moveTargets.ToArray(), "Nudge");
            foreach (var t in moveTargets) t.position += (Vector3)delta;
            RelayoutAll();
            MarkDirty();
        }

        void DeleteSelection()
        {
            var selected = SelectedPieces();
            if (selected.Count == 0) return;
            foreach (var p in selected)
                if (p != null) DeletePiece(p);
            hover = null;
            LevelBuilder.AssignSortingOrders(root);
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

        void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("Clear All", "Remove every piece from the draft?", "Clear", "Cancel")) return;
            ClearRoot();
        }

        void ClearRoot()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
            linkFrom = null;
            hover = null;
            selection.Clear();
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
            ReplaceDraft(level);
            levelName = Path.GetFileNameWithoutExtension(full);
            FrameSelectionOrAll();
        }

        void ReplaceDraft(LevelData level)
        {
            ClearRoot();
            BuildUndoable(level, root, "Load Level");
            MarkDirty();
        }

        void BuildUndoable(LevelData level, Transform target, string undoName)
        {
            var map = LevelBuilder.Build(level, style, target);
            foreach (var p in map.Values) Undo.RegisterCreatedObjectUndo(p.gameObject, undoName);
            foreach (var c in target.GetComponentsInChildren<PipeClamp>(true)) Undo.RegisterCreatedObjectUndo(c.gameObject, undoName);
        }

        // ---------- Draft <-> scene ----------

        void OpenDraft()
        {
            if (!AssetsReady || Playing) return;
            draftScene = EditorSceneManager.NewPreviewScene();
            var go = new GameObject(DraftRootName);
            SceneManager.MoveGameObjectToScene(go, draftScene);
            root = go.transform;

            LevelData level = null;
            if (File.Exists(DraftPath)) level = JsonUtility.FromJson<LevelData>(File.ReadAllText(DraftPath));
            else if (FindSceneRoot() is { } sceneRoot) level = LevelBuilder.Export(sceneRoot, style, levelName);
            if (level != null) LevelBuilder.Build(level, style, root);
        }

        void CloseDraft()
        {
            if (root == null) return;
            if (style != null) File.WriteAllText(DraftPath, JsonUtility.ToJson(LevelBuilder.Export(root, style, levelName), true));
            root = null;
            hover = null;
            linkFrom = null;
            selection.Clear();
            EditorSceneManager.ClosePreviewScene(draftScene);
        }

        /// <summary>Rebuilds the scene's Level root from the draft, as one undo step.</summary>
        void ApplyToScene()
        {
            var sceneRoot = FindSceneRoot();
            if (sceneRoot == null)
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (!EditorUtility.DisplayDialog("Apply to Scene", $"Scene '{sceneName}' has no 'Level' root. Create one here (with a play-test camera)?", "Create", "Cancel"))
                    return;
                sceneRoot = CreateRoot();
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Level");
            int group = Undo.GetCurrentGroup();
            for (int i = sceneRoot.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(sceneRoot.GetChild(i).gameObject);
            BuildUndoable(LevelBuilder.Export(root, style, levelName), sceneRoot, "Apply Level");
            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(sceneRoot.gameObject.scene);
            unapplied = false;
            SceneView.RepaintAll();
            Debug.Log($"[LevelEditor] Applied the draft to '{sceneRoot.gameObject.scene.name}'.");
        }

        void PullFromScene()
        {
            var sceneRoot = FindSceneRoot();
            if (sceneRoot == null)
            {
                Debug.LogWarning("[LevelEditor] This scene has no 'Level' root.");
                return;
            }
            if (unapplied && !EditorUtility.DisplayDialog("From Scene", "The draft has changes not applied to the scene. Replace it anyway?", "Replace", "Cancel"))
                return;
            ReplaceDraft(LevelBuilder.Export(sceneRoot, style, levelName));
            unapplied = false;
            FrameSelectionOrAll();
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
        void RegenerateSolvable(int attempts, float seconds)
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
                for (int a = 0; a < attempts; a++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Regenerate Solvable",
                            $"Attempt {a + 1}/{attempts}  ·  solvable so far: {found}", (float)a / attempts))
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
                    var result = solver.Solve(seconds);
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
                solveResult = new LevelSolver(root).Solve(seconds);
                solveResult.note = "Nothing solvable found, rotations restored. " + stats +
                                   (unsolvable == 0 && found == 0 && unknown > 0 ? " Try a larger Max sec." : " The layout itself may be blocked.");
            }
            showSolution = false;
            Debug.Log("[LevelEditor] " + solveResult.Summary);
        }

        /// <summary>New random layouts until one can be solved; the rotations come from the same re-roll as Regenerate.</summary>
        void GenerateSample()
        {
            if (genRings + genPipes == 0) return;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Sample");
            int group = Undo.GetCurrentGroup();
            for (int i = 0; i < GenLayouts; i++)
            {
                ReplaceDraft(LevelGenerator.Generate(genRings, genPipes, style));
                if (RerollTargets().Count > 0) RegenerateSolvable(GenAttempts, GenSolveSeconds);
                else CheckSolvable(); // nothing to re-roll: only the layout can change
                if (solveResult.solved) break;
            }
            Undo.CollapseUndoOperations(group);
            FrameSelectionOrAll();
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
            selection.RemoveAll(p => p == null); // deleted by an undo
            return new List<Piece>(selection);
        }

        /// <summary>The shape whose handles are showing: exactly one piece selected, and it has this shape component.</summary>
        T SelectedShape<T>() where T : Component
        {
            var selected = SelectedPieces();
            return selected.Count == 1 && selected[0].TryGetComponent<T>(out var shape) ? shape : null;
        }

        static float Snap(float v, float step) => step > 0f ? Mathf.Round(v / step) * step : v;

        Vector2 SnapPos(Vector2 v) => new(Snap(v.x, grid), Snap(v.y, grid));

        static Transform FindSceneRoot()
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
                cam.backgroundColor = DefaultBackground;
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                camGo.AddComponent<PieceInput>().cam = cam;
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            }
            return go.transform;
        }

        void MarkDirty()
        {
            unapplied = true;
            Repaint();
        }
    }
}
