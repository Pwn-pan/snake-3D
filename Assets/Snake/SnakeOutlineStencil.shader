// 第二步：画外扩的描边，但只在模板里没被蛇占用的地方显示
Shader "Snake/OutlineStencil"
{
    Properties
    {
        _Color ("Outline Color", Color) = (0.02, 0.04, 0.07, 1)
    }

    SubShader
    {
        Tags { "Queue"="Geometry+2" "RenderType"="Opaque" }

        Pass
        {
            Cull Front          // 只画背面 → 形成外扩轮廓
            ZWrite Off
            ZTest LEqual

            Stencil
            {
                Ref 1
                Comp NotEqual   // 蛇身覆盖的地方不画
                Pass Keep
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
    FallBack Off
}
