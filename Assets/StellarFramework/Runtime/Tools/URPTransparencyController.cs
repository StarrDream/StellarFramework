using StellarFramework;
using UnityEngine;

/// <summary>
/// URP材质透明度控制器
/// 用于控制使用URP材质的物体透明度
/// 只在需要透明时才切换材质模式，不透明时保持原始材质设置
/// </summary>
public class URPTransparencyController : MonoBehaviour
{
    [Header("材质设置")] [Tooltip("要控制的渲染器，如果为空则自动获取")]
    public Renderer targetRenderer;

    [Header("透明度控制")] [Range(0f, 1f)] [Tooltip("透明度值: 0 = 完全透明, 1 = 完全不透明")]
    public float alpha = 1f;

    [Header("动画设置")] [Tooltip("是否启用透明度动画")]
    public bool enableAnimation = false;

    [Tooltip("动画速度")] public float animationSpeed = 1f;

    [Tooltip("最小透明度")] [Range(0f, 1f)] public float minAlpha = 0f;

    [Tooltip("最大透明度")] [Range(0f, 1f)] public float maxAlpha = 1f;

    private Material materialInstance;
    private float currentAlpha;
    private bool isTransparentMode = false;

    // 保存原始材质设置
    private float originalSurfaceType;
    private float originalBlendMode;
    private int originalRenderQueue;
    private int originalSrcBlend;
    private int originalDstBlend;
    private int originalZWrite;
    private Color originalBaseColor;
    private bool hasOriginalSettings = false;

    // Shader属性ID
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int AlphaProperty = Shader.PropertyToID("_Alpha");
    private static readonly int SurfaceTypeProperty = Shader.PropertyToID("_Surface");
    private static readonly int BlendModeProperty = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");

    void Start()
    {
        // 如果没有指定渲染器，自动获取
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            LogKit.LogError("未找到Renderer组件！");
            return;
        }

        // 创建材质实例，避免影响其他使用相同材质的物体
        materialInstance = targetRenderer.material;

        // 保存原始材质设置
        SaveOriginalMaterialSettings();

        currentAlpha = alpha;
        UpdateAlpha(currentAlpha);
    }

    void Update()
    {
        if (materialInstance == null) return;

        if (enableAnimation)
        {
            // 使用正弦波实现透明度动画
            float t = Mathf.PingPong(Time.time * animationSpeed, 1f);
            currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            UpdateAlpha(currentAlpha);
        }
        else
        {
            // 手动控制透明度
            if (!Mathf.Approximately(currentAlpha, alpha))
            {
                currentAlpha = alpha;
                UpdateAlpha(currentAlpha);
            }
        }
    }

    /// <summary>
    /// 保存原始材质设置
    /// </summary>
    private void SaveOriginalMaterialSettings()
    {
        if (materialInstance == null) return;

        // 保存原始颜色
        if (materialInstance.HasProperty(BaseColorProperty))
        {
            originalBaseColor = materialInstance.GetColor(BaseColorProperty);
        }

        // 保存原始渲染模式设置
        if (materialInstance.HasProperty(SurfaceTypeProperty))
        {
            originalSurfaceType = materialInstance.GetFloat(SurfaceTypeProperty);
        }

        if (materialInstance.HasProperty(BlendModeProperty))
        {
            originalBlendMode = materialInstance.GetFloat(BlendModeProperty);
        }

        if (materialInstance.HasProperty(SrcBlendProperty))
        {
            originalSrcBlend = materialInstance.GetInt(SrcBlendProperty);
        }

        if (materialInstance.HasProperty(DstBlendProperty))
        {
            originalDstBlend = materialInstance.GetInt(DstBlendProperty);
        }

        if (materialInstance.HasProperty(ZWriteProperty))
        {
            originalZWrite = materialInstance.GetInt(ZWriteProperty);
        }

        originalRenderQueue = materialInstance.renderQueue;
        hasOriginalSettings = true;
    }

    /// <summary>
    /// 更新透明度并根据需要切换材质模式
    /// </summary>
    /// <param name="alphaValue">透明度值 (0-1)</param>
    private void UpdateAlpha(float alphaValue)
    {
        if (materialInstance == null) return;

        alphaValue = Mathf.Clamp01(alphaValue);

        // 判断是否需要透明模式
        bool needsTransparency = alphaValue < 0.999f;

        if (needsTransparency && !isTransparentMode)
        {
            // 切换到透明模式
            SetTransparentMode();
        }
        else if (!needsTransparency && isTransparentMode)
        {
            // 恢复到不透明模式
            RestoreOpaqueMode();
        }

        // 设置透明度值
        SetAlphaValue(alphaValue);
    }

    /// <summary>
    /// 设置材质为透明模式
    /// </summary>
    private void SetTransparentMode()
    {
        if (materialInstance == null || isTransparentMode) return;

        // 设置为透明模式
        if (materialInstance.HasProperty(SurfaceTypeProperty))
        {
            materialInstance.SetFloat(SurfaceTypeProperty, 1); // Transparent
        }

        if (materialInstance.HasProperty(BlendModeProperty))
        {
            materialInstance.SetFloat(BlendModeProperty, 0); // Alpha blend
        }

        // 设置渲染队列为透明
        materialInstance.renderQueue = 3000;

        // 启用透明关键字
        materialInstance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        materialInstance.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        materialInstance.EnableKeyword("_ALPHAPREMULTIPLY_ON");

        // 设置混合模式
        if (materialInstance.HasProperty(SrcBlendProperty))
        {
            materialInstance.SetInt(SrcBlendProperty, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (materialInstance.HasProperty(DstBlendProperty))
        {
            materialInstance.SetInt(DstBlendProperty, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (materialInstance.HasProperty(ZWriteProperty))
        {
            materialInstance.SetInt(ZWriteProperty, 0);
        }

        isTransparentMode = true;
    }

    /// <summary>
    /// 恢复材质为不透明模式（使用原始设置）
    /// </summary>
    private void RestoreOpaqueMode()
    {
        if (materialInstance == null || !isTransparentMode || !hasOriginalSettings) return;

        // 恢复原始表面类型
        if (materialInstance.HasProperty(SurfaceTypeProperty))
        {
            materialInstance.SetFloat(SurfaceTypeProperty, originalSurfaceType);
        }

        if (materialInstance.HasProperty(BlendModeProperty))
        {
            materialInstance.SetFloat(BlendModeProperty, originalBlendMode);
        }

        // 恢复渲染队列
        materialInstance.renderQueue = originalRenderQueue;

        // 恢复混合模式
        if (materialInstance.HasProperty(SrcBlendProperty))
        {
            materialInstance.SetInt(SrcBlendProperty, originalSrcBlend);
        }

        if (materialInstance.HasProperty(DstBlendProperty))
        {
            materialInstance.SetInt(DstBlendProperty, originalDstBlend);
        }

        if (materialInstance.HasProperty(ZWriteProperty))
        {
            materialInstance.SetInt(ZWriteProperty, originalZWrite);
        }

        // 恢复关键字
        if (originalSurfaceType == 0) // 如果原始是不透明
        {
            materialInstance.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            materialInstance.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            materialInstance.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        // 恢复原始颜色的alpha值
        if (materialInstance.HasProperty(BaseColorProperty))
        {
            materialInstance.SetColor(BaseColorProperty, originalBaseColor);
        }

        isTransparentMode = false;
    }

    /// <summary>
    /// 设置透明度值
    /// </summary>
    /// <param name="alphaValue">透明度值 (0-1)</param>
    private void SetAlphaValue(float alphaValue)
    {
        if (materialInstance == null) return;

        // 修改BaseColor的alpha通道
        if (materialInstance.HasProperty(BaseColorProperty))
        {
            Color color = materialInstance.GetColor(BaseColorProperty);
            color.a = alphaValue;
            materialInstance.SetColor(BaseColorProperty, color);
        }

        // 如果材质有单独的Alpha属性
        if (materialInstance.HasProperty(AlphaProperty))
        {
            materialInstance.SetFloat(AlphaProperty, alphaValue);
        }
    }

    /// <summary>
    /// 设置透明度（公共方法）
    /// </summary>
    /// <param name="alphaValue">透明度值 (0-1)</param>
    public void SetAlpha(float alphaValue)
    {
        alpha = Mathf.Clamp01(alphaValue);
        currentAlpha = alpha;
        UpdateAlpha(currentAlpha);
    }

    /// <summary>
    /// 设置为完全不透明
    /// </summary>
    public void SetOpaque()
    {
        SetAlpha(1f);
    }

    /// <summary>
    /// 设置为50%透明
    /// </summary>
    public void SetHalfTransparent()
    {
        SetAlpha(0.5f);
    }

    /// <summary>
    /// 设置为完全透明
    /// </summary>
    public void SetFullyTransparent()
    {
        SetAlpha(0f);
    }

    /// <summary>
    /// 平滑过渡到目标透明度
    /// </summary>
    /// <param name="targetAlpha">目标透明度</param>
    /// <param name="duration">过渡时间（秒）</param>
    public void FadeTo(float targetAlpha, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FadeCoroutine(targetAlpha, duration));
    }

    private System.Collections.IEnumerator FadeCoroutine(float targetAlpha, float duration)
    {
        float startAlpha = currentAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            UpdateAlpha(currentAlpha);
            yield return null;
        }

        currentAlpha = targetAlpha;
        alpha = targetAlpha;
        UpdateAlpha(currentAlpha);
    }

    /// <summary>
    /// 重置为原始材质设置
    /// </summary>
    public void ResetToOriginal()
    {
        if (hasOriginalSettings)
        {
            RestoreOpaqueMode();
            SetAlpha(1f);
        }
    }

    void OnDestroy()
    {
        // 清理材质实例
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }

    void OnDisable()
    {
        // 禁用时恢复原始设置
        if (materialInstance != null && hasOriginalSettings)
        {
            RestoreOpaqueMode();
        }
    }
}