// 第一步：把蛇的轮廓写进模板缓冲（不输出颜色）
Shader "Snake/StencilMask"
{
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="Opaque" }

        Pass
        {
            ColorMask 0          // 不画颜色，只写模板
            ZWrite Off           // 不写深度，避免挡住半透明的身体
            ZTest Always         // 无视深度：身体画完就整条盖住，边缘不会漏
            Cull Off

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return fixed4(0,0,0,0); }
            ENDCG
        }
    }
    FallBack Off
}
