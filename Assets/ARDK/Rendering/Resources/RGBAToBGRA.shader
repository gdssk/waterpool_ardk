Shader "Unlit/RGBAToBGRA"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4x4 _textureTransform;

            struct Vertex
            {
                float4 position : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct TexCoordInOut
            {
                float4 position : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            TexCoordInOut vert (Vertex vertex)
            {
                TexCoordInOut o;
                o.position = UnityObjectToClipPos(vertex.position);

                o.texcoord =  vertex.texcoord;
                return o;
            }

            // samplers
            sampler2D _MainTex;

            fixed4 frag (TexCoordInOut i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.texcoord).argb;
				return fixed4(color.b, color.g, color.r, color.a);
            }
            ENDCG
        }
    }
}
