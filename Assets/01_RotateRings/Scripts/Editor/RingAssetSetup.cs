using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Texture imports, materials, sprites cut from the atlases, and the PieceStyle / ShapeLibrary assets.
    /// Run from the Level Editor ("Rebuild Assets"); also refreshes every piece and link in the open scene.
    /// </summary>
    public static class RingAssetSetup
    {
        const string Root = "Assets/01_RotateRings";
        const string Tex = Root + "/Art/Textures";
        const string MatDir = Root + "/Materials";

        public const string StylePath = Root + "/Configs/PieceStyle.asset";
        public const string LibraryPath = Root + "/Configs/ShapeLibrary.asset";
        public const string EditorScenePath = Root + "/Scenes/LevelEditor.unity";

        static readonly Color[] DefaultPalette =
        {
            new(0.12f, 0.64f, 0.88f), // blue
            new(0.95f, 0.55f, 0.12f), // orange
            new(0.56f, 0.27f, 0.91f), // purple
            new(0.89f, 0.19f, 0.18f), // red
            new(0.24f, 0.72f, 0.3f),  // green
            new(0.22f, 0.4f, 0.86f),  // dark blue
            new(0.3f, 0.75f, 0.95f),  // light blue
            new(0.98f, 0.78f, 0.15f), // yellow
            new(0.95f, 0.42f, 0.65f), // pink
            new(0.12f, 0.57f, 0.36f), // dark green
        };

        struct Mats
        {
            public Material Ring, Wood, Marble, Shadow, Break, Link, Outline, Spark, Clamp;
        }

        /// <summary>Creates or refreshes every asset, then re-applies them to the pieces in the open scene.</summary>
        public static void Rebuild(out PieceStyle style, out ShapeLibrary library)
        {
            var mats = SetupMaterials();
            System.IO.Directory.CreateDirectory(Root + "/Configs");

            style = AssetDatabase.LoadAssetAtPath<PieceStyle>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<PieceStyle>();
                style.palette = new System.Collections.Generic.List<Color>(DefaultPalette);
                AssetDatabase.CreateAsset(style, StylePath);
            }
            style.body = mats.Ring;
            style.shadow = mats.Shadow;
            style.outline = mats.Outline;
            style.breakFx = mats.Break;
            style.spark = mats.Spark;
            style.link = mats.Link;
            style.clamp = mats.Clamp;
            style.linkSprite = Load<Sprite>(BlockPlastic);
            style.sleeveSprite = Load<Sprite>(ClipFile);
            style.sleeveMaterial = mats.Clamp;
            style.clipSize = ClipSize;
            style.clampSprite = Load<Sprite>("clamp04.png");
            style.finishes = new System.Collections.Generic.List<PieceStyle.FinishLook>
            {
                new() { finish = Finish.Plastic, body = mats.Ring, linkSprite = Load<Sprite>(BlockPlastic), styleRow = style.styleRow },
                new() { finish = Finish.Wood, body = mats.Wood, linkSprite = Load<Sprite>(BlockWood), styleRow = 0 },
                new() { finish = Finish.Marble, body = mats.Marble, linkSprite = Load<Sprite>(BlockMarble), styleRow = 0 },
            };
            EditorUtility.SetDirty(style);

            library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ShapeLibrary>();
                library.ResetToDefaults();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            AssetDatabase.SaveAssets();

            RefreshScene(style);
            Debug.Log($"[RotateRings] Assets rebuilt. Style: {StylePath}  Library: {LibraryPath}");
        }

        /// <summary>Re-applies materials, finishes and link looks to everything in the open scene (colors kept).</summary>
        static void RefreshScene(PieceStyle style)
        {
            foreach (var p in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(p, "Rebuild Assets");
                foreach (var link in p.GetComponentsInChildren<PieceLink>(true))
                {
                    Undo.RecordObject(link, "Rebuild Assets");
                    if (link.stem != null) Undo.RecordObject(link.stem, "Rebuild Assets");
                    if (link.sleeve != null) Undo.RecordObject(link.sleeve, "Rebuild Assets");
                }
                p.shadowMaterial = style.shadow;
                p.outlineMaterial = style.outline;
                p.breakMaterial = style.breakFx;
                p.sparkMaterial = style.spark;
                style.ApplyFinish(p, p.finish); // also restyles the links it owns
                p.Rebuild();
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static Mats SetupMaterials()
        {
            SetupImports();
            var mats = new Mats();
            mats.Ring = CreateMaterial("Ring", "RotateRings/Ring", m =>
            {
                m.SetTexture("_MainTex", Load<Texture2D>("RingFinal.png"));
                m.SetTexture("_NormalMap", Load<Texture2D>("RingFinal_N.png"));
                m.SetFloat("_Specular", 0.08f);
            });
            // Wood / marble: grey grain tinted with the piece color, body slice repeated instead of stretched.
            mats.Wood = CreateMaterial("RingWood", "RotateRings/Ring", m =>
            {
                SetExtraAtlas(m, WoodAtlas);
                m.SetTexture("_NormalMap", Load<Texture2D>(ExtraNormal));
                m.SetTextureScale("_NormalMap", ExtraTiling);
                m.SetTextureOffset("_NormalMap", ExtraOffset);
                m.SetFloat("_MidGray", MatchedMidGray(WoodAtlas));
                m.SetFloat("_Specular", 0.05f);
            });
            // Marble's profile is a round tube, so the flat-rim normal map doesn't fit: no highlight.
            mats.Marble = CreateMaterial("RingMarble", "RotateRings/Ring", m =>
            {
                SetExtraAtlas(m, MarbleAtlas);
                m.SetFloat("_MidGray", MatchedMidGray(MarbleAtlas));
                m.SetFloat("_Specular", 0f);
            });
            // Soft shadow from the distance field (same capsule layout as every RingExtra atlas).
            mats.Shadow = CreateMaterial("RingShadow", "RotateRings/RingShadow", m =>
            {
                m.SetTexture("_MainTex", Load<Texture2D>(ExtraSdf));
                m.SetTextureScale("_MainTex", ExtraTiling);
                m.SetTextureOffset("_MainTex", ExtraOffset);
                m.SetFloat("_UseSDF", 1f);
                m.SetColor("_ShadowColor", new Color(0.45f, 0.33f, 0.2f, 0.22f));
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
            // Metal clamp and sleeve keep their own colors.
            mats.Clamp = CreateMaterial("PipeClamp", "Sprites/Default", null);
            return mats;
        }

        // ---------- Textures ----------

        // RingExtra atlases (1024x128): capsule body x32..480, square link block x576..704, metal clip x795..868.
        // Capsule is 64px thick like RingFinal, so PathMesh UVs map onto it with tiling (0.5, 2) / offset (0, -1).
        const string ExtraAtlas = "RingExtra.png";
        const string WoodAtlas = "RingWoodExtra.png";
        const string MarbleAtlas = "RingMarbleExtra.png";
        const string ExtraNormal = "RingExtra_N.png";
        const string ExtraSdf = "RingExtra_SDF.png";
        static readonly Vector2 ExtraTiling = new(0.5f, 2f);
        static readonly Vector2 ExtraOffset = new(0f, -1f);

        // Cut out of the atlases (rects in px from the top-left, with a little transparent padding).
        const string BlockPlastic = "LinkBlock_Plastic.png";
        const string BlockWood = "LinkBlock_Wood.png";
        const string BlockMarble = "LinkBlock_Marble.png";
        const string ClipFile = "LinkClip.png";
        static readonly RectInt BlockRect = new(574, 29, 132, 70);
        static readonly RectInt ClipRect = new(793, 11, 78, 107);
        const int BlockEndBorder = 12; // 9-slice: the bevelled ends keep their size, the middle stretches

        const float RingTexelsPerUnit = 64f / 0.34f; // capsules are 64px for a 0.34 thick body
        /// <summary>Clip at its native scale next to a 0.34 body: x along the gripped piece, y across it.</summary>
        static Vector2 ClipSize => new Vector2(ClipRect.width, ClipRect.height) / RingTexelsPerUnit;

        static void SetupImports()
        {
            foreach (var atlas in new[] { ExtraAtlas, WoodAtlas, MarbleAtlas })
                Import(atlas, ti => { ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = true; ti.alphaIsTransparency = true; });
            Import(ExtraNormal, ti => { ti.textureType = TextureImporterType.NormalMap; ti.mipmapEnabled = true; });
            Import(ExtraSdf, ti =>
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false; // distance values, not colors
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed; // keep the gradient smooth
            });

            // Blocks are tinted by the same shader as the ring body, so match their brightness to RingFinal's face.
            float reference = LinearMean("RingFinal.png", RingFinalBodyRect);
            CropAtlas(ExtraAtlas, BlockRect, BlockPlastic, reference);
            CropAtlas(WoodAtlas, BlockRect, BlockWood, reference);
            CropAtlas(MarbleAtlas, BlockRect, BlockMarble, reference);
            CropAtlas(ExtraAtlas, ClipRect, ClipFile);
            foreach (var block in new[] { BlockPlastic, BlockWood, BlockMarble })
                ImportSprite(block, new Vector4(BlockEndBorder, 0f, BlockEndBorder, 0f));
            ImportSprite(ClipFile, Vector4.zero);

            Import("RingFinal.png", ti => { ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = true; ti.alphaIsTransparency = true; });
            Import("RingFinal_N.png", ti => { ti.textureType = TextureImporterType.NormalMap; ti.mipmapEnabled = true; });
            Import("tex_sequence_011.png", ti => { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; });
            Import("tex_star_005.png", ti => { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; });
            Import("clamp04.png", ti => { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; });
        }

        // Body slices (px from the top-left) used to compare brightness between atlases.
        static readonly RectInt RingFinalBodyRect = new(64, 32, 384, 64); // row 0
        static readonly RectInt ExtraBodyRect = new(64, 32, 384, 64);

        /// <summary>
        /// Mean red of the opaque pixels in linear space: the value the Ring shader compares to _MidGray
        /// (the project renders in Linear, so sRGB textures are linearized before shading).
        /// </summary>
        static float LinearMean(string atlas, RectInt rect) => LinearMean(ReadPixels(atlas, rect));

        static float LinearMean(Color[] px)
        {
            double sum = 0;
            int n = 0;
            foreach (var c in px)
            {
                if (c.a < 0.8f) continue;
                sum += Mathf.GammaToLinearSpace(c.r);
                n++;
            }
            return n > 0 ? (float)(sum / n) : 0.5f;
        }

        static Color[] ReadPixels(string atlas, RectInt rect)
        {
            string srcPath = $"{Tex}/{atlas}";
            if (!System.IO.File.Exists(srcPath))
            {
                Debug.LogError($"[RotateRings] Missing texture {atlas}");
                return new Color[0];
            }
            var src = new Texture2D(2, 2);
            src.LoadImage(System.IO.File.ReadAllBytes(srcPath));
            var px = src.GetPixels(rect.x, src.height - rect.y - rect.height, rect.width, rect.height);
            Object.DestroyImmediate(src);
            return px;
        }

        /// <summary>
        /// _MidGray that makes an atlas body tint as bright as the plastic ring (RingFinal at the Ring material's 0.5).
        /// </summary>
        static float MatchedMidGray(string atlas) =>
            0.5f * LinearMean(atlas, ExtraBodyRect) / Mathf.Max(1e-4f, LinearMean("RingFinal.png", RingFinalBodyRect));

        /// <summary>
        /// Copies a rect of an atlas into its own PNG, for use as a sprite. With a target, the grey is scaled (in linear
        /// space) so its mean matches it, i.e. it tints exactly like the ring body.
        /// </summary>
        static void CropAtlas(string atlas, RectInt rect, string outFile, float targetLinearMean = -1f)
        {
            var px = ReadPixels(atlas, rect);
            if (px.Length == 0) return;
            if (targetLinearMean > 0f)
            {
                float gain = targetLinearMean / Mathf.Max(1e-4f, LinearMean(px));
                for (int i = 0; i < px.Length; i++)
                {
                    float r = Mathf.LinearToGammaSpace(Mathf.Clamp01(Mathf.GammaToLinearSpace(px[i].r) * gain));
                    float g = Mathf.LinearToGammaSpace(Mathf.Clamp01(Mathf.GammaToLinearSpace(px[i].g) * gain));
                    float b = Mathf.LinearToGammaSpace(Mathf.Clamp01(Mathf.GammaToLinearSpace(px[i].b) * gain));
                    px[i] = new Color(r, g, b, px[i].a);
                }
            }

            var tex = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{Tex}/{outFile}";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        static void ImportSprite(string file, Vector4 border)
        {
            Import(file, ti =>
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = RingTexelsPerUnit; // same scale as the ring body
                ti.spriteBorder = border; // left, bottom, right, top
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; // required for sliced drawing
                ti.SetTextureSettings(settings);
            });
        }

        static void Import(string file, System.Action<TextureImporter> setup)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath($"{Tex}/{file}");
            if (ti == null) { Debug.LogError($"[RotateRings] Missing texture {file}"); return; }
            setup(ti);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        // ---------- Materials ----------

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

        static void SetExtraAtlas(Material m, string atlas)
        {
            m.SetTexture("_MainTex", Load<Texture2D>(atlas));
            m.SetTextureScale("_MainTex", ExtraTiling);
            m.SetTextureOffset("_MainTex", ExtraOffset);
            m.SetFloat("_BodyTiling", 1f);
            m.SetFloat("_TileThick", 6f); // body slice is 384px = 6 thicknesses long
        }

        static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{Tex}/{file}");
    }
}
