using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RotateRings.EditorTools
{
    /// <summary>Tools > Rotate Rings > Build Test Scene: sets up imports, materials and a 4-ring test level.</summary>
    public static class RingSceneBuilder
    {
        const string Root = "Assets/01_RotateRings";
        const string Tex = Root + "/Art/Textures";
        const string MatDir = Root + "/Materials";
        const string ScenePath = Root + "/Scenes/RingTest.unity";

        static readonly Color Blue = new(0.12f, 0.64f, 0.88f);
        static readonly Color Orange = new(0.95f, 0.55f, 0.12f);
        static readonly Color Purple = new(0.56f, 0.27f, 0.91f);
        static readonly Color Red = new(0.89f, 0.19f, 0.18f);
        static readonly Color Green = new(0.24f, 0.72f, 0.3f);
        static readonly Color DarkBlue = new(0.22f, 0.4f, 0.86f);
        static readonly Color LightBlue = new(0.3f, 0.75f, 0.95f);

        const string ShowcasePath = Root + "/Scenes/RingShowcase.unity";
        const string PipeShowcasePath = Root + "/Scenes/PipeShowcase.unity";

        public const string StylePath = Root + "/Configs/PieceStyle.asset";
        public const string LibraryPath = Root + "/Configs/ShapeLibrary.asset";

        struct Mats
        {
            public Material Ring, Shadow, Break, Link, Outline, Spark, Clamp;
        }

        /// <summary>Creates (or refreshes) the PieceStyle and ShapeLibrary assets used by the level editor and loader.</summary>
        [MenuItem("Tools/Rotate Rings/Create Default Style + Library")]
        public static void CreateDefaultAssetsMenu() => EnsureDefaultAssets(out _, out _);

        public static void EnsureDefaultAssets(out PieceStyle style, out ShapeLibrary library)
        {
            var mats = SetupAssets();
            System.IO.Directory.CreateDirectory(Root + "/Configs");

            style = AssetDatabase.LoadAssetAtPath<PieceStyle>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<PieceStyle>();
                style.palette = new System.Collections.Generic.List<Color>
                {
                    Blue, Orange, Purple, Red, Green, DarkBlue, LightBlue,
                    new(0.98f, 0.78f, 0.15f), // yellow
                    new(0.95f, 0.42f, 0.65f), // pink
                };
                AssetDatabase.CreateAsset(style, StylePath);
            }
            style.body = mats.Ring;
            style.shadow = mats.Shadow;
            style.outline = mats.Outline;
            style.breakFx = mats.Break;
            style.spark = mats.Spark;
            style.link = mats.Link;
            style.clamp = mats.Clamp;
            style.linkSprite = Load<Sprite>(LinkBlockFile);
            style.clampSprite = Load<Sprite>("clamp04.png");
            EditorUtility.SetDirty(style);

            library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ShapeLibrary>();
                library.ResetToDefaults();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[RotateRings] Style: {StylePath}  Library: {LibraryPath}");
        }

        static Mats SetupAssets()
        {
            SetupImports();
            var mats = new Mats();
            mats.Ring = CreateMaterial("Ring", "RotateRings/Ring", m =>
            {
                m.SetTexture("_MainTex", Load<Texture2D>("RingFinal.png"));
                m.SetTexture("_NormalMap", Load<Texture2D>("RingFinal_N.png"));
                m.SetFloat("_Specular", 0.08f);
            });
            mats.Shadow = CreateMaterial("RingShadow", "RotateRings/RingShadow", m =>
            {
                m.SetTexture("_MainTex", Load<Texture2D>("RingFinal.png"));
                m.SetColor("_ShadowColor", new Color(0.45f, 0.33f, 0.2f, 0.18f));
            });
            // Same recolor shader as the ring so chunks keep the ring's bright color (particle color = vertex color).
            mats.Break = CreateMaterial("RingBreak", "RotateRings/Ring", m =>
            {
                m.SetTexture("_MainTex", Load<Texture2D>("tex_sequence_011.png"));
                m.SetFloat("_MidGray", 0.6f);
                m.SetFloat("_ShadeStrength", 0.7f);
                m.SetFloat("_Specular", 0f);
                m.SetFloat("_AlphaClip", 0.3f);
            });
            // SpriteRenderer feeds _MainTex; PieceLink sets _Color.
            mats.Link = CreateMaterial("RingLink", "RotateRings/Ring", m => m.SetFloat("_Specular", 0f));
            // Flat color: no shading, no highlight.
            mats.Outline = CreateMaterial("RingOutline", "RotateRings/Ring", m =>
            {
                m.SetTexture("_MainTex", Load<Texture2D>("RingFinal.png"));
                m.SetFloat("_ShadeStrength", 0f);
                m.SetFloat("_Specular", 0f);
                m.SetFloat("_AlphaClip", 0.4f);
            });
            mats.Spark = CreateMaterial("RingSpark", "RotateRings/ParticleAlpha", m =>
                m.SetTexture("_MainTex", Load<Texture2D>("tex_star_005.png")));
            // Metal clamp keeps its own colors.
            mats.Clamp = CreateMaterial("PipeClamp", "Sprites/Default", null);
            return mats;
        }

        [MenuItem("Tools/Rotate Rings/Update Materials (open scene)")]
        public static void UpdateMaterials()
        {
            var mats = SetupAssets();
            foreach (var r in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(r, "Update Ring Materials");
                AssignMaterials(r, mats);
                r.Rebuild();
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RotateRings] Materials updated.");
        }

        static void AssignMaterials(Piece r, Mats mats)
        {
            r.bodyMaterial = mats.Ring;
            r.shadowMaterial = mats.Shadow;
            r.breakMaterial = mats.Break;
            r.outlineMaterial = mats.Outline;
            r.sparkMaterial = mats.Spark;
        }

        [MenuItem("Tools/Rotate Rings/Build Test Scene")]
        public static void Build()
        {
            var mats = SetupAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(6.5f, new Vector2(0.12f, 0f));

            // Layout traced from the reference shot (ring radius 45px = 1.25u, origin at the picture center).
            var blue = MakeRing("Ring_Blue", new Vector2(-0.28f, 2.92f), 90f, Blue, 0, mats);
            var orange = MakeRing("Ring_Orange", new Vector2(-1.67f, 0f), 180f, Orange, 10, mats);
            var purple = MakeRing("Ring_Purple", new Vector2(1.6f, 0.25f), 180f, Purple, 10, mats);
            var red = MakeRing("Ring_Red", new Vector2(-0.14f, -2.92f), 270f, Red, 20, mats);

            var stemSprite = Load<Sprite>(LinkBlockFile);
            MakeLink(blue, orange, stemSprite, stemSprite, mats.Link);
            MakeLink(blue, purple, stemSprite, stemSprite, mats.Link);
            MakeLink(orange, red, stemSprite, stemSprite, mats.Link);

            foreach (var r in new[] { blue, orange, purple, red }) r.Rebuild();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[RotateRings] Test scene built: {ScenePath}");
        }

        /// <summary>Every ring variant the current system supports, labeled, on one screen.</summary>
        [MenuItem("Tools/Rotate Rings/Build Ring Showcase")]
        public static void BuildShowcase()
        {
            var mats = SetupAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(10f, Vector2.zero);

            var stemSprite = Load<Sprite>(LinkBlockFile);

            // Row 1: gap sizes.
            Labeled(MakeRing("C_Gap70", new Vector2(-3.6f, 7.5f), 90f, Blue, 0, mats), "C  gap 70°");
            Labeled(MakeRing("C_Gap30", new Vector2(0f, 7.5f), 0f, Orange, 0, mats, gapAngle: 30f), "C  gap 30°");
            Labeled(MakeRing("U_Gap120", new Vector2(3.6f, 7.5f), 270f, Purple, 0, mats, gapAngle: 120f), "U  gap 120°");

            // Row 2: closed, small, rim style.
            Labeled(MakeRing("Closed", new Vector2(-3.6f, 3.6f), 90f, Green, 0, mats, gapAngle: 0f), "Closed  gap 0");
            Labeled(MakeRing("Small_R08", new Vector2(0f, 3.6f), 180f, Red, 0, mats, radius: 0.8f), "Small  r 0.8");
            Labeled(MakeRing("Style_Tube", new Vector2(3.6f, 3.6f), 90f, LightBlue, 0, mats, styleRow: 1), "Style row 1 (tube)");

            // Row 3: a chain - closed ring holds a C, the C holds another C.
            var chainA = MakeRing("Chain_Closed", new Vector2(-3.2f, -0.4f), 90f, DarkBlue, 0, mats, gapAngle: 0f);
            var chainB = MakeRing("Chain_C1", new Vector2(-0.1f, -0.4f), 90f, Orange, 10, mats);
            var chainC = MakeRing("Chain_C2", new Vector2(3.0f, -0.4f), 270f, Red, 20, mats);
            MakeLink(chainA, chainB, stemSprite, stemSprite, mats.Link);
            MakeLink(chainB, chainC, stemSprite, stemSprite, mats.Link);
            Labeled(chainB, "Chain: closed > C > C");

            // Row 4: big ring holding a small ring inside it.
            var big = MakeRing("Big_R24", new Vector2(0f, -6f), 90f, Green, 0, mats, radius: 2.4f, gapAngle: 40f);
            var inner = MakeRing("Inner_R08", new Vector2(0f, -6.8f), 90f, Purple, 10, mats, radius: 0.8f);
            MakeLink(big, inner, stemSprite, stemSprite, mats.Link);
            Label("Big r 2.4  gap 40°  +  inner ring", new Vector2(0f, -3.0f));

            foreach (var p in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None)) p.Rebuild();

            EditorSceneManager.SaveScene(scene, ShowcasePath);
            Debug.Log($"[RotateRings] Showcase built: {ShowcasePath}");
        }

        /// <summary>Pipes (I, L, U, S sliding through metal clamps) and rings with rigid arms.</summary>
        [MenuItem("Tools/Rotate Rings/Build Pipe Showcase")]
        public static void BuildPipeShowcase()
        {
            var mats = SetupAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(10f, Vector2.zero);

            var stemSprite = Load<Sprite>(LinkBlockFile);
            var clampSprite = Load<Sprite>("clamp04.png");

            // Row 1: pipe shapes, each in one clamp.
            var straight = MakePipe("Pipe_I", new Vector2(-3.8f, 7f), Orange, mats,
                new Vector2(0f, -1.5f), new Vector2(0f, 1.5f));
            MakeClamp(straight, new Vector2(-3.8f, 7f), clampSprite, mats.Clamp);
            Label("I: slides up/down, off either end", new Vector2(-3.8f, 5.0f));

            // L: clamp on the horizontal leg. Pipe moving left frees it; moving right runs the bend into the clamp.
            var lPipe = MakePipe("Pipe_L", new Vector2(0.6f, 6.4f), Green, mats,
                new Vector2(-1.2f, 1.4f), new Vector2(-1.2f, 0f), new Vector2(1.4f, 0f));
            MakeClamp(lPipe, new Vector2(1.2f, 6.4f), clampSprite, mats.Clamp);
            Label("L: left = free, right = blocked", new Vector2(0.6f, 5.0f));

            // U: clamp on the left leg. Down frees it, up hits the bend.
            var uPipe = MakePipe("Pipe_U", new Vector2(3.9f, 6.2f), LightBlue, mats,
                new Vector2(-0.8f, 1.6f), new Vector2(-0.8f, 0f), new Vector2(0.8f, 0f), new Vector2(0.8f, 1.6f));
            MakeClamp(uPipe, new Vector2(3.1f, 7.2f), clampSprite, mats.Clamp);
            Label("U: down = free, up = blocked", new Vector2(3.9f, 5.0f));

            // Row 2: S pipe, and a ring with rigid arms.
            var sPipe = MakePipe("Pipe_S", new Vector2(-2.4f, 2.4f), Red, mats,
                new Vector2(-1.2f, 1.4f), new Vector2(-1.2f, 0f), new Vector2(1.2f, 0f), new Vector2(1.2f, -1.4f));
            MakeClamp(sPipe, new Vector2(-3.6f, 3.3f), clampSprite, mats.Clamp);
            Label("S: down = free, up = blocked", new Vector2(-2.4f, 0.4f));

            var armRing = MakeRing("Ring_Arms", new Vector2(2.6f, 2.4f), 90f, Purple, 0, mats);
            MakeArm(armRing, 225f, 0.9f);
            MakeArm(armRing, -30f, 0.7f);
            Labeled(armRing, "Ring + 2 arms (free: breaks on release)");

            // Row 3: arm blocking puzzle. The C is held by the closed ring; turning it clockwise frees it, but its
            // arm hits the orange pipe, and counter-clockwise the arm hits the closed ring. Slide the pipe out first.
            var holder = MakeRing("Holder_Closed", new Vector2(-3.2f, -3.2f), 90f, DarkBlue, 0, mats, gapAngle: 0f);
            var armed = MakeRing("Armed_C", new Vector2(-0.1f, -3.2f), 270f, Green, 10, mats);
            MakeLink(holder, armed, stemSprite, stemSprite, mats.Link);
            MakeArm(armed, 30f, 0.9f);
            var blocker = MakePipe("Blocker_I", new Vector2(2.35f, -3.2f), Orange, mats,
                new Vector2(0f, -1.8f), new Vector2(0f, 1.6f));
            MakeClamp(blocker, new Vector2(2.35f, -4.2f), clampSprite, mats.Clamp);
            Label("Puzzle: slide the pipe out, then turn the green C clockwise", new Vector2(0f, -6.2f));

            foreach (var p in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None)) p.Rebuild();
            foreach (var c in Object.FindObjectsByType<PipeClamp>(FindObjectsSortMode.None)) c.Layout();

            EditorSceneManager.SaveScene(scene, PipeShowcasePath);
            Debug.Log($"[RotateRings] Pipe showcase built: {PipeShowcasePath}");
        }

        static Piece MakePipe(string name, Vector2 pos, Color color, Mats mats, params Vector2[] points)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var p = go.AddComponent<Piece>();
            p.color = color;
            AssignMaterials(p, mats);
            var pipe = go.AddComponent<PipeShape>();
            pipe.points = new System.Collections.Generic.List<Vector2>(points);
            pipe.Apply();
            return p;
        }

        static void MakeClamp(Piece pipe, Vector2 worldPos, Sprite sprite, Material mat)
        {
            var go = new GameObject($"Clamp_{pipe.name}");
            go.transform.position = worldPos;
            var clamp = go.AddComponent<PipeClamp>();
            clamp.child = pipe;
            clamp.sprite = MakeSprite("Sprite", go.transform, sprite, mat);
            clamp.Layout();
        }

        static void MakeArm(Piece ring, float angle, float length)
        {
            var go = new GameObject($"Arm_{angle:0}");
            go.transform.SetParent(ring.transform, false);
            var arm = go.AddComponent<PieceArm>();
            arm.angle = angle;
            arm.length = length;
        }

        static void MakeCamera(float orthoSize, Vector2 pos)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
            camGo.transform.position = new Vector3(pos.x, pos.y, -10f);
            camGo.AddComponent<PieceInput>().cam = cam;
        }

        static void Labeled(Piece p, string text)
        {
            var ring = p.GetComponent<RingShape>();
            float r = ring != null ? ring.radius : 1.25f;
            Label(text, (Vector2)p.transform.position + Vector2.down * (r + p.thickness + 0.35f));
        }

        static void Label(string text, Vector2 pos)
        {
            var go = new GameObject("Label_" + text);
            go.transform.position = pos;
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 3.5f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.35f, 0.3f, 0.25f);
            tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
            tmp.sortingOrder = 500;
        }

        static Piece MakeRing(string name, Vector2 pos, float gapCenter, Color color, int order, Mats mats,
            float radius = 1.25f, float gapAngle = 70f, int styleRow = 0)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var r = go.AddComponent<Piece>();
            r.color = color;
            r.sortingOrder = order;
            r.styleRow = styleRow;
            AssignMaterials(r, mats);
            var ring = go.AddComponent<RingShape>();
            ring.radius = radius;
            ring.gapAngle = gapAngle;
            ring.gapCenter = gapCenter;
            ring.Apply();
            return r;
        }

        static void MakeLink(Piece parent, Piece child, Sprite stemSprite, Sprite sleeveSprite, Material mat)
        {
            var go = new GameObject($"Link_{child.name}");
            go.transform.SetParent(parent.transform, false);
            var link = go.AddComponent<PieceLink>();
            link.child = child;
            link.stem = MakeSprite("Stem", go.transform, stemSprite, mat);
            link.sleeve = MakeSprite("Sleeve", go.transform, sleeveSprite, mat);
            link.Layout();
        }

        /// <summary>Sprites with a 9-slice border (LinkBlock) are drawn sliced so they resize without stretching corners.</summary>
        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            if (sprite != null && sprite.border != Vector4.zero) sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        // ---------- Assets ----------

        const string LinkBlockFile = "LinkBar.png";
        const int BlockSize = 128;
        const int BlockRadius = 32;          // rim profile width = 9-slice border, px
        const float RingTexelsPerUnit = 64f / 0.34f; // RingFinal capsule is 64px for a 0.34 thick ring

        /// <summary>
        /// Builds a straight bar in the same style as the ring body: the rim-to-face brightness profile is read
        /// across RingFinal.png's row 0 capsule and applied from the top and bottom edges only. The left/right ends
        /// are cut flat, like a slice of the ring itself.
        /// Used 9-sliced (top/bottom border only) for link stems and sleeves; x runs along the bar.
        /// </summary>
        static void GenerateLinkBlock()
        {
            string ringPath = $"{Tex}/RingFinal.png";
            if (!System.IO.File.Exists(ringPath)) return;

            var src = new Texture2D(2, 2);
            src.LoadImage(System.IO.File.ReadAllBytes(ringPath));
            // Row 0 capsule spans image rows 32..96 from the top; sample its center column from the edge (d=0) to the middle (d=32).
            var profile = new Color[BlockRadius + 1];
            for (int d = 0; d <= BlockRadius; d++)
                profile[d] = src.GetPixelBilinear(0.5f, 1f - (32f + d + 0.5f) / src.height);
            Object.DestroyImmediate(src);

            var tex = new Texture2D(BlockSize, BlockSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < BlockSize; y++)
            {
                // Distance from the nearer of the top/bottom edges; the profile is flat past the rim.
                float inside = Mathf.Min(Mathf.Min(y + 0.5f, BlockSize - y - 0.5f), BlockRadius);
                int i = Mathf.FloorToInt(inside);
                Color c = Color.Lerp(profile[i], profile[Mathf.Min(i + 1, BlockRadius)], inside - i);
                for (int x = 0; x < BlockSize; x++) tex.SetPixel(x, y, c);
            }

            string path = $"{Tex}/{LinkBlockFile}";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        static void SetupImports()
        {
            GenerateLinkBlock();
            Import(LinkBlockFile, ti =>
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = RingTexelsPerUnit; // rim width matches the ring body
                ti.spriteBorder = new Vector4(0f, BlockRadius, 0f, BlockRadius); // left, bottom, right, top
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; // required for sliced drawing
                ti.SetTextureSettings(settings);
            });
            Import("RingFinal.png", ti => { ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = true; ti.alphaIsTransparency = true; });
            Import("RingFinal_N.png", ti => { ti.textureType = TextureImporterType.NormalMap; ti.mipmapEnabled = true; });
            Import("tex_sequence_011.png", ti => { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; });
            Import("tex_star_005.png", ti => { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; });
            Import("catch.png", ti => { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; });
            Import("clamp_06_1.png", ti => { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; });
            Import("clamp04.png", ti => { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; });
        }

        static void Import(string file, System.Action<TextureImporter> setup)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath($"{Tex}/{file}");
            if (ti == null) { Debug.LogError($"[RotateRings] Missing texture {file}"); return; }
            setup(ti);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        static Material CreateMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError($"[RotateRings] Shader not found: {shaderName}"); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            setup?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{Tex}/{file}");
    }
}
