Shader "Custom/MapLine_LightWall"
{
    Properties
    {
        _MainTex ("Base Map (Tile)", 2D) = "white" {}
        
        [Header(Light Wall Settings)]
        [HDR] _CoreColor("Core Color (中心极细亮线)", Color) = (1, 1, 1, 1)
        [HDR] _GlowColor("Glow Color (外围立体光墙)", Color) = (0, 0.8, 1, 1)
        _EdgeWidth("Edge Width (检测宽度)", Range(0.1, 5.0)) = 1.5
        _PatternScale("Wall Pattern Scale (光墙条纹密集度)", Float) = 15.0
        _ScrollSpeed("Scroll Speed (流动速度)", Float) = 3.0
    }
   
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_GlobalRevealMask);
            SAMPLER(sampler_GlobalRevealMask);
            float4 _GlobalRevealMask_TexelSize;
            float4 _GlobalRevealMaskMin;
            float4 _GlobalRevealMaskSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float _EdgeWidth;
                float _PatternScale;
                float _ScrollSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 maskUV = (input.positionWS.xy - _GlobalRevealMaskMin.xy) / _GlobalRevealMaskSize.xy;

                // 超出地图遮罩边界时不处理
                if (maskUV.x < 0 || maskUV.x > 1 || maskUV.y < 0 || maskUV.y > 1) 
                {
                    return baseColor;
                }

                // 【修复】采样 .a 通道而不是 .r 通道，识别擦除操作
                half centerMask = SAMPLE_TEXTURE2D(_GlobalRevealMask, sampler_GlobalRevealMask, maskUV).a;

                float2 offset = _GlobalRevealMask_TexelSize.xy * _EdgeWidth;
                
                half maskUp    = SAMPLE_TEXTURE2D(_GlobalRevealMask, sampler_GlobalRevealMask, maskUV + float2(0, offset.y)).a;
                half maskDown  = SAMPLE_TEXTURE2D(_GlobalRevealMask, sampler_GlobalRevealMask, maskUV + float2(0, -offset.y)).a;
                half maskLeft  = SAMPLE_TEXTURE2D(_GlobalRevealMask, sampler_GlobalRevealMask, maskUV + float2(-offset.x, 0)).a;
                half maskRight = SAMPLE_TEXTURE2D(_GlobalRevealMask, sampler_GlobalRevealMask, maskUV + float2(offset.x, 0)).a;

                // 计算梯度（Gradient）获取边缘强度
                float2 grad = float2(maskRight - maskLeft, maskUp - maskDown);
                float edgeStrength = length(grad);

                // 如果位于边界区域
                if (edgeStrength > 0.01)
                {
                    // 1. 极细的核心光线：用 pow 函数将渐变压缩到非常窄的区域
                    float core = pow(saturate(edgeStrength), 4.0);
                    
                    // 2. 流动的立体光栅：利用世界坐标和时间，生成斜向的移动扫描线条
                    float pattern = sin((input.positionWS.x + input.positionWS.y) * _PatternScale - _Time.y * _ScrollSpeed);
                    pattern = pattern * 0.5 + 0.5; // 将正弦值映射到 0~1
                    
                    // 3. 基础发光范围：结合边缘强度与光栅
                    float glow = saturate(edgeStrength) * pattern;

                    // 4. 合成最终光效 (核心部分更亮，外围带有动态纹理)
                    half3 lightEffect = (_CoreColor.rgb * core) + (_GlowColor.rgb * glow);
                    
                    // 5. Additive 叠加模式：将光效加在地图原色上，而不是遮盖它
                    return half4(baseColor.rgb + lightEffect, baseColor.a);
                }

                return baseColor;
            }
            ENDHLSL
        }
    }

}