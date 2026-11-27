// 纯色 + 方向光 + 轮廓高光
// 用世界法线着色：相邻方块朝向相同的面得到相同的颜色，因此块与块之间不会出现明暗断层
Shader "Snake/SoftShade"
{
    Properties
    {
        _Color      ("Color", Color) = (0.4, 0.7, 0.9, 1)
        _LightDir   ("Light Dir", Vector) = (0.45, 0.85, 0.35, 0)
        _Ambient    ("Ambient", Range(0,1)) = 0.42
        _Diffuse    ("Diffuse", Range(0,1)) = 0.58
        _Specular   ("Specular", Range(0,1)) = 0.07
        _Gloss      ("Gloss", Range(1,64)) = 8
        _RimPower   ("Rim Power", Range(0.5, 8)) = 3.0
        _RimAmount  ("Rim Amount", Range(0,1)) = 0.08
        _HeadLight  ("Head Light Color", Color) = (0.55, 1.0, 0.7, 1)
        _HeadRange  ("Head Light Range", Float) = 7
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
            float4 _LightDir;
            float _Ambient;
            float _Diffuse;
            float _Specular;
            float _Gloss;
            float _RimPower;
            float _RimAmount;
            fixed4 _HeadLight;
            float _HeadRange;
            float4 _HeadPos;      // 全局：蛇头世界坐标

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 wNormal: TEXCOORD0;
                float3 viewDir: TEXCOORD1;
                float3 wPos   : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wNormal);
                float3 l = normalize(_LightDir.xyz);
                float ndl = saturate(dot(n, l));

                float shade = _Ambient + _Diffuse * ndl;

                // 高光：同一朝向的面颜色一致，块之间不会断层
                float3 v = normalize(i.viewDir);
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), _Gloss) * _Specular;

                // 轮廓提亮
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);

                // 蛇头点光源：距离衰减
                float3 toHead = _HeadPos.xyz - i.wPos;
                float dist = length(toHead);
                float att = saturate(1.0 - dist / max(0.001, _HeadRange));
                att = att * att;
                float3 headDir = normalize(toHead);
                float headNdl = saturate(dot(n, headDir));

                // 高光只提亮本体颜色，不掺白
                float3 col = _Color.rgb * shade
                           + _Color.rgb * rim * _RimAmount
                           + _Color.rgb * spec;

                // 头灯：乘法提亮（颜色只会变亮变暖，不会变成白色）
                col *= (1.0 + att * headNdl * 0.55);
                col += _Color.rgb * _HeadLight.rgb * att * headNdl * 0.18;

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
