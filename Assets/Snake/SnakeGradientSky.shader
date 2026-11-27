// 渐变天空：给场景一个从深蓝到近黑的背景，不再是一块死板色
Shader "Snake/GradientSky"
{
    Properties
    {
        _TopColor    ("Top Color", Color) = (0.06, 0.09, 0.16, 1)
        _BottomColor ("Bottom Color", Color) = (0.01, 0.02, 0.04, 1)
        _Power       ("Power", Range(0.2, 4)) = 1.2
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Front
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _BottomColor;
            float _Power;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = saturate(i.dir.y * 0.5 + 0.5);
                t = pow(t, _Power);
                return lerp(_BottomColor, _TopColor, t);
            }
            ENDCG
        }
    }
    FallBack Off
}
