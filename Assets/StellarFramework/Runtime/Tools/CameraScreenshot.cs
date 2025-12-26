using UnityEngine;
using System.IO;
using System;
using StellarFramework;

public class CameraScreenshot : MonoBehaviour
{
    [Header("设置")] [Tooltip("按下此按键截图")] public KeyCode captureKey = KeyCode.S;

    [Tooltip("截图保存的文件夹名称")] public string folderName = "Screenshots";

    [Tooltip("如果不指定，默认使用挂载此脚本的相机")] public Camera targetCamera;

    void Start()
    {
        // 如果没有手动指定相机，尝试获取当前物体上的相机组件
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            LogKit.LogError("未找到相机！请将脚本挂载在相机上或手动指定 Target Camera。");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(captureKey))
        {
            CaptureCameraView();
        }
    }

    public void CaptureCameraView()
    {
        if (targetCamera == null) return;

        // 1. 获取当前屏幕（场景）的分辨率
        int resWidth = Screen.width;
        int resHeight = Screen.height;

        // 2. 创建一个临时的 RenderTexture，深度缓冲设为 24
        RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);

        // 3. 临时将相机的 TargetTexture 设为我们创建的 rt
        RenderTexture currentRT = targetCamera.targetTexture; // 记录原来的设置
        targetCamera.targetTexture = rt;

        // 4. 手动渲染相机
        targetCamera.Render();

        // 5. 激活 rt 以便读取像素
        RenderTexture.active = rt;

        // 6. 创建 Texture2D 并读取像素
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        screenShot.Apply();

        // 7. 重置相机的状态（恢复原状）
        targetCamera.targetTexture = currentRT;
        RenderTexture.active = null; // 释放激活的 RT
        Destroy(rt); // 销毁临时的 RT

        // 8. 保存为 PNG 文件
        byte[] bytes = screenShot.EncodeToPNG();
        string filename = GetSafeFilename();

        File.WriteAllBytes(filename, bytes);

        LogKit.Log(string.Format("截图已保存: {0} (分辨率: {1}x{2})", filename, resWidth, resHeight));
    }

    // 生成带时间戳的文件路径，防止重名
    private string GetSafeFilename()
    {
        // 路径：项目根目录/Screenshots
        // 注意：在打包后，Application.dataPath 通常指向 _Data 文件夹，建议在编辑器下用 dataPath 的父级，打包后用 persistentDataPath

        string path = "";

#if UNITY_EDITOR
        path = Path.Combine(Application.dataPath, "../", folderName); // 项目根目录
#else
        path = Path.Combine(Application.persistentDataPath, folderName); // 持久化数据目录
#endif

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return Path.Combine(path, "Screenshot_" + timeStamp + ".png");
    }
}