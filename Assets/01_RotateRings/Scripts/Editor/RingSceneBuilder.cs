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

        struct Mats
        {
            public Material Ring, Shadow, Break, Link, Outline, Spark;
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
            // SpriteRenderer feeds _MainTex; RingLink sets _Color.
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
            return mats;
        }

        [MenuItem("Tools/Rotate Rings/Update Materials (open scene)")]
        public static void UpdateMaterials()
        {
            var mats = SetupAssets();
            foreach (var r in Object.FindObjectsByType<Ring>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(r, "Update Ring Materials");
                AssignMaterials(r, mats);
                r.Rebuild();
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[RotateRings] Materials updated.");
        }

        static void AssignMaterials(Ring r, Mats mats)
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

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
            camGo.transform.position = new Vector3(0.12f, 0f, -10f);
            camGo.AddComponent<RingInput>().cam = cam;

            // Layout traced from the reference shot (ring radius 45px = 1.25u, origin at the picture center).
            var blue = MakeRing("Ring_Blue", new Vector2(-0.28f, 2.92f), 90f, Blue, 0, mats);
            var orange = MakeRing("Ring_Orange", new Vector2(-1.67f, 0f), 180f, Orange, 10, mats);
            var purple = MakeRing("Ring_Purple", new Vector2(1.6f, 0.25f), 180f, Purple, 10, mats);
            var red = MakeRing("Ring_Red", new Vector2(-0.14f, -2.92f), 270f, Red, 20, mats);

            var catchSprite = Load<Sprite>("catch.png");
            var stemSprite = Load<Sprite>("clamp_06_1.png");
            MakeLink(blue, orange, -114.6f, stemSprite, catchSprite, mats.Link);
            MakeLink(blue, purple, -55f, stemSprite, catchSprite, mats.Link);
            MakeLink(orange, red, -73f, stemSprite, catchSprite, mats.Link);

            foreach (var r in new[] { blue, orange, purple, red }) r.Rebuild();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[RotateRings] Test scene built: {ScenePath}");
        }

        static Ring MakeRing(string name, Vector2 pos, float gapCenter, Color color, int order, Mats mats)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var r = go.AddComponent<Ring>();
            r.gapCenter = gapCenter;
            r.color = color;
            r.sortingOrder = order;
            AssignMaterials(r, mats);
            return r;
        }

        static void MakeLink(Ring parent, Ring child, float worldAngle, Sprite stemSprite, Sprite sleeveSprite, Material mat)
        {
            var go = new GameObject($"Link_{child.name}");
            go.transform.SetParent(parent.transform, false);
            var link = go.AddComponent<RingLink>();
            link.child = child;
            link.angle = worldAngle - parent.Angle;
            link.stem = MakeSprite("Stem", go.transform, stemSprite, mat);
            link.sleeve = MakeSprite("Sleeve", go.transform, sleeveSprite, mat);
            link.Layout();
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            return sr;
        }

        // ---------- Assets ----------

        static void SetupImports()
        {
            Import("RingFinal.png", ti => { ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = true; ti.alphaIsTransparency = true; });
            Import("RingFinal_N.png", ti => { ti.textureType = TextureImporterType.NormalMap; ti.mipmapEnabled = true; });
            Import("tex_sequence_011.png", ti => { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; });
            Import("tex_star_005.png", ti => { ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; });
            Import("catch.png", ti => { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; });
            Import("clamp_06_1.png", ti => { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; });
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
