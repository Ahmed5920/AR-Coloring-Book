using System.IO;
using UnityEngine;

//Help to ScreenShot
public static class ScreenShot
{
    private static string TEMP_FILE_NAME = "cmTmpImg.jpg";

    private static Rect rect;
    private static RenderTexture renderTexture;
    private static Texture2D screenShot;

    public static Texture2D GetTmpTex(GameObject arContents)
    {
        arContents.SetActive(false);

        if (renderTexture == null)
        {
            int sceenWidth = Screen.width;
            int sceenHeight = Screen.height;

            rect = new Rect(0, 0, sceenWidth, sceenHeight);
            renderTexture = new RenderTexture(sceenWidth, sceenHeight, 24);
            screenShot = new Texture2D(sceenWidth, sceenHeight, TextureFormat.RGB24, false);
        }

        Camera camera = Camera.main;
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture.active = renderTexture;
        screenShot.ReadPixels(rect, 0, 0);
        screenShot.Apply();

        camera.targetTexture = null;
        RenderTexture.active = null;

        Texture2D tex = screenShot;

        // (removed: a second JPG encode + file write that nothing used, and the two
        //  "= null" lines that made a brand-new RenderTexture/Texture2D leak on every call)

        arContents.SetActive(true);

        return tex;
    }
}