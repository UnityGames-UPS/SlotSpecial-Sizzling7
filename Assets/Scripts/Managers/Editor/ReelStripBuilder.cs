using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-time editor utility for the Pinball-style reel-tween port: prepends filler SlotIcon
// instances above each reel column's current top image and rewires SlotView.reelImagesList to
// match, so the continuous spin loop has room to travel. Run once via the menu item below, then
// save the scene. Resolves columns strictly through SlotView's own serialized references (never
// by GameObject name) since WinBoxOverlay contains unrelated objects also named "Slot".
public static class ReelStripBuilder
{
    private const string SlotIconPrefabPath = "Assets/Prefabs/SlotIcon.prefab";

    [MenuItem("Tools/Sizzling7/Extend Reel Strips (Reel-Tween Port)")]
    private static void ExtendReelStrips()
    {
        SlotView slotView = Object.FindFirstObjectByType<SlotView>();
        if (slotView == null)
        {
            Debug.LogError("[ReelStripBuilder] No SlotView found in the open scene.");
            return;
        }

        GameObject slotIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotIconPrefabPath);
        if (slotIconPrefab == null)
        {
            Debug.LogError($"[ReelStripBuilder] Could not load prefab at {SlotIconPrefabPath}.");
            return;
        }

        SerializedObject so = new SerializedObject(slotView);
        SerializedProperty reelTransformsProp = so.FindProperty("reelTransforms");
        SerializedProperty reelImagesListProp = so.FindProperty("reelImagesList");
        SerializedProperty bufferRowsAboveProp = so.FindProperty("bufferRowsAbove");
        SerializedProperty symbolHeightProp = so.FindProperty("symbolHeight");

        if (reelTransformsProp == null || reelImagesListProp == null)
        {
            Debug.LogError("[ReelStripBuilder] Could not find reelTransforms/reelImagesList on SlotView — has a field been renamed?");
            return;
        }

        int bufferRowsAbove = bufferRowsAboveProp != null ? bufferRowsAboveProp.intValue : 16;
        float symbolHeight = symbolHeightProp != null ? symbolHeightProp.floatValue : 225f;

        int columnCount = reelTransformsProp.arraySize;
        if (reelImagesListProp.arraySize != columnCount)
        {
            Debug.LogError("[ReelStripBuilder] reelTransforms and reelImagesList counts don't match — aborting to avoid touching the wrong column.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Extend Reel Strips",
                $"This adds {bufferRowsAbove} new SlotIcon instances above the current top image in each of the {columnCount} reel columns, and rewrites SlotView's reelImagesList to include them.\n\nRun once only — running it again on an already-extended reel will double up. Continue?",
                "Extend", "Cancel"))
        {
            return;
        }

        int columnsExtended = 0;

        for (int col = 0; col < columnCount; col++)
        {
            Transform columnTransform = reelTransformsProp.GetArrayElementAtIndex(col).objectReferenceValue as Transform;
            if (columnTransform == null)
            {
                Debug.LogWarning($"[ReelStripBuilder] Column {col} has no reelTransforms entry assigned — skipping.");
                continue;
            }

            SerializedProperty imagesListElement = reelImagesListProp.GetArrayElementAtIndex(col);
            SerializedProperty imagesProp = imagesListElement.FindPropertyRelative("images");
            if (imagesProp == null || imagesProp.arraySize == 0)
            {
                Debug.LogWarning($"[ReelStripBuilder] Column {col} has no images in reelImagesList — skipping.");
                continue;
            }

            // Guard against accidentally re-running this on an already-extended column.
            if (imagesProp.arraySize != 7)
            {
                Debug.LogWarning($"[ReelStripBuilder] Column {col} already has {imagesProp.arraySize} images (expected 7) — skipping to avoid double-extending. Undo/revert first if this needs re-running.");
                continue;
            }

            Image referenceImage = imagesProp.GetArrayElementAtIndex(0).objectReferenceValue as Image;
            if (referenceImage == null)
            {
                Debug.LogWarning($"[ReelStripBuilder] Column {col}'s first image reference is missing — skipping.");
                continue;
            }

            RectTransform referenceRect = referenceImage.rectTransform;

            // Instantiate top to bottom: k=0 is the furthest-above filler, k=bufferRowsAbove-1
            // sits directly above the current index-0 image.
            List<Image> newImages = new List<Image>(bufferRowsAbove);
            for (int k = 0; k < bufferRowsAbove; k++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(slotIconPrefab, columnTransform);
                Undo.RegisterCreatedObjectUndo(instance, "Extend Reel Strip");

                RectTransform rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = referenceRect.anchorMin;
                rect.anchorMax = referenceRect.anchorMax;
                rect.pivot = referenceRect.pivot;
                rect.sizeDelta = referenceRect.sizeDelta;

                float yOffset = symbolHeight * (bufferRowsAbove - k);
                rect.anchoredPosition = referenceRect.anchoredPosition + new Vector2(0f, yOffset);

                instance.name = "SlotIcon (buffer)";
                instance.transform.SetSiblingIndex(k);

                newImages.Add(instance.GetComponent<Image>());
            }

            // Rebuild the serialized list: new buffer images first, existing 7 unchanged after them.
            int existingCount = imagesProp.arraySize;
            List<Image> existingImages = new List<Image>(existingCount);
            for (int i = 0; i < existingCount; i++)
                existingImages.Add(imagesProp.GetArrayElementAtIndex(i).objectReferenceValue as Image);

            List<Image> combined = new List<Image>(newImages);
            combined.AddRange(existingImages);

            imagesProp.arraySize = combined.Count;
            for (int i = 0; i < combined.Count; i++)
                imagesProp.GetArrayElementAtIndex(i).objectReferenceValue = combined[i];

            columnsExtended++;
            Debug.Log($"[ReelStripBuilder] Column {col}: added {bufferRowsAbove} buffer images, total now {combined.Count}.");
        }

        so.ApplyModifiedProperties();

        if (columnsExtended > 0)
        {
            EditorSceneManager.MarkSceneDirty(slotView.gameObject.scene);
            Debug.Log($"[ReelStripBuilder] Done — {columnsExtended} column(s) extended. Save the scene (Ctrl+S) to persist the changes.");
        }
        else
        {
            Debug.LogWarning("[ReelStripBuilder] No columns were extended — see warnings above.");
        }
    }
}
