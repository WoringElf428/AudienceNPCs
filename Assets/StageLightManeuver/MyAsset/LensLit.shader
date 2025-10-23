Shader "Custom/LensLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0

        _EmissionColor("Emission Color", Color) = (0,0,0,1)
        [HDR]_EmissionMap("Emission Map", 2D) = "white" {}

        _FresnelPower("Fresnel Power", Range(0.1, 10.0)) = 3.0
        _FresnelColor("Fresnel Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 300

        Pass
        {
            Name "FORWARD"
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            sampler2D _BaseMap;
            float4 _BaseColor;
            float _Smoothness;
            float _Metallic;

            sampler2D _EmissionMap;
            float4 _EmissionColor;

            float _FresnelPower;
            float4 _FresnelColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = _WorldSpaceCameraPos.xyz - positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);

                float NdotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                float3 fresnelColor = _FresnelColor.rgb * fresnel;

                float4 baseColor = tex2D(_BaseMap, IN.uv) * _BaseColor;
                float3 emission = tex2D(_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;

                float alpha = baseColor.a;

                float3 finalColor = baseColor.rgb + fresnelColor + emission;
                return float4(finalColor, alpha);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
