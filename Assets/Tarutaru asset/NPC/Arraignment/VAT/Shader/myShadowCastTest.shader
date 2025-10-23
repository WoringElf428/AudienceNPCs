Shader "Unlit/myShadowCastTest"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes 
            {
                float4 positionOS   : POSITION;
                // uv 変数には特定の頂点のテクスチャにおける UV 座標が
                // 含まれます。
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                // uv 変数には特定の頂点のテクスチャにおける UV 座標が
                // 含まれます。
                float2 uv           : TEXCOORD0;
                 // フォグの計算で使うfog factor用のinterpolator
                half fogFactor : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // TRANSFORM_TEX マクロはタイリングとオフセットの
                // 変換を行います。
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                 // Fog factorを計算
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                // Fogを適用する
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                
                return color;
            }

            ENDHLSL
        }

        Pass {
	        Name "ShadowCaster"
	        Tags { "LightMode"="ShadowCaster" }

	        ZWrite On
	        ZTest LEqual

	        HLSLPROGRAM
	        #pragma vertex vert
	        #pragma fragment frag

	        // Material Keywords
	        #pragma shader_feature _ALPHATEST_ON
	        #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

	        // GPU Instancing
	        #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata i)
            {
                v2f o;
                o.pos = TransformObjectToHClip(i.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return 0;
            }
	        ENDHLSL
        }
    }
}
