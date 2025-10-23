Shader "ShirayuriMeshibe/URP/MeshAudience(Non Particle)"
{ 
    Properties
    {
        [Header(AnimationTime)]
        [KeywordEnum(Auto, Manual)] _TimeUpdateMode("Time Update Mode", Float) = 0
        _ManualTime ("ManualTime", Float) = 0

        [Header(VertexAnimationTexture)]
        _Framecount("Frame count", Float) = 240
        _InterpolateTime("Interpolation time", Float) = 0.05
        [NoScaleOffset] _PositionTexture ("Position Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalTexture("Normal Texture", 2D) = "white" {}
        _BoundsMin ("Bounds Min", Vector) = (0,0,0,0)
        _BoundsMax ("Bounds Max", Vector) = (1,1,1,0)
        
        _Speed("Speed(Base 60BPM)", Float) = 1
        _ManualDelay("Delay", Float) = 0
        _Anime0 ("Anchor point:AnimeA", Vector) = (0.0, 0.1875,0.1875, 0.015625)
        _Anime1 ("Anchor point:AnimeB", Vector) = (0.203125, 0.390625,0.1875, 0.015625)
        _Anime2 ("Anchor point:AnimeC", Vector) = (0.40625, 0.59375,0.1875, 0.015625)
        _Anime3 ("Anchor point:AnimeD", Vector) = (0.609375, 0.796875,0.1875, 0.015625)
        _Anime4 ("Anchor point:AnimeE", Vector) = (0.8125, 1.0,0.1875, 0.015625)

        [Header(BlendAnimation)]
        _Blend1("Blend Anime1", Range(0 , 1)) = 0
        _Blend2("Blend Anime2", Range(0 , 1)) = 0
        _Blend3("Blend Anime3", Range(0 , 1)) = 0
        _Blend4("Blend Anime4", Range(0 , 1)) = 0
        [Toggle(APPLY_JUMP_DOUBLE_SPEED)] _ApplyJumpDoubleSpeed("Apply Double Jump Speed", Float) = 0

        [Header(Fog)]
        [KeywordEnum(None, User)] _FogMode("Fog Mode", Float) = 0
        _FogColorTop ("Fog Color(TOP)", Color) = (1.0, 1.0, 1.0, 1.0)
        _FogColorBottom ("Fog Color(BOTTOM)", Color) = (1.0, 1.0, 1.0, 1.0)
        _FogStart("Fog Start(Z)", Float) = 0.5
        _FogEnd("Fog End(Z)", Float) = 10
        _FogHeightStart("Fog Height Start(Local Y)", Float) = 0.5
        _FogHeightEnd("Fog Height End(Local Y)", Float) = 1.5

        [Header(ObjectSettings)]
        [MainTexture] _BaseMap("Color Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR] _PenLightColor("Penlight Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Toggle(_ALPHATEST_ON)] _AlphaTestToggle ("Alpha Clipping", Float) = 0
	    _Cutoff ("Alpha Cutoff", Float) = 0.5

        [Toggle(APPLY_MATCAP)] _ApplyMatCap("Apply MatCap", Float) = 0
        [NoScaleOffset] _MatCapTexture("MatCap Texture",2D) = "black" {}
        [HDR] _MatCapColor ("MatCap Color", Color) = (1.0, 1.0, 1.0, 1.0)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "IgnoreProjector"="True" "RenderType"="Opaque"}

        HLSLINCLUDE
            inline float remap(float value, float inputMin, float inputMax, float outputMin, float outputMax)
            {
                return outputMin + (value - inputMin) * (outputMax - outputMin) / (inputMax - inputMin);
            }

            inline float3 normalBlendReoriented(float3 norma1, float3 normal2)
            {
                const float3 t = norma1.xyz + float3(0.0, 0.0, 1.0);
                const float3 u = normal2.xyz * float3(-1.0, -1.0, 1.0);
                return (t / t.z) * dot(t, u) - u;
            }
        ENDHLSL

        Pass
        {
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers gles
            #pragma target 4.5  

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup


            // 独自のインスタンシング用のデータ構造を定義する
            #define UNITY_PARTICLE_INSTANCE_DATA MyParticleInstanceData
            #define UNITY_PARTICLE_INSTANCE_DATA_NO_ANIM_FRAME
            struct MyParticleInstanceData
            {
                float3x4 transform;
                uint color;
                float2 delay;
            };

            #pragma multi_compile_local _ _TIMEUPDATEMODE_MANUAL
            #pragma multi_compile_local _ APPLY_JUMP_DOUBLE_SPEED
            #pragma multi_compile_local _ APPLY_MATCAP
            #pragma multi_compile_local _ _FOGMODE_USER

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ParticlesInstancing.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
#if !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                float2 delay : TEXCOORD2;
#endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color1 : COLOR;
                float4 color2 : TEXCOORD0;
                float2 uv0 : TEXCOORD1;
                float3 viewNormal : TEXCOORD2;
                float3 fogParam : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform float _ManualTime;

            uniform float _Framecount;
            uniform float _InterpolateTime;
            uniform sampler2D _PositionTexture;
            uniform float4 _PositionTexture_TexelSize;
            uniform sampler2D _NormalTexture;
            uniform float4 _BoundsMin;
            uniform float4 _BoundsMax;

            uniform float _Speed;
            uniform half4 _Anime0;
            uniform half4 _Anime1;
            uniform half4 _Anime2;
            uniform half4 _Anime3;
            uniform half4 _Anime4;

            uniform float _ManualDelay;
            uniform float _Blend1;
            uniform float _Blend2;
            uniform float _Blend3;
            uniform float _Blend4;

            uniform float3 _FogColorTop;
            uniform float3 _FogColorBottom;
            uniform float _FogStart;
            uniform float _FogEnd;
            uniform float _FogHeightStart;
            uniform float _FogHeightEnd;

            uniform sampler2D _BaseMap;
            uniform float3 _BaseColor;
            uniform float3 _PenLightColor;
            uniform sampler2D _MatCapTexture;
            uniform float3 _MatCapColor;

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

#if defined(_TIMEUPDATEMODE_MANUAL)
                float t = _ManualTime;
#else
    #if defined(APPLY_JUMP_DOUBLE_SPEED)
                float speed = 1.0;
                speed = lerp(1.0, 2.0, step(0.999, _Blend3));
                float t = _Time.y * _Speed * speed;
    #else
                float t = _Time.y * _Speed;
    #endif
#endif
                float2 d = float2(0.0, 0.0);
#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
                d.x = data.delay.x;
                d.y = data.delay.y;
                
                //t += UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random0).x;
                //o.penLightColor = UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random1);
#else
                d.x = _ManualDelay;
                d.y = _ManualDelay;
#endif                
                //スタイル一貫性の維持
                float incorp_rate = saturate((frac(t) - 1 + _InterpolateTime) / _InterpolateTime) * step(1.0 - _InterpolateTime, frac(t));
                //incorp_rate = 1.0;
                
                const float t_nowStart = frac(t + d.x);
                const float t_nextStart = frac(t + d.y);

                //現在サイクル
                const float t0 = remap(t_nowStart, 0.0, 1.0, _Anime0.x, _Anime0.y);
                const float t1 = remap(t_nowStart, 0.0, 1.0, _Anime1.x, _Anime1.y);
                const float t2 = remap(t_nowStart, 0.0, 1.0, _Anime2.x, _Anime2.y);
                const float t3 = remap(t_nowStart, 0.0, 1.0, _Anime3.x, _Anime3.y);
                const float t4 = remap(t_nowStart, 0.0, 1.0, _Anime4.x, _Anime4.y);
                
                //フレーム情報を復元するためのUVoffset
                const float fameCount = _Framecount - 1.0;
                const float2 offsetUV0 = float2((_PositionTexture_TexelSize.x * fameCount * t0), 0.0);
                const float2 offsetUV1 = float2((_PositionTexture_TexelSize.x * fameCount * t1), 0.0);
                const float2 offsetUV2 = float2((_PositionTexture_TexelSize.x * fameCount * t2), 0.0);
                const float2 offsetUV3 = float2((_PositionTexture_TexelSize.x * fameCount * t3), 0.0);
                const float2 offsetUV4 = float2((_PositionTexture_TexelSize.x * fameCount * t4), 0.0);
                const float2 uv0 = (offsetUV0 + v.uv1.xy);
                const float2 uv1 = (offsetUV1 + v.uv1.xy);
                const float2 uv2 = (offsetUV2 + v.uv1.xy);
                const float2 uv3 = (offsetUV3 + v.uv1.xy);
                const float2 uv4 = (offsetUV4 + v.uv1.xy);
                //時刻tのpos情報の復元
                const float3 offsetPosition0 = tex2Dlod(_PositionTexture, float4(uv0, 0.0, 0.0)).rgb;
                const float3 offsetPosition1 = tex2Dlod(_PositionTexture, float4(uv1, 0.0, 0.0)).rgb;
                const float3 offsetPosition2 = tex2Dlod(_PositionTexture, float4(uv2, 0.0, 0.0)).rgb;
                const float3 offsetPosition3 = tex2Dlod(_PositionTexture, float4(uv3, 0.0, 0.0)).rgb;
                const float3 offsetPosition4 = tex2Dlod(_PositionTexture, float4(uv4, 0.0, 0.0)).rgb;

                //次のセグメントの時刻0のフレーム情報を復元するためのUVoffeset
                const float2 offsetUV0t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(t_nextStart, 0.0, 1.0, _Anime0.x, _Anime0.y)), 0.0);
                const float2 offsetUV1t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(t_nextStart, 0.0, 1.0, _Anime1.x, _Anime1.y)), 0.0);
                const float2 offsetUV2t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(t_nextStart, 0.0, 1.0, _Anime2.x, _Anime2.y)), 0.0);
                const float2 offsetUV3t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(t_nextStart, 0.0, 1.0, _Anime3.x, _Anime3.y)), 0.0);
                const float2 offsetUV4t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(t_nextStart, 0.0, 1.0, _Anime4.x, _Anime4.y)), 0.0);
                const float2 uv0t0 = (offsetUV0t0 + v.uv1.xy);
                const float2 uv1t0 = (offsetUV1t0 + v.uv1.xy);
                const float2 uv2t0 = (offsetUV2t0 + v.uv1.xy);
                const float2 uv3t0 = (offsetUV3t0 + v.uv1.xy);
                const float2 uv4t0 = (offsetUV4t0 + v.uv1.xy);
                //次のセグメントの時刻0のpos情報復元
                const float3 offsetPosition0t0 = tex2Dlod(_PositionTexture, float4(uv0t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition1t0 = tex2Dlod(_PositionTexture, float4(uv1t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition2t0 = tex2Dlod(_PositionTexture, float4(uv2t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition3t0 = tex2Dlod(_PositionTexture, float4(uv3t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition4t0 = tex2Dlod(_PositionTexture, float4(uv4t0, 0.0, 0.0)).rgb;

                //フレーム補完
                const float3 position0 = lerp(offsetPosition0, offsetPosition0t0, incorp_rate);
                const float3 position1 = lerp(offsetPosition1, offsetPosition1t0, incorp_rate);
                const float3 position2 = lerp(offsetPosition2, offsetPosition2t0, incorp_rate);
                const float3 position3 = lerp(offsetPosition3, offsetPosition3t0, incorp_rate);
                const float3 position4 = lerp(offsetPosition4, offsetPosition4t0, incorp_rate);

                //アニメーションブレンド
                const float3 vertex01 = lerp(position0, position1, _Blend1);
                const float3 vertex12 = lerp(vertex01, position2, _Blend2);
                const float3 vertex23 = lerp(vertex12, position3, _Blend3);
                const float3 vertex34 = lerp(vertex23, position4, _Blend4);
                float3 rawPos = lerp(_BoundsMin.xyz, _BoundsMax.xyz, vertex34);
                //x軸に90度回転
                //float3 rotPos   = float3(rawPos.x, rawPos.z, -rawPos.y);
                //v.vertex.xyz = mul(unity_ObjectToWorld, float4(rotPos,1));
                v.vertex.xyz =rawPos;

                //時刻tのnor情報の復元
                const float3 normal0t = tex2Dlod(_NormalTexture, float4(uv0, 0.0, 0.0)).rgb;
                const float3 normal1t = tex2Dlod(_NormalTexture, float4(uv1, 0.0, 0.0)).rgb;
                const float3 normal2t = tex2Dlod(_NormalTexture, float4(uv2, 0.0, 0.0)).rgb;
                const float3 normal3t = tex2Dlod(_NormalTexture, float4(uv3, 0.0, 0.0)).rgb;
                const float3 normal4t = tex2Dlod(_NormalTexture, float4(uv4, 0.0, 0.0)).rgb;
                //時刻0のnor情報の復元
                const float3 normal0t0 = tex2Dlod(_NormalTexture, float4(uv0t0, 0.0, 0.0)).rgb;
                const float3 normal1t0 = tex2Dlod(_NormalTexture, float4(uv1t0, 0.0, 0.0)).rgb;
                const float3 normal2t0 = tex2Dlod(_NormalTexture, float4(uv2t0, 0.0, 0.0)).rgb;
                const float3 normal3t0 = tex2Dlod(_NormalTexture, float4(uv3t0, 0.0, 0.0)).rgb;
                const float3 normal4t0 = tex2Dlod(_NormalTexture, float4(uv4t0, 0.0, 0.0)).rgb;
                //フレーム補完
                const float3 normal0 = lerp(normal0t, normal0t0, incorp_rate);
                const float3 normal1 = lerp(normal1t, normal1t0, incorp_rate);
                const float3 normal2 = lerp(normal2t, normal2t0, incorp_rate);
                const float3 normal3 = lerp(normal3t, normal3t0, incorp_rate);
                const float3 normal4 = lerp(normal4t, normal4t0, incorp_rate);           

                //アニメーションブレンド
                const float3 normal01 = lerp(normal0, normal1, _Blend1);
                const float3 normal12 = lerp(normal01, normal2, _Blend2);
                const float3 normal23 = lerp(normal12, normal3, _Blend3);
                const float3 normal34 = lerp(normal23, normal4, _Blend4);
                v.normal.xyz = normal34;

                o.pos = TransformObjectToHClip(v.vertex);
                o.color1 = v.color;
                o.color2 = v.color;

#if defined(_FOGMODE_USER)
                o.fogParam.x = -mul(UNITY_MATRIX_MV, v.vertex).z; //UNITY_MATRIX_MV使えない
                //o.fogParam.x = -mul(GetWorldToViewMatrix(), float4(v.vertex.xyz, 1.0)).z;
                o.fogParam.y = v.vertex.y;
#endif


#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                //vertInstancingColor(o.color1);
                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, o.color1);
#endif

                o.uv0 = v.uv0;

#if defined(APPLY_MATCAP)
                /*URP実装*/
                const float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                const float3 viewNormal = mul(GetWorldToViewMatrix(), float4(worldNormal, 1)).xyz;
                const float3 worldViewDirection = normalize(GetWorldSpaceViewDir(v.vertex));
                const float3 viewViewDirection = mul(GetWorldToViewMatrix(), float4(worldViewDirection, 0)).xyz * float3(-1.0, -1.0, 1.0);
                const float3 normal = normalize(normalBlendReoriented(viewNormal, viewViewDirection));// 法線のブレンド

                o.viewNormal = normal;
#endif
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                const float4 c0 = tex2D(_BaseMap, i.uv0);
                float3 c1 = lerp(c0.rgb * _BaseColor, float3(0.0, 0.0, 0.0), i.color2.r);

#if defined(_FOGMODE_USER)
                const float3 fogColor = lerp(_FogColorBottom, _FogColorTop, smoothstep(_FogHeightStart, _FogHeightEnd, i.fogParam.y));
                c1 = lerp(c1, fogColor, smoothstep(_FogStart, _FogEnd, i.fogParam.x));
#endif

#if defined(APPLY_MATCAP)
                const float2 uvMatcap = i.viewNormal.xy * 0.5 + 0.5;
                const float4 cMatcap = tex2D(_MatCapTexture, uvMatcap);
                c1 += cMatcap.rgb * _MatCapColor.rgb;
#endif
                // Apply PenLight Color
                c1 += i.color1.rgb * _PenLightColor * c0.rgb;
                c1 = max(0.0, c1); // Flashing対応

                float4 f = float4(c1, 1.0);
                return f;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            BlendOp Add
            Blend One Zero
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles
            #pragma target 4.5

            #define UNITY_PARTICLE_INSTANCE_DATA MyParticleInstanceData
            #define UNITY_PARTICLE_INSTANCE_DATA_NO_ANIM_FRAME
            struct MyParticleInstanceData
            {
                float3x4 transform;
                uint color;
                float2 delay;
            };

            #pragma multi_compile_local _ _TIMEUPDATEMODE_MANUAL
            #pragma multi_compile_local _ APPLY_JUMP_DOUBLE_SPEED

            //pass
            #pragma vertex vert
	        #pragma fragment frag

	        // Material Keywords
            #pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON
	        #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

	        // GPU Instancing
	        #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ParticlesInstancing.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
#if !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                float2 delay : TEXCOORD2;
#endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS  : SV_POSITION;

                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            uniform float _ManualTime;

            uniform float _Framecount;
            uniform sampler2D _PositionTexture;
            uniform float4 _PositionTexture_TexelSize;
            uniform sampler2D _NormalTexture;
            uniform float4 _BoundsMin;
            uniform float4 _BoundsMax;
            uniform float _Speed;
            uniform float4 _BaseColor;
            uniform half4 _Anime0;
            uniform half4 _Anime1;
            uniform half4 _Anime2;
            uniform half4 _Anime3;
            uniform half4 _Anime4;
            uniform float _InterpolateTime;
            uniform float _ManualDelay;
            uniform float _Blend1;
            uniform float _Blend2;
            uniform float _Blend3;
            uniform float _Blend4;
        
            uniform float4 _BaseMap_ST;
            
            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                //UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

#if defined(_TIMEUPDATEMODE_MANUAL)
                float t = _ManualTime;
#else
    #if defined(APPLY_JUMP_DOUBLE_SPEED)
                float speed = 1.0;
                speed = lerp(1.0, 2.0, step(0.999, _Blend3));
                float t = _Time.y * _Speed * speed;
    #else
                float t = _Time.y * _Speed;
    #endif
#endif
                float2 d = float2(0.0, 0.0);
#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
                d.x = data.delay.x;
                d.y = data.delay.y;
                
                //t += UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random0).x;
                //o.penLightColor = UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random1);
#else
                d.x = _ManualDelay;
                d.y = _ManualDelay;
#endif
               
                //スタイル一貫性の維持
                float incorp_rate = saturate((frac(t) - (1 - _InterpolateTime)) / (_InterpolateTime)) * step(1.0 - _InterpolateTime, frac(t));
                //incorp_rate = 0.0;
 
                const float t0 = remap(frac(t + d.x), 0.0, 1.0, _Anime0.x, _Anime0.y);
                const float t1 = remap(frac(t + d.x), 0.0, 1.0, _Anime1.x, _Anime1.y);
                const float t2 = remap(frac(t + d.x), 0.0, 1.0, _Anime2.x, _Anime2.y);
                const float t3 = remap(frac(t + d.x), 0.0, 1.0, _Anime3.x, _Anime3.y);
                const float t4 = remap(frac(t + d.x), 0.0, 1.0, _Anime4.x, _Anime4.y);

                //フレーム情報を復元するためのUVoffset
                const float fameCount = _Framecount - 1.0;
                const float2 offsetUV0 = float2((_PositionTexture_TexelSize.x * fameCount * t0), 0.0);
                const float2 offsetUV1 = float2((_PositionTexture_TexelSize.x * fameCount * t1), 0.0);
                const float2 offsetUV2 = float2((_PositionTexture_TexelSize.x * fameCount * t2), 0.0);
                const float2 offsetUV3 = float2((_PositionTexture_TexelSize.x * fameCount * t3), 0.0);
                const float2 offsetUV4 = float2((_PositionTexture_TexelSize.x * fameCount * t4), 0.0);
                const float2 uv0 = (offsetUV0 + v.uv1.xy);
                const float2 uv1 = (offsetUV1 + v.uv1.xy);
                const float2 uv2 = (offsetUV2 + v.uv1.xy);
                const float2 uv3 = (offsetUV3 + v.uv1.xy);
                const float2 uv4 = (offsetUV4 + v.uv1.xy);
                //時刻tのpos情報の復元
                const float3 offsetPosition0 = tex2Dlod(_PositionTexture, float4(uv0, 0.0, 0.0)).rgb;
                const float3 offsetPosition1 = tex2Dlod(_PositionTexture, float4(uv1, 0.0, 0.0)).rgb;
                const float3 offsetPosition2 = tex2Dlod(_PositionTexture, float4(uv2, 0.0, 0.0)).rgb;
                const float3 offsetPosition3 = tex2Dlod(_PositionTexture, float4(uv3, 0.0, 0.0)).rgb;
                const float3 offsetPosition4 = tex2Dlod(_PositionTexture, float4(uv4, 0.0, 0.0)).rgb;

                //次のセグメントの時刻0のフレーム情報を復元するためのUVoffeset
                const float2 offsetUV0t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(frac(t + d.y), 0.0, 1.0, _Anime0.x, _Anime0.y)), 0.0);
                const float2 offsetUV1t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(frac(t + d.y), 0.0, 1.0, _Anime1.x, _Anime1.y)), 0.0);
                const float2 offsetUV2t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(frac(t + d.y), 0.0, 1.0, _Anime2.x, _Anime2.y)), 0.0);
                const float2 offsetUV3t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(frac(t + d.y), 0.0, 1.0, _Anime3.x, _Anime3.y)), 0.0);
                const float2 offsetUV4t0 = float2((_PositionTexture_TexelSize.x * fameCount * remap(frac(t + d.y), 0.0, 1.0, _Anime4.x, _Anime4.y)), 0.0);
                const float2 uv0t0 = (offsetUV0t0 + v.uv1.xy);
                const float2 uv1t0 = (offsetUV1t0 + v.uv1.xy);
                const float2 uv2t0 = (offsetUV2t0 + v.uv1.xy);
                const float2 uv3t0 = (offsetUV3t0 + v.uv1.xy);
                const float2 uv4t0 = (offsetUV4t0 + v.uv1.xy);
                //次のセグメントの時刻0のpos情報復元
                const float3 offsetPosition0t0 = tex2Dlod(_PositionTexture, float4(uv0t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition1t0 = tex2Dlod(_PositionTexture, float4(uv1t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition2t0 = tex2Dlod(_PositionTexture, float4(uv2t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition3t0 = tex2Dlod(_PositionTexture, float4(uv3t0, 0.0, 0.0)).rgb;
                const float3 offsetPosition4t0 = tex2Dlod(_PositionTexture, float4(uv4t0, 0.0, 0.0)).rgb;

                //フレーム補完
                const float3 position0 = lerp(offsetPosition0, offsetPosition0t0, incorp_rate);
                const float3 position1 = lerp(offsetPosition1, offsetPosition1t0, incorp_rate);
                const float3 position2 = lerp(offsetPosition2, offsetPosition2t0, incorp_rate);
                const float3 position3 = lerp(offsetPosition3, offsetPosition3t0, incorp_rate);
                const float3 position4 = lerp(offsetPosition4, offsetPosition4t0, incorp_rate);
                
                //アニメーションブレンド
                const float3 vertex01 = lerp(position0, position1, _Blend1);
                const float3 vertex12 = lerp(vertex01, position2, _Blend2);
                const float3 vertex23 = lerp(vertex12, position3, _Blend3);
                const float3 vertex34 = lerp(vertex23, position4, _Blend4);
                
                v.vertex.xyz = lerp(_BoundsMin.xyz, _BoundsMax.xyz, vertex34);
	            o.positionCS = TransformObjectToHClip(v.vertex.xyz);
	          
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