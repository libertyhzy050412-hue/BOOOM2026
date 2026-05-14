Shader "Custom/EnemyBackground"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _Intensity ("Distortion Intensity", Range(0, 0.2)) = 0.05
        _Speed ("Animation Speed", Vector) = (0.5, 0.5, 0, 0)
    }
    SubShader
    {
        // 需要在透明队列渲染，确保背景已经绘制完毕
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"
        }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha

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
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            // URP 2D专用的背景抓取纹理
            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float2 _Speed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 计算屏幕空间UV
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 采样并滚动噪声图
                float2 noiseUV = input.uv + _Time.y * _Speed;
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV);

                // 将噪声范围从 [0, 1] 映射到 [-1, 1]
                float2 offset = (noise.xy - 0.5) * 2.0;

                // 计算边缘遮罩，防止扭曲在Quad边缘产生明显的生硬切边
                float dist = length(input.uv - 0.5);
                float mask = smoothstep(0.5, 0.2, dist);

                // 应用偏移量
                screenUV += offset * _Intensity * mask;

                // 采样被扭曲的背景
                half4 bgColor = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture,
                                    screenUV);

                return bgColor;
            }
            ENDHLSL
        }
    }
}