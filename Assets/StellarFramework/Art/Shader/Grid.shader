Shader "Custom/BuiltIn/ProceduralGrid"
{
    Properties
    {
        [Header(Colors)]
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.1, 1)
        _LineColor ("Line Color", Color) = (0.3, 0.3, 0.3, 1)
        _MajorLineColor ("Major Line Color", Color) = (0.5, 0.5, 0.5, 1)

        [Header(Grid Settings)]
        _GridSize ("Grid Size (Units)", Float) = 1.0
        _MajorGridStep ("Major Grid Step (Count)", Int) = 10
        _LineThickness ("Line Thickness", Range(0.001, 0.1)) = 0.02
        _MajorLineThickness ("Major Line Thickness", Range(0.001, 0.1)) = 0.04

        [Header(Fading)]
        _FadeDistance ("Fade Distance", Float) = 50.0
        _FadeFalloff ("Fade Falloff", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            // 变量声明
            float4 _BackgroundColor;
            float4 _LineColor;
            float4 _MajorLineColor;
            float _GridSize;
            int _MajorGridStep;
            float _LineThickness;
            float _MajorLineThickness;
            float _FadeDistance;
            float _FadeFalloff;

            v2f vert(appdata v)
            {
                v2f o;
                // 内置管线标准变换
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // 核心网格计算函数
            float GridFactor(float3 pos, float scale, float thickness)
            {
                float2 uv = pos.xz / scale;

                // fwidth 计算屏幕空间导数，用于抗锯齿
                float2 derivative = fwidth(uv);

                // 避免除以0
                derivative = max(derivative, 0.00001);

                float2 grid = abs(frac(uv - 0.5) - 0.5) / derivative;
                float lineAA = min(grid.x, grid.y);

                return 1.0 - smoothstep(0.0, thickness / length(derivative), lineAA);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. 基础网格 (Minor Grid)
                float minorGrid = GridFactor(i.worldPos, _GridSize, _LineThickness);

                // 2. 主网格 (Major Grid)
                float majorGrid = GridFactor(i.worldPos, _GridSize * _MajorGridStep, _MajorLineThickness);

                // 3. 混合颜色
                float4 finalColor = _BackgroundColor;
                finalColor = lerp(finalColor, _LineColor, minorGrid);
                finalColor = lerp(finalColor, _MajorLineColor, majorGrid);

                // 4. 距离衰减
                // 内置管线使用 _WorldSpaceCameraPos 获取相机位置
                float dist = distance(_WorldSpaceCameraPos, i.worldPos);
                float alpha = 1.0 - smoothstep(_FadeDistance * 0.5, _FadeDistance, dist);
                alpha = pow(alpha, _FadeFalloff);

                // 计算网格强度，用于远处淡出网格线
                float gridStrength = max(minorGrid, majorGrid) * alpha;

                // 最终混合：背景常驻，网格线随距离消失
                // 如果你想让整个地面在远处透明，可以把 alpha 乘到最终 alpha 通道
                fixed4 outColor = lerp(_BackgroundColor, (majorGrid > minorGrid ? _MajorLineColor : _LineColor), gridStrength);

                return outColor;
            }
            ENDCG
        }
    }
}