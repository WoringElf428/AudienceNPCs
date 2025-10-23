Shader "Tarutaru/MeshAudience_URP (ParticleSystem)"
{ 
    Properties
    {
        [Header(AnimationTime)]
        [KeywordEnum(Auto, Manual)] _TimeUpdateMode("Time Update Mode", Float) = 0
        _ManualTime ("ManualTime", Float) = 0

        [Header(VAT_CommonAnimation)]
        _TotalFrame_C("Frame count", Float) = 240
        [NoScaleOffset] _PositionTexture_C("Position Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalTexture_C("Normal Texture", 2D) = "white" {}
        _BoundsMin_C ("Bounds Min", Vector) = (0,0,0,0)
        _BoundsMax_C ("Bounds Max", Vector) = (1,1,1,0)
        _Speed_C("Speed(Base 60BPM)", Float) = 1
        _RandomDelay("RandomDelay ", Range(0 , 1)) = 0
        //startFrame, endFrame, frameCount, merginFrame
        _Anime_C0 ("Common Anime A", Vector) = (0.0, 0.06,0.06, 0.025)
        _Anime_C1 ("Common Anime B", Vector) = (0.203125, 0.390625,0.1875, 0.015625)
        _Anime_C2 ("Common Anime C", Vector) = (0.40625, 0.59375,0.1875, 0.015625)
        _Anime_C3 ("Common Anime D", Vector) = (0.609375, 0.796875,0.1875, 0.015625)
        _Anime_C4 ("Common Anime E", Vector) = (0.8125, 1.0,0.1875, 0.015625)
        [Header(AnimationBlend)]
        _Blend_C1("Blend: Common1", Range(0 , 1)) = 0
        _Blend_C2("Blend: Common2", Range(0 , 1)) = 0
        _Blend_C3("Blend: Common3", Range(0 , 1)) = 0
        _Blend_C4("Blend: Common4", Range(0 , 1)) = 0

        [Header(VAT_UniqueAnimation)]
        _TotalFrame_U("Frame count", Float) = 2000
        _SequenceFrame("Anime Length", Float) = 120
        [NoScaleOffset] _PositionTextureUnique ("Position Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalTextureUnique("Normal Texture", 2D) = "white" {}
        _BoundsMin ("Bounds Min", Vector) = (0,0,0,0)
        _BoundsMax ("Bounds Max", Vector) = (1,1,1,0)
        _Speed("Speed(Base 60BPM)", Float) = 1
        _Interpolate("Interpolation monitor", Float) = 0
        _Transition("Transition", Range(0 , 1)) = 0
        _AnimeBlend("Blend Anime", Range(0 , 1)) = 0

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
        [MainColor] _BaseColor("Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
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

            // HSV->RGB変換
            inline float3 hsv2rgb(float3 hsv)
            {
                float3 rgb;

                if (hsv.y == 0){
                    // S（彩度）が0と等しいならば無色もしくは灰色
                    rgb.r = rgb.g = rgb.b = hsv.z;
                } else {
                    // 色環のH（色相）の位置とS（彩度）、V（明度）からRGB値を算出する
                    hsv.x *= 6.0;
                    float i = floor (hsv.x);
                    float f = hsv.x - i;
                    float aa = hsv.z * (1 - hsv.y);
                    float bb = hsv.z * (1 - (hsv.y * f));
                    float cc = hsv.z * (1 - (hsv.y * (1 - f)));
                    if( i < 1 ) {
                        rgb.r = hsv.z;
                        rgb.g = cc;
                        rgb.b = aa;
                    } else if( i < 2 ) {
                        rgb.r = bb;
                        rgb.g = hsv.z;
                        rgb.b = aa;
                    } else if( i < 3 ) {
                        rgb.r = aa;
                        rgb.g = hsv.z;
                        rgb.b = cc;
                    } else if( i < 4 ) {
                        rgb.r = aa;
                        rgb.g = bb;
                        rgb.b = hsv.z;
                    } else if( i < 5 ) {
                        rgb.r = cc;
                        rgb.g = aa;
                        rgb.b = hsv.z;
                    } else {
                        rgb.r = hsv.z;
                        rgb.g = aa;
                        rgb.b = bb;
                    }
                }
                return rgb;
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
            //#pragma enable_d3d11_debug_symbols
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            // 独自のインスタンシング用のデータ構造を定義する
            #define UNITY_PARTICLE_INSTANCE_DATA MyParticleInstanceData
            #define UNITY_PARTICLE_INSTANCE_DATA_NO_ANIM_FRAME
            struct MyParticleInstanceData
            {
                float3x4 transform;
                uint color;
                float4 motiondata;      //startPointA, delayA, startPointB, delayB
                float4 motiondata2;     //animeBlend, ペンライトの色彩(hue)、ペンライトの明るさ(value)
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
#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                float4 motiondata : TEXCOORD3;      //StartPos1, Delay1, StartPos2, Delay2
                float4 motiondata2 : TEXCOORD4;     //Transition, PLcol_Hue, PLcol_Intensity
#endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color1 : COLOR;
                float2 uv0 : TEXCOORD0;
                float3 viewNormal : TEXCOORD1;
                float3 fogParam : TEXCOORD2;
                float4 customColor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform float _ManualTime;

            //Unique Anime
            uniform float _TotalFrame_U;
            uniform float _SequenceFrame;
            //uniform float _Interpolate;
            uniform float _Transition;  //interpolateを変える
            uniform sampler2D _PositionTextureUnique;
            uniform sampler2D _NormalTextureUnique;
            uniform float4 _PositionTextureUnique_TexelSize;
            uniform float _Speed;
            uniform float4 _BoundsMin;
            uniform float4 _BoundsMax;
            uniform float2 _UniqueAnime1;
            uniform float2 _UniqueAnime2;

            uniform half4 _Anime_C0;

            //uniform float _RandomDelay;
            uniform float _AnimeBlend;

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
                float t = _ManualTime * _Speed;
#else
    #if defined(APPLY_JUMP_DOUBLE_SPEED)
                float speed = 1.0;
                speed = lerp(1.0, 2.0, step(0.999, 1));
                float t = _Time.y * _Speed * speed;
    #else
                float t = _Time.y * _Speed;
    #endif
#endif

#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];

                //t += UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random0).x;
                //o.penLightColor = UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random1);
                _UniqueAnime1 = float2(data.motiondata.x, data.motiondata.y);
                _UniqueAnime2 = float2(data.motiondata.z, data.motiondata.w);
                //_Transition = data.motiondata2.x;
                float3 rgb = hsv2rgb(float3(data.motiondata2.y, 1.0, data.motiondata2.z));
                o.customColor = float4(rgb.xyz, 1.0);
#endif                
                //スタイル一貫性の維持
                //float incorp_rate = saturate((frac(t) - _Interpolate) / (1.0 - _Interpolate)) * (1.01 - step(1.0, frac(t)));

                const float t_nowStart = frac(t + _UniqueAnime1.y);
                const float t_nextStart = frac(t + _UniqueAnime2.y);
                
                //待機サイクル
                const float t0 = remap(t_nowStart, 0.0, 1.0, _Anime_C0.x, _Anime_C0.y);
                
                //固有モーション
                float endFramePos = (_SequenceFrame - 1) / _TotalFrame_U;
                const float t1 = remap(t_nowStart, 0.0, 1.0, _UniqueAnime1.x, _UniqueAnime1.x + endFramePos);
                const float t2 = remap(t_nextStart, 0.0, 1.0, _UniqueAnime2.x, _UniqueAnime2.x + endFramePos);
                
                //フレーム情報を復元するためのUV情報
                const float fameCount = _TotalFrame_U;
                const float2 offsetUVbase = float2((_PositionTextureUnique_TexelSize.x * fameCount * t0), 0.0);
                const float2 offsetUVcurrent = float2((_PositionTextureUnique_TexelSize.x * fameCount * t1), 0.0);
                const float2 offsetUVnext = float2((_PositionTextureUnique_TexelSize.x * fameCount * t2), 0.0);
                const float2 uvBase = (offsetUVbase + v.uv1.xy);
                const float2 uvCurrent = (offsetUVcurrent + v.uv1.xy);
                const float2 uvNext = (offsetUVnext + v.uv1.xy);

                //位置
                const float3 posBase = tex2Dlod(_PositionTextureUnique, float4(uvBase, 0.0, 0.0)).rgb;
                const float3 posCurrent = tex2Dlod(_PositionTextureUnique, float4(uvCurrent, 0.0, 0.0)).rgb;
                const float3 posNext = tex2Dlod(_PositionTextureUnique, float4(uvNext, 0.0, 0.0)).rgb;
                const float3 vertex01 = lerp(posCurrent, posNext, _Transition);
                const float3 vertex12 = lerp(vertex01, posBase, _AnimeBlend);
                
                float3 rawPos = lerp(_BoundsMin.xyz, _BoundsMax.xyz, vertex12);
                //x軸に90度回転
                //float3 rotPos   = float3(rawPos.x, rawPos.z, -rawPos.y);
                //v.vertex.xyz = mul(unity_ObjectToWorld, float4(rotPos,1));
                v.vertex.xyz = rawPos;
                
                //法線
                const float3 norBase = tex2Dlod(_NormalTextureUnique, float4(uvBase, 0.0, 0.0)).rgb;
                const float3 norCurrent = tex2Dlod(_NormalTextureUnique, float4(uvCurrent, 0.0, 0.0)).rgb;
                const float3 norNext = tex2Dlod(_NormalTextureUnique, float4(uvNext, 0.0, 0.0)).rgb;        
                const float3 normal01 = lerp(norCurrent, norNext, _Transition);
                const float3 normal12 = lerp(normal01, norBase, _AnimeBlend);
                v.normal.xyz = normalize(normal12 * 2.0 - 1.0);

                o.pos = TransformObjectToHClip(v.vertex);
                o.color1 = v.color;

#if defined(_FOGMODE_USER)
                o.fogParam.x = -mul(UNITY_MATRIX_MV, v.vertex).z; //UNITY_MATRIX_MV使えない
                //o.fogParam.x = -mul(GetWorldToViewMatrix(), float4(v.vertex.xyz, 1.0)).z;
                o.fogParam.y = v.vertex.y;
#endif

#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                //vertInstancingColor(o.color1);
                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, o.color1);
                UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, o.customColor);
#endif
                o.uv0 = v.uv0;

#if defined(APPLY_MATCAP)
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
                //頂点カラーが0のところのみにTintColorを適用
                float3 c1 = lerp(c0.rgb * _BaseColor, float3(0.0, 0.0, 0.0), i.color1.r);
                
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
                c1 += i.color1.rgb * i.customColor * c0.r;//
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
                float4 motiondata;
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
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
#if !defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                float4 motiondata : TEXCOORD2;
#endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS  : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            uniform float _ManualTime;

            uniform float _TotalFrame_U;
            uniform sampler2D _PositionTextureUnique;
            uniform float4 _PositionTextureUnique_TexelSize;
            uniform float _Speed;
            uniform float _Transition;
            uniform float _SequenceFrame;
            uniform float4 _BoundsMin;
            uniform float4 _BoundsMax;
            uniform float2 _UniqueAnime1;
            uniform float2 _UniqueAnime2;
            uniform half4 _Anime_C0;
            uniform float _Interpolate;
            //uniform float _RandomDelay;
            uniform float _AnimeBlend;
            
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

#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
                _UniqueAnime1 = float2(data.motiondata.x, data.motiondata.y);
                _UniqueAnime2 = float2(data.motiondata.z, data.motiondata.w);
                //_Transition = data.motiondata2.x;
                //t += UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random0).x;
                //o.penLightColor = UNITY_ACCESS_INSTANCED_PROP(unity_ParticleInstanceData, random1);
#else
                _UniqueAnime1.y = 0.0;
                _UniqueAnime2.y = 0.0;
#endif
                //スタイル一貫性の維持
                //float incorp_rate = saturate((frac(t) - 1 + _Interpolate) / _Interpolate) * step(1.0 - _Interpolate, frac(t));

                const float t_nowStart = frac(t + _UniqueAnime1.y);
                const float t_nextStart = frac(t + _UniqueAnime2.y);
                
                //待機サイクル
                const float t0 = remap(t_nowStart, 0.0, 1.0, _Anime_C0.x, _Anime_C0.y);
                
                //固有モーション
                float endFramePos = (_SequenceFrame - 1) / _TotalFrame_U;
                const float t1 = remap(t_nowStart, 0.0, 1.0, _UniqueAnime1.x, _UniqueAnime1.x + endFramePos);
                const float t2 = remap(t_nextStart, 0.0, 1.0, _UniqueAnime2.x, _UniqueAnime2.x + endFramePos);
                
                //フレーム情報を復元するためのUV情報
                const float fameCount = _TotalFrame_U;
                const float2 offsetUVbase = float2((_PositionTextureUnique_TexelSize.x * fameCount * t0), 0.0);
                const float2 offsetUVcurrent = float2((_PositionTextureUnique_TexelSize.x * fameCount * t1), 0.0);
                const float2 offsetUVnext = float2((_PositionTextureUnique_TexelSize.x * fameCount * t2), 0.0);
                const float2 uvBase = (offsetUVbase + v.uv1.xy);
                const float2 uvCurrent = (offsetUVcurrent + v.uv1.xy);
                const float2 uvNext = (offsetUVnext + v.uv1.xy);

                //位置情報
                const float3 posBase = tex2Dlod(_PositionTextureUnique, float4(uvBase, 0.0, 0.0)).rgb;
                const float3 posCurrent = tex2Dlod(_PositionTextureUnique, float4(uvCurrent, 0.0, 0.0)).rgb;
                const float3 posNext = tex2Dlod(_PositionTextureUnique, float4(uvNext, 0.0, 0.0)).rgb;
                const float3 vertex01 = lerp(posCurrent, posNext, _Transition);
                const float3 vertex12 = lerp(vertex01, posBase, _AnimeBlend);
                
                float3 rawPos = lerp(_BoundsMin.xyz, _BoundsMax.xyz, vertex12);
                //x軸に90度回転
                //float3 rotPos   = float3(rawPos.x, rawPos.z, -rawPos.y);
                //v.vertex.xyz = mul(unity_ObjectToWorld, float4(rotPos,1));
                v.vertex.xyz = rawPos;

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