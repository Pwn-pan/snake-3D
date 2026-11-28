// 纯色 + 方向光 + 轮廓高光 + 可选花纹贴图
Shader "Snake/SoftShade"
{
    Properties
    {
        _Color      ("Color", Color) = (0.4, 0.7, 0.9, 1)
        _MainTex    ("Pattern", 2D) = "white" {}
        _PatternAmt ("Pattern Amount", Range(0,1)) = 0.0
        _Tiling     ("Pattern Tiling", Float) = 1
        _UseTex     ("Use Texture Directly", Range(0,1)) = 0
        _LightDir   ("Light Dir", Vector) = (0.45, 0.85, 0.35, 0)
        _Ambient    ("Ambient", Range(0,1)) = 0.42
        _Diffuse    ("Diffuse", Range(0,1)) = 0.58
        _Specular   ("Specular", Range(0,1)) = 0.07
        _Gloss      ("Gloss", Range(1,64)) = 8
        _RimPower   ("Rim Power", Range(0.5, 8)) = 3.0
        _RimAmount  ("Rim Amount", Range(0,1)) = 0.08
        _HeadLight  ("Head Light Color", Color) = (0.55, 1.0, 0.7, 1)
        _HeadRange  ("Head Light Range", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            sampler2D _MainTex;
            float _PatternAmt;
            float _Tiling;
            float _UseTex;
            float4 _LightDir;
            float _Ambient;
            float _Diffuse;
            float _Specular;
            float _Gloss;
            float _RimPower;
            float _RimAmount;
            fixed4 _HeadLight;
            float _HeadRange;
            float4 _HeadPos;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 wNormal: TEXCOORD0;
                float3 viewDir: TEXCOORD1;
                float3 wPos   : TEXCOORD2;
                float2 uv     : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv * _Tiling;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wNormal);
                float3 l = normalize(_LightDir.xyz);
                float ndl = saturate(dot(n, l));

                float shade = _Ambient + _Diffuse * ndl;

                float3 v = normalize(i.viewDir);
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), _Gloss) * _Specular;
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);

                // 花纹：贴图灰度调制本体颜色，六个面都有纹路
                float4 tex = tex2D(_MainTex, i.uv);
                float3 base = _Color.rgb;

                if (_UseTex > 0.5)
                {
                    // 直接贴图（脸、图案），保留明暗
                    base = tex.rgb;
                }
                else if (_PatternAmt > 0.001)
                {
                    base = lerp(base, base * (0.35 + tex.r * 1.3), _PatternAmt);
                }

                float3 col = base * shade + base * rim * _RimAmount + base * spec;

                // 头灯
                float3 toHead = _HeadPos.xyz - i.wPos;
                float dist = length(toHead);
                float att = saturate(1.0 - dist / max(0.001, _HeadRange));
                att = att * att;
                float3 headDir = normalize(toHead);
                float headNdl = saturate(dot(n, headDir));

                col *= (1.0 + att * headNdl * 0.55);
                col += base * _HeadLight.rgb * att * headNdl * 0.18;

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
