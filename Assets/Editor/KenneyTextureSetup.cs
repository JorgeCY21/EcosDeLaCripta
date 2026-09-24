#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>
    /// El colormap.png de Kenney es un atlas de franjas de color angostas, no una textura de
    /// detalle. Con mipmaps + compresión + filtro bilineal (los valores por defecto de Unity),
    /// las franjas vecinas se mezclan y todo el modelo se ve borroso/sucio. Aquí se fuerza
    /// sin mipmaps, sin compresión y filtro Point para bordes de color nítidos.
    /// </summary>
    public static class KenneyTextureSetup
    {
        private const string TexturePath = "Assets/Art/Kenney/MiniDungeon/Textures/colormap.png";

        public static void FixColormapImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[KenneyTextureSetup] No se encontró el importer de " + TexturePath);
                return;
            }

            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.anisoLevel = 1;
            importer.maxTextureSize = 512;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log("[KenneyTextureSetup] colormap.png reimportado sin mipmaps/compresión, filtro Point.");
        }
    }
}
#endif
