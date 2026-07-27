using System.IO;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Генерирует процедурные текстуры для блок-аута: асфальт, бетон, кирпич,
    /// штукатурку и фасады с окнами. Это не финальный арт — он производится
    /// после утверждения художественного эталона (п. 14 ТЗ). Задача текстур —
    /// дать поверхностям масштаб и деталь вместо плоской заливки.
    /// </summary>
    public static class ProceduralTextureLibrary
    {
        private const string TextureFolder = "Assets/_Project/Art/Prototype/Textures";
        private const int DefaultSize = 512;

        /// <summary>Асфальт: тёмная крошка с редкими светлыми вкраплениями.</summary>
        public static Texture2D GetAsphalt()
            => LoadOrGenerate("Tex_Asphalt", DefaultSize, (x, y, size) =>
            {
                var grain = Noise(x, y, 3.5f, seed: 11) * 0.14f;
                var speck = Noise(x, y, 24f, seed: 27) > 0.86f ? 0.12f : 0f;
                var value = 0.17f + grain + speck;
                return new Color(value, value, value * 1.03f);
            });

        /// <summary>Бетон: холодный серый с крупными пятнами и потёками.</summary>
        public static Texture2D GetConcrete()
            => LoadOrGenerate("Tex_Concrete", DefaultSize, (x, y, size) =>
            {
                var blotch = Noise(x, y, 2.2f, seed: 5) * 0.18f;
                var grain = Noise(x, y, 18f, seed: 41) * 0.06f;

                // Вертикальные потёки от дождя делают стену менее «пластиковой».
                var streak = Mathf.PerlinNoise(x * 0.02f, y * 0.002f) * 0.07f;

                var value = 0.52f + blotch + grain - streak;
                return new Color(value, value * 1.01f, value * 1.04f);
            });

        /// <summary>Кирпич: ряды со смещением и швами.</summary>
        public static Texture2D GetBrick()
            => LoadOrGenerate("Tex_Brick", DefaultSize, (x, y, size) =>
            {
                const int brickHeight = 32;
                const int brickWidth = 72;
                const int mortar = 5;

                var row = y / brickHeight;

                // Каждый второй ряд сдвинут на половину кирпича — как в кладке.
                var offset = row % 2 == 0 ? 0 : brickWidth / 2;
                var localX = (x + offset) % brickWidth;
                var localY = y % brickHeight;

                var isMortar = localX < mortar || localY < mortar;

                if (isMortar)
                {
                    var m = 0.62f + Noise(x, y, 12f, seed: 7) * 0.05f;
                    return new Color(m, m * 0.99f, m * 0.96f);
                }

                // Кирпичи слегка различаются по тону — иначе стена выглядит печатью.
                var tint = Noise(row * brickWidth + localX / brickWidth, row, 6f, seed: 19);
                var r = 0.52f + tint * 0.14f;
                return new Color(r, r * 0.55f, r * 0.44f);
            });

        /// <summary>Штукатурка: тёплый светлый фасад с мелкой шероховатостью.</summary>
        public static Texture2D GetPlaster()
            => LoadOrGenerate("Tex_Plaster", DefaultSize, (x, y, size) =>
            {
                var grain = Noise(x, y, 26f, seed: 63) * 0.05f;
                var patch = Noise(x, y, 1.8f, seed: 31) * 0.09f;
                var value = 0.72f + grain + patch;
                return new Color(value, value * 0.97f, value * 0.9f);
            });

        /// <summary>
        /// Фасад с окнами: сетка проёмов, часть окон светится. Освещённые окна
        /// задаются в текстуре, потому что отдельная геометрия на каждое окно
        /// не влезает в бюджет отрисовки (NFR-001, п. 8.4 ТЗ).
        /// </summary>
        public static Texture2D GetFacade(bool nightLights)
            => LoadOrGenerate(nightLights ? "Tex_Facade_Lit" : "Tex_Facade", DefaultSize,
                (x, y, size) =>
                {
                    const int floorHeight = 64;
                    const int bayWidth = 64;
                    const int windowMargin = 14;

                    var localX = x % bayWidth;
                    var localY = y % floorHeight;

                    var insideWindow = localX >= windowMargin
                                       && localX < bayWidth - windowMargin
                                       && localY >= windowMargin
                                       && localY < floorHeight - windowMargin;

                    if (!insideWindow)
                    {
                        var wall = 0.58f + Noise(x, y, 14f, seed: 77) * 0.06f;
                        return new Color(wall, wall * 0.98f, wall * 0.93f);
                    }

                    var bay = x / bayWidth;
                    var floor = y / floorHeight;
                    var lit = nightLights && Noise(bay, floor, 1f, seed: 91) > 0.55f;

                    if (lit)
                        return new Color(1f, 0.86f, 0.55f);

                    // Тёмное стекло с намёком на отражение неба.
                    var reflection = Noise(x, y, 5f, seed: 103) * 0.08f;
                    return new Color(0.14f + reflection, 0.17f + reflection, 0.22f + reflection);
                });

        /// <summary>Тротуарная плитка: квадраты со швами.</summary>
        public static Texture2D GetSidewalk()
            => LoadOrGenerate("Tex_Sidewalk", DefaultSize, (x, y, size) =>
            {
                const int tile = 64;
                const int seam = 4;

                var localX = x % tile;
                var localY = y % tile;
                var isSeam = localX < seam || localY < seam;

                if (isSeam)
                {
                    var s = 0.38f;
                    return new Color(s, s, s * 1.02f);
                }

                var tint = Noise(x / tile, y / tile, 4f, seed: 55) * 0.08f;
                var value = 0.6f + tint + Noise(x, y, 20f, seed: 13) * 0.04f;
                return new Color(value, value * 0.99f, value * 0.97f);
            });

        /// <summary>Дорожная разметка: сплошная белая полоса на асфальте.</summary>
        public static Texture2D GetRoadMarking()
            => LoadOrGenerate("Tex_RoadMarking", 64, (x, y, size) =>
            {
                var value = 0.86f + Noise(x, y, 10f, seed: 23) * 0.08f;
                return new Color(value, value, value * 0.95f);
            });

        // ------------------------------------------------------------------

        private delegate Color PixelShader(int x, int y, int size);

        /// <summary>
        /// Загружает текстуру из проекта или генерирует и сохраняет её.
        /// Повторные запуски генератора переиспользуют готовый ассет.
        /// </summary>
        private static Texture2D LoadOrGenerate(string assetName, int size, PixelShader shader)
        {
            var path = $"{TextureFolder}/{assetName}.png";

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            EnsureFolder();

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true);
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = shader(x, y, size);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            // Повтор по обеим осям: текстуры натягиваются на длинные стены и дороги.
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Prototype"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Prototype");

            if (!AssetDatabase.IsValidFolder(TextureFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Prototype", "Textures");
        }

        /// <summary>
        /// Perlin-шум в диапазоне [0, 1] с масштабом и смещением по семени.
        /// Смещение делает слои шума независимыми друг от друга.
        /// </summary>
        private static float Noise(float x, float y, float scale, int seed)
        {
            var offset = seed * 13.37f;
            return Mathf.PerlinNoise(
                x / 512f * scale + offset,
                y / 512f * scale + offset);
        }
    }
}
