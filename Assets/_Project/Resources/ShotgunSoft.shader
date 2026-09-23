Shader "FPS/ShotgunSoft"
{
    Properties { _Intensity ("Intensity", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            float _Intensity;
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float edge = saturate(1 - dot(p,p));
                float wisps = 0.75 + 0.25 * sin(p.x * 17 + sin(p.y * 12) + _Time.y * 9);
                return half4(i.color.rgb * _Intensity, i.color.a * edge * edge * wisps);
            }
            ENDHLSL
        }
    }
}
