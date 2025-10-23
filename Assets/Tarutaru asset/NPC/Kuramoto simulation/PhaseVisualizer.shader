Shader "Unlit/PhaseVisualizer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NorTime("NorTime", Range(0.005, 0.995)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _NorTime)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float norTime : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.norTime = UNITY_ACCESS_INSTANCED_PROP(Props, _NorTime);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = frac(i.norTime);
                fixed4 texColor = tex2D(_MainTex, float2(t,0));
                return texColor;
            }
            ENDCG
        }
    }
}
